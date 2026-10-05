<#
.SYNOPSIS
  The whole path in one run: a dead desktop -> Steam starts the game -> the opening cut scene is skipped ->
  the title screen -> 继续 (continue) -> the day scene, with a screenshot of every state.

.DESCRIPTION
  Every action goes through GameDriver.ps1, so every step resolves the game window afresh and never reuses a
  handle or a coordinate from an earlier one. Each step is judged by evidence rather than by a fixed wait,
  and the evidence is the game's own log (Player.log), which `start` deletes first, so it only ever describes
  this launch:

    * the title screen  = "ScenMana: Game Enter Main Scene"
    * the day scene     = "ScenMana: Game Enter Day Scene"
    * the newspaper the game opens at day start = "Open Panel: NoteBook_MainPannel"

  继续 is the one place a coordinate is written down. It is a fraction of the window (measured on the
  2560x1440 window: centre 186,1352), so the same step works at 720p, and the step is confirmed by the
  screenshot it saves plus the day scene wait that follows.

  The newspaper is closed with K - the key the panel's own corner hint shows - and only when Player.log says
  it is open. The game is left running on purpose: the caller keeps driving from the day scene. Stop it with
  `GameDriver.ps1 kill`.
#>
param(
  [string]$Shots = (Join-Path $PSScriptRoot 'shots'),
  [int]$TitleTimeoutSeconds = 180,
  [int]$DayTimeoutSeconds = 150
)

$ErrorActionPreference = 'Stop'

$driver = Join-Path $PSScriptRoot 'GameDriver.ps1'
$launcher = if ($env:MEFX_LAUNCHER) { $env:MEFX_LAUNCHER }
            else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../MystiaExtensionFramework/artifacts/launcher')) }
$hostLog = Join-Path $launcher 'host.log'
$playerLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\Epicomic\Touhou Mystia Izakaya\Player.log'

New-Item -ItemType Directory -Force -Path $Shots | Out-Null

function Step([string]$text) { Write-Output ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $text) }

function Read-Log([string]$path) {
  # Get-Content, not ReadAllText: the game keeps Player.log open while it writes, and ReadAllText refuses a
  # file opened that way. UTF8 decoding both here and for the file as a whole keeps the text consistent.
  if (-not (Test-Path $path)) { return '' }
  $text = Get-Content -Path $path -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
  if ($null -eq $text) { return '' }
  return $text
}

function Wait-For([string]$path, [string]$pattern, [int]$seconds, [string]$what) {
  $deadline = (Get-Date).AddSeconds($seconds)
  while ((Get-Date) -lt $deadline) {
    if ((Read-Log $path) -match $pattern) { return }
    Start-Sleep -Milliseconds 700
  }
  throw "waited ${seconds}s for $what and did not see it in $path"
}

Step 'stopping any running game'
& $driver kill

Step 'starting through the Steam client (proxy installed, mods mounted by the activation argument)'
& $driver start

Step 'skipping the opening cut scene'
& $driver skipintro -TimeoutSeconds 150

Step 'waiting for the title screen'
Wait-For $playerLog 'Game Enter Main Scene' $TitleTimeoutSeconds 'the main scene'
Start-Sleep -Seconds 3
& $driver shot -Out (Join-Path $Shots '01-title.png')

Step 'clicking 继续 on the title screen'
& $driver click -Fx 0.07266 -Fy 0.93889 -Out (Join-Path $Shots '02-continue-clicked.png') -SettleMs 2000

Step 'waiting for the day scene'
Wait-For $playerLog 'Game Enter Day Scene' $DayTimeoutSeconds 'the day scene'
Start-Sleep -Seconds 5
& $driver shot -Out (Join-Path $Shots '03-day-notebook.png')

if ((Read-Log $playerLog).LastIndexOf('Open Panel: NoteBook_MainPannel') -gt
    (Read-Log $playerLog).LastIndexOf('Close Panel: NoteBook_MainPannel')) {
  Step 'closing the newspaper the game opens at day start (K)'
  & $driver key -Key k -Out (Join-Path $Shots '04-day.png') -SettleMs 2500
} else {
  Step 'no newspaper open, taking the day scene screenshot'
  & $driver shot -Out (Join-Path $Shots '04-day.png')
}

Step 'what the mod logged in this launch (host.log tail)'
Get-Content -Path $hostLog -Tail 400 -Encoding UTF8 -ErrorAction SilentlyContinue |
  Select-String -Pattern 'seams:|Active DLC keys|DaySync|EventNodeRegistry|SchedulerDataRecovery|LocalPlayer' |
  Select-Object -Last 8 |
  ForEach-Object { Write-Output ("  " + $_.Line) }

Step 'done - the game is in the day scene and stays running'
& $driver where

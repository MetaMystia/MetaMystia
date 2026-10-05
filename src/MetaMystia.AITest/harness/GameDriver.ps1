<#
.SYNOPSIS
  Drives the game the way a player does: the Steam client starts it, and every other action is an
  OS-level input event sent by the computer-use CLI and verified by a screenshot of the game window.

.DESCRIPTION
  One action per call. The game window is resolved afresh in every call - it runs 720p windowed, so its
  bounds move, and the desktop is shared - and nothing reuses a handle, a coordinate or a foreground
  assumption from an earlier call.

  Actions:
    start      install the proxy through the launcher and let the Steam client start the game
    kill       stop the game process
    where      print the process and the window the CLI sees
    shot       save a screenshot of the game window (focused) to -Out
    click      click the game window at screenshot pixel -X -Y (or -Fx -Fy, fractions of the window),
               then screenshot to -Out if given
    key        tap -Key into the game window (-HoldMs > 0 holds it that long instead, for walking), then
               screenshot to -Out if given
    skipintro  tap space in a bounded loop until the opening cut scene is over

  Coordinates: `screenshot --hwnd` captures the window rect and `mouse click --coord client` addresses
  the client area, so -X/-Y are pixels of such a screenshot and the chrome offset between the two is
  measured on the same handle inside the same call, right before the click.

  The old harness sent `keybd_event` to a fullscreen window and its keys never landed; every input here
  goes through computer-use, which resolves the window itself and never guesses an absolute point.
#>
param(
  [Parameter(Mandatory = $true)][ValidateSet('start', 'kill', 'where', 'shot', 'click', 'key', 'skipintro')][string]$Action,
  [string]$Out = '',
  [int]$X = -1,
  [int]$Y = -1,
  [double]$Fx = -1,
  [double]$Fy = -1,
  [string]$Key = 'space',
  [int]$HoldMs = 0,
  [int]$TimeoutSeconds = 180,
  [int]$SettleMs = 700,
  [string]$Launcher = ''
)

$ErrorActionPreference = 'Stop'

$windowTitle = '*Touhou Mystia Izakaya*'
$processName = 'Touhou Mystia Izakaya'
$launcher = if ($Launcher) { $Launcher }
             elseif ($env:MEFX_LAUNCHER) { $env:MEFX_LAUNCHER }
             else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../MystiaExtensionFramework/artifacts/launcher')) }
$playerLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\Epicomic\Touhou Mystia Izakaya\Player.log'

# Screenshot pixels are window-rect pixels, clicks are client-rect pixels; this is how the two are related.
Add-Type -Namespace Driver -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
public struct POINT { public int X; public int Y; }
'@

$computerUse = Get-Command computer-use -ErrorAction SilentlyContinue
if ($null -eq $computerUse) { throw 'computer-use is not on PATH' }
$cu = $computerUse.Source

function Invoke-Cu {
  param([string[]]$Arguments, [switch]$Soft)
  $output = & $cu @Arguments 2>&1 | Out-String
  $code = $LASTEXITCODE
  $text = $output.Trim()
  if ($code -ne 0) {
    if ($Soft) { return $null }
    throw "computer-use $($Arguments -join ' ') failed with $code`: $text"
  }
  $json = $text | ConvertFrom-Json
  if (-not $json.ok) {
    if ($Soft) { return $null }
    throw "computer-use $($Arguments -join ' ') answered ok=false: $text"
  }
  return $json
}

function ConvertTo-Handle([string]$text) {
  if ($text -like '0x*') { return [IntPtr]([Convert]::ToInt64($text.Substring(2), 16)) }
  return [IntPtr]([Convert]::ToInt64($text))
}

function Get-GameWindow {
  # Resolved freshly on every call; $null when the game has no window yet.
  $found = Invoke-Cu @('window', 'find', '--title', $windowTitle, '--process', $processName) -Soft
  if ($null -eq $found) { $found = Invoke-Cu @('window', 'find', '--title', $windowTitle) -Soft }
  if ($null -eq $found) { return $null }
  return $found.windows[0]
}

function Get-ChromeOffset([string]$hwnd) {
  $handle = ConvertTo-Handle $hwnd
  $window = New-Object Driver.Native+RECT
  $client = New-Object Driver.Native+POINT
  if (-not [Driver.Native]::GetWindowRect($handle, [ref]$window)) { throw "GetWindowRect failed for $hwnd" }
  [Driver.Native]::ClientToScreen($handle, [ref]$client) | Out-Null
  return [pscustomobject]@{
    Left = $client.X - $window.Left
    Top = $client.Y - $window.Top
    WindowWidth = $window.Right - $window.Left
    WindowHeight = $window.Bottom - $window.Top
  }
}

function Save-Screenshot {
  param([string]$hwnd, [string]$path)
  if ([string]::IsNullOrWhiteSpace($path)) { throw 'this action needs -Out <png>' }
  $full = [IO.Path]::GetFullPath($path)
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $full) | Out-Null
  $shot = Invoke-Cu @('screenshot', '--path', $full, '--hwnd', $hwnd, '--focus')
  Write-Output ("shot  {0}  {1}x{2}  region={3},{4}" -f $shot.screenshot.path, $shot.screenshot.width, $shot.screenshot.height, $shot.screenshot.region.x, $shot.screenshot.region.y)
}

function Send-Key {
  param([string]$hwnd, [string]$key)
  # key tap focuses the window by default when --hwnd is given.
  Invoke-Cu @('key', 'tap', '--hwnd', $hwnd, '--key', $key) | Out-Null
}

switch ($Action) {
  'start' {
    # The launcher installs the proxy and starts the game through the Steam client with the activation
    # argument; the proxy stays inert without it, and the DRM is never touched.
    $installer = Join-Path $launcher 'Mystia.Syringe.exe'
    if (-not (Test-Path $installer)) { throw "no launcher found: $installer" }
    # The opening cut scene decision reads Player.log, which Unity appends to across launches: a log left
    # over from the last run makes skipintro finish on yesterday's lines. Every launch starts from none.
    Remove-Item -Force -ErrorAction SilentlyContinue $playerLog
    & $installer --force
    if ($LASTEXITCODE -ne 0) { throw "the launcher refused to start the game ($LASTEXITCODE)" }
    Write-Output 'started through the Steam client'
  }
  'kill' {
    Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 3
    $remaining = (Get-Process -Name $processName -ErrorAction SilentlyContinue | Measure-Object).Count
    Write-Output "remaining=$remaining"
  }
  'where' {
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
      ForEach-Object { Write-Output ("pid={0} start={1}" -f $_.Id, $_.StartTime) }
    $window = Get-GameWindow
    if ($null -eq $window) { Write-Output 'window=none'; return }
    Write-Output ("window={0} title='{1}' bounds={2},{3} {4}x{5} foreground={6}" -f `
      $window.hwnd, $window.title, $window.bounds.x, $window.bounds.y, $window.bounds.width, $window.bounds.height, $window.foreground)
  }
  'shot' {
    $window = Get-GameWindow
    if ($null -eq $window) { throw 'the game has no window' }
    Save-Screenshot -hwnd $window.hwnd -path $Out
  }
  'click' {
    # One chain: resolve the window, measure the chrome offset, click, verify.
    $window = Get-GameWindow
    if ($null -eq $window) { throw 'the game has no window' }
    if ($Fx -ge 0 -and $Fy -ge 0) {
      # A target given as a fraction of the window: the same button of a 720p window and of a 1440p one.
      $X = [int][Math]::Round($Fx * $window.bounds.width)
      $Y = [int][Math]::Round($Fy * $window.bounds.height)
    }
    if ($X -lt 0 -or $Y -lt 0) { throw 'click needs -X -Y (screenshot pixels) or -Fx -Fy (fractions of the window)' }
    $offset = Get-ChromeOffset $window.hwnd
    $clientX = $X - $offset.Left
    $clientY = $Y - $offset.Top
    Invoke-Cu @('mouse', 'click', '--hwnd', $window.hwnd, '--coord', 'client', '--x', "$clientX", '--y', "$clientY", '--focus') | Out-Null
    Write-Output ("click screenshot({0},{1}) -> client({2},{3})  hwnd={4} bounds={5},{6} {7}x{8} chrome={9},{10}" -f `
      $X, $Y, $clientX, $clientY, $window.hwnd, $window.bounds.x, $window.bounds.y, $window.bounds.width, $window.bounds.height, $offset.Left, $offset.Top)
    Start-Sleep -Milliseconds $SettleMs
    if (-not [string]::IsNullOrWhiteSpace($Out)) { Save-Screenshot -hwnd $window.hwnd -path $Out }
  }
  'key' {
    $window = Get-GameWindow
    if ($null -eq $window) { throw 'the game has no window' }
    if ($HoldMs -gt 0) {
      # Walking needs a hold: key down/up go to the foreground window, so the game is put in front first.
      Invoke-Cu @('window', 'focus', '--hwnd', $window.hwnd) | Out-Null
      Invoke-Cu @('key', 'down', '--key', $Key) | Out-Null
      Start-Sleep -Milliseconds $HoldMs
      Invoke-Cu @('key', 'up', '--key', $Key) | Out-Null
      Write-Output ("key {0} held {1}ms -> hwnd={2} bounds={3},{4} {5}x{6}" -f $Key, $HoldMs, $window.hwnd, $window.bounds.x, $window.bounds.y, $window.bounds.width, $window.bounds.height)
    } else {
      Send-Key -hwnd $window.hwnd -key $Key
      Write-Output ("key {0} -> hwnd={1} bounds={2},{3} {4}x{5}" -f $Key, $window.hwnd, $window.bounds.x, $window.bounds.y, $window.bounds.width, $window.bounds.height)
    }
    Start-Sleep -Milliseconds $SettleMs
    if (-not [string]::IsNullOrWhiteSpace($Out)) { Save-Screenshot -hwnd $window.hwnd -path $Out }
  }
  'skipintro' {
    # The opening cut scene advances on any key. Taps stop as soon as the log of *this* launch says the
    # load scene is running, so they never reach the title menu behind it; the slice from the last
    # "Initialize engine version" is what keeps yesterday's line out of that decision.
    #
    # Steam can take a minute to hand the game over (it syncs cloud archives first), during which there is
    # no window at all: that wait is not a failure, so it does not count as a miss.
    $windowDeadline = (Get-Date).AddSeconds([Math]::Min(90, $TimeoutSeconds))
    while ((Get-Date) -lt $windowDeadline -and $null -eq (Get-GameWindow)) {
      Start-Sleep -Milliseconds 700
    }
    if ($null -eq (Get-GameWindow)) {
      Write-Output 'no game window appeared, nothing to skip'
      return
    }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $taps = 0
    $misses = 0
    while ((Get-Date) -lt $deadline) {
      $text = if (Test-Path $playerLog) { Get-Content $playerLog -Raw -ErrorAction SilentlyContinue } else { '' }
      $start = if ($text) { $text.LastIndexOf('Initialize engine version') } else { -1 }
      if ($start -ge 0) { $text = $text.Substring($start) }
      if ($text -match 'Enter Load Scene|load Main Menu|Game Enter Main Scene|Load Save Data') {
        Write-Output "intro over after $taps taps"
        break
      }
      $window = Get-GameWindow
      if ($null -eq $window) {
        $misses++
        if ($misses -ge 8) { Write-Output "no game window for 8 checks, taps=$taps"; break }
        Start-Sleep -Milliseconds 700
        continue
      }
      $misses = 0
      Send-Key -hwnd $window.hwnd -key 'space'
      $taps++
      Start-Sleep -Milliseconds 1000
    }
    Write-Output "skipintro done, taps=$taps"
  }
}

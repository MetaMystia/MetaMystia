# 构建一个特效集合：生成贴图 -> Unity 批处理生成 prefab 与 AssetBundle -> 可选粒子检查。
#
# 用法：pwsh scripts/build.ps1 -Spell Mai [-SkipTextures] [-Probe] [-Unity <Unity.exe>]
# Unity 版本须与游戏一致（2021.3.28f1）；未传 -Unity 时读取环境变量 UNITY_EXE。
# 贴图生成脚本约定为 scripts/<spell 小写>/gen_textures.py。

param(
    [Parameter(Mandatory)][string]$Spell,
    [switch]$SkipTextures,
    [switch]$Probe,
    [string]$Unity = $env:UNITY_EXE
)

$ErrorActionPreference = 'Stop'
if (-not $Unity) { throw '未指定 Unity：传入 -Unity 或设置环境变量 UNITY_EXE' }

$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $root 'Logs') | Out-Null

function Invoke-Unity([string]$method, [string]$log) {
    # 经管道输出可让 PowerShell 等待 Unity 进程结束
    & $Unity -batchmode -quit -nographics -projectPath $root -executeMethod $method -spell $Spell -logFile $log | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unity 退出码 $LASTEXITCODE，见 $log" }
}

if (-not $SkipTextures) {
    $gen = Join-Path $PSScriptRoot "$($Spell.ToLower())/gen_textures.py"
    Write-Host "== 生成贴图 $gen"
    python $gen
    if ($LASTEXITCODE -ne 0) { throw '贴图生成失败' }
}

$buildLog = Join-Path $root 'Logs/build.log'
Write-Host '== 构建 AssetBundle'
Invoke-Unity 'SpellBundleBuilder.BuildFromBatch' $buildLog
$written = Select-String -Path $buildLog -Pattern '\[SpellVfx\] bundle written: (.+)' | Select-Object -Last 1
if (-not $written) { throw "构建失败，见 $buildLog" }
Write-Host "== $($written.Matches[0].Groups[1].Value)"

if ($Probe) {
    $probeLog = Join-Path $root 'Logs/probe.log'
    Write-Host '== 粒子检查'
    Invoke-Unity 'ParticleProbe.Run' $probeLog
    Select-String -Path $probeLog -Pattern '^PROBE ' | ForEach-Object { $_.Line }
}

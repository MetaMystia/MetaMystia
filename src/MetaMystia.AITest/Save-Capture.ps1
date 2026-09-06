param([Parameter(Mandatory)][string]$Token)

$response = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:18765/script' -Headers @{ 'X-Debug-Token' = $Token } -ContentType 'text/plain' -Body 'AITestCapture.Png' -TimeoutSec 30
if (!$response.Success) { throw ($response | ConvertTo-Json -Depth 8) }
if (!$response.Result) { throw '截图尚未完成' }
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'capture.png'), [Convert]::FromBase64String($response.Result))
'Saved capture.png'

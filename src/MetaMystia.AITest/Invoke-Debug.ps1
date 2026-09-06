param(
    [Parameter(Mandatory)][string]$Token,
    [string]$File,
    [string]$Code,
    [ValidateSet('exec', 'script')][string]$Endpoint = 'exec'
)

[byte[]]$body = if ($File) { [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $File).Path) } else { [System.Text.Encoding]::UTF8.GetBytes($Code) }
$response = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:18765/$Endpoint" -Headers @{ 'X-Debug-Token' = $Token } -ContentType 'text/plain; charset=utf-8' -Body $body -TimeoutSec 30
if (!$response.Success) { throw ($response | ConvertTo-Json -Depth 8) }
$response.Output
$response.Result

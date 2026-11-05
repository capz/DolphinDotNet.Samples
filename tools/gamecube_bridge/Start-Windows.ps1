[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $env:LOCALAPPDATA 'DolphinDotNet\HardwareBridge\config.json'),
    [string]$TunnelId
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ConfigPath)) { throw 'Run Setup-Windows.ps1 first.' }
$python = (Get-Command py.exe -ErrorAction Stop).Source
& $python -3 -c 'import sys; assert sys.version_info >= (3,10), "Python 3.10+ required"'
if ($LASTEXITCODE -ne 0) { throw 'Install Python 3.10 or later with its Windows py launcher.' }
$service = Join-Path $PSScriptRoot 'service.py'
$logRoot = Split-Path -Parent $ConfigPath
$arguments = @('-3', ('"{0}"' -f $service), '--config', ('"{0}"' -f $ConfigPath))
$process = Start-Process -FilePath $python -ArgumentList $arguments -PassThru -NoNewWindow -RedirectStandardOutput (Join-Path $logRoot 'bridge.stdout.log') -RedirectStandardError (Join-Path $logRoot 'bridge.stderr.log')
try {
    Start-Sleep -Seconds 1
    if ($process.HasExited) { throw 'Bridge exited; see bridge.stderr.log in the configuration directory.' }
    if ($TunnelId) {
        $tunnel = (Get-Command devtunnel -ErrorAction Stop).Source
        # Existing private tunnel: OAuth sign-in/provisioning stays on Windows.
        & $tunnel host $TunnelId
        if ($LASTEXITCODE -ne 0) { throw 'Dev tunnel host exited with an error.' }
    } else {
        Write-Host 'Bridge is running locally. Host the private dev tunnel in a second window, or stop with Ctrl+C.'
        Wait-Process -Id $process.Id
    }
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}

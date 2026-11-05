[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$TunnelId)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'DolphinDotNet\HardwareBridge'
if (-not (Test-Path -LiteralPath (Join-Path $root 'config.json'))) { throw 'Run Setup-Windows.ps1 first.' }
# The token is captured directly to the protected directory, never printed.
$output = (& devtunnel token $TunnelId --scopes connect | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot issue a connect token. Sign in to devtunnel as the tunnel owner.' }
$tokenMatches = [regex]::Matches($output, '[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+')
if ($tokenMatches.Count -ne 1) { throw 'Unexpected CLI token output; no credential file was written. Check devtunnel token --help.' }
$path = Join-Path $root 'tunnel-connect-token.txt'
[System.IO.File]::WriteAllText($path, $tokenMatches[0].Value, [System.Text.Encoding]::ASCII)
Write-Host "Connect token saved to $path. Configure it as DND_TUNNEL_ACCESS_TOKEN in Codex Cloud secrets."

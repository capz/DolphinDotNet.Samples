[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][System.Net.IPAddress]$ConsoleAddress,
    [int]$ApiPort = 6280,
    [int]$CallbackPort = 6281,
    [switch]$ConfigureFirewall
)
$ErrorActionPreference = 'Stop'
if ($ConsoleAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { throw 'Use the GameCube LAN IPv4 address shown by the resident launcher.' }
if ($ApiPort -lt 1 -or $ApiPort -gt 65535 -or $CallbackPort -lt 1 -or $CallbackPort -gt 65535) { throw 'Invalid port.' }
$root = Join-Path $env:LOCALAPPDATA 'DolphinDotNet\HardwareBridge'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
& icacls.exe $root /inheritance:r /grant:r "*${sid}:(OI)(CI)F" '*S-1-5-18:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the bridge configuration directory.' }
$tokenPath = Join-Path $root 'api-token.txt'
if (-not (Test-Path -LiteralPath $tokenPath)) {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $token = [System.BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    [System.IO.File]::WriteAllText($tokenPath, $token, [System.Text.Encoding]::ASCII)
}
$configPath = Join-Path $root 'config.json'
if (Test-Path -LiteralPath $configPath) { throw "Configuration already exists at $configPath. Review/edit it instead of overwriting it." }
$settings = [ordered]@{
    target_kind = 'gamecube'; console_address = $ConsoleAddress.ToString(); launcher_port = 4299
    api_port = $ApiPort; callback_port = $CallbackPort; api_token_file = 'api-token.txt'
    capture_timeout = 120; ready_timeout = 30; artifacts = 'results'
}
[System.IO.File]::WriteAllText($configPath, ($settings | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
if ($ConfigureFirewall) {
    $admin = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $admin.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run with elevation for -ConfigureFirewall, or add the documented private-network callback rule separately.' }
    $name = 'DolphinDotNet-HardwareBridge-Callback'
    if (Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue) { throw 'The bridge firewall rule already exists; review it before changing the console address.' }
    New-NetFirewallRule -Name $name -DisplayName 'DolphinDotNet GameCube telemetry' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $CallbackPort -RemoteAddress $ConsoleAddress.ToString() -Profile Private | Out-Null
}
Write-Host "Configuration created: $configPath"
Write-Host 'API credentials were saved in the protected api-token.txt file; no credential is printed.'
Write-Host 'Next: boot DolphinDotNet.Launcher.dol from GC Loader/Swiss, then run Start-Windows.ps1.'

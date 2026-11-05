[CmdletBinding()]
param([string]$Output = (Join-Path $env:LOCALAPPDATA 'DolphinDotNet\HardwareBridge\launcher'))
$ErrorActionPreference = 'Stop'
$Output = [System.IO.Path]::GetFullPath($Output)
$revision = '6ea774fdf377324e587ee19b48ebc426207bb001'
$image = 'ghcr.io/extremscorner/libogc2@sha256:13bb658d3f18903816617223f5d7c6f776d3db3ea806aae67f8d35a7a69d4c85'
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$source = Join-Path $Output ('source-' + $revision)
if (-not (Test-Path -LiteralPath $source)) {
    & git clone --filter=blob:none --no-checkout https://github.com/capz/DolphinDotNet.Debuggers.git $source
    if ($LASTEXITCODE -ne 0) { throw 'Could not obtain the pinned launcher source.' }
    & git -C $source checkout --detach $revision
    if ($LASTEXITCODE -ne 0) { throw 'Could not check out the pinned launcher revision.' }
}
$actual = (& git -C $source rev-parse HEAD | Out-String).Trim()
if ($actual -ne $revision) { throw 'Launcher cache has a different revision; choose a fresh output directory.' }
& git -C $source diff --quiet HEAD
if ($LASTEXITCODE -ne 0) { throw 'Launcher cache has local changes; choose a fresh output directory.' }
$mount = 'type=bind,source=' + $source + ',target=/work'
& docker run --rm --mount $mount -w /work/tools/DolphinDotNet.Launcher $image make -j2
if ($LASTEXITCODE -ne 0) { throw 'Native launcher build failed.' }
$binary = Join-Path $source 'tools\DolphinDotNet.Launcher\build\DolphinDotNet.Launcher.dol'
Copy-Item -LiteralPath $binary -Destination (Join-Path $Output 'DolphinDotNet.Launcher.dol')
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $Output 'COPYING-launcher.txt')
$archive = Join-Path $Output 'launcher-corresponding-source.zip'
& git -C $source archive --format=zip "--output=$archive" $revision
if ($LASTEXITCODE -ne 0) { throw 'Could not archive the corresponding GPL launcher source.' }
(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Output 'DolphinDotNet.Launcher.dol')).Hash | Set-Content -Encoding ASCII -LiteralPath (Join-Path $Output 'launcher.sha256')
Write-Host "Pinned launcher and corresponding source are in $Output. Boot the DOL once through GC Loader/Swiss."

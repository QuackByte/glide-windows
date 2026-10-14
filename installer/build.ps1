# Publishes Glide (Release, self-contained) and wraps it in an installer:
#   installer\build.ps1 [-Platform x64|ARM64] [-Version 0.1.0] [-PublishOnly | -PackageOnly]
# Needs Inno Setup 6 (winget install JRSoftware.InnoSetup). Output: artifacts\Glide-<version>-<arch>-Setup.exe
# The release workflow runs the two halves separately, to sign the published files in between.
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }),
    [string]$Version = "0.1.0",
    # Only publish to artifacts\publish-<arch>.
    [switch]$PublishOnly,
    # Only build the installer from an existing artifacts\publish-<arch>.
    [switch]$PackageOnly
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$arch = $Platform.ToLowerInvariant()
$publish = Join-Path $root "artifacts\publish-$arch"

if (-not $PackageOnly) {
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    dotnet publish (Join-Path $root "src\Glide\Glide.csproj") -c Release -p:Platform=$Platform -p:Version=$Version -o $publish
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    if ($PublishOnly) { exit 0 }
}

$onPath = Get-Command iscc -ErrorAction SilentlyContinue
$iscc = @(
    $(if ($onPath) { $onPath.Source }),
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

& $iscc /Q "/DArch=$arch" "/DVersion=$Version" (Join-Path $PSScriptRoot "Glide.iss")
if ($LASTEXITCODE) { exit $LASTEXITCODE }
Get-Item (Join-Path $root "artifacts\Glide-$Version-$arch-Setup.exe")

<#
.SYNOPSIS
  Build the tray app (and optionally the Linux binary) on Windows.

.DESCRIPTION
  Produces dist\win-x64\gh-actions-tray.exe. By default the build is
  self-contained, so the result runs on a machine with no .NET installed.

.PARAMETER FrameworkDependent
  Build a much smaller exe that requires the .NET Desktop Runtime at run time.

.PARAMETER LinuxToo
  Also build the Linux waybar binary. Cross-publishing works from Windows.

.PARAMETER Out
  Output directory. Defaults to .\dist

.EXAMPLE
  .\build.ps1
  .\build.ps1 -FrameworkDependent
#>
[CmdletBinding()]
param(
    [string]$Out = 'dist',
    [switch]$FrameworkDependent,
    [switch]$LinuxToo
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "The .NET SDK is not installed, or dotnet is not on PATH. Install the .NET 10 SDK: winget install Microsoft.DotNet.SDK.10"
}

# The projects target net10.0, so an older SDK cannot build them and the error
# it gives ("NETSDK1045") is not obvious. Check up front instead.
$sdk = (& dotnet --version).Trim()
$major = [int]($sdk -split '\.')[0]
if ($major -lt 10) {
    throw "Found .NET SDK $sdk, but this needs 10 or newer. Install it: winget install Microsoft.DotNet.SDK.10"
}
Write-Host "using .NET SDK $sdk"

$selfContained = (-not $FrameworkDependent).ToString().ToLower()

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }

function Publish-Target([string]$Project, [string]$Rid, [string]$Label) {
    Write-Host "==> $Rid  ($Label)"
    $publishArgs = @(
        'publish', $Project,
        '-r', $Rid,
        '-c', 'Release',
        '--self-contained', $selfContained,
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-o', (Join-Path $Out $Rid)
    )
    & dotnet @publishArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid (exit $LASTEXITCODE)" }
}

Publish-Target 'src/GhActions.Tray' 'win-x64' 'gh-actions-tray: notification-area app'
if ($LinuxToo) {
    Publish-Target 'src/GhActions.Cli' 'linux-x64' 'gh-actions-core: waybar module + poller'
}

# Symbols are large and of no use to someone installing this.
Get-ChildItem $Out -Recurse -Filter *.pdb | Remove-Item -Force

Write-Host "`nbuilt:"
Get-ChildItem $Out -Recurse -Include 'gh-actions-tray.exe', 'gh-actions-core' |
    ForEach-Object {
        '  {0,-38} {1,8:N1} MB' -f (Resolve-Path -Relative $_.FullName), ($_.Length / 1MB)
    }

Write-Host "`nnext:  .\install\install-windows.ps1"

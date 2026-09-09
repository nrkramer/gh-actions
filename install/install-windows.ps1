<#
.SYNOPSIS
  Install the GitHub Actions tray app for the current user.

.DESCRIPTION
  Copies the binary to %LOCALAPPDATA%\Programs\gh-actions, writes a starter
  config if none exists, and optionally starts it at login. Per-user only --
  it needs no administrator rights and touches nothing outside your profile.

.PARAMETER NoStartup
  Skip the "run at login" shortcut.

.PARAMETER Uninstall
  Remove the app, its shortcuts and its cache. Leaves config.json alone.
#>
[CmdletBinding()]
param(
    [switch]$NoStartup,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$AppName   = 'gh-actions'
$ExeName   = 'gh-actions-tray.exe'
$InstallTo = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$ConfigDir = Join-Path $env:APPDATA $AppName
$CacheDir  = Join-Path $env:LOCALAPPDATA $AppName
$StartupLnk = Join-Path ([Environment]::GetFolderPath('Startup')) "$AppName.lnk"
$MenuLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'GitHub Actions.lnk'

function Stop-Running {
    Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($ExeName)) -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Kill(); $_.WaitForExit(5000) }
}

function New-Shortcut([string]$Path, [string]$Target) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($Path)
    $lnk.TargetPath = $Target
    $lnk.WorkingDirectory = Split-Path $Target
    $lnk.Description = 'GitHub Actions status in the notification area'
    $lnk.Save()
}

if ($Uninstall) {
    Stop-Running
    foreach ($p in @($StartupLnk, $MenuLnk, $InstallTo, $CacheDir)) {
        if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Host "removed $p" }
    }
    Write-Host "`nDone. Your config at $ConfigDir was left in place."
    return
}

# The build drops the exe in dist\win-x64; allow running this script from
# beside a loose exe too, so a zip download works without the repo.
$source = @(
    (Join-Path $PSScriptRoot "..\dist\win-x64\$ExeName"),
    (Join-Path $PSScriptRoot $ExeName)
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $source) {
    throw "Could not find $ExeName. Run .\build.sh first, or put the exe next to this script."
}

Stop-Running
New-Item -ItemType Directory -Force -Path $InstallTo, $ConfigDir | Out-Null
Copy-Item $source (Join-Path $InstallTo $ExeName) -Force
Write-Host "installed  $InstallTo\$ExeName"

$configPath = Join-Path $ConfigDir 'config.json'
if (-not (Test-Path $configPath)) {
@'
{
  "org": "YOUR-ORG-OR-USERNAME",
  "repos": ["first-repo", "second-repo"],

  "active_poll_seconds": 15,
  "idle_poll_seconds": 60,
  "runs_per_repo": 20,

  "_token": "Omit to use the gh CLI or the GH_TOKEN environment variable.",
  "token": ""
}
'@ | Set-Content -Path $configPath -Encoding UTF8
    Write-Host "wrote      $configPath  (edit this before use)"
}

New-Shortcut $MenuLnk (Join-Path $InstallTo $ExeName)
Write-Host "start menu $MenuLnk"

if (-not $NoStartup) {
    New-Shortcut $StartupLnk (Join-Path $InstallTo $ExeName)
    Write-Host "at login   $StartupLnk"
}

Write-Host "`nEdit $configPath, then launch 'GitHub Actions' from the Start menu."
Write-Host "Windows 11 hides new tray icons: drag it out of the '^' overflow to pin it."

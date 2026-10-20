<#
.SYNOPSIS
    Builds X-Touch Middleware and puts an "X-Touch Middleware" shortcut (golden-yellow icon) on your desktop.

.DESCRIPTION
    1. Publishes XTouch.Service and XTouch.UI into -InstallDir (service\ and ui\ subfolders).
       Existing settings (appsettings.Local.json) and profiles in the install folder are kept.
    2. Copies the icon and creates "X-Touch Middleware.lnk" on the desktop pointing at the UI.
       The UI starts the service automatically if it is not already running.

    Requires the .NET 10 SDK (https://dotnet.microsoft.com/download) to build.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\Install-DesktopShortcut.ps1

.EXAMPLE
    # Only (re)create the shortcut for an existing install
    .\scripts\Install-DesktopShortcut.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\XTouchMiddleware'),
    [string]$Runtime = 'win-x64',
    [string]$ShortcutName = 'X-Touch Middleware',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot                     # ...\XTouchMiddleware
$serviceDir = Join-Path $InstallDir 'service'
$uiDir = Join-Path $InstallDir 'ui'
$uiExe = Join-Path $uiDir 'XTouch.UI.exe'
$iconSource = Join-Path $repo 'src\XTouch.UI\Assets\xtouch.ico'
$iconTarget = Join-Path $InstallDir 'xtouch.ico'

if (-not $SkipPublish) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK was not found. Install the .NET 10 SDK from https://dotnet.microsoft.com/download and run this script again.'
    }

    # A running service locks its files; stop it so it can be updated (the UI restarts it).
    Get-Process -Name 'XTouch.Service', 'XTouch.UI' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Write-Host "Stopping $($_.Name) (pid $($_.Id))"; $_ | Stop-Process -Force; $_.WaitForExit(5000) | Out-Null }

    Write-Host "Publishing service to $serviceDir ..."
    dotnet publish (Join-Path $repo 'src\XTouch.Service') -c Release -r $Runtime --self-contained -o $serviceDir --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publishing XTouch.Service failed.' }

    Write-Host "Publishing UI to $uiDir ..."
    dotnet publish (Join-Path $repo 'src\XTouch.UI') -c Release -r $Runtime --self-contained -o $uiDir --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publishing XTouch.UI failed.' }
}

if (-not (Test-Path $uiExe)) {
    throw "XTouch.UI.exe not found in $uiDir. Run the script without -SkipPublish first."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item $iconSource $iconTarget -Force

$desktop = [Environment]::GetFolderPath('Desktop')            # also correct when the desktop is redirected to OneDrive
$lnkPath = Join-Path $desktop "$ShortcutName.lnk"

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($lnkPath)
$lnk.TargetPath = $uiExe
$lnk.WorkingDirectory = $uiDir
$lnk.IconLocation = "$iconTarget,0"
$lnk.Description = 'Behringer X-Touch middleware - control surface for the X32'
$lnk.Save()

Write-Host ''
Write-Host "Shortcut created: $lnkPath" -ForegroundColor Green
Write-Host "Installed to:     $InstallDir"
Write-Host "Service settings: $(Join-Path $serviceDir 'appsettings.Local.json') (create it to set the X32 address; see README)"

# Packages Dizzy Sailwind Fixes for GitHub Releases.
# Usage: .\scripts\package-release.ps1 [-Version 0.1.0]

param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$dll = "src\Dizzy.Fixes\bin\Release\Dizzy.Fixes.dll"
if (-not (Test-Path $dll)) {
    Write-Host "Building Dizzy.Fixes Release..."
    dotnet build src\Dizzy.Fixes\Dizzy.Fixes.csproj -c Release
}

if (-not (Test-Path $dll)) {
    throw "Missing $dll - build failed."
}

$staging = "dist\Dizzy.Fixes-$Version"
$pluginDir = "$staging\Dizzy.Fixes"
$zipPath = "dist\Dizzy.Fixes-$Version.zip"
$notesPath = "dist\GITHUB_RELEASE_NOTES-v$Version.md"

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll $pluginDir -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path $pluginDir -DestinationPath $zipPath -Force

$notes = @"
## Install

Requires Sailwind + [BepInEx 5](https://thunderstore.io/c/sailwind/p/BepInEx/BepInExPack/) (Thunderstore BepInExPack recommended).

1. Download ``Dizzy.Fixes-$Version.zip`` below.
2. Extract the ``Dizzy.Fixes`` folder into ``BepInEx\plugins\`` (next to ``Sailwind.exe``: ``Sailwind\BepInEx\plugins\Dizzy.Fixes\``).
3. Launch the game.

## Contents

``````
Dizzy.Fixes/
  Dizzy.Fixes.dll
``````

Config: ``BepInEx\config\com.dizzy.sailwind.fixes.cfg``

## Source

Built from tag ``v$Version`` on this repository.
"@

New-Item -ItemType Directory -Force -Path dist | Out-Null
Set-Content -Path $notesPath -Value $notes -Encoding UTF8

Write-Host "Created: $zipPath"
Write-Host "Release notes: $notesPath"
Write-Host ""
Write-Host "Next: GitHub -> Releases -> Draft new release -> tag v$Version -> attach zip -> paste notes."

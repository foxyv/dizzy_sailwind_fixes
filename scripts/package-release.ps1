# Packages Dizzy Sailwind Fixes for GitHub Releases.
# Usage: .\scripts\package-release.ps1 [-Version 0.3.1]
# The version defaults to PluginVersion in Plugin.cs. Passing -Version only
# checks that it matches, so a zip can never carry the wrong label.

param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$pluginSource = Get-Content "src\Dizzy.Fixes\Plugin.cs" -Raw
$match = [regex]::Match($pluginSource, 'PluginVersion\s*=\s*"([^"]+)"')
if (-not $match.Success) {
    throw "Could not find PluginVersion in src\Dizzy.Fixes\Plugin.cs."
}
$pluginVersion = $match.Groups[1].Value

if ($Version -eq "") {
    $Version = $pluginVersion
} elseif ($Version -ne $pluginVersion) {
    throw "-Version $Version does not match PluginVersion $pluginVersion in Plugin.cs."
}

# Always build, so the zip never carries a stale DLL.
Write-Host "Building Dizzy.Fixes $Version Release..."
dotnet build src\Dizzy.Fixes\Dizzy.Fixes.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$dll = "src\Dizzy.Fixes\bin\Release\Dizzy.Fixes.dll"
if (-not (Test-Path $dll)) {
    throw "Missing $dll - build failed."
}

$built = [System.Reflection.AssemblyName]::GetAssemblyName((Resolve-Path $dll)).Version
if ("$($built.Major).$($built.Minor).$($built.Build)" -ne $Version) {
    throw "Built DLL is version $built, expected $Version. Keep <Version> in the csproj in sync with PluginVersion."
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

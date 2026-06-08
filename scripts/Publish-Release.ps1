<#
.SYNOPSIS
  Build distributable WeatherWizard packages for Windows x64.

.DESCRIPTION
  - Portable: self-contained folder + zip (extract and run WeatherWizard.exe).
  - PortableInstaller: same binaries plus README + Run-WeatherWizard.cmd, zipped as a "portable install" bundle.
  - Msix: MSIX for sideload / Store pipeline.
  - Both: Portable zip + PortableInstaller zip + MSIX (one dotnet publish for portable variants).

.PARAMETER Mode
  Portable | PortableInstaller | Msix | Both

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\Publish-Release.ps1 -Mode Portable
  powershell -ExecutionPolicy Bypass -File scripts\Publish-Release.ps1 -Mode PortableInstaller
  powershell -ExecutionPolicy Bypass -File scripts\Publish-Release.ps1 -Mode Both
#>
param(
    [ValidateSet('Portable', 'PortableInstaller', 'Msix', 'Both')]
    [string] $Mode = 'Both'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $repoRoot 'WeatherWizard\WeatherWizard.csproj'
$dist = Join-Path $repoRoot 'dist'
if (-not (Test-Path $proj)) { throw "Project not found: $proj" }

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$common = @(
    'publish', $proj
    '-c', 'Release'
    '-r', 'win-x64'
    '-p:Platform=x64'
    '-p:PublishTrimmed=false'
    '-p:PublishReadyToRun=true'
)

function Add-PortableInstallerExtras([string] $targetDir) {
    $readme = @"
WeatherWizard — portable package

Install: unzip this folder anywhere you like (for example Desktop or Documents).

Run: double-click WeatherWizard.exe, or double-click Run-WeatherWizard.cmd.

Update: replace the folder with a newer build.

Uninstall: delete the folder. Nothing is written to Program Files or the registry for this portable layout.

This build includes the Windows App SDK runtime (self-contained); no separate SDK install is required.
"@
    Set-Content -LiteralPath (Join-Path $targetDir 'README-PORTABLE.txt') -Value $readme.TrimEnd() -Encoding utf8

    $cmd = @'
@echo off
cd /d "%~dp0"
start "" "%~dp0WeatherWizard.exe"
'@
    Set-Content -LiteralPath (Join-Path $targetDir 'Run-WeatherWizard.cmd') -Value $cmd.TrimEnd() -Encoding ascii
}

function Compress-FolderToZip([string] $sourceDir, [string] $zipPath) {
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (Test-Path $tar) {
        Push-Location $sourceDir
        try { & $tar -a -c -f $zipPath * } finally { Pop-Location }
    }
    else {
        Start-Sleep -Seconds 3
        Compress-Archive -Path (Join-Path $sourceDir '*') -DestinationPath $zipPath
    }
}

$needsPortablePublish = ($Mode -eq 'Portable' -or $Mode -eq 'PortableInstaller' -or $Mode -eq 'Both')
$portableDir = Join-Path $dist 'WeatherWizard-win-x64-portable'

if ($needsPortablePublish) {
    if (Test-Path $portableDir) { Remove-Item -Recurse -Force $portableDir }
    Write-Host "Publishing portable (self-contained) -> $portableDir"
    dotnet @common --self-contained true -p:WindowsAppSDKSelfContained=true -o $portableDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed" }

    if ($Mode -eq 'Portable' -or $Mode -eq 'Both') {
        $zipPlain = Join-Path $dist 'WeatherWizard-win-x64-portable.zip'
        Write-Host "Zipping plain portable -> $zipPlain"
        Compress-FolderToZip $portableDir $zipPlain
        Write-Host "Created: $zipPlain"
    }

    if ($Mode -eq 'PortableInstaller' -or $Mode -eq 'Both') {
        $staging = Join-Path $dist '_portable-installer-staging'
        if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
        Write-Host "Staging portable installer -> $staging"
        New-Item -ItemType Directory -Force -Path $staging | Out-Null
        robocopy $portableDir $staging /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy staging failed (exit $LASTEXITCODE)" }
        Add-PortableInstallerExtras $staging
        $zipInstaller = Join-Path $dist 'WeatherWizard-PortableInstaller-win-x64.zip'
        Write-Host "Zipping portable installer -> $zipInstaller"
        Compress-FolderToZip $staging $zipInstaller
        Remove-Item -Recurse -Force $staging
        Write-Host "Created: $zipInstaller"
    }
}

if ($Mode -eq 'Msix' -or $Mode -eq 'Both') {
    $msixRoot = (Join-Path $dist 'msix-build').TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (Test-Path (Join-Path $dist 'msix-build')) { Remove-Item -Recurse -Force (Join-Path $dist 'msix-build') }
    New-Item -ItemType Directory -Force -Path (Join-Path $dist 'msix-build') | Out-Null
    Write-Host "Publishing MSIX -> $msixRoot"
    dotnet @common `
        -p:WindowsPackageType=MSIX `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageDir=$msixRoot `
        -p:SelfContained=true `
        -p:WindowsAppSDKSelfContained=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (MSIX) failed" }

    $msix = Get-ChildItem -Path $msixRoot -Recurse -Filter '*.msix' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $msix) {
        $msix = Get-ChildItem -Path $dist -Recurse -Filter '*.msix' -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if (-not $msix) { throw "No .msix file found under $msixRoot" }
    $destMsix = Join-Path $dist $msix.Name
    if (Test-Path $destMsix) { Remove-Item -Force $destMsix }
    Copy-Item -Path $msix.FullName -Destination $destMsix
    Write-Host "Created: $destMsix"
    Write-Host "Note: Test-signed MSIX may require 'Sideload apps' and trusting the signing certificate on first install."
}

Write-Host 'Done.'

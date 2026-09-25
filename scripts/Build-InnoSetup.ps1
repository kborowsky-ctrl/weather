<#
.SYNOPSIS
  Publish the portable app, then compile an Inno Setup installer EXE.

.DESCRIPTION
  Requires Inno Setup 6 or 7 (ISCC.exe). Looks in common install paths, or set
  env var INNO_SETUP_ISCC to the full path of ISCC.exe.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\Build-InnoSetup.ps1
  powershell -ExecutionPolicy Bypass -File scripts\Build-InnoSetup.ps1 -SkipPublish
  powershell -ExecutionPolicy Bypass -File scripts\Build-InnoSetup.ps1 -IfChanged
#>
param(
    [switch] $SkipPublish,
    # Skip everything when the portable exe and installer already match Version.props.
    [switch] $IfChanged
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$iss = Join-Path $repoRoot 'installer\WeatherWizard.iss'
$portableDir = Join-Path $repoRoot 'dist\WeatherWizard-win-x64-portable'
$versionProps = Join-Path $repoRoot 'WeatherWizard\Version.props'

if (-not (Test-Path $iss)) { throw "Inno script not found: $iss" }

function Get-AppVersion {
    if (-not (Test-Path $versionProps)) { return '1.0.0' }
    $xml = [xml](Get-Content -LiteralPath $versionProps -Raw)
    $v = $xml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($v)) { return '1.0.0' }
    return $v.Trim()
}

function Find-ISCC {
    if ($env:INNO_SETUP_ISCC -and (Test-Path $env:INNO_SETUP_ISCC)) {
        return $env:INNO_SETUP_ISCC
    }
    $candidates = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe"
        "${env:LocalAppData}\Programs\Inno Setup 7\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path $p) { return $p }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$setup = Join-Path $repoRoot 'dist\WeatherWizard-Setup-win-x64.exe'

if ($IfChanged) {
    $version = Get-AppVersion
    $portableExe = Join-Path $portableDir 'WeatherWizard.exe'
    if ((Test-Path $portableExe) -and (Test-Path $setup)) {
        $exeVersion = (Get-Item $portableExe).VersionInfo.FileVersion
        $setupIsNewer = (Get-Item $setup).LastWriteTime -ge (Get-Item $portableExe).LastWriteTime
        if ($exeVersion -eq "$version.0" -and $setupIsNewer) {
            Write-Host "Portable build and installer already at $version; skipping republish."
            return
        }
    }
    Write-Host "Republishing portable build and installer for $version..."
}

if (-not $SkipPublish) {
    $running = Get-Process WeatherWizard -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($portableDir, [StringComparison]::OrdinalIgnoreCase) }
    if ($running) {
        $msg = "WeatherWizard is running from $portableDir; close it and rebuild to republish."
        if ($IfChanged) {
            Write-Warning $msg
            return
        }
        throw $msg
    }
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'Publish-Release.ps1') -Mode Portable
}

if (-not (Test-Path (Join-Path $portableDir 'WeatherWizard.exe'))) {
    throw "Portable build missing WeatherWizard.exe under $portableDir. Run without -SkipPublish first."
}

$iscc = Find-ISCC
if (-not $iscc) {
    throw @"
Inno Setup compiler (ISCC.exe) not found.
Install Inno Setup 6, or set INNO_SETUP_ISCC to the full path of ISCC.exe.
"@
}

$version = Get-AppVersion
Write-Host "Compiling Inno Setup installer (version $version)..."
Write-Host "ISCC: $iscc"

& $iscc `
    "/DMyAppVersion=$version" `
    "/DSourceDir=$portableDir" `
    $iss

if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }

if (-not (Test-Path $setup)) { throw "Expected installer not found: $setup" }
Write-Host "Created: $setup"
Write-Host ""
Write-Host "To publish an update on GitHub:"
Write-Host "  1. Create a Release tagged like v1.0.27 (three parts: major.minor.patch - not v1.02.7)"
Write-Host "  2. Attach this file as: WeatherWizard-Setup-win-x64.exe"
Write-Host "  3. Users with Check for updates will see it automatically"
Write-Host 'Done.'

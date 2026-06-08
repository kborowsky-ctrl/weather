<#
.SYNOPSIS
  Build only the portable installer zip (self-contained + README + launcher).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\Publish-PortableInstaller.ps1
#>
$ErrorActionPreference = 'Stop'
& (Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Publish-Release.ps1') -Mode PortableInstaller

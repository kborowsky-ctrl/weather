<#
.SYNOPSIS
  Bump patch version in Version.props when WeatherWizard source files change.

.DESCRIPTION
  Fingerprints *.cs and *.xaml under the project directory. When the fingerprint
  differs from the last build, increments the patch segment (e.g. 1.0.3 -> 1.0.4).
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ProjectDir
)

$ErrorActionPreference = 'Stop'

$versionProps = Join-Path $ProjectDir 'Version.props'
$statePath = Join-Path $ProjectDir 'version.state.json'
$fingerprintPath = Join-Path $ProjectDir 'obj\version-fingerprint.txt'

$sourceFiles = Get-ChildItem -Path $ProjectDir -Recurse -File -Include *.cs, *.xaml |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object FullName

$hash = [System.Security.Cryptography.SHA256]::Create()
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.StreamWriter($stream)
foreach ($file in $sourceFiles) {
    $writer.WriteLine($file.FullName)
    $writer.WriteLine((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
}
$writer.Flush()
$null = $stream.Seek(0, [System.IO.SeekOrigin]::Begin)
$fingerprint = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
$hash.Dispose()
$writer.Dispose()
$stream.Dispose()

$lastFingerprint = $null
if (Test-Path $fingerprintPath) {
    $lastFingerprint = (Get-Content -LiteralPath $fingerprintPath -Raw).Trim()
}

$version = '1.0.0'
if (Test-Path $statePath) {
    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.version) { $version = [string]$state.version }
    }
    catch { }
}
elseif (Test-Path $versionProps) {
    $propsMatch = [regex]::Match((Get-Content -LiteralPath $versionProps -Raw), '<Version>([^<]+)</Version>')
    if ($propsMatch.Success) {
        $version = $propsMatch.Groups[1].Value
    }
}

if ($fingerprint -eq $lastFingerprint) {
    Write-Host "Version unchanged ($version); source fingerprint matches last build."
    exit 0
}

$parts = $version.Split('.')
$major = [int]($parts[0])
$minor = if ($parts.Length -gt 1) { [int]$parts[1] } else { 0 }
$patch = if ($parts.Length -gt 2) { [int]$parts[2] } else { 0 }
$patch++

$newVersion = "$major.$minor.$patch"
$assemblyVersion = "$major.$minor.$patch.0"

$propsContent = @"
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <Version>$newVersion</Version>
    <AssemblyVersion>$assemblyVersion</AssemblyVersion>
    <FileVersion>$assemblyVersion</FileVersion>
  </PropertyGroup>
</Project>
"@
Set-Content -LiteralPath $versionProps -Value $propsContent.TrimEnd() -Encoding utf8 -NoNewline

$stateContent = @{ version = $newVersion; sourceFingerprint = $fingerprint } | ConvertTo-Json
Set-Content -LiteralPath $statePath -Value $stateContent -Encoding utf8

$objDir = Join-Path $ProjectDir 'obj'
if (-not (Test-Path $objDir)) { New-Item -ItemType Directory -Force -Path $objDir | Out-Null }
Set-Content -LiteralPath $fingerprintPath -Value $fingerprint -Encoding ascii -NoNewline

Write-Host "Version bumped to $newVersion (source changed)."

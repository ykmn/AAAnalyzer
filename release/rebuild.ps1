[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Path $PSScriptRoot -Parent
$releaseFile = Join-Path $projectRoot 'VERSION.txt'
$projectFile = Join-Path $projectRoot 'src\SystemAudioAnalyzer.App\SystemAudioAnalyzer.App.csproj'

if (-not (Test-Path -LiteralPath $releaseFile)) {
    throw "Version file not found: $releaseFile"
}

$release = (Get-Content -LiteralPath $releaseFile -Raw).Trim()
if ($release -notmatch '^(?<version>\d+\.\d{3})\s-\s(?<date>\d{4}\.\d{2}\.\d{2})$') {
    throw 'VERSION.txt must have the format VERSION - DATE, for example 0.001 - 2026.10.06.'
}

$version = $Matches.version
Write-Host "Building version: $version" -ForegroundColor Green
$outputDirectory = Join-Path $PSScriptRoot "AAAnalyzer-$version"

if (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

dotnet publish $projectFile `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    --output $outputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$localePattern = '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'
$localeDirectories = Get-ChildItem -LiteralPath $outputDirectory -Directory |
    Where-Object { $_.Name -match $localePattern -and $_.Name -notin @('en', 'ru') }
foreach ($localeDirectory in $localeDirectories) {
    Remove-Item -LiteralPath $localeDirectory.FullName -Recurse -Force
}

$executable = Join-Path $outputDirectory 'AAAnalyzer.exe'
$libVlc = Join-Path $outputDirectory ("libvlc\{0}\libvlc.dll" -f $RuntimeIdentifier)
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Portable build is missing AAAnalyzer.exe: $outputDirectory"
}

if (-not (Test-Path -LiteralPath $libVlc)) {
    throw "Portable build is missing LibVLC runtime (libvlc.dll): $libVlc"
}

Write-Host "Portable build created: $outputDirectory"

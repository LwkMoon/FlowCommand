<#
.SYNOPSIS
  Builds the two release archives and a SHA-256 checksum file into .\artifacts.
.DESCRIPTION
  1. FlowCommand-<version>-win-x64.zip               small; needs the .NET 10 Desktop Runtime installed
  2. FlowCommand-<version>-win-x64-self-contained.zip  larger; runs on a PC with nothing extra installed
.PARAMETER Version
  A label such as v1.0.0. When it looks like a version number it is also stamped into the executable.
#>
[CmdletBinding()]
param([string]$Version = "dev")

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$out = "artifacts"
$project = "src/FlowCommand/FlowCommand.csproj"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item $out -ItemType Directory | Out-Null

function Publish-Build([string]$Name, [bool]$SelfContained) {
    $dir = Join-Path $out $Name
    $publishArgs = @(
        "publish", $project, "-c", "Release", "-r", "win-x64", "-o", $dir,
        "-p:PublishSingleFile=true", "-p:DebugType=none", "-p:DebugSymbols=false"
    )
    if ($Version -match '^v?(\d+\.\d+\.\d+.*)$') { $publishArgs += "-p:Version=$($Matches[1])" }
    if ($SelfContained) {
        $publishArgs += @("--self-contained", "true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true")
    } else {
        $publishArgs += @("--self-contained", "false")
    }

    Write-Host "==> Publishing $Name" -ForegroundColor Cyan
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Name." }

    Copy-Item "config.example.json", "README.md", "LICENSE" $dir
    Compress-Archive -Path (Join-Path $dir "*") -DestinationPath (Join-Path $out "$Name.zip")
    Remove-Item $dir -Recurse -Force
}

Publish-Build "FlowCommand-$Version-win-x64" $false
Publish-Build "FlowCommand-$Version-win-x64-self-contained" $true

$sums = Get-ChildItem $out -Filter *.zip | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
}
$sums | Set-Content (Join-Path $out "SHA256SUMS.txt") -Encoding ascii

Write-Host ""
Write-Host "Archives are in .\$out" -ForegroundColor Green
$sums

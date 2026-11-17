<#
.SYNOPSIS
  Builds FlowCommand as an MSIX package (self-contained, x64) into .\artifacts\msix.

.DESCRIPTION
  -Target dev    Test build. Uses a throw-away identity and signs with a self-signed certificate so you can
                 install it on your own PC. NOT for the Store.
  -Target store  Store build. Requires the three identity values from Partner Center (Product identity page)
                 and produces an UNSIGNED .msix, because the Microsoft Store signs packages itself.

  Needs: .NET 10 SDK and the Windows 10/11 SDK (makeappx, makepri, signtool). Run on Windows.

.EXAMPLE
  .\scripts\New-MsixPackage.ps1                                  # dev build, version from Directory.Build.props
.EXAMPLE
  .\scripts\New-MsixPackage.ps1 -Target store -IdentityName "12345Publisher.FlowCommand" `
      -Publisher "CN=00000000-0000-0000-0000-000000000000" -PublisherDisplayName "Your Name"
#>
[CmdletBinding()]
param(
    [ValidateSet("dev", "store")] [string]$Target = "dev",
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [switch]$NoReadyToRun
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

# ---------------------------------------------------------------- version (Store rule: 4 parts, last part 0)
if (-not $Version) {
    $props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
    if ($props -match '<Version>\s*(\d+\.\d+\.\d+)\s*</Version>') { $Version = $Matches[1] }
    else { throw "Could not read <Version> from Directory.Build.props. Pass -Version 1.2.3." }
}
if ($Version -notmatch '^(\d+\.\d+\.\d+)(\.0)?$') {
    throw "Version '$Version' is not valid. Use 3 parts such as 1.0.1 (the Store reserves the 4th part, so it is always .0)."
}
$semver = $Matches[1]
$msixVersion = "$semver.0"

# ---------------------------------------------------------------- identity
if ($Target -eq "store") {
    foreach ($pair in @(@("IdentityName", $IdentityName), @("Publisher", $Publisher), @("PublisherDisplayName", $PublisherDisplayName))) {
        if ([string]::IsNullOrWhiteSpace($pair[1]) -or $pair[1] -match "REPLACE") {
            throw "-$($pair[0]) is required for a Store build. Copy it exactly from Partner Center > Product management > Product identity."
        }
    }
} else {
    if (-not $IdentityName)         { $IdentityName = "LwkMoon.FlowCommand.Dev" }
    if (-not $Publisher)            { $Publisher = "CN=FlowCommand Dev" }
    if (-not $PublisherDisplayName) { $PublisherDisplayName = "FlowCommand (development build)" }
}

# ---------------------------------------------------------------- locate Windows SDK tools
function Find-SdkTool([string]$name) {
    $bases = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "${env:ProgramFiles}\Windows Kits\10\bin") | Where-Object { Test-Path $_ }
    $found = foreach ($b in $bases) { Get-ChildItem $b -Directory | Where-Object { $_.Name -match '^10\.' } |
        Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName "x64\$name" } | Where-Object { Test-Path $_ } }
    if (-not $found) { throw "$name not found. Install the Windows 10/11 SDK (Visual Studio installer > 'Windows 11 SDK')." }
    return @($found)[0]
}
$makeappx = Find-SdkTool "makeappx.exe"
$makepri  = Find-SdkTool "makepri.exe"

# ---------------------------------------------------------------- publish (self-contained: a clean PC has no .NET)
$out = Join-Path $root "artifacts\msix"
$layout = Join-Path $out "layout"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item $layout -ItemType Directory | Out-Null

$publish = @(
    "publish", "src/FlowCommand/FlowCommand.csproj", "-c", "Release", "-r", "win-x64",
    "--self-contained", "true", "-p:PublishSingleFile=false", "-p:DebugType=none", "-p:DebugSymbols=false",
    "-p:Version=$semver", "-o", $layout
)
if (-not $NoReadyToRun) { $publish += "-p:PublishReadyToRun=true" }
Write-Host "==> dotnet publish (self-contained, win-x64)" -ForegroundColor Cyan
& dotnet @publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# ---------------------------------------------------------------- manifest + assets
Copy-Item (Join-Path $root "packaging\Assets") (Join-Path $layout "Assets") -Recurse
$manifest = Get-Content (Join-Path $root "packaging\AppxManifest.xml") -Raw
$manifest = $manifest.Replace("{{IDENTITY_NAME}}", [Security.SecurityElement]::Escape($IdentityName)).
    Replace("{{PUBLISHER}}", [Security.SecurityElement]::Escape($Publisher)).
    Replace("{{PUBLISHER_DISPLAY_NAME}}", [Security.SecurityElement]::Escape($PublisherDisplayName)).
    Replace("{{VERSION}}", $msixVersion)
[IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object Text.UTF8Encoding($false)))

# ---------------------------------------------------------------- resource index so Windows can pick the right icon size
Write-Host "==> makepri" -ForegroundColor Cyan
$priConfig = Join-Path $out "priconfig.xml"
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed." }
& $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o
if ($LASTEXITCODE -ne 0) { throw "makepri new failed." }

# ---------------------------------------------------------------- pack
$msix = Join-Path $out "FlowCommand_${msixVersion}_x64_$Target.msix"
Write-Host "==> makeappx pack" -ForegroundColor Cyan
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed." }

# ---------------------------------------------------------------- dev signing (never used for the Store)
if ($Target -eq "dev") {
    $signtool = Find-SdkTool "signtool.exe"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
        -FriendlyName "FlowCommand development signing" -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") -NotAfter (Get-Date).AddMonths(6)
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool failed." }
    Export-Certificate -Cert $cert -FilePath (Join-Path $out "FlowCommand-dev.cer") | Out-Null
    Write-Host "Signed with a self-signed development certificate (exported as FlowCommand-dev.cer)." -ForegroundColor Yellow
} else {
    Write-Host "Store build left UNSIGNED on purpose: the Microsoft Store signs it after certification." -ForegroundColor Yellow
}

$hash = (Get-FileHash $msix -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $msix -Leaf)" | Set-Content (Join-Path $out "SHA256SUMS.txt") -Encoding ascii
Write-Host ""
Write-Host "Package: $msix" -ForegroundColor Green
Write-Host ("Size:    {0:N1} MB" -f ((Get-Item $msix).Length / 1MB))
Write-Host "Version: $msixVersion (x64)    Target: $Target"
Write-Host "SHA-256: $hash"

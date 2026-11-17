<#
.SYNOPSIS
  Installs the development MSIX built by New-MsixPackage.ps1 on this PC so you can test it like a Store install.
  Run from an ADMINISTRATOR PowerShell (the test certificate must be trusted once). Dev builds only.
#>
[CmdletBinding()]
param([string]$Directory = (Join-Path $PSScriptRoot "..\artifacts\msix"))

$ErrorActionPreference = "Stop"
$cer  = Join-Path $Directory "FlowCommand-dev.cer"
$msix = Get-ChildItem $Directory -Filter "*_dev.msix" | Select-Object -First 1
if (-not $msix -or -not (Test-Path $cer)) { throw "Run scripts\New-MsixPackage.ps1 first (dev target)." }

Import-Certificate -FilePath $cer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
Add-AppxPackage -Path $msix.FullName
Write-Host "Installed. Start FlowCommand from the Start menu." -ForegroundColor Green
Write-Host "To remove it again:  Get-AppxPackage *FlowCommand.Dev* | Remove-AppxPackage"
Write-Host "To untrust the test certificate:  Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=FlowCommand Dev' | Remove-Item"

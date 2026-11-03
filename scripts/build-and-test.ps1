<#
.SYNOPSIS
  Restores, builds and tests the solution, then publishes the app to .\dist.
.NOTES
  Requires the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
  If PowerShell blocks this script, run it with:
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-and-test.ps1
#>
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)." }
}

Invoke-Step "dotnet --version" { dotnet --version }
Invoke-Step "Restore" { dotnet restore FlowCommand.slnx }
Invoke-Step "Build" { dotnet build FlowCommand.slnx -c Release --no-restore }
Invoke-Step "Test" { dotnet test tests/FlowCommand.Tests/FlowCommand.Tests.csproj -c Release --no-build }

# Framework-dependent single-file build: needs the .NET 10 Desktop Runtime (included with the SDK).
Invoke-Step "Publish" {
    dotnet publish src/FlowCommand/FlowCommand.csproj -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -o dist
}

Write-Host ""
Write-Host "Done. Run:  .\dist\FlowCommand.exe" -ForegroundColor Green

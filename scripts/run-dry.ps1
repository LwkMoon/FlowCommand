<#
.SYNOPSIS
  Runs FlowCommand from source with dry-run switched on, so commands are interpreted but never executed.
.NOTES
  Dry-run is a setting in config.json. This script turns it on, so turn it off again in Settings or the tray menu.
  Launch FlowCommand once first so the configuration file exists.
#>
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

$config = Join-Path $env:APPDATA "FlowCommand\config.json"
if (Test-Path $config) {
    (Get-Content $config -Raw) -replace '"dryRun":\s*false', '"dryRun": true' | Set-Content $config
} else {
    Write-Warning "No configuration file yet. Start FlowCommand once, enable Dry-run mode in Settings, or run this script again."
}
dotnet run --project src/FlowCommand/FlowCommand.csproj -c Release

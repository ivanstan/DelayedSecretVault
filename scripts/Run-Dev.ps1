#Requires -Version 5.1
<#
.SYNOPSIS
  Build and run Delayed Secret Vault in Debug (dev) — 10 second access delay.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\DelayedSecretVault\DelayedSecretVault.csproj"

Set-Location $repoRoot
Write-Host "Starting Delayed Secret Vault (Debug / 10s delay)..."
& dotnet run --project $project -c Debug
if ($LASTEXITCODE -ne 0) {
    throw "dotnet run failed with exit code $LASTEXITCODE"
}

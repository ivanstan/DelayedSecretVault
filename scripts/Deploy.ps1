#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Delayed Secret Vault (Release/prod) as a single EXE and packs it into a zip.

  Default is framework-dependent single-file (small; requires .NET 8 Desktop Runtime).

.EXAMPLE
  .\scripts\Deploy.ps1

.EXAMPLE
  .\scripts\Deploy.ps1 -SelfContained
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\DelayedSecretVault\DelayedSecretVault.csproj"
$artifactsRoot = if ($OutputDir) { $OutputDir } else { Join-Path $repoRoot "artifacts" }
$publishDir = Join-Path $artifactsRoot "publish"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$modeTag = if ($SelfContained) { "selfcontained-singlefile" } else { "frameworkdependent-singlefile" }
$zipPath = Join-Path $artifactsRoot "DelayedSecretVault-$Configuration-$Runtime-$modeTag-$stamp.zip"

if ($Configuration -ne "Release") {
    Write-Warning "Deploying with Configuration='$Configuration'. Prod delay (60 minutes) requires Release."
}

$selfContained = [bool]$SelfContained

Write-Host "Cleaning publish output: $publishDir"
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing ($Configuration, $Runtime, self-contained=$selfContained, single-file=true)..."
$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", ($selfContained.ToString().ToLowerInvariant()),
    "-o", $publishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)
if ($selfContained) {
    $publishArgs += "/p:EnableCompressionInSingleFile=true"
}
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $publishDir "DelayedSecretVault.exe"
if (-not (Test-Path $exePath)) {
    throw "Missing required artifact: DelayedSecretVault.exe"
}

# Drop leftover satellite files that aren't needed to run (PDBs already disabled).
Get-ChildItem $publishDir -File |
    Where-Object { $_.Name -ne "DelayedSecretVault.exe" } |
    ForEach-Object {
        Write-Host "Removing non-exe publish file: $($_.Name)"
        Remove-Item -Force $_.FullName
    }

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Creating archive: $zipPath"
Compress-Archive -Path $exePath -DestinationPath $zipPath -CompressionLevel Optimal

$zip = Get-Item $zipPath
$exe = Get-Item $exePath
Write-Host ""
Write-Host "Deploy package ready."
Write-Host "  EXE:            $($exe.FullName) ($([math]::Round($exe.Length / 1MB, 2)) MB)"
Write-Host "  Zip archive:    $($zip.FullName) ($([math]::Round($zip.Length / 1MB, 2)) MB)"
Write-Host "  Access delay:   60 minutes (Release) / 10 seconds (Debug)"
if (-not $selfContained) {
    Write-Host "  Runtime:        Requires .NET 8 Desktop Runtime (windowsdesktop)"
}

#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes Delayed Secret Vault as a framework-dependent single-file EXE for Windows.

  Requires .NET 8 Desktop Runtime on the target machine.

.EXAMPLE
  .\scripts\Deploy.ps1

.EXAMPLE
  .\scripts\Deploy.ps1 -Version 1.0.0 -Runtime win-x64
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\DelayedSecretVault\DelayedSecretVault.csproj"
$artifactsRoot = if ($OutputDir) { $OutputDir } else { Join-Path $repoRoot "artifacts" }
$publishDir = Join-Path $artifactsRoot "publish"
$exeName = "DelayedSecretVault-$Runtime.exe"
$zipName = "DelayedSecretVault-$Version-$Runtime.zip"
$exePath = Join-Path $publishDir $exeName
$zipPath = Join-Path $artifactsRoot $zipName

if ($Configuration -ne "Release") {
    Write-Warning "Deploying with Configuration='$Configuration'. Prod delay (60 minutes) requires Release."
}

Write-Host "Cleaning publish output: $publishDir"
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing framework-dependent single-file ($Configuration, $Runtime, v$Version)..."
$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "false",
    "-o", $publishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:Version=$Version",
    "/p:AssemblyVersion=$Version.0",
    "/p:FileVersion=$Version.0"
)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExe = Join-Path $publishDir "DelayedSecretVault.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Missing required artifact: DelayedSecretVault.exe"
}

# Keep a single architecture-tagged EXE in the publish folder.
Get-ChildItem $publishDir -File |
    Where-Object { $_.FullName -ne $publishedExe } |
    ForEach-Object {
        Write-Host "Removing non-exe publish file: $($_.Name)"
        Remove-Item -Force $_.FullName
    }

if (Test-Path $exePath) {
    Remove-Item -Force $exePath
}
Move-Item -Force $publishedExe $exePath

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-Host "Creating archive: $zipPath"
Compress-Archive -Path $exePath -DestinationPath $zipPath -CompressionLevel Optimal

$zip = Get-Item $zipPath
$exe = Get-Item $exePath
Write-Host ""
Write-Host "Deploy package ready."
Write-Host "  Architecture:   $Runtime"
Write-Host "  Mode:           framework-dependent single-file"
Write-Host "  EXE:            $($exe.FullName) ($([math]::Round($exe.Length / 1KB, 1)) KB)"
Write-Host "  Zip archive:    $($zip.FullName) ($([math]::Round($zip.Length / 1KB, 1)) KB)"
Write-Host "  Access delay:   60 minutes (Release) / 10 seconds (Debug)"
Write-Host "  Runtime:        Requires .NET 8 Desktop Runtime (windowsdesktop-$Runtime)"

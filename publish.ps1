#!/usr/bin/env pwsh
#Requires -Version 7

param(
    [string]$Runtime,
    [string]$OutputDir = "$PSScriptRoot/publish",
    [switch]$NoRuntimeDirectory
)

$project = "$PSScriptRoot/src/CSnip/CSnip.csproj"

if (-not $Runtime) {
    $Runtime = switch ($true) {
        $IsWindows { "win-x64" }
        $IsMacOS   {
            $arch = (uname -m)
            if ($arch -eq "arm64") { "osx-arm64" } else { "osx-x64" }
        }
        $IsLinux   { "linux-x64" }
        default    { throw "Cannot detect runtime. Pass -Runtime explicitly (e.g. win-x64, osx-arm64, linux-x64)." }
    }
}

$publishOutputDir = if ($NoRuntimeDirectory) {
    $OutputDir
} else {
    Join-Path $OutputDir $Runtime
}

Write-Host "Publishing $project" -ForegroundColor Cyan
Write-Host "  Runtime  : $Runtime"
Write-Host "  Output   : $publishOutputDir"

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $publishOutputDir `
    -p:PublishAot=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

$binary = Get-ChildItem $publishOutputDir -File |
    Where-Object { $_.Name -like "csnip*" -and $_.Extension -notin @('.json', '.pdb', '.dbg', '.dsym') } |
    Select-Object -First 1

if ($binary) {
    Write-Host "`nOutput binary: $($binary.FullName)" -ForegroundColor Green
    Write-Host "Size          : $([math]::Round($binary.Length / 1MB, 2)) MB"
}

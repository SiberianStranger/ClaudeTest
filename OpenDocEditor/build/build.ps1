# Build script for Windows (run on Windows with .NET 8 SDK installed)
# Usage: .\build.ps1

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$App = "$Root\src\OpenDocEditor.App"
$Dist = "$Root\dist"

Write-Host "=== OpenDocEditor Build (Windows) ===" -ForegroundColor Cyan

Write-Host "[1/3] Restoring packages..." -ForegroundColor Yellow
dotnet restore $Root

Write-Host "[2/3] Building (Debug check)..." -ForegroundColor Yellow
dotnet build $Root -c Debug --no-restore

Write-Host "[3/3] Publishing win-x64..." -ForegroundColor Yellow
dotnet publish $App `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=embedded `
  -o "$Dist\win-x64" `
  --no-restore

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "Output: $Dist\win-x64\" -ForegroundColor Green
Get-ChildItem "$Dist\win-x64\OpenDocEditor.exe" | Format-List Name, Length, LastWriteTime

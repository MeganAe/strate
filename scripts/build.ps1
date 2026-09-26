# Compile Strate sur une machine Windows où le SDK .NET 10 est installé.
# Usage :  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Write-Host "SDK :"
dotnet --version

dotnet publish .\src\Strate\Strate.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o .\artifacts\Strate

if (-not (Test-Path .\artifacts\Strate\Strate.exe)) {
    throw "La publication n'a pas produit Strate.exe."
}

Write-Host ""
Write-Host "Exécutable : .\artifacts\Strate\Strate.exe"
Write-Host "Ce dossier est autonome : le runtime .NET est inclus."

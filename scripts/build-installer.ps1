param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstallerCompile
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$publishDir = Join-Path $rootDir "dist\publish"
$outputDir = Join-Path $rootDir "dist\output"
$installerScript = Join-Path $rootDir "installer\AutoSys.iss"
$webView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
$webView2Installer = Join-Path $publishDir "MicrosoftEdgeWebView2Setup.exe"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format "yyyy.MM.dd.HHmm"
}

Write-Step "Limpiando carpeta dist"
if (Test-Path (Join-Path $rootDir "dist")) {
    Remove-Item (Join-Path $rootDir "dist") -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Write-Step "Publicando app .NET self-contained compatible con Windows 8/10"
dotnet publish (Join-Path $rootDir "Taller.csproj") -c Release -f net6.0-windows -r $Runtime --self-contained true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Falló dotnet publish. Revisa los errores de compilación antes de generar el instalador."
}

Write-Step "Descargando WebView2 Runtime"
Invoke-WebRequest -Uri $webView2Url -OutFile $webView2Installer

if ($SkipInstallerCompile) {
    Write-Step "Build finalizado (sin compilar instalador Inno Setup)"
    Write-Host "Publish: $publishDir"
    Write-Host "Siguiente paso: compilar installer\AutoSys.iss con Inno Setup"
    exit 0
}

Write-Step "Buscando Inno Setup (ISCC.exe)"
$iscc = Get-Command iscc -ErrorAction SilentlyContinue

if (-not $iscc) {
    $commonPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $iscc = @{ Source = $path }
            break
        }
    }
}

if (-not $iscc) {
    throw "No se encontro Inno Setup (ISCC.exe). Instala Inno Setup 6 y reintenta."
}

$isccPath = $iscc.Source
Write-Step "Compilando instalador compatible con Windows 8/10"

$env:AUTOSYS_VERSION = $Version
$env:AUTOSYS_SOURCE_DIR = $publishDir

& $isccPath $installerScript

Write-Step "Listo"
Write-Host "Instalador generado en: $outputDir"

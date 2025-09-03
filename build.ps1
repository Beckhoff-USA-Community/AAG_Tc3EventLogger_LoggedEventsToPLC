param(
    [switch]$SkipDotNet,
    [switch]$SkipPlcLibrary,
    [switch]$InstallLibrary
)

$ErrorActionPreference = "Stop"

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "Building ReadTc3Events2 - Multi-Platform Deployment with PLC Library" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""

# Change to ReadTc3Events2 directory for .NET builds
Set-Location "dotnetLoggedEventsToPLC\ReadTc3Events2"

if (-not $SkipDotNet) {
    Write-Host "[1/4] Building .NET Release version..." -ForegroundColor Yellow
    try {
        dotnet build -c Release
        if ($LASTEXITCODE -ne 0) { throw "Release build failed!" }
        Write-Host "V Release build completed" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        exit 1
    }

    Write-Host "[2/4] Creating framework-dependent publish for TwinCAT/BSD (FreeBSD)..." -ForegroundColor Yellow
    try {
        dotnet publish -c Release -o ..\..\build-artifacts\freebsd\ReadTc3Events2
        if ($LASTEXITCODE -ne 0) { throw "FreeBSD publish failed!" }
        Write-Host "V FreeBSD deployment package created: .\build-artifacts\freebsd\ReadTc3Events2\" -ForegroundColor Green
        Write-Host "  Usage: dotnet ReadTc3Events2.dll [arguments]" -ForegroundColor Gray
        Write-Host ""
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        exit 1
    }

    Write-Host "[3/4] Creating self-contained executable for Windows..." -ForegroundColor Yellow
    try {
        dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ..\..\build-artifacts\windows\ReadTc3Events2
        if ($LASTEXITCODE -ne 0) { throw "Windows publish failed!" }
        Write-Host "V Windows executable created: .\build-artifacts\windows\ReadTc3Events2\ReadTc3Events2.exe" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "[1-3/4] Skipping .NET builds..." -ForegroundColor Yellow
    Write-Host ""
}

# Change back to root directory
Set-Location "..\.."

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "Build Summary:" -ForegroundColor Cyan  
Write-Host "====================================================================" -ForegroundColor Cyan

if (-not $SkipDotNet) {
    Write-Host "TwinCAT/BSD (FreeBSD): .\build-artifacts\freebsd\ReadTc3Events2\" -ForegroundColor White
    Write-Host "  - Copy entire ReadTc3Events2 folder to target" -ForegroundColor Gray
    Write-Host "  - Run: dotnet ReadTc3Events2.dll --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2" -ForegroundColor Gray
    Write-Host "  - Requires: pkg install dotnet-runtime" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Windows: .\build-artifacts\windows\ReadTc3Events2\ReadTc3Events2.exe" -ForegroundColor White
    Write-Host "  - Copy entire ReadTc3Events2 folder to target, or just the .exe" -ForegroundColor Gray
    Write-Host "  - Run: ReadTc3Events2.exe --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "  .\build.ps1                    # Build .NET applications" -ForegroundColor Gray
Write-Host "  .\build.ps1 -SkipDotNet        # Skip .NET builds" -ForegroundColor Gray  
Write-Host ""
Write-Host "NOTE: PLC library generation temporarily disabled due to TwinCAT automation requirements." -ForegroundColor Yellow
Write-Host "      The PLC library can be manually created from LoggedEventsToPLCLib project." -ForegroundColor Yellow
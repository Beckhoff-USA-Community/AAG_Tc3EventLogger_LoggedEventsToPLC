@echo off
echo ====================================================================
echo Building ReadTc3Events2 - Multi-Platform Deployment
echo ====================================================================
echo.

cd ReadTc3Events2

echo [1/3] Building Release version...
dotnet build -c Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Release build failed!
    pause
    exit /b 1
)
echo ✓ Release build completed
echo.

echo [2/3] Creating framework-dependent publish for TwinCAT/BSD (FreeBSD)...
dotnet publish -c Release -o ..\publish-freebsd\ReadTc3Events2
if %ERRORLEVEL% neq 0 (
    echo ERROR: FreeBSD publish failed!
    pause
    exit /b 1
)
echo ✓ FreeBSD deployment package created: ..\publish-freebsd\ReadTc3Events2\
echo   Usage: dotnet ReadTc3Events2.dll [arguments]
echo.

echo [3/3] Creating self-contained executable for Windows...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ..\publish-win-x64\ReadTc3Events2
if %ERRORLEVEL% neq 0 (
    echo ERROR: Windows publish failed!
    pause
    exit /b 1
)
echo ✓ Windows executable created: ..\publish-win-x64\ReadTc3Events2\ReadTc3Events2.exe
echo.

echo ====================================================================
echo Build Summary:
echo ====================================================================
echo TwinCAT/BSD (FreeBSD): ..\publish-freebsd\ReadTc3Events2\
echo   - Copy entire ReadTc3Events2 folder to target
echo   - Run: dotnet ReadTc3Events2.dll --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2
echo   - Requires: pkg install dotnet-runtime
echo.
echo Windows: ..\publish-win-x64\ReadTc3Events2\ReadTc3Events2.exe
echo   - Copy entire ReadTc3Events2 folder to target, or just the .exe
echo   - Run: ReadTc3Events2.exe --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2
echo.
echo Build completed successfully!
echo ====================================================================
pause
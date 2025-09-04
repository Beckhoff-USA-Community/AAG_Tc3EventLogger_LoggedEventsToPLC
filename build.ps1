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

# PLC Library Generation
if (-not $SkipPlcLibrary) {
    Write-Host "[4/4] Generating PLC Library..." -ForegroundColor Yellow
    try {
        # Import MessageFilter for COM stability
        . "$PSScriptRoot\Script\MessageFilter.ps1"
        AddMessageFilterClass
        [EnvDTEUtils.MessageFilter]::Register()
        
        # Paths
        $solutionPath = Join-Path $PSScriptRoot "Test Bench\Test Bench.sln"
        $outputDir = Join-Path $PSScriptRoot "build-artifacts\plc-library"
        $libraryPath = Join-Path $outputDir "LoggedEventsToPLCLib.library"
        
        # Verify solution exists
        if (-not (Test-Path $solutionPath)) {
            throw "Solution file not found: $solutionPath"
        }
        
        # Create output directory if it doesn't exist
        if (-not (Test-Path $outputDir)) {
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        }
        
        # Delete existing library file if it exists
        if (Test-Path $libraryPath) {
            Remove-Item $libraryPath -Force
        }
        
        # Start TcXaeShell
        $dte = New-Object -ComObject TcXaeShell.DTE.17.0
        $dte.SuppressUI = $true
        $dte.MainWindow.Visible = $false
        
        # Open solution
        $solution = $dte.Solution
        $solution.Open($solutionPath)
        
        # Find the LoggedEventsToPLCLib project by name
        $targetProjectName = "LoggedEventsToPLCLib"
        $plcLibProject = $null
        
        foreach ($project in $solution.Projects) {
            if ($project.Name -eq $targetProjectName) {
                $plcLibProject = $project
                break
            }
        }
        
        if ($null -eq $plcLibProject) {
            throw "LoggedEventsToPLCLib project not found in solution (Name: $targetProjectName)"
        }
        
        # Get system manager and lookup PLC project tree item
        $systemManager = $plcLibProject.Object
        $plcTreeItem = $systemManager.LookupTreeItem("TIPC^LoggedEventsToPLC^LoggedEventsToPLC Project")
        
        if ($null -eq $plcTreeItem) {
            throw "PLC project tree item not found"
        }
        
        # Generate library
        # Second parameter: true = install to repository, false = just save to file
        $installToRepo = $InstallLibrary.IsPresent
        $plcTreeItem.SaveAsLibrary($libraryPath, $installToRepo)
        
        # Verify library file was created
        if (Test-Path $libraryPath) {
            $fileInfo = Get-Item $libraryPath
            Write-Host "V PLC library created: $($fileInfo.Name) ($([math]::Round($fileInfo.Length / 1KB, 2)) KB)" -ForegroundColor Green
        } else {
            throw "Library file was not created at expected location"
        }
        
        Write-Host ""
        
    } catch {
        Write-Host "ERROR: $_" -ForegroundColor Red
        exit 1
    } finally {
        # Clean up COM objects
        if ($null -ne $dte) {
            try {
                # Save all projects in the solution
                if ($null -ne $solution) {
                    for ($i = 1; $i -le $solution.Projects.Count; $i++) {
                        try {
                            $solution.Projects.Item($i).Save()
                        } catch {
                            # Ignore save errors for individual projects
                        }
                    }
                    
                    # Close solution without saving dialog (false = don't save)
                    $solution.Close($false)
                }
                
                # Quit DTE
                $dte.Quit()
            } catch {
                # Ignore cleanup errors
            }
        }
        [EnvDTEUtils.MessageFilter]::Revoke()
    }
}
else {
    Write-Host "[4/4] Skipping PLC library generation..." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "Build Summary:" -ForegroundColor Cyan  
Write-Host "====================================================================" -ForegroundColor Cyan

if (-not $SkipDotNet) {
    Write-Host "TwinCAT/BSD (FreeBSD): .\build-artifacts\freebsd\ReadTc3Events2\" -ForegroundColor White
    Write-Host "  - Copy entire ReadTc3Events2 folder to /usr/local/etc/TwinCAT/3.1/Components/Plc" -ForegroundColor Gray
    Write-Host "  - Run: dotnet ReadTc3Events2.dll --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2" -ForegroundColor Gray
    Write-Host "  - Requires: pkg install dotnet-runtime" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Windows: .\build-artifacts\windows\ReadTc3Events2\ReadTc3Events2.exe" -ForegroundColor White
    Write-Host "  - Copy entire ReadTc3Events2 folder to C:\Program Files (x86)\Beckhoff\TwinCAT\3.1\Components\Plc, or just the .exe" -ForegroundColor Gray
    Write-Host "  - Run: ReadTc3Events2.exe --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2" -ForegroundColor Gray
    Write-Host ""
}

if (-not $SkipPlcLibrary) {
    Write-Host "PLC Library: .\build-artifacts\plc-library\LoggedEventsToPLCLib.library" -ForegroundColor White
    Write-Host "  - Import in TwinCAT XAE: References > Add Library > Browse to library file" -ForegroundColor Gray
    Write-Host "  - Use FB_ReadTc3Events2 function block in your PLC project" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "  .\build.ps1                    # Build everything (.NET + PLC library)" -ForegroundColor Gray
Write-Host "  .\build.ps1 -SkipDotNet        # Only build PLC library" -ForegroundColor Gray
Write-Host "  .\build.ps1 -SkipPlcLibrary    # Only build .NET applications" -ForegroundColor Gray
Write-Host "  .\build.ps1 -InstallLibrary    # Build and install PLC library to TwinCAT repository" -ForegroundColor Gray
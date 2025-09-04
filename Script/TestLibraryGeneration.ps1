#
# TestLibraryGeneration.ps1
# Test script for TwinCAT Automation Interface library generation
#

# Import MessageFilter for COM stability
. "$PSScriptRoot\MessageFilter.ps1"

$ErrorActionPreference = "Stop"

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "Testing TwinCAT Automation Interface - Library Generation" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""

try {
    # Register MessageFilter for COM stability
    AddMessageFilterClass
    [EnvDTEUtils.MessageFilter]::Register()
    
    # Solution and project paths
    $solutionPath = Join-Path $PSScriptRoot "..\Test Bench\Test Bench.sln"
    $outputDir = Join-Path $PSScriptRoot "..\build-artifacts\plc-library"
    $libraryPath = Join-Path $outputDir "LoggedEventsToPLCLib.library"
    
    Write-Host "Solution Path: $solutionPath" -ForegroundColor Gray
    Write-Host "Library Output: $libraryPath" -ForegroundColor Gray
    Write-Host ""
    
    # Verify solution exists
    if (-not (Test-Path $solutionPath)) {
        throw "Solution file not found: $solutionPath"
    }
    
    # Create output directory if it doesn't exist
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        Write-Host "Created output directory: $outputDir" -ForegroundColor Yellow
    }
    
    # Delete existing library file if it exists
    if (Test-Path $libraryPath) {
        Remove-Item $libraryPath -Force
        Write-Host "Deleted existing library file: $libraryPath" -ForegroundColor Yellow
    }
    
    Write-Host "Starting TcXaeShell..." -ForegroundColor Yellow
    
    # Start TcXaeShell
    $dte = New-Object -ComObject TcXaeShell.DTE.17.0
    
    # Configure DTE
    $dte.SuppressUI = $true
    $dte.MainWindow.Visible = $false
    
    Write-Host "Opening solution..." -ForegroundColor Yellow
    
    # Open solution
    $solution = $dte.Solution
    $solution.Open($solutionPath)
    
    Write-Host "Solution opened successfully. Projects found: $($solution.Projects.Count)" -ForegroundColor Green
    
    # Find the LoggedEventsToPLCLib project by name
    $targetProjectName = "LoggedEventsToPLCLib"
    $plcLibProject = $null
    
    foreach ($project in $solution.Projects) {
        Write-Host "Project: '$($project.Name)'" -ForegroundColor Gray
        if ($project.Name -eq $targetProjectName) {
            $plcLibProject = $project
            break
        }
    }
    
    if ($null -eq $plcLibProject) {
        throw "LoggedEventsToPLCLib project not found in solution (Name: $targetProjectName)"
    }
    
    Write-Host "Found LoggedEventsToPLCLib project: $($plcLibProject.Name)" -ForegroundColor Green
    
    # Get system manager from project
    $systemManager = $plcLibProject.Object
    
    Write-Host "Looking up PLC project tree item..." -ForegroundColor Yellow
    
    # Look up the PLC project tree item
    # The path format should be "TIPC^LoggedEventsToPLC^LoggedEventsToPLC Project"
    $plcTreeItem = $systemManager.LookupTreeItem("TIPC^LoggedEventsToPLC^LoggedEventsToPLC Project")
    
    if ($null -eq $plcTreeItem) {
        throw "PLC project tree item not found. Available tree items:"
        # List available tree items for debugging
        $systemManager.GetTreeItems() | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }
    }
    
    Write-Host "Found PLC project tree item: $($plcTreeItem.Name)" -ForegroundColor Green
    
    # Cast to ITcPlcIECProject interface
    Write-Host "Casting to ITcPlcIECProject interface..." -ForegroundColor Yellow
    $iecProject = $plcTreeItem
    
    Write-Host "Generating library file..." -ForegroundColor Yellow
    
    # Call SaveAsLibrary method
    # Parameters: libraryPath, installToRepository (false)
    $iecProject.SaveAsLibrary($libraryPath, $false)
    
    Write-Host "Library generation completed!" -ForegroundColor Green
    
    # Verify library file was created
    if (Test-Path $libraryPath) {
        $fileInfo = Get-Item $libraryPath
        Write-Host "Library file created successfully:" -ForegroundColor Green
        Write-Host "  Path: $($fileInfo.FullName)" -ForegroundColor Gray
        Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
        Write-Host "  Modified: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
    } else {
        Write-Host "WARNING: Library file was not created at expected location!" -ForegroundColor Red
    }
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    $exitCode = 1
} finally {
    # Clean up COM objects
    Write-Host "" 
    Write-Host "Cleaning up..." -ForegroundColor Yellow
    
    if ($null -ne $dte) {
        try {
            # Save all projects in the solution
            if ($null -ne $solution) {
                for ($i = 1; $i -le $solution.Projects.Count; $i++) {
                    try {
                        $solution.Projects.Item($i).Save()
                        Write-Host "  Saved project: $($solution.Projects.Item($i).Name)" -ForegroundColor Gray
                    } catch {
                        Write-Host "  Warning: Could not save project $i" -ForegroundColor Yellow
                    }
                }
                
                # Close solution without saving dialog (false = don't save)
                $solution.Close($false)
                Write-Host "  Solution closed" -ForegroundColor Gray
            }
            
            # Quit DTE
            $dte.Quit()
            Write-Host "  DTE closed" -ForegroundColor Gray
        } catch {
            Write-Host "Warning: Error during cleanup: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    # Revoke MessageFilter
    [EnvDTEUtils.MessageFilter]::Revoke()
    
    Write-Host "Cleanup completed." -ForegroundColor Green
}

Write-Host ""
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "Test completed." -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

if ($exitCode -eq 1) {
    Exit 1
} else {
    Exit 0
}
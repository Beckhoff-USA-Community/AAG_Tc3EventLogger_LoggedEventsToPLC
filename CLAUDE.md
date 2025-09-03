# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This repository contains a proof-of-concept integration between TwinCAT Event Logger and PLC systems, consisting of two main components:

1. **dotnetLoggedEventsToPLC** - A .NET 8 console application that reads events from TwinCAT Event Logger and writes them to PLC arrays
2. **TwinCATLoggedEventsToPLC** - A TwinCAT PLC project that provides the receiving end for logged events

## Architecture

### .NET Application (dotnetLoggedEventsToPLC)
- **Language**: C# (.NET 8.0)
- **Main Project**: `ReadTc3Events2`
- **Key Dependencies**: 
  - Beckhoff.TwinCAT.Ads (6.2.485) - ADS communication
  - Beckhoff.TwinCAT.TcEventLoggerAdsProxy.Net (2.8.33) - Event Logger integration
  - CommandLineParser (2.9.1) - CLI argument parsing

### TwinCAT PLC Project (TwinCATLoggedEventsToPLC)
- **Project Name**: FB_ReadTc3Events2
- **Main Components**:
  - `MAIN.TcPOU` - Main program entry point
  - `FB_ReadTc3Events2.TcPOU` - Function block for event processing
  - `PRG_GenerateEvents.TcPOU` - Event generation for testing

## Development Commands

### .NET Application

**Build the application:**
```powershell
# Development build
cd dotnetLoggedEventsToPLC/ReadTc3Events2
dotnet build

# Multi-platform deployment build (PowerShell - Recommended)
.\build.ps1

# Legacy batch file (deprecated)  
cd dotnetLoggedEventsToPLC
.\build.bat
```

**PowerShell Build Script Options:**
```powershell
.\build.ps1                    # Build everything (.NET + PLC library)
.\build.ps1 -SkipDotNet        # Only build PLC library
.\build.ps1 -SkipPlcLibrary    # Only build .NET applications  
.\build.ps1 -InstallLibrary    # Build and install PLC library to TwinCAT repository
```

**Build Script Output:**
- `build-artifacts/freebsd/ReadTc3Events2/` - Framework-dependent build for TwinCAT/BSD (FreeBSD)
- `build-artifacts/windows/ReadTc3Events2/` - Self-contained executable for Windows
- `build-artifacts/plc-library/LoggedEventsToPLCLib.library` - TwinCAT PLC library

**Run the application:**
```bash
cd dotnetLoggedEventsToPLC/ReadTc3Events2
# Local TwinCAT (uses default 127.0.0.1.1.1:851)
dotnet run -- --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2

# Remote TwinCAT
dotnet run -- --amsnetid <AMS_NET_ID>:851 --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2
```

**Deployment Examples:**

*TwinCAT/BSD (FreeBSD):*
```bash
# Copy build-artifacts/freebsd/ReadTc3Events2/ folder to target, then run:
dotnet ReadTc3Events2.dll --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2
```

*Windows:*
```bash
# Copy build-artifacts/windows/ReadTc3Events2/ folder to target, then run:
ReadTc3Events2.exe --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2
```

**Development runs (from launchSettings.json):**
- Local English: `--symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2`
- Local German: `--symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1031 --datetimeformat 0`
- Remote: `--amsnetid 39.120.71.102.1.1:851 --symbolpath MAIN.fbReadTc3Events.LoggedEvents --languageid 1033 --datetimeformat 2`
- Verbose mode: Add `--verbose` flag for detailed TwinCAT logging

### TwinCAT Project

**Open TwinCAT project:**
```bash
# Open the solution file in TwinCAT XAE
TwinCATLoggedEventsToPLC/FB_ReadTc3Events2.sln
```

**Using the PLC Library:**
1. **Import Library**: In TwinCAT XAE, right-click References > Add Library > Browse to `build-artifacts/plc-library/LoggedEventsToPLCLib.library`
2. **Add Function Block**: Declare `fbReadTc3Events : FB_ReadTc3Events2;` in your program
3. **Call Method**: Use `fbReadTc3Events.ReadLoggedEvents(bExec);` to trigger event reading
4. **Access Events**: Read events from `fbReadTc3Events.LoggedEvents` array

**Library Contents:**
- `FB_ReadTc3Events2` - Main function block that calls the .NET application
- `ST_ReadEventW` - Event structure matching .NET application format
- Automatic path detection for Windows vs TwinCAT/BSD deployment

## Key Data Structures

### ST_ReadEventW Structure
The core data structure for event transfer between .NET and PLC:
- `nSourceID` (UDINT) - Event source identifier
- `nEventID` (UDINT) - Event identifier  
- `nClass` (UDINT) - Event classification (2=Message, 7=Alarm)
- `nConfirmState` (UDINT) - Confirmation state (0-4)
- `nResetState` (UDINT) - Reset state for alarms
- `sSource` (WSTRING(255)) - Source description
- `sDate` (WSTRING(23)) - Formatted date
- `sTime` (WSTRING(23)) - Formatted time
- `sComputer` (WSTRING(80)) - Computer/severity info
- `sMessageText` (WSTRING(255)) - Event message text
- `bQuitMessage` (BOOL) - Quit message flag
- `bConfirmable` (BOOL) - Confirmable flag

### Command Line Arguments
- `--amsnetid`: (Optional) TwinCAT AMS Net ID with port (format: x.x.x.x.x.x:port). Defaults to 127.0.0.1.1.1:851 for local connections
- `--symbolpath`: Full PLC symbol path to event array
- `--languageid`: Language ID (1033=English, 1031=German, 2057=English UK)
- `--datetimeformat`: Format enum (0=de_DE, 1=en_GB, 2=en_US)
- `--verbose`: Enable verbose logging to TwinCAT Event Logger

## Application Flow

1. **Argument Parsing & Validation** - Validates AMS Net ID format, symbol paths, and parameters
2. **System Connection** - Connects to both TwinCAT Event Logger and ADS Client
3. **Array Validation** - Verifies PLC array exists and contains ST_ReadEventW structures
4. **Event Retrieval** - Gets logged events from TwinCAT Event Logger
5. **Event Processing** - Converts events to PLC format with localization
6. **PLC Writing** - Writes processed events to PLC array
7. **Cleanup** - Properly disconnects all connections

## Localization Support

The application supports multiple date/time formats and languages:
- **German (de_DE)**: dd.MM.yyyy HH:mm:ss
- **English UK (en_GB)**: dd/MM/yyyy HH:mm:ss  
- **English US (en_US)**: MM/dd/yyyy h:mm:ss tt

## Error Handling

The application includes comprehensive error logging to TwinCAT Event Logger with JSON-formatted additional data for debugging and monitoring.
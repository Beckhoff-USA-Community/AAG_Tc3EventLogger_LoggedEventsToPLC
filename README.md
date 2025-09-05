# TwinCAT Event Logger to PLC Integration

Bridge TwinCAT Event Logger data to PLC arrays for real-time monitoring and automation workflows.

## What This Is

This proof-of-concept demonstrates seamless integration between TwinCAT Event Logger and PLC systems:

- **Read logged events** from TwinCAT Event Logger
- **Convert and format** events for PLC consumption  
- **Write to PLC arrays** via ADS communication
- **Multi-language support** (English, German, English UK)
- **Cross-platform deployment** (Windows, TwinCAT/BSD)

## Quick Start

### 1. Prerequisites
- .NET 8 SDK
- TwinCAT XAE Shell (for PLC library development)
- TwinCAT runtime with TwinCAT 3 Event Logger
- TwinCAT project using [TF1800 | TwinCAT 3 PLC HMI](https://infosys.beckhoff.com/content/1033/tf1800_tc3_plc_hmi/index.html?id=9090092027299420151) or [TF1810 | TwinCAT 3 PLC HMI Web](https://infosys.beckhoff.com/content/1033/tf1810_tc3_plc_hmi_web/index.html?id=5545791418639730350) and the [Event table](https://infosys.beckhoff.com/content/1033/tc3_plc_intro/3524166155.html?id=4373836669159094324)

### 2. Build
```powershell
.\build.ps1
```
This creates deployments for both Windows and TwinCAT/BSD systems.

### 3. Deploy & Run

**TwinCAT/BSD:**
```bash
# Copy build-artifacts/freebsd/ReadTc3Events2/ to target
dotnet ReadTc3Events2.dll --symbolpath MAIN.fbReadTc3Events --languageid 1033 --datetimeformat 2
```

**Windows:**
```bash
# Copy build-artifacts/windows/ReadTc3Events2/ to target  
ReadTc3Events2.exe --symbolpath MAIN.fbReadTc3Events --languageid 1033 --datetimeformat 2
```

### 4. PLC Integration
1. Import the PLC library: `build-artifacts/plc-library/LoggedEventsToPLCLib.library`
2. Add function block: `fbReadTc3Events : FB_ReadTc3Events2;`
3. Call: `fbReadTc3Events.ReadLoggedEvents(bExec);`
4. Access events: `fbReadTc3Events.LoggedEvents[]`

## Architecture

- **.NET Application** (`dotnetLoggedEventsToPLC/`) - Core event processing engine
- **PLC Library** (`LoggedEventsToPLCLib/`) - TwinCAT function blocks and data structures
- **Test Bench** (`Test Bench/`) - Development and testing environment

## Development Setup

**VS Code (Recommended):**
1. Open `Tc3EventLogger_LoggedEventsToPLC.code-workspace`
2. Install recommended extensions
3. Press `F5` to debug

**Build Options:**
```powershell
.\build.ps1                    # Build everything
.\build.ps1 -SkipPlcLibrary    # .NET only
.\build.ps1 -SkipDotNet        # PLC library only
```


## Documentation

- **[VSCode-Setup.md](VSCode-Setup.md)** - Development environment setup
- **PLC Library** - Auto-detects Windows vs TwinCAT/BSD deployment paths


## Disclaimer
All sample code provided by Beckhoff Automation LLC are for illustrative purposes only and are provided “as is” and without any warranties, express or implied. Actual implementations in applications will vary significantly. Beckhoff Automation LLC shall have no liability for, and does not waive any rights in relation to, any code samples that it provides or the use of such code samples for any purpose.
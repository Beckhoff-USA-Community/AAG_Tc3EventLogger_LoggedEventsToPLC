# TwinCAT Event Logger to PLC Integration

Bridge TwinCAT 3 Event Logger logged events to PLC arrays.

## What This Is

This proof-of-concept demonstrates seamless integration between TwinCAT Event Logger and the PLC:

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

## PLC Function Block Usage

### FB_ReadTc3Events2

The `FB_ReadTc3Events2` function block extends the [FB_ReadTc3Events](https://infosys.beckhoff.com/content/1033/tc3_plc_intro/11028199435.html?id=6310535699191927314) functionality to integrate with the .NET application.

**Declaration:**
```iec
VAR
    fbReadTc3Events : FB_ReadTc3Events2;
    bTrigger : BOOL;
END_VAR
```

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `nLanguageID` | DINT | Language of event messages (1033=EN, 1031=DE, 2057=EN-UK) |
| `eDateAndTimeFormat` | E_DateAndTimeFormat | Date/time format (0=de_DE, 1=en_GB, 2=en_US) |
| `bClearLoggedTable` | BOOL | Clear the LoggedEvents array |
| `TimeOut` | TIME | Timeout for .NET application response |
| `LoggedEvents` | ARRAY[1..80] OF ST_ReadEventW | Array to connect to the event table  |

**Usage Example:**
```iec
// Configure the function block
fbReadTc3Events.nLanguageID := 1033;           // English US
fbReadTc3Events.eDateAndTimeFormat := 2;       // MM/dd/yyyy format

//Service the FB
fbReadTc3Events();

// Trigger event reading with rising edge
IF bTrigger THEN
    IF fbReadTc3Events.ReadLoggedEvents(bTrigger) THEN
        // Events successfully retrieved
        // Access via fbReadTc3Events.LoggedEvents[1..80]
    END_IF
END_IF
```

**ReadLoggedEvents Method:**
- **Input**: `bExec` - Rising edge triggers the read operation
- **Returns**: `BOOL` - TRUE if successful, FALSE if failed
- **Output**: `hrErrorCode` - HRESULT error code if operation fails

The method calls the .NET application asynchronously and populates the `LoggedEvents` array with formatted event data.

## Documentation

- **[VSCode-Setup.md](VSCode-Setup.md)** - Development environment setup
- **PLC Library** - Auto-detects Windows vs TwinCAT/BSD deployment paths


## Disclaimer
All sample code provided by Beckhoff Automation LLC are for illustrative purposes only and are provided “as is” and without any warranties, express or implied. Actual implementations in applications will vary significantly. Beckhoff Automation LLC shall have no liability for, and does not waive any rights in relation to, any code samples that it provides or the use of such code samples for any purpose.

# Visual Studio Code Setup

This guide will help you set up Visual Studio Code (VS Code) for this TwinCAT Event Logger project. This assumes you have some coding experience but may be new to VS Code.

## Quick Setup

### Install VS Code and Extensions

1. **Download VS Code** from [https://code.visualstudio.com/](https://code.visualstudio.com/) if needed
2. **Open the workspace**: `File` → `Open Workspace from File...` → select `AAG_Tc3EventLogger_LoggedEventsToPLC.code-workspace`
3. **Install extensions**: VS Code will prompt to install recommended extensions (C# Dev Kit, PowerShell, etc.) - click "Install All"

### Project Structure

The workspace is organized into logical folders:
- **.NET Application** - Main C# project (`dotnetLoggedEventsToPLC/ReadTc3Events2`)
- **Project Root** - Build scripts, documentation, and configuration files

## Development Workflow

### Debug Configurations

Pre-configured launch profiles for different scenarios:
- **ReadTc3Events2 (English - Local)** - Local TwinCAT testing
- **ReadTc3Events2 (English - Remote)** - Remote TwinCAT system  
- **ReadTc3Events2 (Verbose - Local)** - Detailed logging enabled
- **ReadTc3Events2 (Error - Remote)** - Error testing configuration

Press `F5` to run with debugging, `Ctrl+F5` for release mode.

### Build Tasks

Available via `Ctrl+Shift+B` or `Ctrl+Shift+P` → "Tasks: Run Task":

- **Build Everything** - Complete build including PLC library
- **Build .NET Only** - C# application only
- **Build PLC Library Only** - TwinCAT library generation

### Terminal Commands

Open integrated terminal (`Ctrl+``) for direct commands:

```powershell
# Full build (both .NET and PLC library)
.\build.ps1

# .NET only
.\build.ps1 -SkipPlcLibrary

# Manual run
cd dotnetLoggedEventsToPLC\ReadTc3Events2
dotnet run -- --symbolpath MAIN.fbReadTc3Events --languageid 1033 --datetimeformat 2
```

## Folder Structure

The workspace is organized into logical folders:

- **.NET Application** - Main C# project (`dotnetLoggedEventsToPLC/ReadTc3Events2`)
- **Build Scripts** - PowerShell automation scripts (`Script/`)
- **Project Root** - Root level files and documentation


## Common Beginner Tips

### Keyboard Shortcuts (Time Savers!)

| Shortcut | What it does |
|----------|-------------|
| `F5` | Start debugging (run your program) |
| `Ctrl+F5` | Run without debugging (faster startup) |
| `Ctrl+Shift+P` | Open Command Palette (access all commands) |
| `Ctrl+Shift+B` | Build the project |
| `Ctrl+`` | Open/close integrated terminal |
| `F12` | Go to definition of a function/variable |
| `Shift+F12` | Find all places where something is used |
| `F2` | Rename a variable everywhere it's used |
| `Ctrl+/` | Comment/uncomment selected lines |

### Using the Integrated Terminal

VS Code has a built-in terminal (command line) at the bottom:

1. Open it with `Ctrl+`` (backtick key)
2. It opens in PowerShell by default on Windows
3. You can run commands like:

```powershell
# Build the entire project (both .NET and PLC library)
.\build.ps1

# Build only the .NET application  
.\build.ps1 -SkipPlcLibrary

# Navigate to the .NET project and run it manually
cd dotnetLoggedEventsToPLC\ReadTc3Events2
dotnet run -- --symbolpath MAIN.fbReadTc3Events --languageid 1033 --datetimeformat 2
```

## VS Code Features

### Essential Shortcuts

| Shortcut | Action |
|----------|--------|
| `F5` | Start debugging |
| `Ctrl+F5` | Run without debugging |
| `Ctrl+Shift+B` | Build |
| `Ctrl+Shift+P` | Command palette |
| `F12` | Go to definition |
| `Shift+F12` | Find references |
| `F2` | Rename symbol |


### Troubleshooting

**C# features not working?** Install C# Dev Kit extension and restart VS Code.

**Build errors?** Ensure .NET 8 SDK is installed: `dotnet --version`

**Workspace issues?** Open the `.code-workspace` file, not just the folder.

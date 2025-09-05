# Visual Studio Code Setup

This project is now fully configured for development with Visual Studio Code.

## Quick Start

1. **Install Required Extensions:**
   - Open VS Code
   - Open the project workspace: `AAG_Tc3EventLogger_LoggedEventsToPLC.code-workspace`
   - VS Code will automatically prompt to install recommended extensions
   - Or manually install: `C# Dev Kit` and `C#` extensions from Microsoft

2. **Open the Project:**
   - File > Open Workspace from File...
   - Select `AAG_Tc3EventLogger_LoggedEventsToPLC.code-workspace`

## Development Features

### Debug Configurations
All your existing launch profiles from Visual Studio have been converted to VS Code debug configurations:

- **ReadTc3Events2 (English - Local)** - Default local configuration
- **ReadTc3Events2 (English - Remote)** - Remote TwinCAT system
- **ReadTc3Events2 (German - Local/Remote)** - German localization
- **ReadTc3Events2 (English UK - Local/Remote)** - UK English localization  
- **ReadTc3Events2 (Verbose - Local)** - Enable verbose logging
- **ReadTc3Events2 (Error - Remote)** - Error testing configuration

### Build Tasks
Available via `Ctrl+Shift+P` > "Tasks: Run Task":

- **build** (default) - Debug build
- **build-release** - Release build
- **publish-freebsd** - Framework-dependent FreeBSD build
- **publish-windows** - Self-contained Windows executable
- **clean** - Clean build artifacts
- **restore** - Restore NuGet packages
- **run-local-english** - Run with local English settings
- **run-verbose** - Run with verbose logging

### Workspace Tasks
Available from the workspace level:

- **Build Everything** - Run the full build.ps1 script (.NET + PLC Library)
- **Build .NET Only** - Run build.ps1 -SkipPlcLibrary
- **Build PLC Library Only** - Run build.ps1 -SkipDotNet  
- **Test Library Generation** - Run the standalone library generation test

## Folder Structure

The workspace is organized into logical folders:

- **.NET Application** - Main C# project (`dotnetLoggedEventsToPLC/ReadTc3Events2`)
- **Build Scripts** - PowerShell automation scripts (`Script/`)
- **Project Root** - Root level files and documentation

## IntelliSense & Features

VS Code provides full .NET development features:

- ✅ **IntelliSense** - Code completion and suggestions
- ✅ **Debugging** - Full debugging with breakpoints
- ✅ **Error Squiggles** - Real-time error detection
- ✅ **Code Actions** - Quick fixes and refactoring
- ✅ **Go to Definition** - Navigate to symbol definitions
- ✅ **Find References** - Find all usages of symbols
- ✅ **Rename Symbol** - Rename across entire codebase
- ✅ **Format Document** - Automatic code formatting
- ✅ **NuGet Integration** - Package management

## Keyboard Shortcuts

- `F5` - Start debugging (will prompt for configuration)
- `Ctrl+F5` - Run without debugging
- `Ctrl+Shift+P` - Command palette
- `Ctrl+Shift+B` - Run build task
- `F12` - Go to definition
- `Shift+F12` - Find all references
- `F2` - Rename symbol

## Terminal Integration

VS Code's integrated terminal defaults to PowerShell on Windows, making it easy to run:

```powershell
# Build everything
.\build.ps1

# Build just .NET
.\build.ps1 -SkipPlcLibrary

# Test library generation
.\Script\TestLibraryGeneration.ps1

# Run the application
cd dotnetLoggedEventsToPLC\ReadTc3Events2
dotnet run -- --symbolpath MAIN.fbReadTc3Events --languageid 1033 --datetimeformat 2
```

## Compatibility

- ✅ **Cross-platform** - Works on Windows, macOS, and Linux
- ✅ **Lightweight** - Much faster startup than Visual Studio 2022
- ✅ **Same tooling** - Uses identical `dotnet` commands and MSBuild
- ✅ **Git integration** - Built-in source control
- ✅ **Extensions** - Rich ecosystem of extensions

Your existing code requires **zero changes** - everything works identically to Visual Studio 2022!
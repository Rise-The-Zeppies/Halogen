# Halogen for Windows

Native Windows desktop editor/runtime written in C# with Windows Forms and .NET 8.

## Download the EXE
Open the repository's Actions tab and download the `Halogen-Windows-x64` artifact from a successful **Build Halogen for Windows** run. Extract it and run `Halogen.exe` on 64-bit Windows.

## Build locally
Install the .NET 8 SDK, then run from the repository root in PowerShell:

```powershell
dotnet publish .\Windows\HalogenWindows.csproj -c Release -r win-x64 --self-contained true -o .\Windows\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

Halogen is experimental. The editor supports .hg files, console output, ask() input, variables, comments, rpt loops, conditions, and basic functions. HalogenUI is still in development.

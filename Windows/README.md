# Halogen for Windows

Native Windows desktop editor/runtime written in C# and Windows Forms on .NET 8.

## Run and edit

Run Halogen.exe on 64-bit Windows. The editor opens with a runnable HalogenUI demo. You can edit it, press Run, open another .hg file, and save your code.

## Package a HalogenUI project

From PowerShell at the repository root:

~~~powershell
.\Windows\package_ui.ps1 .\Windows\Examples\halogenui_demo.hg "My Halogen App"
~~~

This publishes a self-contained Windows app folder containing My Halogen App.exe and Main.hg, then creates a ZIP distribution. Running the packaged EXE opens the bundled UI directly. The standard Windows build remains the Halogen editor.

## Build the editor locally

Install the .NET 8 SDK, then run:

~~~powershell
dotnet publish .\Windows\HalogenWindows.csproj -c Release -r win-x64 --self-contained true -o .\Windows\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
~~~

## HalogenUI features

- ui.declare [...] end for title, window size, and optional icon path.
- ui.background("#101820")
- ui.rect(x, y, width, height, "#RRGGBB")
- ui.circle(x, y, diameter, "#RRGGBB")
- ui.text("Hello", x, y, "#RRGGBB")
- ui.show()
- ui.framerpt N:, ui.secrpt N:, ui.minrpt N:, and ui.hrsrpt N:

The renderer refreshes around 60 FPS without sleeping the UI thread. Re-running source resets frame/time counters. The first pass accepts literal drawing values; variables, math+ imports in UI expressions, input events, buttons, and callbacks remain future work.

Use a .ico file for Windows window icons. The macOS app supports native .icns/image support.

Platform note: .app, .dmg, and .pkg are macOS distribution formats. Windows exports use .exe and .zip. A data .iso is not a bootable operating system; see HalogenOS/README.md for the OS plan.

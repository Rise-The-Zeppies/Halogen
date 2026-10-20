# Halogen

Halogen is an experimental programming language and UI runtime. Source files use the .hg extension.

## Version 0.2 direction

- Cross-platform HalogenUI preview: SwiftUI on macOS; Windows Forms on Windows.
- macOS project exporter for .app, .dmg, .pkg, and a data .iso.
- Windows project exporter for a self-contained .exe plus .zip.
- A separate HalogenOS architecture track for a future bootable OS image.

## macOS: build and export

~~~bash
cd MacOS
bash build_app.command
~~~

Export a standalone HalogenUI project:

~~~bash
bash MacOS/package_ui.command MacOS/Examples/hello_ui.hg "My Halogen App" all
~~~

Output is written to dist/ by default.

## Windows: build and export

~~~powershell
dotnet publish .\Windows\HalogenWindows.csproj -c Release -r win-x64 --self-contained true -o .\Windows\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
.\Windows\package_ui.ps1 .\Windows\Examples\halogenui_demo.hg "My Halogen App"
~~~

## Current HalogenUI commands

- ui.declare [...] end: window title, size, and optional platform icon.
- ui.background("#RRGGBB")
- ui.rect(x, y, width, height, "#RRGGBB")
- ui.circle(x, y, diameter, "#RRGGBB")
- ui.text("label", x, y, "#RRGGBB")
- ui.show()
- ui.framerpt N:, ui.secrpt N:, ui.minrpt N:, ui.hrsrpt N:

V0.2 packaging embeds literal UI source into a standalone app. Variable/expression evaluation inside drawing commands, buttons, input events, and the HalogenOS runtime are still under development.

Important format distinction: the generated .iso is distribution media, not a bootable OS. See HalogenOS/README.md for the separate OS engineering plan.

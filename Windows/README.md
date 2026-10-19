# Halogen for Windows

Native Windows desktop editor/runtime written in C# and Windows Forms on .NET 8.

## Download the EXE

Open the repository's Actions tab and download the \`Halogen-Windows-x64\` artifact from a successful **Build Halogen for Windows** run. Extract it and run \`Halogen.exe\` on 64-bit Windows.

## Build locally

Install the .NET 8 SDK, then run from the repository root in PowerShell:

\`\`\`powershell
dotnet publish .\Windows\HalogenWindows.csproj -c Release -r win-x64 --self-contained true -o .\Windows\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
\`\`\`

## HalogenUI preview

Open \`Examples\halogenui_demo.hg\` in the editor and click **Run** to open a native preview window.

Supported first-pass drawing commands:

- \`ui.declare [...] end\` for title and window size.
- \`ui.background("#101820")\`
- \`ui.rect(x, y, width, height, "#RRGGBB")\`
- \`ui.circle(x, y, diameter, "#RRGGBB")\`
- \`ui.text("Hello", x, y, "#RRGGBB")\`
- \`ui.show()\`

The frame/timing blocks \`ui.framerpt N:\`, \`ui.secrpt N:\`, \`ui.minrpt N:\`, and \`ui.hrsrpt N:\` do not sleep the UI thread; drawing commands are conditionally displayed while their duration is active. The first version accepts literal drawing arguments and does not yet support normal Halogen variables/imports inside UI commands.

On Windows, use an \`.ico\` file for the optional window icon. The macOS app supports \`.icns\`.

Halogen remains experimental. Regular scripting syntax includes \`write()\`, \`ask()\`, variables, comments, repeat loops, conditions, and basic functions.

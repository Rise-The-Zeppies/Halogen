# Halogen for Windows

Native Windows desktop editor/runtime written in C# and Windows Forms on .NET 8.

## Launch

Run \`Halogen.exe\` on 64-bit Windows. The editor opens with a runnable HalogenUI demo. You can edit it, press **Run**, open another \`.hg\` file, and save your code. You can also launch a source file directly by passing its path to \`Halogen.exe\`.

## Build locally

Install the .NET 8 SDK, then run from the repository root in PowerShell:

\`\`\`powershell
dotnet publish .\Windows\HalogenWindows.csproj -c Release -r win-x64 --self-contained true -o .\Windows\publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
\`\`\`

## HalogenUI features

The Windows renderer provides the same first-pass drawing features as the macOS starter:

- \`ui.declare [...] end\` for title, window size, and optional icon path.
- \`ui.background("#101820")\` for the canvas background.
- \`ui.rect(x, y, width, height, "#RRGGBB")\` for rectangles.
- \`ui.circle(x, y, diameter, "#RRGGBB")\` for circles.
- \`ui.text("Hello", x, y, "#RRGGBB")\` for text.
- \`ui.show()\` to display the rendered interface.
- \`ui.framerpt N:\`, \`ui.secrpt N:\`, \`ui.minrpt N:\`, and \`ui.hrsrpt N:\` for non-blocking frame- and duration-based drawing blocks.

The renderer updates at approximately 60 FPS without sleeping the UI thread. Re-running the source updates the existing preview and resets its frame/time counters. If UI drawing commands are present without \`ui.show()\`, a placeholder screen is displayed, matching the macOS starter.

For Windows window icons, use an \`.ico\` path with \`icon? = T "path"\`. The macOS app uses its native \`.icns\`/image support.

This is the same scope as the macOS first pass: UI commands currently accept literal values. Regular Halogen variable assignments, \`from math+ import ...\`, \`halolib\`, input events, buttons, and user-defined functions inside UI drawing expressions are not yet integrated.

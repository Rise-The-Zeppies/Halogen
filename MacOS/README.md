# HalogenUI for macOS

Native SwiftUI preview/runtime targeting macOS 12 Monterey or newer.

## Build the editor/preview

From the repository root:

~~~bash
cd MacOS
swift build -c release
bash build_app.command
~~~

This creates build/HalogenUI.app.

## Export a standalone HalogenUI project

From the repository root:

~~~bash
bash MacOS/package_ui.command MacOS/Examples/hello_ui.hg "My Halogen App" all
~~~

The exporter embeds the selected .hg source as Contents/Resources/Main.hg. The resulting .app runs that source automatically without the developer toolbar. Output is written to dist/ by default.

Formats:
- **.app:** standalone macOS application bundle.
- **.dmg:** compressed disk image containing the app.
- **.pkg:** macOS installer package that installs the app to /Applications.
- **.iso:** data-disc ISO containing the generated app and installer materials. This is **not** a bootable operating-system ISO.

Use app, dmg, pkg, or iso as the third argument to generate a narrower selection; use all for all formats. An optional fourth argument sets the output directory.

The script uses macOS-native packaging tools (hdiutil, pkgbuild, plutil), so it must run on a Mac. Apps and packages are unsigned and not notarized; distributing outside your own Mac may trigger Gatekeeper. A Developer ID certificate and Apple notarization credentials are needed for a trusted public release.

## Supported HalogenUI syntax

~~~halogen
ui.declare [
    command.parameter:
        (title: "My App")
        (size: 900x620)
        (icon? = T "~/Downloads/app.icns")
]
end

ui.background("#101820")
ui.rect(80, 80, 240, 140, "#00D9FF")
ui.circle(400, 100, 100, "#FFB000")
ui.text("Hello, Halogen!", 80, 270, "#FFFFFF")
ui.show()
~~~

Also supported: ui.framerpt, ui.secrpt, ui.minrpt, and ui.hrsrpt. Drawing arguments are currently literals. Regular variables, from math+ import ..., interactive controls, and callbacks are not yet wired into UI expressions.

For the long-term bootable OS image plan, see HalogenOS/README.md.

# HalogenUI for macOS

This is the first source-controlled native macOS renderer for HalogenUI. It targets macOS 12 Monterey or newer and uses SwiftUI's native drawing canvas.

## Build on macOS

Install Xcode Command Line Tools, then from the repository root run:

~~~bash
cd MacOS
swift build -c release
~~~

To create a double-clickable app bundle, run this from the MacOS folder:

~~~bash
bash build_app.command
~~~

The bundle is written to build/HalogenUI.app at the repository root.

## Run a Halogen source file

Open build/HalogenUI.app, click **Open .hg…**, choose an example, and click **Run**. You can also pass a source file to the executable:

~~~bash
cd MacOS
.build/release/HalogenMacOS Examples/hello_ui.hg
~~~

## Initial syntax

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

Supported rendering commands in this first pass: ui.background, ui.rect, ui.circle, ui.text, and ui.show. Window declaration supports title, size, and the optional icon? = T "path" setting. Colors use #RRGGBB.

The repetition blocks do not sleep or freeze the window. ui.framerpt N: activates its body on the first N render frames; ui.secrpt N:, ui.minrpt N:, and ui.hrsrpt N: activate their bodies until the corresponding elapsed duration has passed. The timeline runs at approximately 60 Hz.

Limitations of this first renderer: it currently reads literal UI drawing commands only. It does not yet execute regular Halogen variable assignments, imports, math+, input events, or user-defined functions inside the UI blocks. The next integration step is connecting these statements to Halogen's main interpreter so UI scripts can use normal Halogen variables.

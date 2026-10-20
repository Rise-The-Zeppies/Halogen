# Halogen v0.2 — packaging and OS foundations

## Goal

Make a HalogenUI source file distributable as a standalone desktop app, then establish a realistic path toward a bootable HalogenOS image.

## Included in this milestone

- macOS: MacOS/package_ui.command accepts a .hg project and can generate .app, .dmg, .pkg, and a data .iso.
- macOS app bundles embed the source as Contents/Resources/Main.hg, which is loaded on launch. Packaged apps hide the developer toolbar.
- Windows: Windows/package_ui.ps1 publishes a self-contained executable alongside the project and ZIPs the distributable folder.
- CI packages all four macOS distribution forms and uploads them as a GitHub Actions artifact.

## Usage

macOS:

~~~bash
bash MacOS/package_ui.command MacOS/Examples/hello_ui.hg "My Halogen App" all
~~~

Optional format argument: app, dmg, pkg, iso, or all. An optional fifth argument sets the output directory.

Windows PowerShell:

~~~powershell
.\Windows\package_ui.ps1 .\Windows\Examples\halogenui_demo.hg "My Halogen App"
~~~

## Constraints that remain

- macOS packages are unsigned/notarized because signing requires the publisher's Apple Developer credentials.
- The ISO exported by this app packager is a data ISO, not a bootable operating system.
- The first HalogenUI runtime only accepts literal drawing values; it is not yet a complete UI widget toolkit or source-to-native compiler.
- The macOS and Windows UI backends currently use native platform frameworks and cannot simply be reused as the Linux OS renderer.

## V0.2 follow-on

1. Add a portable expression evaluator so drawing commands can use Halogen variables and imported modules.
2. Add HalogenUI controls, events, input/focus handling, and layout.
3. Add a platform-neutral UI/runtime contract implemented by macOS, Windows, and Linux backends.
4. Build a Linux userspace image with a custom boot/session process and launch a Halogen shell/desktop.
5. Produce and boot-test a UEFI/BIOS ISO in a virtual machine before calling it HalogenOS.

The most practical first HalogenOS target is a small Linux-based OS distribution using the Linux kernel plus a Halogen-managed userland and shell. A new kernel from scratch is a separate, much larger undertaking.

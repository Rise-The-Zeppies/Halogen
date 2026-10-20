# HalogenOS — architecture and build plan

**Status: design groundwork only. This folder is not yet a bootable operating system.**

The long-term idea is to build an OS whose shell, desktop, and applications are authored in Halogen and HalogenUI. A real OS image needs a kernel, a bootloader, userspace, a filesystem, drivers, a startup/session manager, and an installer/update story. Packaging a desktop app as an ISO doesn't provide those parts.

## Recommended first target

Use a Linux kernel and a small distribution build system for the first bootable prototype. Keep Halogen responsible for the shell, UI, app format, and higher-level userland. This is a practical route to a working OS image while the language and UI runtime mature.

### Target architecture

1. **Boot path:** UEFI/BIOS bootloader -> Linux kernel -> initramfs/root filesystem.
2. **Base userspace:** BusyBox or a similarly small userspace, device management, networking basics, filesystem mounts, and recovery shell.
3. **Halogen runtime:** a portable interpreter/runtime build that can run on Linux. The current macOS SwiftUI and Windows Forms implementations are separate native backends, so neither can simply be the Linux desktop backend.
4. **HalogenUI Linux backend:** a portable graphics/input backend (for example, SDL) that supports windows or a full-screen session, keyboard/mouse, and display scaling.
5. **Halogen shell/session:** a startup application launched by init/session manager, with a desktop, launcher, settings, terminal, and app runner written in Halogen.
6. **Packaging:** a reproducible build that emits a bootable ISO and optionally a VM disk image; boot-test it in QEMU/UTM before calling it a release.

## Phased milestones

- **OS-0 — Desktop export:** turn .hg into macOS .app/.dmg/.pkg or Windows .exe/.zip. This is app distribution, not an OS.
- **OS-1 — Portable runtime:** define Halogen's language/runtime contract independently of SwiftUI and WinForms.
- **OS-2 — Linux HalogenUI:** implement the Linux backend and a simple Halogen shell.
- **OS-3 — Bootable prototype:** use a minimal Linux base and launch the Halogen session automatically.
- **OS-4 — OS services:** settings, users, persistent storage, networking, package/app management, logs and recovery.
- **OS-5 — Installer and release:** bootable ISO, UEFI tests, virtual machine images, documented hardware support, and signed/reproducible release process.

## What is not claimed yet

No custom kernel, bootloader, driver stack, or bootable ISO exists in this repository at this milestone. The goal is to build towards a bootable HalogenOS without confusing app export media with an operating-system image.

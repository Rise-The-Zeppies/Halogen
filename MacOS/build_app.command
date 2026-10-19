#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="$REPO_ROOT/build/HalogenUI.app"

cd "$SCRIPT_DIR"
swift build --configuration release

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp ".build/release/HalogenMacOS" "$APP_DIR/Contents/MacOS/HalogenMacOS"
cp "Info.plist" "$APP_DIR/Contents/Info.plist"
chmod +x "$APP_DIR/Contents/MacOS/HalogenMacOS"

printf '\nHalogenUI app created at: %s\n' "$APP_DIR"
printf 'Double-click it to open the built-in demo, or use Open .hg… to load a Halogen source file.\n'

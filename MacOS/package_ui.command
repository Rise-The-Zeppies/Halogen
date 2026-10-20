#!/bin/bash
set -euo pipefail

usage() {
  echo "Usage: $0 <project.hg> [App Name] [app|dmg|pkg|iso|all] [output-directory]"
  exit 2
}

[[ $# -ge 1 ]] || usage
PROJECT_INPUT="$1"
APP_NAME="$(basename "$PROJECT_INPUT" .hg)"
FORMAT="all"
OUTPUT_INPUT=""
if [[ $# -ge 2 && -n "$2" ]]; then APP_NAME="$2"; fi
if [[ $# -ge 3 ]]; then FORMAT="$3"; fi
if [[ $# -ge 4 ]]; then OUTPUT_INPUT="$4"; fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ ! -f "$PROJECT_INPUT" ]]; then
  echo "ERROR: Halogen source file not found: $PROJECT_INPUT" >&2
  exit 1
fi
PROJECT_PATH="$(cd "$(dirname "$PROJECT_INPUT")" && pwd)/$(basename "$PROJECT_INPUT")"

case "$FORMAT" in
  app|dmg|pkg|iso|all) ;;
  *) usage ;;
esac

if [[ -n "$OUTPUT_INPUT" ]]; then
  mkdir -p "$OUTPUT_INPUT"
  DIST_DIR="$(cd "$OUTPUT_INPUT" && pwd)"
else
  DIST_DIR="$REPO_ROOT/dist"
  mkdir -p "$DIST_DIR"
fi
BUILD_DIR="$REPO_ROOT/build/packaged"
mkdir -p "$BUILD_DIR"

SAFE_SLUG="$(printf '%s' "$APP_NAME" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9-]/-/g' | sed 's/^-*//; s/-*$//')"
[[ -n "$SAFE_SLUG" ]] || SAFE_SLUG="app"
BUNDLE_ID="com.risethezeppies.halogen.$SAFE_SLUG"
SOURCE_APP="$REPO_ROOT/build/HalogenUI.app"
APP_PATH="$DIST_DIR/$APP_NAME.app"
DMG_PATH="$DIST_DIR/$APP_NAME.dmg"
PKG_PATH="$DIST_DIR/$APP_NAME.pkg"
ISO_PATH="$DIST_DIR/$APP_NAME.iso"

echo "Building HalogenUI runtime..."
bash "$SCRIPT_DIR/build_app.command"

echo "Embedding project source into $APP_PATH"
rm -rf "$APP_PATH"
cp -R "$SOURCE_APP" "$APP_PATH"
cp "$PROJECT_PATH" "$APP_PATH/Contents/Resources/Main.hg"
INFO="$APP_PATH/Contents/Info.plist"
plutil -replace CFBundleName -string "$APP_NAME" "$INFO"
plutil -replace CFBundleDisplayName -string "$APP_NAME" "$INFO"
plutil -replace CFBundleIdentifier -string "$BUNDLE_ID" "$INFO"

if [[ "$FORMAT" == "all" || "$FORMAT" == "dmg" ]]; then
  echo "Creating DMG..."
  rm -f "$DMG_PATH"
  hdiutil create -volname "$APP_NAME" -srcfolder "$APP_PATH" -ov -format UDZO "$DMG_PATH"
fi

if [[ "$FORMAT" == "all" || "$FORMAT" == "pkg" || "$FORMAT" == "iso" ]]; then
  echo "Creating installer PKG..."
  rm -f "$PKG_PATH"
  pkgbuild --component "$APP_PATH" --install-location "/Applications" --identifier "$BUNDLE_ID" --version "0.2.0" "$PKG_PATH"
fi

if [[ "$FORMAT" == "all" || "$FORMAT" == "iso" ]]; then
  echo "Creating data ISO (not a bootable OS image)..."
  ISO_ROOT="$BUILD_DIR/iso-root"
  rm -rf "$ISO_ROOT"
  mkdir -p "$ISO_ROOT"
  cp -R "$APP_PATH" "$ISO_ROOT/"
  cp "$PKG_PATH" "$ISO_ROOT/"
  if [[ "$FORMAT" == "all" && -f "$DMG_PATH" ]]; then cp "$DMG_PATH" "$ISO_ROOT/"; fi
  cat > "$ISO_ROOT/README.txt" <<EOF
$APP_NAME — HalogenUI distribution
This ISO contains a macOS application bundle and installer materials.
It is a data disc image, not a bootable operating system image.
The app is currently unsigned and not notarized.
EOF
  rm -f "$ISO_PATH"
  hdiutil makehybrid -o "$ISO_PATH" -iso -joliet "$ISO_ROOT"
fi

echo
echo "HalogenUI export complete. Output directory: $DIST_DIR"
echo "Standalone app: $APP_PATH"
[[ ! -f "$DMG_PATH" ]] || echo "Disk image:     $DMG_PATH"
[[ ! -f "$PKG_PATH" ]] || echo "Installer:      $PKG_PATH"
[[ ! -f "$ISO_PATH" ]] || echo "Data ISO:       $ISO_PATH"

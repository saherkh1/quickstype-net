#!/usr/bin/env bash
# Publish QuickSType for macOS, wrap into a .app bundle.
# Usage: ./build/publish-mac.sh [osx-arm64|osx-x64]
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="$ROOT/publish/$RID"
APP_DIR="$ROOT/publish/QuickSType.app"
RESOURCES_DIR="$APP_DIR/Contents/Resources"
MACOS_DIR="$APP_DIR/Contents/MacOS"

echo "==> Publishing for $RID"
rm -rf "$PUBLISH_DIR" "$APP_DIR"

# AOT publish; if AOT fails on this machine, fall back to self-contained without AOT.
if ! dotnet publish "$ROOT/src/QuickSType.UI" \
    -r "$RID" -c Release \
    -p:PublishAot=true \
    -p:StripSymbols=true \
    -p:InvariantGlobalization=false \
    -o "$PUBLISH_DIR"; then
    echo "==> AOT publish failed; retrying as self-contained without AOT"
    rm -rf "$PUBLISH_DIR"
    dotnet publish "$ROOT/src/QuickSType.UI" \
        -r "$RID" -c Release \
        --self-contained true \
        -p:PublishAot=false \
        -p:PublishSingleFile=false \
        -o "$PUBLISH_DIR"
fi

echo "==> Wrapping into .app"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR/"* "$MACOS_DIR/"
cp "$ROOT/build/Info.plist" "$APP_DIR/Contents/Info.plist"
cp "$ROOT/src/QuickSType.UI/Assets/AppIcon.png" "$RESOURCES_DIR/AppIcon.png" || true

# Ad-hoc sign for ARM64 launch on unsigned binaries.
echo "==> Ad-hoc signing"
codesign --force --deep --sign - "$APP_DIR"

echo "==> Done: $APP_DIR"

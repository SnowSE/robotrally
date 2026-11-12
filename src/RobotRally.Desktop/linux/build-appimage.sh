#!/usr/bin/env bash
# Packs a linux-x64 publish folder into an AppImage. Used by .github/workflows/release.yml; runs locally too.
#   build-appimage.sh <publish dir> <output .AppImage> [appimagetool]
set -euo pipefail
publish=$1 out=$2 tool=${3:-appimagetool}
here="$(cd "$(dirname "$0")" && pwd)"
appdir="$(mktemp -d)/RobotRally.AppDir"

mkdir -p "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/512x512/apps"
cp -R "$publish"/. "$appdir/usr/bin/"
mv "$appdir/usr/bin/RobotRally.Desktop" "$appdir/usr/bin/robot-rally"
chmod +x "$appdir/usr/bin/robot-rally"
cp "$here/AppRun" "$appdir/AppRun"
chmod +x "$appdir/AppRun"
cp "$here/robot-rally.desktop" "$appdir/"
cp "$here/robot-rally.desktop" "$appdir/usr/share/applications/"
cp "$here/robot-rally.png" "$appdir/"
cp "$here/robot-rally.png" "$appdir/usr/share/icons/hicolor/512x512/apps/"
ln -s robot-rally.png "$appdir/.DirIcon"

# appimagetool is itself an AppImage; extract-and-run means the build machine doesn't need FUSE.
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --no-appstream "$appdir" "$out"
chmod +x "$out"

#!/usr/bin/env bash
# Nusantara Colossi - Linux packaging.
# Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
#
# Publishes the desktop head self-contained for linux-x64, lays out an AppImage AppDir
# (AppRun, .desktop, icon) and then:
#   - builds an .AppImage if `appimagetool` is on PATH (or $APPIMAGETOOL points at it), or
#   - leaves a .tar.gz of the AppDir (extract it and run ./AppRun).
#
# Usage: build/package-linux.sh            (env: CONFIGURATION=Release, OUT_DIR=<repo>/artifacts)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
OUT_DIR="${OUT_DIR:-$ROOT/artifacts}"
RID="linux-x64"
PROJECT="$ROOT/src/NusantaraColossi.Desktop/NusantaraColossi.Desktop.csproj"
EXE="NusantaraColossi"
APP_ID="nusantara-colossi"

VERSION="$(dotnet msbuild "$PROJECT" -getProperty:Version | tr -d '\r[:space:]')"
VERSION="${VERSION:-0.0.0}"
PUBLISH_DIR="$OUT_DIR/publish/$RID"
APPDIR="$OUT_DIR/appdir/NusantaraColossi.AppDir"
PACKAGES="$OUT_DIR/packages"

echo "==> Publishing $RID ($CONFIGURATION, v$VERSION)"
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" --self-contained -o "$PUBLISH_DIR" -nologo

# Sanity: the Rust/wgpu core and the runtime assets must be next to the executable.
for f in "$EXE" libthreenet_core.so libSkiaSharp.so libHarfBuzzSharp.so assets/art/icon.png; do
  [ -e "$PUBLISH_DIR/$f" ] || { echo "error: $f missing from publish output" >&2; exit 1; }
done

echo "==> Laying out AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/1024x1024/apps"
cp -R "$PUBLISH_DIR/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/$EXE"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
# AppImage entry point: assets are resolved from the executable's folder (AppContext.BaseDirectory).
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/NusantaraColossi" "$@"
EOF
chmod +x "$APPDIR/AppRun"

cat > "$APPDIR/$APP_ID.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Nusantara Colossi
Comment=Hunt the eight guardians of an alternate Nusantara, 2045
Comment[id]=Buru delapan penjaga Nusantara alternatif, 2045
Exec=$EXE
Icon=$APP_ID
Terminal=false
Categories=Game;ActionGame;
X-AppImage-Version=$VERSION
EOF
cp "$APPDIR/$APP_ID.desktop" "$APPDIR/usr/share/applications/"

cp "$ROOT/assets/art/icon.png" "$APPDIR/$APP_ID.png"
cp "$ROOT/assets/art/icon.png" "$APPDIR/usr/share/icons/hicolor/1024x1024/apps/$APP_ID.png"
ln -sf "$APP_ID.png" "$APPDIR/.DirIcon" 2>/dev/null || cp "$APPDIR/$APP_ID.png" "$APPDIR/.DirIcon"

mkdir -p "$PACKAGES"
APPIMAGETOOL="${APPIMAGETOOL:-$(command -v appimagetool || true)}"
if [ -n "$APPIMAGETOOL" ]; then
  OUT="$PACKAGES/NusantaraColossi-$VERSION-x86_64.AppImage"
  echo "==> Building AppImage with $APPIMAGETOOL"
  # --appimage-extract-and-run lets appimagetool (itself an AppImage) work without FUSE, e.g. in CI.
  ARCH=x86_64 "$APPIMAGETOOL" --appimage-extract-and-run "$APPDIR" "$OUT" \
    || ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUT"
else
  OUT="$PACKAGES/NusantaraColossi-$VERSION-$RID.tar.gz"
  echo "==> appimagetool not found; writing a tarball of the AppDir instead"
  tar -czf "$OUT" -C "$(dirname "$APPDIR")" "$(basename "$APPDIR")"
fi
echo "==> Done: $OUT"

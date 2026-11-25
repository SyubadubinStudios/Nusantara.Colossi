#!/usr/bin/env bash
# Nusantara Colossi - macOS packaging.
# Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
#
# For each RID (default: osx-arm64 and osx-x64) this publishes the desktop head self-contained and
# wraps it in "Nusantara Colossi.app" (Info.plist, .icns icon via iconutil, ad-hoc codesign), then
# zips the bundle. iconutil/sips/codesign/ditto are used only when present, so the layout logic can
# also be dry-run on Linux or Git Bash.
#
# The game resolves assets from AppContext.BaseDirectory, so the published files (including
# assets/) stay together in Contents/MacOS.
#
# Usage: build/package-macos.sh            (env: RIDS="osx-arm64 osx-x64", CONFIGURATION=Release,
#                                            OUT_DIR=<repo>/artifacts, CODESIGN_IDENTITY=- )
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
OUT_DIR="${OUT_DIR:-$ROOT/artifacts}"
RIDS="${RIDS:-osx-arm64 osx-x64}"
CODESIGN_IDENTITY="${CODESIGN_IDENTITY:--}"
PROJECT="$ROOT/src/NusantaraColossi.Desktop/NusantaraColossi.Desktop.csproj"
EXE="NusantaraColossi"
APP_NAME="Nusantara Colossi"
BUNDLE_ID="com.syubadubin.nusantaracolossi"

VERSION="$(dotnet msbuild "$PROJECT" -getProperty:Version | tr -d '\r[:space:]')"
VERSION="${VERSION:-0.0.0}"
PACKAGES="$OUT_DIR/packages"
mkdir -p "$PACKAGES"

# Builds NusantaraColossi.icns from assets/art/icon.png (1024x1024). Returns non-zero if the tools are missing.
make_icns() {
  local dest="$1"
  command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1 || return 1
  local set
  set="$(mktemp -d)/NusantaraColossi.iconset"
  mkdir -p "$set"
  for s in 16 32 128 256 512; do
    sips -z "$s" "$s" "$ROOT/assets/art/icon.png" --out "$set/icon_${s}x${s}.png" >/dev/null
    local d=$((s * 2))
    sips -z "$d" "$d" "$ROOT/assets/art/icon.png" --out "$set/icon_${s}x${s}@2x.png" >/dev/null
  done
  iconutil -c icns "$set" -o "$dest"
}

for RID in $RIDS; do
  ARCH="${RID#osx-}"
  PUBLISH_DIR="$OUT_DIR/publish/$RID"
  STAGE="$OUT_DIR/macos/$RID"
  APP="$STAGE/$APP_NAME.app"

  echo "==> Publishing $RID ($CONFIGURATION, v$VERSION)"
  rm -rf "$PUBLISH_DIR"
  dotnet publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" --self-contained -o "$PUBLISH_DIR" -nologo

  for f in "$EXE" libthreenet_core.dylib libSkiaSharp.dylib libHarfBuzzSharp.dylib assets/art/icon.png; do
    [ -e "$PUBLISH_DIR/$f" ] || { echo "error: $f missing from publish output" >&2; exit 1; }
  done

  echo "==> Assembling $APP"
  rm -rf "$STAGE"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
  # Managed debug symbols are not needed in a player build.
  find "$APP/Contents/MacOS" -maxdepth 1 -name '*.pdb' -delete
  chmod +x "$APP/Contents/MacOS/$EXE"

  ICON_KEY=""
  if make_icns "$APP/Contents/Resources/NusantaraColossi.icns"; then
    ICON_KEY="  <key>CFBundleIconFile</key>
  <string>NusantaraColossi</string>"
  else
    echo "    (iconutil/sips not available: bundle has no .icns icon)"
  fi

  cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleExecutable</key>
  <string>$EXE</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
$ICON_KEY
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.action-games</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSHumanReadableCopyright</key>
  <string>Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios</string>
</dict>
</plist>
EOF
  if command -v plutil >/dev/null 2>&1; then plutil -lint "$APP/Contents/Info.plist"; fi

  if command -v codesign >/dev/null 2>&1; then
    echo "==> Codesigning (identity: $CODESIGN_IDENTITY)"
    # Sign nested native libraries first, then the bundle. Ad-hoc ("-") is enough to run locally on
    # Apple Silicon; distribution needs a Developer ID identity, hardened runtime and notarization.
    find "$APP/Contents/MacOS" -name '*.dylib' -exec codesign --force --timestamp=none --sign "$CODESIGN_IDENTITY" {} \;
    codesign --force --deep --timestamp=none --sign "$CODESIGN_IDENTITY" "$APP" \
      && codesign --verify --verbose=1 "$APP" \
      || echo "warning: codesign failed; the app will need 'xattr -dr com.apple.quarantine' to launch" >&2
  else
    echo "    (codesign not available: bundle is unsigned)"
  fi

  OUT="$PACKAGES/NusantaraColossi-$VERSION-macos-$ARCH.zip"
  rm -f "$OUT"
  if command -v ditto >/dev/null 2>&1; then
    ditto -c -k --sequesterRsrc --keepParent "$APP" "$OUT"
  elif command -v zip >/dev/null 2>&1; then
    (cd "$STAGE" && zip -qry "$OUT" "$APP_NAME.app") || true
  fi
  if [ ! -s "$OUT" ]; then
    rm -f "$OUT"
    OUT="${OUT%.zip}.tar.gz"
    tar -czf "$OUT" -C "$STAGE" "$APP_NAME.app"
  fi
  [ -s "$OUT" ] || { echo "error: failed to write $OUT" >&2; exit 1; }
  echo "==> Done: $OUT"
done

#!/bin/sh
set -eu

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PKG_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# まずソースリンクを更新
sh "$SCRIPT_DIR/link_sources.sh"

# 出力先（Unity 側）
UNITY_ROOT="$(cd "$PKG_DIR/../../.." && pwd)"
MAC_PLUGIN_DIR="$UNITY_ROOT/Assets/Plugins/macOS"

# ビルド用
BUILD_DIR="$PKG_DIR/build"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

echo "== swift build (arm64)"
swift build -c release --arch arm64 --package-path "$PKG_DIR"

echo "== swift build (x86_64)"
swift build -c release --arch x86_64 --package-path "$PKG_DIR"

LIB_ARM64="$PKG_DIR/.build/arm64-apple-macosx/release/libOSSpeech.dylib"
LIB_X64="$PKG_DIR/.build/x86_64-apple-macosx/release/libOSSpeech.dylib"
FAT_LIB="$BUILD_DIR/libOSSpeech.dylib"

echo "== lipo (create universal dylib)"
lipo -create "$LIB_ARM64" "$LIB_X64" -output "$FAT_LIB"

# 任意: コード署名（配布やGatekeeper対策が必要な場合）
# 環境変数 SIGN_ID に署名IDを指定すると署名します。例: export SIGN_ID="Developer ID Application: Your Name (TEAMID)"
if [ "${SIGN_ID:-}" != "" ]; then
  echo "== codesign"
  codesign --force --sign "$SIGN_ID" --options runtime --timestamp "$FAT_LIB"
fi

# 配置
mkdir -p "$MAC_PLUGIN_DIR"
cp -f "$FAT_LIB" "$MAC_PLUGIN_DIR/"

echo "== done =="
echo "Placed: $MAC_PLUGIN_DIR/$(basename "$FAT_LIB")"

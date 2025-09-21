#!/bin/sh
set -eu

# このスクリプトの場所からパス解決
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PKG_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

MAC_SRC_DIR="$PKG_DIR/Sources/OSSpeech"
IOS_SRC_DIR_REL="../../../Assets/Plugins/iOS/OSSpeech"
IOS_SRC_DIR="$(cd "$PKG_DIR/$IOS_SRC_DIR_REL" && pwd)"

# 既存のディレクトリ/リンクを掃除
if [ -e "$MAC_SRC_DIR" ] || [ -L "$MAC_SRC_DIR" ]; then
  rm -rf "$MAC_SRC_DIR"
fi

# iOS のソースフォルダへシンボリックリンク
ln -s "$IOS_SRC_DIR" "$MAC_SRC_DIR"

echo "Linked: $MAC_SRC_DIR -> $IOS_SRC_DIR"

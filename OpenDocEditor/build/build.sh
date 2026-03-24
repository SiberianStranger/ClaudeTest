#!/bin/bash
# Build script — cross-compile OpenDocEditor for Windows x64
set -e

export PATH=$PATH:/home/user/.dotnet
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$SCRIPT_DIR/.."
APP="$ROOT/src/OpenDocEditor.App"
DIST="$ROOT/dist"

echo "=== OpenDocEditor Build Script ==="
echo "Target: Windows x64 (self-contained)"

# Restore
echo "[1/3] Restoring packages..."
dotnet restore "$ROOT"

# Build (verify no errors)
echo "[2/3] Building (Debug check)..."
dotnet build "$ROOT" -c Debug --no-restore

# Publish Windows self-contained
echo "[3/3] Publishing win-x64 self-contained..."
dotnet publish "$APP" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=embedded \
  -o "$DIST/win-x64" \
  --no-restore

echo ""
echo "=== Build Complete ==="
echo "Output: $DIST/win-x64/"
ls -lh "$DIST/win-x64/OpenDocEditor.exe" 2>/dev/null || ls -lh "$DIST/win-x64/"

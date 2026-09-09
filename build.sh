#!/usr/bin/env bash
# Build both front ends. Runs on Linux and produces the Windows .exe too --
# WPF needs EnableWindowsTargeting to build off-Windows, which the tray
# project already sets.
set -euo pipefail
cd "$(dirname "$0")"

OUT="${OUT:-dist}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"

rm -rf "$OUT"
mkdir -p "$OUT/linux-x64" "$OUT/win-x64"

echo "==> linux-x64  (gh-actions-core: waybar module + poller)"
dotnet publish src/GhActions.Cli -r linux-x64 -c Release \
  --self-contained "$SELF_CONTAINED" -p:PublishSingleFile=true \
  -o "$OUT/linux-x64" >/dev/null

echo "==> win-x64    (gh-actions-tray: notification-area app)"
dotnet publish src/GhActions.Tray -r win-x64 -c Release \
  --self-contained "$SELF_CONTAINED" -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT/win-x64" >/dev/null

# The .pdb files are large and useless to a user installing this.
find "$OUT" -name '*.pdb' -delete

echo
echo "built:"
find "$OUT" -maxdepth 2 -type f \( -name 'gh-actions-core' -o -name '*.exe' \) -printf '%s\t%p\n' \
  | awk -F'\t' '{printf "  %-34s %8.1f MB\n", $2, $1/1048576}'

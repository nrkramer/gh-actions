#!/usr/bin/env bash
# Install the Linux poller/bar binary. Deliberately does NOT touch the waybar
# config or the GTK panel -- see README, "Cutting Linux over".
set -euo pipefail
cd "$(dirname "$0")/.."

BIN="${BIN:-$HOME/.local/bin}"
CFG="${XDG_CONFIG_HOME:-$HOME/.config}/gh-actions"

[ -x dist/linux-x64/gh-actions-core ] || { echo "run ./build.sh first" >&2; exit 1; }

mkdir -p "$BIN" "$CFG"
install -m755 dist/linux-x64/gh-actions-core "$BIN/gh-actions-core"
echo "installed $BIN/gh-actions-core"

if [ ! -f "$CFG/config.json" ]; then
  cat > "$CFG/config.json" <<'JSON'
{
  "org": "YOUR-ORG-OR-USERNAME",
  "repos": ["first-repo", "second-repo"],
  "active_poll_seconds": 15,
  "idle_poll_seconds": 60,
  "runs_per_repo": 20
}
JSON
  echo "wrote starter config at $CFG/config.json -- edit it before use"
fi

case ":$PATH:" in
  *":$BIN:"*) ;;
  *) echo "note: $BIN is not on PATH" ;;
esac

echo
echo "check it:  gh-actions-core tick"

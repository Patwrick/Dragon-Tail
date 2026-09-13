#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_PATH="$REPO_ROOT/Builds/Mac/Dragon Tail.app/Contents/MacOS/Dragon Tail"
CAPTURE_DIR="$REPO_ROOT/artifacts/screenshots"

if [[ ! -x "$APP_PATH" ]]; then
  printf 'Build the app first with scripts/build-mac.sh\n' >&2
  exit 1
fi
mkdir -p "$CAPTURE_DIR"
"$APP_PATH" -screen-fullscreen 0 -screen-width 1440 -screen-height 900 \
  --capture-dir "$CAPTURE_DIR" -logFile "$REPO_ROOT/artifacts/player-review.log"
cp "$CAPTURE_DIR/runtime-checks.json" "$REPO_ROOT/artifacts/runtime-checks.json"
cat "$REPO_ROOT/artifacts/runtime-checks.json"
printf '\nScreenshots: %s\n' "$CAPTURE_DIR"

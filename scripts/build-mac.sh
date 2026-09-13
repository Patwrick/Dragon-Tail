#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
UNITY_EDITOR="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}"
PROJECT_PATH="$REPO_ROOT/DragonTailUnity"
LOG_PATH="$REPO_ROOT/artifacts/unity-build-mac.log"

if [[ ! -x "$UNITY_EDITOR" ]]; then
  printf 'Unity Editor was not found at %s. Set UNITY_EDITOR to its executable.\n' "$UNITY_EDITOR" >&2
  exit 1
fi

mkdir -p "$REPO_ROOT/artifacts" "$REPO_ROOT/Builds/Mac"
printf 'Building Dragon Tail for macOS. Unity log: %s\n' "$LOG_PATH"
if ! "$UNITY_EDITOR" -batchmode -nographics -quit -projectPath "$PROJECT_PATH" \
    -buildTarget StandaloneOSX -executeMethod DragonTail.EditorTools.DragonTailBuild.BuildMac \
    -logFile "$LOG_PATH"; then
  tail -n 100 "$LOG_PATH"
  exit 1
fi

if [[ ! -x "$REPO_ROOT/Builds/Mac/Dragon Tail.app/Contents/MacOS/Dragon Tail" ]]; then
  printf 'Unity exited without producing the expected app executable.\n' >&2
  exit 1
fi
if ! /usr/bin/grep -q 'DRAGON_TAIL_BUILD_SUCCEEDED' "$LOG_PATH"; then
  printf 'The Unity log does not confirm a successful build.\n' >&2
  tail -n 100 "$LOG_PATH"
  exit 1
fi
printf 'Built: %s\n' "$REPO_ROOT/Builds/Mac/Dragon Tail.app"

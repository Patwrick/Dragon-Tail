#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd -P)"
APP_PATH="$REPO_ROOT/Builds/Mac/Dragon Tail.app/Contents/MacOS/Dragon Tail"
FRAME_DIR="$REPO_ROOT/artifacts/video-frames"
MEDIA_DIR="$REPO_ROOT/docs/media"
FFMPEG="${FFMPEG:-ffmpeg}"

if [[ ! -x "$APP_PATH" ]]; then
  printf 'Build the app first with scripts/build-mac.sh\n' >&2
  exit 1
fi
if ! command -v "$FFMPEG" >/dev/null 2>&1; then
  printf 'Install FFmpeg or set FFMPEG to its executable path.\n' >&2
  exit 1
fi

# Each run gets a fresh directory so older frames cannot enter a new export.
mkdir -p "$FRAME_DIR" "$MEDIA_DIR"
RUN_DIR="$(mktemp -d "$FRAME_DIR/capture.XXXXXX")"
ENCODE_DIR="$(mktemp -d "$FRAME_DIR/encoded.XXXXXX")"
"$APP_PATH" -screen-fullscreen 0 -screen-width 1440 -screen-height 900 \
  --video-dir "$RUN_DIR" -logFile "$REPO_ROOT/artifacts/player-video.log"

if [[ ! -f "$RUN_DIR/video-manifest.json" || ! -f "$RUN_DIR/frame00959.png" ]]; then
  printf 'The native export did not finish. See artifacts/player-video.log\n' >&2
  exit 1
fi

"$FFMPEG" -hide_banner -loglevel warning -y \
  -framerate 30 -start_number 0 -i "$RUN_DIR/frame%05d.png" -frames:v 960 \
  -c:v libx264 -preset slow -crf 19 -pix_fmt yuv420p -movflags +faststart -an \
  -metadata title='Dragon Tail | 2x demo' \
  -metadata comment='58-second authored Unity visualization at 2x, with 1-second opening and 2-second final holds.' \
  "$ENCODE_DIR/dragon-tail-demo-2x.mp4"

"$FFMPEG" -hide_banner -loglevel warning -y \
  -i "$ENCODE_DIR/dragon-tail-demo-2x.mp4" \
  -filter_complex 'fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:stats_mode=full[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle' \
  -loop 0 "$ENCODE_DIR/dragon-tail-demo-2x.gif"

# Preserve the previous published pair if capture or either encoder fails.
mv "$ENCODE_DIR/dragon-tail-demo-2x.mp4" "$MEDIA_DIR/dragon-tail-demo-2x.mp4"
mv "$ENCODE_DIR/dragon-tail-demo-2x.gif" "$MEDIA_DIR/dragon-tail-demo-2x.gif"
cp "$RUN_DIR/video-manifest.json" "$MEDIA_DIR/video-manifest.json"
rmdir "$ENCODE_DIR"
printf 'Video: %s\nPreview: %s\n' \
  "$MEDIA_DIR/dragon-tail-demo-2x.mp4" "$MEDIA_DIR/dragon-tail-demo-2x.gif"

# Demo media

The README preview and MP4 show the complete authored Unity sequence at **2× speed**. The video is 32 seconds: a one-second opening hold, 29 seconds of playback, and a two-second final hold. It has no audio.

- `dragon-tail-demo-2x.mp4`: 1440 × 900, 30 fps, H.264, with fast-start playback.
- `dragon-tail-demo-2x.gif`: 960 × 600, 12 fps, looping preview for the GitHub README.
- `video-manifest.json`: native capture metadata and runtime error count.

To regenerate from the repository root on an unlocked graphical macOS session with Unity and FFmpeg installed:

```sh
./scripts/build-mac.sh
./scripts/record-demo.sh
```

Set `FFMPEG` to an executable path if FFmpeg is not on your `PATH`. The recorder samples each story frame deterministically, so capture performance does not change the exported playback speed. Raw frames remain in the ignored `artifacts/video-frames` directory.

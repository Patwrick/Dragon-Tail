# Play the Dragon Tail animation

Dragon Tail is a 58-second authored Unity presentation of the game concept. Eight drones leave a concealed ridge camp; three climb above the swarm and circle their relay points inside arcade shields, with the link following their motion. The other five hit A, B, and C: A clears while B and C are damaged. The three relays then descend and repeat the cached A/B/C sequence, each at an earlier primary impact position. All three objectives are cleared at the end. Each drone makes one diving attack, explodes, and disappears.

**Current status:** The expanded-map native build with Unity 6000.3.24f1 and all 25 timeline/configuration checks passed. Native playback checks passed with zero runtime errors and twelve captured frames. The expanded overview, bright orange communication links, and final objective state were visually checked.

## Open the macOS app

Open `Builds/Mac/Dragon Tail.app` in Finder, or run this from the repository root:

```sh
open "Builds/Mac/Dragon Tail.app"
```

The app starts paused, requests a **1600 × 1000** window, and positions it within the current display. You can resize it. Click **PLAY** to begin. The native build targets Apple Silicon macOS. Explicit capture-resolution overrides are respected.

The three-quarter perspective makes the relay drones' higher altitude and descent easier to see against the landscape.

The map spans 128 × 80 arbitrary scene units, with green terrain textures, contour lines, smooth tree canopies, rocky relief, and detailed village and industrial sites under neutral daylight. A concealed camp sits beside a ridge, with no direct road connecting it to the objectives. The quadcopters have shaped light-gray bodies, dark covers, motor hubs, thin two-blade propellers, landing skids, nose cameras, and small role lights. Communications lines and moving message pulses use bright orange.

Red translucent circles follow visible drones to illustrate game range. Their radius is fixed artwork in scene units; it does not calculate radio coverage, terrain propagation, or real-world distances. Terrain contours help show the shape of the landscape.

The repeat route and impacts are cached before playback. They do not depend on new messages or the current objective-state display: the former relays repeat A, then B, then C at the earlier hit positions. No targets are added after departure.

## Controls

| Control | Action |
| --- | --- |
| PLAY / PAUSE | Start or pause the sequence. At the end, the button becomes REPLAY. |
| RESET | Return to the opening frame and pause. |
| 0.5x / 1x / 2x | Choose playback speed. |
| Timeline slider | Seek to any point; seeking pauses playback. |
| Chapter buttons | Jump to Orders, Advance, First Pass, Release, Follow-up, or Complete. |
| LINKS ON / OFF | Show or hide the communications-link visualization. |
| RANGES ON / OFF | Show or hide the red illustrative range circles; starts on. |
| Mouse wheel over the map / − and + buttons | Zoom the view out or in. |
| Right- or middle-button drag over the map | Pan while zoomed in. |
| OVERVIEW | Restore the full-map view. |
| Space | Play / pause, or replay from the end. |
| R | Reset to the opening frame and pause. |
| Left / right arrow | Seek backward / forward by three seconds. |

Cyan identifies the primary wave, amber identifies relays, and coral identifies the follow-up wave. The side panel displays active role counts and objective states. Drone identities remain consistent as their roles change. A drone stays hidden after its attack; resetting or seeking backward restores the earlier authored state.

## Open in Unity

1. Use Unity Hub to add the `DragonTailUnity` folder as a project.
2. Open it with **Unity 6000.3.24f1**. Allow the initial import and script compilation to finish.
3. Choose **Dragon Tail → Prepare Scene**. This creates or opens `Assets/Scenes/DragonTail.unity` and sets up the build scene, player options, and shader references.
4. Enter Play Mode with Unity's toolbar Play button.
5. Click the animation's own **PLAY** button in the Game view, or press Space while that view has focus.

The saved scene contains the `DragonTailExperience` entry component. It creates the camera, landscape, drones, objective models, effects, and interface at runtime; the Edit Mode scene is intentionally minimal.

## Build from the repository

The scripts default to the Unity installation at `/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity`. Close this project's Editor window before running a batch build or check so two Unity processes do not open the same project.

From the repository root:

```sh
./scripts/check.sh
./scripts/build-mac.sh
```

`build-mac.sh` also runs the deterministic validation before building. To use another installation of the same Editor version, provide its executable path:

```sh
UNITY_EDITOR="/path/to/Unity.app/Contents/MacOS/Unity" ./scripts/build-mac.sh
```

The Editor menu offers the same operations under **Dragon Tail → Validate Animation** and **Dragon Tail → Build macOS App**.

## Verification

The scripts write reports under the repository's `artifacts` directory:

The current runtime report records `passed: true`, zero runtime errors, and twelve captured frames for this update. Earlier-map reports and screenshots are archived under `artifacts/screenshots/previous-map/<timestamp>`.

| Report | What it records |
| --- | --- |
| `artifacts/checks.json` | Scene/map and larger-window configuration plus deterministic timeline checks: all three objectives hit, exact cached A/B/C repeats, one-use destruction, objective outcomes, higher circling relays, passive-shield lifetime, explosion windows, reverse seeks, counts, and motion continuity. |
| `artifacts/unity-check.log` | Unity's batch validation log. |
| `artifacts/build-mac.json` | Build result, architecture, output path, size, and duration. |
| `artifacts/unity-build-mac.log` | Unity's native build log. |
| `artifacts/runtime-checks.json` | Playback controls, rendered drone lifecycle, relay-link endpoints, range/shield/explosion visibility, rewind restoration, and runtime error count. |
| `artifacts/screenshots/` | Twelve captured story, shield, orbit, and explosion frames, plus the runtime review report. |

The built app also has a visual-review mode. Run this from the repository root in a normal graphical macOS session:

```sh
./scripts/test-playback.sh
```

This mode checks the public playback controls and rendered lifecycle, captures twelve representative story frames under `artifacts/screenshots`, copies the result to `artifacts/runtime-checks.json`, and closes the review app. The checks inspect actual drone visibility, trails, shadows, protection and explosion effects, and link endpoints as the relays circle. Read the report and inspect the screenshots to assess the rendered result. A successful timeline check alone does not verify visual presentation or pointer interaction.

## Source layout

| File | Responsibility |
| --- | --- |
| `DragonTailUnity/Assets/Scripts/StoryTimeline.cs` | Samples the authored timeline; owns paths, relay circles, phase boundaries, drone lifecycle, passive shields, explosion cues, and objective outcomes. |
| `DragonTailUnity/Assets/Scripts/DragonTailExperience.cs` | Builds the 3D scene and concealed camp, controls the camera and initial window, exposes playback controls, and captures review frames. |
| `DragonTailUnity/Assets/Scripts/DragonTailCommsRanges.cs` | Draws fixed-radius red game overlays beneath visible drones, using cached terrain heights to follow the ground surface. |
| `DragonTailUnity/Assets/Scripts/DragonTailDroneModel.cs` | Builds the shaped quadcopter airframes, motor hubs, animated two-blade propellers, landing skids, nose cameras, and role lights. |
| `DragonTailUnity/Assets/Scripts/DragonTailLandscape.cs` | Builds the expanded green terrain, contours, forests, rocky relief, surrounding roads, a stream, village buildings, fences, and utility scenery. |
| `DragonTailUnity/Assets/Scripts/DragonTailEffects.cs` | Renders authored arcade explosions, passive relay protection bubbles, and orbit guides. |
| `DragonTailUnity/Assets/Scripts/DragonTailObjectiveSite.cs` | Builds the textured logistics shed, service depot, and storage tanks, with authored intact, damaged, and collapsed scenery states. |
| `DragonTailUnity/Assets/Scripts/DragonTailHUD.cs` | Draws the interface and handles playback, chapter jumps, timeline seeking, map zoom/pan, and display toggles. |
| `DragonTailUnity/Assets/Editor/DragonTailBuild.cs` | Prepares the scene and native app configuration, validates the storyboard, and builds macOS. |
| `scripts/check.sh` | Runs Editor validation in batch mode. |
| `scripts/build-mac.sh` | Runs validation and builds the Apple Silicon app. |
| `scripts/test-playback.sh` | Runs the built app's playback checks, captures review frames, and writes `artifacts/runtime-checks.json`. |

The implementation follows the fixed example storyboard. The broader proposed rules in [the concept record](concept.md), such as interrupted links, unavailable drones, equipment costs, and upgrades, remain design ideas for later game work.

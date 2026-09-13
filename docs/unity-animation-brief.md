# Unity animation brief

**Expanded-map native build and all 25 timeline/configuration checks passed. Native playback checks passed with zero runtime errors and twelve captured frames; the expanded overview, bright orange communication links, and final objective state were visually checked.** This brief records the interactive 58-second animation in `DragonTailUnity`, using Unity 6000.3.24f1, with all three objectives hit in both passes, exact cached A/B/C repeats, elevated circling relays, a larger landscape, a concealed camp, contours, and illustrative game-range circles. See [Play the animation](PLAY_ANIMATION.md) for controls, setup, and evidence.

## Scene elements

- A concealed ground-team camp beside a ridge at one side of the scene.
- Eight distinguishable drone tokens on an authored path.
- Three visible checkpoints where individual drones peel off, rise to a higher level, and circle their relay points while maintaining the link.
- Passive fictional protection bubbles around the circling relays.
- A 128 × 80 scene-unit landscape with rolling terrain, contour lines, forests, rocky ridges, a stream, farm buildings, fences, and utility scenery. No road connects the camp directly to the objective sites.
- One-use attack dives, small explosions, and immediate disappearance of each consumed drone.
- Objective markers A, B, and C with clear intact, damaged, and cleared appearances.
- Bright orange communications lines with pulses moving in both directions.
- Red translucent game-range circles following visible drones, with a fixed artistic radius in arbitrary scene units.
- A compact phase label and active primary / relay / follow-up counts.

Use color and labels together: primary drones could be blue, relays amber, and the follow-up wave orange. Keep each drone's identifier unchanged when its role changes so viewers can see that the same units are being reused.

## Animation beats

| Beat | Visual action | What the viewer should understand |
| --- | --- | --- |
| Orders | An order packet moves from the controllers to the swarm; a small stored-orders indicator appears. | Both waves will use the original plan. |
| Advance | The main group moves along the authored path. | The primary force is leaving the control point. |
| Tail formation | At each of three checkpoints, one drone peels off, climbs above the formation, and circles inside a protection bubble while the group continues. The link follows its current position. | The communications chain grows one drone at a time; the relays maintain their link while circling in a distinct protected role. |
| Relay protection | Passive protection bubbles follow the elevated, circling relays. | The visual effect identifies the relay role and ends on departure. |
| Two-way link | Pulses travel toward the swarm and back toward the ground team. | The tail carries orders and status in both directions. |
| First pass | Five drones hit A, B, and C, then disappear in small bursts. A clears; B and C are damaged. | All three objectives receive the primary pass's authored actions. |
| Role change | The phase label changes; the three relays lose their shields, change color, and descend in a staggered departure. The old communications lines fade. | The tail is becoming a repeat wave with its A/B/C sequence already cached. |
| Follow-up | The three former relays repeat one earlier hit each on A, B, and C, at the same impact positions. Each explodes and disappears. | The wave replays the cached sequence and receives no fresh selection information. |
| Result | A, B, and C are all cleared. | The same three drones served first as relays and then repeated only previous hits. |

The current presentation uses an angled three-quarter perspective of the expanded 3D landscape. It keeps the control point, relay chain, advancing formation, and objectives visible while making the relays' higher altitude and descent easier to read. The larger initial window improves the scene's readable size. The interface provides play/pause, replay, reset, playback speed, timeline scrubbing, chapter jumps, and separate link and range toggles. Viewers can zoom with the mouse wheel or − / + buttons, pan while zoomed in with a right- or middle-button drag, and restore the full map with OVERVIEW.

The red circles are a game-art overlay. They do not compute radio coverage, terrain propagation, or real-world range. Contours describe the visible landscape rather than changing the cached sequence.

## Conceptual Unity responsibilities

| Responsibility | Purpose |
| --- | --- |
| Sequence director | Own phase changes and advance the authored sequence. |
| Drone views | Animate game tokens and display their current roles. |
| Landscape view | Build the detailed procedural terrain, forests, roads, and farm scenery around the authored route. |
| Communications view | Draw the fictional link and animate message pulses. |
| Objective views | Show storyboard outcomes for A, B, and C. |
| Overlay | Explain counts, phase, and stored-order reuse without obscuring the action. |

The pseudocode events `RelayAdded`, `TailReleased`, and `PrimaryPassFinished` remain conceptual names from the original proposal. The current implementation samples a deterministic `StoryTimeline` from the playhead: `DragonTailExperience` renders the scene, and `DragonTailHUD` handles the explanation and playback controls. This makes pause, replay, and backward seeking reproduce the same authored outcomes.

The animation should demonstrate the idea through authored motion and outcomes. It does not need a physics-based swarm simulator, real communications behavior, or live drone connections.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail.EditorTools
{
    /// <summary>Reproducible scene setup, story checks, and local desktop builds.</summary>
    public static class DragonTailBuild
    {
        private const string ScenePath = "Assets/Scenes/DragonTail.unity";
        private const string MacBuildOutput = "Builds/Mac/Dragon Tail.app";
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        private static string ArtifactsPath => Path.Combine(RepositoryRoot, "artifacts");

        [Serializable]
        private sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class CheckReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string scope = "Deterministic fictional animation timeline and Unity project configuration";
            public bool passed;
            public int passedChecks;
            public int totalChecks;
            public CheckResult[] checks;
        }

        [Serializable]
        private sealed class BuildSummaryReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string result;
            public string outputPath;
            public string architecture = "ARM64";
            public ulong totalBytes;
            public double durationSeconds;
        }

        [MenuItem("Dragon Tail/Prepare Scene")]
        public static void PrepareScene()
        {
            ConfigurePlayer();
            RetainRuntimeShaders();

            if (File.Exists(Path.Combine(ProjectRoot, ScenePath)))
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (UnityEngine.Object.FindFirstObjectByType<DragonTailExperience>() == null)
                {
                    new GameObject("Dragon Tail Experience").AddComponent<DragonTailExperience>();
                    EditorSceneManager.SaveScene(scene, ScenePath);
                }
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(ProjectRoot, ScenePath)));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Dragon Tail Experience").AddComponent<DragonTailExperience>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dragon Tail/Build macOS App")]
        public static void BuildMac()
        {
            Validate();
            string appPath = Path.Combine(RepositoryRoot, MacBuildOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(appPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            Directory.CreateDirectory(ArtifactsPath);
            File.WriteAllText(Path.Combine(ArtifactsPath, "build-mac.json"), JsonUtility.ToJson(new BuildSummaryReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(),
                outputPath = MacBuildOutput,
                totalBytes = report.summary.totalSize,
                durationSeconds = report.summary.totalTime.TotalSeconds
            }, true));

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Dragon Tail macOS build failed: " + report.summary.result);

            Debug.Log("DRAGON_TAIL_BUILD_SUCCEEDED " + appPath);
        }

        [MenuItem("Dragon Tail/Validate Animation")]
        public static void Validate()
        {
            var results = new List<CheckResult>();
            Check(results, "scene_and_player_configuration", () =>
            {
                PrepareScene();
                Require(File.Exists(Path.Combine(ProjectRoot, ScenePath)), "The entry scene was not saved.");
                Require(UnityEngine.Object.FindFirstObjectByType<DragonTailExperience>() != null, "The scene is missing DragonTailExperience.");
                Require(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].enabled &&
                    EditorBuildSettings.scenes[0].path == ScenePath, "The entry scene is not the enabled build scene.");
                Require(PlayerSettings.fullScreenMode == FullScreenMode.Windowed && PlayerSettings.resizableWindow,
                    "The app must launch in a resizable window.");
                Require(PlayerSettings.defaultScreenWidth == 1600 && PlayerSettings.defaultScreenHeight == 1000,
                    "Unexpected initial window size.");
                Require(GraphicsSettings.defaultRenderPipeline == null && QualitySettings.renderPipeline == null,
                    "The scene requires the built-in render pipeline.");
            });
            Check(results, "story_duration", () => Require(StoryTimeline.Duration == 58f, "The story duration should be 58 seconds."));
            Check(results, "initial_formation_and_objectives", () =>
            {
                StoryFrame frame = StoryTimeline.Sample(0f);
                Require(frame.Drones.Length == 8, "Expected eight drones at the start.");
                Require(frame.PrimaryCount == 8 && frame.RelayCount == 0 && frame.FollowUpCount == 0,
                    "The initial formation should contain eight primary drones.");
                Require(frame.DestroyedCount == 0, "No drones should be consumed before playback.");
                foreach (DronePose drone in frame.Drones)
                    Require(drone.Visible && !drone.Shielded, "Every initial drone should be visible and unshielded.");
                Require(frame.Objectives.Length == 3, "Expected three abstract objectives.");
                foreach (ObjectiveState state in frame.Objectives)
                    Require(state == ObjectiveState.Untouched, "An objective was changed before playback.");
            });
            Check(results, "three_detach_events", () =>
            {
                float[] times = { 10f, 17f, 24f };
                for (int i = 0; i < times.Length; i++)
                {
                    Require(StoryTimeline.Sample(times[i] - 0.001f).Drones[i + 5].Role == DroneRole.Primary,
                        "Drone " + (i + 5) + " detached too early.");
                    Require(StoryTimeline.Sample(times[i]).Drones[i + 5].Role == DroneRole.Relay,
                        "Drone " + (i + 5) + " did not enter its relay role.");
                }
            });
            Check(results, "full_tail_at_26_seconds", () =>
            {
                StoryFrame frame = StoryTimeline.Sample(26f);
                Require(frame.PrimaryCount == 5 && frame.RelayCount == 3 && frame.FollowUpCount == 0,
                    "The full tail should have three relays and five primary drones.");
                Require(frame.CommsActive, "The relay-chain animation must be active.");
                for (int i = 5; i < 8; i++)
                    Require(frame.Drones[i].Role == DroneRole.Relay, "Unexpected relay identity at index " + i);
            });
            Check(results, "primary_wave_objective_transitions", () =>
            {
                Require(StoryTimeline.Sample(30.299f).Objectives[0] != ObjectiveState.Cleared, "Objective A cleared before its final impact cue.");
                Require(StoryTimeline.Sample(30.3f).Objectives[0] == ObjectiveState.Cleared, "Objective A should clear at its final impact cue.");
                Require(StoryTimeline.Sample(33.999f).Objectives[1] == ObjectiveState.Untouched, "Objective B changed before its cue.");
                Require(StoryTimeline.Sample(34f).Objectives[1] == ObjectiveState.Damaged, "Objective B should be damaged at its cue.");
                Require(StoryTimeline.Sample(36.999f).Objectives[2] == ObjectiveState.Untouched, "Objective C changed before its first hit.");
                Require(StoryTimeline.Sample(37f).Objectives[2] == ObjectiveState.Damaged, "The primary pass should hit objective C.");
            });
            Check(results, "first_wave_aftermath_at_38_seconds", () =>
            {
                StoryFrame frame = StoryTimeline.Sample(38f);
                Require(frame.Objectives[0] == ObjectiveState.Cleared && frame.Objectives[1] == ObjectiveState.Damaged &&
                    frame.Objectives[2] == ObjectiveState.Damaged, "The aftermath must show A cleared and B/C damaged.");
                for (int i = 0; i < 5; i++)
                    Require(frame.Drones[i].Role == DroneRole.Destroyed && !frame.Drones[i].Visible,
                        "Primary drone " + i + " must be consumed and hidden after its impact.");
                Require(frame.DestroyedCount == 5, "The first pass should consume all five primary drones.");
            });
            Check(results, "staggered_follow_up_release", () =>
            {
                float[] times = { 38f, 40f, 42f };
                for (int i = 0; i < times.Length; i++)
                {
                    Require(StoryTimeline.Sample(times[i] - 0.001f).Drones[i + 5].Role == DroneRole.Relay,
                        "Relay " + (i + 5) + " was released too early.");
                    StoryFrame frame = StoryTimeline.Sample(times[i]);
                    Require(frame.Drones[i + 5].Role == DroneRole.FollowUp, "A relay did not become a follow-up drone.");
                    Require(frame.RelayCount == 2 - i && frame.FollowUpCount == i + 1,
                        "Follow-up drones should release one at a time.");
                }
            });
            Check(results, "follow_up_objective_transitions", () =>
            {
                StoryFrame repeatA = StoryTimeline.Sample(48f);
                Require(repeatA.Objectives[0] == ObjectiveState.Cleared && repeatA.ActiveObjective == 0,
                    "The cached repeat must strike A even though A was already cleared.");
                Require(repeatA.Objectives[1] == ObjectiveState.Damaged, "Repeating A must not change B.");
                Require(StoryTimeline.Sample(50.999f).Objectives[1] == ObjectiveState.Damaged,
                    "Objective B cleared before its repeat cue.");
                Require(StoryTimeline.Sample(51f).Objectives[1] == ObjectiveState.Cleared,
                    "Objective B did not clear at its repeat cue.");
                Require(StoryTimeline.Sample(54.299f).Objectives[2] == ObjectiveState.Damaged,
                    "Objective C cleared before its repeat cue.");
                Require(StoryTimeline.Sample(54.3f).Objectives[2] == ObjectiveState.Cleared,
                    "Objective C did not clear at its repeat cue.");
            });
            Check(results, "completed_story", () =>
            {
                StoryFrame frame = StoryTimeline.Sample(58f);
                Require(frame.Objectives[0] == ObjectiveState.Cleared && frame.Objectives[1] == ObjectiveState.Cleared &&
                    frame.Objectives[2] == ObjectiveState.Cleared, "The final frame must show all three objectives cleared.");
                Require(frame.PrimaryCount == 0 && frame.RelayCount == 0 && frame.FollowUpCount == 0 && frame.DestroyedCount == 8,
                    "The end frame should show all eight drones consumed.");
                foreach (DronePose drone in frame.Drones)
                    Require(!drone.Visible && !drone.Shielded, "Consumed drones must have no visible body or protection shield.");
                Require(frame.Explosions.Length == 0, "Transient effects must finish before the final frame.");
            });
            Check(results, "follow_up_repeats_exact_cached_primary_hits", () =>
            {
                float[] primaryTimes = { 30f, 34f, 37f };
                float[] followUpTimes = { 48f, 51f, 54.3f };
                int[] primaryIds = { 0, 2, 4 };
                int[] objectives = { 0, 1, 2 };
                for (int i = 0; i < followUpTimes.Length; i++)
                {
                    StoryFrame earlier = StoryTimeline.Sample(primaryTimes[i]);
                    StoryFrame repeated = StoryTimeline.Sample(followUpTimes[i]);
                    Require(earlier.ActiveObjective == objectives[i] && repeated.ActiveObjective == objectives[i],
                        "A former relay must repeat the same objective as its cached earlier hit.");
                    Require(Vector3.Distance(earlier.Drones[primaryIds[i]].Position, repeated.Drones[i + 5].Position) < 0.0001f,
                        "A former relay must repeat the exact cached primary impact position.");
                    Require(CountExplosion(earlier, primaryIds[i]) == 1 && CountExplosion(repeated, i + 5) == 1,
                        "Both the earlier hit and its repeat need their own one-use impact cue.");
                    foreach (ExplosionCue cue in repeated.Explosions)
                        if (cue.DroneId == i + 5)
                            Require(!cue.IsMiss, "Follow-up repeats must come from earlier successful hit cues.");
                }
            });
            Check(results, "all_three_objectives_hit_before_repeats", () =>
            {
                var primaryHits = new HashSet<int>();
                var repeatedHits = new HashSet<int>();
                for (int tick = 0; tick <= 580; tick++)
                {
                    StoryFrame frame = StoryTimeline.Sample(tick * 0.1f);
                    if (frame.ActiveObjective >= 0)
                    {
                        Require(frame.ActiveObjective < 3, "An impact selected an unknown objective.");
                        if (frame.Time < 38f) primaryHits.Add(frame.ActiveObjective);
                        else
                        {
                            Require(primaryHits.Contains(frame.ActiveObjective),
                                "Every repeat must refer to an objective already hit by the primary pass.");
                            repeatedHits.Add(frame.ActiveObjective);
                        }
                    }
                    foreach (ExplosionCue cue in frame.Explosions)
                        Require(!cue.IsMiss, "Every authored attack should hit its assigned objective.");
                }
                Require(primaryHits.SetEquals(new[] { 0, 1, 2 }) && repeatedHits.SetEquals(new[] { 0, 1, 2 }),
                    "Both passes must include hits on A, B, and C.");
            });
            Check(results, "time_clamping", () =>
            {
                Equivalent(StoryTimeline.Sample(-100f), StoryTimeline.Sample(0f));
                Equivalent(StoryTimeline.Sample(100f), StoryTimeline.Sample(58f));
                Equivalent(StoryTimeline.Sample(float.NegativeInfinity), StoryTimeline.Sample(0f));
                Equivalent(StoryTimeline.Sample(float.PositiveInfinity), StoryTimeline.Sample(58f));
                Equivalent(StoryTimeline.Sample(float.NaN), StoryTimeline.Sample(0f));
            });
            Check(results, "reverse_seeking_has_no_state_leak", () =>
            {
                float[] times = { 0f, 9f, 14.5f, 19.5f, 26f, 30.4f, 34.4f, 37.1f, 38f, 48.2f, 51.2f, 54.4f, 58f };
                StoryFrame[] original = Array.ConvertAll(times, StoryTimeline.Sample);
                for (int i = times.Length - 1; i >= 0; i--)
                    Equivalent(original[i], StoryTimeline.Sample(times[i]));
                StoryTimeline.Sample(58f);
                Equivalent(original[0], StoryTimeline.Sample(0f));
            });
            Check(results, "sample_results_are_independent", () =>
            {
                StoryFrame changed = StoryTimeline.Sample(26f);
                StoryFrame expected = StoryTimeline.Sample(26f);
                changed.Drones[0].Position = new Vector3(999f, 999f, 999f);
                changed.Objectives[0] = ObjectiveState.Cleared;
                Equivalent(expected, StoryTimeline.Sample(26f));
                StoryFrame explosion = StoryTimeline.Sample(30.4f);
                StoryFrame expectedExplosion = StoryTimeline.Sample(30.4f);
                Require(explosion.Explosions.Length > 0, "Missing explosion sample for isolation check.");
                explosion.Explosions[0].Age = 999f;
                Equivalent(expectedExplosion, StoryTimeline.Sample(30.4f));
            });
            Check(results, "finite_positions_and_consistent_counts", () =>
            {
                for (int tick = 0; tick <= 232; tick++)
                {
                    StoryFrame frame = StoryTimeline.Sample(tick * 0.25f);
                    Require(frame.Drones.Length == 8 && frame.Objectives.Length == 3, "Object identities changed during playback.");
                    int primary = 0, relays = 0, followUp = 0, destroyed = 0;
                    foreach (DronePose drone in frame.Drones)
                    {
                        Vector3 position = drone.Position;
                        Require(IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z), "Non-finite drone position at " + frame.Time);
                        if (drone.Role == DroneRole.Primary) primary++;
                        if (drone.Role == DroneRole.Relay) relays++;
                        if (drone.Role == DroneRole.FollowUp) followUp++;
                        if (drone.Role == DroneRole.Destroyed) destroyed++;
                        Require(drone.Visible == (drone.Role != DroneRole.Destroyed), "Visibility disagrees with a drone's lifecycle at " + frame.Time);
                        Require(drone.Shielded == (drone.Role == DroneRole.Relay), "Shield protection should exist only during the relay role at " + frame.Time);
                    }
                    Require(primary == frame.PrimaryCount && relays == frame.RelayCount && followUp == frame.FollowUpCount &&
                        destroyed == frame.DestroyedCount && primary + relays + followUp + destroyed == 8,
                        "Displayed counts disagree with drone roles at " + frame.Time);
                    Require(IsFinite(frame.AttackPulse) && frame.AttackPulse >= 0f && frame.AttackPulse <= 1f,
                        "The visual effect intensity is invalid at " + frame.Time);
                }
            });
            Check(results, "continuous_motion_at_role_transitions", () =>
            {
                float[] edges = { 8f, 10f, 11.5f, 12.3f, 15f, 17f, 18.5f, 19.3f, 22f, 24f, 25.5f, 26.3f, 38f, 40f, 42f, 44f };
                foreach (float edge in edges)
                {
                    StoryFrame before = StoryTimeline.Sample(edge - 0.001f);
                    StoryFrame after = StoryTimeline.Sample(edge + 0.001f);
                    for (int i = 0; i < before.Drones.Length; i++)
                        Require(Vector3.Distance(before.Drones[i].Position, after.Drones[i].Position) < 0.1f,
                            "Drone " + i + " teleported at the role transition at " + edge);
                }
            });
            Check(results, "six_ordered_narrative_phases", () =>
            {
                var phases = new HashSet<int>();
                int previous = -1;
                for (int tick = 0; tick <= 232; tick++)
                {
                    StoryFrame frame = StoryTimeline.Sample(tick * 0.25f);
                    Require(frame.PhaseIndex >= previous && frame.PhaseIndex >= 0 && frame.PhaseIndex < 6,
                        "The narrative phase moved backward during forward playback.");
                    Require(!string.IsNullOrWhiteSpace(frame.PhaseTitle) && !string.IsNullOrWhiteSpace(frame.Description),
                        "A narrative phase is missing its on-screen explanation.");
                    phases.Add(frame.PhaseIndex);
                    previous = frame.PhaseIndex;
                }
                Require(phases.Count == 6, "The timeline should expose six narrative phases.");
            });
            Check(results, "one_use_drone_impact_lifecycle", () =>
            {
                float[] impacts = { 30f, 30.3f, 34f, 34.3f, 37f, 48f, 51f, 54.3f };
                for (int id = 0; id < impacts.Length; id++)
                {
                    StoryFrame before = StoryTimeline.Sample(impacts[id] - 0.001f);
                    StoryFrame impact = StoryTimeline.Sample(impacts[id]);
                    Require(before.Drones[id].Visible && before.Drones[id].Role != DroneRole.Destroyed,
                        "Drone " + id + " disappeared before its impact.");
                    Require(!impact.Drones[id].Visible && impact.Drones[id].Role == DroneRole.Destroyed && !impact.Drones[id].Shielded,
                        "Drone " + id + " must be consumed exactly at its impact.");
                    Require(impact.DestroyedCount == before.DestroyedCount + 1, "An impact must consume exactly one drone.");
                    Require(Vector3.Distance(before.Drones[id].Position, impact.Drones[id].Position) < 0.1f,
                        "Drone " + id + " teleported at impact.");
                    for (float later = impacts[id]; later <= StoryTimeline.Duration; later += 0.5f)
                    {
                        DronePose consumed = StoryTimeline.Sample(later).Drones[id];
                        Require(consumed.Role == DroneRole.Destroyed && !consumed.Visible && !consumed.Shielded,
                            "Drone " + id + " reappeared after consumption.");
                    }
                }
            });
            Check(results, "expanded_map_and_authored_scene_coordinates", () =>
            {
                Require(DragonTailLandscape.Width == 128f && DragonTailLandscape.Depth == 80f,
                    "The landscape should use the expanded 128 by 80 authored canvas.");
                Require(StoryTimeline.BasePosition == new Vector3(-48f, 0f, 0f), "Unexpected concealed camp anchor.");
                Require(StoryTimeline.SwarmAltitude == 10f && StoryTimeline.RelayAltitude == 16f,
                    "The enlarged scene should preserve the intended lower swarm and higher relay levels.");
                Vector3[] objectives = { new Vector3(43f, 0f, -14f), new Vector3(46f, 0f, 0f), new Vector3(43f, 0f, 14f) };
                float[] relayX = { -28f, -8f, 12f };
                for (int i = 0; i < 3; i++)
                {
                    Require(StoryTimeline.ObjectivePositions[i] == objectives[i], "Unexpected objective anchor " + i);
                    Require(StoryTimeline.RelayPositions[i] == new Vector3(relayX[i], 16f, 0f), "Unexpected relay anchor " + i);
                    Require(Mathf.Abs(objectives[i].x) < DragonTailLandscape.Width * 0.5f &&
                        Mathf.Abs(objectives[i].z) < DragonTailLandscape.Depth * 0.5f, "An objective falls outside the landscape.");
                }
            });
            Check(results, "relay_height_and_protection_lifetime", () =>
            {
                Require(StoryTimeline.RelayAltitude > StoryTimeline.SwarmAltitude, "Relay hover must be above the moving swarm.");
                float[] detach = { 10f, 17f, 24f };
                float[] release = { 38f, 40f, 42f };
                for (int i = 0; i < 3; i++)
                {
                    int id = i + 5;
                    DronePose start = StoryTimeline.Sample(detach[i]).Drones[id];
                    DronePose rising = StoryTimeline.Sample(detach[i] + 0.75f).Drones[id];
                    DronePose hover = StoryTimeline.Sample(detach[i] + 1.5f).Drones[id];
                    Require(Mathf.Abs(start.Position.y - StoryTimeline.SwarmAltitude) < 0.001f && start.Shielded,
                        "Relay " + id + " must start its protected rise at swarm height.");
                    Require(rising.Position.y > StoryTimeline.SwarmAltitude && rising.Position.y < StoryTimeline.RelayAltitude,
                        "Relay " + id + " must rise continuously.");
                    Require(Mathf.Abs(hover.Position.y - StoryTimeline.RelayAltitude) < 0.001f && hover.Shielded,
                        "Relay " + id + " did not reach the protected hover height.");
                    Require(StoryTimeline.Sample(release[i] - 0.001f).Drones[id].Shielded,
                        "Relay " + id + " lost its shield before release.");
                    DronePose depart = StoryTimeline.Sample(release[i]).Drones[id];
                    Require(!depart.Shielded && depart.Role == DroneRole.FollowUp &&
                        Mathf.Abs(depart.Position.y - StoryTimeline.RelayAltitude) < 0.001f,
                        "Relay " + id + " must lose its shield when its departure begins.");
                    DronePose descent = StoryTimeline.Sample((release[i] + 44f) * 0.5f).Drones[id];
                    Require(descent.Position.y < StoryTimeline.RelayAltitude && descent.Position.y > StoryTimeline.SwarmAltitude,
                        "Relay " + id + " must descend continuously into the follow-up wave.");
                    Require(Mathf.Abs(StoryTimeline.Sample(44f).Drones[id].Position.y - StoryTimeline.SwarmAltitude) < 0.001f,
                        "Relay " + id + " must rejoin the lower formation by the follow-up chapter.");
                }
            });
            Check(results, "elevated_relays_circle_while_holding_link", () =>
            {
                float[] detach = { 10f, 17f, 24f };
                Require(StoryTimeline.RelayOrbitRadius > 0f && StoryTimeline.RelayOrbitPeriod > 0f,
                    "The relay orbit must have a visible radius and positive authored period.");
                for (int i = 0; i < 3; i++)
                {
                    int id = i + 5;
                    float start = detach[i] + 2.4f;
                    StoryFrame first = StoryTimeline.Sample(start);
                    StoryFrame next = StoryTimeline.Sample(start + 1f);
                    StoryFrame loop = StoryTimeline.Sample(start + StoryTimeline.RelayOrbitPeriod);
                    Require(first.CommsActive && next.CommsActive && loop.CommsActive,
                        "The communications display must remain active while the elevated relays circle.");
                    foreach (StoryFrame frame in new[] { first, next, loop })
                    {
                        DronePose drone = frame.Drones[id];
                        Vector3 offset = drone.Position - StoryTimeline.RelayPositions[i];
                        Require(drone.Role == DroneRole.Relay && drone.Visible && drone.Shielded,
                            "Circling must preserve the relay role and its protection.");
                        Require(Mathf.Abs(drone.Position.y - StoryTimeline.RelayAltitude) < 0.001f &&
                            Mathf.Abs(new Vector2(offset.x, offset.z).magnitude - StoryTimeline.RelayOrbitRadius) < 0.001f,
                            "The relay must circle at its authored height and radius.");
                    }
                    Require(Vector3.Distance(first.Drones[id].Position, next.Drones[id].Position) > 0.1f,
                        "The elevated relay must move around its checkpoint.");
                    Require(Vector3.Distance(first.Drones[id].Position, loop.Drones[id].Position) < 0.001f,
                        "The authored orbit should return to the same point after one period.");
                    for (float time = detach[i]; time < 38f; time += 0.25f)
                    {
                        Vector3 offset = StoryTimeline.Sample(time).Drones[id].Position - StoryTimeline.RelayPositions[i];
                        Require(new Vector2(offset.x, offset.z).magnitude <= StoryTimeline.RelayOrbitRadius + 0.001f,
                            "A relay exceeded its authored orbit radius during the rise or loop.");
                    }
                }
            });
            Check(results, "deterministic_explosion_cue_windows", () =>
            {
                StoryFrame preImpact = StoryTimeline.Sample(29.999f);
                Require(preImpact.Explosions.Length == 0 && preImpact.AttackPulse == 0f && preImpact.ActiveObjective == -1,
                    "No explosion or objective impact cue may appear before the first collision.");
                float[] impacts = { 30f, 30.3f, 34f, 34.3f, 37f, 48f, 51f, 54.3f };
                for (int id = 0; id < impacts.Length; id++)
                {
                    Require(CountExplosion(StoryTimeline.Sample(impacts[id] - 0.001f), id) == 0,
                        "Explosion " + id + " appeared before impact.");
                    foreach (float elapsed in new[] { 0f, 0.9f, 1.799f })
                    {
                        StoryFrame frame = StoryTimeline.Sample(impacts[id] + elapsed);
                        Require(CountExplosion(frame, id) == 1, "Explosion " + id + " should be present once during its effect window.");
                        foreach (ExplosionCue cue in frame.Explosions)
                            if (cue.DroneId == id)
                            {
                                Require(Mathf.Abs(cue.Age - elapsed) < 0.001f, "Explosion age drifted after seeking.");
                                Require(!cue.IsMiss, "Every explosion should belong to a successful authored hit.");
                                Require(Vector3.Distance(cue.Position, frame.Drones[id].Position) < 0.001f,
                                    "Explosion must remain at its drone's impact position.");
                            }
                    }
                    Require(CountExplosion(StoryTimeline.Sample(impacts[id] + 1.801f), id) == 0,
                        "Explosion " + id + " outlived its effect window.");
                }
            });
            Check(results, "large_readable_mac_window_configuration", () =>
            {
                Require(PlayerSettings.defaultScreenWidth == 1600 && PlayerSettings.defaultScreenHeight == 1000,
                    "The app should request the larger 1600 by 1000 launch window.");
                Require(!PlayerSettings.macRetinaSupport,
                    "Retina support must be disabled so the launch size is not halved in macOS window points.");
                Require(PlayerSettings.fullScreenMode == FullScreenMode.Windowed && PlayerSettings.resizableWindow,
                    "The enlarged initial window must remain resizable.");
            });
            Check(results, "rewind_restores_all_eight_drones", () =>
            {
                StoryTimeline.Sample(58f);
                StoryFrame restarted = StoryTimeline.Sample(0f);
                Require(restarted.PrimaryCount == 8 && restarted.DestroyedCount == 0,
                    "Restarting the authored sequence must restore all eight drones.");
                foreach (DronePose drone in restarted.Drones)
                    Require(drone.Visible && !drone.Shielded && drone.Role == DroneRole.Primary,
                        "A replayed drone retained consumed or relay state.");
                Require(restarted.Explosions.Length == 0,
                    "Reset retained a transient effect from the prior playback.");
                Equivalent(StoryTimeline.Sample(30.4f), StoryTimeline.Sample(30.4f));
                Equivalent(StoryTimeline.Sample(19.5f), StoryTimeline.Sample(19.5f));
            });
            WriteChecks(results);
        }

        private static int CountExplosion(StoryFrame frame, int droneId)
        {
            int count = 0;
            foreach (ExplosionCue cue in frame.Explosions) if (cue.DroneId == droneId) count++;
            return count;
        }

        private static void Equivalent(StoryFrame expected, StoryFrame actual)
        {
            Require(Mathf.Abs(expected.Time - actual.Time) < 0.0001f, "Sample time changed.");
            Require(expected.PhaseIndex == actual.PhaseIndex && expected.PhaseTitle == actual.PhaseTitle &&
                expected.Description == actual.Description, "Narrative phase changed after seeking.");
            Require(expected.PrimaryCount == actual.PrimaryCount && expected.RelayCount == actual.RelayCount &&
                expected.FollowUpCount == actual.FollowUpCount && expected.CommsActive == actual.CommsActive &&
                expected.DestroyedCount == actual.DestroyedCount && expected.ActiveObjective == actual.ActiveObjective,
                "Frame state changed after seeking.");
            Require(Mathf.Abs(expected.AttackPulse - actual.AttackPulse) < 0.0001f, "Visual effect cue changed after seeking.");
            Require(expected.Drones.Length == actual.Drones.Length && expected.Objectives.Length == actual.Objectives.Length &&
                expected.Explosions.Length == actual.Explosions.Length,
                "Sample array lengths changed.");
            for (int i = 0; i < expected.Drones.Length; i++)
                Require(expected.Drones[i].Role == actual.Drones[i].Role &&
                    expected.Drones[i].Visible == actual.Drones[i].Visible && expected.Drones[i].Shielded == actual.Drones[i].Shielded &&
                    Vector3.Distance(expected.Drones[i].Position, actual.Drones[i].Position) < 0.0001f,
                    "Drone " + i + " changed after seeking.");
            for (int i = 0; i < expected.Objectives.Length; i++)
                Require(expected.Objectives[i] == actual.Objectives[i], "Objective " + i + " changed after seeking.");
            for (int i = 0; i < expected.Explosions.Length; i++)
            {
                ExplosionCue left = expected.Explosions[i], right = actual.Explosions[i];
                Require(left.DroneId == right.DroneId && left.IsMiss == right.IsMiss && Mathf.Abs(left.Age - right.Age) < 0.0001f &&
                    Vector3.Distance(left.Position, right.Position) < 0.0001f, "Explosion " + i + " changed after seeking.");
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Dragon Tail";
            PlayerSettings.productName = "Dragon Tail";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.dragontail.animation");
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.macRetinaSupport = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
        }

        private static void RetainRuntimeShaders()
        {
            const string folder = "Assets/Resources/DragonTailMaterials";
            Directory.CreateDirectory(Path.Combine(ProjectRoot, folder));
            AssetDatabase.Refresh();

            RetainShader("Standard", folder + "/Standard.mat", true);
            RetainShader("Sprites/Default", folder + "/SpritesDefault.mat", false);
            RetainShader("Unlit/Color", folder + "/UnlitColor.mat", false);
        }

        private static void RetainShader(string shaderName, string assetPath, bool emission)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Required built-in shader is unavailable: " + shaderName);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(assetPath) };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else material.shader = shader;

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white);
            }
            EditorUtility.SetDirty(material);
        }

        private static void Check(List<CheckResult> results, string name, Action action)
        {
            try
            {
                action();
                results.Add(new CheckResult { name = name, passed = true, detail = "Passed" });
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult { name = name, passed = false, detail = ex.Message });
                Debug.LogError("Dragon Tail check failed: " + name + ": " + ex.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void WriteChecks(List<CheckResult> results)
        {
            int passed = results.FindAll(result => result.passed).Count;
            var report = new CheckReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                passed = passed == results.Count,
                passedChecks = passed,
                totalChecks = results.Count,
                checks = results.ToArray()
            };
            Directory.CreateDirectory(ArtifactsPath);
            File.WriteAllText(Path.Combine(ArtifactsPath, "checks.json"), JsonUtility.ToJson(report, true));
            if (!report.passed)
                throw new InvalidOperationException("Dragon Tail validation failed: " + passed + "/" + results.Count + " checks passed. See artifacts/checks.json.");

            Debug.Log("DRAGON_TAIL_VALIDATION_SUCCEEDED " + passed + "/" + results.Count);
        }
    }
}

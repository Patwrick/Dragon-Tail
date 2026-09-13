using System.Collections.Generic;
using UnityEngine;

namespace DragonTail
{
    public enum DroneRole { Primary, Relay, FollowUp, Finished, Destroyed }
    public enum ObjectiveState { Untouched, Damaged, Cleared }

    public struct DronePose
    {
        public Vector3 Position;
        public DroneRole Role;
        public bool Visible;
        public bool Shielded;
    }

    public struct ExplosionCue
    {
        public int DroneId;
        public Vector3 Position;
        public float Age;
        public bool IsMiss;
    }

    public sealed class StoryFrame
    {
        public float Time;
        public int PhaseIndex;
        public string PhaseTitle;
        public string Description;
        public DronePose[] Drones;
        public ObjectiveState[] Objectives;
        public int PrimaryCount;
        public int RelayCount;
        public int FollowUpCount;
        public int DestroyedCount;
        public ExplosionCue[] Explosions;
        public bool CommsActive;
        public int ActiveObjective;
        public float AttackPulse;
    }

    /// <summary>
    /// A fixed, fictional game storyboard. Sampling has no side effects, so the
    /// presentation can pause, scrub backwards, or replay without state drift.
    /// Positions, roles and outcomes are authored animation cues only.
    /// </summary>
    public static class StoryTimeline
    {
        public const float Duration = 58f;
        public const float SwarmAltitude = 10f;
        public const float RelayAltitude = 16f;
        public const float RelayOrbitRadius = 2f;
        public const float RelayOrbitPeriod = 7f;
        public const float ExplosionDuration = 1.8f;
        private const float DiveDuration = 1.2f;
        public static readonly Vector3 BasePosition = new Vector3(-48f, 0f, 0f);
        public static readonly Vector3[] ObjectivePositions =
        {
            new Vector3(43f, 0f, -14f),
            new Vector3(46f, 0f, 0f),
            new Vector3(43f, 0f, 14f)
        };
        public static readonly Vector3[] RelayPositions =
        {
            new Vector3(-28f, RelayAltitude, 0f),
            new Vector3(-8f, RelayAltitude, 0f),
            new Vector3(12f, RelayAltitude, 0f)
        };

        // The array index is the drone's identity throughout the entire film.
        private static readonly Vector3[] FormationOffsets =
        {
            new Vector3(2.5f, 0f, -2.5f),
            new Vector3(2.5f, 0f, 0f),
            new Vector3(2.5f, 0f, 2.5f),
            new Vector3(0f, 0f, -2.5f),
            new Vector3(0f, 0f, 2.5f),
            new Vector3(-2.5f, 0f, -2.5f),
            new Vector3(-2.5f, 0f, 2.5f),
            new Vector3(-2.5f, 0f, 0f)
        };
        private static readonly Vector3[] FollowUpOffsets =
        {
            new Vector3(-2.5f, 0f, -2.5f),
            new Vector3(-2.5f, 0f, 2.5f),
            new Vector3(0.6f, 0f, 0f)
        };
        private static readonly float[] DetachTimes = { 10f, 17f, 24f };
        private static readonly float[] ReleaseTimes = { 38f, 40f, 42f };
        // One visual impact per identity: the drone is consumed at this keyframe.
        private static readonly float[] ImpactTimes = { 30f, 30.3f, 34f, 34.3f, 37f, 48f, 51f, 54.3f };
        private static readonly int[] ImpactObjectives = { 0, 0, 1, 1, 2, 0, 1, 2 };
        private static readonly Vector3[] PrimaryImpactPositions =
        {
            ObjectivePositions[0] + new Vector3(-0.45f, 0.8f, -0.35f),
            ObjectivePositions[0] + new Vector3(0.45f, 0.8f, 0.35f),
            ObjectivePositions[1] + new Vector3(-0.45f, 0.8f, 0.35f),
            ObjectivePositions[1] + new Vector3(0.45f, 0.8f, -0.35f),
            ObjectivePositions[2] + new Vector3(0f, 0.8f, 0f)
        };
        // This repeat sequence is fixed before playback. The former relays replay
        // prior A/B/C hit positions without selecting targets from later objective state.
        private static readonly Vector3[] ImpactPositions =
        {
            PrimaryImpactPositions[0], PrimaryImpactPositions[1],
            PrimaryImpactPositions[2], PrimaryImpactPositions[3], PrimaryImpactPositions[4],
            PrimaryImpactPositions[0], PrimaryImpactPositions[2], PrimaryImpactPositions[4]
        };
        private static readonly string[] PhaseTitles =
        {
            "Orders cached", "The tail forms", "Primary pass",
            "Release the tail", "Follow-up wave", "Sequence complete"
        };
        private static readonly string[] PhaseDescriptions =
        {
            "Eight drones receive one stored plan from the control point.",
            "Three drones climb and circle as shielded relays, keeping the link.",
            "The primary wave hits A, B and C. Each attack ends in a small burst.",
            "The relays leave their shields and descend to replay the cached A-to-B-to-C sequence.",
            "The former relays repeat the earlier A, B and C hits from the stored plan.",
            "Primary and repeat passes complete. All three objectives are clear."
        };

        private struct Keyframe
        {
            public readonly float Time;
            public readonly Vector3 Position;

            public Keyframe(float time, float x, float z)
            {
                Time = time;
                Position = new Vector3(x, SwarmAltitude, z);
            }
        }

        private static readonly Keyframe[] PrimaryPath =
        {
            new Keyframe(3f, -45f, 0f),
            new Keyframe(10f, -28f, 0f),
            new Keyframe(17f, -8f, 0f),
            new Keyframe(24f, 12f, 0f),
            new Keyframe(27f, 34f, 0f),
            new Keyframe(30f, 43f, -14f),
            new Keyframe(34f, 46f, 0f),
            new Keyframe(37f, 43f, 14f),
            new Keyframe(38f, 43f, 14f)
        };
        private static readonly Keyframe[] FollowUpPath =
        {
            new Keyframe(44f, 34f, 0f),
            new Keyframe(48f, 43f, -14f),
            new Keyframe(51f, 46f, 0f),
            new Keyframe(54.3f, 43f, 14f),
            new Keyframe(58f, 43f, 14f)
        };

        public static StoryFrame Sample(float seconds)
        {
            float time = float.IsNaN(seconds) ? 0f : Mathf.Clamp(seconds, 0f, Duration);
            int phase = time < 3f ? 0 : time < 27f ? 1 : time < 38f ? 2 :
                time < 44f ? 3 : time < Duration ? 4 : 5;

            StoryFrame frame = new StoryFrame
            {
                Time = time,
                PhaseIndex = phase,
                PhaseTitle = PhaseTitles[phase],
                Description = PhaseDescriptions[phase],
                Drones = new DronePose[8],
                Objectives = new[]
                {
                    time >= 30.3f ? ObjectiveState.Cleared : ObjectiveState.Untouched,
                    time >= 51f ? ObjectiveState.Cleared :
                        time >= 34f ? ObjectiveState.Damaged : ObjectiveState.Untouched,
                    time >= 54.3f ? ObjectiveState.Cleared :
                        time >= 37f ? ObjectiveState.Damaged : ObjectiveState.Untouched
                },
                CommsActive = time < 38f,
                ActiveObjective = -1,
                AttackPulse = 0f
            };

            for (int id = 0; id < frame.Drones.Length; id++)
            {
                DronePose pose = SampleFlightDrone(id, time);
                float impactTime = ImpactTimes[id];
                if (time >= impactTime)
                {
                    pose = new DronePose
                    {
                        Position = ImpactPositions[id],
                        Role = DroneRole.Destroyed,
                        Visible = false,
                        Shielded = false
                    };
                }
                else if (time >= impactTime - DiveDuration)
                {
                    float startTime = impactTime - DiveDuration;
                    Vector3 start = SampleFlightDrone(id, startTime).Position;
                    Vector3 velocity = (SampleFlightDrone(id, startTime + 0.01f).Position -
                        SampleFlightDrone(id, startTime - 0.01f).Position) / 0.02f;
                    pose.Position = Hermite(start, ImpactPositions[id], velocity * DiveDuration,
                        Vector3.zero, (time - startTime) / DiveDuration);
                }

                frame.Drones[id] = pose;
                if (pose.Role == DroneRole.Destroyed) frame.DestroyedCount++;
                if (!pose.Visible) continue;
                if (pose.Role == DroneRole.Primary) frame.PrimaryCount++;
                else if (pose.Role == DroneRole.Relay) frame.RelayCount++;
                else if (pose.Role == DroneRole.FollowUp) frame.FollowUpCount++;
            }

            List<ExplosionCue> explosions = new List<ExplosionCue>(2);
            for (int impact = 0; impact < ImpactTimes.Length; impact++)
            {
                float age = time - ImpactTimes[impact];
                if (time >= ImpactTimes[impact] && time < ImpactTimes[impact] + ExplosionDuration)
                {
                    explosions.Add(new ExplosionCue
                    {
                        DroneId = impact,
                        Position = ImpactPositions[impact],
                        Age = age,
                        IsMiss = false
                    });
                    // Compatibility cue follows each authored target impact.
                    float pulse = Mathf.Clamp01(1f - age / 0.65f);
                    if (ImpactObjectives[impact] >= 0 && pulse > frame.AttackPulse)
                    {
                        frame.AttackPulse = pulse;
                        frame.ActiveObjective = ImpactObjectives[impact];
                    }
                }
            }
            frame.Explosions = explosions.ToArray();

            return frame;
        }

        private static DronePose SampleFlightDrone(int id, float time)
        {
            if (id >= 5) return SampleTailDrone(id - 5, time);
            return new DronePose
            {
                Position = Evaluate(PrimaryPath, time) + FormationOffsets[id],
                Role = DroneRole.Primary,
                Visible = true,
                Shielded = false
            };
        }

        private static DronePose SampleTailDrone(int relayIndex, float time)
        {
            float detach = DetachTimes[relayIndex];
            float release = ReleaseTimes[relayIndex];
            Vector3 relayPosition = RelayPositions[relayIndex];
            Vector3 lowPosition = new Vector3(relayPosition.x, SwarmAltitude, relayPosition.z);

            if (time < detach)
            {
                float peelStart = detach - 2f;
                Vector3 position;
                if (time < peelStart)
                {
                    position = Evaluate(PrimaryPath, time) + FormationOffsets[relayIndex + 5];
                }
                else
                {
                    Vector3 start = Evaluate(PrimaryPath, peelStart) + FormationOffsets[relayIndex + 5];
                    // Match the formation velocity before gently braking to the marker.
                    Vector3 velocity = (Evaluate(PrimaryPath, peelStart + 0.01f) -
                        Evaluate(PrimaryPath, peelStart - 0.01f)) / 0.02f;
                    position = Hermite(start, lowPosition, velocity * 2f,
                        Vector3.zero, (time - peelStart) / 2f);
                }
                return new DronePose { Position = position, Role = DroneRole.Primary, Visible = true };
            }

            if (time < release)
            {
                return new DronePose
                {
                    Position = SampleRelayHoldPosition(relayIndex, time),
                    Role = DroneRole.Relay,
                    Visible = true,
                    Shielded = true
                };
            }

            Vector3 followUpPosition;
            if (time < 44f)
            {
                // Join the departure to the moving circle without snapping or stopping.
                Vector3 departure = SampleRelayHoldPosition(relayIndex, release);
                Vector3 velocity = (SampleRelayHoldPosition(relayIndex, release + 0.01f) -
                    SampleRelayHoldPosition(relayIndex, release - 0.01f)) / 0.02f;
                Vector3 destination = FollowUpPath[0].Position + FollowUpOffsets[relayIndex];
                float progress = Mathf.Clamp01((time - release) / (44f - release));
                float remaining = 1f - progress;
                float curve = 16f * progress * progress * remaining * remaining;
                followUpPosition = Hermite(departure, destination, velocity * (44f - release),
                    Vector3.zero, progress) + new Vector3(0f, 0f, -1.1f - relayIndex * 0.15f) * curve;
            }
            else
            {
                followUpPosition = Evaluate(FollowUpPath, time) + FollowUpOffsets[relayIndex];
            }

            return new DronePose
            {
                Position = followUpPosition,
                Role = DroneRole.FollowUp,
                Visible = true,
                Shielded = false
            };
        }

        private static Vector3 SampleRelayHoldPosition(int relayIndex, float time)
        {
            Vector3 center = RelayPositions[relayIndex];
            Vector3 lowPosition = new Vector3(center.x, SwarmAltitude, center.z);
            float rise = Mathf.Clamp01((time - DetachTimes[relayIndex]) / 1.5f);
            float easedRise = rise * rise * (3f - 2f * rise);
            Vector3 position = Vector3.Lerp(lowPosition, center, easedRise);

            // This small, authored display loop begins only after the climb finishes.
            float orbitAge = Mathf.Max(0f, time - DetachTimes[relayIndex] - 1.5f);
            float ramp = Mathf.Clamp01(orbitAge / 0.8f);
            float radius = RelayOrbitRadius * ramp * ramp * (3f - 2f * ramp);
            float angle = orbitAge * (2f * Mathf.PI / RelayOrbitPeriod) +
                relayIndex * (2f * Mathf.PI / 3f);
            return position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        private static Vector3 Evaluate(Keyframe[] path, float time)
        {
            if (time <= path[0].Time) return path[0].Position;
            int last = path.Length - 1;
            if (time >= path[last].Time) return path[last].Position;

            for (int index = 0; index < last; index++)
            {
                if (time > path[index + 1].Time) continue;
                float length = path[index + 1].Time - path[index].Time;
                Vector3 outgoing = index == 0 ? Vector3.zero :
                    (path[index + 1].Position - path[index - 1].Position) /
                    (path[index + 1].Time - path[index - 1].Time);
                Vector3 incoming = index + 1 == last ? Vector3.zero :
                    (path[index + 2].Position - path[index].Position) /
                    (path[index + 2].Time - path[index].Time);
                return Hermite(path[index].Position, path[index + 1].Position,
                    outgoing * length, incoming * length, (time - path[index].Time) / length);
            }
            return path[last].Position;
        }

        private static Vector3 Hermite(Vector3 start, Vector3 end,
            Vector3 startTangent, Vector3 endTangent, float progress)
        {
            float u = Mathf.Clamp01(progress);
            float u2 = u * u;
            float u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * start + (u3 - 2f * u2 + u) * startTangent +
                (-2f * u3 + 3f * u2) * end + (u3 - u2) * endTangent;
        }
    }
}

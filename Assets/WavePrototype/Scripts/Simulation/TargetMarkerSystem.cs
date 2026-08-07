using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Owns the optional roaming target and its deterministic relocation stream.
    /// It deliberately has no route, timer, score, collision, or progression rules.
    /// </summary>
    internal sealed class TargetMarkerSystem
    {
        private readonly SimulationConfig config;
        private readonly IOceanEnvironment environment;
        private DeterministicRandom random;
        private TargetMarkerData data;

        public TargetMarkerData Data => data;
        public uint RandomState => random.State;

        public TargetMarkerSystem(SimulationConfig config, IOceanEnvironment environment)
        {
            this.config = config;
            this.environment = environment;
        }

        public void Reset(int seed, Vector2 playerPosition)
        {
            random = new DeterministicRandom(seed ^ 0x6D2B79);
            data = new TargetMarkerData
            {
                Position = Vector2.zero,
                VisitRadius = Mathf.Clamp(config.DefaultTargetVisitRadius, 2f, 15f),
                VisitCount = 0,
                RelocationCount = 0,
                Enabled = true
            };
            TryRelocate(playerPosition, false);
        }

        public bool Relocate(Vector2 playerPosition) => TryRelocate(playerPosition, true);

        public void SetEnabled(bool enabled) => data.Enabled = enabled;

        public void SetVisitRadius(float radius) => data.VisitRadius = Mathf.Clamp(radius, 2f, 15f);

        public void ResetVisitCount() => data.VisitCount = 0;

        public void Evaluate(IReadOnlyList<BoatData> boats, int playerBoatId,
            List<SimulationEvent> pendingEvents)
        {
            if (!data.Enabled) return;
            for (int i = 0; i < boats.Count; i++)
            {
                BoatData boat = boats[i];
                if (boat.Id != playerBoatId) continue;
                float radiusSquared = data.VisitRadius * data.VisitRadius;
                if ((boat.Position - data.Position).sqrMagnitude > radiusSquared) return;

                Vector2 visitedPosition = data.Position;
                data.VisitCount++;
                TryRelocate(boat.Position, true);
                pendingEvents.Add(new SimulationEvent(SimulationEventType.TargetVisited,
                    0, boat.Id, visitedPosition, data.VisitCount));
                return;
            }
        }

        public bool IsSafePosition(Vector2 position)
        {
            Vector2 half = config.WorldHalfExtents;
            float margin = Mathf.Max(7f, config.TargetSafeClearance + 1f);
            if (Mathf.Abs(position.x) > half.x - margin || Mathf.Abs(position.y) > half.y - margin)
                return false;
            if (environment.SampleDepth(position) < 1.15f) return false;
            if (environment.FindRock(position, config.TargetSafeClearance) >= 0) return false;

            for (int sample = 0; sample < 12; sample++)
            {
                float angle = sample * Mathf.PI * 2f / 12f;
                Vector2 ring = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))
                    * config.TargetSafeClearance;
                if (environment.SampleDepth(ring) < 0.55f ||
                    environment.FindRock(ring, 1.25f) >= 0)
                    return false;
            }
            return true;
        }

        private bool TryRelocate(Vector2 avoidPosition, bool countRelocation)
        {
            Vector2 half = config.WorldHalfExtents;
            float margin = Mathf.Max(7f, config.TargetSafeClearance + 1f);
            float minimumDistance = Mathf.Max(config.TargetMinimumRelocationDistance,
                data.VisitRadius * 2.5f);
            float minimumDistanceSquared = minimumDistance * minimumDistance;

            for (int attempt = 0; attempt < 320; attempt++)
            {
                Vector2 candidate = new Vector2(
                    random.Range(-half.x + margin, half.x - margin),
                    random.Range(-half.y + margin, half.y - margin));
                if ((candidate - avoidPosition).sqrMagnitude < minimumDistanceSquared) continue;
                if (!IsSafePosition(candidate)) continue;
                data.Position = candidate;
                if (countRelocation) data.RelocationCount++;
                return true;
            }

            // Deterministic grid fallback prevents a failed random search from making the
            // marker sticky. The offset varies by RNG state but the result remains replayable.
            float offset = random.Range(0f, 7f);
            for (float y = -half.y + margin + offset; y <= half.y - margin; y += 7f)
            for (float x = -half.x + margin + offset; x <= half.x - margin; x += 7f)
            {
                Vector2 candidate = new Vector2(x, y);
                if ((candidate - avoidPosition).sqrMagnitude < minimumDistanceSquared) continue;
                if (!IsSafePosition(candidate)) continue;
                data.Position = candidate;
                if (countRelocation) data.RelocationCount++;
                return true;
            }
            return false;
        }
    }
}

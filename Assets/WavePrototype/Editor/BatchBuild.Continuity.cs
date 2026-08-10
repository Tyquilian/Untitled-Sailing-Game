using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private readonly struct OceanContinuityProbe
        {
            public readonly int InitialSegments;
            public readonly int MinimumActiveSegments;
            public readonly int ShelfArrivalTick;
            public readonly int ExpirationTick;
            public readonly float MaximumTravelX;
            public readonly float ShelfArrivalDepth;
            public readonly float ShelfArrivalEnergy;
            public readonly bool SurvivedBelowLegacyCutoff;

            public OceanContinuityProbe(int initialSegments, int minimumActiveSegments,
                int shelfArrivalTick, int expirationTick, float maximumTravelX,
                float shelfArrivalDepth, float shelfArrivalEnergy,
                bool survivedBelowLegacyCutoff)
            {
                InitialSegments = initialSegments;
                MinimumActiveSegments = minimumActiveSegments;
                ShelfArrivalTick = shelfArrivalTick;
                ExpirationTick = expirationTick;
                MaximumTravelX = maximumTravelX;
                ShelfArrivalDepth = shelfArrivalDepth;
                ShelfArrivalEnergy = shelfArrivalEnergy;
                SurvivedBelowLegacyCutoff = survivedBelowLegacyCutoff;
            }
        }

        private static OceanContinuityProbe RunOceanContinuityProbe()
        {
            var simulation = new WaveSimulation(1847, new SimulationConfig
            {
                TargetWaveCount = 0,
                InitialFloatingObjectCount = 0
            });
            Vector2 half = simulation.Config.WorldHalfExtents;
            Require(simulation.SpawnSwellFront(new Vector2(-half.x + 0.5f, 0f), 0.82f),
                "Could not create a natural-format boundary front for the continuity probe.");

            int trackedWaveId = simulation.Waves[0].Id;
            int initialSegments = simulation.Waves[0].Segments.Length;
            int minimumActive = initialSegments;
            int legacyCutoff = Mathf.CeilToInt(initialSegments * 0.45f);
            int shelfArrivalTick = -1;
            int expirationTick = -1;
            float maximumX = -half.x;
            float shelfDepth = float.MaxValue;
            float shelfEnergy = 0f;
            bool survivedBelowLegacyCutoff = false;
            float nearShelfX = half.x - 155f;

            // Eight minutes of authoritative time is intentionally longer than the complete
            // boundary-to-land transit. The probe follows one front, not a populated ocean,
            // so it remains cheap while exposing map-size-dependent lifetime regressions.
            const int maximumTicks = 14400;
            for (int step = 0; step < maximumTicks; step++)
            {
                simulation.Step();
                int waveIndex = -1;
                for (int index = 0; index < simulation.Waves.Count; index++)
                {
                    if (simulation.Waves[index].Id != trackedWaveId) continue;
                    waveIndex = index;
                    break;
                }

                if (waveIndex < 0)
                {
                    expirationTick = (int)simulation.Tick;
                    break;
                }

                WaveSegmentCollection segments = simulation.Waves[waveIndex].Segments;
                int active = 0;
                for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    WaveSegmentData segment = segments[segmentIndex];
                    if (!segment.Active) continue;
                    active++;
                    maximumX = Mathf.Max(maximumX, segment.Position.x);
                    float depth = simulation.Environment.SampleDepth(segment.Position);
                    if (shelfArrivalTick < 0 && segment.Position.x >= nearShelfX &&
                        depth > 0.24f && depth < 2.5f)
                    {
                        shelfArrivalTick = (int)simulation.Tick;
                        shelfDepth = depth;
                        shelfEnergy = segment.Energy;
                    }
                }

                minimumActive = Mathf.Min(minimumActive, active);
                if (active > 0 && active < legacyCutoff)
                    survivedBelowLegacyCutoff = true;
            }

            return new OceanContinuityProbe(initialSegments, minimumActive,
                shelfArrivalTick, expirationTick, maximumX, shelfDepth, shelfEnergy,
                survivedBelowLegacyCutoff);
        }
    }
}

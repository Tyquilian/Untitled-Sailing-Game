using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private readonly struct ExplorationScaleProbe
        {
            public readonly int ReferencePhases;
            public readonly int PriorPhases;
            public readonly int ExpandedPhases;
            public readonly int ReferenceRocks;
            public readonly int PriorRocks;
            public readonly int ExpandedRocks;
            public readonly float ReferenceSpacing;
            public readonly float ExpandedSpacing;
            public readonly float CrestScale;
            public readonly int ExplicitOverridePhases;
            public readonly int DisabledWaves;
            public readonly int DisabledObjects;

            public ExplorationScaleProbe(int referencePhases, int priorPhases,
                int expandedPhases, int referenceRocks, int priorRocks,
                int expandedRocks, float referenceSpacing, float expandedSpacing,
                float crestScale, int explicitOverridePhases, int disabledWaves,
                int disabledObjects)
            {
                ReferencePhases = referencePhases;
                PriorPhases = priorPhases;
                ExpandedPhases = expandedPhases;
                ReferenceRocks = referenceRocks;
                PriorRocks = priorRocks;
                ExpandedRocks = expandedRocks;
                ReferenceSpacing = referenceSpacing;
                ExpandedSpacing = expandedSpacing;
                CrestScale = crestScale;
                ExplicitOverridePhases = explicitOverridePhases;
                DisabledWaves = disabledWaves;
                DisabledObjects = disabledObjects;
            }
        }

        private static ExplorationScaleProbe RunExplorationScaleProbe()
        {
            const int seed = 1847;
            var reference = new WaveSimulation(seed, new SimulationConfig
            {
                WorldHalfExtents = new Vector2(225f, 125f),
                TargetWaveCount = -1,
                InitialFloatingObjectCount = 0
            });
            var prior = new WaveSimulation(seed, new SimulationConfig
            {
                WorldHalfExtents = new Vector2(450f, 250f),
                TargetWaveCount = -1,
                InitialFloatingObjectCount = 0
            });
            var expanded = new WaveSimulation(seed, new SimulationConfig
            {
                WorldHalfExtents = new Vector2(675f, 250f),
                TargetWaveCount = -1,
                InitialFloatingObjectCount = 0
            });
            var explicitOverride = new WaveSimulation(seed, new SimulationConfig
            {
                WorldHalfExtents = new Vector2(675f, 250f),
                TargetWaveCount = 17,
                InitialFloatingObjectCount = 0
            });
            var disabled = new WaveSimulation(seed, new SimulationConfig
            {
                TargetWaveCount = 0,
                InitialFloatingObjectCount = 48
            });

            SwellSystemData referenceSystem = reference.SwellSystems[0];
            SwellSystemData expandedSystem = expanded.SwellSystems[0];
            return new ExplorationScaleProbe(reference.InitialWaveTarget,
                prior.InitialWaveTarget, expanded.InitialWaveTarget,
                reference.Environment.Rocks.Count, prior.Environment.Rocks.Count,
                expanded.Environment.Rocks.Count, referenceSystem.PacketSpacing,
                expandedSystem.PacketSpacing, expandedSystem.MeanCrestLength /
                    referenceSystem.MeanCrestLength, explicitOverride.Waves.Count,
                    disabled.Waves.Count, disabled.FloatingObjects.Count);
        }
    }
}

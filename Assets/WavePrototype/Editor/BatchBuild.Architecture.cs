using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private static void ValidateArchitectureBoundaries()
        {
            var sourceConfig = new SimulationConfig
            {
                TargetWaveCount = 2,
                InitialFloatingObjectCount = 0,
                MaximumRecordedBoatControls = 8,
                PendingInputCompactionThreshold = 2
            };
            var simulation = new WaveSimulation(7411, sourceConfig);

            FieldInfo[] builderFields = typeof(SimulationConfig).GetFields(
                BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < builderFields.Length; i++)
            {
                FieldInfo builderField = builderFields[i];
                FieldInfo snapshotField = typeof(SimulationConfigSnapshot).GetField(
                    builderField.Name, BindingFlags.Instance | BindingFlags.Public);
                Require(snapshotField != null && snapshotField.FieldType == builderField.FieldType,
                    $"Immutable config snapshot is missing {builderField.Name}.");
                Require(Equals(builderField.GetValue(sourceConfig),
                        snapshotField.GetValue(simulation.Config)),
                    $"Immutable config snapshot did not copy {builderField.Name}.");
            }

            // The caller's mutable startup builder must no longer be the runtime object.
            sourceConfig.TargetWaveCount = 0;
            sourceConfig.WorldHalfExtents = Vector2.one;
            simulation.Reset(7411);
            Require(simulation.Config.TargetWaveCount == 2 && simulation.Waves.Count == 2,
                "Runtime configuration changed after its startup builder was mutated.");
            Require(simulation.Config.WorldHalfExtents == new Vector2(450f, 250f),
                "Runtime world dimensions changed after startup configuration mutation.");

            // Public views must not be down-castable to their authoritative List/array storage.
            Require(!(simulation.Waves is List<WaveData>) &&
                    !(simulation.Boats is List<BoatData>) &&
                    !(simulation.Events is List<SimulationEvent>),
                "An authoritative entity collection escaped through a castable public view.");
            object segmentView = simulation.Waves[0].Segments;
            Require(!(segmentView is WaveSegmentData[]),
                "A wave exposed its mutable segment backing array.");

            // Default recording is explicit and bounded; pending commands compact internally.
            for (int tick = 0; tick < 24; tick++)
            {
                simulation.SetPlayerControl(0.5f, (tick & 1) == 0 ? 0.25f : -0.25f);
                simulation.Step();
            }
            Require(simulation.RecordedControls.Count == 8,
                $"Bounded input history retained {simulation.RecordedControls.Count}/8 commands.");

            var noHistory = new WaveSimulation(7412, new SimulationConfig
            {
                TargetWaveCount = 0,
                InitialFloatingObjectCount = 0,
                RecordBoatControlHistory = false
            });
            for (int tick = 0; tick < 4; tick++)
            {
                noHistory.SetPlayerControl(1f, 0f);
                noHistory.Step();
            }
            Require(noHistory.RecordedControls.Count == 0,
                "Disabled input recording retained applied commands.");
        }
    }
}

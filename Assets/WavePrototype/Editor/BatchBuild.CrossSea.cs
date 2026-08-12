using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private readonly struct CrossSeaEventProbe
        {
            public readonly int CompletionTick;
            public readonly int EmittedPackets;
            public readonly int MaximumActivePackets;
            public readonly int MaximumWorldFronts;
            public readonly float DirectionSeparation;
            public readonly int ExpectedPeriodTicks;
            public readonly int MinimumIntervalTicks;
            public readonly int MaximumIntervalTicks;
            public readonly int MaximumEmissionBurst;
            public readonly int CarrierMatchingTicks;
            public readonly int LocalOverlapTicks;
            public readonly Vector2 EntryPoint;
            public readonly Vector2 EmissionCenter;
            public readonly int FirstEmissionTick;
            public readonly int FirstEntryTick;
            public readonly int FirstEmissionPendingSegments;
            public readonly int FirstEmissionEnteredSegments;
            public readonly int MaximumPendingSegments;
            public readonly int InteriorSegmentsAtEmission;
            public readonly bool PendingEnergyStable;
            public readonly bool SawBuilding;
            public readonly bool SawEstablished;
            public readonly bool SawDeparting;
            public readonly bool SawDraining;
            public readonly bool Deterministic;
            public readonly int FirstSystemId;
            public readonly int RepeatSystemId;

            public CrossSeaEventProbe(int completionTick, int emittedPackets,
                int maximumActivePackets, int maximumWorldFronts, float directionSeparation,
                int expectedPeriodTicks, int minimumIntervalTicks, int maximumIntervalTicks,
                int maximumEmissionBurst, int carrierMatchingTicks, int localOverlapTicks,
                Vector2 entryPoint, Vector2 emissionCenter, int firstEmissionTick,
                int firstEntryTick, int firstEmissionPendingSegments,
                int firstEmissionEnteredSegments, int maximumPendingSegments,
                int interiorSegmentsAtEmission, bool pendingEnergyStable,
                bool sawBuilding, bool sawEstablished, bool sawDeparting, bool sawDraining,
                bool deterministic, int firstSystemId, int repeatSystemId)
            {
                CompletionTick = completionTick;
                EmittedPackets = emittedPackets;
                MaximumActivePackets = maximumActivePackets;
                MaximumWorldFronts = maximumWorldFronts;
                DirectionSeparation = directionSeparation;
                ExpectedPeriodTicks = expectedPeriodTicks;
                MinimumIntervalTicks = minimumIntervalTicks;
                MaximumIntervalTicks = maximumIntervalTicks;
                MaximumEmissionBurst = maximumEmissionBurst;
                CarrierMatchingTicks = carrierMatchingTicks;
                LocalOverlapTicks = localOverlapTicks;
                EntryPoint = entryPoint;
                EmissionCenter = emissionCenter;
                FirstEmissionTick = firstEmissionTick;
                FirstEntryTick = firstEntryTick;
                FirstEmissionPendingSegments = firstEmissionPendingSegments;
                FirstEmissionEnteredSegments = firstEmissionEnteredSegments;
                MaximumPendingSegments = maximumPendingSegments;
                InteriorSegmentsAtEmission = interiorSegmentsAtEmission;
                PendingEnergyStable = pendingEnergyStable;
                SawBuilding = sawBuilding;
                SawEstablished = sawEstablished;
                SawDeparting = sawDeparting;
                SawDraining = sawDraining;
                Deterministic = deterministic;
                FirstSystemId = firstSystemId;
                RepeatSystemId = repeatSystemId;
            }
        }

        private static CrossSeaEventProbe RunCrossSeaEventProbe()
        {
            SimulationConfig CreateConfig() => new SimulationConfig
            {
                WorldHalfExtents = new Vector2(225f, 125f),
                // The lifecycle probe needs representative carrier overlap, not a second
                // playable-density benchmark. Eight ordered carrier phases keep the long
                // entry/drain proof focused and avoid thermally biasing later stress gates.
                TargetWaveCount = 8,
                InitialFloatingObjectCount = 0,
                CrossSeaAutomaticStartSeconds = -1f,
                CrossSeaBuildSeconds = 6f,
                CrossSeaEstablishedSeconds = 8f,
                CrossSeaDepartureSeconds = 6f,
                EnergyDecayPerSecond = 0f,
                BreakingMinimumEnergyLossPerSecond = 0f,
                BreakingEnergyLossPerSecond = 0f,
                SpentEnergyLossPerSecond = 0f
            };

            var first = new WaveSimulation(9182, CreateConfig(),
                new ConstantDepthEnvironmentFactory(12f));
            var second = new WaveSimulation(9182, CreateConfig(),
                new ConstantDepthEnvironmentFactory(12f));
            var carrierControl = new WaveSimulation(9182, CreateConfig(),
                new ConstantDepthEnvironmentFactory(12f));
            Require(first.CrossSeaEvent.Phase == CrossSeaEventPhase.Inactive &&
                    first.ActiveWaveSourceCount == 1 && first.SwellSystems.Count == 1,
                "Cross-sea probe did not begin from the one-carrier baseline.");
            Require(first.TriggerCrossSeaEvent() && second.TriggerCrossSeaEvent(),
                "Cross-sea event could not authorize its dormant source.");
            int firstSystemId = first.CrossSeaEvent.SwellSystemId;
            Require(firstSystemId > first.SwellSystems[0].Id && first.SwellSystems.Count == 2,
                "Cross-sea trigger did not create a distinct swell system.");

            SwellSystemData carrier = first.SwellSystems[0];
            SwellSystemData cross = first.SwellSystems[1];
            Require(cross.UsesDirectionalBoundaryEntry,
                "Cross-sea system did not select directional boundary entry.");
            float directionSeparation = Vector2.Angle(carrier.Direction, cross.Direction);
            int expectedPeriodTicks = Mathf.CeilToInt(cross.CalmGapSeconds /
                first.Config.FixedDeltaTime);
            int priorEventPackets = 0;
            int priorEmissionTick = -1;
            int minimumInterval = int.MaxValue;
            int maximumInterval = 0;
            int maximumBurst = 0;
            int maximumActive = 0;
            int maximumWorld = first.Waves.Count;
            int carrierMatchingTicks = 0;
            int localOverlapTicks = 0;
            int firstEmissionTick = 0;
            int firstEntryTick = 0;
            int firstEmissionPending = 0;
            int firstEmissionEntered = 0;
            int maximumPending = 0;
            int interiorAtEmission = 0;
            bool pendingEnergyStable = true;
            bool deterministic = first.CalculateStateHash() == second.CalculateStateHash();
            bool sawBuilding = false, sawEstablished = false, sawDeparting = false, sawDraining = false;
            int completionTick = 0;

            for (int step = 0; step < 4800; step++)
            {
                first.SetPlayerControl(0f, 0f);
                second.SetPlayerControl(0f, 0f);
                carrierControl.SetPlayerControl(0f, 0f);
                first.Step();
                second.Step();
                carrierControl.Step();
                deterministic &= first.CalculateStateHash() == second.CalculateStateHash();

                CrossSeaEventData state = first.CrossSeaEvent;
                sawBuilding |= state.Phase == CrossSeaEventPhase.Building;
                sawEstablished |= state.Phase == CrossSeaEventPhase.Established;
                sawDeparting |= state.Phase == CrossSeaEventPhase.Departing;
                sawDraining |= state.Phase == CrossSeaEventPhase.Draining;
                maximumActive = Mathf.Max(maximumActive, state.ActivePacketCount);
                maximumWorld = Mathf.Max(maximumWorld, first.Waves.Count);
                int pendingSegments = CountSystemSegments(first, state.SwellSystemId, false);
                int enteredSegments = CountSystemSegments(first, state.SwellSystemId, true);
                maximumPending = Mathf.Max(maximumPending, pendingSegments);
                if (firstEntryTick == 0 && enteredSegments > 0)
                    firstEntryTick = (int)first.Tick;
                pendingEnergyStable &= PendingSystemEnergyIsStable(first,
                    state.SwellSystemId);

                int burst = state.EmittedPacketCount - priorEventPackets;
                maximumBurst = Mathf.Max(maximumBurst, burst);
                if (burst > 0)
                {
                    int emissionTick = (int)first.Tick;
                    if (firstEmissionTick == 0)
                    {
                        firstEmissionTick = emissionTick;
                        firstEmissionPending = pendingSegments;
                        firstEmissionEntered = enteredSegments;
                        interiorAtEmission = CountInteriorSystemSegments(first,
                            state.SwellSystemId, 2f);
                    }
                    if (priorEmissionTick >= 0)
                    {
                        int interval = emissionTick - priorEmissionTick;
                        minimumInterval = Mathf.Min(minimumInterval, interval);
                        maximumInterval = Mathf.Max(maximumInterval, interval);
                    }
                    priorEmissionTick = emissionTick;
                    priorEventPackets = state.EmittedPacketCount;
                }

                WaveSourceData eventCarrier = FindWaveSource(first, WaveSourceKind.WesternSwell);
                WaveSourceData controlCarrier = FindWaveSource(carrierControl,
                    WaveSourceKind.WesternSwell);
                if (eventCarrier.SpawnedPackets == controlCarrier.SpawnedPackets &&
                    eventCarrier.NextEmissionTick == controlCarrier.NextEmissionTick)
                    carrierMatchingTicks++;
                if (CountNearbySourceFronts(first, 1, Vector2.zero, 45f) > 0 &&
                    CountNearbySourceFronts(first, state.SourceId, Vector2.zero, 45f) > 0)
                    localOverlapTicks++;

                if (state.Phase == CrossSeaEventPhase.Inactive && state.EventId > 0)
                {
                    completionTick = (int)first.Tick;
                    break;
                }
            }

            CrossSeaEventData completed = first.CrossSeaEvent;
            int completedPackets = completed.EmittedPacketCount;
            Require(completionTick > 0 && completed.ActivePacketCount == 0,
                "Cross-sea event did not drain all emitted fronts.");
            WaveSourceData completedSource = FindWaveSource(first, completed.SourceKind);
            Require(!completedSource.Enabled && first.ActiveWaveSourceCount == 1,
                "Cross-sea source remained authorized after its fronts drained.");

            Require(first.TriggerCrossSeaEvent() && second.TriggerCrossSeaEvent(),
                "A completed cross-sea event could not be repeated.");
            int repeatSystemId = first.CrossSeaEvent.SwellSystemId;
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            Require(repeatSystemId != firstSystemId && first.SwellSystems.Count == 3,
                "Repeated cross-sea event reused stale stream identity.");
            Require(first.RequestCrossSeaDeparture() && second.RequestCrossSeaDeparture(),
                "Manual departure could not end a repeated cross-sea event.");
            Require(first.CrossSeaEvent.Phase == CrossSeaEventPhase.Departing &&
                    second.CrossSeaEvent.Phase == CrossSeaEventPhase.Departing,
                "Manual departure did not enter the authoritative departure phase.");
            first.Step();
            second.Step();
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();

            SimulationConfig automaticConfig = CreateConfig();
            automaticConfig.TargetWaveCount = 1;
            automaticConfig.CrossSeaAutomaticStartSeconds = 0.5f;
            var automatic = new WaveSimulation(9183, automaticConfig,
                new ConstantDepthEnvironmentFactory(12f));
            for (int tick = 0; tick <= 15; tick++) automatic.Step();
            Require(automatic.CrossSeaEvent.TriggerCount == 1 &&
                    automatic.CrossSeaEvent.StartedTick == 15 &&
                    automatic.CrossSeaEvent.Phase == CrossSeaEventPhase.Building,
                "Configured automatic cross-sea start did not fire on its deterministic tick.");

            SimulationConfig southernConfig = CreateConfig();
            southernConfig.TargetWaveCount = 1;
            southernConfig.CrossSeaSourceKind = WaveSourceKind.SouthernCrossSea;
            var southern = new WaveSimulation(9184, southernConfig,
                new ConstantDepthEnvironmentFactory(12f));
            Require(southern.TriggerCrossSeaEvent(),
                "Southern directional-entry source could not be triggered.");
            SwellSystemData southernSystem = southern.SwellSystems[1];
            Require(Vector2.Distance(southernSystem.BoundaryEntryPoint,
                        new Vector2(-225f, -125f)) < 0.01f &&
                    southernSystem.UsesDirectionalBoundaryEntry,
                $"Southern cross-sea selected invalid upstream entry {southernSystem.BoundaryEntryPoint}.");
            for (int tick = 0; tick < 300 && southern.CrossSeaEvent.EmittedPacketCount == 0; tick++)
                southern.Step();
            Require(southern.CrossSeaEvent.EmittedPacketCount == 1 &&
                    CountSystemSegments(southern, southernSystem.Id, true) == 0 &&
                    CountSystemSegments(southern, southernSystem.Id, false) >= 30,
                "Southern cross-sea materialized inside the map instead of pending at its corner.");

            return new CrossSeaEventProbe(completionTick, completedPackets,
                maximumActive, maximumWorld, directionSeparation, expectedPeriodTicks,
                minimumInterval == int.MaxValue ? 0 : minimumInterval, maximumInterval,
                maximumBurst, carrierMatchingTicks, localOverlapTicks,
                cross.BoundaryEntryPoint, cross.EmissionCenter, firstEmissionTick,
                firstEntryTick, firstEmissionPending, firstEmissionEntered,
                maximumPending, interiorAtEmission, pendingEnergyStable,
                sawBuilding, sawEstablished, sawDeparting, sawDraining, deterministic,
                firstSystemId, repeatSystemId);
        }

        private static WaveSourceData FindWaveSource(WaveSimulation simulation,
            WaveSourceKind kind)
        {
            for (int i = 0; i < simulation.WaveSources.Count; i++)
                if (simulation.WaveSources[i].Kind == kind) return simulation.WaveSources[i];
            return default;
        }

        private static int CountNearbySourceFronts(WaveSimulation simulation, int sourceId,
            Vector2 point, float radius)
        {
            float radiusSquared = radius * radius;
            int count = 0;
            for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
            {
                WaveData wave = simulation.Waves[waveIndex];
                if (wave.SourceId != sourceId) continue;
                WaveSegmentCollection segments = wave.Segments;
                for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    WaveSegmentData segment = segments[segmentIndex];
                    if (!segment.Active || segment.State == WaveState.PendingEntry ||
                        (segment.Position - point).sqrMagnitude > radiusSquared)
                        continue;
                    count++;
                    break;
                }
            }
            return count;
        }

        private static int CountSystemSegments(WaveSimulation simulation, int systemId,
            bool entered)
        {
            int count = 0;
            for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
            {
                WaveData wave = simulation.Waves[waveIndex];
                if (wave.SwellSystemId != systemId) continue;
                for (int segmentIndex = 0; segmentIndex < wave.Segments.Length; segmentIndex++)
                {
                    WaveSegmentData segment = wave.Segments[segmentIndex];
                    if ((segment.State != WaveState.PendingEntry) == entered &&
                        (entered ? segment.Active : true)) count++;
                }
            }
            return count;
        }

        private static bool PendingSystemEnergyIsStable(WaveSimulation simulation, int systemId)
        {
            for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
            {
                WaveData wave = simulation.Waves[waveIndex];
                if (wave.SwellSystemId != systemId) continue;
                float pendingEnergy = 0f;
                bool foundPending = false;
                for (int segmentIndex = 0; segmentIndex < wave.Segments.Length; segmentIndex++)
                {
                    WaveSegmentData segment = wave.Segments[segmentIndex];
                    if (segment.State != WaveState.PendingEntry) continue;
                    if (!foundPending)
                    {
                        pendingEnergy = segment.Energy;
                        foundPending = true;
                    }
                    if (Mathf.Abs(segment.Energy - pendingEnergy) > 0.0001f ||
                        segment.BreakingIntensity > 0f || segment.FoamEnergy > 0f)
                        return false;
                }
            }
            return true;
        }

        private static int CountInteriorSystemSegments(WaveSimulation simulation,
            int systemId, float minimumEdgeDistance)
        {
            int count = 0;
            Vector2 half = simulation.Config.WorldHalfExtents;
            for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
            {
                WaveData wave = simulation.Waves[waveIndex];
                if (wave.SwellSystemId != systemId) continue;
                for (int segmentIndex = 0; segmentIndex < wave.Segments.Length; segmentIndex++)
                {
                    WaveSegmentData segment = wave.Segments[segmentIndex];
                    if (!segment.Active || segment.State == WaveState.PendingEntry) continue;
                    float edgeDistance = Mathf.Min(half.x - Mathf.Abs(segment.Position.x),
                        half.y - Mathf.Abs(segment.Position.y));
                    if (edgeDistance > minimumEdgeDistance) count++;
                }
            }
            return count;
        }
    }
}

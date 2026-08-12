using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Owns deterministic wave creation. Each boundary source now maintains one persistent,
    /// ocean-scale swell stream and emits individual phase fronts on an authoritative clock.
    /// Population is an outcome of that clock and front lifetime, never a spawn trigger.
    /// Source zero and swell-system zero remain reserved for local manual tests.
    /// </summary>
    internal sealed class WaveSourceSystem
    {
        private readonly SimulationConfig config;
        private readonly IOceanEnvironment environment;
        private readonly List<WaveSourceData> sources = new List<WaveSourceData>(4);
        private readonly List<SwellSystemData> swellSystems = new List<SwellSystemData>(4);
        private readonly Dictionary<int, int> activeSystemCounts = new Dictionary<int, int>(8);
        private readonly int[] streamSystemIds = new int[3];
        private readonly ReadOnlyCollection<WaveSourceData> sourceView;
        private readonly ReadOnlyCollection<SwellSystemData> swellSystemView;
        private DeterministicRandom random;
        private int nextWaveId;
        private int nextSwellSystemId;
        private int maintenanceCursor;

        public IReadOnlyList<WaveSourceData> Sources => sourceView;
        public IReadOnlyList<SwellSystemData> SwellSystems => swellSystemView;
        public uint RandomState => random.State;
        public int NextWaveId => nextWaveId;
        public int NextSwellSystemId => nextSwellSystemId;
        public bool HasDirectionalBoundarySystems { get; private set; }

        public WaveSourceSystem(SimulationConfig config, IOceanEnvironment environment)
        {
            this.config = config;
            this.environment = environment;
            sourceView = sources.AsReadOnly();
            swellSystemView = swellSystems.AsReadOnly();
        }

        public void Reset(int seed)
        {
            random = new DeterministicRandom(seed ^ 0x31A95);
            nextWaveId = 1;
            nextSwellSystemId = 1;
            maintenanceCursor = 0;
            HasDirectionalBoundarySystems = false;
            sources.Clear();
            swellSystems.Clear();
            for (int i = 0; i < streamSystemIds.Length; i++) streamSystemIds[i] = 0;

            Vector2 half = config.WorldHalfExtents;
            sources.Add(CreateSource(1, WaveSourceKind.WesternSwell,
                new Vector2(-half.x, -half.y + 2f), new Vector2(-half.x, half.y - 2f),
                Vector2.right, 0.6f, 1f, 2.3f, 2.7f, true));
            sources.Add(CreateSource(2, WaveSourceKind.NorthernCrossSea,
                new Vector2(-half.x, half.y), new Vector2(half.x, half.y),
                DirectionFromDegrees(-58f), 3.8f, 0f, 4.2f, 6.2f, false));
            sources.Add(CreateSource(3, WaveSourceKind.SouthernCrossSea,
                new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y),
                DirectionFromDegrees(42f), 3.8f, 0f, 4.5f, 6.5f, false));
        }

        public void PopulateInitialWorld(List<WaveData> waves, int targetCount)
        {
            if (targetCount <= 0) return;
            EnsureContinuousStreams();

            int assigned = 0;
            float totalWeight = TotalSourceWeight();
            var counts = new int[sources.Count];
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                if (!sources[sourceIndex].Enabled) continue;
                counts[sourceIndex] = Mathf.FloorToInt(targetCount *
                    sources[sourceIndex].SelectionWeight / totalWeight);
                assigned += counts[sourceIndex];
            }
            for (int remainder = assigned; remainder < targetCount; remainder++)
                counts[NextEnabledSourceIndex(remainder)]++;

            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                for (int packet = 0; packet < counts[sourceIndex]; packet++)
                    TrySeedContinuousFront(waves, sourceIndex, packet);
            }

            int guard = 0;
            while (waves.Count < targetCount && guard++ < targetCount * 4 + 32)
                TrySeedContinuousFront(waves, NextEnabledSourceIndex(maintenanceCursor++), guard + targetCount);

            // Extreme density diagnostics can exhaust the useful phase/lane combinations
            // long before they exhaust open water. Fill only that benchmark tail with
            // deterministic stream-attributed samples so the requested count is real.
            guard = 0;
            while (waves.Count < targetCount && guard++ < targetCount * 20 + 64)
                TrySeedHighDensityFallback(waves, NextEnabledSourceIndex(maintenanceCursor++));

            for (int systemIndex = 0; systemIndex < swellSystems.Count; systemIndex++)
            {
                SwellSystemData system = swellSystems[systemIndex];
                system.InitialPacketCount = CountWavesInSystem(waves, system.Id);
                system.EmittedPacketCount = system.InitialPacketCount;
                system.ActivePacketCount = system.InitialPacketCount;
                swellSystems[systemIndex] = system;
            }

            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                WaveSourceData source = sources[sourceIndex];
                if (!source.Enabled)
                {
                    source.NextEmissionTick = ulong.MaxValue;
                    sources[sourceIndex] = source;
                    continue;
                }
                SwellSystemData system = swellSystems[FindSystemIndex(streamSystemIds[sourceIndex])];
                source.SpawnedSystems = 1;
                source.SpawnedTrains = source.SpawnedPackets;
                // Initial fronts begin half a phase inside the source boundary, so the next
                // boundary front is due after the other half of that same phase. There is no
                // randomized startup gap: initial placement and runtime emission share one clock.
                source.NextEmissionTick = SecondsToTicks(system.CalmGapSeconds * 0.5f);
                sources[sourceIndex] = source;
            }
        }

        public void MaintainPopulation(List<WaveData> waves, int targetCount, ulong currentTick,
            int eventSourceId = 0, float eventEnergyScale = 1f)
        {
            UpdateSystemActivity(waves);
            if (targetCount <= 0) return;
            EnsureContinuousStreams(currentTick);

            // The source clock is authoritative. The resolved initial target reconstructs an
            // already-running sea; it does not authorize population refills or suppress phases.
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                WaveSourceData source = sources[sourceIndex];
                if (!source.Enabled) continue;
                if (source.NextEmissionTick > currentTick) continue;
                float energyScale = source.Id == eventSourceId
                    ? Mathf.Clamp01(eventEnergyScale) : 1f;
                TryEmitContinuousFront(waves, sourceIndex, energyScale);
                source = sources[sourceIndex];
                SwellSystemData system = swellSystems[FindSystemIndex(streamSystemIds[sourceIndex])];
                source.NextEmissionTick += SecondsToTicks(system.CalmGapSeconds);
                sources[sourceIndex] = source;
            }
        }

        public bool StartEventSource(WaveSourceKind kind, ulong currentTick,
            out int sourceId, out int swellSystemId)
        {
            sourceId = 0;
            swellSystemId = 0;
            int sourceIndex = FindSourceIndex(kind);
            if (sourceIndex < 0 || kind == WaveSourceKind.WesternSwell) return false;
            WaveSourceData source = sources[sourceIndex];
            if (source.Enabled || streamSystemIds[sourceIndex] != 0) return false;

            source.Enabled = true;
            source.NextEmissionTick = ulong.MaxValue;
            sources[sourceIndex] = source;
            EnsureContinuousStreams(currentTick);
            int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
            if (systemIndex < 0)
            {
                source.Enabled = false;
                sources[sourceIndex] = source;
                return false;
            }

            SwellSystemData system = swellSystems[systemIndex];
            source = sources[sourceIndex];
            source.SpawnedSystems++;
            source.NextEmissionTick = currentTick + SecondsToTicks(system.CalmGapSeconds * 0.5f);
            sources[sourceIndex] = source;
            sourceId = source.Id;
            swellSystemId = system.Id;
            return true;
        }

        public bool StopEventSource(int sourceId)
        {
            int sourceIndex = FindSourceIndex(sourceId);
            if (sourceIndex < 0 || sources[sourceIndex].Kind == WaveSourceKind.WesternSwell)
                return false;
            WaveSourceData source = sources[sourceIndex];
            source.Enabled = false;
            source.NextEmissionTick = ulong.MaxValue;
            sources[sourceIndex] = source;
            return true;
        }

        public bool ReleaseEventStream(int sourceId, int swellSystemId)
        {
            int sourceIndex = FindSourceIndex(sourceId);
            int systemIndex = FindSystemIndex(swellSystemId);
            if (sourceIndex < 0 || systemIndex < 0 || sources[sourceIndex].Enabled ||
                streamSystemIds[sourceIndex] != swellSystemId ||
                swellSystems[systemIndex].ActivePacketCount > 0)
                return false;
            streamSystemIds[sourceIndex] = 0;
            return true;
        }

        public bool TryGetSource(WaveSourceKind kind, out WaveSourceData source)
        {
            int index = FindSourceIndex(kind);
            source = index < 0 ? default : sources[index];
            return index >= 0;
        }

        public int GetSystemActivePacketCount(int systemId)
        {
            int index = FindSystemIndex(systemId);
            return index < 0 ? 0 : swellSystems[index].ActivePacketCount;
        }

        public bool SpawnSwellFront(List<WaveData> waves, Vector2 position, float energy)
        {
            EnsureContinuousStreams();
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                WaveSourceData source = sources[sourceIndex];
                if (!source.Enabled) continue;
                int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
                if (systemIndex < 0) continue;
                SwellSystemData system = swellSystems[systemIndex];
                AddSystemWave(waves, system.Id, source.Id, position, system.Direction,
                    energy, system.MeanPacketLength, system.MeanCrestLength,
                    source.SpawnedPackets);
                source.SpawnedTrains++;
                source.SpawnedPackets++;
                source.SpawnedSystems = Mathf.Max(1, source.SpawnedSystems);
                sources[sourceIndex] = source;
                system.EmittedPacketCount++;
                system.ActivePacketCount++;
                swellSystems[systemIndex] = system;
                return true;
            }
            return false;
        }

        public void SpawnManual(List<WaveData> waves, Vector2 position, Vector2 direction, float energy)
            => AddManualWave(waves, position, direction, energy);

        public void SpawnManualForValidation(List<WaveData> waves, Vector2 position,
            Vector2 direction, float energy, float packetLength, float crestLength)
            => AddManualWaveExact(waves, position, direction, energy, packetLength, crestLength);

        public float DeepWaterCruiseSpeed(float packetLength)
            => Mathf.Min(config.BaseWaveSpeed, 3.2f + Mathf.Sqrt(Mathf.Max(0.1f, packetLength)) * 2.45f);

        public int ResolveInitialWaveCount(int configuredCount)
        {
            if (configuredCount >= 0) return configuredCount;
            EnsureContinuousStreams();
            int count = 0;
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                WaveSourceData source = sources[sourceIndex];
                if (!source.Enabled) continue;
                int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
                if (systemIndex < 0) continue;
                SwellSystemData system = swellSystems[systemIndex];
                Vector2 boundary = (source.SegmentStart + source.SegmentEnd) * 0.5f;
                float travelSpan = DistanceToWorldExit(boundary, system.Direction);
                count += Mathf.Max(1, Mathf.FloorToInt((travelSpan - 1f) /
                    Mathf.Max(1f, system.PacketSpacing)));
            }
            return count;
        }

        private void EnsureContinuousStreams(ulong bornTick = 0)
        {
            for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
            {
                if (streamSystemIds[sourceIndex] != 0) continue;
                WaveSourceData source = sources[sourceIndex];
                if (!source.Enabled) continue;
                float meanPacketLength = random.Range(4.35f, 5.9f);
                Vector2 preliminaryDirection = Rotate(source.Direction,
                    random.Range(-source.DirectionSpreadDegrees * 0.28f,
                        source.DirectionSpreadDegrees * 0.28f));
                Vector2 crestAxis = new Vector2(-preliminaryDirection.y,
                    preliminaryDirection.x);
                Vector2 half = config.WorldHalfExtents;
                float crossMapSpan = 2f * (Mathf.Abs(crestAxis.x) * half.x +
                    Mathf.Abs(crestAxis.y) * half.y);
                float meanCrestLength = crossMapSpan +
                    Mathf.Max(8f, config.WaveSegmentTargetSpacing * 1.15f);
                Vector2 boundaryEntryPoint = (source.SegmentStart + source.SegmentEnd) * 0.5f;
                Vector2 emissionCenter = boundaryEntryPoint;
                bool directionalEntry = source.EntryMode == WaveSourceEntryMode.DirectionalCorner;
                if (directionalEntry)
                {
                    CalculateDirectionalBoundaryEntry(preliminaryDirection,
                        out boundaryEntryPoint, out emissionCenter, out meanCrestLength);
                    HasDirectionalBoundarySystems = true;
                }
                float period = random.Range(source.MinimumCalmSeconds, source.MaximumCalmSeconds);
                float spacing = DeepWaterCruiseSpeed(meanPacketLength) * period;
                var system = new SwellSystemData
                {
                    Id = nextSwellSystemId++,
                    SourceId = source.Id,
                    Direction = preliminaryDirection,
                    BoundaryEntryPoint = boundaryEntryPoint,
                    EmissionCenter = emissionCenter,
                    BaseEnergy = random.Range(source.MinimumEnergy, source.MaximumEnergy),
                    PacketSpacing = spacing,
                    MeanPacketLength = meanPacketLength,
                    MeanCrestLength = meanCrestLength,
                    CalmGapSeconds = period,
                    InitialPacketCount = 0,
                    EmittedPacketCount = 0,
                    ActivePacketCount = 0,
                    BornTick = bornTick,
                    UsesDirectionalBoundaryEntry = directionalEntry
                };
                streamSystemIds[sourceIndex] = system.Id;
                swellSystems.Add(system);
            }
        }

        private bool TrySeedContinuousFront(List<WaveData> waves, int sourceIndex, int packetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= sources.Count) return false;
            WaveSourceData source = sources[sourceIndex];
            if (!source.Enabled) return false;
            int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
            if (systemIndex < 0) return false;
            SwellSystemData system = swellSystems[systemIndex];

            for (int attempt = 0; attempt < 18; attempt++)
            {
                Vector2 boundary = system.EmissionCenter;
                float maximumTravel = DistanceToWorldExit(boundary, system.Direction);
                int slots = Mathf.Max(1, Mathf.FloorToInt((maximumTravel - 1f) /
                    Mathf.Max(1f, system.PacketSpacing)));
                float offset = Mathf.Min(maximumTravel - 0.5f,
                    system.PacketSpacing * (0.5f + packetIndex % slots));
                if (offset <= 0.2f) continue;

                Vector2 direction = Rotate(system.Direction,
                    Mathf.Sin(packetIndex * 0.73f + source.Id) * 0.12f);
                Vector2 position = boundary + direction * offset;
                if (!InsideWorld(position) || environment.IsLand(position)) continue;
                float energy = system.BaseEnergy *
                    (0.9f + 0.13f * Mathf.Sin(packetIndex * 0.31f + source.Id * 1.7f));
                AddSystemWave(waves, system.Id, source.Id, position, direction, energy,
                    system.MeanPacketLength, system.MeanCrestLength, source.SpawnedPackets,
                    false);
                source.SpawnedPackets++;
                sources[sourceIndex] = source;
                return true;
            }
            return false;
        }

        private bool TryEmitContinuousFront(List<WaveData> waves, int sourceIndex,
            float energyScale)
        {
            if (sourceIndex < 0 || sourceIndex >= sources.Count) return false;
            WaveSourceData source = sources[sourceIndex];
            if (!source.Enabled) return false;
            int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
            if (systemIndex < 0) return false;
            SwellSystemData system = swellSystems[systemIndex];
            int phaseIndex = source.SpawnedPackets;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 boundary = system.EmissionCenter;
                Vector2 direction = system.UsesDirectionalBoundaryEntry
                    ? system.Direction
                    : Rotate(system.Direction,
                        Mathf.Sin(phaseIndex * 0.29f + source.Id) * 0.12f);
                Vector2 position = boundary + direction * 0.45f;
                if (!system.UsesDirectionalBoundaryEntry &&
                    (!InsideWorld(position) || environment.IsLand(position))) continue;

                float energy = system.BaseEnergy *
                    (0.88f + 0.16f * Mathf.Sin(phaseIndex * 0.23f + source.Id * 0.7f)) *
                    energyScale;
                AddSystemWave(waves, system.Id, source.Id, position, direction, energy,
                    system.MeanPacketLength, system.MeanCrestLength, phaseIndex,
                    system.UsesDirectionalBoundaryEntry);
                source.SpawnedTrains++;
                source.SpawnedSystems = Mathf.Max(1, source.SpawnedSystems);
                source.SpawnedPackets++;
                sources[sourceIndex] = source;
                system.EmittedPacketCount++;
                system.ActivePacketCount++;
                swellSystems[systemIndex] = system;
                return true;
            }
            return false;
        }

        private bool TrySeedHighDensityFallback(List<WaveData> waves, int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= sources.Count) return false;
            WaveSourceData source = sources[sourceIndex];
            if (!source.Enabled) return false;
            int systemIndex = FindSystemIndex(streamSystemIds[sourceIndex]);
            if (systemIndex < 0) return false;
            SwellSystemData system = swellSystems[systemIndex];
            Vector2 half = config.WorldHalfExtents;
            float longitudinalHalfSpan = Mathf.Max(1f,
                Mathf.Abs(system.Direction.x) * half.x +
                Mathf.Abs(system.Direction.y) * half.y - 3f);
            // A full-map crest must stay centered on its cross-swell axis. Random lateral
            // offsets would place most sections outside the world and are not a valid load.
            Vector2 position = system.Direction *
                random.Range(-longitudinalHalfSpan, longitudinalHalfSpan);
            // This tail exists only to fill artificial high-density diagnostics. Seed it
            // in the basin so thousands of fallback fronts do not all begin already breaking.
            if (environment.IsLand(position) || environment.SampleDepth(position) < 7f) return false;
            int phaseIndex = source.SpawnedPackets;
            Vector2 direction = Rotate(system.Direction,
                Mathf.Sin(phaseIndex * 0.29f + source.Id) * 0.12f);
            float energy = system.BaseEnergy *
                (0.88f + 0.16f * Mathf.Sin(phaseIndex * 0.23f + source.Id * 0.7f));
            AddSystemWave(waves, system.Id, source.Id, position, direction, energy,
                system.MeanPacketLength, system.MeanCrestLength, phaseIndex, false);
            source.SpawnedPackets++;
            sources[sourceIndex] = source;
            return true;
        }

        private void UpdateSystemActivity(IReadOnlyList<WaveData> waves)
        {
            activeSystemCounts.Clear();
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                int systemId = waves[waveIndex].SwellSystemId;
                if (systemId <= 0) continue;
                activeSystemCounts.TryGetValue(systemId, out int count);
                activeSystemCounts[systemId] = count + 1;
            }

            for (int systemIndex = 0; systemIndex < swellSystems.Count; systemIndex++)
            {
                SwellSystemData system = swellSystems[systemIndex];
                activeSystemCounts.TryGetValue(system.Id, out int active);
                system.ActivePacketCount = active;
                swellSystems[systemIndex] = system;
            }
        }

        private void AddSystemWave(List<WaveData> waves, int swellSystemId, int sourceId,
            Vector2 position, Vector2 direction, float energy, float meanPacketLength,
            float meanCrestLength, int phaseIndex, bool directionalBoundaryEntry = false)
        {
            direction = direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
            energy = Mathf.Clamp(energy, 0.08f, 3.2f);
            // Phase-local variation prevents a temporary source from perturbing the carrier
            // swell's shape sequence through a shared random-number stream.
            float packetVariation = Frac(Mathf.Sin((swellSystemId * 71 + phaseIndex * 29) *
                0.173f) * 43758.5453f);
            float crestVariation = Frac(Mathf.Sin((swellSystemId * 43 + phaseIndex * 47) *
                0.219f) * 24634.6345f);
            float packetLength = meanPacketLength * Mathf.Lerp(0.97f, 1.03f, packetVariation);
            float crestLength = meanCrestLength * (directionalBoundaryEntry
                ? Mathf.Lerp(0.97f, 1f, crestVariation)
                : Mathf.Lerp(0.97f, 1.03f, crestVariation));
            float speed = DeepWaterCruiseSpeed(packetLength);
            waves.Add(new WaveData
            {
                Id = nextWaveId++,
                SourceId = sourceId,
                SwellSystemId = swellSystemId,
                Position = position,
                TravelDirection = direction,
                Energy = energy,
                Speed = speed,
                PacketLength = packetLength,
                CrestLength = crestLength,
                State = directionalBoundaryEntry ? WaveState.PendingEntry : WaveState.Traveling,
                MutableSegments = CreateSegments(position, direction, energy, speed, crestLength,
                    directionalBoundaryEntry)
            });
        }

        private void AddManualWave(List<WaveData> waves, Vector2 position, Vector2 direction, float energy)
        {
            energy = Mathf.Clamp(energy, 0.08f, 3.2f);
            float energyScale = Mathf.Lerp(0.82f, 1.72f,
                Mathf.InverseLerp(0.08f, 3.2f, energy));
            float packetLength = random.Range(2.5f, 4.05f) * energyScale;
            float crestLength = random.Range(5f, 8.15f) * energyScale;
            AddManualWaveExact(waves, position, direction, energy, packetLength, crestLength);
        }

        private void AddManualWaveExact(List<WaveData> waves, Vector2 position,
            Vector2 direction, float energy, float packetLength, float crestLength)
        {
            direction = direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
            energy = Mathf.Clamp(energy, 0.08f, 3.2f);
            packetLength = Mathf.Max(0.25f, packetLength);
            crestLength = Mathf.Max(0.5f, crestLength);
            float speed = DeepWaterCruiseSpeed(packetLength);
            waves.Add(new WaveData
            {
                Id = nextWaveId++,
                SourceId = 0,
                SwellSystemId = 0,
                Position = position,
                TravelDirection = direction,
                Energy = energy,
                Speed = speed,
                PacketLength = packetLength,
                CrestLength = crestLength,
                State = WaveState.Traveling,
                MutableSegments = CreateSegments(position, direction, energy, speed, crestLength,
                    false)
            });
        }

        private WaveSegmentData[] CreateSegments(Vector2 position, Vector2 direction,
            float energy, float speed, float crestLength, bool directionalBoundaryEntry)
        {
            int count = crestLength < 16f
                ? 1
                : Mathf.Clamp(Mathf.RoundToInt(crestLength /
                    Mathf.Max(2f, config.WaveSegmentTargetSpacing)) + 1,
                    5, Mathf.Max(5, config.WaveMaximumSegments));
            var segments = new WaveSegmentData[count];
            Vector2 crestAxis = new Vector2(-direction.y, direction.x);
            for (int index = 0; index < count; index++)
            {
                float crest01 = count == 1 ? 0.5f : index / (count - 1f);
                Vector2 segmentPosition = position + crestAxis * ((crest01 - 0.5f) * crestLength);
                bool enteredWorld = !directionalBoundaryEntry || InsideWorld(segmentPosition);
                float depth = enteredWorld ? environment.SampleDepth(segmentPosition) : 12f;
                segments[index] = new WaveSegmentData
                {
                    Index = index,
                    PreviousPosition = segmentPosition,
                    Position = segmentPosition,
                    TravelDirection = direction,
                    Energy = energy,
                    Speed = speed,
                    SampledDepth = depth,
                    DepthGradient = depth < 6.5f
                        ? environment.SampleDepthGradient(segmentPosition) : Vector2.zero,
                    BreakingIntensity = 0f,
                    FoamEnergy = 0f,
                    State = enteredWorld ? WaveState.Traveling : WaveState.PendingEntry,
                    Active = enteredWorld
                };
            }
            return segments;
        }

        private int CountWavesInSystem(IReadOnlyList<WaveData> waves, int systemId)
        {
            int count = 0;
            for (int i = 0; i < waves.Count; i++)
                if (waves[i].SwellSystemId == systemId) count++;
            return count;
        }

        private int FindSystemIndex(int systemId)
        {
            for (int i = 0; i < swellSystems.Count; i++)
                if (swellSystems[i].Id == systemId) return i;
            return -1;
        }

        private int FindSourceIndex(WaveSourceKind kind)
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].Kind == kind) return i;
            return -1;
        }

        private int FindSourceIndex(int sourceId)
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].Id == sourceId) return i;
            return -1;
        }

        private float TotalSourceWeight()
        {
            float total = 0f;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].Enabled) total += Mathf.Max(0f, sources[i].SelectionWeight);
            return Mathf.Max(0.001f, total);
        }

        private int NextEnabledSourceIndex(int cursor)
        {
            int enabledCount = 0;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].Enabled) enabledCount++;
            if (enabledCount == 0) return 0;
            int target = Mathf.Abs(cursor) % enabledCount;
            for (int i = 0; i < sources.Count; i++)
            {
                if (!sources[i].Enabled) continue;
                if (target-- == 0) return i;
            }
            return 0;
        }

        private ulong SecondsToTicks(float seconds)
            => (ulong)Mathf.Max(1, Mathf.CeilToInt(seconds / config.FixedDeltaTime));

        private float DistanceToWorldExit(Vector2 origin, Vector2 direction)
        {
            Vector2 half = config.WorldHalfExtents;
            float distance = float.MaxValue;
            if (direction.x > 0.0001f) distance = Mathf.Min(distance, (half.x - origin.x) / direction.x);
            else if (direction.x < -0.0001f) distance = Mathf.Min(distance, (-half.x - origin.x) / direction.x);
            if (direction.y > 0.0001f) distance = Mathf.Min(distance, (half.y - origin.y) / direction.y);
            else if (direction.y < -0.0001f) distance = Mathf.Min(distance, (-half.y - origin.y) / direction.y);
            return distance == float.MaxValue ? 0f : Mathf.Max(0f, distance);
        }

        private void CalculateDirectionalBoundaryEntry(Vector2 direction,
            out Vector2 entryPoint, out Vector2 emissionCenter, out float crestLength)
        {
            direction = direction.sqrMagnitude < 0.001f ? Vector2.right : direction.normalized;
            Vector2 axis = new Vector2(-direction.y, direction.x);
            Vector2 half = config.WorldHalfExtents;
            Vector2[] corners =
            {
                new Vector2(-half.x, -half.y), new Vector2(-half.x, half.y),
                new Vector2(half.x, -half.y), new Vector2(half.x, half.y)
            };
            float minimumPhase = float.MaxValue;
            float minimumLateral = float.MaxValue;
            float maximumLateral = float.MinValue;
            entryPoint = corners[0];
            for (int i = 0; i < corners.Length; i++)
            {
                float phase = Vector2.Dot(corners[i], direction);
                float lateral = Vector2.Dot(corners[i], axis);
                if (phase < minimumPhase)
                {
                    minimumPhase = phase;
                    entryPoint = corners[i];
                }
                minimumLateral = Mathf.Min(minimumLateral, lateral);
                maximumLateral = Mathf.Max(maximumLateral, lateral);
            }

            // Avoid measure-zero corner rays that could step across a corner without ever
            // producing an in-bounds sample. Every retained section trajectory intersects
            // real map area while the crest still begins at the upstream phase plane.
            float lateralInset = Mathf.Min(config.WaveSegmentTargetSpacing * 0.35f,
                (maximumLateral - minimumLateral) * 0.01f);
            minimumLateral += lateralInset;
            maximumLateral -= lateralInset;
            float centerLateral = (minimumLateral + maximumLateral) * 0.5f;
            emissionCenter = direction * minimumPhase + axis * centerLateral;
            crestLength = Mathf.Max(config.WaveSegmentTargetSpacing,
                maximumLateral - minimumLateral);
        }

        private bool InsideWorld(Vector2 position)
        {
            Vector2 half = config.WorldHalfExtents;
            return Mathf.Abs(position.x) <= half.x && Mathf.Abs(position.y) <= half.y;
        }

        private static WaveSourceData CreateSource(int id, WaveSourceKind kind,
            Vector2 start, Vector2 end, Vector2 direction, float spread, float weight,
            float minimumPeriodSeconds, float maximumPeriodSeconds, bool enabled)
        {
            return new WaveSourceData
            {
                Id = id,
                Kind = kind,
                EntryMode = kind == WaveSourceKind.WesternSwell
                    ? WaveSourceEntryMode.BoundarySegment
                    : WaveSourceEntryMode.DirectionalCorner,
                Enabled = enabled,
                SegmentStart = start,
                SegmentEnd = end,
                Direction = direction.normalized,
                DirectionSpreadDegrees = spread,
                SelectionWeight = weight,
                MinimumEnergy = kind == WaveSourceKind.WesternSwell ? 0.82f : 0.68f,
                MaximumEnergy = kind == WaveSourceKind.WesternSwell ? 2.1f : 1.85f,
                MinimumSpacing = 0f,
                MaximumSpacing = 0f,
                MinimumPackets = 1,
                MaximumPackets = 1,
                MinimumCalmSeconds = minimumPeriodSeconds,
                MaximumCalmSeconds = maximumPeriodSeconds,
                NextEmissionTick = 0
            };
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        private static Vector2 DirectionFromDegrees(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }
    }
}

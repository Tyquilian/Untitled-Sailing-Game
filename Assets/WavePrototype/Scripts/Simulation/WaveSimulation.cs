using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Deterministic coordinator and sole owner of authoritative entity state.
    /// Systems observe and decide into temporary buffers; this class applies all persistent changes.
    /// </summary>
    public sealed class WaveSimulation
    {
        private readonly IOceanEnvironmentFactory environmentFactory;
        private readonly List<WaveData> waves = new List<WaveData>(1100);
        private readonly List<BoatData> boats = new List<BoatData>(8);
        private readonly List<FloatingObjectData> floatingObjects = new List<FloatingObjectData>(64);
        private readonly List<SimulationEvent> events = new List<SimulationEvent>(256);
        private readonly List<SimulationEvent> pendingEvents = new List<SimulationEvent>(256);
        private readonly List<WaveDecision> waveDecisions = new List<WaveDecision>(1100);
        private readonly List<BoatDecision> boatDecisions = new List<BoatDecision>(8);
        private readonly List<FloatingObjectDecision> floatingObjectDecisions =
            new List<FloatingObjectDecision>(64);
        private readonly SimulationConfig runtimeConfig;
        private readonly BoatInputBuffer inputBuffer;
        private readonly ReadOnlyCollection<WaveData> waveView;
        private readonly ReadOnlyCollection<BoatData> boatView;
        private readonly ReadOnlyCollection<FloatingObjectData> floatingObjectView;
        private readonly ReadOnlyCollection<SimulationEvent> eventView;
        private WaveSourceSystem waveSourceSystem;
        private CrossSeaEventSystem crossSeaEventSystem;
        private WavePropagationSystem wavePropagationSystem;
        private WaveSectionSpatialIndex waveSectionSpatialIndex;
        private WaveBoatInteractionSystem waveBoatInteractionSystem;
        private BoatMotionSystem boatMotionSystem;
        private TargetMarkerSystem targetMarkerSystem;
        private FloatingObjectSystem floatingObjectSystem;
        private int nextBoatId;

        public SimulationConfigSnapshot Config { get; }
        public IOceanEnvironment Environment { get; private set; }
        public IReadOnlyList<WaveData> Waves => waveView;
        public IReadOnlyList<BoatData> Boats => boatView;
        public IReadOnlyList<FloatingObjectData> FloatingObjects => floatingObjectView;
        public IReadOnlyList<SimulationEvent> Events => eventView;
        public IReadOnlyList<WaveSourceData> WaveSources => waveSourceSystem.Sources;
        public IReadOnlyList<SwellSystemData> SwellSystems => waveSourceSystem.SwellSystems;
        public CrossSeaEventData CrossSeaEvent => crossSeaEventSystem.Data;
        public IReadOnlyList<BoatControlCommand> RecordedControls => inputBuffer.AppliedCommands;
        public TargetMarkerData Target => targetMarkerSystem.Data;
        public int CollectedSalvageCount => floatingObjectSystem.CollectedCount;
        public float CollectedSalvageValue => floatingObjectSystem.CollectedValue;
        public int Seed { get; private set; }
        public ulong Tick { get; private set; }
        public int PlayerBoatId { get; private set; }
        public int InitialWaveTarget { get; private set; }
        public float SimulatedTime => Tick * Config.FixedDeltaTime;
        public SpatialBroadphaseSnapshot SpatialBroadphase => new SpatialBroadphaseSnapshot(
            runtimeConfig.EnableSpatialBroadphase,
            waveSectionSpatialIndex == null ? 0 : waveSectionSpatialIndex.IndexedSectionCount,
            waveSectionSpatialIndex == null ? 0 : waveSectionSpatialIndex.OccupiedCellCount,
            waveSectionSpatialIndex == null ? 0 : waveSectionSpatialIndex.QueryCount,
            waveSectionSpatialIndex == null ? 0 : waveSectionSpatialIndex.CandidateReferenceCount,
            waveBoatInteractionSystem == null ? 0 : waveBoatInteractionSystem.ExactSegmentChecks,
            waveBoatInteractionSystem == null ? 0 : waveBoatInteractionSystem.PotentialSegmentChecks,
            floatingObjectSystem == null ? 0 : floatingObjectSystem.WaveExactSegmentChecks,
            floatingObjectSystem == null ? 0 : floatingObjectSystem.WavePotentialSegmentChecks,
            boatMotionSystem == null ? 0 : boatMotionSystem.RockQueryCount,
            boatMotionSystem == null ? 0 : boatMotionSystem.RockCandidateChecks,
            boatMotionSystem == null ? 0 : boatMotionSystem.RockPotentialChecks);
        public int ActiveWaveSourceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < WaveSources.Count; i++)
                    if (WaveSources[i].Enabled) count++;
                return count;
            }
        }
        public BoatControl PlayerControl => inputBuffer.GetControl(PlayerBoatId);
        public Vector2 WindVelocity => Config.WindDirection.normalized * Config.WindSpeed;
        public int TotalWaveSegmentCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < waves.Count; i++)
                    count += waves[i].MutableSegments == null ? 0 : waves[i].MutableSegments.Length;
                return count;
            }
        }
        public int ActiveWaveSegmentCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < waves.Count; i++)
                {
                    WaveSegmentData[] segments = waves[i].MutableSegments;
                    if (segments == null) continue;
                    for (int segment = 0; segment < segments.Length; segment++)
                        if (segments[segment].Active) count++;
                }
                return count;
            }
        }
        public int PendingWaveSegmentCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < waves.Count; i++)
                {
                    WaveSegmentData[] segments = waves[i].MutableSegments;
                    if (segments == null) continue;
                    for (int segment = 0; segment < segments.Length; segment++)
                        if (segments[segment].State == WaveState.PendingEntry) count++;
                }
                return count;
            }
        }

        public WaveSimulation(int seed, SimulationConfig config = null,
            IOceanEnvironmentFactory environmentFactory = null)
        {
            runtimeConfig = (config ?? new SimulationConfig()).Clone();
            Config = new SimulationConfigSnapshot(runtimeConfig);
            inputBuffer = new BoatInputBuffer(runtimeConfig.RecordBoatControlHistory,
                runtimeConfig.MaximumRecordedBoatControls,
                runtimeConfig.PendingInputCompactionThreshold);
            waveView = waves.AsReadOnly();
            boatView = boats.AsReadOnly();
            floatingObjectView = floatingObjects.AsReadOnly();
            eventView = events.AsReadOnly();
            this.environmentFactory = environmentFactory ?? new OceanEnvironmentFactory();
            Reset(seed);
        }

        public void Reset(int seed)
        {
            Seed = seed;
            Tick = 0;
            nextBoatId = 1;
            PlayerBoatId = 0;
            waves.Clear();
            boats.Clear();
            floatingObjects.Clear();
            events.Clear();
            pendingEvents.Clear();
            waveDecisions.Clear();
            boatDecisions.Clear();
            floatingObjectDecisions.Clear();
            inputBuffer.Reset();

            Environment = environmentFactory.Create(runtimeConfig.WorldHalfExtents, seed);
            waveSourceSystem = new WaveSourceSystem(runtimeConfig, Environment);
            wavePropagationSystem = new WavePropagationSystem(runtimeConfig, Environment, waveSourceSystem);
            waveSectionSpatialIndex = new WaveSectionSpatialIndex(
                runtimeConfig.SpatialWaveCellSize, runtimeConfig.WorldHalfExtents);
            waveBoatInteractionSystem = new WaveBoatInteractionSystem(runtimeConfig);
            boatMotionSystem = new BoatMotionSystem(runtimeConfig, Environment);
            waveSourceSystem.Reset(seed);
            crossSeaEventSystem = new CrossSeaEventSystem(runtimeConfig, waveSourceSystem);
            crossSeaEventSystem.Reset();
            InitialWaveTarget = waveSourceSystem.ResolveInitialWaveCount(
                runtimeConfig.TargetWaveCount);
            PlayerBoatId = PrototypeScenario.AddInitialBoats(this);
            targetMarkerSystem = new TargetMarkerSystem(runtimeConfig, Environment);
            targetMarkerSystem.Reset(seed, boats[FindBoatIndex(PlayerBoatId)].Position);
            floatingObjectSystem = new FloatingObjectSystem(runtimeConfig, Environment);
            floatingObjectSystem.Reset(seed, floatingObjects,
                boats[FindBoatIndex(PlayerBoatId)].Position, InitialWaveTarget > 0);
            waveSourceSystem.PopulateInitialWorld(waves, InitialWaveTarget);
        }

        public bool QueueBoatControl(BoatControlCommand command)
        {
            if (FindBoatIndex(command.BoatId) < 0) return false;
            return inputBuffer.Queue(command, Tick);
        }

        public void SetPlayerControl(float throttle, float steering)
            => QueueBoatControl(new BoatControlCommand(Tick, PlayerBoatId, new BoatControl(throttle, steering)));

        public int AddBoat(Vector2 position, float heading)
            => AddBoat(position, heading, VesselProfileId.ArcadeSkiff);

        public int AddBoat(Vector2 position, float heading, VesselProfileId profileId)
        {
            VesselProfileDefinition profile = runtimeConfig.GetVesselProfile(profileId);
            if (boatMotionSystem.HullIntersectsLand(position, heading, profile) ||
                Environment.FindRock(position, profile.CollisionRadius) >= 0)
                position = boatMotionSystem.FindNearbyWater(position, heading, profile);
            int id = nextBoatId++;
            boats.Add(new BoatData
            {
                Id = id,
                Profile = profile.Id,
                Position = position,
                Velocity = Vector2.zero,
                Heading = heading,
                Health = 100f,
                Mass = profile.Mass
            });
            return id;
        }

        public bool SetBoatProfile(int boatId, VesselProfileId profileId)
        {
            int index = FindBoatIndex(boatId);
            if (index < 0) return false;
            VesselProfileDefinition profile = runtimeConfig.GetVesselProfile(profileId);
            BoatData boat = boats[index];
            boat.Profile = profile.Id;
            boat.Mass = profile.Mass;
            if (boatMotionSystem.HullIntersectsLand(boat.Position, boat.Heading, profile) ||
                Environment.FindRock(boat.Position, profile.CollisionRadius) >= 0)
            {
                boat.Position = boatMotionSystem.FindNearbyWater(boat.Position, boat.Heading, profile);
                boat.Velocity = Vector2.zero;
            }
            boats[index] = boat;
            return true;
        }

        public bool ConfigureBoatForValidation(int boatId, Vector2 position, Vector2 velocity, float heading)
        {
            int index = FindBoatIndex(boatId);
            if (index < 0) return false;
            BoatData boat = boats[index];
            boat.Position = position;
            boat.Velocity = velocity;
            boat.Heading = heading;
            boats[index] = boat;
            return true;
        }

        public void SpawnWave(Vector2 position, Vector2 direction, float energy = 1f)
            => waveSourceSystem.SpawnManual(waves, position, direction, energy);

        public bool SpawnSwellFront(Vector2 position, float energy = 1f)
            => waveSourceSystem.SpawnSwellFront(waves, position, energy);

        public bool TriggerCrossSeaEvent() => crossSeaEventSystem.Trigger(Tick);

        public bool RequestCrossSeaDeparture() => crossSeaEventSystem.RequestDeparture(Tick);

        public void SpawnWaveForValidation(Vector2 position, Vector2 direction,
            float energy, float packetLength, float crestLength)
            => waveSourceSystem.SpawnManualForValidation(waves, position, direction,
                energy, packetLength, crestLength);

        public int SpawnFloatingObject(FloatingObjectKind kind, Vector2 position)
            => floatingObjectSystem.Spawn(floatingObjects, kind, position);

        public bool RelocateTarget()
        {
            int playerIndex = FindBoatIndex(PlayerBoatId);
            return playerIndex >= 0 && targetMarkerSystem.Relocate(boats[playerIndex].Position);
        }

        public void SetTargetEnabled(bool enabled) => targetMarkerSystem.SetEnabled(enabled);

        public void SetTargetVisitRadius(float radius) => targetMarkerSystem.SetVisitRadius(radius);

        public void ResetTargetVisitCount() => targetMarkerSystem.ResetVisitCount();

        public bool IsSafeTargetPosition(Vector2 position) => targetMarkerSystem.IsSafePosition(position);

        public void Step()
        {
            events.Clear();
            pendingEvents.Clear();
            inputBuffer.BeginTick(Tick);
            wavePropagationSystem.Decide(waves, waveDecisions, pendingEvents, Tick);
            if (runtimeConfig.EnableSpatialBroadphase)
                waveSectionSpatialIndex.Build(waves, waveDecisions, runtimeConfig);
            else
                waveSectionSpatialIndex.ClearForDisabledMode();
            waveBoatInteractionSystem.Accumulate(waves, waveDecisions, boats, boatDecisions,
                pendingEvents, waveSectionSpatialIndex);
            floatingObjectSystem.Decide(floatingObjects, waves, waveDecisions, boats,
                boatDecisions, floatingObjectDecisions, pendingEvents,
                waveSectionSpatialIndex);
            boatMotionSystem.Decide(boats, boatDecisions, inputBuffer);
            Apply();
            Tick++;
        }

        private void Apply()
        {
            for (int i = boats.Count - 1; i >= 0; i--)
            {
                BoatData boat = boats[i];
                BoatDecision decision = boatDecisions[i];
                boat.Position = decision.Position;
                boat.Velocity = decision.Velocity;
                boat.Heading = decision.Heading;
                if (decision.Damage > 0f)
                {
                    boat.Health = Mathf.Max(0f, boat.Health - decision.Damage);
                    pendingEvents.Add(new SimulationEvent(SimulationEventType.BoatDamaged,
                        0, boat.Id, boat.Position, decision.Damage));
                }
                if (decision.Collision == SimulationEventType.BoatHitRock ||
                    decision.Collision == SimulationEventType.BoatGrounded)
                {
                    pendingEvents.Add(new SimulationEvent(decision.Collision,
                        0, boat.Id, boat.Position, decision.Damage));
                }
                boats[i] = boat;
            }

            targetMarkerSystem.Evaluate(boats, PlayerBoatId, pendingEvents);
            floatingObjectSystem.Apply(floatingObjects, floatingObjectDecisions);

            for (int i = waves.Count - 1; i >= 0; i--)
            {
                WaveDecision decision = waveDecisions[i];
                if (decision.Expired)
                {
                    WaveData expired = waves[i];
                    pendingEvents.Add(new SimulationEvent(SimulationEventType.WaveExpired,
                        expired.Id, 0, expired.Position, expired.Energy));
                    waves.RemoveAt(i);
                    waveDecisions.RemoveAt(i);
                    continue;
                }
                WaveData wave = waves[i];
                WaveSegmentData[] segments = wave.MutableSegments;
                WaveSegmentDecision[] segmentDecisions = decision.Segments;
                if (segments != null && segmentDecisions != null)
                {
                    int count = Mathf.Min(segments.Length, segmentDecisions.Length);
                    for (int segmentIndex = 0; segmentIndex < count; segmentIndex++)
                    {
                        WaveSegmentData segment = segments[segmentIndex];
                        WaveSegmentDecision segmentDecision = segmentDecisions[segmentIndex];
                        segment.PreviousPosition = segment.Position;
                        segment.Position = segmentDecision.Position;
                        segment.TravelDirection = segmentDecision.Direction;
                        segment.Speed = segmentDecision.Speed;
                        segment.Energy = segmentDecision.Energy;
                        segment.SampledDepth = segmentDecision.SampledDepth;
                        segment.DepthGradient = segmentDecision.DepthGradient;
                        segment.BreakingIntensity = segmentDecision.BreakingIntensity;
                        segment.FoamEnergy = segmentDecision.FoamEnergy;
                        segment.State = segmentDecision.State;
                        segment.Active = segmentDecision.Active;
                        segments[segmentIndex] = segment;
                    }
                }
                wave.Position = decision.Position;
                wave.TravelDirection = decision.Direction;
                wave.Speed = decision.Speed;
                wave.Energy = decision.Energy;
                wave.State = decision.State;
                waves[i] = wave;
            }

            crossSeaEventSystem.AdvanceBeforeEmission(Tick);
            waveSourceSystem.MaintainPopulation(waves, InitialWaveTarget, Tick,
                crossSeaEventSystem.EmittingSourceId, crossSeaEventSystem.EmissionEnergyScale);
            crossSeaEventSystem.SynchronizeAfterEmission(Tick);
            events.AddRange(pendingEvents);
        }

        public float GetWindEfficiency(float heading) => boatMotionSystem.GetWindEfficiency(heading);

        public Vector2 SampleAmbientWaveField(Vector2 position, float radius = 12f)
        {
            Vector2 force = Vector2.zero;
            float radiusSquared = radius * radius;
            for (int i = 0; i < waves.Count; i++)
            {
                WaveData wave = waves[i];
                WaveSegmentData[] segments = wave.MutableSegments;
                int nearest = -1;
                float nearestSquared = radiusSquared;
                if (segments != null)
                {
                    for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                    {
                        if (!segments[segmentIndex].Active) continue;
                        float distanceSquared = (position - segments[segmentIndex].Position).sqrMagnitude;
                        if (distanceSquared >= nearestSquared) continue;
                        nearestSquared = distanceSquared;
                        nearest = segmentIndex;
                    }
                }
                if (nearest < 0) continue;
                WaveSegmentData local = segments[nearest];
                float proximity = 1f - Mathf.Sqrt(nearestSquared) / radius;
                force += local.TravelDirection * new WaveDerived(local.Energy,
                    local.SampledDepth, wave.PacketLength).Force * proximity;
            }
            return force;
        }

        // Source-compatible alias for Batch 1-4 debug consumers.
        public Vector2 SampleWaveForce(Vector2 position, float radius = 12f)
            => SampleAmbientWaveField(position, radius);

        public WaveDensitySample SampleWaveDensity(Vector2 position, float radius)
        {
            float segmentReach = Mathf.Max(0f, Config.WaveSegmentTargetSpacing * 0.55f);
            float radiusSquared = (radius + segmentReach) * (radius + segmentReach);
            int localCount = 0;
            for (int i = 0; i < waves.Count; i++)
            {
                WaveSegmentData[] segments = waves[i].MutableSegments;
                bool visible = false;
                if (segments != null)
                {
                    for (int segment = 0; segment < segments.Length; segment++)
                    {
                        if (!segments[segment].Active ||
                            (segments[segment].Position - position).sqrMagnitude > radiusSquared)
                            continue;
                        visible = true;
                        break;
                    }
                }
                else visible = (waves[i].Position - position).sqrMagnitude <= radiusSquared;
                if (visible) localCount++;
            }
            return new WaveDensitySample(waves.Count, localCount, radius, Config.DesiredVisibleWaveCount);
        }

        public static string GetWaveSourceLabel(WaveSourceKind kind)
        {
            switch (kind)
            {
                case WaveSourceKind.WesternSwell: return "WESTERN SWELL";
                case WaveSourceKind.NorthernCrossSea: return "NORTH CROSS-SEA";
                case WaveSourceKind.SouthernCrossSea: return "SOUTH CROSS-SEA";
                default: return "MANUAL";
            }
        }

        public string GetWaveSourceLabel(int sourceId)
        {
            if (sourceId == 0) return "MANUAL";
            for (int i = 0; i < WaveSources.Count; i++)
                if (WaveSources[i].Id == sourceId) return GetWaveSourceLabel(WaveSources[i].Kind);
            return "UNKNOWN";
        }

        private int FindBoatIndex(int boatId)
        {
            for (int i = 0; i < boats.Count; i++)
                if (boats[i].Id == boatId) return i;
            return -1;
        }

        public ulong CalculateStateHash()
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                Mix(ref hash, (uint)Seed);
                Mix64(ref hash, Tick);
                Mix(ref hash, (uint)PlayerBoatId);
                Mix(ref hash, (uint)nextBoatId);
                Mix(ref hash, (uint)waveSourceSystem.NextWaveId);
                Mix(ref hash, (uint)waveSourceSystem.NextSwellSystemId);
                Mix(ref hash, waveSourceSystem.RandomState);
                Mix(ref hash, (uint)crossSeaEventSystem.NextEventId);
                Mix(ref hash, targetMarkerSystem.RandomState);
                Mix(ref hash, (uint)floatingObjectSystem.NextObjectId);
                Mix(ref hash, floatingObjectSystem.RandomState);
                Mix(ref hash, (uint)floatingObjectSystem.CollectedCount);
                MixFloat(ref hash, floatingObjectSystem.CollectedValue);
                MixConfig(ref hash);

                CrossSeaEventData crossSea = crossSeaEventSystem.Data;
                Mix(ref hash, (uint)crossSea.EventId);
                Mix(ref hash, (uint)crossSea.TriggerCount);
                Mix(ref hash, (uint)crossSea.Phase);
                Mix(ref hash, (uint)crossSea.SourceKind);
                Mix(ref hash, (uint)crossSea.SourceId);
                Mix(ref hash, (uint)crossSea.SwellSystemId);
                MixFloat(ref hash, crossSea.Intensity);
                MixFloat(ref hash, crossSea.DepartureStartIntensity);
                Mix(ref hash, (uint)crossSea.InitialSourcePacketCount);
                Mix(ref hash, (uint)crossSea.EmittedPacketCount);
                Mix(ref hash, (uint)crossSea.ActivePacketCount);
                Mix64(ref hash, crossSea.StartedTick);
                Mix64(ref hash, crossSea.PhaseStartedTick);
                Mix64(ref hash, crossSea.EmissionsStoppedTick);
                Mix64(ref hash, crossSea.NextAutomaticStartTick);

                TargetMarkerData target = targetMarkerSystem.Data;
                MixVector(ref hash, target.Position);
                MixFloat(ref hash, target.VisitRadius);
                Mix(ref hash, (uint)target.VisitCount);
                Mix(ref hash, (uint)target.RelocationCount);
                Mix(ref hash, target.Enabled ? 1u : 0u);

                for (int i = 0; i < WaveSources.Count; i++)
                {
                    WaveSourceData source = WaveSources[i];
                    Mix(ref hash, (uint)source.Id);
                    Mix(ref hash, (uint)source.Kind);
                    Mix(ref hash, (uint)source.EntryMode);
                    Mix(ref hash, source.Enabled ? 1u : 0u);
                    MixVector(ref hash, source.SegmentStart);
                    MixVector(ref hash, source.SegmentEnd);
                    MixVector(ref hash, source.Direction);
                    MixFloat(ref hash, source.DirectionSpreadDegrees);
                    MixFloat(ref hash, source.SelectionWeight);
                    MixFloat(ref hash, source.MinimumEnergy);
                    MixFloat(ref hash, source.MaximumEnergy);
                    MixFloat(ref hash, source.MinimumSpacing);
                    MixFloat(ref hash, source.MaximumSpacing);
                    Mix(ref hash, (uint)source.MinimumPackets);
                    Mix(ref hash, (uint)source.MaximumPackets);
                    Mix(ref hash, (uint)source.SpawnedTrains);
                    Mix(ref hash, (uint)source.SpawnedPackets);
                    Mix(ref hash, (uint)source.SpawnedSystems);
                    MixFloat(ref hash, source.MinimumCalmSeconds);
                    MixFloat(ref hash, source.MaximumCalmSeconds);
                    Mix64(ref hash, source.NextEmissionTick);
                }

                for (int i = 0; i < SwellSystems.Count; i++)
                {
                    SwellSystemData system = SwellSystems[i];
                    Mix(ref hash, (uint)system.Id);
                    Mix(ref hash, (uint)system.SourceId);
                    MixVector(ref hash, system.Direction);
                    MixVector(ref hash, system.BoundaryEntryPoint);
                    MixVector(ref hash, system.EmissionCenter);
                    MixFloat(ref hash, system.BaseEnergy);
                    MixFloat(ref hash, system.PacketSpacing);
                    MixFloat(ref hash, system.MeanPacketLength);
                    MixFloat(ref hash, system.MeanCrestLength);
                    MixFloat(ref hash, system.CalmGapSeconds);
                    Mix(ref hash, (uint)system.InitialPacketCount);
                    Mix(ref hash, (uint)system.EmittedPacketCount);
                    Mix(ref hash, (uint)system.ActivePacketCount);
                    Mix64(ref hash, system.BornTick);
                    Mix(ref hash, system.UsesDirectionalBoundaryEntry ? 1u : 0u);
                }

                for (int i = 0; i < waves.Count; i++)
                {
                    WaveData wave = waves[i];
                    Mix(ref hash, (uint)wave.Id);
                    Mix(ref hash, (uint)wave.SourceId);
                    Mix(ref hash, (uint)wave.SwellSystemId);
                    MixVector(ref hash, wave.Position);
                    MixVector(ref hash, wave.TravelDirection);
                    MixFloat(ref hash, wave.Energy);
                    MixFloat(ref hash, wave.Speed);
                    MixFloat(ref hash, wave.PacketLength);
                    MixFloat(ref hash, wave.CrestLength);
                    Mix(ref hash, (uint)wave.State);
                    WaveSegmentData[] segments = wave.MutableSegments;
                    Mix(ref hash, (uint)(segments == null ? 0 : segments.Length));
                    if (segments != null)
                    {
                        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                        {
                            WaveSegmentData segment = segments[segmentIndex];
                            Mix(ref hash, (uint)segment.Index);
                            MixVector(ref hash, segment.PreviousPosition);
                            MixVector(ref hash, segment.Position);
                            MixVector(ref hash, segment.TravelDirection);
                            MixFloat(ref hash, segment.Energy);
                            MixFloat(ref hash, segment.Speed);
                            MixFloat(ref hash, segment.SampledDepth);
                            MixVector(ref hash, segment.DepthGradient);
                            MixFloat(ref hash, segment.BreakingIntensity);
                            MixFloat(ref hash, segment.FoamEnergy);
                            Mix(ref hash, (uint)segment.State);
                            Mix(ref hash, segment.Active ? 1u : 0u);
                        }
                    }
                }

                for (int i = 0; i < boats.Count; i++)
                {
                    BoatData boat = boats[i];
                    Mix(ref hash, (uint)boat.Id);
                    Mix(ref hash, (uint)boat.Profile);
                    MixVector(ref hash, boat.Position);
                    MixVector(ref hash, boat.Velocity);
                    MixFloat(ref hash, boat.Heading);
                    MixFloat(ref hash, boat.Health);
                    MixFloat(ref hash, boat.Mass);
                    BoatControl activeControl = inputBuffer.GetControl(boat.Id);
                    MixFloat(ref hash, activeControl.Throttle);
                    MixFloat(ref hash, activeControl.Steering);
                }

                for (int i = 0; i < floatingObjects.Count; i++)
                {
                    FloatingObjectData item = floatingObjects[i];
                    Mix(ref hash, (uint)item.Id);
                    Mix(ref hash, (uint)item.Kind);
                    MixVector(ref hash, item.PreviousPosition);
                    MixVector(ref hash, item.Position);
                    MixVector(ref hash, item.Velocity);
                    MixFloat(ref hash, item.Radius);
                    MixFloat(ref hash, item.Value);
                    Mix(ref hash, unchecked((uint)item.LastBreakingWaveId));
                    Mix(ref hash, item.Active ? 1u : 0u);
                }

                IReadOnlyList<BoatControlCommand> pendingCommands = inputBuffer.PendingCommands;
                Mix(ref hash, (uint)(pendingCommands.Count - inputBuffer.PendingCursor));
                for (int i = inputBuffer.PendingCursor; i < pendingCommands.Count; i++)
                {
                    BoatControlCommand command = pendingCommands[i];
                    Mix64(ref hash, command.Tick);
                    Mix(ref hash, (uint)command.BoatId);
                    MixFloat(ref hash, command.Control.Throttle);
                    MixFloat(ref hash, command.Control.Steering);
                }

                for (int i = 0; i < events.Count; i++)
                {
                    SimulationEvent simulationEvent = events[i];
                    Mix(ref hash, (uint)simulationEvent.Type);
                    Mix(ref hash, (uint)simulationEvent.WaveId);
                    Mix(ref hash, (uint)simulationEvent.BoatId);
                    MixVector(ref hash, simulationEvent.Position);
                    MixFloat(ref hash, simulationEvent.Magnitude);
                    Mix(ref hash, unchecked((uint)simulationEvent.SegmentIndex));
                    Mix(ref hash, unchecked((uint)simulationEvent.ObjectId));
                }
                return hash;
            }
        }

        private void MixConfig(ref ulong hash)
        {
            // Spatial broadphase settings are deliberately omitted: they are execution
            // policy, not authoritative game state. Broadphase and brute force must hash
            // identically when their exact ordered decisions agree.
            MixFloat(ref hash, Config.FixedDeltaTime);
            MixVector(ref hash, Config.WorldHalfExtents);
            MixFloat(ref hash, Config.BaseWaveSpeed);
            MixFloat(ref hash, Config.EnergyDecayPerSecond);
            MixFloat(ref hash, Config.BreakingMinimumEnergyLossPerSecond);
            MixFloat(ref hash, Config.BreakingEnergyLossPerSecond);
            MixFloat(ref hash, Config.BreakingIntensityAttackPerSecond);
            MixFloat(ref hash, Config.BreakingIntensityRecoveryPerSecond);
            MixFloat(ref hash, Config.BreakingReleaseIntensity);
            MixFloat(ref hash, Config.BreakingEnergyToFoam);
            MixFloat(ref hash, Config.FoamEnergyLossPerSecond);
            MixFloat(ref hash, Config.MinimumFoamEnergy);
            MixFloat(ref hash, Config.SpentEnergyLossPerSecond);
            MixFloat(ref hash, Config.MinimumEnergy);
            MixFloat(ref hash, Config.BreakingSteepness);
            MixFloat(ref hash, Config.DepthLimitedBreakingRatio);
            MixFloat(ref hash, Config.BoatInteractionRadius);
            MixFloat(ref hash, Config.RockInteractionRadius);
            MixFloat(ref hash, Config.BoatLinearDrag);
            MixFloat(ref hash, Config.BoatLateralDrag);
            MixFloat(ref hash, Config.RockEnergyAbsorption);
            MixFloat(ref hash, Config.WaveBoatForceScale);
            MixFloat(ref hash, Config.BreakingImpactMultiplier);
            MixFloat(ref hash, Config.WaveYawScale);
            MixFloat(ref hash, Config.TravelingImpactMultiplier);
            MixFloat(ref hash, Config.TravelingLongitudinalScale);
            MixFloat(ref hash, Config.TravelingLongitudinalPadding);
            MixFloat(ref hash, Config.TravelingCarrySpeedFraction);
            MixFloat(ref hash, Config.TravelingYawMultiplier);
            MixFloat(ref hash, Config.WaveRefractionStrength);
            MixFloat(ref hash, Config.WaveShoalingDeceleration);
            MixFloat(ref hash, Config.WaveDeepRecovery);
            MixFloat(ref hash, Config.WaveSegmentTargetSpacing);
            Mix(ref hash, (uint)Config.WaveMaximumSegments);
            Mix(ref hash, (uint)Config.WaveEnvironmentSampleInterval);
            MixFloat(ref hash, Config.WaveSegmentDirectionCoherence);
            MixFloat(ref hash, Config.WaveSegmentPositionCoherence);
            MixFloat(ref hash, Config.WaveSegmentLinkBreakMultiplier);
            MixFloat(ref hash, Config.WaveMinimumActiveSegmentFraction);
            MixFloat(ref hash, Config.WindSpeed);
            MixVector(ref hash, Config.WindDirection);
            MixFloat(ref hash, Config.SailingForce);
            MixFloat(ref hash, Config.BoatCruiseSpeed);
            MixFloat(ref hash, Config.BoatSurfSpeedCap);
            MixFloat(ref hash, Config.BoatCruisePropulsionFadeRange);
            MixFloat(ref hash, Config.BoatSurfExcessDecay);
            MixFloat(ref hash, Config.BoatCollisionRadius);
            MixFloat(ref hash, Config.RockImpactRestitution);
            MixFloat(ref hash, Config.RockTangentialRetention);
            MixFloat(ref hash, Config.RockContactSkin);
            MixFloat(ref hash, Config.BoatTurnRate);
            Mix(ref hash, (uint)Config.TargetWaveCount);
            Mix(ref hash, (uint)Config.DesiredVisibleWaveCount);
            MixFloat(ref hash, Config.DefaultTargetVisitRadius);
            MixFloat(ref hash, Config.TargetSafeClearance);
            MixFloat(ref hash, Config.TargetMinimumRelocationDistance);
            Mix(ref hash, (uint)Config.InitialFloatingObjectCount);
            MixFloat(ref hash, Config.FloatingObjectWaveResponse);
            MixFloat(ref hash, Config.FloatingObjectDrag);
            MixFloat(ref hash, Config.CargoCollectionRadius);
            MixFloat(ref hash, Config.WreckageBoatForce);
            MixFloat(ref hash, Config.BreakingFloatingObjectImpulse);
            MixFloat(ref hash, Config.WreckageInertiaScale);
            MixFloat(ref hash, Config.FloatingObjectMaximumSpeed);
            Mix(ref hash, Config.RecordBoatControlHistory ? 1u : 0u);
            Mix(ref hash, unchecked((uint)Config.MaximumRecordedBoatControls));
            Mix(ref hash, unchecked((uint)Config.PendingInputCompactionThreshold));
            MixVesselProfile(ref hash, Config.ArcadeSkiffProfile);
            MixVesselProfile(ref hash, Config.HeavyCutterProfile);
            Mix(ref hash, (uint)Config.CrossSeaSourceKind);
            MixFloat(ref hash, Config.CrossSeaAutomaticStartSeconds);
            MixFloat(ref hash, Config.CrossSeaBuildSeconds);
            MixFloat(ref hash, Config.CrossSeaEstablishedSeconds);
            MixFloat(ref hash, Config.CrossSeaDepartureSeconds);
            MixFloat(ref hash, Config.CrossSeaMinimumEnergyScale);
            MixFloat(ref hash, Config.WaveFollowingThrustScale);
            MixFloat(ref hash, Config.WaveHeadOnDampingScale);
            MixFloat(ref hash, Config.BreakingBoatDamageThreshold);
            MixFloat(ref hash, Config.BreakingBoatDamageScale);
            MixFloat(ref hash, Config.BoatReverseBrakeScale);
            MixFloat(ref hash, Config.BoatReversePropulsionScale);
            MixFloat(ref hash, Config.BoatMinimumTurnAuthority);
            MixFloat(ref hash, Config.BoatFullTurnAuthoritySpeed);
            MixFloat(ref hash, Config.GroundingBaseDamage);
            MixFloat(ref hash, Config.GroundingSpeedDamageScale);
            MixFloat(ref hash, Config.GroundingBounce);
            MixFloat(ref hash, Config.RockBaseDamage);
            MixFloat(ref hash, Config.RockSpeedDamageScale);
        }

        private static void MixVesselProfile(ref ulong hash, VesselProfileDefinition profile)
        {
            Mix(ref hash, (uint)profile.Id);
            MixFloat(ref hash, profile.Mass);
            MixFloat(ref hash, profile.HullLength);
            MixFloat(ref hash, profile.HullBeam);
            MixFloat(ref hash, profile.CollisionRadius);
            Mix(ref hash, unchecked((uint)profile.HullSampleCount));
            MixFloat(ref hash, profile.PropulsionScale);
            MixFloat(ref hash, profile.TurnRateScale);
            MixFloat(ref hash, profile.CruiseSpeedScale);
            MixFloat(ref hash, profile.SurfSpeedScale);
            MixFloat(ref hash, profile.LinearDragScale);
            MixFloat(ref hash, profile.LateralDragScale);
            MixFloat(ref hash, profile.WaveForceScale);
            MixFloat(ref hash, profile.WaveYawScale);
            MixFloat(ref hash, profile.DamageTakenScale);
        }

        private static void MixVector(ref ulong hash, Vector2 value)
        {
            MixFloat(ref hash, value.x);
            MixFloat(ref hash, value.y);
        }

        private static void MixFloat(ref ulong hash, float value)
            => Mix(ref hash, unchecked((uint)BitConverter.SingleToInt32Bits(value)));

        private static void Mix64(ref ulong hash, ulong value)
        {
            Mix(ref hash, (uint)value);
            Mix(ref hash, (uint)(value >> 32));
        }

        private static void Mix(ref ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
    }
}

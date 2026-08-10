using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private static void ValidateInitialSwellSystems(WaveSimulation simulation)
        {
            int attributedPackets = 0;
            for (int sourceIndex = 0; sourceIndex < simulation.WaveSources.Count; sourceIndex++)
            {
                WaveSourceData source = simulation.WaveSources[sourceIndex];
                if (source.Enabled)
                {
                    Require(source.Kind == WaveSourceKind.WesternSwell,
                        $"Unexpected normal-ocean source {source.Kind} is enabled.");
                    Require(source.SpawnedSystems == 1 && source.SpawnedPackets > 0,
                        $"Enabled source {source.Id} did not populate its unified stream.");
                    Require(simulation.SwellSystems.Count > 0,
                        $"Enabled source {source.Id} has no phase system.");
                    ulong expectedFirstEmission = (ulong)Mathf.Max(1, Mathf.CeilToInt(
                        simulation.SwellSystems[0].CalmGapSeconds * 0.5f /
                        simulation.Config.FixedDeltaTime));
                    Require(source.NextEmissionTick == expectedFirstEmission,
                        $"Enabled source {source.Id} first phase is scheduled at {source.NextEmissionTick} instead of {expectedFirstEmission}.");
                }
                else
                {
                    Require(source.SpawnedSystems == 0 && source.SpawnedPackets == 0,
                        $"Dormant source {source.Id} populated unauthorized fronts.");
                    Require(source.NextEmissionTick == ulong.MaxValue,
                        $"Dormant source {source.Id} has an active emission schedule.");
                }
            }

            for (int systemIndex = 0; systemIndex < simulation.SwellSystems.Count; systemIndex++)
            {
                SwellSystemData system = simulation.SwellSystems[systemIndex];
                Require(system.SourceId == 1,
                    $"Unified swell stream {system.Id} belongs to source {system.SourceId} instead of the western generator.");
                Require(system.InitialPacketCount == simulation.Config.TargetWaveCount,
                    $"Unified stream {system.Id} begins with {system.InitialPacketCount}/{simulation.Config.TargetWaveCount} fronts.");
                Require(system.EmittedPacketCount == system.InitialPacketCount,
                    $"Initial stream {system.Id} reports {system.EmittedPacketCount}/{system.InitialPacketCount} emitted fronts.");
                Require(system.ActivePacketCount == system.InitialPacketCount,
                    $"Initial swell system {system.Id} reports {system.ActivePacketCount}/{system.InitialPacketCount} active packets.");
                Require(system.CalmGapSeconds >= 2.2f && system.CalmGapSeconds <= 2.8f,
                    $"Swell stream {system.Id} has invalid phase period {system.CalmGapSeconds:0.0}s.");

                int counted = 0;
                var projections = new List<float>(system.InitialPacketCount);
                for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
                {
                    WaveData wave = simulation.Waves[waveIndex];
                    if (wave.SwellSystemId != system.Id) continue;
                    float deviation = Vector2.Angle(system.Direction, wave.TravelDirection);
                    Require(deviation <= 0.2f,
                        $"Wave {wave.Id} diverges {deviation:0.00} degrees from swell system {system.Id}.");
                    Require(wave.CrestLength > simulation.Config.WorldHalfExtents.y * 2f,
                        $"Wave {wave.Id} spans only {wave.CrestLength:0.0} across a {simulation.Config.WorldHalfExtents.y * 2f:0}-unit map.");
                    projections.Add(Vector2.Dot(wave.Position, system.Direction));
                    counted++;
                }
                Require(counted == system.InitialPacketCount,
                    $"Swell system {system.Id} owns {counted}/{system.InitialPacketCount} initial packets.");
                projections.Sort();
                int periodicGaps = 0;
                for (int i = 1; i < projections.Count; i++)
                {
                    float gap = projections[i] - projections[i - 1];
                    if (gap >= system.PacketSpacing * 0.72f &&
                        gap <= system.PacketSpacing * 1.28f) periodicGaps++;
                }
                Require(periodicGaps >= Mathf.FloorToInt((projections.Count - 1) * 0.7f),
                    $"Unified stream has only {periodicGaps}/{projections.Count - 1} periodic phase gaps.");
                attributedPackets += counted;
            }
            Require(attributedPackets == simulation.Waves.Count,
                $"Swell systems account for {attributedPackets}/{simulation.Waves.Count} initial packets.");
            Require(simulation.SwellSystems.Count == simulation.ActiveWaveSourceCount,
                "Unified swell stream count does not match the active source count.");
        }

        private static void ValidateSegmentedWave(WaveData wave, SimulationConfigSnapshot config)
        {
            Require(wave.Segments.Length >= 5,
                $"Broad wave {wave.Id} has no useful crest segmentation.");
            Require(wave.Segments.Length <= config.WaveMaximumSegments,
                $"Wave {wave.Id} exceeds the configured segment ceiling.");
            Vector2 crestAxis = new Vector2(-wave.TravelDirection.y, wave.TravelDirection.x);
            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            for (int segmentIndex = 0; segmentIndex < wave.Segments.Length; segmentIndex++)
            {
                WaveSegmentData segment = wave.Segments[segmentIndex];
                    Require(segment.Index == segmentIndex,
                        $"Wave {wave.Id} segment order is not stable at {segmentIndex}.");
                    Require(segment.Active, $"Wave {wave.Id} segment {segmentIndex} begins inactive.");
                    Require(segment.BreakingIntensity == 0f && segment.FoamEnergy == 0f,
                        $"Wave {wave.Id} segment {segmentIndex} begins with stale breaker state.");
                float projection = Vector2.Dot(segment.Position - wave.Position, crestAxis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            Require(Mathf.Abs((maximum - minimum) - wave.CrestLength) < 0.05f,
                $"Wave {wave.Id} segment span {maximum - minimum:0.00} does not match crest {wave.CrestLength:0.00}.");
        }

        private static CruiseProbe RunCruiseProbe()
        {
            var simulation = new WaveSimulation(614, new SimulationConfig { TargetWaveCount = 0 });
            float peak = 0f;
            int collisions = 0;
            for (int step = 0; step < 120; step++)
            {
                simulation.SetPlayerControl(1f, 0f);
                simulation.Step();
                peak = Mathf.Max(peak, simulation.Boats[0].Velocity.magnitude);
                collisions += CountPlayerEvents(simulation, SimulationEventType.BoatHitRock);
                collisions += CountPlayerEvents(simulation, SimulationEventType.BoatGrounded);
            }
            return new CruiseProbe(peak, simulation.Boats[0].Velocity.magnitude, collisions);
        }

        private static RockSweepProbe RunSweptRockProbe()
        {
            SimulationConfig firstConfig = CreateRockProbeConfig(true);
            SimulationConfig secondConfig = CreateRockProbeConfig(false);
            var first = new WaveSimulation(1847, firstConfig);
            var second = new WaveSimulation(1847, secondConfig);

            Require(TryFindRockSweepSetup(first, out int rockIndex, out Vector2 direction,
                    out Vector2 escapeTangent, out Vector2 start, out float combinedRadius),
                "Could not find a deterministic swept-rock probe corridor in the generated environment.");

            RockData rock = first.Environment.Rocks[rockIndex];
            Require(second.Environment.Rocks.Count > rockIndex &&
                    second.Environment.Rocks[rockIndex].Position == rock.Position &&
                    Mathf.Abs(second.Environment.Rocks[rockIndex].Radius - rock.Radius) < 0.0001f,
                "Identical seeds did not generate the same swept-rock target.");

            SetProbeBoat(first, start, direction, firstConfig.BoatSurfSpeedCap);
            SetProbeBoat(second, start, direction, secondConfig.BoatSurfSpeedCap);
            first.Step();
            second.Step();

            bool deterministic = first.CalculateStateHash() == second.CalculateStateHash();
            int impactEvents = CountPlayerEvents(first, SimulationEventType.BoatHitRock);
            BoatData afterImpact = first.Boats[0];
            float projection = Vector2.Dot(afterImpact.Position - rock.Position, direction);
            bool tunneled = projection > combinedRadius + firstConfig.RockContactSkin + 0.08f;

            SetProbeBoat(first, afterImpact.Position, escapeTangent, firstConfig.BoatCruiseSpeed * 0.5f);
            SetProbeBoat(second, second.Boats[0].Position, escapeTangent, secondConfig.BoatCruiseSpeed * 0.5f);
            Vector2 escapeStart = first.Boats[0].Position;
            int escapeImpacts = 0;
            for (int step = 0; step < 8; step++)
            {
                first.Step();
                second.Step();
                escapeImpacts += CountPlayerEvents(first, SimulationEventType.BoatHitRock);
                deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            }

            return new RockSweepProbe(rockIndex, impactEvents, escapeImpacts, combinedRadius,
                projection, Vector2.Distance(escapeStart, first.Boats[0].Position), tunneled, deterministic);
        }

        private static SimulationConfig CreateRockProbeConfig(bool enableSpatialBroadphase)
        {
            return new SimulationConfig
            {
                FixedDeltaTime = 0.2f,
                TargetWaveCount = 0,
                BoatSurfSpeedCap = 36f,
                EnableSpatialBroadphase = enableSpatialBroadphase
            };
        }

        private static bool TryFindRockSweepSetup(WaveSimulation simulation, out int rockIndex,
            out Vector2 direction, out Vector2 escapeTangent, out Vector2 start, out float combinedRadius)
        {
            const int directionSteps = 24;
            const float approachGap = 0.35f;
            const float escapeLength = 8f;
            float resolvedSpeed = simulation.Config.BoatSurfSpeedCap *
                                  Mathf.Exp(-simulation.Config.BoatLinearDrag * simulation.Config.FixedDeltaTime);
            if (resolvedSpeed > simulation.Config.BoatCruiseSpeed && simulation.Config.BoatSurfExcessDecay > 0f)
            {
                float excess = resolvedSpeed - simulation.Config.BoatCruiseSpeed;
                resolvedSpeed = simulation.Config.BoatCruiseSpeed + excess *
                                Mathf.Exp(-simulation.Config.BoatSurfExcessDecay * simulation.Config.FixedDeltaTime);
            }
            float estimatedTravel = Mathf.Min(resolvedSpeed, simulation.Config.BoatSurfSpeedCap) *
                                    simulation.Config.FixedDeltaTime;
            Vector2 half = simulation.Config.WorldHalfExtents;

            for (int candidateIndex = 0; candidateIndex < simulation.Environment.Rocks.Count; candidateIndex++)
            {
                RockData rock = simulation.Environment.Rocks[candidateIndex];
                float radius = rock.Radius + simulation.Config.BoatCollisionRadius;
                if (estimatedTravel <= radius * 2f + approachGap * 2f) continue;

                for (int directionIndex = 0; directionIndex < directionSteps; directionIndex++)
                {
                    float radians = directionIndex * Mathf.PI * 2f / directionSteps;
                    Vector2 candidateDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    Vector2 candidateStart = rock.Position - candidateDirection * (radius + approachGap);
                    Vector2 candidateEnd = candidateStart + candidateDirection * estimatedTravel;
                    if (!PointInsideWorld(candidateStart, half) || !PointInsideWorld(candidateEnd, half)) continue;
                    if (!SegmentIsWater(simulation, candidateStart, candidateEnd)) continue;
                    if (!ClearOfRocks(simulation, candidateStart, simulation.Config.BoatCollisionRadius, -1)) continue;
                    if (!ClearOfRocks(simulation, candidateEnd, simulation.Config.BoatCollisionRadius, -1)) continue;

                    Vector2 contact = rock.Position - candidateDirection * (radius + simulation.Config.RockContactSkin);
                    Vector2 tangent = new Vector2(-candidateDirection.y, candidateDirection.x);
                    if (!EscapeCorridorIsClear(simulation, contact, tangent, escapeLength, candidateIndex))
                    {
                        tangent = -tangent;
                        if (!EscapeCorridorIsClear(simulation, contact, tangent, escapeLength, candidateIndex)) continue;
                    }

                    rockIndex = candidateIndex;
                    direction = candidateDirection;
                    escapeTangent = tangent;
                    start = candidateStart;
                    combinedRadius = radius;
                    return true;
                }
            }

            rockIndex = -1;
            direction = Vector2.right;
            escapeTangent = Vector2.up;
            start = Vector2.zero;
            combinedRadius = 0f;
            return false;
        }

        private static bool EscapeCorridorIsClear(WaveSimulation simulation, Vector2 start,
            Vector2 tangent, float length, int ignoredRock)
        {
            for (int sample = 1; sample <= 12; sample++)
            {
                Vector2 point = start + tangent * (length * sample / 12f);
                if (!PointInsideWorld(point, simulation.Config.WorldHalfExtents) || simulation.Environment.IsLand(point))
                    return false;
                if (!ClearOfRocks(simulation, point, simulation.Config.BoatCollisionRadius + 0.04f, ignoredRock))
                    return false;
            }
            return true;
        }

        private static bool SegmentIsWater(WaveSimulation simulation, Vector2 start, Vector2 end)
        {
            for (int sample = 0; sample <= 12; sample++)
            {
                Vector2 point = Vector2.Lerp(start, end, sample / 12f);
                if (simulation.Environment.IsLand(point)) return false;
            }
            return true;
        }

        private static bool ClearOfRocks(WaveSimulation simulation, Vector2 point, float extraRadius, int ignoredRock)
        {
            for (int i = 0; i < simulation.Environment.Rocks.Count; i++)
            {
                if (i == ignoredRock) continue;
                RockData rock = simulation.Environment.Rocks[i];
                float radius = rock.Radius + extraRadius;
                if ((point - rock.Position).sqrMagnitude <= radius * radius) return false;
            }
            return true;
        }

        private static bool PointInsideWorld(Vector2 point, Vector2 half)
        {
            return Mathf.Abs(point.x) < half.x - 0.5f && Mathf.Abs(point.y) < half.y - 0.5f;
        }

        private static void SetProbeBoat(WaveSimulation simulation, Vector2 position, Vector2 heading, float speed)
        {
            BoatData boat = simulation.Boats[0];
            Require(simulation.ConfigureBoatForValidation(boat.Id, position, heading.normalized * speed,
                Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg),
                $"Could not configure validation boat {boat.Id}.");
            simulation.SetPlayerControl(0f, 0f);
        }

        private static ReplayProbe RunReplayProbe()
        {
            const int ticks = 360;
            const int seed = 2371;
            var original = new WaveSimulation(seed, new SimulationConfig { TargetWaveCount = 20 });
            int playerBoatId = original.PlayerBoatId;
            for (int step = 0; step < ticks; step++)
            {
                var control = new BoatControl(1f, Mathf.Sin(step * 0.027f) * 0.52f);
                Require(original.QueueBoatControl(new BoatControlCommand(original.Tick, playerBoatId, control)),
                    $"Original input command was rejected at tick {original.Tick}.");
                original.Step();
            }

            var replay = new WaveSimulation(seed, new SimulationConfig { TargetWaveCount = 20 });
            for (int i = 0; i < original.RecordedControls.Count; i++)
            {
                BoatControlCommand command = original.RecordedControls[i];
                Require(replay.QueueBoatControl(command),
                    $"Replay input command was rejected at tick {command.Tick}.");
            }
            for (int step = 0; step < ticks; step++) replay.Step();

            return new ReplayProbe(ticks, original.RecordedControls.Count,
                original.CalculateStateHash(), replay.CalculateStateHash());
        }

        private static TargetProbe RunTargetProbe()
        {
            var first = new WaveSimulation(9317, new SimulationConfig { TargetWaveCount = 0 });
            var second = new WaveSimulation(9317, new SimulationConfig { TargetWaveCount = 0 });
            bool deterministic = first.Target.Position == second.Target.Position &&
                                 first.CalculateStateHash() == second.CalculateStateHash();

            Require(first.RelocateTarget() && second.RelocateTarget(),
                "Manual target relocation could not find safe open water.");
            deterministic &= first.Target.Position == second.Target.Position &&
                             first.CalculateStateHash() == second.CalculateStateHash();
            Require(first.IsSafeTargetPosition(first.Target.Position),
                "Manually relocated target is not in safe open water.");

            first.SetTargetVisitRadius(6f);
            second.SetTargetVisitRadius(6f);
            Vector2 arrival = first.Target.Position;
            Require(first.ConfigureBoatForValidation(first.PlayerBoatId, arrival, Vector2.zero, 0f) &&
                    second.ConfigureBoatForValidation(second.PlayerBoatId, arrival, Vector2.zero, 0f),
                "Could not place validation boats at the roaming target.");
            first.SetPlayerControl(0f, 0f);
            second.SetPlayerControl(0f, 0f);
            first.Step();
            second.Step();
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            int visitEvents = CountPlayerEvents(first, SimulationEventType.TargetVisited);
            int visitCount = first.Target.VisitCount;
            float relocationDistance = Vector2.Distance(arrival, first.Target.Position);
            Require(first.IsSafeTargetPosition(first.Target.Position),
                "Automatically relocated target is not in safe open water.");

            first.SetTargetEnabled(false);
            second.SetTargetEnabled(false);
            Vector2 disabledTarget = first.Target.Position;
            Require(first.ConfigureBoatForValidation(first.PlayerBoatId, disabledTarget, Vector2.zero, 0f) &&
                    second.ConfigureBoatForValidation(second.PlayerBoatId, disabledTarget, Vector2.zero, 0f),
                "Could not place validation boats at the disabled target.");
            first.Step();
            second.Step();
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            int disabledVisitCount = first.Target.VisitCount;

            first.ResetTargetVisitCount();
            second.ResetTargetVisitCount();
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            return new TargetProbe(visitEvents, visitCount, disabledVisitCount,
                first.Target.VisitCount, relocationDistance, deterministic);
        }

        private static FloatingObjectProbe RunFloatingObjectProbe()
        {
            var configA = new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 };
            var configB = new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 };
            var first = new WaveSimulation(8117, configA,
                new ConstantDepthEnvironmentFactory(11.2f));
            var second = new WaveSimulation(8117, configB,
                new ConstantDepthEnvironmentFactory(11.2f));
            Vector2 boatPosition = first.Boats[0].Position;
            Require(first.SpawnFloatingObject(FloatingObjectKind.Cargo, boatPosition) > 0 &&
                    second.SpawnFloatingObject(FloatingObjectKind.Cargo, boatPosition) > 0,
                "Could not spawn deterministic cargo probe objects.");
            first.Step();
            second.Step();
            int collectionEvents = CountPlayerEvents(first,
                SimulationEventType.FloatingObjectCollected);
            bool deterministic = first.CalculateStateHash() == second.CalculateStateHash();

            Vector2 wreckagePosition = first.Boats[0].Position + Vector2.right * 1.15f;
            Require(first.SpawnFloatingObject(FloatingObjectKind.Wreckage, wreckagePosition) > 0 &&
                    second.SpawnFloatingObject(FloatingObjectKind.Wreckage, wreckagePosition) > 0,
                "Could not spawn deterministic wreckage probe objects.");
            Require(first.ConfigureBoatForValidation(first.PlayerBoatId, first.Boats[0].Position,
                        Vector2.right * 4f, 0f) &&
                    second.ConfigureBoatForValidation(second.PlayerBoatId, second.Boats[0].Position,
                        Vector2.right * 4f, 0f),
                "Could not configure wreckage-probe boats.");
            float speedBefore = first.Boats[0].Velocity.magnitude;
            first.Step();
            second.Step();
            deterministic &= first.CalculateStateHash() == second.CalculateStateHash();
            int wreckageEvents = CountPlayerEvents(first,
                SimulationEventType.BoatHitWreckage);
            float speedChange = Mathf.Abs(first.Boats[0].Velocity.magnitude - speedBefore);
            return new FloatingObjectProbe(collectionEvents, first.CollectedSalvageCount,
                first.CollectedSalvageValue, wreckageEvents, speedChange, deterministic);
        }

        private static BreakingDebrisProbe RunBreakingDebrisProbe()
        {
            Vector2 objectPosition = new Vector2(-100f, -20f);
            var traveling = new WaveSimulation(8119,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new ConstantDepthEnvironmentFactory(8f));
            Require(traveling.SpawnFloatingObject(FloatingObjectKind.Wreckage,
                    objectPosition) > 0,
                "Could not spawn traveling-wave wreckage probe object.");
            traveling.SpawnWaveForValidation(objectPosition, Vector2.right,
                2f, 5f, 60f);
            traveling.Step();
            float travelingSpeed = traveling.FloatingObjects[0].Velocity.magnitude;

            var first = new WaveSimulation(8119,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new ConstantDepthEnvironmentFactory(4.5f));
            var second = new WaveSimulation(8119,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new ConstantDepthEnvironmentFactory(4.5f));
            Require(first.SpawnFloatingObject(FloatingObjectKind.Wreckage,
                        objectPosition) > 0 &&
                    second.SpawnFloatingObject(FloatingObjectKind.Wreckage,
                        objectPosition) > 0,
                "Could not spawn deterministic breaking-wave wreckage probe objects.");
            first.SpawnWaveForValidation(objectPosition, Vector2.right,
                2f, 5f, 60f);
            second.SpawnWaveForValidation(objectPosition, Vector2.right,
                2f, 5f, 60f);
            first.Step();
            second.Step();
            int breakingEvents = CountEvents(first,
                SimulationEventType.FloatingObjectHitByBreakingWave);
            return new BreakingDebrisProbe(travelingSpeed,
                first.FloatingObjects[0].Velocity.magnitude, breakingEvents,
                first.CalculateStateHash() == second.CalculateStateHash());
        }

        private static OffshoreBreakingProbe RunOffshoreBreakingProbe()
        {
            int deepEvents = RunConstantDepthBreakingProbe(8f);
            int shelfEvents = RunConstantDepthBreakingProbe(4.5f);
            return new OffshoreBreakingProbe(deepEvents, shelfEvents, 4.5f);
        }

        private static SourceCadenceProbe RunSourceCadenceProbe()
        {
            var simulation = new WaveSimulation(8131, new SimulationConfig
            {
                TargetWaveCount = 20,
                InitialFloatingObjectCount = 0,
                EnergyDecayPerSecond = 50f
            }, new ConstantDepthEnvironmentFactory(11.2f));
            SwellSystemData system = simulation.SwellSystems[0];
            int expectedPeriod = Mathf.Max(1, Mathf.CeilToInt(system.CalmGapSeconds /
                simulation.Config.FixedDeltaTime));
            // MaintainPopulation runs during the scheduled tick; the public Tick value advances
            // immediately afterward, so observation sees that phase at schedule + 1.
            int expectedFirst = Mathf.Max(1, Mathf.CeilToInt(system.CalmGapSeconds * 0.5f /
                simulation.Config.FixedDeltaTime)) + 1;
            int previousPacketCount = simulation.WaveSources[0].SpawnedPackets;
            int firstEmission = -1;
            int previousEmission = -1;
            int minimumInterval = int.MaxValue;
            int maximumInterval = 0;
            int emissionCount = 0;
            int maximumBurst = 0;
            int minimumPopulation = simulation.Waves.Count;
            for (int step = 0; step < 240; step++)
            {
                simulation.Step();
                int packetCount = simulation.WaveSources[0].SpawnedPackets;
                int emitted = packetCount - previousPacketCount;
                previousPacketCount = packetCount;
                maximumBurst = Mathf.Max(maximumBurst, emitted);
                minimumPopulation = Mathf.Min(minimumPopulation, simulation.Waves.Count);
                if (emitted <= 0) continue;
                int tick = (int)simulation.Tick;
                if (firstEmission < 0) firstEmission = tick;
                if (previousEmission >= 0)
                {
                    int interval = tick - previousEmission;
                    minimumInterval = Mathf.Min(minimumInterval, interval);
                    maximumInterval = Mathf.Max(maximumInterval, interval);
                }
                previousEmission = tick;
                emissionCount += emitted;
            }
            if (minimumInterval == int.MaxValue) minimumInterval = 0;
            return new SourceCadenceProbe(expectedPeriod, expectedFirst, firstEmission,
                emissionCount, maximumBurst, minimumInterval, maximumInterval,
                minimumPopulation);
        }

        private static BreakingLifecycleProbe RunBreakingLifecycleProbe()
        {
            var simulation = new WaveSimulation(8137,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new ConstantDepthEnvironmentFactory(4.5f));
            simulation.SpawnWaveForValidation(new Vector2(-100f, -70f),
                Vector2.right, 2f, 5f, 60f);
            float initialEnergy = AverageActiveSegmentEnergy(simulation.Waves[0]);
            float oneSecondEnergy = 0f;
            float peakFoam = 0f;
            int breakingEvents = 0;
            bool observedBreaking = false;
            bool resumedTraveling = false;
            for (int step = 0; step < 120; step++)
            {
                simulation.Step();
                breakingEvents += CountEvents(simulation,
                    SimulationEventType.WaveStartedBreaking);
                if (simulation.Waves.Count == 0) break;
                WaveData wave = simulation.Waves[0];
                int breakingSegments = 0;
                int travelingSegments = 0;
                for (int segment = 0; segment < wave.Segments.Length; segment++)
                {
                    WaveSegmentData section = wave.Segments[segment];
                    peakFoam = Mathf.Max(peakFoam, section.FoamEnergy);
                    if (!section.Active) continue;
                    if (section.State == WaveState.Breaking) breakingSegments++;
                    else if (section.State == WaveState.Traveling) travelingSegments++;
                }
                observedBreaking |= breakingSegments > 0;
                resumedTraveling |= observedBreaking && breakingSegments == 0 &&
                    travelingSegments > 0;
                if (step == 29) oneSecondEnergy = AverageActiveSegmentEnergy(wave);
            }

            float finalEnergy = simulation.Waves.Count > 0
                ? AverageActiveSegmentEnergy(simulation.Waves[0]) : 0f;
            int activeSegments = 0;
            if (simulation.Waves.Count > 0)
                for (int segment = 0; segment < simulation.Waves[0].Segments.Length; segment++)
                    if (simulation.Waves[0].Segments[segment].Active) activeSegments++;
            return new BreakingLifecycleProbe(initialEnergy, oneSecondEnergy, finalEnergy,
                peakFoam, breakingEvents, activeSegments, resumedTraveling);
        }

        private static float AverageActiveSegmentEnergy(WaveData wave)
        {
            float energy = 0f;
            int active = 0;
            for (int segment = 0; segment < wave.Segments.Length; segment++)
            {
                if (!wave.Segments[segment].Active) continue;
                energy += wave.Segments[segment].Energy;
                active++;
            }
            return active > 0 ? energy / active : 0f;
        }

        private static int RunConstantDepthBreakingProbe(float depth)
        {
            var simulation = new WaveSimulation(8121,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new ConstantDepthEnvironmentFactory(depth));
            simulation.SpawnWaveForValidation(new Vector2(-100f, -70f),
                Vector2.right, 2f, 5f, 60f);
            simulation.Step();
            return CountEvents(simulation, SimulationEventType.WaveStartedBreaking);
        }

        private static int CountPlayerEvents(WaveSimulation simulation, SimulationEventType eventType)
        {
            int count = 0;
            int playerId = simulation.Boats[0].Id;
            for (int i = 0; i < simulation.Events.Count; i++)
                if (simulation.Events[i].Type == eventType && simulation.Events[i].BoatId == playerId) count++;
            return count;
        }

        private static int CountEvents(WaveSimulation simulation, SimulationEventType eventType)
        {
            int count = 0;
            for (int i = 0; i < simulation.Events.Count; i++)
                if (simulation.Events[i].Type == eventType) count++;
            return count;
        }

        private static PerformanceProbe RunPerformanceProbe(int waveCount, int ticks, int seed,
            bool enableSpatialBroadphase = true)
        {
            Vector2 benchmarkHalfExtents = waveCount >= 1000
                ? new Vector2(11200f, 125f)
                : waveCount >= 320 ? new Vector2(3600f, 125f) : new Vector2(225f, 125f);
            var simulation = new WaveSimulation(seed, new SimulationConfig
            {
                TargetWaveCount = waveCount,
                WorldHalfExtents = benchmarkHalfExtents,
                InitialFloatingObjectCount = 0,
                EnableSpatialBroadphase = enableSpatialBroadphase
            });
            int minimumWaveCount = simulation.Waves.Count;
            Process process = Process.GetCurrentProcess();
            TimeSpan cpuStart = process.TotalProcessorTime;
            var timer = Stopwatch.StartNew();
            for (int step = 0; step < ticks; step++)
            {
                simulation.SetPlayerControl(step < ticks * 3 / 4 ? 1f : 0f, Mathf.Sin(step * 0.017f) * 0.42f);
                simulation.Step();
                minimumWaveCount = Mathf.Min(minimumWaveCount, simulation.Waves.Count);
            }
            timer.Stop();
            double cpuSeconds = (process.TotalProcessorTime - cpuStart).TotalSeconds;
            process.Dispose();

            bool finite = true;
            for (int i = 0; i < simulation.Waves.Count && finite; i++)
            {
                WaveData wave = simulation.Waves[i];
                finite = IsFinite(wave.Position) && IsFinite(wave.TravelDirection) &&
                          IsFinite(wave.Energy) && IsFinite(wave.Speed);
                WaveSegmentCollection segments = wave.Segments;
                for (int segmentIndex = 0;
                    segmentIndex < segments.Length && finite; segmentIndex++)
                {
                    WaveSegmentData segment = segments[segmentIndex];
                    finite = IsFinite(segment.PreviousPosition) && IsFinite(segment.Position) &&
                             IsFinite(segment.TravelDirection) && IsFinite(segment.Energy) &&
                             IsFinite(segment.Speed) && IsFinite(segment.SampledDepth) &&
                             IsFinite(segment.DepthGradient) &&
                             IsFinite(segment.BreakingIntensity) && IsFinite(segment.FoamEnergy);
                }
            }
            for (int i = 0; i < simulation.Boats.Count && finite; i++)
            {
                BoatData boat = simulation.Boats[i];
                finite = IsFinite(boat.Position) && IsFinite(boat.Velocity) &&
                         IsFinite(boat.Heading) && IsFinite(boat.Health);
            }
            for (int i = 0; i < simulation.SwellSystems.Count && finite; i++)
            {
                SwellSystemData system = simulation.SwellSystems[i];
                finite = IsFinite(system.Direction) && IsFinite(system.BaseEnergy) &&
                         IsFinite(system.PacketSpacing) && IsFinite(system.MeanPacketLength) &&
                         IsFinite(system.MeanCrestLength) && IsFinite(system.CalmGapSeconds);
            }

            return new PerformanceProbe(ticks, minimumWaveCount, simulation.Waves.Count,
                cpuSeconds, timer.Elapsed.TotalSeconds, simulation.CalculateStateHash(), finite);
        }

        private static PerformanceProbe RunLargeWorldPerformanceProbe(int waveCount, int ticks, int seed)
        {
            var config = new SimulationConfig
            {
                TargetWaveCount = waveCount,
                WorldHalfExtents = new Vector2(900f, 500f),
                InitialFloatingObjectCount = 0,
                EnergyDecayPerSecond = 0f,
                BreakingMinimumEnergyLossPerSecond = 0f,
                BreakingEnergyLossPerSecond = 0f,
                SpentEnergyLossPerSecond = 0f
            };
            var simulation = new WaveSimulation(seed, config);
            int minimumWaveCount = simulation.Waves.Count;
            Process process = Process.GetCurrentProcess();
            TimeSpan cpuStart = process.TotalProcessorTime;
            var timer = Stopwatch.StartNew();
            for (int step = 0; step < ticks; step++)
            {
                simulation.SetPlayerControl(0f, 0f);
                simulation.Step();
                minimumWaveCount = Mathf.Min(minimumWaveCount, simulation.Waves.Count);
            }
            timer.Stop();
            double cpuSeconds = (process.TotalProcessorTime - cpuStart).TotalSeconds;
            process.Dispose();
            bool finite = simulation.Waves.Count > 0;
            for (int i = 0; i < simulation.Waves.Count && finite; i++)
            {
                WaveData wave = simulation.Waves[i];
                finite = IsFinite(wave.Position) && IsFinite(wave.TravelDirection) &&
                         IsFinite(wave.Energy) && IsFinite(wave.Speed);
            }
            return new PerformanceProbe(ticks, minimumWaveCount, simulation.Waves.Count,
                cpuSeconds, timer.Elapsed.TotalSeconds, simulation.CalculateStateHash(), finite);
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static ImpactProbe RunImpactProbe(Vector2 waveDirection, float energy)
        {
            var config = new SimulationConfig { TargetWaveCount = 0 };
            var simulation = new WaveSimulation(991, config);
            BoatData before = simulation.Boats[0];
            simulation.SpawnWave(before.Position, waveDirection, energy);
            for (int i = 0; i < 45; i++) simulation.Step();
            BoatData after = simulation.Boats[0];
            Vector2 side = new Vector2(0f, 1f);
            return new ImpactProbe(Mathf.Abs(Vector2.Dot(after.Position - before.Position, side)),
                Mathf.Abs(Mathf.DeltaAngle(before.Heading, after.Heading)));
        }

        private static CrestCoverageProbe RunCrestCoverageProbe()
        {
            const float crestLength = 70f;
            const float insideOffset = crestLength * 0.48f;
            const float outsideOffset = crestLength * 0.72f;
            Vector2 wavePosition = new Vector2(-175f, -90f);

            var inside = new WaveSimulation(4401, new SimulationConfig { TargetWaveCount = 0 });
            Require(inside.ConfigureBoatForValidation(inside.PlayerBoatId,
                    wavePosition + Vector2.up * insideOffset, Vector2.zero, 0f),
                "Could not place the inside-crest validation boat.");
            inside.SpawnWaveForValidation(wavePosition, Vector2.right, 2f, 5f, crestLength);
            inside.Step();

            var outside = new WaveSimulation(4401, new SimulationConfig { TargetWaveCount = 0 });
            Require(outside.ConfigureBoatForValidation(outside.PlayerBoatId,
                    wavePosition + Vector2.up * outsideOffset, Vector2.zero, 0f),
                "Could not place the outside-crest validation boat.");
            outside.SpawnWaveForValidation(wavePosition, Vector2.right, 2f, 5f, crestLength);
            outside.Step();

            return new CrestCoverageProbe(crestLength, insideOffset, outsideOffset,
                CountPlayerEvents(inside, SimulationEventType.WaveHitBoat),
                CountPlayerEvents(outside, SimulationEventType.WaveHitBoat));
        }

        private static TravelingPassageProbe RunTravelingPassageProbe()
        {
            var simulation = new WaveSimulation(6101,
                new SimulationConfig { TargetWaveCount = 0 },
                new SegmentProbeEnvironmentFactory(false));
            Vector2 boatStart = new Vector2(-100f, -70f);
            Require(simulation.ConfigureBoatForValidation(simulation.PlayerBoatId,
                    boatStart, Vector2.zero, 0f),
                "Could not place the stationary passage-probe boat.");
            simulation.SpawnWaveForValidation(boatStart - Vector2.right * 12f,
                Vector2.right, 1f, 5f, 60f);

            int contactTicks = 0;
            int consecutive = 0;
            int maximumConsecutive = 0;
            int breakingEvents = 0;
            float peakSpeed = 0f;
            for (int step = 0; step < 150; step++)
            {
                simulation.Step();
                int contacts = CountPlayerEvents(simulation, SimulationEventType.WaveHitBoat);
                if (contacts > 0)
                {
                    contactTicks++;
                    consecutive++;
                    maximumConsecutive = Mathf.Max(maximumConsecutive, consecutive);
                }
                else consecutive = 0;
                breakingEvents += CountEvents(simulation, SimulationEventType.WaveStartedBreaking);
                peakSpeed = Mathf.Max(peakSpeed, simulation.Boats[0].Velocity.magnitude);
            }

            BoatData boat = simulation.Boats[0];
            float waveLead = simulation.Waves.Count > 0
                ? simulation.Waves[0].Position.x - boat.Position.x : 0f;
            return new TravelingPassageProbe(contactTicks, maximumConsecutive,
                breakingEvents, Vector2.Distance(boatStart, boat.Position), peakSpeed, waveLead);
        }

        private static StateImpactProbe RunStateImpactProbe(float energy, float packetLength)
        {
            var simulation = new WaveSimulation(6102,
                new SimulationConfig { TargetWaveCount = 0 },
                new SegmentProbeEnvironmentFactory(false));
            Vector2 boatStart = new Vector2(-100f, -70f);
            Require(simulation.ConfigureBoatForValidation(simulation.PlayerBoatId,
                    boatStart, Vector2.zero, 0f),
                "Could not place the state-separation probe boat.");
            simulation.SpawnWaveForValidation(boatStart, Vector2.up,
                energy, packetLength, 60f);
            float initialHeading = simulation.Boats[0].Heading;
            int contactTicks = 0;
            int breakingEvents = 0;
            for (int step = 0; step < 45; step++)
            {
                simulation.Step();
                if (CountPlayerEvents(simulation, SimulationEventType.WaveHitBoat) > 0)
                    contactTicks++;
                breakingEvents += CountEvents(simulation, SimulationEventType.WaveStartedBreaking);
            }
            BoatData boat = simulation.Boats[0];
            return new StateImpactProbe(Vector2.Distance(boatStart, boat.Position),
                Mathf.Abs(Mathf.DeltaAngle(initialHeading, boat.Heading)),
                contactTicks, breakingEvents);
        }

        private static SegmentOcclusionProbe RunSegmentOcclusionProbe()
        {
            var simulation = new WaveSimulation(5201,
                new SimulationConfig { TargetWaveCount = 0 },
                new SegmentProbeEnvironmentFactory(true));
            simulation.SpawnWaveForValidation(new Vector2(-22f, 0f), Vector2.right,
                2f, 5f, 60f);
            int initialSegments = simulation.Waves[0].Segments.Length;
            for (int step = 0; step < 180; step++) simulation.Step();

            WaveSegmentCollection segments = simulation.Waves[0].Segments;
            int active = 0;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i].Active) active++;
            int center = segments.Length / 2;
            float outerPosition = (segments[0].Position.x +
                segments[segments.Length - 1].Position.x) * 0.5f;
            return new SegmentOcclusionProbe(initialSegments, active,
                segments[center].Active, outerPosition - segments[center].Position.x);
        }

        private static ShelfDeformationProbe RunShelfDeformationProbe()
        {
            var simulation = new WaveSimulation(5202,
                new SimulationConfig
                {
                    TargetWaveCount = 0,
                    DepthLimitedBreakingRatio = 10f
                },
                new SegmentProbeEnvironmentFactory(false));
            simulation.SpawnWaveForValidation(new Vector2(-40f, 0f), Vector2.right,
                1.4f, 5f, 60f);
            int initialSegments = simulation.Waves[0].Segments.Length;
            for (int step = 0; step < 150; step++) simulation.Step();

            WaveSegmentCollection segments = simulation.Waves[0].Segments;
            int active = 0;
            float minimumX = float.MaxValue;
            float maximumX = float.MinValue;
            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].Active) continue;
                active++;
                minimumX = Mathf.Min(minimumX, segments[i].Position.x);
                maximumX = Mathf.Max(maximumX, segments[i].Position.x);
            }
            return new ShelfDeformationProbe(initialSegments, active, maximumX - minimumX);
        }

        private sealed class SegmentProbeEnvironmentFactory : IOceanEnvironmentFactory
        {
            private readonly bool island;
            public SegmentProbeEnvironmentFactory(bool island) { this.island = island; }
            public IOceanEnvironment Create(Vector2 worldHalfExtents, int seed)
                => new SegmentProbeEnvironment(island);
        }

        private sealed class ConstantDepthEnvironmentFactory : IOceanEnvironmentFactory
        {
            private readonly float depth;
            public ConstantDepthEnvironmentFactory(float depth) { this.depth = depth; }
            public IOceanEnvironment Create(Vector2 worldHalfExtents, int seed)
                => new ConstantDepthEnvironment(depth);
        }

        private sealed class ConstantDepthEnvironment : IOceanEnvironment
        {
            private static readonly RockData[] NoRocks = Array.Empty<RockData>();
            private readonly float depth;
            public IReadOnlyList<RockData> Rocks => NoRocks;
            public ConstantDepthEnvironment(float depth) { this.depth = depth; }
            public float SampleDepth(Vector2 position) => depth;
            public bool IsLand(Vector2 position) => false;
            public Vector2 SampleDepthGradient(Vector2 position) => Vector2.zero;
            public int FindRock(Vector2 position, float extraRadius) => -1;
        }

        private sealed class SegmentProbeEnvironment : IOceanEnvironment
        {
            private static readonly RockData[] NoRocks = Array.Empty<RockData>();
            private readonly bool island;
            public IReadOnlyList<RockData> Rocks => NoRocks;

            public SegmentProbeEnvironment(bool island) { this.island = island; }

            public float SampleDepth(Vector2 position)
            {
                if (island) return position.sqrMagnitude <= 36f ? 0.08f : 11.2f;
                float shelf = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(-18f, 18f, position.y));
                return Mathf.Lerp(11.2f, 1.35f, shelf);
            }

            public bool IsLand(Vector2 position) => island && position.sqrMagnitude <= 36f;

            public Vector2 SampleDepthGradient(Vector2 position)
            {
                const float offset = 0.25f;
                float dx = SampleDepth(position + Vector2.right * offset) -
                           SampleDepth(position - Vector2.right * offset);
                float dy = SampleDepth(position + Vector2.up * offset) -
                           SampleDepth(position - Vector2.up * offset);
                return new Vector2(dx, dy) / (offset * 2f);
            }

            public int FindRock(Vector2 position, float extraRadius) => -1;
        }

        private static SpeedProbe RunSpeedProbe(Vector2 waveDirection, float energy)
        {
            var config = new SimulationConfig { TargetWaveCount = 0 };
            var simulation = new WaveSimulation(772, config);
            for (int i = 0; i < 90; i++) { simulation.SetPlayerControl(1f, 0f); simulation.Step(); }
            float before = simulation.Boats[0].Velocity.magnitude;
            simulation.SetPlayerControl(0f, 0f);
            simulation.SpawnWave(simulation.Boats[0].Position, waveDirection, energy);
            float peak = before, minimum = before;
            for (int i = 0; i < 45; i++)
            {
                simulation.Step();
                float speed = simulation.Boats[0].Velocity.magnitude;
                peak = Mathf.Max(peak, speed);
                minimum = Mathf.Min(minimum, speed);
            }
            return new SpeedProbe(before, peak, minimum);
        }

    }
}

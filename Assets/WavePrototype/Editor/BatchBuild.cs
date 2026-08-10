using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using WavePrototype.Simulation;
using Debug = UnityEngine.Debug;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private const string ScenePath = "Assets/WavePrototype/WaveDemo.unity";
        private const int BenchmarkTicks = 900;
        private const double ReferenceBenchmarkLimitSeconds = 10.0;
        private const double SecondaryBenchmarkLimitSeconds = 18.0;
        private const double StressBenchmarkLimitSeconds = 30.0;

        [MenuItem("Wave Prototype/Run Validation")]
        public static void Validate()
        {
            try
            {
                RunValidation();
                Debug.Log("[WAVE-VALIDATION] ALL CHECKS PASSED");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void ValidateBatch9Contact()
        {
            TravelingPassageProbe passage = RunTravelingPassageProbe();
            StateImpactProbe traveling = RunStateImpactProbe(0.9f, 5f);
            StateImpactProbe breaking = RunStateImpactProbe(3.2f, 3f);
            Require(passage.BreakingEvents == 0 && passage.ContactTicks > 0 &&
                    passage.MaximumConsecutiveContactTicks <= 22 && passage.WaveLead > 10f &&
                    passage.BoatDisplacement < 3f && passage.PeakBoatSpeed < 4f,
                "Batch 9 traveling-passage probe failed.");
            Require(traveling.BreakingEvents == 0 && breaking.BreakingEvents > 0 &&
                    breaking.Displacement > traveling.Displacement * 2.5f &&
                    breaking.HeadingChange > traveling.HeadingChange + 8f,
                "Batch 9 traveling/breaking separation probe failed.");
            Debug.Log($"[WAVE-CONTACT] PASS passage contacts={passage.ContactTicks}/{passage.MaximumConsecutiveContactTicks} displacement={passage.BoatDisplacement:0.00} peak={passage.PeakBoatSpeed:0.00} lead={passage.WaveLead:0.00}");
            Debug.Log($"[WAVE-CONTACT] PASS traveling displacement/yaw={traveling.Displacement:0.00}/{traveling.HeadingChange:0.0}° contacts={traveling.ContactTicks}; breaking={breaking.Displacement:0.00}/{breaking.HeadingChange:0.0}° contacts={breaking.ContactTicks}");
        }

        public static void ValidateBatch10Scalability()
        {
            var simulation = new WaveSimulation(4041, new SimulationConfig
            {
                TargetWaveCount = 10000,
                WorldHalfExtents = new Vector2(900f, 500f),
                InitialFloatingObjectCount = 0,
                EnergyDecayPerSecond = 0f,
                BreakingMinimumEnergyLossPerSecond = 0f,
                BreakingEnergyLossPerSecond = 0f,
                SpentEnergyLossPerSecond = 0f
            });
            Debug.Log($"[WAVE-10K] initialized={simulation.Waves.Count} segments={simulation.TotalWaveSegmentCount}");
            for (int step = 0; step < 30; step++)
            {
                simulation.Step();
                if (step < 3 || step == 29)
                    Debug.Log($"[WAVE-10K] tick={simulation.Tick} waves={simulation.Waves.Count} segments={simulation.ActiveWaveSegmentCount}/{simulation.TotalWaveSegmentCount}");
            }
        }

        public static void ValidateBatch13Scalability()
        {
            PerformanceProbe secondary = RunPerformanceProbe(320, BenchmarkTicks, 4041);
            PerformanceProbe stress = RunPerformanceProbe(1000, BenchmarkTicks, 4041);
            Debug.Log($"[WAVE-B13-SCALE] 320 fronts ticks={secondary.Ticks} cpu/wall={secondary.CpuSeconds:0.000}/{secondary.WallSeconds:0.000}s rate={secondary.UpdatesPerSecond:0.0} min/final={secondary.MinimumWaveCount}/{secondary.FinalWaveCount} hash={secondary.FinalHash:X16}");
            Debug.Log($"[WAVE-B13-SCALE] 1000 fronts ticks={stress.Ticks} cpu/wall={stress.CpuSeconds:0.000}/{stress.WallSeconds:0.000}s rate={stress.UpdatesPerSecond:0.0} min/final={stress.MinimumWaveCount}/{stress.FinalWaveCount} hash={stress.FinalHash:X16}");
        }

        public static void RunValidation()
        {
            const int seed = 1847;
            ValidateArchitectureBoundaries();
            var first = new WaveSimulation(seed);
            var second = new WaveSimulation(seed);
            Require(first.Waves.Count == first.InitialWaveTarget,
                "Initial wave population must match its resolved span/period target.");
            Require(first.Boats.Count == 3, "Batch 17 must initialize with one player and two passive boats.");
            VesselProfileDefinition initialSkiff = first.Config.GetVesselProfile(
                VesselProfileId.ArcadeSkiff);
            VesselProfileDefinition initialHeavy = first.Config.GetVesselProfile(
                VesselProfileId.HeavyCutter);
            for (int boatIndex = 0; boatIndex < first.Boats.Count; boatIndex++)
                Require(first.Boats[boatIndex].Profile == VesselProfileId.ArcadeSkiff &&
                        Mathf.Abs(first.Boats[boatIndex].Mass - initialSkiff.Mass) < 0.001f,
                    $"Initial boat {first.Boats[boatIndex].Id} did not preserve the arcade-skiff baseline.");
            Require(initialSkiff.HullSampleCount == 1 && initialHeavy.HullSampleCount == 5 &&
                    initialHeavy.Mass > initialSkiff.Mass * 3f &&
                    initialHeavy.HullLength > initialSkiff.HullLength * 2f,
                "Vessel definitions do not provide the intended point-skiff / broad-heavy contrast.");
            Require(first.Config.WorldHalfExtents == new Vector2(675f, 250f),
                "Batch 17 playable world must be 1350 x 500 units.");
            Require(first.Config.TargetWaveCount < 0 &&
                    first.InitialWaveTarget >= 58 && first.InitialWaveTarget <= 64,
                $"Batch 17 span/period reconstruction resolved {first.InitialWaveTarget} fronts.");
            Require(first.Config.DesiredVisibleWaveCount == 7,
                "Batch 17 must preserve the seven-front local density reference.");
            Require(Mathf.Abs(first.Config.EnergyDecayPerSecond - 0.0025f) < 0.00001f &&
                    first.Config.WaveMinimumActiveSegmentFraction <= 0f,
                "Batch 17 must use long-range deep-water retention and last-section lifetime.");
            Require(first.Environment.Rocks.Count >= 650,
                $"Expanded shelves produced only {first.Environment.Rocks.Count} rock hazards.");
            ExplorationScaleProbe exploration = RunExplorationScaleProbe();
            Require(exploration.PriorPhases >= exploration.ReferencePhases * 2 &&
                    exploration.PriorPhases <= exploration.ReferencePhases * 2 + 1 &&
                    exploration.ExpandedPhases >= exploration.ReferencePhases * 3 &&
                    exploration.ExpandedPhases <= exploration.ReferencePhases * 3 + 2,
                $"Span-derived phases did not scale with travel distance: {exploration.ReferencePhases}->{exploration.PriorPhases}->{exploration.ExpandedPhases}.");
            Require(Mathf.Abs(exploration.ReferenceSpacing - exploration.ExpandedSpacing) < 0.001f,
                $"Map expansion changed local swell spacing: {exploration.ReferenceSpacing:0.00}/{exploration.ExpandedSpacing:0.00}.");
            Require(exploration.CrestScale > 1.9f && exploration.CrestScale < 2.01f,
                $"Full-width crest scale is invalid: {exploration.CrestScale:0.000}x.");
            Require(exploration.ReferenceRocks == 320 &&
                    exploration.PriorRocks > exploration.ReferenceRocks &&
                    exploration.ExpandedRocks > exploration.PriorRocks,
                $"Shelf hazard scaling produced {exploration.ReferenceRocks}/{exploration.PriorRocks}/{exploration.ExpandedRocks} rocks.");
            Require(exploration.ExplicitOverridePhases == 17,
                $"Explicit phase-count override produced {exploration.ExplicitOverridePhases}/17 fronts.");
            Require(exploration.DisabledWaves == 0 && exploration.DisabledObjects == 0,
                "Zero-count ocean did not disable initial waves and floating objects.");
            OceanContinuityProbe continuity = RunOceanContinuityProbe();
            Require(continuity.ShelfArrivalTick > 3000 &&
                    continuity.MaximumTravelX >= first.Config.WorldHalfExtents.x - 155f &&
                    continuity.ShelfArrivalDepth > 0.24f &&
                    continuity.ShelfArrivalDepth < 2.5f &&
                    continuity.ShelfArrivalEnergy > first.Config.MinimumEnergy,
                $"Boundary-born swell failed to reach the eastern near shelf: tick={continuity.ShelfArrivalTick}, x={continuity.MaximumTravelX:0.0}, depth={continuity.ShelfArrivalDepth:0.00}, energy={continuity.ShelfArrivalEnergy:0.000}.");
            Require(continuity.SurvivedBelowLegacyCutoff &&
                    continuity.MinimumActiveSegments <
                        Mathf.CeilToInt(continuity.InitialSegments * 0.45f),
                $"A partially shadowed front did not survive below the legacy group cutoff: {continuity.MinimumActiveSegments}/{continuity.InitialSegments}.");
            Require(continuity.ExpirationTick > continuity.ShelfArrivalTick,
                $"Boundary-born swell expired before completing its terrain encounter: shelf={continuity.ShelfArrivalTick}, expired={continuity.ExpirationTick}.");
            Require(first.Target.Enabled, "The optional roaming target must begin enabled.");
            Require(first.Target.VisitCount == 0, "The roaming target visit counter must begin at zero.");
            Require(first.IsSafeTargetPosition(first.Target.Position), "Initial target position is not safe open water.");
            Require(first.WaveSources.Count == 3, $"Expected one active and two dormant source definitions, found {first.WaveSources.Count}.");
            Require(first.ActiveWaveSourceCount == 1,
                $"Batch 13 normal ocean must have exactly one active source, found {first.ActiveWaveSourceCount}.");
            Require(first.SwellSystems.Count == 1,
                $"The unified ocean should own one continuous swell stream, found {first.SwellSystems.Count}.");
            Require(first.FloatingObjects.Count == first.Config.InitialFloatingObjectCount,
                $"Floating-object service created {first.FloatingObjects.Count}/{first.Config.InitialFloatingObjectCount} objects.");
            int initialCargo = 0, initialWreckage = 0;
            for (int i = 0; i < first.FloatingObjects.Count; i++)
            {
                FloatingObjectData item = first.FloatingObjects[i];
                if (item.Kind == FloatingObjectKind.Cargo) initialCargo++;
                else initialWreckage++;
                Require(item.Active && !first.Environment.IsLand(item.Position) &&
                        first.Environment.FindRock(item.Position, item.Radius) < 0,
                    $"Floating object {item.Id} did not begin in clear water.");
            }
            Require(initialCargo > 0 && initialWreckage > 0,
                $"Floating-object mix is incomplete: cargo={initialCargo}, wreckage={initialWreckage}.");
            int initialSwellSystemCount = first.SwellSystems.Count;
            int initiallySourcedPackets = 0;
            for (int i = 0; i < first.Waves.Count; i++)
            {
                Require(first.Waves[i].SourceId > 0, $"Initial wave {first.Waves[i].Id} has no explicit source identity.");
                Require(first.Waves[i].SwellSystemId > 0, $"Initial wave {first.Waves[i].Id} has no swell-system identity.");
                ValidateSegmentedWave(first.Waves[i], first.Config);
                initiallySourcedPackets++;
            }
            Require(initiallySourcedPackets == first.InitialWaveTarget,
                "The unified source did not populate the complete playable sea.");
            Require(first.TotalWaveSegmentCount >= first.InitialWaveTarget * 35,
                $"The exploration-scale fronts produced only {first.TotalWaveSegmentCount} crest segments.");
            ValidateInitialSwellSystems(first);
            var initialSourcePackets = new int[first.WaveSources.Count];
            for (int i = 0; i < initialSourcePackets.Length; i++)
                initialSourcePackets[i] = first.WaveSources[i].SpawnedPackets;

            Vector2 initialWavePosition = first.Waves[0].Position;
            Vector2 initialBoatPosition = first.Boats[0].Position;
            float initialHeading = first.Boats[0].Heading;
            float initialEnergy = first.Waves[0].Energy;
            int breakingObserved = 0;
            int rockHitsObserved = 0;
            int damageEventsObserved = 0;
            int waveBoatHitsObserved = 0;
            int spentFramesObserved = 0;
            float maximumHeadingExcursion = 0f;
            int minimumPopulation = first.Waves.Count;
            int maximumPopulation = first.Waves.Count;

            // Identical explicit input and full-front injection validates the same public
            // interface now used by the cursor tool.
            int preInjectionCount = first.Waves.Count;
            Require(first.SpawnSwellFront(initialBoatPosition, 3.1f) &&
                    second.SpawnSwellFront(initialBoatPosition, 3.1f),
                "The cursor-facing swell-front interface could not use the active system.");
            Require(first.Waves.Count == preInjectionCount + 1,
                "The cursor-facing swell interface did not add exactly one front.");
            WaveData injectedFront = first.Waves[first.Waves.Count - 1];
            Require(injectedFront.SourceId == first.SwellSystems[0].SourceId &&
                    injectedFront.SwellSystemId == first.SwellSystems[0].Id &&
                    injectedFront.CrestLength > first.Config.WorldHalfExtents.y * 2f &&
                    injectedFront.Segments.Length >= 5 &&
                    Vector2.Angle(injectedFront.TravelDirection,
                        first.SwellSystems[0].Direction) < 0.01f,
                "Cursor injection did not create a natural-format segmented swell front.");

            var timer = Stopwatch.StartNew();
            for (int step = 0; step < 900; step++)
            {
                float steering = Mathf.Sin(step * 0.021f) * 0.58f;
                float throttle = step < 760 ? 1f : -0.25f;
                first.SetPlayerControl(throttle, steering);
                second.SetPlayerControl(throttle, steering);
                first.Step(); second.Step();
                maximumHeadingExcursion = Mathf.Max(maximumHeadingExcursion,
                    Mathf.Abs(Mathf.DeltaAngle(first.Boats[0].Heading, initialHeading)));
                ulong a = first.CalculateStateHash();
                ulong b = second.CalculateStateHash();
                Require(a == b, $"Determinism failed at tick {step}: {a:X16} != {b:X16}");
                minimumPopulation = Mathf.Min(minimumPopulation, first.Waves.Count);
                maximumPopulation = Mathf.Max(maximumPopulation, first.Waves.Count);
                Require(first.Waves.Count >= first.InitialWaveTarget - 14,
                    $"Phase-authoritative swell lifecycle dropped population too far: {first.Waves.Count}.");
                for (int i = 0; i < first.Waves.Count; i++)
                    if (first.Waves[i].State == WaveState.Spent) spentFramesObserved++;
                for (int i = 0; i < first.Events.Count; i++)
                {
                    switch (first.Events[i].Type)
                    {
                        case SimulationEventType.WaveStartedBreaking: breakingObserved++; break;
                        case SimulationEventType.WaveHitRock: rockHitsObserved++; break;
                        case SimulationEventType.BoatDamaged: damageEventsObserved++; break;
                        case SimulationEventType.WaveHitBoat: waveBoatHitsObserved++; break;
                    }
                }
            }
            timer.Stop();

            Require(first.Tick == 900, "Tick counter did not advance exactly.");
            Require(first.Waves[0].Position != initialWavePosition, "Waves did not propagate.");
            Require(first.Waves[0].Energy < initialEnergy || first.Waves[0].Id != 1, "Wave energy did not decay or expire.");
            Require(Vector2.Distance(first.Boats[0].Position, initialBoatPosition) > 10f, "Arcade sailing did not move the player meaningfully.");
            Require(maximumHeadingExcursion > 20f, $"Arcade steering excursion reached only {maximumHeadingExcursion:0.0} degrees.");
            Require(breakingObserved > 0, "No breaking transitions were observed.");
            Require(rockHitsObserved > 0, "No rock interactions were observed.");
            Require(waveBoatHitsObserved > 0, "No wave-to-boat interactions were observed.");
            Require(damageEventsObserved > 0, "Breaking waves never produced boat-damage events.");
            Require(spentFramesObserved > 0, "Breaking waves never collapsed into spent foam.");
            Require(first.GetWindEfficiency(0f) > first.GetWindEfficiency(180f), "Forgiving wind influence is oriented incorrectly.");
            for (int i = 0; i < first.WaveSources.Count; i++)
            {
                WaveSourceData source = first.WaveSources[i];
                if (source.Enabled)
                    Require(source.SpawnedPackets > initialSourcePackets[i],
                        $"Unified source {source.Id} emitted no new phase fronts during the 30-second run.");
                else
                    Require(source.SpawnedPackets == 0,
                        $"Dormant source {source.Id} emitted {source.SpawnedPackets} unauthorized fronts.");
            }

            int landSamples = 0, shallowSamples = 0, deepSamples = 0;
            for (int y = -240; y <= 240; y += 10)
            for (int x = -665; x <= 665; x += 10)
            {
                float depth = first.Environment.SampleDepth(new Vector2(x, y));
                if (depth <= 0.24f) landSamples++;
                else if (depth < 2.4f) shallowSamples++;
                else if (depth > 7f) deepSamples++;
            }
            Require(landSamples > 260 && shallowSamples > 390 && deepSamples > 2600,
                $"Continental and insular shelves are not legible in sampled bathymetry: land={landSamples}, shallow={shallowSamples}, deep={deepSamples}.");
            Require(first.Environment.SampleDepth(new Vector2(650f, 0f)) <= 0.24f,
                "Eastern continental landmass is missing.");
            float continentalShelfDepth = first.Environment.SampleDepth(new Vector2(525f, 0f));
            Require(continentalShelfDepth > 0.24f && continentalShelfDepth < 2.5f,
                $"Continental shelf sample is not shallow navigable water: {continentalShelfDepth:0.00}.");
            float outerContinentalShelfDepth = first.Environment.SampleDepth(new Vector2(375f, 0f));
            Require(outerContinentalShelfDepth > 2.5f && outerContinentalShelfDepth < 7f,
                $"Outer continental shelf/slope is not prominent: {outerContinentalShelfDepth:0.00}.");
            Require(first.Environment.SampleDepth(new Vector2(-205f, 85f)) <= 0.24f,
                "Primary insular landmass is missing.");
            float insularShelfDepth = first.Environment.SampleDepth(new Vector2(-142.5f, 85f));
            Require(insularShelfDepth > 0.24f && insularShelfDepth < 4f,
                $"Insular shelf sample is not shallow navigable water: {insularShelfDepth:0.00}.");
            Require(first.Environment.SampleDepth(new Vector2(-565f, 45f)) <= 0.24f,
                "New western island-chain landmass is missing.");
            float westernShelfDepth = first.Environment.SampleDepth(new Vector2(-510f, 45f));
            Require(westernShelfDepth > 0.24f && westernShelfDepth < 4f,
                $"Western island shelf sample is not shallow navigable water: {westernShelfDepth:0.00}.");
            Require(first.Environment.SampleDepth(new Vector2(-630f, -220f)) > 7f,
                "Open basin sample should remain mechanically deep water.");

            float minimumRockRadius = float.MaxValue;
            float maximumRockRadius = 0f;
            float totalRockRadius = 0f;
            int westernChainRocks = 0;
            for (int rockIndex = 0; rockIndex < first.Environment.Rocks.Count; rockIndex++)
            {
                RockData rock = first.Environment.Rocks[rockIndex];
                minimumRockRadius = Mathf.Min(minimumRockRadius, rock.Radius);
                maximumRockRadius = Mathf.Max(maximumRockRadius, rock.Radius);
                totalRockRadius += rock.Radius;
                if (rock.Position.x < -450f) westernChainRocks++;
            }
            float averageRockRadius = totalRockRadius / first.Environment.Rocks.Count;
            Require(minimumRockRadius >= 0.79f && maximumRockRadius >= 2.5f &&
                    averageRockRadius > 1.15f,
                $"Rock scale is not substantial enough: min/avg/max={minimumRockRadius:0.00}/{averageRockRadius:0.00}/{maximumRockRadius:0.00}.");
            Require(westernChainRocks >= 30,
                $"Western extension received only {westernChainRocks} shelf-driven rocks.");

            float averageCrest = 0f;
            for (int i = 0; i < first.Waves.Count; i++) averageCrest += first.Waves[i].CrestLength;
            averageCrest /= first.Waves.Count;
            Require(averageCrest > first.Config.WorldHalfExtents.y * 2f,
                $"Unified swell regressed below the map cross-span: average crest {averageCrest:0.0}.");

            ImpactProbe side = RunImpactProbe(Vector2.up, 2.25f);
            Require(side.LateralDisplacement > 1.5f,
                $"Side wave displaced the boat only {side.LateralDisplacement:0.00} units.");
            Require(side.HeadingChange > 8f,
                $"Side wave changed heading only {side.HeadingChange:0.0} degrees.");

            CrestCoverageProbe crestCoverage = RunCrestCoverageProbe();
            Require(crestCoverage.InsideHits == 1,
                $"Boat inside a broad crest received {crestCoverage.InsideHits} hits instead of one.");
            Require(crestCoverage.OutsideHits == 0,
                $"Boat outside a broad crest received {crestCoverage.OutsideHits} hits.");

            TravelingPassageProbe passage = RunTravelingPassageProbe();
            Require(passage.BreakingEvents == 0,
                $"Ordinary passage probe unexpectedly broke {passage.BreakingEvents} crest sections.");
            Require(passage.ContactTicks > 0 && passage.MaximumConsecutiveContactTicks <= 22,
                $"Traveling crest contact lasted {passage.MaximumConsecutiveContactTicks} consecutive ticks ({passage.ContactTicks} total).");
            Require(passage.WaveLead > 10f,
                $"Traveling crest failed to overtake the stationary hull; final lead {passage.WaveLead:0.00}.");
            Require(passage.BoatDisplacement < 3f && passage.PeakBoatSpeed < 4f,
                $"Ordinary crest carried the stationary hull too strongly: displacement={passage.BoatDisplacement:0.00}, peak={passage.PeakBoatSpeed:0.00}.");

            StateImpactProbe travelingImpact = RunStateImpactProbe(0.9f, 5f);
            StateImpactProbe breakingImpact = RunStateImpactProbe(3.2f, 3f);
            Require(travelingImpact.BreakingEvents == 0 && breakingImpact.BreakingEvents > 0,
                $"Impact-state probes did not separate traveling/breaking behavior: {travelingImpact.BreakingEvents}/{breakingImpact.BreakingEvents}.");
            Require(breakingImpact.Displacement > travelingImpact.Displacement * 2.5f,
                $"Breaking displacement {breakingImpact.Displacement:0.00} is not distinct from traveling {travelingImpact.Displacement:0.00}.");
            Require(breakingImpact.HeadingChange > travelingImpact.HeadingChange + 8f,
                $"Breaking yaw {breakingImpact.HeadingChange:0.0} is not distinct from traveling {travelingImpact.HeadingChange:0.0}.");

            VesselProfileProbe vesselProfiles = RunVesselProfileProbe();
            Require(vesselProfiles.Deterministic,
                "Heavy-vessel profile behavior diverged between identical simulations.");
            Require(vesselProfiles.HeavyMass > vesselProfiles.SkiffMass * 3f,
                $"Heavy/skiff mass contrast is too small: {vesselProfiles.HeavyMass:0.0}/{vesselProfiles.SkiffMass:0.0}.");
            Require(vesselProfiles.HeavySpeed < vesselProfiles.SkiffSpeed * 0.9f &&
                    vesselProfiles.HeavySpeed > vesselProfiles.SkiffSpeed * 0.35f,
                $"Heavy propulsion response is not distinct but usable: skiff/heavy={vesselProfiles.SkiffSpeed:0.00}/{vesselProfiles.HeavySpeed:0.00}.");
            Require(vesselProfiles.HeavyTurn < vesselProfiles.SkiffTurn * 0.75f,
                $"Heavy turn response is insufficiently distinct: skiff/heavy={vesselProfiles.SkiffTurn:0.0}/{vesselProfiles.HeavyTurn:0.0} degrees.");
            Require(vesselProfiles.SkiffBroadHits == 0 && vesselProfiles.HeavyBroadHits == 1,
                $"Broad-hull reach probe produced skiff/heavy hits {vesselProfiles.SkiffBroadHits}/{vesselProfiles.HeavyBroadHits}.");
            Require(vesselProfiles.HeavyCenterHits == 1,
                $"Five heavy-hull samples produced {vesselProfiles.HeavyCenterHits} impulses from one crest.");
            Require(vesselProfiles.SkiffGroundings == 0 && vesselProfiles.HeavyGroundings == 1,
                $"Broad-hull grounding probe produced skiff/heavy contacts {vesselProfiles.SkiffGroundings}/{vesselProfiles.HeavyGroundings}.");
            Require(vesselProfiles.SkiffBreakingDamage > 0f &&
                    vesselProfiles.HeavyBreakingDamage < vesselProfiles.SkiffBreakingDamage * 0.8f,
                $"Heavy breaker resistance is not distinct: skiff/heavy damage={vesselProfiles.SkiffBreakingDamage:0.000}/{vesselProfiles.HeavyBreakingDamage:0.000}.");
            Require(vesselProfiles.HeavyBreakingDisplacement <
                    vesselProfiles.SkiffBreakingDisplacement * 0.9f,
                $"Heavy breaker inertia is not distinct: skiff/heavy displacement={vesselProfiles.SkiffBreakingDisplacement:0.00}/{vesselProfiles.HeavyBreakingDisplacement:0.00}.");

            SegmentOcclusionProbe occlusion = RunSegmentOcclusionProbe();
            Require(occlusion.InitialSegments >= 5,
                $"Island probe created only {occlusion.InitialSegments} crest segments.");
            Require(!occlusion.CenterActive && occlusion.ActiveSegments >= 4,
                $"Island did not remove only the blocked crest section: centerActive={occlusion.CenterActive}, active={occlusion.ActiveSegments}.");
            Require(occlusion.CenterLag > 12f,
                $"Island shadow opened only {occlusion.CenterLag:0.00} units behind the passing outer crest.");

            ShelfDeformationProbe shelfDeformation = RunShelfDeformationProbe();
            Require(shelfDeformation.ForwardSpread > 5f,
                $"Cross-crest shelf sampling deformed the front by only {shelfDeformation.ForwardSpread:0.00} units.");
            Require(shelfDeformation.ActiveSegments == shelfDeformation.InitialSegments,
                $"Navigable shelf deformation incorrectly removed segments: {shelfDeformation.ActiveSegments}/{shelfDeformation.InitialSegments}.");

            SpeedProbe following = RunSpeedProbe(Vector2.right, 2.75f);
            SpeedProbe headOn = RunSpeedProbe(Vector2.left, 2.75f);
            CruiseProbe cruise = RunCruiseProbe();
            Require(first.Config.BoatSurfSpeedCap > first.Config.BoatCruiseSpeed,
                "Surf speed cap must remain explicitly above the normal propulsion cruise speed.");
            Require(Mathf.Abs(first.Config.MaximumBoatSpeed - first.Config.BoatCruiseSpeed) < 0.001f,
                "MaximumBoatSpeed compatibility alias must resolve to BoatCruiseSpeed.");
            Require(cruise.PeakSpeed <= first.Config.BoatCruiseSpeed + 0.08f,
                $"Normal propulsion escaped the cruise envelope: peak {cruise.PeakSpeed:0.00}, cap {first.Config.BoatCruiseSpeed:0.00}.");
            Require(cruise.PeakSpeed >= first.Config.BoatCruiseSpeed * 0.88f,
                $"Normal propulsion never approached cruise speed: peak {cruise.PeakSpeed:0.00}, cap {first.Config.BoatCruiseSpeed:0.00}.");
            Require(cruise.CollisionEvents == 0,
                $"Cruise envelope probe encountered {cruise.CollisionEvents} environmental collisions and is not isolated.");
            Require(following.PeakAfterImpact > following.SpeedBeforeImpact * 1.12f,
                $"Following swell produced insufficient surfing: {following.SpeedBeforeImpact:0.00} -> {following.PeakAfterImpact:0.00}.");
            Require(following.PeakAfterImpact > first.Config.BoatCruiseSpeed * 1.02f,
                $"Following swell never crossed the explicit cruise ceiling: peak {following.PeakAfterImpact:0.00}, cruise {first.Config.BoatCruiseSpeed:0.00}.");
            Require(following.PeakAfterImpact <= first.Config.BoatSurfSpeedCap + 0.08f,
                $"Following swell escaped the surf envelope: peak {following.PeakAfterImpact:0.00}, cap {first.Config.BoatSurfSpeedCap:0.00}.");
            Require(headOn.MinimumAfterImpact < headOn.SpeedBeforeImpact * 0.78f,
                $"Head-on swell produced insufficient slowdown: {headOn.SpeedBeforeImpact:0.00} -> {headOn.MinimumAfterImpact:0.00}.");

            RockSweepProbe rockSweep = RunSweptRockProbe();
            Require(rockSweep.Deterministic, "Swept rock contact diverged between identical simulations.");
            Require(rockSweep.ImpactEvents == 1,
                $"A one-tick swept crossing produced {rockSweep.ImpactEvents} player rock impacts instead of exactly one.");
            Require(!rockSweep.Tunneled,
                $"Boat tunneled through swept rock contact (projection {rockSweep.PostImpactProjection:0.00}, radius {rockSweep.CombinedRadius:0.00}).");
            Require(rockSweep.EscapeDistance > 1.25f,
                $"Boat remained sticky after swept contact; tangential escape reached only {rockSweep.EscapeDistance:0.00} units.");
            Require(rockSweep.EscapeImpactEvents == 0,
                $"Tangential escape generated {rockSweep.EscapeImpactEvents} repeated player rock impacts.");

            ReplayProbe replay = RunReplayProbe();
            Require(replay.CommandCount == replay.Ticks,
                $"Tick-addressed recording captured {replay.CommandCount} commands for {replay.Ticks} ticks.");
            Require(replay.Deterministic,
                $"Recorded-input replay diverged: original={replay.OriginalHash:X16}, replay={replay.ReplayHash:X16}.");

            TargetProbe targetProbe = RunTargetProbe();
            Require(targetProbe.Deterministic, "Roaming-target operations diverged between identical worlds.");
            Require(targetProbe.VisitEvents == 1 && targetProbe.VisitCountAfterArrival == 1,
                $"Target arrival produced {targetProbe.VisitEvents} events and count {targetProbe.VisitCountAfterArrival}.");
            Require(targetProbe.DisabledVisitCount == targetProbe.VisitCountAfterArrival,
                "Disabled target continued counting visits.");
            Require(targetProbe.FinalVisitCount == 0, "Target visit counter did not reset to zero.");
            Require(targetProbe.RelocationDistance >= first.Config.TargetMinimumRelocationDistance,
                $"Visited target relocated only {targetProbe.RelocationDistance:0.0} units from the boat.");

            FloatingObjectProbe floatingProbe = RunFloatingObjectProbe();
            Require(floatingProbe.Deterministic,
                "Floating-object collection/contact diverged between identical simulations.");
            Require(floatingProbe.CollectionEvents == 1 && floatingProbe.CollectedCount == 1 &&
                    floatingProbe.CollectedValue >= 1f,
                $"Cargo probe produced events/count/value {floatingProbe.CollectionEvents}/{floatingProbe.CollectedCount}/{floatingProbe.CollectedValue:0.0}.");
            Require(floatingProbe.WreckageEvents > 0 && floatingProbe.WreckageSpeedChange > 0.05f,
                $"Wreckage probe produced events/speed-change {floatingProbe.WreckageEvents}/{floatingProbe.WreckageSpeedChange:0.00}.");

            BreakingDebrisProbe debrisProbe = RunBreakingDebrisProbe();
            Require(debrisProbe.Deterministic,
                "Breaking-wave wreckage impulse diverged between identical simulations.");
            Require(debrisProbe.BreakingEvents == 1,
                $"Breaking-wave wreckage probe emitted {debrisProbe.BreakingEvents} impulse events.");
            Require(debrisProbe.BreakingSpeed > debrisProbe.TravelingSpeed + 1.5f,
                $"Breaking water did not decisively throw wreckage: traveling={debrisProbe.TravelingSpeed:0.00}, breaking={debrisProbe.BreakingSpeed:0.00}.");

            OffshoreBreakingProbe offshoreBreaking = RunOffshoreBreakingProbe();
            Require(offshoreBreaking.DeepControlBreakingEvents == 0,
                $"Deep-water control produced {offshoreBreaking.DeepControlBreakingEvents} breaking events.");
            Require(offshoreBreaking.ShelfBreakingEvents > 0 && offshoreBreaking.BreakingDepth > 3.5f,
                $"Depth-limited probe failed to break offshore: events={offshoreBreaking.ShelfBreakingEvents}, depth={offshoreBreaking.BreakingDepth:0.0}.");

            SourceCadenceProbe cadence = RunSourceCadenceProbe();
            Require(cadence.ActualFirstTick == cadence.ExpectedFirstTick,
                $"First source phase arrived at tick {cadence.ActualFirstTick} instead of {cadence.ExpectedFirstTick}.");
            Require(cadence.MaximumTickBurst == 1,
                $"Population loss triggered a {cadence.MaximumTickBurst}-front same-tick burst.");
            Require(cadence.EmissionCount >= 2 &&
                    cadence.MinimumIntervalTicks == cadence.ExpectedPeriodTicks &&
                    cadence.MaximumIntervalTicks == cadence.ExpectedPeriodTicks,
                $"Source phase intervals are not authoritative: count={cadence.EmissionCount}, interval={cadence.MinimumIntervalTicks}-{cadence.MaximumIntervalTicks}, expected={cadence.ExpectedPeriodTicks}.");
            Require(cadence.MinimumPopulation <= 2,
                $"Cadence probe never exposed population loss; minimum was {cadence.MinimumPopulation}.");

            BreakingLifecycleProbe breakingLifecycle = RunBreakingLifecycleProbe();
            Require(breakingLifecycle.BreakingEvents > 0 &&
                    breakingLifecycle.PeakFoamEnergy > 0.03f,
                $"Breaking lifecycle produced events/foam {breakingLifecycle.BreakingEvents}/{breakingLifecycle.PeakFoamEnergy:0.000}.");
            Require(breakingLifecycle.OneSecondEnergy > breakingLifecycle.InitialEnergy * 0.55f &&
                    breakingLifecycle.OneSecondEnergy < breakingLifecycle.InitialEnergy * 0.95f,
                $"Breaking did not shed a partial energy share after one second: {breakingLifecycle.InitialEnergy:0.00}->{breakingLifecycle.OneSecondEnergy:0.00}.");
            Require(breakingLifecycle.FinalEnergy > breakingLifecycle.InitialEnergy * 0.5f &&
                    breakingLifecycle.ActiveSegments >= 5 && breakingLifecycle.ResumedTraveling,
                $"Residual swell failed to survive and resume: energy={breakingLifecycle.FinalEnergy:0.00}, active={breakingLifecycle.ActiveSegments}, resumed={breakingLifecycle.ResumedTraveling}.");

            SpatialBroadphaseProbe spatial = RunSpatialBroadphaseProbe();
            Require(spatial.MatchingTicks == 480 &&
                    spatial.BroadphaseHash == spatial.BruteForceHash,
                $"Spatial broadphase diverged from brute force after {spatial.MatchingTicks}/480 ticks: {spatial.BroadphaseHash:X16}/{spatial.BruteForceHash:X16}.");
            Require(spatial.IndexedSections > 0 && spatial.OccupiedCells > 0,
                $"Spatial wave index was empty: sections/cells={spatial.IndexedSections}/{spatial.OccupiedCells}.");
            Require(spatial.WaveBoatPotential > 0 &&
                    spatial.WaveBoatExact < spatial.WaveBoatPotential / 3,
                $"Wave/boat broadphase retained too many candidates: {spatial.WaveBoatExact}/{spatial.WaveBoatPotential}.");
            Require(spatial.FloatingPotential > 0 &&
                    spatial.FloatingExact < spatial.FloatingPotential / 3,
                $"Floating-object broadphase retained too many candidates: {spatial.FloatingExact}/{spatial.FloatingPotential}.");
            Require(spatial.RockPotential > 0 && spatial.RockExact < spatial.RockPotential,
                $"Static-rock broadphase did not cull swept checks: {spatial.RockExact}/{spatial.RockPotential}.");
            Require(spatial.Gen0Collections <= 1,
                $"Warmed broadphase triggered {spatial.Gen0Collections} generation-0 collections over 240 ticks.");

            WaveDensitySample density = first.SampleWaveDensity(first.Boats[0].Position, 36.5f);
            Require(density.WorldCount == first.Waves.Count,
                "Density diagnostics did not match authoritative wave state.");
            Require(density.LocalCount >= 0 && density.LocalCount <= density.WorldCount,
                $"Local density is invalid: {density.LocalCount}/{density.WorldCount}.");
            Require(density.LocalCount >= 2 && density.LocalCount <= 9,
                $"Expanded ocean local swell density left its readable band: {density.LocalCount} fronts.");
            var densityProbe = new WaveSimulation(1482,
                new SimulationConfig { TargetWaveCount = 0, InitialFloatingObjectCount = 0 },
                new SegmentProbeEnvironmentFactory(false));
            densityProbe.SpawnWaveForValidation(new Vector2(0f, 30f), Vector2.right,
                0.7f, 3f, 60f);
            WaveDensitySample segmentDensity = densityProbe.SampleWaveDensity(Vector2.zero, 4f);
            Require(segmentDensity.LocalCount == 1,
                "Visible-front density ignored an on-screen crest because its parent center was off-screen.");
            Require(first.ActiveWaveSegmentCount >= Mathf.CeilToInt(first.TotalWaveSegmentCount * 0.7f),
                $"Long-run crest population hollowed into scraps: {first.ActiveWaveSegmentCount}/{first.TotalWaveSegmentCount} segments remain active.");

            float nominalWidthCrossingSeconds = first.Config.WorldHalfExtents.x * 2f /
                                                first.Config.BoatCruiseSpeed;
            Require(nominalWidthCrossingSeconds >= 105f,
                $"Expanded ocean still crosses nominally in only {nominalWidthCrossingSeconds:0.0}s.");

            PerformanceProbe playable = RunPerformanceProbe(20, BenchmarkTicks, 4041);
            PerformanceProbe secondary = RunPerformanceProbe(320, BenchmarkTicks, 4041);
            PerformanceProbe stress = RunPerformanceProbe(1000, BenchmarkTicks, 4041);
            PerformanceProbe tenThousand = RunLargeWorldPerformanceProbe(10000, 30, 4041);
            Require(playable.CpuSeconds < ReferenceBenchmarkLimitSeconds,
                $"20-front reference benchmark consumed {playable.CpuSeconds:0.000}s CPU; limit is {ReferenceBenchmarkLimitSeconds:0.0}s.");
            Require(secondary.CpuSeconds < SecondaryBenchmarkLimitSeconds,
                $"320-wave secondary benchmark consumed {secondary.CpuSeconds:0.000}s CPU; limit is {SecondaryBenchmarkLimitSeconds:0.0}s.");
            Require(stress.CpuSeconds < StressBenchmarkLimitSeconds,
                $"1,000-wave stress soak consumed {stress.CpuSeconds:0.000}s CPU; limit is {StressBenchmarkLimitSeconds:0.0}s.");
            Require(playable.MinimumWaveCount >= 8 && playable.FinalWaveCount >= 8,
                $"20-front reference source/lifetime equilibrium collapsed: min={playable.MinimumWaveCount}, final={playable.FinalWaveCount}.");
            // Expanded synthetic profiles now scale shelf-rock counts and crest coverage, so
            // terrain-driven loss is intentionally higher than in the Batch 15 fixed-rock runs.
            Require(secondary.MinimumWaveCount >= 270 && secondary.FinalWaveCount >= 270,
                $"Secondary source/lifetime profile collapsed: min={secondary.MinimumWaveCount}, final={secondary.FinalWaveCount}.");
            Require(stress.MinimumWaveCount >= 800 && stress.FinalWaveCount >= 800,
                $"Stress source/lifetime profile collapsed: min={stress.MinimumWaveCount}, final={stress.FinalWaveCount}.");
            Require(playable.StateFinite && secondary.StateFinite && stress.StateFinite,
                "A benchmark completed with non-finite simulation state.");
            Require(tenThousand.StateFinite && tenThousand.MinimumWaveCount >= 9000,
                $"10,000-front large-world diagnostic became invalid: min={tenThousand.MinimumWaveCount}, finite={tenThousand.StateFinite}.");

            Debug.Log($"[WAVE-VALIDATION] Determinism: 900/900 matching ticks; final hash {first.CalculateStateHash():X16}");
            Debug.Log($"[WAVE-VALIDATION] Behaviors: breaking={breakingObserved}, rockHits={rockHitsObserved}, waveBoatHits={waveBoatHitsObserved}, damageEvents={damageEventsObserved}");
            Debug.Log($"[WAVE-VALIDATION] World: 1350x500, rocks={first.Environment.Rocks.Count}, rockRadius={minimumRockRadius:0.00}/{averageRockRadius:0.00}/{maximumRockRadius:0.00}, westernRocks={westernChainRocks}, averageCrest={averageCrest:0.00}, terrainSamples={landSamples}/{shallowSamples}/{deepSamples}, shelfDepths={continentalShelfDepth:0.00}/{outerContinentalShelfDepth:0.00}/{insularShelfDepth:0.00}/{westernShelfDepth:0.00}, nominalCrossing={nominalWidthCrossingSeconds:0.0}s");
            Debug.Log($"[WAVE-VALIDATION] Exploration scale: phases={exploration.ReferencePhases}->{exploration.PriorPhases}->{exploration.ExpandedPhases}, rocks={exploration.ReferenceRocks}->{exploration.PriorRocks}->{exploration.ExpandedRocks}, spacing={exploration.ReferenceSpacing:0.00}/{exploration.ExpandedSpacing:0.00}, crestScale={exploration.CrestScale:0.000}x, explicit={exploration.ExplicitOverridePhases}, disabled={exploration.DisabledWaves}/{exploration.DisabledObjects}");
            Debug.Log($"[WAVE-VALIDATION] Ocean continuity: segments={continuity.MinimumActiveSegments}/{continuity.InitialSegments} minimum/initial, shelfTick={continuity.ShelfArrivalTick}, expireTick={continuity.ExpirationTick}, maxX={continuity.MaximumTravelX:0.0}, shelfDepth/energy={continuity.ShelfArrivalDepth:0.00}/{continuity.ShelfArrivalEnergy:0.000}, survivedLegacyCutoff={continuity.SurvivedBelowLegacyCutoff}");
            Debug.Log($"[WAVE-VALIDATION] Impact: sideDisplacement={side.LateralDisplacement:0.00}, sideYaw={side.HeadingChange:0.0}°, surf={following.SpeedBeforeImpact:0.00}->{following.PeakAfterImpact:0.00}, headOn={headOn.SpeedBeforeImpact:0.00}->{headOn.MinimumAfterImpact:0.00}");
            Debug.Log($"[WAVE-VALIDATION] Crest coverage: width={crestCoverage.CrestLength:0}, inside={crestCoverage.InsideOffset:0.0}/{crestCoverage.InsideHits} hit, outside={crestCoverage.OutsideOffset:0.0}/{crestCoverage.OutsideHits} hits");
            Debug.Log($"[WAVE-VALIDATION] Passage: contacts={passage.ContactTicks}/{passage.MaximumConsecutiveContactTicks} total/consecutive, displacement={passage.BoatDisplacement:0.00}, peak={passage.PeakBoatSpeed:0.00}, lead={passage.WaveLead:0.00}");
            Debug.Log($"[WAVE-VALIDATION] State separation: traveling displacement/yaw={travelingImpact.Displacement:0.00}/{travelingImpact.HeadingChange:0.0}°, breaking={breakingImpact.Displacement:0.00}/{breakingImpact.HeadingChange:0.0}°");
            Debug.Log($"[WAVE-VALIDATION] Vessels: mass={vesselProfiles.SkiffMass:0.0}/{vesselProfiles.HeavyMass:0.0}, speed={vesselProfiles.SkiffSpeed:0.00}/{vesselProfiles.HeavySpeed:0.00}, turn={vesselProfiles.SkiffTurn:0.0}/{vesselProfiles.HeavyTurn:0.0}°, broadHits={vesselProfiles.SkiffBroadHits}/{vesselProfiles.HeavyBroadHits}/{vesselProfiles.HeavyCenterHits}, grounding={vesselProfiles.SkiffGroundings}/{vesselProfiles.HeavyGroundings}, breakerDamage={vesselProfiles.SkiffBreakingDamage:0.000}/{vesselProfiles.HeavyBreakingDamage:0.000}, breakerMove={vesselProfiles.SkiffBreakingDisplacement:0.00}/{vesselProfiles.HeavyBreakingDisplacement:0.00}");
            Debug.Log($"[WAVE-VALIDATION] Segments: reference={first.ActiveWaveSegmentCount}/{first.TotalWaveSegmentCount}, island={occlusion.ActiveSegments}/{occlusion.InitialSegments} active center={occlusion.CenterActive} lag={occlusion.CenterLag:0.00}, shelfSpread={shelfDeformation.ForwardSpread:0.00} active={shelfDeformation.ActiveSegments}/{shelfDeformation.InitialSegments}");
            Debug.Log($"[WAVE-VALIDATION] Speed envelope: cruisePeak/final={cruise.PeakSpeed:0.00}/{cruise.FinalSpeed:0.00}, cruiseCap={first.Config.BoatCruiseSpeed:0.00}, surfPeak/cap={following.PeakAfterImpact:0.00}/{first.Config.BoatSurfSpeedCap:0.00}");
            Debug.Log($"[WAVE-VALIDATION] Swept rock: index={rockSweep.RockIndex}, impacts={rockSweep.ImpactEvents}, projection={rockSweep.PostImpactProjection:0.00}/{rockSweep.CombinedRadius:0.00}, escape={rockSweep.EscapeDistance:0.00}, escapeImpacts={rockSweep.EscapeImpactEvents}");
            Debug.Log($"[WAVE-VALIDATION] Swells: systems={initialSwellSystemCount}/{first.SwellSystems.Count} initial/final, sources={first.WaveSources.Count}, initialPackets={initiallySourcedPackets}, populationRange={minimumPopulation}-{maximumPopulation}, local/world/reference={density.LocalCount}/{density.WorldCount}/{density.DesiredVisibleCount}");
            Debug.Log($"[WAVE-VALIDATION] Replay: ticks={replay.Ticks}, commands={replay.CommandCount}, hash={replay.ReplayHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] Target: visits={targetProbe.VisitCountAfterArrival}, event={targetProbe.VisitEvents}, relocation={targetProbe.RelocationDistance:0.0}, disabledCount={targetProbe.DisabledVisitCount}, reset={targetProbe.FinalVisitCount}");
            Debug.Log($"[WAVE-VALIDATION] Floating objects: initial cargo/wreckage={initialCargo}/{initialWreckage}, collection={floatingProbe.CollectionEvents}/{floatingProbe.CollectedCount}/{floatingProbe.CollectedValue:0}, wreckageEvents={floatingProbe.WreckageEvents}, speedChange={floatingProbe.WreckageSpeedChange:0.00}");
            Debug.Log($"[WAVE-VALIDATION] Breaking debris: traveling/breaking speed={debrisProbe.TravelingSpeed:0.00}/{debrisProbe.BreakingSpeed:0.00}, impulseEvents={debrisProbe.BreakingEvents}");
            Debug.Log($"[WAVE-VALIDATION] Offshore breaking: deepEvents={offshoreBreaking.DeepControlBreakingEvents}, shelfEvents={offshoreBreaking.ShelfBreakingEvents}, shelfDepth={offshoreBreaking.BreakingDepth:0.0}");
            Debug.Log($"[WAVE-VALIDATION] Source cadence: first={cadence.ActualFirstTick}/{cadence.ExpectedFirstTick}, period={cadence.MinimumIntervalTicks}-{cadence.MaximumIntervalTicks}/{cadence.ExpectedPeriodTicks} ticks, emissions={cadence.EmissionCount}, maxBurst={cadence.MaximumTickBurst}, minPopulation={cadence.MinimumPopulation}");
            Debug.Log($"[WAVE-VALIDATION] Breaking lifecycle: energy={breakingLifecycle.InitialEnergy:0.00}->{breakingLifecycle.OneSecondEnergy:0.00}->{breakingLifecycle.FinalEnergy:0.00}, peakFoam={breakingLifecycle.PeakFoamEnergy:0.000}, events={breakingLifecycle.BreakingEvents}, active={breakingLifecycle.ActiveSegments}, resumed={breakingLifecycle.ResumedTraveling}");
            Debug.Log($"[WAVE-VALIDATION] Spatial broadphase: hashes={spatial.BroadphaseHash:X16}/{spatial.BruteForceHash:X16} matching={spatial.MatchingTicks}, waveBoat={spatial.WaveBoatExact:N0}/{spatial.WaveBoatPotential:N0}, floating={spatial.FloatingExact:N0}/{spatial.FloatingPotential:N0}, rocks={spatial.RockExact:N0}/{spatial.RockPotential:N0}, sections/cells={spatial.IndexedSections}/{spatial.OccupiedCells}, gen0={spatial.Gen0Collections}");
            Debug.Log($"[WAVE-VALIDATION] Expanded determinism benchmark: 1,800 world-steps with {first.InitialWaveTarget}+ waves in {timer.Elapsed.TotalSeconds:0.000}s");
            Debug.Log($"[WAVE-VALIDATION] 20-front reference benchmark: ticks={playable.Ticks} cpu/wall={playable.CpuSeconds:0.000}/{playable.WallSeconds:0.000}s cpuRate={playable.UpdatesPerSecond:0.0} ticks/s hash={playable.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] Secondary benchmark: waves=320 ticks={secondary.Ticks} cpu/wall={secondary.CpuSeconds:0.000}/{secondary.WallSeconds:0.000}s cpuRate={secondary.UpdatesPerSecond:0.0} ticks/s hash={secondary.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] Stress soak: waves=1000 ticks={stress.Ticks} cpu/wall={stress.CpuSeconds:0.000}/{stress.WallSeconds:0.000}s cpuRate={stress.UpdatesPerSecond:0.0} ticks/s min/final={stress.MinimumWaveCount}/{stress.FinalWaveCount} hash={stress.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] 10k diagnostic: world=1800x1000 waves=10000 ticks={tenThousand.Ticks} cpu/wall={tenThousand.CpuSeconds:0.000}/{tenThousand.WallSeconds:0.000}s cpuRate={tenThousand.UpdatesPerSecond:0.0} ticks/s min/final={tenThousand.MinimumWaveCount}/{tenThousand.FinalWaveCount} hash={tenThousand.FinalHash:X16}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WAVE-VALIDATION] " + message);
        }
    }
}

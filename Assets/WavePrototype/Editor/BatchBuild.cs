using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WavePrototype.Simulation;
using Debug = UnityEngine.Debug;

namespace WavePrototype.Editor
{
    public static class BatchBuild
    {
        private const string ScenePath = "Assets/WavePrototype/WaveDemo.unity";
        private const int BenchmarkTicks = 900;
        private const double PlayableBenchmarkLimitSeconds = 10.0;
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
            var first = new WaveSimulation(seed);
            var second = new WaveSimulation(seed);
            Require(first.Waves.Count == first.Config.TargetWaveCount, "Initial wave population must match its configured target.");
            Require(first.Boats.Count == 3, "Batch 13 must initialize with one player and two passive boats.");
            Require(first.Config.WorldHalfExtents == new Vector2(225f, 125f), "Batch 13 world must remain 450 x 250 units.");
            Require(first.Config.TargetWaveCount == 20, "Batch 13 playable profile must begin with exactly 20 long-period fronts.");
            Require(first.Config.DesiredVisibleWaveCount == 7, "Batch 13 must halve the local full-width-front density reference.");
            Require(first.Environment.Rocks.Count >= 140, $"Bathymetry produced only {first.Environment.Rocks.Count} shelf-driven rock hazards.");
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
            Require(initiallySourcedPackets == first.Config.TargetWaveCount,
                "The unified source did not populate the complete playable sea.");
            Require(first.TotalWaveSegmentCount >= 400,
                $"The 20 map-spanning fronts produced only {first.TotalWaveSegmentCount} crest segments.");
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
                Require(first.Waves.Count >= first.Config.TargetWaveCount - 8,
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
            for (int y = -120; y <= 120; y += 5)
            for (int x = -220; x <= 220; x += 5)
            {
                float depth = first.Environment.SampleDepth(new Vector2(x, y));
                if (depth <= 0.24f) landSamples++;
                else if (depth < 2.4f) shallowSamples++;
                else if (depth > 7f) deepSamples++;
            }
            Require(landSamples > 220 && shallowSamples > 300 && deepSamples > 1500,
                $"Continental and insular shelves are not legible in sampled bathymetry: land={landSamples}, shallow={shallowSamples}, deep={deepSamples}.");
            Require(first.Environment.SampleDepth(new Vector2(210f, 0f)) <= 0.24f,
                "Eastern continental landmass is missing.");
            float continentalShelfDepth = first.Environment.SampleDepth(new Vector2(150f, 0f));
            Require(continentalShelfDepth > 0.24f && continentalShelfDepth < 2.5f,
                $"Continental shelf sample is not shallow navigable water: {continentalShelfDepth:0.00}.");
            float outerContinentalShelfDepth = first.Environment.SampleDepth(new Vector2(75f, 0f));
            Require(outerContinentalShelfDepth > 2.5f && outerContinentalShelfDepth < 7f,
                $"Outer continental shelf/slope is not prominent: {outerContinentalShelfDepth:0.00}.");
            Require(first.Environment.SampleDepth(new Vector2(-102.5f, 42.5f)) <= 0.24f,
                "Primary insular landmass is missing.");
            float insularShelfDepth = first.Environment.SampleDepth(new Vector2(-71.25f, 42.5f));
            Require(insularShelfDepth > 0.24f && insularShelfDepth < 4f,
                $"Insular shelf sample is not shallow navigable water: {insularShelfDepth:0.00}.");
            Require(first.Environment.SampleDepth(new Vector2(-187.5f, -105f)) > 7f,
                "Open basin sample should remain mechanically deep water.");

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

            WaveDensitySample density = first.SampleWaveDensity(first.Boats[0].Position, 36.5f);
            Require(density.WorldCount == first.Waves.Count,
                "Density diagnostics did not match authoritative wave state.");
            Require(density.LocalCount >= 0 && density.LocalCount <= density.WorldCount,
                $"Local density is invalid: {density.LocalCount}/{density.WorldCount}.");
            Require(first.ActiveWaveSegmentCount >= Mathf.CeilToInt(first.TotalWaveSegmentCount * 0.7f),
                $"Long-run crest population hollowed into scraps: {first.ActiveWaveSegmentCount}/{first.TotalWaveSegmentCount} segments remain active.");

            float nominalWidthCrossingSeconds = first.Config.WorldHalfExtents.x * 2f /
                                                first.Config.BoatCruiseSpeed;
            Require(nominalWidthCrossingSeconds >= 35f,
                $"Expanded ocean still crosses nominally in only {nominalWidthCrossingSeconds:0.0}s.");

            PerformanceProbe playable = RunPerformanceProbe(20, BenchmarkTicks, 4041);
            PerformanceProbe secondary = RunPerformanceProbe(320, BenchmarkTicks, 4041);
            PerformanceProbe stress = RunPerformanceProbe(1000, BenchmarkTicks, 4041);
            PerformanceProbe tenThousand = RunLargeWorldPerformanceProbe(10000, 30, 4041);
            Require(playable.CpuSeconds < PlayableBenchmarkLimitSeconds,
                $"20-front playable benchmark consumed {playable.CpuSeconds:0.000}s CPU; limit is {PlayableBenchmarkLimitSeconds:0.0}s.");
            Require(secondary.CpuSeconds < SecondaryBenchmarkLimitSeconds,
                $"320-wave secondary benchmark consumed {secondary.CpuSeconds:0.000}s CPU; limit is {SecondaryBenchmarkLimitSeconds:0.0}s.");
            Require(stress.CpuSeconds < StressBenchmarkLimitSeconds,
                $"1,000-wave stress soak consumed {stress.CpuSeconds:0.000}s CPU; limit is {StressBenchmarkLimitSeconds:0.0}s.");
            Require(playable.MinimumWaveCount >= 8 && playable.FinalWaveCount >= 8,
                $"Playable source/lifetime equilibrium collapsed: min={playable.MinimumWaveCount}, final={playable.FinalWaveCount}.");
            Require(secondary.MinimumWaveCount >= 285 && secondary.FinalWaveCount >= 285,
                $"Secondary source/lifetime profile collapsed: min={secondary.MinimumWaveCount}, final={secondary.FinalWaveCount}.");
            Require(stress.MinimumWaveCount >= 850 && stress.FinalWaveCount >= 850,
                $"Stress source/lifetime profile collapsed: min={stress.MinimumWaveCount}, final={stress.FinalWaveCount}.");
            Require(playable.StateFinite && secondary.StateFinite && stress.StateFinite,
                "A benchmark completed with non-finite simulation state.");
            Require(tenThousand.StateFinite && tenThousand.MinimumWaveCount >= 9000,
                $"10,000-front large-world diagnostic became invalid: min={tenThousand.MinimumWaveCount}, finite={tenThousand.StateFinite}.");

            Debug.Log($"[WAVE-VALIDATION] Determinism: 900/900 matching ticks; final hash {first.CalculateStateHash():X16}");
            Debug.Log($"[WAVE-VALIDATION] Behaviors: breaking={breakingObserved}, rockHits={rockHitsObserved}, waveBoatHits={waveBoatHitsObserved}, damageEvents={damageEventsObserved}");
            Debug.Log($"[WAVE-VALIDATION] World: 450x250, rocks={first.Environment.Rocks.Count}, averageCrest={averageCrest:0.00}, terrainSamples={landSamples}/{shallowSamples}/{deepSamples}, shelfDepths={continentalShelfDepth:0.00}/{outerContinentalShelfDepth:0.00}/{insularShelfDepth:0.00}, nominalCrossing={nominalWidthCrossingSeconds:0.0}s");
            Debug.Log($"[WAVE-VALIDATION] Impact: sideDisplacement={side.LateralDisplacement:0.00}, sideYaw={side.HeadingChange:0.0}°, surf={following.SpeedBeforeImpact:0.00}->{following.PeakAfterImpact:0.00}, headOn={headOn.SpeedBeforeImpact:0.00}->{headOn.MinimumAfterImpact:0.00}");
            Debug.Log($"[WAVE-VALIDATION] Crest coverage: width={crestCoverage.CrestLength:0}, inside={crestCoverage.InsideOffset:0.0}/{crestCoverage.InsideHits} hit, outside={crestCoverage.OutsideOffset:0.0}/{crestCoverage.OutsideHits} hits");
            Debug.Log($"[WAVE-VALIDATION] Passage: contacts={passage.ContactTicks}/{passage.MaximumConsecutiveContactTicks} total/consecutive, displacement={passage.BoatDisplacement:0.00}, peak={passage.PeakBoatSpeed:0.00}, lead={passage.WaveLead:0.00}");
            Debug.Log($"[WAVE-VALIDATION] State separation: traveling displacement/yaw={travelingImpact.Displacement:0.00}/{travelingImpact.HeadingChange:0.0}°, breaking={breakingImpact.Displacement:0.00}/{breakingImpact.HeadingChange:0.0}°");
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
            Debug.Log($"[WAVE-VALIDATION] Determinism benchmark: 1,800 world-steps with {first.Config.TargetWaveCount}+ waves in {timer.Elapsed.TotalSeconds:0.000}s");
            Debug.Log($"[WAVE-VALIDATION] Playable benchmark: waves=20 ticks={playable.Ticks} cpu/wall={playable.CpuSeconds:0.000}/{playable.WallSeconds:0.000}s cpuRate={playable.UpdatesPerSecond:0.0} ticks/s hash={playable.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] Secondary benchmark: waves=320 ticks={secondary.Ticks} cpu/wall={secondary.CpuSeconds:0.000}/{secondary.WallSeconds:0.000}s cpuRate={secondary.UpdatesPerSecond:0.0} ticks/s hash={secondary.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] Stress soak: waves=1000 ticks={stress.Ticks} cpu/wall={stress.CpuSeconds:0.000}/{stress.WallSeconds:0.000}s cpuRate={stress.UpdatesPerSecond:0.0} ticks/s min/final={stress.MinimumWaveCount}/{stress.FinalWaveCount} hash={stress.FinalHash:X16}");
            Debug.Log($"[WAVE-VALIDATION] 10k diagnostic: world=1800x1000 waves=10000 ticks={tenThousand.Ticks} cpu/wall={tenThousand.CpuSeconds:0.000}/{tenThousand.WallSeconds:0.000}s cpuRate={tenThousand.UpdatesPerSecond:0.0} ticks/s min/final={tenThousand.MinimumWaveCount}/{tenThousand.FinalWaveCount} hash={tenThousand.FinalHash:X16}");
        }

        private readonly struct ImpactProbe
        {
            public readonly float LateralDisplacement;
            public readonly float HeadingChange;
            public ImpactProbe(float lateralDisplacement, float headingChange)
            {
                LateralDisplacement = lateralDisplacement;
                HeadingChange = headingChange;
            }
        }

        private readonly struct CrestCoverageProbe
        {
            public readonly float CrestLength;
            public readonly float InsideOffset;
            public readonly float OutsideOffset;
            public readonly int InsideHits;
            public readonly int OutsideHits;

            public CrestCoverageProbe(float crestLength, float insideOffset,
                float outsideOffset, int insideHits, int outsideHits)
            {
                CrestLength = crestLength;
                InsideOffset = insideOffset;
                OutsideOffset = outsideOffset;
                InsideHits = insideHits;
                OutsideHits = outsideHits;
            }
        }

        private readonly struct TravelingPassageProbe
        {
            public readonly int ContactTicks;
            public readonly int MaximumConsecutiveContactTicks;
            public readonly int BreakingEvents;
            public readonly float BoatDisplacement;
            public readonly float PeakBoatSpeed;
            public readonly float WaveLead;

            public TravelingPassageProbe(int contactTicks, int maximumConsecutiveContactTicks,
                int breakingEvents, float boatDisplacement, float peakBoatSpeed, float waveLead)
            {
                ContactTicks = contactTicks;
                MaximumConsecutiveContactTicks = maximumConsecutiveContactTicks;
                BreakingEvents = breakingEvents;
                BoatDisplacement = boatDisplacement;
                PeakBoatSpeed = peakBoatSpeed;
                WaveLead = waveLead;
            }
        }

        private readonly struct StateImpactProbe
        {
            public readonly float Displacement;
            public readonly float HeadingChange;
            public readonly int ContactTicks;
            public readonly int BreakingEvents;

            public StateImpactProbe(float displacement, float headingChange,
                int contactTicks, int breakingEvents)
            {
                Displacement = displacement;
                HeadingChange = headingChange;
                ContactTicks = contactTicks;
                BreakingEvents = breakingEvents;
            }
        }

        private readonly struct SegmentOcclusionProbe
        {
            public readonly int InitialSegments;
            public readonly int ActiveSegments;
            public readonly bool CenterActive;
            public readonly float CenterLag;

            public SegmentOcclusionProbe(int initialSegments, int activeSegments,
                bool centerActive, float centerLag)
            {
                InitialSegments = initialSegments;
                ActiveSegments = activeSegments;
                CenterActive = centerActive;
                CenterLag = centerLag;
            }
        }

        private readonly struct ShelfDeformationProbe
        {
            public readonly int InitialSegments;
            public readonly int ActiveSegments;
            public readonly float ForwardSpread;

            public ShelfDeformationProbe(int initialSegments, int activeSegments,
                float forwardSpread)
            {
                InitialSegments = initialSegments;
                ActiveSegments = activeSegments;
                ForwardSpread = forwardSpread;
            }
        }

        private readonly struct SpeedProbe
        {
            public readonly float SpeedBeforeImpact;
            public readonly float PeakAfterImpact;
            public readonly float MinimumAfterImpact;
            public SpeedProbe(float before, float peak, float minimum)
            {
                SpeedBeforeImpact = before; PeakAfterImpact = peak; MinimumAfterImpact = minimum;
            }
        }

        private readonly struct CruiseProbe
        {
            public readonly float PeakSpeed;
            public readonly float FinalSpeed;
            public readonly int CollisionEvents;

            public CruiseProbe(float peakSpeed, float finalSpeed, int collisionEvents)
            {
                PeakSpeed = peakSpeed;
                FinalSpeed = finalSpeed;
                CollisionEvents = collisionEvents;
            }
        }

        private readonly struct RockSweepProbe
        {
            public readonly int RockIndex;
            public readonly int ImpactEvents;
            public readonly int EscapeImpactEvents;
            public readonly float CombinedRadius;
            public readonly float PostImpactProjection;
            public readonly float EscapeDistance;
            public readonly bool Tunneled;
            public readonly bool Deterministic;

            public RockSweepProbe(int rockIndex, int impactEvents, int escapeImpactEvents,
                float combinedRadius, float postImpactProjection, float escapeDistance,
                bool tunneled, bool deterministic)
            {
                RockIndex = rockIndex;
                ImpactEvents = impactEvents;
                EscapeImpactEvents = escapeImpactEvents;
                CombinedRadius = combinedRadius;
                PostImpactProjection = postImpactProjection;
                EscapeDistance = escapeDistance;
                Tunneled = tunneled;
                Deterministic = deterministic;
            }
        }

        private readonly struct PerformanceProbe
        {
            public readonly int Ticks;
            public readonly int MinimumWaveCount;
            public readonly int FinalWaveCount;
            public readonly double CpuSeconds;
            public readonly double WallSeconds;
            public readonly ulong FinalHash;
            public readonly bool StateFinite;
            public double UpdatesPerSecond => Ticks / Math.Max(0.000001, CpuSeconds);

            public PerformanceProbe(int ticks, int minimumWaveCount, int finalWaveCount,
                double cpuSeconds, double wallSeconds, ulong finalHash, bool stateFinite)
            {
                Ticks = ticks;
                MinimumWaveCount = minimumWaveCount;
                FinalWaveCount = finalWaveCount;
                CpuSeconds = cpuSeconds;
                WallSeconds = wallSeconds;
                FinalHash = finalHash;
                StateFinite = stateFinite;
            }
        }

        private readonly struct ReplayProbe
        {
            public readonly int Ticks;
            public readonly int CommandCount;
            public readonly ulong OriginalHash;
            public readonly ulong ReplayHash;
            public bool Deterministic => OriginalHash == ReplayHash;

            public ReplayProbe(int ticks, int commandCount, ulong originalHash, ulong replayHash)
            {
                Ticks = ticks;
                CommandCount = commandCount;
                OriginalHash = originalHash;
                ReplayHash = replayHash;
            }
        }

        private readonly struct TargetProbe
        {
            public readonly int VisitEvents;
            public readonly int VisitCountAfterArrival;
            public readonly int DisabledVisitCount;
            public readonly int FinalVisitCount;
            public readonly float RelocationDistance;
            public readonly bool Deterministic;

            public TargetProbe(int visitEvents, int visitCountAfterArrival,
                int disabledVisitCount, int finalVisitCount, float relocationDistance,
                bool deterministic)
            {
                VisitEvents = visitEvents;
                VisitCountAfterArrival = visitCountAfterArrival;
                DisabledVisitCount = disabledVisitCount;
                FinalVisitCount = finalVisitCount;
                RelocationDistance = relocationDistance;
                Deterministic = deterministic;
            }
        }

        private readonly struct FloatingObjectProbe
        {
            public readonly int CollectionEvents;
            public readonly int CollectedCount;
            public readonly float CollectedValue;
            public readonly int WreckageEvents;
            public readonly float WreckageSpeedChange;
            public readonly bool Deterministic;

            public FloatingObjectProbe(int collectionEvents, int collectedCount,
                float collectedValue, int wreckageEvents, float wreckageSpeedChange,
                bool deterministic)
            {
                CollectionEvents = collectionEvents;
                CollectedCount = collectedCount;
                CollectedValue = collectedValue;
                WreckageEvents = wreckageEvents;
                WreckageSpeedChange = wreckageSpeedChange;
                Deterministic = deterministic;
            }
        }

        private readonly struct OffshoreBreakingProbe
        {
            public readonly int DeepControlBreakingEvents;
            public readonly int ShelfBreakingEvents;
            public readonly float BreakingDepth;

            public OffshoreBreakingProbe(int deepControlBreakingEvents,
                int shelfBreakingEvents, float breakingDepth)
            {
                DeepControlBreakingEvents = deepControlBreakingEvents;
                ShelfBreakingEvents = shelfBreakingEvents;
                BreakingDepth = breakingDepth;
            }
        }

        private readonly struct BreakingDebrisProbe
        {
            public readonly float TravelingSpeed;
            public readonly float BreakingSpeed;
            public readonly int BreakingEvents;
            public readonly bool Deterministic;

            public BreakingDebrisProbe(float travelingSpeed, float breakingSpeed,
                int breakingEvents, bool deterministic)
            {
                TravelingSpeed = travelingSpeed;
                BreakingSpeed = breakingSpeed;
                BreakingEvents = breakingEvents;
                Deterministic = deterministic;
            }
        }

        private readonly struct SourceCadenceProbe
        {
            public readonly int ExpectedPeriodTicks;
            public readonly int ExpectedFirstTick;
            public readonly int ActualFirstTick;
            public readonly int EmissionCount;
            public readonly int MaximumTickBurst;
            public readonly int MinimumIntervalTicks;
            public readonly int MaximumIntervalTicks;
            public readonly int MinimumPopulation;

            public SourceCadenceProbe(int expectedPeriodTicks, int expectedFirstTick,
                int actualFirstTick, int emissionCount, int maximumTickBurst,
                int minimumIntervalTicks, int maximumIntervalTicks, int minimumPopulation)
            {
                ExpectedPeriodTicks = expectedPeriodTicks;
                ExpectedFirstTick = expectedFirstTick;
                ActualFirstTick = actualFirstTick;
                EmissionCount = emissionCount;
                MaximumTickBurst = maximumTickBurst;
                MinimumIntervalTicks = minimumIntervalTicks;
                MaximumIntervalTicks = maximumIntervalTicks;
                MinimumPopulation = minimumPopulation;
            }
        }

        private readonly struct BreakingLifecycleProbe
        {
            public readonly float InitialEnergy;
            public readonly float OneSecondEnergy;
            public readonly float FinalEnergy;
            public readonly float PeakFoamEnergy;
            public readonly int BreakingEvents;
            public readonly int ActiveSegments;
            public readonly bool ResumedTraveling;

            public BreakingLifecycleProbe(float initialEnergy, float oneSecondEnergy,
                float finalEnergy, float peakFoamEnergy, int breakingEvents,
                int activeSegments, bool resumedTraveling)
            {
                InitialEnergy = initialEnergy;
                OneSecondEnergy = oneSecondEnergy;
                FinalEnergy = finalEnergy;
                PeakFoamEnergy = peakFoamEnergy;
                BreakingEvents = breakingEvents;
                ActiveSegments = activeSegments;
                ResumedTraveling = resumedTraveling;
            }
        }

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

        private static void ValidateSegmentedWave(WaveData wave, SimulationConfig config)
        {
            Require(wave.Segments != null && wave.Segments.Length >= 5,
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
            SimulationConfig firstConfig = CreateRockProbeConfig();
            SimulationConfig secondConfig = CreateRockProbeConfig();
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

        private static SimulationConfig CreateRockProbeConfig()
        {
            return new SimulationConfig
            {
                FixedDeltaTime = 0.2f,
                TargetWaveCount = 0,
                BoatSurfSpeedCap = 36f
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

        private static PerformanceProbe RunPerformanceProbe(int waveCount, int ticks, int seed)
        {
            Vector2 benchmarkHalfExtents = waveCount >= 1000
                ? new Vector2(11200f, 125f)
                : waveCount >= 320 ? new Vector2(3600f, 125f) : new Vector2(225f, 125f);
            var simulation = new WaveSimulation(seed, new SimulationConfig
            {
                TargetWaveCount = waveCount,
                WorldHalfExtents = benchmarkHalfExtents,
                InitialFloatingObjectCount = 0
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
                WaveSegmentData[] segments = wave.Segments;
                for (int segmentIndex = 0; segments != null &&
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

            WaveSegmentData[] segments = simulation.Waves[0].Segments;
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

            WaveSegmentData[] segments = simulation.Waves[0].Segments;
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

        [MenuItem("Wave Prototype/Build Batch 3 Windows")]
        public static void BuildBatch3()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch3/TacticalSailingBatch3.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 4 Windows")]
        public static void BuildBatch4()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch4/TacticalSailingBatch4.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=4: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 5 Windows")]
        public static void BuildBatch5()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch5/TacticalSailingBatch5.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=5: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 6 Windows")]
        public static void BuildBatch6()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch6/TacticalSailingBatch6.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=6: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 7 Windows")]
        public static void BuildBatch7()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch7/TacticalSailingBatch7.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=7: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 8 Windows")]
        public static void BuildBatch8()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch8/TacticalSailingBatch8.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=8: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 9 Windows")]
        public static void BuildBatch9()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch9/TacticalSailingBatch9.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=9: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 10 Windows")]
        public static void BuildBatch10()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch10/TacticalSailingBatch10.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=10: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 11 Windows")]
        public static void BuildBatch11()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch11/TacticalSailingBatch11.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=11: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 12 Windows")]
        public static void BuildBatch12()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch12/TacticalSailingBatch12.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=12: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 13 Windows")]
        public static void BuildBatch13()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch13/TacticalSailingBatch13.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=13: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        // Kept as a stable command-line alias for existing automation.
        public static void BuildWindows() => BuildBatch13();

        private static void EnsureScene()
        {
            Directory.CreateDirectory("Assets/WavePrototype");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[WAVE-VALIDATION] " + message);
        }
    }
}

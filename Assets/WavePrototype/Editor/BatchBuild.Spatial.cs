using System;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        public static void ValidateBatch15SpatialPerformance()
        {
            PerformanceProbe broad320 = RunPerformanceProbe(320, 300, 8520, true);
            PerformanceProbe brute320 = RunPerformanceProbe(320, 300, 8520, false);
            PerformanceProbe broad1000 = RunPerformanceProbe(1000, 120, 8521, true);
            PerformanceProbe brute1000 = RunPerformanceProbe(1000, 120, 8521, false);
            Require(broad320.FinalHash == brute320.FinalHash,
                "320-front spatial/brute performance samples did not end in the same state.");
            Require(broad1000.FinalHash == brute1000.FinalHash,
                "1,000-front spatial/brute performance samples did not end in the same state.");
            Debug.Log($"[WAVE-B15-SPACE-PERF] 320 fronts/300 ticks broad={broad320.CpuSeconds:0.000}s brute={brute320.CpuSeconds:0.000}s hashes={broad320.FinalHash:X16}/{brute320.FinalHash:X16}");
            Debug.Log($"[WAVE-B15-SPACE-PERF] 1000 fronts/120 ticks broad={broad1000.CpuSeconds:0.000}s brute={brute1000.CpuSeconds:0.000}s hashes={broad1000.FinalHash:X16}/{brute1000.FinalHash:X16}");
        }

        private readonly struct SpatialBroadphaseProbe
        {
            public readonly int MatchingTicks;
            public readonly ulong BroadphaseHash;
            public readonly ulong BruteForceHash;
            public readonly long WaveBoatExact;
            public readonly long WaveBoatPotential;
            public readonly long FloatingExact;
            public readonly long FloatingPotential;
            public readonly long RockExact;
            public readonly long RockPotential;
            public readonly int IndexedSections;
            public readonly int OccupiedCells;
            public readonly int Gen0Collections;

            public SpatialBroadphaseProbe(int matchingTicks, ulong broadphaseHash,
                ulong bruteForceHash, long waveBoatExact, long waveBoatPotential,
                long floatingExact, long floatingPotential, long rockExact,
                long rockPotential, int indexedSections, int occupiedCells,
                int gen0Collections)
            {
                MatchingTicks = matchingTicks;
                BroadphaseHash = broadphaseHash;
                BruteForceHash = bruteForceHash;
                WaveBoatExact = waveBoatExact;
                WaveBoatPotential = waveBoatPotential;
                FloatingExact = floatingExact;
                FloatingPotential = floatingPotential;
                RockExact = rockExact;
                RockPotential = rockPotential;
                IndexedSections = indexedSections;
                OccupiedCells = occupiedCells;
                Gen0Collections = gen0Collections;
            }
        }

        private static SpatialBroadphaseProbe RunSpatialBroadphaseProbe()
        {
            const int seed = 8515;
            const int comparisonTicks = 480;
            var broadphase = new WaveSimulation(seed, CreateSpatialProbeConfig(true));
            var bruteForce = new WaveSimulation(seed, CreateSpatialProbeConfig(false));
            broadphase.SetBoatProfile(broadphase.PlayerBoatId, VesselProfileId.MerchantShip);
            bruteForce.SetBoatProfile(bruteForce.PlayerBoatId, VesselProfileId.MerchantShip);

            long waveBoatExact = 0;
            long waveBoatPotential = 0;
            long floatingExact = 0;
            long floatingPotential = 0;
            long rockExact = 0;
            long rockPotential = 0;
            int indexedSections = 0;
            int occupiedCells = 0;
            int matchingTicks = 0;

            for (int step = 0; step < comparisonTicks; step++)
            {
                float throttle = step < 390 ? 1f : -0.2f;
                float steering = Mathf.Sin(step * 0.027f) * 0.72f;
                broadphase.SetPlayerControl(throttle, steering);
                bruteForce.SetPlayerControl(throttle, steering);
                if (step == 120)
                {
                    broadphase.SpawnSwellFront(broadphase.Boats[0].Position, 2.8f);
                    bruteForce.SpawnSwellFront(bruteForce.Boats[0].Position, 2.8f);
                }
                if (step == 260)
                {
                    broadphase.RelocateTarget();
                    bruteForce.RelocateTarget();
                }

                broadphase.Step();
                bruteForce.Step();
                ulong broadHash = broadphase.CalculateStateHash();
                ulong bruteHash = bruteForce.CalculateStateHash();
                if (broadHash != bruteHash) break;
                matchingTicks++;

                SpatialBroadphaseSnapshot spatial = broadphase.SpatialBroadphase;
                SpatialBroadphaseSnapshot brute = bruteForce.SpatialBroadphase;
                waveBoatExact += spatial.WaveBoatExactChecks;
                waveBoatPotential += spatial.WaveBoatPotentialChecks;
                floatingExact += spatial.FloatingWaveExactChecks;
                floatingPotential += spatial.FloatingWavePotentialChecks;
                rockExact += spatial.RockCandidateChecks;
                rockPotential += spatial.RockPotentialChecks;
                indexedSections = Mathf.Max(indexedSections, spatial.IndexedWaveSections);
                occupiedCells = Mathf.Max(occupiedCells, spatial.OccupiedWaveCells);

                Require(brute.WaveBoatExactChecks == brute.WaveBoatPotentialChecks,
                    "Brute-force wave/boat reference path skipped an exact section check.");
                Require(brute.FloatingWaveExactChecks == brute.FloatingWavePotentialChecks,
                    "Brute-force floating-object reference path skipped an exact section check.");
                Require(!brute.Enabled && brute.IndexedWaveSections == 0,
                    "Disabled broadphase unexpectedly built a wave index.");
            }

            ulong finalBroadHash = broadphase.CalculateStateHash();
            ulong finalBruteHash = bruteForce.CalculateStateHash();

            // A separate warmed simulation catches accidental per-tick container churn.
            var allocationProbe = new WaveSimulation(8516, CreateSpatialProbeConfig(true));
            for (int step = 0; step < 180; step++) allocationProbe.Step();
            int collectionStart = GC.CollectionCount(0);
            for (int step = 0; step < 240; step++) allocationProbe.Step();
            int gen0Collections = GC.CollectionCount(0) - collectionStart;

            return new SpatialBroadphaseProbe(matchingTicks, finalBroadHash,
                finalBruteHash, waveBoatExact, waveBoatPotential, floatingExact,
                floatingPotential, rockExact, rockPotential, indexedSections,
                occupiedCells, gen0Collections);
        }

        private static SimulationConfig CreateSpatialProbeConfig(bool enabled)
            => new SimulationConfig
            {
                EnableSpatialBroadphase = enabled,
                SpatialWaveCellSize = 16f,
                TargetWaveCount = 20,
                InitialFloatingObjectCount = 24
            };
    }
}

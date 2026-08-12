using System.Collections.Generic;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        private readonly struct VesselProfileProbe
        {
            public readonly float SkiffMass;
            public readonly float HeavyMass;
            public readonly float SkiffSpeed;
            public readonly float HeavySpeed;
            public readonly float SkiffTurn;
            public readonly float HeavyTurn;
            public readonly int SkiffBroadHits;
            public readonly int HeavyBroadHits;
            public readonly int HeavyCenterHits;
            public readonly int SkiffGroundings;
            public readonly int HeavyGroundings;
            public readonly float SkiffBreakingDamage;
            public readonly float HeavyBreakingDamage;
            public readonly float SkiffBreakingDisplacement;
            public readonly float HeavyBreakingDisplacement;
            public readonly bool Deterministic;

            public VesselProfileProbe(float skiffMass, float heavyMass, float skiffSpeed,
                float heavySpeed, float skiffTurn, float heavyTurn, int skiffBroadHits,
                int heavyBroadHits, int heavyCenterHits, int skiffGroundings,
                int heavyGroundings, float skiffBreakingDamage, float heavyBreakingDamage,
                float skiffBreakingDisplacement, float heavyBreakingDisplacement,
                bool deterministic)
            {
                SkiffMass = skiffMass;
                HeavyMass = heavyMass;
                SkiffSpeed = skiffSpeed;
                HeavySpeed = heavySpeed;
                SkiffTurn = skiffTurn;
                HeavyTurn = heavyTurn;
                SkiffBroadHits = skiffBroadHits;
                HeavyBroadHits = heavyBroadHits;
                HeavyCenterHits = heavyCenterHits;
                SkiffGroundings = skiffGroundings;
                HeavyGroundings = heavyGroundings;
                SkiffBreakingDamage = skiffBreakingDamage;
                HeavyBreakingDamage = heavyBreakingDamage;
                SkiffBreakingDisplacement = skiffBreakingDisplacement;
                HeavyBreakingDisplacement = heavyBreakingDisplacement;
                Deterministic = deterministic;
            }
        }

        private readonly struct MerchantShipProbe
        {
            public readonly float Mass;
            public readonly float Length;
            public readonly float Beam;
            public readonly int Samples;
            public readonly float Speed;
            public readonly float Turn;
            public readonly int BroadWaveHits;
            public readonly int CenterWaveHits;
            public readonly int Groundings;
            public readonly int RockHits;
            public readonly int BowCargoCollections;
            public readonly int SkiffCargoCollections;
            public readonly float BreakingDamage;
            public readonly float BreakingDisplacement;
            public readonly bool Deterministic;

            public MerchantShipProbe(float mass, float length, float beam, int samples,
                float speed, float turn, int broadWaveHits, int centerWaveHits,
                int groundings, int rockHits, int bowCargoCollections,
                int skiffCargoCollections, float breakingDamage,
                float breakingDisplacement, bool deterministic)
            {
                Mass = mass;
                Length = length;
                Beam = beam;
                Samples = samples;
                Speed = speed;
                Turn = turn;
                BroadWaveHits = broadWaveHits;
                CenterWaveHits = centerWaveHits;
                Groundings = groundings;
                RockHits = rockHits;
                BowCargoCollections = bowCargoCollections;
                SkiffCargoCollections = skiffCargoCollections;
                BreakingDamage = breakingDamage;
                BreakingDisplacement = breakingDisplacement;
                Deterministic = deterministic;
            }
        }

        private static VesselProfileProbe RunVesselProfileProbe()
        {
            var skiffHandling = CreateVesselProbe(VesselProfileId.ArcadeSkiff, false);
            var heavyHandling = CreateVesselProbe(VesselProfileId.HeavyCutter, false);
            float skiffMass = skiffHandling.Boats[0].Mass;
            float heavyMass = heavyHandling.Boats[0].Mass;

            for (int tick = 0; tick < 90; tick++)
            {
                skiffHandling.SetPlayerControl(1f, 0f);
                heavyHandling.SetPlayerControl(1f, 0f);
                skiffHandling.Step();
                heavyHandling.Step();
            }
            float skiffSpeed = skiffHandling.Boats[0].Velocity.magnitude;
            float heavySpeed = heavyHandling.Boats[0].Velocity.magnitude;

            skiffHandling.ConfigureBoatForValidation(skiffHandling.PlayerBoatId,
                new Vector2(-100f, -70f), Vector2.right * 5f, 0f);
            heavyHandling.ConfigureBoatForValidation(heavyHandling.PlayerBoatId,
                new Vector2(-100f, -70f), Vector2.right * 5f, 0f);
            for (int tick = 0; tick < 30; tick++)
            {
                skiffHandling.SetPlayerControl(0f, 1f);
                heavyHandling.SetPlayerControl(0f, 1f);
                skiffHandling.Step();
                heavyHandling.Step();
            }
            float skiffTurn = Mathf.Abs(Mathf.DeltaAngle(0f, skiffHandling.Boats[0].Heading));
            float heavyTurn = Mathf.Abs(Mathf.DeltaAngle(0f, heavyHandling.Boats[0].Heading));

            var skiffBroad = CreateVesselProbe(VesselProfileId.ArcadeSkiff, false);
            var heavyBroad = CreateVesselProbe(VesselProfileId.HeavyCutter, false);
            Vector2 broadPosition = new Vector2(-3.25f, -70f);
            skiffBroad.ConfigureBoatForValidation(skiffBroad.PlayerBoatId,
                broadPosition, Vector2.zero, 0f);
            heavyBroad.ConfigureBoatForValidation(heavyBroad.PlayerBoatId,
                broadPosition, Vector2.zero, 0f);
            skiffBroad.SpawnWaveForValidation(new Vector2(0f, -70f), Vector2.right,
                0.7f, 3f, 60f);
            heavyBroad.SpawnWaveForValidation(new Vector2(0f, -70f), Vector2.right,
                0.7f, 3f, 60f);
            skiffBroad.Step();
            heavyBroad.Step();
            int skiffBroadHits = CountPlayerEvents(skiffBroad, SimulationEventType.WaveHitBoat);
            int heavyBroadHits = CountPlayerEvents(heavyBroad, SimulationEventType.WaveHitBoat);

            var heavyCenter = CreateVesselProbe(VesselProfileId.HeavyCutter, false);
            heavyCenter.ConfigureBoatForValidation(heavyCenter.PlayerBoatId,
                new Vector2(0f, -70f), Vector2.zero, 0f);
            heavyCenter.SpawnWaveForValidation(new Vector2(0f, -70f), Vector2.right,
                0.7f, 3f, 60f);
            heavyCenter.Step();
            int heavyCenterHits = CountPlayerEvents(heavyCenter,
                SimulationEventType.WaveHitBoat);

            var skiffGround = CreateVesselProbe(VesselProfileId.ArcadeSkiff, true);
            var heavyGround = CreateVesselProbe(VesselProfileId.HeavyCutter, true);
            Vector2 groundingPosition = new Vector2(-7.8f, 0f);
            skiffGround.ConfigureBoatForValidation(skiffGround.PlayerBoatId,
                groundingPosition, Vector2.zero, 0f);
            heavyGround.ConfigureBoatForValidation(heavyGround.PlayerBoatId,
                groundingPosition, Vector2.zero, 0f);
            skiffGround.Step();
            heavyGround.Step();
            int skiffGroundings = CountPlayerEvents(skiffGround,
                SimulationEventType.BoatGrounded);
            int heavyGroundings = CountPlayerEvents(heavyGround,
                SimulationEventType.BoatGrounded);

            RunVesselBreakingProbe(VesselProfileId.ArcadeSkiff,
                out float skiffDamage, out float skiffDisplacement);
            RunVesselBreakingProbe(VesselProfileId.HeavyCutter,
                out float heavyDamage, out float heavyDisplacement);

            var deterministicA = CreateVesselProbe(VesselProfileId.HeavyCutter, false);
            var deterministicB = CreateVesselProbe(VesselProfileId.HeavyCutter, false);
            bool deterministic = true;
            for (int tick = 0; tick < 120; tick++)
            {
                float steering = Mathf.Sin(tick * 0.037f) * 0.7f;
                deterministicA.SetPlayerControl(1f, steering);
                deterministicB.SetPlayerControl(1f, steering);
                deterministicA.Step();
                deterministicB.Step();
                deterministic &= deterministicA.CalculateStateHash() ==
                    deterministicB.CalculateStateHash();
            }

            return new VesselProfileProbe(skiffMass, heavyMass, skiffSpeed, heavySpeed,
                skiffTurn, heavyTurn, skiffBroadHits, heavyBroadHits, heavyCenterHits,
                skiffGroundings, heavyGroundings, skiffDamage, heavyDamage,
                skiffDisplacement, heavyDisplacement, deterministic);
        }

        private static MerchantShipProbe RunMerchantShipProbe()
        {
            VesselProfileDefinition profile = VesselProfileDefinition.MerchantShip;
            var handling = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            for (int tick = 0; tick < 90; tick++)
            {
                handling.SetPlayerControl(1f, 0f);
                handling.Step();
            }
            float speed = handling.Boats[0].Velocity.magnitude;
            handling.ConfigureBoatForValidation(handling.PlayerBoatId,
                new Vector2(-100f, -70f), Vector2.right * 5f, 0f);
            for (int tick = 0; tick < 30; tick++)
            {
                handling.SetPlayerControl(0f, 1f);
                handling.Step();
            }
            float turn = Mathf.Abs(Mathf.DeltaAngle(0f, handling.Boats[0].Heading));

            var broad = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            broad.ConfigureBoatForValidation(broad.PlayerBoatId,
                new Vector2(-8f, -70f), Vector2.zero, 0f);
            broad.SpawnWaveForValidation(new Vector2(0f, -70f), Vector2.right,
                0.7f, 3f, 60f);
            broad.Step();
            int broadHits = CountPlayerEvents(broad, SimulationEventType.WaveHitBoat);

            var center = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            center.ConfigureBoatForValidation(center.PlayerBoatId,
                new Vector2(0f, -70f), Vector2.zero, 0f);
            center.SpawnWaveForValidation(new Vector2(0f, -70f), Vector2.right,
                0.7f, 3f, 60f);
            center.Step();
            int centerHits = CountPlayerEvents(center, SimulationEventType.WaveHitBoat);

            var ground = CreateVesselProbe(VesselProfileId.MerchantShip, true);
            ground.ConfigureBoatForValidation(ground.PlayerBoatId,
                new Vector2(-13f, 0f), Vector2.zero, 0f);
            ground.Step();
            int groundings = CountPlayerEvents(ground, SimulationEventType.BoatGrounded);

            var rock = new WaveSimulation(8820, new SimulationConfig
            {
                TargetWaveCount = 0,
                InitialFloatingObjectCount = 0
            }, new SingleRockEnvironmentFactory(new RockData(new Vector2(1.3f, 0f), 0.7f)));
            rock.SetBoatProfile(rock.PlayerBoatId, VesselProfileId.MerchantShip);
            rock.ConfigureBoatForValidation(rock.PlayerBoatId,
                new Vector2(-8f, 0f), Vector2.right * 6f, 0f);
            rock.Step();
            int rockHits = CountPlayerEvents(rock, SimulationEventType.BoatHitRock);

            var merchantCargo = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            merchantCargo.ConfigureBoatForValidation(merchantCargo.PlayerBoatId,
                Vector2.zero, Vector2.zero, 0f);
            merchantCargo.SpawnFloatingObject(FloatingObjectKind.Cargo,
                new Vector2(profile.HullLength * 0.46f, 0f));
            merchantCargo.Step();
            int merchantCollections = CountPlayerEvents(merchantCargo,
                SimulationEventType.FloatingObjectCollected);

            var skiffCargo = CreateVesselProbe(VesselProfileId.ArcadeSkiff, false);
            skiffCargo.ConfigureBoatForValidation(skiffCargo.PlayerBoatId,
                Vector2.zero, Vector2.zero, 0f);
            skiffCargo.SpawnFloatingObject(FloatingObjectKind.Cargo,
                new Vector2(profile.HullLength * 0.46f, 0f));
            skiffCargo.Step();
            int skiffCollections = CountPlayerEvents(skiffCargo,
                SimulationEventType.FloatingObjectCollected);

            RunVesselBreakingProbe(VesselProfileId.MerchantShip,
                out float breakingDamage, out float breakingDisplacement);

            var deterministicA = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            var deterministicB = CreateVesselProbe(VesselProfileId.MerchantShip, false);
            bool deterministic = true;
            for (int tick = 0; tick < 180; tick++)
            {
                float steering = Mathf.Sin(tick * 0.031f) * 0.82f;
                deterministicA.SetPlayerControl(1f, steering);
                deterministicB.SetPlayerControl(1f, steering);
                deterministicA.Step();
                deterministicB.Step();
                deterministic &= deterministicA.CalculateStateHash() ==
                    deterministicB.CalculateStateHash();
            }

            return new MerchantShipProbe(profile.Mass, profile.HullLength,
                profile.HullBeam, profile.EffectiveHullSampleCount, speed, turn,
                broadHits, centerHits, groundings, rockHits, merchantCollections,
                skiffCollections, breakingDamage, breakingDisplacement, deterministic);
        }

        private static WaveSimulation CreateVesselProbe(VesselProfileId profile, bool island)
        {
            var simulation = new WaveSimulation(8814, new SimulationConfig
            {
                TargetWaveCount = 0,
                InitialFloatingObjectCount = 0
            }, new SegmentProbeEnvironmentFactory(island));
            Require(simulation.SetBoatProfile(simulation.PlayerBoatId, profile),
                $"Could not select vessel profile {profile} for validation.");
            return simulation;
        }

        private static void RunVesselBreakingProbe(VesselProfileId profile,
            out float damage, out float displacement)
        {
            var simulation = CreateVesselProbe(profile, false);
            Vector2 start = new Vector2(-100f, -70f);
            simulation.ConfigureBoatForValidation(simulation.PlayerBoatId,
                start, Vector2.zero, 0f);
            simulation.SpawnWaveForValidation(start, Vector2.up, 3.2f, 3f, 60f);
            for (int tick = 0; tick < 24; tick++) simulation.Step();
            damage = 100f - simulation.Boats[0].Health;
            displacement = Vector2.Distance(start, simulation.Boats[0].Position);
        }

        private sealed class SingleRockEnvironmentFactory : IOceanEnvironmentFactory
        {
            private readonly RockData rock;
            public SingleRockEnvironmentFactory(RockData rock) { this.rock = rock; }
            public IOceanEnvironment Create(Vector2 worldHalfExtents, int seed)
                => new SingleRockEnvironment(rock);
        }

        private sealed class SingleRockEnvironment : IOceanEnvironment
        {
            private readonly RockData[] rocks;
            public IReadOnlyList<RockData> Rocks => rocks;
            public SingleRockEnvironment(RockData rock) { rocks = new[] { rock }; }
            public float SampleDepth(Vector2 position) => 12f;
            public bool IsLand(Vector2 position) => false;
            public Vector2 SampleDepthGradient(Vector2 position) => Vector2.zero;
            public int FindRock(Vector2 position, float extraRadius)
            {
                float radius = rocks[0].Radius + extraRadius;
                return (position - rocks[0].Position).sqrMagnitude <= radius * radius ? 0 : -1;
            }
        }
    }
}

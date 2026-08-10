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
    }
}

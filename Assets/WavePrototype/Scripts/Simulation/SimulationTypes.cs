using System;
using UnityEngine;

namespace WavePrototype.Simulation
{
    public enum WaveState : byte { Traveling, Breaking, Spent }
    public enum WaveSourceKind : byte { WesternSwell, NorthernCrossSea, SouthernCrossSea }
    public enum FloatingObjectKind : byte { Cargo, Wreckage }
    public enum SimulationEventType : byte
    {
        WaveStartedBreaking, WaveHitRock, WaveHitBoat, BoatDamaged, BoatHitRock, BoatGrounded, WaveExpired,
        TargetVisited, FloatingObjectCollected, BoatHitWreckage, FloatingObjectHitByBreakingWave
    }

    [Serializable]
    public struct WaveSegmentData
    {
        public int Index;
        public Vector2 PreviousPosition;
        public Vector2 Position;
        public Vector2 TravelDirection;
        public float Energy;
        public float Speed;
        public float SampledDepth;
        public Vector2 DepthGradient;
        public float BreakingIntensity;
        public float FoamEnergy;
        public WaveState State;
        public bool Active;
    }

    [Serializable]
    public struct WaveData
    {
        public int Id;
        public int SourceId;
        public int SwellSystemId;
        public Vector2 Position;
        public Vector2 TravelDirection;
        public float Energy;
        public float Speed;
        public float PacketLength;
        public float CrestLength;
        public WaveState State;
        public WaveSegmentData[] Segments;
    }

    [Serializable]
    public struct WaveSourceData
    {
        public int Id;
        public WaveSourceKind Kind;
        public bool Enabled;
        public Vector2 SegmentStart;
        public Vector2 SegmentEnd;
        public Vector2 Direction;
        public float DirectionSpreadDegrees;
        public float SelectionWeight;
        public float MinimumEnergy;
        public float MaximumEnergy;
        public float MinimumSpacing;
        public float MaximumSpacing;
        public int MinimumPackets;
        public int MaximumPackets;
        public int SpawnedTrains;
        public int SpawnedPackets;
        public int SpawnedSystems;
        public float MinimumCalmSeconds;
        public float MaximumCalmSeconds;
        public ulong NextEmissionTick;
    }

    [Serializable]
    public struct SwellSystemData
    {
        public int Id;
        public int SourceId;
        public Vector2 Direction;
        public float BaseEnergy;
        public float PacketSpacing;
        public float MeanPacketLength;
        public float MeanCrestLength;
        public float CalmGapSeconds;
        public int InitialPacketCount;
        public int EmittedPacketCount;
        public int ActivePacketCount;
        public ulong BornTick;
    }

    [Serializable]
    public struct BoatData
    {
        public int Id;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Heading;
        public float Health;
        public float Mass;
    }

    [Serializable]
    public struct FloatingObjectData
    {
        public int Id;
        public FloatingObjectKind Kind;
        public Vector2 PreviousPosition;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Value;
        public int LastBreakingWaveId;
        public bool Active;
    }

    public readonly struct WaveDerived
    {
        public readonly float Amplitude;
        public readonly float Steepness;
        public readonly float Force;

        public WaveDerived(float energy, float depth, float packetLength)
        {
            float safeDepth = EffectiveDepth(depth, packetLength);
            Amplitude = Mathf.Sqrt(Mathf.Max(0f, energy)) * (1f + 0.45f / safeDepth);
            Steepness = Amplitude / Mathf.Max(0.25f, packetLength);
            Force = energy * (1f + 0.7f / safeDepth);
        }

        public static float EffectiveDepth(float sampledDepth, float packetLength)
        {
            // Deeper seabed stops affecting a packet once it is sufficiently deep relative
            // to that packet's scale. This keeps abyssal bathymetry mechanically irrelevant.
            float influenceLimit = Mathf.Max(6.5f, packetLength * 1.75f);
            return Mathf.Clamp(sampledDepth, 0.35f, influenceLimit);
        }
    }

    public readonly struct SimulationEvent
    {
        public readonly SimulationEventType Type;
        public readonly int WaveId;
        public readonly int BoatId;
        public readonly Vector2 Position;
        public readonly float Magnitude;
        public readonly int SegmentIndex;
        public readonly int ObjectId;

        public SimulationEvent(SimulationEventType type, int waveId, int boatId, Vector2 position,
            float magnitude, int segmentIndex = -1, int objectId = 0)
        {
            Type = type;
            WaveId = waveId;
            BoatId = boatId;
            Position = position;
            Magnitude = magnitude;
            SegmentIndex = segmentIndex;
            ObjectId = objectId;
        }
    }

    public readonly struct BoatControl
    {
        public readonly float Throttle;
        public readonly float Steering;

        public BoatControl(float throttle, float steering)
        {
            Throttle = Mathf.Clamp(throttle, -0.35f, 1f);
            Steering = Mathf.Clamp(steering, -1f, 1f);
        }
    }

    public readonly struct BoatControlCommand
    {
        public readonly ulong Tick;
        public readonly int BoatId;
        public readonly BoatControl Control;

        public BoatControlCommand(ulong tick, int boatId, BoatControl control)
        {
            Tick = tick;
            BoatId = boatId;
            Control = control;
        }
    }

    public readonly struct WaveDensitySample
    {
        public readonly int WorldCount;
        public readonly int LocalCount;
        public readonly float Radius;
        public readonly int DesiredVisibleCount;

        public WaveDensitySample(int worldCount, int localCount, float radius, int desiredVisibleCount)
        {
            WorldCount = worldCount;
            LocalCount = localCount;
            Radius = radius;
            DesiredVisibleCount = desiredVisibleCount;
        }
    }

    [Serializable]
    public struct TargetMarkerData
    {
        public Vector2 Position;
        public float VisitRadius;
        public int VisitCount;
        public int RelocationCount;
        public bool Enabled;
    }

    public sealed class SimulationConfig
    {
        public float FixedDeltaTime = 1f / 30f;
        public Vector2 WorldHalfExtents = new Vector2(225f, 125f);
        public float BaseWaveSpeed = 8.6f;
        public float EnergyDecayPerSecond = 0.012f;
        public float BreakingMinimumEnergyLossPerSecond = 0.14f;
        public float BreakingEnergyLossPerSecond = 0.78f;
        public float BreakingIntensityAttackPerSecond = 4.2f;
        public float BreakingIntensityRecoveryPerSecond = 0.9f;
        public float BreakingReleaseIntensity = 0.08f;
        public float BreakingEnergyToFoam = 0.72f;
        public float FoamEnergyLossPerSecond = 0.75f;
        public float MinimumFoamEnergy = 0.018f;
        public float SpentEnergyLossPerSecond = 3.2f;
        public float MinimumEnergy = 0.06f;
        public float BreakingSteepness = 0.34f;
        public float DepthLimitedBreakingRatio = 0.3f;
        public float BoatInteractionRadius = 2.35f;
        public float RockInteractionRadius = 1.15f;
        public float BoatLinearDrag = 0.135f;
        public float BoatLateralDrag = 0.72f;
        public float RockEnergyAbsorption = 0.42f;
        public float WaveBoatForceScale = 15.5f;
        public float BreakingImpactMultiplier = 2.15f;
        public float WaveYawScale = 25f;
        public float TravelingImpactMultiplier = 0.38f;
        public float TravelingLongitudinalScale = 0.3f;
        public float TravelingLongitudinalPadding = 0.85f;
        public float TravelingCarrySpeedFraction = 0.72f;
        public float TravelingYawMultiplier = 0.58f;
        public float WaveRefractionStrength = 0.16f;
        public float WaveShoalingDeceleration = 7f;
        public float WaveDeepRecovery = 0.72f;
        public float WaveSegmentTargetSpacing = 13.5f;
        public int WaveMaximumSegments = 20;
        public int WaveEnvironmentSampleInterval = 4;
        public float WaveSegmentDirectionCoherence = 1.4f;
        public float WaveSegmentPositionCoherence = 1.15f;
        public float WaveSegmentLinkBreakMultiplier = 1.9f;
        public float WaveMinimumActiveSegmentFraction = 0.45f;
        public float WindSpeed = 7.5f;
        public Vector2 WindDirection = new Vector2(0.94f, 0.342f);
        public float SailingForce = 31f;
        public float BoatCruiseSpeed = 12.5f;
        public float BoatSurfSpeedCap = 18f;
        public float BoatCruisePropulsionFadeRange = 0.9f;
        public float BoatSurfExcessDecay = 0.22f;
        public float BoatCollisionRadius = 0.72f;
        public float RockImpactRestitution = 0.14f;
        public float RockTangentialRetention = 0.82f;
        public float RockContactSkin = 0.025f;
        public float BoatTurnRate = 72f;
        public int TargetWaveCount = 20;
        public int DesiredVisibleWaveCount = 7;
        public float DefaultTargetVisitRadius = 5f;
        public float TargetSafeClearance = 4.5f;
        public float TargetMinimumRelocationDistance = 36f;
        public int InitialFloatingObjectCount = 24;
        public float FloatingObjectWaveResponse = 0.42f;
        public float FloatingObjectDrag = 0.34f;
        public float CargoCollectionRadius = 1.15f;
        public float WreckageBoatForce = 17f;
        public float BreakingFloatingObjectImpulse = 2.15f;
        public float WreckageInertiaScale = 1.65f;
        public float FloatingObjectMaximumSpeed = 9f;

        // Kept as a source-compatible alias while presentation and external probes migrate
        // to the intentionally distinct cruise and surf limits.
        public float MaximumBoatSpeed
        {
            get => BoatCruiseSpeed;
            set => BoatCruiseSpeed = value;
        }
    }
}

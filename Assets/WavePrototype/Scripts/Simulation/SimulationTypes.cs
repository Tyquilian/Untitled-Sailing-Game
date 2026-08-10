using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private WaveSegmentData[] segments;

        /// <summary>
        /// Read-only segment state for presentation, diagnostics, and external callers.
        /// Elements are value copies and the backing authoritative array is never exposed.
        /// </summary>
        public WaveSegmentCollection Segments => new WaveSegmentCollection(segments);

        internal WaveSegmentData[] MutableSegments
        {
            get => segments;
            set => segments = value;
        }
    }

    /// <summary>
    /// Allocation-free read-only view over a wave's authoritative segment storage.
    /// </summary>
    public readonly struct WaveSegmentCollection : IReadOnlyList<WaveSegmentData>
    {
        private readonly WaveSegmentData[] segments;

        internal WaveSegmentCollection(WaveSegmentData[] segments)
        {
            this.segments = segments;
        }

        public int Count => segments == null ? 0 : segments.Length;
        public int Length => Count;
        public WaveSegmentData this[int index] => segments[index];
        public Enumerator GetEnumerator() => new Enumerator(segments);
        IEnumerator<WaveSegmentData> IEnumerable<WaveSegmentData>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<WaveSegmentData>
        {
            private readonly WaveSegmentData[] segments;
            private int index;

            internal Enumerator(WaveSegmentData[] segments)
            {
                this.segments = segments;
                index = -1;
            }

            public WaveSegmentData Current => segments[index];
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int next = index + 1;
                if (segments == null || next >= segments.Length) return false;
                index = next;
                return true;
            }

            public void Reset() => index = -1;
            public void Dispose() { }
        }
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
        public VesselProfileId Profile;
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
        // WorldCount is authoritative parent-front population. LocalCount counts parent
        // fronts with at least one active crest section intersecting the sampled view.
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

    public readonly struct SpatialBroadphaseSnapshot
    {
        public readonly bool Enabled;
        public readonly int IndexedWaveSections;
        public readonly int OccupiedWaveCells;
        public readonly int WaveQueries;
        public readonly int WaveCandidates;
        public readonly int WaveBoatExactChecks;
        public readonly int WaveBoatPotentialChecks;
        public readonly int FloatingWaveExactChecks;
        public readonly int FloatingWavePotentialChecks;
        public readonly int RockQueries;
        public readonly int RockCandidateChecks;
        public readonly int RockPotentialChecks;

        public SpatialBroadphaseSnapshot(bool enabled, int indexedWaveSections,
            int occupiedWaveCells, int waveQueries, int waveCandidates,
            int waveBoatExactChecks, int waveBoatPotentialChecks,
            int floatingWaveExactChecks, int floatingWavePotentialChecks,
            int rockQueries, int rockCandidateChecks, int rockPotentialChecks)
        {
            Enabled = enabled;
            IndexedWaveSections = indexedWaveSections;
            OccupiedWaveCells = occupiedWaveCells;
            WaveQueries = waveQueries;
            WaveCandidates = waveCandidates;
            WaveBoatExactChecks = waveBoatExactChecks;
            WaveBoatPotentialChecks = waveBoatPotentialChecks;
            FloatingWaveExactChecks = floatingWaveExactChecks;
            FloatingWavePotentialChecks = floatingWavePotentialChecks;
            RockQueries = rockQueries;
            RockCandidateChecks = rockCandidateChecks;
            RockPotentialChecks = rockPotentialChecks;
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
        public bool RecordBoatControlHistory = true;
        public int MaximumRecordedBoatControls = 65536;
        public int PendingInputCompactionThreshold = 1024;
        public VesselProfileDefinition ArcadeSkiffProfile = VesselProfileDefinition.ArcadeSkiff;
        public VesselProfileDefinition HeavyCutterProfile = VesselProfileDefinition.HeavyCutter;
        public bool EnableSpatialBroadphase = true;
        public float SpatialWaveCellSize = 16f;

        // Named balance values that were previously embedded in system equations.
        public float WaveFollowingThrustScale = 5.2f;
        public float WaveHeadOnDampingScale = 1.45f;
        public float BreakingBoatDamageThreshold = 0.35f;
        public float BreakingBoatDamageScale = 5.2f;
        public float BoatReverseBrakeScale = 3.4f;
        public float BoatReversePropulsionScale = 0.18f;
        public float BoatMinimumTurnAuthority = 0.32f;
        public float BoatFullTurnAuthoritySpeed = 5f;
        public float GroundingBaseDamage = 0.12f;
        public float GroundingSpeedDamageScale = 0.16f;
        public float GroundingBounce = 0.08f;
        public float RockBaseDamage = 0.22f;
        public float RockSpeedDamageScale = 0.34f;

        // Kept as a source-compatible alias while presentation and external probes migrate
        // to the intentionally distinct cruise and surf limits.
        public float MaximumBoatSpeed
        {
            get => BoatCruiseSpeed;
            set => BoatCruiseSpeed = value;
        }

        internal SimulationConfig Clone() => (SimulationConfig)MemberwiseClone();

        internal VesselProfileDefinition GetVesselProfile(VesselProfileId id)
            => id == VesselProfileId.HeavyCutter ? HeavyCutterProfile : ArcadeSkiffProfile;
    }

    /// <summary>
    /// Immutable startup snapshot exposed by <see cref="WaveSimulation"/>. Runtime systems
    /// receive a private cloned configuration, preventing partially applied live edits.
    /// </summary>
    public sealed class SimulationConfigSnapshot
    {
        public readonly float FixedDeltaTime;
        public readonly Vector2 WorldHalfExtents;
        public readonly float BaseWaveSpeed;
        public readonly float EnergyDecayPerSecond;
        public readonly float BreakingMinimumEnergyLossPerSecond;
        public readonly float BreakingEnergyLossPerSecond;
        public readonly float BreakingIntensityAttackPerSecond;
        public readonly float BreakingIntensityRecoveryPerSecond;
        public readonly float BreakingReleaseIntensity;
        public readonly float BreakingEnergyToFoam;
        public readonly float FoamEnergyLossPerSecond;
        public readonly float MinimumFoamEnergy;
        public readonly float SpentEnergyLossPerSecond;
        public readonly float MinimumEnergy;
        public readonly float BreakingSteepness;
        public readonly float DepthLimitedBreakingRatio;
        public readonly float BoatInteractionRadius;
        public readonly float RockInteractionRadius;
        public readonly float BoatLinearDrag;
        public readonly float BoatLateralDrag;
        public readonly float RockEnergyAbsorption;
        public readonly float WaveBoatForceScale;
        public readonly float BreakingImpactMultiplier;
        public readonly float WaveYawScale;
        public readonly float TravelingImpactMultiplier;
        public readonly float TravelingLongitudinalScale;
        public readonly float TravelingLongitudinalPadding;
        public readonly float TravelingCarrySpeedFraction;
        public readonly float TravelingYawMultiplier;
        public readonly float WaveRefractionStrength;
        public readonly float WaveShoalingDeceleration;
        public readonly float WaveDeepRecovery;
        public readonly float WaveSegmentTargetSpacing;
        public readonly int WaveMaximumSegments;
        public readonly int WaveEnvironmentSampleInterval;
        public readonly float WaveSegmentDirectionCoherence;
        public readonly float WaveSegmentPositionCoherence;
        public readonly float WaveSegmentLinkBreakMultiplier;
        public readonly float WaveMinimumActiveSegmentFraction;
        public readonly float WindSpeed;
        public readonly Vector2 WindDirection;
        public readonly float SailingForce;
        public readonly float BoatCruiseSpeed;
        public readonly float BoatSurfSpeedCap;
        public readonly float BoatCruisePropulsionFadeRange;
        public readonly float BoatSurfExcessDecay;
        public readonly float BoatCollisionRadius;
        public readonly float RockImpactRestitution;
        public readonly float RockTangentialRetention;
        public readonly float RockContactSkin;
        public readonly float BoatTurnRate;
        public readonly int TargetWaveCount;
        public readonly int DesiredVisibleWaveCount;
        public readonly float DefaultTargetVisitRadius;
        public readonly float TargetSafeClearance;
        public readonly float TargetMinimumRelocationDistance;
        public readonly int InitialFloatingObjectCount;
        public readonly float FloatingObjectWaveResponse;
        public readonly float FloatingObjectDrag;
        public readonly float CargoCollectionRadius;
        public readonly float WreckageBoatForce;
        public readonly float BreakingFloatingObjectImpulse;
        public readonly float WreckageInertiaScale;
        public readonly float FloatingObjectMaximumSpeed;
        public readonly bool RecordBoatControlHistory;
        public readonly int MaximumRecordedBoatControls;
        public readonly int PendingInputCompactionThreshold;
        public readonly VesselProfileDefinition ArcadeSkiffProfile;
        public readonly VesselProfileDefinition HeavyCutterProfile;
        public readonly bool EnableSpatialBroadphase;
        public readonly float SpatialWaveCellSize;
        public readonly float WaveFollowingThrustScale;
        public readonly float WaveHeadOnDampingScale;
        public readonly float BreakingBoatDamageThreshold;
        public readonly float BreakingBoatDamageScale;
        public readonly float BoatReverseBrakeScale;
        public readonly float BoatReversePropulsionScale;
        public readonly float BoatMinimumTurnAuthority;
        public readonly float BoatFullTurnAuthoritySpeed;
        public readonly float GroundingBaseDamage;
        public readonly float GroundingSpeedDamageScale;
        public readonly float GroundingBounce;
        public readonly float RockBaseDamage;
        public readonly float RockSpeedDamageScale;
        public float MaximumBoatSpeed => BoatCruiseSpeed;

        internal SimulationConfigSnapshot(SimulationConfig source)
        {
            FixedDeltaTime = source.FixedDeltaTime;
            WorldHalfExtents = source.WorldHalfExtents;
            BaseWaveSpeed = source.BaseWaveSpeed;
            EnergyDecayPerSecond = source.EnergyDecayPerSecond;
            BreakingMinimumEnergyLossPerSecond = source.BreakingMinimumEnergyLossPerSecond;
            BreakingEnergyLossPerSecond = source.BreakingEnergyLossPerSecond;
            BreakingIntensityAttackPerSecond = source.BreakingIntensityAttackPerSecond;
            BreakingIntensityRecoveryPerSecond = source.BreakingIntensityRecoveryPerSecond;
            BreakingReleaseIntensity = source.BreakingReleaseIntensity;
            BreakingEnergyToFoam = source.BreakingEnergyToFoam;
            FoamEnergyLossPerSecond = source.FoamEnergyLossPerSecond;
            MinimumFoamEnergy = source.MinimumFoamEnergy;
            SpentEnergyLossPerSecond = source.SpentEnergyLossPerSecond;
            MinimumEnergy = source.MinimumEnergy;
            BreakingSteepness = source.BreakingSteepness;
            DepthLimitedBreakingRatio = source.DepthLimitedBreakingRatio;
            BoatInteractionRadius = source.BoatInteractionRadius;
            RockInteractionRadius = source.RockInteractionRadius;
            BoatLinearDrag = source.BoatLinearDrag;
            BoatLateralDrag = source.BoatLateralDrag;
            RockEnergyAbsorption = source.RockEnergyAbsorption;
            WaveBoatForceScale = source.WaveBoatForceScale;
            BreakingImpactMultiplier = source.BreakingImpactMultiplier;
            WaveYawScale = source.WaveYawScale;
            TravelingImpactMultiplier = source.TravelingImpactMultiplier;
            TravelingLongitudinalScale = source.TravelingLongitudinalScale;
            TravelingLongitudinalPadding = source.TravelingLongitudinalPadding;
            TravelingCarrySpeedFraction = source.TravelingCarrySpeedFraction;
            TravelingYawMultiplier = source.TravelingYawMultiplier;
            WaveRefractionStrength = source.WaveRefractionStrength;
            WaveShoalingDeceleration = source.WaveShoalingDeceleration;
            WaveDeepRecovery = source.WaveDeepRecovery;
            WaveSegmentTargetSpacing = source.WaveSegmentTargetSpacing;
            WaveMaximumSegments = source.WaveMaximumSegments;
            WaveEnvironmentSampleInterval = source.WaveEnvironmentSampleInterval;
            WaveSegmentDirectionCoherence = source.WaveSegmentDirectionCoherence;
            WaveSegmentPositionCoherence = source.WaveSegmentPositionCoherence;
            WaveSegmentLinkBreakMultiplier = source.WaveSegmentLinkBreakMultiplier;
            WaveMinimumActiveSegmentFraction = source.WaveMinimumActiveSegmentFraction;
            WindSpeed = source.WindSpeed;
            WindDirection = source.WindDirection;
            SailingForce = source.SailingForce;
            BoatCruiseSpeed = source.BoatCruiseSpeed;
            BoatSurfSpeedCap = source.BoatSurfSpeedCap;
            BoatCruisePropulsionFadeRange = source.BoatCruisePropulsionFadeRange;
            BoatSurfExcessDecay = source.BoatSurfExcessDecay;
            BoatCollisionRadius = source.BoatCollisionRadius;
            RockImpactRestitution = source.RockImpactRestitution;
            RockTangentialRetention = source.RockTangentialRetention;
            RockContactSkin = source.RockContactSkin;
            BoatTurnRate = source.BoatTurnRate;
            TargetWaveCount = source.TargetWaveCount;
            DesiredVisibleWaveCount = source.DesiredVisibleWaveCount;
            DefaultTargetVisitRadius = source.DefaultTargetVisitRadius;
            TargetSafeClearance = source.TargetSafeClearance;
            TargetMinimumRelocationDistance = source.TargetMinimumRelocationDistance;
            InitialFloatingObjectCount = source.InitialFloatingObjectCount;
            FloatingObjectWaveResponse = source.FloatingObjectWaveResponse;
            FloatingObjectDrag = source.FloatingObjectDrag;
            CargoCollectionRadius = source.CargoCollectionRadius;
            WreckageBoatForce = source.WreckageBoatForce;
            BreakingFloatingObjectImpulse = source.BreakingFloatingObjectImpulse;
            WreckageInertiaScale = source.WreckageInertiaScale;
            FloatingObjectMaximumSpeed = source.FloatingObjectMaximumSpeed;
            RecordBoatControlHistory = source.RecordBoatControlHistory;
            MaximumRecordedBoatControls = source.MaximumRecordedBoatControls;
            PendingInputCompactionThreshold = source.PendingInputCompactionThreshold;
            ArcadeSkiffProfile = source.ArcadeSkiffProfile;
            HeavyCutterProfile = source.HeavyCutterProfile;
            EnableSpatialBroadphase = source.EnableSpatialBroadphase;
            SpatialWaveCellSize = source.SpatialWaveCellSize;
            WaveFollowingThrustScale = source.WaveFollowingThrustScale;
            WaveHeadOnDampingScale = source.WaveHeadOnDampingScale;
            BreakingBoatDamageThreshold = source.BreakingBoatDamageThreshold;
            BreakingBoatDamageScale = source.BreakingBoatDamageScale;
            BoatReverseBrakeScale = source.BoatReverseBrakeScale;
            BoatReversePropulsionScale = source.BoatReversePropulsionScale;
            BoatMinimumTurnAuthority = source.BoatMinimumTurnAuthority;
            BoatFullTurnAuthoritySpeed = source.BoatFullTurnAuthoritySpeed;
            GroundingBaseDamage = source.GroundingBaseDamage;
            GroundingSpeedDamageScale = source.GroundingSpeedDamageScale;
            GroundingBounce = source.GroundingBounce;
            RockBaseDamage = source.RockBaseDamage;
            RockSpeedDamageScale = source.RockSpeedDamageScale;
        }

        public VesselProfileDefinition GetVesselProfile(VesselProfileId id)
            => id == VesselProfileId.HeavyCutter ? HeavyCutterProfile : ArcadeSkiffProfile;
    }
}

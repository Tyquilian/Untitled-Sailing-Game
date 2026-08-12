using System;
using UnityEngine;

namespace WavePrototype.Simulation
{
    public enum VesselProfileId : byte
    {
        ArcadeSkiff,
        HeavyCutter,
        MerchantShip
    }

    /// <summary>
    /// Immutable physical and handling definition selected by a boat's profile identity.
    /// Profile values are copied into the simulation's immutable startup configuration.
    /// </summary>
    [Serializable]
    public readonly struct VesselProfileDefinition
    {
        public readonly VesselProfileId Id;
        public readonly float Mass;
        public readonly float HullLength;
        public readonly float HullBeam;
        public readonly float CollisionRadius;
        public readonly float RockContactRadius;
        public readonly int HullSampleCount;
        public readonly float PropulsionScale;
        public readonly float TurnRateScale;
        public readonly float CruiseSpeedScale;
        public readonly float SurfSpeedScale;
        public readonly float LinearDragScale;
        public readonly float LateralDragScale;
        public readonly float WaveForceScale;
        public readonly float WaveYawScale;
        public readonly float DamageTakenScale;

        public VesselProfileDefinition(VesselProfileId id, float mass, float hullLength,
            float hullBeam, float collisionRadius, float rockContactRadius,
            int hullSampleCount, float propulsionScale, float turnRateScale,
            float cruiseSpeedScale, float surfSpeedScale, float linearDragScale,
            float lateralDragScale, float waveForceScale, float waveYawScale,
            float damageTakenScale)
        {
            Id = id;
            Mass = mass;
            HullLength = hullLength;
            HullBeam = hullBeam;
            CollisionRadius = collisionRadius;
            RockContactRadius = rockContactRadius;
            HullSampleCount = hullSampleCount;
            PropulsionScale = propulsionScale;
            TurnRateScale = turnRateScale;
            CruiseSpeedScale = cruiseSpeedScale;
            SurfSpeedScale = surfSpeedScale;
            LinearDragScale = linearDragScale;
            LateralDragScale = lateralDragScale;
            WaveForceScale = waveForceScale;
            WaveYawScale = waveYawScale;
            DamageTakenScale = damageTakenScale;
        }

        /// <summary>
        /// Returns a local hull sample as (forward, starboard) coordinates. Sampling stays
        /// allocation-free and the first sample is always the vessel center.
        /// </summary>
        public Vector2 GetHullSampleOffset(int index)
        {
            if (index <= 0 || EffectiveHullSampleCount <= 1) return Vector2.zero;
            if (EffectiveHullSampleCount > 5)
            {
                switch (index)
                {
                    case 1: return new Vector2(HullLength * 0.46f, 0f);
                    case 2: return new Vector2(-HullLength * 0.42f, 0f);
                    case 3: return new Vector2(HullLength * 0.28f, HullBeam * 0.42f);
                    case 4: return new Vector2(HullLength * 0.28f, -HullBeam * 0.42f);
                    case 5: return new Vector2(0f, HullBeam * 0.48f);
                    case 6: return new Vector2(0f, -HullBeam * 0.48f);
                    case 7: return new Vector2(-HullLength * 0.28f, HullBeam * 0.42f);
                    case 8: return new Vector2(-HullLength * 0.28f, -HullBeam * 0.42f);
                    case 9: return new Vector2(HullLength * 0.43f, HullBeam * 0.24f);
                    case 10: return new Vector2(HullLength * 0.43f, -HullBeam * 0.24f);
                    case 11: return new Vector2(-HullLength * 0.40f, HullBeam * 0.24f);
                    case 12: return new Vector2(-HullLength * 0.40f, -HullBeam * 0.24f);
                    default: return Vector2.zero;
                }
            }
            switch (index)
            {
                case 1: return new Vector2(HullLength * 0.38f, 0f);
                case 2: return new Vector2(-HullLength * 0.38f, 0f);
                case 3: return new Vector2(0f, HullBeam * 0.38f);
                case 4: return new Vector2(0f, -HullBeam * 0.38f);
                default: return Vector2.zero;
            }
        }

        public int EffectiveHullSampleCount => Mathf.Clamp(HullSampleCount, 1, 13);

        public float MaximumHullSampleDistance
        {
            get
            {
                float maximum = 0f;
                for (int sample = 0; sample < EffectiveHullSampleCount; sample++)
                    maximum = Mathf.Max(maximum, GetHullSampleOffset(sample).magnitude);
                return maximum;
            }
        }

        public static VesselProfileDefinition ArcadeSkiff => new VesselProfileDefinition(
            VesselProfileId.ArcadeSkiff,
            mass: 7.2f,
            hullLength: 2.95f,
            hullBeam: 1.64f,
            collisionRadius: 0.72f,
            rockContactRadius: 0.72f,
            hullSampleCount: 1,
            propulsionScale: 1f,
            turnRateScale: 1f,
            cruiseSpeedScale: 1f,
            surfSpeedScale: 1f,
            linearDragScale: 1f,
            lateralDragScale: 1f,
            waveForceScale: 1f,
            waveYawScale: 1f,
            damageTakenScale: 1f);

        public static VesselProfileDefinition HeavyCutter => new VesselProfileDefinition(
            VesselProfileId.HeavyCutter,
            mass: 24f,
            hullLength: 6.2f,
            hullBeam: 2.8f,
            collisionRadius: 1.5f,
            rockContactRadius: 0.82f,
            hullSampleCount: 5,
            propulsionScale: 2.3f,
            turnRateScale: 0.55f,
            cruiseSpeedScale: 0.82f,
            surfSpeedScale: 0.74f,
            linearDragScale: 0.82f,
            lateralDragScale: 1.28f,
            waveForceScale: 1.8f,
            waveYawScale: 0.45f,
            damageTakenScale: 0.48f);

        public static VesselProfileDefinition MerchantShip => new VesselProfileDefinition(
            VesselProfileId.MerchantShip,
            mass: 96f,
            hullLength: 16.5f,
            hullBeam: 5.2f,
            collisionRadius: 2.6f,
            rockContactRadius: 0.92f,
            hullSampleCount: 13,
            propulsionScale: 7.4f,
            turnRateScale: 0.3f,
            cruiseSpeedScale: 0.68f,
            surfSpeedScale: 0.58f,
            linearDragScale: 0.7f,
            lateralDragScale: 1.58f,
            waveForceScale: 4.2f,
            waveYawScale: 0.22f,
            damageTakenScale: 0.25f);
    }

    public static class VesselProfiles
    {
        public static Vector2 GetHullSampleWorldPosition(BoatData boat,
            VesselProfileDefinition profile, int sampleIndex)
        {
            Vector2 local = profile.GetHullSampleOffset(sampleIndex);
            Vector2 forward = SimulationMath.HeadingVector(boat.Heading);
            Vector2 side = new Vector2(-forward.y, forward.x);
            return boat.Position + forward * local.x + side * local.y;
        }

        public static string GetLabel(VesselProfileId id)
        {
            switch (id)
            {
                case VesselProfileId.MerchantShip: return "MERCHANT SHIP";
                case VesselProfileId.HeavyCutter: return "HEAVY CUTTER";
                default: return "ARCADE SKIFF";
            }
        }
    }
}

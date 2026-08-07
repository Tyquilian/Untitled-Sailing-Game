using UnityEngine;

namespace WavePrototype.Simulation
{
    internal struct WaveSegmentDecision
    {
        public Vector2 Position;
        public Vector2 Direction;
        public Vector2 CoherentDirection;
        public float Speed;
        public float Energy;
        public float SampledDepth;
        public Vector2 DepthGradient;
        public float BreakingIntensity;
        public float FoamEnergy;
        public float InteractionForce;
        public WaveState State;
        public bool Active;
    }

    internal struct WaveDecision
    {
        public int WaveId;
        public Vector2 Position;
        public Vector2 Direction;
        public float Speed;
        public float Energy;
        public float InteractionForce;
        public WaveState State;
        public bool Expired;
        public int ActiveSegmentCount;
        public WaveSegmentDecision[] Segments;
    }

    internal struct BoatDecision
    {
        public Vector2 Force;
        public float HeadingImpulse;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Heading;
        public float Damage;
        public SimulationEventType Collision;
    }

    internal struct FloatingObjectDecision
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int LastBreakingWaveId;
        public bool Active;
    }

    internal static class SimulationMath
    {
        public static Vector2 HeadingVector(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

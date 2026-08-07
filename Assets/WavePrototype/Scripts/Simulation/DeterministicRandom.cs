using UnityEngine;

namespace WavePrototype.Simulation
{
    public struct DeterministicRandom
    {
        private uint state;
        public uint State => state;
        public DeterministicRandom(int seed) { state = (uint)(seed == 0 ? 1 : seed); }
        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x;
            return x;
        }
        public float Value() => (NextUInt() & 0x00FFFFFF) / 16777216f;
        public float Range(float min, float max) => min + (max - min) * Value();
        public Vector2 InsideUnitCircle()
        {
            float a = Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Value());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
    }
}

using System.Collections.Generic;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    /// <summary>
    /// Owns bounded presentation diagnostics. None of this state participates in simulation.
    /// </summary>
    internal sealed class PrototypeDiagnostics
    {
        private const int FrameWindow = 240;
        private readonly float[] frameSamples = new float[FrameWindow];
        private readonly Queue<string> eventLog = new Queue<string>(6);
        private int frameSampleCount;
        private ulong cachedStateHash;
        private ulong cachedHashTick = ulong.MaxValue;
        private float nextHashRefreshTime;

        public float AverageFrameMilliseconds { get; private set; }
        public float MaximumFrameMilliseconds { get; private set; }
        public IEnumerable<string> EventLog => eventLog;

        public void PushLog(float simulatedTime, string message)
        {
            eventLog.Enqueue($"{simulatedTime,6:0.0}s  {message}");
            while (eventLog.Count > 6) eventLog.Dequeue();
        }

        public void ClearLog() => eventLog.Clear();

        public void RecordFrame(float milliseconds)
        {
            frameSamples[frameSampleCount++] = milliseconds;
            if (frameSampleCount < frameSamples.Length) return;
            float total = 0f;
            float maximum = 0f;
            for (int i = 0; i < frameSamples.Length; i++)
            {
                total += frameSamples[i];
                maximum = UnityEngine.Mathf.Max(maximum, frameSamples[i]);
            }
            AverageFrameMilliseconds = total / frameSamples.Length;
            MaximumFrameMilliseconds = maximum;
            frameSampleCount = 0;
        }

        public void InvalidateHash()
        {
            cachedHashTick = ulong.MaxValue;
            nextHashRefreshTime = 0f;
        }

        public ulong GetStateHash(WaveSimulation simulation, float unscaledTime)
        {
            if (cachedHashTick == ulong.MaxValue ||
                (cachedHashTick != simulation.Tick && unscaledTime >= nextHashRefreshTime))
            {
                cachedStateHash = simulation.CalculateStateHash();
                cachedHashTick = simulation.Tick;
                nextHashRefreshTime = unscaledTime + 0.25f;
            }
            return cachedStateHash;
        }
    }
}

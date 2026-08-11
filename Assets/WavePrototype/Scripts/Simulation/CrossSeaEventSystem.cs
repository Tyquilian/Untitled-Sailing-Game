using UnityEngine;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Owns one bounded secondary-swell lifecycle. It authorizes a dormant boundary source,
    /// shapes its emission strength, then leaves already-emitted fronts to propagate and drain.
    /// </summary>
    internal sealed class CrossSeaEventSystem
    {
        private readonly SimulationConfig config;
        private readonly WaveSourceSystem sourceSystem;
        private CrossSeaEventData data;
        private int nextEventId;

        public CrossSeaEventData Data => data;
        public int NextEventId => nextEventId;
        public int EmittingSourceId => IsEmitting ? data.SourceId : 0;
        public float EmissionEnergyScale => IsEmitting
            ? Mathf.Lerp(Mathf.Clamp01(config.CrossSeaMinimumEnergyScale), 1f,
                Mathf.Clamp01(data.Intensity))
            : 0f;

        private bool IsEmitting => data.Phase == CrossSeaEventPhase.Building ||
            data.Phase == CrossSeaEventPhase.Established ||
            data.Phase == CrossSeaEventPhase.Departing;

        public CrossSeaEventSystem(SimulationConfig config, WaveSourceSystem sourceSystem)
        {
            this.config = config;
            this.sourceSystem = sourceSystem;
        }

        public void Reset()
        {
            nextEventId = 1;
            WaveSourceKind kind = config.CrossSeaSourceKind == WaveSourceKind.WesternSwell
                ? WaveSourceKind.NorthernCrossSea : config.CrossSeaSourceKind;
            ulong automaticTick = config.CrossSeaAutomaticStartSeconds < 0f
                ? ulong.MaxValue : SecondsToTicks(config.CrossSeaAutomaticStartSeconds);
            data = new CrossSeaEventData
            {
                Phase = CrossSeaEventPhase.Inactive,
                SourceKind = kind,
                NextAutomaticStartTick = automaticTick
            };
        }

        public bool Trigger(ulong currentTick)
        {
            if (data.Phase != CrossSeaEventPhase.Inactive) return false;
            if (!sourceSystem.TryGetSource(data.SourceKind, out WaveSourceData source)) return false;
            if (!sourceSystem.StartEventSource(data.SourceKind, currentTick,
                    out int sourceId, out int systemId))
                return false;

            data.EventId = nextEventId++;
            data.TriggerCount++;
            data.Phase = CrossSeaEventPhase.Building;
            data.SourceId = sourceId;
            data.SwellSystemId = systemId;
            data.Intensity = 0f;
            data.DepartureStartIntensity = 0f;
            data.InitialSourcePacketCount = source.SpawnedPackets;
            data.EmittedPacketCount = 0;
            data.ActivePacketCount = 0;
            data.StartedTick = currentTick;
            data.PhaseStartedTick = currentTick;
            data.EmissionsStoppedTick = 0;
            data.NextAutomaticStartTick = ulong.MaxValue;
            return true;
        }

        public bool RequestDeparture(ulong currentTick)
        {
            if (data.Phase != CrossSeaEventPhase.Building &&
                data.Phase != CrossSeaEventPhase.Established)
                return false;
            data.DepartureStartIntensity = data.Intensity;
            data.Phase = CrossSeaEventPhase.Departing;
            data.PhaseStartedTick = currentTick;
            return true;
        }

        public void AdvanceBeforeEmission(ulong currentTick)
        {
            if (data.Phase == CrossSeaEventPhase.Inactive)
            {
                if (data.NextAutomaticStartTick != ulong.MaxValue &&
                    currentTick >= data.NextAutomaticStartTick)
                    Trigger(currentTick);
                return;
            }

            ulong elapsed = currentTick >= data.PhaseStartedTick
                ? currentTick - data.PhaseStartedTick : 0;
            switch (data.Phase)
            {
                case CrossSeaEventPhase.Building:
                    data.Intensity = PhaseProgress(elapsed, config.CrossSeaBuildSeconds);
                    if (elapsed >= SecondsToTicks(config.CrossSeaBuildSeconds))
                    {
                        data.Phase = CrossSeaEventPhase.Established;
                        data.PhaseStartedTick = currentTick;
                        data.Intensity = 1f;
                    }
                    break;
                case CrossSeaEventPhase.Established:
                    data.Intensity = 1f;
                    if (elapsed >= SecondsToTicks(config.CrossSeaEstablishedSeconds))
                    {
                        data.Phase = CrossSeaEventPhase.Departing;
                        data.PhaseStartedTick = currentTick;
                        data.DepartureStartIntensity = 1f;
                    }
                    break;
                case CrossSeaEventPhase.Departing:
                    data.Intensity = data.DepartureStartIntensity *
                        (1f - PhaseProgress(elapsed, config.CrossSeaDepartureSeconds));
                    if (elapsed >= SecondsToTicks(config.CrossSeaDepartureSeconds))
                    {
                        sourceSystem.StopEventSource(data.SourceId);
                        data.Phase = CrossSeaEventPhase.Draining;
                        data.PhaseStartedTick = currentTick;
                        data.EmissionsStoppedTick = currentTick;
                        data.Intensity = 0f;
                    }
                    break;
            }
        }

        public void SynchronizeAfterEmission(ulong currentTick)
        {
            if (data.EventId == 0) return;
            if (sourceSystem.TryGetSource(data.SourceKind, out WaveSourceData source))
                data.EmittedPacketCount = Mathf.Max(0,
                    source.SpawnedPackets - data.InitialSourcePacketCount);
            data.ActivePacketCount = sourceSystem.GetSystemActivePacketCount(data.SwellSystemId);
            if (data.Phase != CrossSeaEventPhase.Draining || data.ActivePacketCount > 0) return;

            if (!sourceSystem.ReleaseEventStream(data.SourceId, data.SwellSystemId)) return;
            data.Phase = CrossSeaEventPhase.Inactive;
            data.PhaseStartedTick = currentTick;
        }

        private float PhaseProgress(ulong elapsedTicks, float durationSeconds)
        {
            ulong durationTicks = SecondsToTicks(durationSeconds);
            return durationTicks == 0 ? 1f : Mathf.Clamp01(elapsedTicks / (float)durationTicks);
        }

        private ulong SecondsToTicks(float seconds)
            => seconds <= 0f ? 0UL : (ulong)Mathf.Max(1,
                Mathf.CeilToInt(seconds / config.FixedDeltaTime));
    }
}

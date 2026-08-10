using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    internal sealed class WavePropagationSystem
    {
        private readonly SimulationConfig config;
        private readonly IOceanEnvironment environment;
        private readonly WaveSourceSystem sourceSystem;
        private readonly float energyRetentionPerTick;
        private readonly float foamRetentionPerTick;
        private readonly float spentRetentionPerTick;

        public WavePropagationSystem(SimulationConfig config, IOceanEnvironment environment,
            WaveSourceSystem sourceSystem)
        {
            this.config = config;
            this.environment = environment;
            this.sourceSystem = sourceSystem;
            energyRetentionPerTick = Mathf.Exp(-config.EnergyDecayPerSecond *
                config.FixedDeltaTime);
            foamRetentionPerTick = Mathf.Exp(-config.FoamEnergyLossPerSecond *
                config.FixedDeltaTime);
            spentRetentionPerTick = Mathf.Exp(-config.SpentEnergyLossPerSecond *
                config.FixedDeltaTime);
        }

        public void Decide(IReadOnlyList<WaveData> waves, List<WaveDecision> decisions,
            List<SimulationEvent> pendingEvents, ulong tick)
        {
            while (decisions.Count < waves.Count) decisions.Add(default);
            if (decisions.Count > waves.Count)
                decisions.RemoveRange(waves.Count, decisions.Count - waves.Count);

            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                WaveData wave = waves[waveIndex];
                WaveSegmentData[] segments = wave.MutableSegments;
                WaveDecision decision = decisions[waveIndex];
                int segmentCount = segments == null ? 0 : segments.Length;
                if (decision.WaveId != wave.Id || decision.Segments == null ||
                    decision.Segments.Length != segmentCount)
                {
                    decision.WaveId = wave.Id;
                    decision.Segments = new WaveSegmentDecision[segmentCount];
                }

                float deepWaterCruiseSpeed = sourceSystem.DeepWaterCruiseSpeed(wave.PacketLength);
                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    decision.Segments[segmentIndex] = DecideSegment(wave,
                        segments[segmentIndex], deepWaterCruiseSpeed, pendingEvents, tick);
                }

                ApplyCoherence(wave, ref decision);
                AggregateWaveDecision(wave, ref decision);
                decisions[waveIndex] = decision;
            }
        }

        private WaveSegmentDecision DecideSegment(WaveData wave, WaveSegmentData segment,
            float deepWaterCruiseSpeed, List<SimulationEvent> pendingEvents, ulong tick)
        {
            if (!segment.Active)
            {
                return new WaveSegmentDecision
                {
                    Position = segment.Position,
                    Direction = segment.TravelDirection,
                    CoherentDirection = segment.TravelDirection,
                    Speed = 0f,
                    Energy = segment.Energy,
                    SampledDepth = segment.SampledDepth,
                    DepthGradient = segment.DepthGradient,
                    BreakingIntensity = segment.BreakingIntensity,
                    FoamEnergy = segment.FoamEnergy,
                    State = WaveState.Spent,
                    Active = false
                };
            }

            float dt = config.FixedDeltaTime;
            float sampledDepth = segment.SampledDepth;
            Vector2 depthGradient = segment.DepthGradient;
            float currentEffectiveDepth = WaveDerived.EffectiveDepth(sampledDepth, wave.PacketLength);
            float depthInfluenceLimit = Mathf.Max(6.5f, wave.PacketLength * 1.75f);
            float targetSpeed = sampledDepth >= depthInfluenceLimit
                ? deepWaterCruiseSpeed
                : Mathf.Min(deepWaterCruiseSpeed,
                    Mathf.Sqrt(9.81f * Mathf.Max(0.1f, currentEffectiveDepth)));
            float speed;
            if (segment.State == WaveState.Traveling)
            {
                float acceleration = targetSpeed < segment.Speed
                    ? config.WaveShoalingDeceleration : config.WaveDeepRecovery;
                speed = Mathf.MoveTowards(segment.Speed, targetSpeed, acceleration * dt);
            }
            else if (segment.State == WaveState.Breaking)
                speed = Mathf.MoveTowards(segment.Speed, Mathf.Min(targetSpeed, segment.Speed),
                    config.WaveShoalingDeceleration * dt);
            else
                speed = Mathf.MoveTowards(segment.Speed, 0f, 4.2f * dt);

            Vector2 direction = segment.TravelDirection.sqrMagnitude < 0.0001f
                ? wave.TravelDirection : segment.TravelDirection.normalized;
            float refractionWeight = Mathf.Clamp01((6.5f - currentEffectiveDepth) / 5.5f);
            if (refractionWeight > 0f && segment.State == WaveState.Traveling &&
                depthGradient.sqrMagnitude > 0.0001f)
            {
                Vector2 towardShallow = -depthGradient.normalized;
                Vector2 lateralBend = towardShallow - direction * Vector2.Dot(towardShallow, direction);
                direction = (direction + lateralBend * config.WaveRefractionStrength *
                    refractionWeight * dt).normalized;
            }

            float energy = segment.Energy * energyRetentionPerTick;
            float foamEnergy = segment.FoamEnergy * foamRetentionPerTick;

            Vector2 nextPosition = segment.Position + direction * speed * dt;
            int interval = Mathf.Max(1, config.WaveEnvironmentSampleInterval);
            ulong samplePhase = (ulong)(wave.Id * 17 + segment.Index * 7);
            bool refreshEnvironment = (tick + samplePhase) % (ulong)interval == 0;
            if (refreshEnvironment)
            {
                sampledDepth = environment.SampleDepth(nextPosition);
                depthGradient = sampledDepth < 6.5f
                    ? environment.SampleDepthGradient(nextPosition) : Vector2.zero;
            }

            float effectiveDepth = WaveDerived.EffectiveDepth(sampledDepth, wave.PacketLength);
            float amplitude = Mathf.Sqrt(Mathf.Max(0f, energy)) *
                (1f + 0.45f / effectiveDepth);
            float steepness = amplitude / Mathf.Max(0.25f, wave.PacketLength);
            float depthLimitedRatio = amplitude / Mathf.Max(0.35f, effectiveDepth);
            float steepnessIntensity = BreakingSeverity(steepness,
                config.BreakingSteepness);
            float depthIntensity = BreakingSeverity(depthLimitedRatio,
                config.DepthLimitedBreakingRatio);
            bool onLand = sampledDepth <= 0.24f;
            // Generated rocks are restricted to shelf water below 3.6 units. The margin
            // retains collision coverage across the three-tick environment sample interval
            // without paying nine hash-grid lookups for every deep-ocean section.
            int rockIndex = sampledDepth < 5f
                ? environment.FindRock(nextPosition, config.RockInteractionRadius * 0.3f) : -1;
            float requestedIntensity = Mathf.Max(steepnessIntensity, depthIntensity);
            if (onLand) requestedIntensity = 1f;
            if (rockIndex >= 0) requestedIntensity = Mathf.Max(requestedIntensity, 0.85f);

            float breakingIntensity = segment.BreakingIntensity;
            if (requestedIntensity > breakingIntensity)
                breakingIntensity = Mathf.MoveTowards(breakingIntensity, requestedIntensity,
                    config.BreakingIntensityAttackPerSecond * dt);
            else
                breakingIntensity = Mathf.MoveTowards(breakingIntensity, requestedIntensity,
                    config.BreakingIntensityRecoveryPerSecond * dt);
            if (requestedIntensity > 0f)
                breakingIntensity = Mathf.Max(breakingIntensity,
                    Mathf.Min(0.22f, requestedIntensity));

            bool startedBreaking = segment.State == WaveState.Traveling &&
                requestedIntensity > 0f;
            if (startedBreaking)
                pendingEvents.Add(new SimulationEvent(SimulationEventType.WaveStartedBreaking,
                    wave.Id, 0, nextPosition,
                    Mathf.Max(steepness, depthLimitedRatio), segment.Index));

            if (breakingIntensity >= config.BreakingReleaseIntensity)
            {
                float beforeBreaking = energy;
                float lossRate = Mathf.Lerp(config.BreakingMinimumEnergyLossPerSecond,
                    config.BreakingEnergyLossPerSecond, breakingIntensity);
                energy *= Mathf.Exp(-lossRate * dt);
                foamEnergy += (beforeBreaking - energy) * config.BreakingEnergyToFoam;
            }

            if (rockIndex >= 0)
            {
                float previousEnergy = energy;
                energy *= Mathf.Clamp01(1f - config.RockEnergyAbsorption * 0.32f);
                foamEnergy += (previousEnergy - energy) * config.BreakingEnergyToFoam;
                pendingEvents.Add(new SimulationEvent(SimulationEventType.WaveHitRock,
                    wave.Id, 0, nextPosition, previousEnergy - energy, segment.Index));
            }

            WaveState state;
            if (onLand)
            {
                float beforeLand = energy;
                energy *= spentRetentionPerTick;
                foamEnergy += (beforeLand - energy) * config.BreakingEnergyToFoam;
                nextPosition = segment.Position;
                speed = 0f;
                state = WaveState.Spent;
            }
            else if (energy < config.MinimumEnergy)
            {
                state = WaveState.Spent;
                speed = Mathf.MoveTowards(speed, 0f, 4.2f * dt);
            }
            else
                state = breakingIntensity >= config.BreakingReleaseIntensity
                    ? WaveState.Breaking : WaveState.Traveling;

            Vector2 half = config.WorldHalfExtents;
            bool outside = Mathf.Abs(nextPosition.x) > half.x + 1f ||
                           Mathf.Abs(nextPosition.y) > half.y + 1f;
            bool active = !outside && (energy >= config.MinimumEnergy ||
                (!onLand && foamEnergy >= config.MinimumFoamEnergy));
            return new WaveSegmentDecision
            {
                Position = nextPosition,
                Direction = direction,
                CoherentDirection = direction,
                Speed = speed,
                Energy = energy,
                SampledDepth = sampledDepth,
                DepthGradient = depthGradient,
                BreakingIntensity = breakingIntensity,
                FoamEnergy = foamEnergy,
                InteractionForce = energy * (1f + 0.7f / effectiveDepth),
                State = active ? state : WaveState.Spent,
                Active = active
            };
        }

        private static float BreakingSeverity(float value, float threshold)
        {
            if (value < threshold) return 0f;
            float excess = Mathf.InverseLerp(threshold, threshold * 1.8f, value);
            return Mathf.Lerp(0.22f, 1f, excess);
        }

        private void ApplyCoherence(WaveData wave, ref WaveDecision decision)
        {
            WaveSegmentDecision[] segments = decision.Segments;
            if (segments == null || segments.Length <= 1) return;
            float spacing = wave.CrestLength / Mathf.Max(1, segments.Length - 1);
            float maximumLink = spacing * config.WaveSegmentLinkBreakMultiplier;
            float directionWeight = Mathf.Clamp01(config.WaveSegmentDirectionCoherence *
                config.FixedDeltaTime);
            float positionWeight = Mathf.Clamp01(config.WaveSegmentPositionCoherence *
                config.FixedDeltaTime);

            for (int index = 0; index < segments.Length; index++)
            {
                WaveSegmentDecision current = segments[index];
                current.CoherentDirection = current.Direction;
                if (!current.Active || current.State != WaveState.Traveling)
                {
                    segments[index] = current;
                    continue;
                }

                Vector2 neighborDirection = Vector2.zero;
                Vector2 expectedPosition = Vector2.zero;
                int neighbors = 0;
                if (index > 0 && CanLink(current, segments[index - 1], maximumLink))
                {
                    neighborDirection += segments[index - 1].Direction;
                    expectedPosition += segments[index - 1].Position;
                    neighbors++;
                }
                if (index + 1 < segments.Length && CanLink(current, segments[index + 1], maximumLink))
                {
                    neighborDirection += segments[index + 1].Direction;
                    expectedPosition += segments[index + 1].Position;
                    neighbors++;
                }

                if (neighbors > 0)
                {
                    neighborDirection.Normalize();
                    Vector2 coherent = Vector2.Lerp(current.Direction,
                        neighborDirection, directionWeight);
                    if (coherent.sqrMagnitude > 0.0001f) current.CoherentDirection = coherent.normalized;

                    if (neighbors == 2)
                    {
                        Vector2 correction = expectedPosition * 0.5f - current.Position;
                        current.Position += current.Direction * Vector2.Dot(correction,
                            current.Direction) * positionWeight;
                    }
                }
                segments[index] = current;
            }

            for (int index = 0; index < segments.Length; index++)
            {
                WaveSegmentDecision segment = segments[index];
                segment.Direction = segment.CoherentDirection;
                segments[index] = segment;
            }
        }

        private static bool CanLink(WaveSegmentDecision a, WaveSegmentDecision b, float maximumLink)
        {
            return b.Active && b.State == WaveState.Traveling &&
                   (a.Position - b.Position).sqrMagnitude <= maximumLink * maximumLink;
        }

        private void AggregateWaveDecision(WaveData wave, ref WaveDecision decision)
        {
            Vector2 position = Vector2.zero;
            Vector2 direction = Vector2.zero;
            float energy = 0f;
            float speed = 0f;
            float force = 0f;
            int active = 0;
            bool anyTraveling = false;
            bool anyBreaking = false;
            WaveSegmentDecision[] segments = decision.Segments;
            for (int index = 0; index < segments.Length; index++)
            {
                WaveSegmentDecision segment = segments[index];
                if (!segment.Active) continue;
                position += segment.Position;
                direction += segment.Direction;
                energy += segment.Energy;
                speed += segment.Speed;
                force += segment.InteractionForce;
                active++;
                anyTraveling |= segment.State == WaveState.Traveling;
                anyBreaking |= segment.State == WaveState.Breaking;
            }

            decision.ActiveSegmentCount = active;
            int minimumCoherentSegments = segments.Length <= 1 ? 1 :
                Mathf.CeilToInt(segments.Length * config.WaveMinimumActiveSegmentFraction);
            decision.Expired = active < minimumCoherentSegments;
            if (active > 0)
            {
                decision.Position = position / active;
                decision.Direction = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized : wave.TravelDirection;
                decision.Energy = energy / active;
                decision.Speed = speed / active;
                decision.InteractionForce = force / active;
                decision.State = anyTraveling ? WaveState.Traveling
                    : anyBreaking ? WaveState.Breaking : WaveState.Spent;
            }
            else
            {
                decision.Position = wave.Position;
                decision.Direction = wave.TravelDirection;
                decision.Energy = 0f;
                decision.Speed = 0f;
                decision.InteractionForce = 0f;
                decision.State = WaveState.Spent;
            }
        }
    }
}

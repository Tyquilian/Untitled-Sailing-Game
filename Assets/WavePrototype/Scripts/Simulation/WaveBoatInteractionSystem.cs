using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    internal sealed class WaveBoatInteractionSystem
    {
        private readonly SimulationConfig config;

        public WaveBoatInteractionSystem(SimulationConfig config)
        {
            this.config = config;
        }

        public void Accumulate(IReadOnlyList<WaveData> waves,
            IReadOnlyList<WaveDecision> waveDecisions, IReadOnlyList<BoatData> boats,
            List<BoatDecision> boatDecisions, List<SimulationEvent> pendingEvents)
        {
            boatDecisions.Clear();
            for (int i = 0; i < boats.Count; i++) boatDecisions.Add(default);

            float dt = config.FixedDeltaTime;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                WaveData wave = waves[waveIndex];
                WaveDecision waveDecision = waveDecisions[waveIndex];
                WaveSegmentData[] segments = wave.MutableSegments;
                WaveSegmentDecision[] segmentDecisions = waveDecision.Segments;
                if (segments == null || segmentDecisions == null) continue;
                float segmentSpan = segments.Length <= 1
                    ? wave.CrestLength
                    : wave.CrestLength / (segments.Length - 1f);

                for (int boatIndex = 0; boatIndex < boats.Count; boatIndex++)
                {
                    BoatData boat = boats[boatIndex];
                    VesselProfileDefinition profile = config.GetVesselProfile(boat.Profile);
                    int bestSegment = -1;
                    float bestNormalizedDistance = 1f;
                    for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                    {
                        WaveSegmentDecision segmentDecision = segmentDecisions[segmentIndex];
                        if (!segmentDecision.Active) continue;
                        Vector2 direction = segmentDecision.Direction;
                        Vector2 crestAxis = new Vector2(-direction.y, direction.x);
                        Vector2 segmentPosition = Vector2.Lerp(segments[segmentIndex].Position,
                            segmentDecision.Position, 0.5f);
                        bool breaking = segmentDecision.State == WaveState.Breaking;
                        float alongRadius = breaking
                            ? wave.PacketLength * 0.62f + config.BoatInteractionRadius
                            : wave.PacketLength * config.TravelingLongitudinalScale +
                              config.TravelingLongitudinalPadding;
                        float acrossRadius = segmentSpan * 0.62f + config.BoatInteractionRadius;
                        int sampleCount = Mathf.Max(1, profile.HullSampleCount);
                        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                        {
                            Vector2 samplePosition = VesselProfiles.GetHullSampleWorldPosition(
                                boat, profile, sampleIndex);
                            Vector2 offset = samplePosition - segmentPosition;
                            float along = Vector2.Dot(offset, direction) / alongRadius;
                            float across = Vector2.Dot(offset, crestAxis) / acrossRadius;
                            float normalizedSquared = along * along + across * across;
                            if (normalizedSquared >= bestNormalizedDistance *
                                bestNormalizedDistance) continue;
                            float normalizedDistance = Mathf.Sqrt(normalizedSquared);
                            if (normalizedDistance >= 1f) continue;
                            bestNormalizedDistance = normalizedDistance;
                            bestSegment = segmentIndex;
                        }
                    }

                    if (bestSegment < 0) continue;
                    WaveSegmentDecision best = segmentDecisions[bestSegment];
                    float proximity = Mathf.Pow(1f - bestNormalizedDistance, 0.68f);
                    Vector2 forward = SimulationMath.HeadingVector(boat.Heading);
                    float following = Mathf.Max(0f, Vector2.Dot(forward, best.Direction));
                    float headOn = Mathf.Max(0f, -Vector2.Dot(forward, best.Direction));
                    BoatDecision decision = boatDecisions[boatIndex];
                    float breakingScale = 0.55f + best.BreakingIntensity * 0.45f;
                    float stateMultiplier = best.State == WaveState.Breaking
                        ? Mathf.Lerp(config.TravelingImpactMultiplier,
                            config.BreakingImpactMultiplier, breakingScale)
                        : best.State == WaveState.Spent ? 0.12f : config.TravelingImpactMultiplier;
                    float impact = best.InteractionForce * proximity * stateMultiplier;
                    if (best.State == WaveState.Traveling)
                    {
                        float carrySpeed = Mathf.Max(1f,
                            best.Speed * config.TravelingCarrySpeedFraction);
                        float boatSpeedWithWave = Vector2.Dot(boat.Velocity, best.Direction);
                        float relativePassage = Mathf.Clamp01((carrySpeed - boatSpeedWithWave) /
                            carrySpeed);
                        impact *= relativePassage;
                    }
                    decision.Force += best.Direction * impact * config.WaveBoatForceScale *
                        profile.WaveForceScale;
                    decision.Force += forward * impact * following *
                        config.WaveFollowingThrustScale * profile.WaveForceScale;
                    decision.Force -= boat.Velocity * impact * headOn *
                        config.WaveHeadOnDampingScale * profile.WaveForceScale;
                    float yawMultiplier = best.State == WaveState.Traveling
                        ? config.TravelingYawMultiplier : 1f;
                    decision.HeadingImpulse += SimulationMath.Cross(forward, best.Direction)
                        * impact * proximity * config.WaveYawScale * yawMultiplier *
                        profile.WaveYawScale;
                    if (best.State == WaveState.Breaking)
                        decision.Damage += Mathf.Max(0f, best.InteractionForce -
                            config.BreakingBoatDamageThreshold) * proximity * dt *
                            config.BreakingBoatDamageScale * breakingScale *
                            profile.DamageTakenScale;
                    boatDecisions[boatIndex] = decision;
                    pendingEvents.Add(new SimulationEvent(SimulationEventType.WaveHitBoat,
                        wave.Id, boat.Id, boat.Position, impact, bestSegment));
                }
            }
        }
    }
}

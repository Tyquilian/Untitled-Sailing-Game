using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    internal sealed class BoatMotionSystem
    {
        private readonly SimulationConfig config;
        private readonly IOceanEnvironment environment;
        private readonly List<int> rockCandidates = new List<int>(64);

        public int RockQueryCount { get; private set; }
        public int RockCandidateChecks { get; private set; }
        public int RockPotentialChecks { get; private set; }

        public BoatMotionSystem(SimulationConfig config, IOceanEnvironment environment)
        {
            this.config = config;
            this.environment = environment;
        }

        public void Decide(IReadOnlyList<BoatData> boats, List<BoatDecision> decisions,
            BoatInputBuffer inputBuffer)
        {
            float dt = config.FixedDeltaTime;
            RockQueryCount = 0;
            RockCandidateChecks = 0;
            RockPotentialChecks = 0;
            for (int i = 0; i < boats.Count; i++)
            {
                BoatData boat = boats[i];
                BoatDecision decision = decisions[i];
                BoatControl control = inputBuffer.GetControl(boat.Id);
                VesselProfileDefinition profile = config.GetVesselProfile(boat.Profile);
                Vector2 forward = SimulationMath.HeadingVector(boat.Heading);
                float cruiseSpeed = config.BoatCruiseSpeed * profile.CruiseSpeedScale;
                float surfSpeed = config.BoatSurfSpeedCap * profile.SurfSpeedScale;

                float windEfficiency = GetWindEfficiency(boat.Heading);
                if (control.Throttle >= 0f)
                {
                    float fadeRange = Mathf.Max(0.01f, config.BoatCruisePropulsionFadeRange);
                    float fadeStart = Mathf.Max(0f, cruiseSpeed - fadeRange);
                    float speedIntoFade = Mathf.InverseLerp(fadeStart, cruiseSpeed, boat.Velocity.magnitude);
                    float propulsionHeadroom = 1f - Mathf.SmoothStep(0f, 1f, speedIntoFade);
                    decision.Force += forward * config.SailingForce * profile.PropulsionScale *
                        control.Throttle * windEfficiency * propulsionHeadroom;
                }
                else
                {
                    decision.Force -= boat.Velocity * (-control.Throttle) * boat.Mass *
                        config.BoatReverseBrakeScale;
                    decision.Force += forward * config.SailingForce * profile.PropulsionScale *
                        control.Throttle * config.BoatReversePropulsionScale;
                }

                float turnAuthority = Mathf.Lerp(config.BoatMinimumTurnAuthority, 1f,
                    Mathf.Clamp01(boat.Velocity.magnitude /
                        Mathf.Max(0.01f, config.BoatFullTurnAuthoritySpeed)));
                decision.HeadingImpulse += control.Steering * config.BoatTurnRate *
                    profile.TurnRateScale * turnAuthority;
                decision.Heading = boat.Heading + decision.HeadingImpulse * dt;
                Vector2 decidedForward = SimulationMath.HeadingVector(decision.Heading);
                Vector2 decidedSide = new Vector2(-decidedForward.y, decidedForward.x);
                decision.Velocity = boat.Velocity + decision.Force / Mathf.Max(0.1f, boat.Mass) * dt;
                float forwardSpeed = Vector2.Dot(decision.Velocity, decidedForward);
                float sideSpeed = Vector2.Dot(decision.Velocity, decidedSide) * Mathf.Exp(
                    -config.BoatLateralDrag * profile.LateralDragScale * dt);
                decision.Velocity = decidedForward * forwardSpeed + decidedSide * sideSpeed;
                decision.Velocity *= Mathf.Exp(-config.BoatLinearDrag * profile.LinearDragScale * dt);

                float speed = decision.Velocity.magnitude;
                if (speed > cruiseSpeed && config.BoatSurfExcessDecay > 0f)
                {
                    float excess = speed - cruiseSpeed;
                    float decayedSpeed = cruiseSpeed + excess * Mathf.Exp(-config.BoatSurfExcessDecay * dt);
                    decision.Velocity *= decayedSpeed / speed;
                }
                decision.Velocity = Vector2.ClampMagnitude(decision.Velocity,
                    Mathf.Max(cruiseSpeed, surfSpeed));
                decision.Position = boat.Position + decision.Velocity * dt;

                Vector2 half = config.WorldHalfExtents;
                bool outside = Mathf.Abs(decision.Position.x) > half.x || Mathf.Abs(decision.Position.y) > half.y;
                if (outside || HullIntersectsLand(decision.Position, decision.Heading, profile))
                {
                    decision.Collision = SimulationEventType.BoatGrounded;
                    decision.Damage += (config.GroundingBaseDamage + boat.Velocity.magnitude *
                        config.GroundingSpeedDamageScale) * profile.DamageTakenScale;
                    decision.Position = boat.Position;
                    decision.Velocity *= -config.GroundingBounce;
                }
                else if (ResolveRockMotion(boat.Position, decision.Velocity, dt,
                    profile.CollisionRadius, out Vector2 resolvedPosition,
                    out Vector2 resolvedVelocity, out float impactDamage))
                {
                    decision.Collision = SimulationEventType.BoatHitRock;
                    decision.Damage += impactDamage * profile.DamageTakenScale;
                    decision.Position = resolvedPosition;
                    decision.Velocity = resolvedVelocity;
                }
                decisions[i] = decision;
            }
        }

        public float GetWindEfficiency(float heading)
        {
            float alignment = Vector2.Dot(SimulationMath.HeadingVector(heading), config.WindDirection.normalized);
            float favorable = Mathf.Pow(Mathf.Clamp01((alignment + 1f) * 0.5f), 0.72f);
            return 0.38f + favorable * 0.62f;
        }

        public bool HullIntersectsLand(Vector2 position, float heading,
            VesselProfileDefinition profile)
        {
            var boat = new BoatData { Position = position, Heading = heading };
            int sampleCount = Mathf.Max(1, profile.HullSampleCount);
            for (int sample = 0; sample < sampleCount; sample++)
                if (environment.IsLand(VesselProfiles.GetHullSampleWorldPosition(
                        boat, profile, sample)))
                    return true;
            return false;
        }

        public Vector2 FindNearbyWater(Vector2 origin, float heading,
            VesselProfileDefinition profile)
        {
            for (int ring = 1; ring < 30; ring++)
            {
                for (int step = 0; step < 16; step++)
                {
                    float radians = step * Mathf.PI * 2f / 16f;
                    Vector2 candidate = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ring;
                    if (!HullIntersectsLand(candidate, heading, profile) &&
                        environment.FindRock(candidate, profile.CollisionRadius) < 0)
                        return candidate;
                }
            }
            return Vector2.zero;
        }

        private bool ResolveRockMotion(Vector2 start, Vector2 initialVelocity, float dt,
            float collisionRadius,
            out Vector2 resolvedPosition, out Vector2 resolvedVelocity, out float damage)
        {
            const int maximumContacts = 4;
            const float minimumTime = 0.000001f;
            resolvedPosition = start;
            resolvedVelocity = initialVelocity;
            damage = 0f;
            float remainingTime = dt;
            bool hitAnyRock = false;

            for (int contact = 0; contact < maximumContacts && remainingTime > minimumTime; contact++)
            {
                Vector2 end = resolvedPosition + resolvedVelocity * remainingTime;
                if (!TryFindEarliestRockHit(resolvedPosition, end, collisionRadius,
                    out int rockIndex, out float hitFraction))
                {
                    resolvedPosition = end;
                    break;
                }

                RockData rock = environment.Rocks[rockIndex];
                Vector2 segment = end - resolvedPosition;
                Vector2 contactPoint = resolvedPosition + segment * hitFraction;
                Vector2 normal = contactPoint - rock.Position;
                if (normal.sqrMagnitude < 0.000001f)
                    normal = resolvedVelocity.sqrMagnitude > 0.000001f ? -resolvedVelocity.normalized : Vector2.right;
                else
                    normal.Normalize();

                float expandedRadius = rock.Radius + collisionRadius;
                resolvedPosition = rock.Position + normal * (expandedRadius + config.RockContactSkin);
                float normalSpeed = Vector2.Dot(resolvedVelocity, normal);
                float impactSpeed = Mathf.Max(0f, -normalSpeed);
                Vector2 tangentVelocity = resolvedVelocity - normal * normalSpeed;
                if (normalSpeed < 0f)
                    resolvedVelocity = tangentVelocity * config.RockTangentialRetention
                        - normal * normalSpeed * config.RockImpactRestitution;
                else
                    resolvedVelocity = tangentVelocity * config.RockTangentialRetention + normal * normalSpeed;

                damage += config.RockBaseDamage + impactSpeed * config.RockSpeedDamageScale;
                hitAnyRock = true;
                remainingTime *= 1f - hitFraction;
            }
            return hitAnyRock;
        }

        private bool TryFindEarliestRockHit(Vector2 start, Vector2 end, float collisionRadius,
            out int rockIndex, out float hitFraction)
        {
            rockIndex = -1;
            hitFraction = float.MaxValue;
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;
            if (segmentLengthSquared < 0.00000001f) return false;

            IReadOnlyList<RockData> rocks = environment.Rocks;
            RockQueryCount++;
            RockPotentialChecks += rocks.Count;
            IRockSpatialQuery spatial = config.EnableSpatialBroadphase
                ? environment as IRockSpatialQuery : null;
            bool useSpatial = spatial != null;
            if (useSpatial)
            {
                float expansion = collisionRadius + spatial.MaximumRockRadius;
                Vector2 padding = Vector2.one * expansion;
                spatial.QueryRockIndices(Vector2.Min(start, end) - padding,
                    Vector2.Max(start, end) + padding, rockCandidates);
            }

            int candidateCount = useSpatial ? rockCandidates.Count : rocks.Count;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                int i = useSpatial ? rockCandidates[candidateIndex] : candidateIndex;
                RockCandidateChecks++;
                RockData rock = rocks[i];
                float expandedRadius = rock.Radius + collisionRadius;
                Vector2 offset = start - rock.Position;
                float distanceFromSurface = offset.sqrMagnitude - expandedRadius * expandedRadius;
                float approach = Vector2.Dot(offset, segment);
                float candidate;
                if (distanceFromSurface <= 0f)
                {
                    if (approach >= 0f) continue;
                    candidate = 0f;
                }
                else
                {
                    if (approach >= 0f) continue;
                    float discriminant = approach * approach - segmentLengthSquared * distanceFromSurface;
                    if (discriminant < 0f) continue;
                    candidate = (-approach - Mathf.Sqrt(discriminant)) / segmentLengthSquared;
                    if (candidate < 0f || candidate > 1f) continue;
                }

                if (candidate < hitFraction)
                {
                    hitFraction = candidate;
                    rockIndex = i;
                }
            }
            return rockIndex >= 0;
        }
    }
}

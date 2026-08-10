using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Reusable service for lightweight world objects. Cargo exercises collection and
    /// lifecycle events; wreckage exercises wave drift and two-way boat contact.
    /// </summary>
    internal sealed class FloatingObjectSystem
    {
        private readonly SimulationConfig config;
        private readonly IOceanEnvironment environment;
        private readonly List<WaveSectionReference> spatialCandidates =
            new List<WaveSectionReference>(256);
        private DeterministicRandom random;
        private int nextObjectId;

        public uint RandomState => random.State;
        public int NextObjectId => nextObjectId;
        public int CollectedCount { get; private set; }
        public float CollectedValue { get; private set; }
        public int WaveExactSegmentChecks { get; private set; }
        public int WavePotentialSegmentChecks { get; private set; }

        public FloatingObjectSystem(SimulationConfig config, IOceanEnvironment environment)
        {
            this.config = config;
            this.environment = environment;
        }

        public void Reset(int seed, List<FloatingObjectData> objects, Vector2 playerPosition)
        {
            random = new DeterministicRandom(seed ^ 0x71C43);
            nextObjectId = 1;
            CollectedCount = 0;
            CollectedValue = 0f;
            objects.Clear();
            int target = config.TargetWaveCount <= 0
                ? 0 : Mathf.Max(0, config.InitialFloatingObjectCount);
            for (int index = 0; index < target; index++)
            {
                FloatingObjectKind kind = index % 3 == 2
                    ? FloatingObjectKind.Wreckage : FloatingObjectKind.Cargo;
                if (!TryFindSpawnPosition(objects, playerPosition, index, out Vector2 position)) continue;
                Add(objects, kind, position);
            }
        }

        public int Spawn(List<FloatingObjectData> objects, FloatingObjectKind kind, Vector2 position)
        {
            if (!IsSafe(position, kind == FloatingObjectKind.Wreckage ? 1.7f : 1.1f)) return 0;
            return Add(objects, kind, position);
        }

        public void Decide(IReadOnlyList<FloatingObjectData> objects,
            IReadOnlyList<WaveData> waves, IReadOnlyList<WaveDecision> waveDecisions,
            IReadOnlyList<BoatData> boats, List<BoatDecision> boatDecisions,
            List<FloatingObjectDecision> decisions, List<SimulationEvent> pendingEvents,
            WaveSectionSpatialIndex spatialIndex)
        {
            WaveExactSegmentChecks = 0;
            WavePotentialSegmentChecks = 0;
            while (decisions.Count < objects.Count) decisions.Add(default);
            if (decisions.Count > objects.Count)
                decisions.RemoveRange(objects.Count, decisions.Count - objects.Count);

            float dt = config.FixedDeltaTime;
            Vector2 half = config.WorldHalfExtents;
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                FloatingObjectData item = objects[objectIndex];
                var decision = new FloatingObjectDecision
                {
                    Position = item.Position,
                    Velocity = item.Velocity,
                    LastBreakingWaveId = item.LastBreakingWaveId,
                    Active = item.Active
                };
                if (!item.Active)
                {
                    decisions[objectIndex] = decision;
                    continue;
                }

                FloatingWaveSample waveSample = SampleWaveDrift(item.Position, waves,
                    waveDecisions, spatialIndex);
                decision.Velocity += waveSample.Drift * config.FloatingObjectWaveResponse * dt;
                if (waveSample.BreakingWaveId > 0 &&
                    waveSample.BreakingWaveId != item.LastBreakingWaveId)
                {
                    float inertia = item.Kind == FloatingObjectKind.Cargo
                        ? 0.58f : Mathf.Max(1f, item.Radius * item.Radius *
                            config.WreckageInertiaScale);
                    float scatterDegrees = ((item.Id * 37 + waveSample.BreakingWaveId * 17) % 13 - 6) * 0.85f;
                    Vector2 impulseDirection = Rotate(waveSample.BreakingDirection, scatterDegrees);
                    float impulse = (1.15f + waveSample.BreakingForce *
                        config.BreakingFloatingObjectImpulse) / inertia;
                    decision.Velocity += impulseDirection * impulse;
                    decision.LastBreakingWaveId = waveSample.BreakingWaveId;
                    pendingEvents.Add(new SimulationEvent(
                        SimulationEventType.FloatingObjectHitByBreakingWave,
                        waveSample.BreakingWaveId, 0, item.Position, impulse,
                        waveSample.BreakingSegmentIndex, item.Id));
                }
                decision.Velocity *= Mathf.Exp(-config.FloatingObjectDrag * dt);
                decision.Velocity = Vector2.ClampMagnitude(decision.Velocity,
                    config.FloatingObjectMaximumSpeed);

                for (int boatIndex = 0; boatIndex < boats.Count && decision.Active; boatIndex++)
                {
                    BoatData boat = boats[boatIndex];
                    VesselProfileDefinition profile = config.GetVesselProfile(boat.Profile);
                    float contactRadius = profile.CollisionRadius +
                        (item.Kind == FloatingObjectKind.Cargo
                            ? config.CargoCollectionRadius : item.Radius);
                    Vector2 offset = boat.Position - item.Position;
                    if (offset.sqrMagnitude > contactRadius * contactRadius) continue;

                    if (item.Kind == FloatingObjectKind.Cargo)
                    {
                        decision.Active = false;
                        pendingEvents.Add(new SimulationEvent(
                            SimulationEventType.FloatingObjectCollected, 0, boat.Id,
                            item.Position, item.Value, -1, item.Id));
                        continue;
                    }

                    Vector2 normal = offset.sqrMagnitude > 0.0001f
                        ? offset.normalized : -SimulationMath.HeadingVector(boat.Heading);
                    float closingSpeed = Mathf.Max(0f, -Vector2.Dot(boat.Velocity -
                        decision.Velocity, normal));
                    float impulse = config.WreckageBoatForce * (0.35f + closingSpeed * 0.12f);
                    BoatDecision boatDecision = boatDecisions[boatIndex];
                    boatDecision.Force += normal * impulse;
                    boatDecision.HeadingImpulse += SimulationMath.Cross(
                        SimulationMath.HeadingVector(boat.Heading), normal) * impulse * 0.7f *
                        profile.WaveYawScale;
                    boatDecisions[boatIndex] = boatDecision;
                    decision.Velocity -= normal * impulse * dt / Mathf.Max(0.5f, item.Radius * 3f);
                    if (closingSpeed > 0.2f)
                        pendingEvents.Add(new SimulationEvent(
                            SimulationEventType.BoatHitWreckage, 0, boat.Id,
                            item.Position, closingSpeed, -1, item.Id));
                }

                Vector2 nextPosition = item.Position + decision.Velocity * dt;
                bool outside = Mathf.Abs(nextPosition.x) > half.x - item.Radius ||
                               Mathf.Abs(nextPosition.y) > half.y - item.Radius;
                bool blocked = outside || environment.IsLand(nextPosition) ||
                               environment.FindRock(nextPosition, item.Radius) >= 0;
                if (blocked)
                {
                    decision.Velocity *= -0.18f;
                    nextPosition = item.Position;
                }
                decision.Position = nextPosition;
                decisions[objectIndex] = decision;
            }
        }

        public void Apply(List<FloatingObjectData> objects,
            IReadOnlyList<FloatingObjectDecision> decisions)
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                FloatingObjectData item = objects[index];
                FloatingObjectDecision decision = decisions[index];
                if (!decision.Active)
                {
                    if (item.Kind == FloatingObjectKind.Cargo)
                    {
                        CollectedCount++;
                        CollectedValue += item.Value;
                    }
                    objects.RemoveAt(index);
                    continue;
                }
                item.PreviousPosition = item.Position;
                item.Position = decision.Position;
                item.Velocity = decision.Velocity;
                item.LastBreakingWaveId = decision.LastBreakingWaveId;
                item.Active = true;
                objects[index] = item;
            }
        }

        private FloatingWaveSample SampleWaveDrift(Vector2 position, IReadOnlyList<WaveData> waves,
            IReadOnlyList<WaveDecision> waveDecisions, WaveSectionSpatialIndex spatialIndex)
        {
            const float radius = 7f;
            float radiusSquared = radius * radius;
            Vector2 drift = Vector2.zero;
            int breakingWaveId = 0;
            int breakingSegmentIndex = -1;
            float breakingForce = 0f;
            Vector2 breakingDirection = Vector2.zero;

            for (int waveIndex = 0; waveIndex < waveDecisions.Count; waveIndex++)
            {
                WaveSegmentDecision[] segments = waveDecisions[waveIndex].Segments;
                if (segments != null) WavePotentialSegmentChecks += segments.Length;
            }

            if (config.EnableSpatialBroadphase)
            {
                spatialIndex.Query(position, radius + spatialIndex.MaximumDecisionOffset,
                    spatialCandidates);
                int candidateIndex = 0;
                while (candidateIndex < spatialCandidates.Count)
                {
                    int waveIndex = spatialCandidates[candidateIndex].WaveIndex;
                    int nearest = -1;
                    float nearestSquared = radiusSquared;
                    while (candidateIndex < spatialCandidates.Count &&
                           spatialCandidates[candidateIndex].WaveIndex == waveIndex)
                    {
                        WaveSectionReference reference = spatialCandidates[candidateIndex++];
                        if (waveIndex >= waveDecisions.Count) continue;
                        WaveSegmentDecision[] segments = waveDecisions[waveIndex].Segments;
                        if (segments == null || reference.SegmentIndex >= segments.Length) continue;
                        WaveExactSegmentChecks++;
                        WaveSegmentDecision segment = segments[reference.SegmentIndex];
                        if (!segment.Active) continue;
                        float distanceSquared = (segment.Position - position).sqrMagnitude;
                        if (distanceSquared >= nearestSquared) continue;
                        nearest = reference.SegmentIndex;
                        nearestSquared = distanceSquared;
                    }
                    AccumulateNearestWave(waves, waveDecisions, waveIndex, nearest,
                        nearestSquared, radius, ref drift, ref breakingWaveId,
                        ref breakingSegmentIndex, ref breakingForce, ref breakingDirection);
                }
            }
            else
            {
                for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
                {
                    WaveSegmentDecision[] segments = waveDecisions[waveIndex].Segments;
                    if (segments == null) continue;
                    int nearest = -1;
                    float nearestSquared = radiusSquared;
                    for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                    {
                        WaveExactSegmentChecks++;
                        if (!segments[segmentIndex].Active) continue;
                        float distanceSquared = (segments[segmentIndex].Position - position).sqrMagnitude;
                        if (distanceSquared >= nearestSquared) continue;
                        nearest = segmentIndex;
                        nearestSquared = distanceSquared;
                    }
                    AccumulateNearestWave(waves, waveDecisions, waveIndex, nearest,
                        nearestSquared, radius, ref drift, ref breakingWaveId,
                        ref breakingSegmentIndex, ref breakingForce, ref breakingDirection);
                }
            }
            return new FloatingWaveSample(Vector2.ClampMagnitude(drift, 8f),
                breakingWaveId, breakingSegmentIndex, breakingForce, breakingDirection);
        }

        private static void AccumulateNearestWave(IReadOnlyList<WaveData> waves,
            IReadOnlyList<WaveDecision> waveDecisions, int waveIndex, int nearest,
            float nearestSquared, float radius, ref Vector2 drift, ref int breakingWaveId,
            ref int breakingSegmentIndex, ref float breakingForce,
            ref Vector2 breakingDirection)
        {
            if (nearest < 0 || waveIndex < 0 || waveIndex >= waves.Count ||
                waveIndex >= waveDecisions.Count) return;
            WaveSegmentDecision[] segments = waveDecisions[waveIndex].Segments;
            if (segments == null || nearest >= segments.Length) return;
            WaveSegmentDecision local = segments[nearest];
            float proximity = 1f - Mathf.Sqrt(nearestSquared) / radius;
            float stateScale = local.State == WaveState.Breaking
                ? Mathf.Lerp(0.55f, 0.9f, 0.55f + local.BreakingIntensity * 0.45f) :
                local.State == WaveState.Spent ? 0.12f : 0.55f;
            float localForce = Mathf.Min(5f, local.InteractionForce) * proximity;
            drift += local.Direction * localForce * stateScale;
            if (local.State == WaveState.Breaking && localForce > breakingForce)
            {
                breakingWaveId = waves[waveIndex].Id;
                breakingSegmentIndex = nearest;
                breakingForce = localForce;
                breakingDirection = local.Direction;
            }
        }

        private bool TryFindSpawnPosition(IReadOnlyList<FloatingObjectData> objects,
            Vector2 playerPosition, int index, out Vector2 position)
        {
            Vector2 half = config.WorldHalfExtents;
            for (int attempt = 0; attempt < 96; attempt++)
            {
                if (index < 5)
                {
                    float angle = (index * 72f + attempt * 29f) * Mathf.Deg2Rad;
                    float distance = 10f + index * 2.4f + attempt * 0.5f;
                    position = playerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                }
                else
                {
                    position = new Vector2(random.Range(-half.x + 7f, half.x - 7f),
                        random.Range(-half.y + 7f, half.y - 7f));
                }
                if (!IsSafe(position, 1.8f)) continue;
                bool separated = true;
                for (int other = 0; other < objects.Count; other++)
                    if ((objects[other].Position - position).sqrMagnitude < 20f)
                    {
                        separated = false;
                        break;
                    }
                if (separated) return true;
            }
            position = Vector2.zero;
            return false;
        }

        private bool IsSafe(Vector2 position, float clearance)
        {
            Vector2 half = config.WorldHalfExtents;
            return Mathf.Abs(position.x) < half.x - clearance &&
                   Mathf.Abs(position.y) < half.y - clearance &&
                   !environment.IsLand(position) &&
                   environment.FindRock(position, clearance) < 0;
        }

        private int Add(List<FloatingObjectData> objects, FloatingObjectKind kind, Vector2 position)
        {
            int id = nextObjectId++;
            float radius = kind == FloatingObjectKind.Cargo
                ? random.Range(0.55f, 0.78f) : random.Range(0.92f, 1.45f);
            float value = kind == FloatingObjectKind.Cargo
                ? (random.Value() < 0.22f ? 2f : 1f) : 0f;
            objects.Add(new FloatingObjectData
            {
                Id = id,
                Kind = kind,
                PreviousPosition = position,
                Position = position,
                Velocity = random.InsideUnitCircle() * 0.08f,
                Radius = radius,
                Value = value,
                LastBreakingWaveId = 0,
                Active = true
            });
            return id;
        }

        private readonly struct FloatingWaveSample
        {
            public readonly Vector2 Drift;
            public readonly int BreakingWaveId;
            public readonly int BreakingSegmentIndex;
            public readonly float BreakingForce;
            public readonly Vector2 BreakingDirection;

            public FloatingWaveSample(Vector2 drift, int breakingWaveId,
                int breakingSegmentIndex, float breakingForce, Vector2 breakingDirection)
            {
                Drift = drift;
                BreakingWaveId = breakingWaveId;
                BreakingSegmentIndex = breakingSegmentIndex;
                BreakingForce = breakingForce;
                BreakingDirection = breakingDirection;
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            if (direction.sqrMagnitude < 0.0001f) return Vector2.right;
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }
    }
}

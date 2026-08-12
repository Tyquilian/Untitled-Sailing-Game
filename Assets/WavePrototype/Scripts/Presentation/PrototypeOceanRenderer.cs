using System.Collections.Generic;
using UnityEngine;
using WavePrototype.Simulation;

namespace WavePrototype.Presentation
{
    /// <summary>
    /// Owns all generated mesh resources and translates observed simulation snapshots into
    /// static bathymetry and interpolated actor geometry.
    /// </summary>
    internal sealed class PrototypeOceanRenderer
    {
        private readonly WaveSimulation simulation;
        private readonly PrototypeSnapshotBuffer snapshots;
        private readonly Mesh staticMesh;
        private readonly Mesh dynamicMesh;
        private readonly List<Vector3> staticVertices = new List<Vector3>(140000);
        private readonly List<Color32> staticColors = new List<Color32>(140000);
        private readonly List<int> staticTriangles = new List<int>(210000);
        private readonly List<Vector3> dynamicVertices = new List<Vector3>(24000);
        private readonly List<Color32> dynamicColors = new List<Color32>(24000);
        private readonly List<int> dynamicTriangles = new List<int>(40000);
        private float interpolationAlpha;
        private bool debugOverlay;

        public int StaticVertexCount => staticVertices.Count;
        public int DynamicVertexCount { get; private set; }

        public PrototypeOceanRenderer(Transform parent, WaveSimulation simulation,
            PrototypeSnapshotBuffer snapshots)
        {
            this.simulation = simulation;
            this.snapshots = snapshots;
            Shader shader = Shader.Find("WavePrototype/VertexColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = "Ocean Vertex Color Material" };

            staticMesh = new Mesh { name = "Static Ocean Geometry" };
            staticMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            GameObject staticObject = new GameObject("Static Bathymetry and Rocks");
            staticObject.transform.SetParent(parent, false);
            staticObject.AddComponent<MeshFilter>().sharedMesh = staticMesh;
            MeshRenderer staticRenderer = staticObject.AddComponent<MeshRenderer>();
            staticRenderer.sharedMaterial = material;
            staticRenderer.sortingOrder = 0;

            dynamicMesh = new Mesh { name = "Interpolated Ocean Actors" };
            dynamicMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            dynamicMesh.MarkDynamic();
            GameObject dynamicObject = new GameObject("Dynamic Waves and Boats");
            dynamicObject.transform.SetParent(parent, false);
            dynamicObject.AddComponent<MeshFilter>().sharedMesh = dynamicMesh;
            MeshRenderer dynamicRenderer = dynamicObject.AddComponent<MeshRenderer>();
            dynamicRenderer.sharedMaterial = material;
            dynamicRenderer.sortingOrder = 1;
        }

        public void RebuildStatic()
        {
            staticVertices.Clear();
            staticColors.Clear();
            staticTriangles.Clear();
            AddBathymetry();
            for (int i = 0; i < simulation.Environment.Rocks.Count; i++)
            {
                RockData rock = simulation.Environment.Rocks[i];
                AddCircle(staticVertices, staticColors, staticTriangles, rock.Position,
                    rock.Radius * 1.12f, new Color(0.075f, 0.085f, 0.082f), 0.16f, 14);
                AddCircle(staticVertices, staticColors, staticTriangles,
                    rock.Position + Vector2.one * 0.1f, rock.Radius * 0.72f,
                    new Color(0.31f, 0.32f, 0.29f), 0.12f, 12);
            }
            staticMesh.Clear(true);
            staticMesh.SetVertices(staticVertices);
            staticMesh.SetColors(staticColors);
            staticMesh.SetTriangles(staticTriangles, 0, false);
            ApplyWorldMeshBounds(staticMesh);
        }

        public void BuildDynamic(float alpha, bool showDebugOverlay, bool showTargetBearing)
        {
            interpolationAlpha = alpha;
            debugOverlay = showDebugOverlay;
            dynamicVertices.Clear();
            dynamicColors.Clear();
            dynamicTriangles.Clear();

            if (debugOverlay)
            {
                AddSwellStructureBands(alpha);
                AddWaveSourceDiagnostics();
            }

            for (int i = 0; i < simulation.Waves.Count; i++)
                AddWave(snapshots.GetWave(simulation.Waves[i], alpha));

            if (simulation.Target.Enabled) AddTargetMarker(simulation.Target);

            for (int i = 0; i < simulation.FloatingObjects.Count; i++)
                AddFloatingObject(simulation.FloatingObjects[i], alpha);

            if (debugOverlay)
            {
                for (int i = 0; i < simulation.Environment.Rocks.Count; i++)
                {
                    RockData rock = simulation.Environment.Rocks[i];
                    AddCircle(dynamicVertices, dynamicColors, dynamicTriangles, rock.Position,
                        rock.Radius + 1.15f, new Color(0.95f, 0.2f, 0.12f, 0.16f), 0.31f, 14);
                }
            }

            BoatData player = default;
            if (simulation.Boats.Count > 0)
            {
                player = snapshots.GetPlayer(simulation, alpha);
                VesselProfileDefinition playerProfile = simulation.Config.GetVesselProfile(player.Profile);
                float highlightRadius = Mathf.Max(2.05f, playerProfile.CollisionRadius + 0.65f);
                AddCircle(dynamicVertices, dynamicColors, dynamicTriangles, player.Position, highlightRadius,
                    new Color(1f, 0.62f, 0.08f, 0.16f), 0.1f, 20);
                if (showTargetBearing && simulation.Target.Enabled)
                    AddTargetBearingArrow(player.Position, simulation.Target.Position);
            }
            for (int i = 0; i < simulation.Boats.Count; i++)
                AddBoat(snapshots.GetBoat(simulation.Boats[i], alpha), i == 0);

            if (debugOverlay && simulation.Boats.Count > 0)
            {
                Vector2 waveForce = simulation.SampleAmbientWaveField(player.Position);
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, player.Position,
                    waveForce * 0.42f, new Color(0.98f, 0.24f, 0.14f, 0.86f), 0.04f);
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, player.Position,
                    simulation.WindVelocity * 0.72f,
                    new Color(1f, 0.86f, 0.28f, 0.86f), 0.05f);
            }

            DynamicVertexCount = dynamicVertices.Count;
            dynamicMesh.Clear(true);
            dynamicMesh.SetVertices(dynamicVertices);
            dynamicMesh.SetColors(dynamicColors);
            dynamicMesh.SetTriangles(dynamicTriangles, 0, false);
            ApplyWorldMeshBounds(dynamicMesh);
        }

        private void AddWaveSourceDiagnostics()
        {
            for (int i = 0; i < simulation.WaveSources.Count; i++)
            {
                WaveSourceData source = simulation.WaveSources[i];
                Color color = source.Enabled ? SourceColor(source.Kind)
                    : new Color(0.42f, 0.48f, 0.5f, 0.08f);
                Vector2 segment = source.SegmentEnd - source.SegmentStart;
                float angle = Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg;
                Vector2 midpoint = (source.SegmentStart + source.SegmentEnd) * 0.5f;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, midpoint,
                    new Vector2(segment.magnitude, 0.22f), angle, color, 0.27f);
                if (!source.Enabled) continue;
                bool directionalEntryShown = false;
                for (int systemIndex = simulation.SwellSystems.Count - 1;
                     systemIndex >= 0; systemIndex--)
                {
                    SwellSystemData system = simulation.SwellSystems[systemIndex];
                    if (system.SourceId != source.Id || !system.UsesDirectionalBoundaryEntry)
                        continue;
                    Vector2 crestAxis = new Vector2(-system.Direction.y, system.Direction.x);
                    float crestAngle = Mathf.Atan2(crestAxis.y, crestAxis.x) * Mathf.Rad2Deg;
                    AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                        system.EmissionCenter,
                        new Vector2(system.MeanCrestLength, 0.28f), crestAngle,
                        new Color(color.r, color.g, color.b, 0.26f), 0.275f);
                    AddCircle(dynamicVertices, dynamicColors, dynamicTriangles,
                        system.BoundaryEntryPoint, 2.4f,
                        new Color(color.r, color.g, color.b, 0.72f), 0.28f, 16);
                    AddVector(dynamicVertices, dynamicColors, dynamicTriangles,
                        system.BoundaryEntryPoint, system.Direction * 9f,
                        color, 0.285f, 0.18f);
                    directionalEntryShown = true;
                    break;
                }
                if (!directionalEntryShown)
                    AddVector(dynamicVertices, dynamicColors, dynamicTriangles, midpoint,
                        source.Direction * 7f, color, 0.28f, 0.18f);
            }
        }

        private void AddSwellStructureBands(float alpha)
        {
            for (int systemIndex = 0; systemIndex < simulation.SwellSystems.Count; systemIndex++)
            {
                SwellSystemData system = simulation.SwellSystems[systemIndex];
                Vector2 direction = system.Direction.sqrMagnitude < 0.001f
                    ? Vector2.right : system.Direction.normalized;
                Vector2 crestAxis = new Vector2(-direction.y, direction.x);
                float minimumProjection = float.MaxValue;
                float maximumProjection = float.MinValue;
                float lateralSum = 0f;
                int count = 0;
                for (int waveIndex = 0; waveIndex < simulation.Waves.Count; waveIndex++)
                {
                    WaveData authoritative = simulation.Waves[waveIndex];
                    if (authoritative.SwellSystemId != system.Id) continue;
                    WaveData wave = snapshots.GetWave(authoritative, alpha);
                    float projection = Vector2.Dot(wave.Position, direction);
                    minimumProjection = Mathf.Min(minimumProjection, projection);
                    maximumProjection = Mathf.Max(maximumProjection, projection);
                    lateralSum += Vector2.Dot(wave.Position, crestAxis);
                    count++;
                }
                if (count < 2) continue;
                float centerProjection = (minimumProjection + maximumProjection) * 0.5f;
                Vector2 center = direction * centerProjection + crestAxis * (lateralSum / count);
                float length = maximumProjection - minimumProjection + system.PacketSpacing * 0.72f;
                float width = Mathf.Max(5f, system.MeanCrestLength * 1.12f);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, center,
                    new Vector2(length, width), angle,
                    SwellColor(system.SourceId, system.Id, 0.13f), 0.64f);
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, center,
                    direction * Mathf.Min(10f, length * 0.35f),
                    SwellColor(system.SourceId, system.Id, 0.68f), 0.6f, 0.18f);
            }
        }

        private static Color SwellColor(int sourceId, int systemId, float alpha)
        {
            Color baseColor;
            if (sourceId == 1) baseColor = new Color(0.26f, 0.72f, 0.92f);
            else if (sourceId == 2) baseColor = new Color(0.62f, 0.54f, 0.94f);
            else baseColor = new Color(0.28f, 0.88f, 0.72f);
            float variation = ((systemId * 37) % 7) / 6f;
            baseColor = Color.Lerp(baseColor, Color.white, variation * 0.16f);
            baseColor.a = alpha;
            return baseColor;
        }

        private static Color SourceColor(WaveSourceKind kind)
        {
            switch (kind)
            {
                case WaveSourceKind.WesternSwell: return new Color(0.28f, 1f, 0.68f, 0.42f);
                case WaveSourceKind.NorthernCrossSea: return new Color(0.85f, 0.42f, 1f, 0.42f);
                default: return new Color(1f, 0.58f, 0.26f, 0.42f);
            }
        }

        private void AddTargetMarker(TargetMarkerData target)
        {
            Color ring = new Color(1f, 0.76f, 0.12f, 0.82f);
            AddRing(dynamicVertices, dynamicColors, dynamicTriangles, target.Position,
                target.VisitRadius, 0.26f, ring, 0.68f, 40);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.12f;
            AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, target.Position,
                Vector2.one * 2.4f * pulse, 45f,
                new Color(1f, 0.86f, 0.22f, 0.96f), 0.69f);
            AddVector(dynamicVertices, dynamicColors, dynamicTriangles,
                target.Position + Vector2.down * 3.5f, Vector2.up * 2.2f,
                new Color(1f, 0.92f, 0.52f, 0.9f), 0.7f, 0.18f);
        }

        private void AddTargetBearingArrow(Vector2 boatPosition, Vector2 targetPosition)
        {
            Vector2 direction = targetPosition - boatPosition;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            Vector2 start = boatPosition + direction * 3f;
            AddArrow(dynamicVertices, dynamicColors, dynamicTriangles, start,
                direction * 5.5f, new Color(1f, 0.82f, 0.18f, 0.86f), 0.72f, 0.2f);
        }

        private void AddFloatingObject(FloatingObjectData item, float alpha)
        {
            Vector2 position = Vector2.Lerp(item.PreviousPosition, item.Position, alpha);
            if (item.Kind == FloatingObjectKind.Cargo)
            {
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    Vector2.one * item.Radius * 1.75f, 45f,
                    new Color(0.94f, 0.64f, 0.12f, 0.98f), 0.7f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 1.45f, 0.16f), 45f,
                    new Color(1f, 0.9f, 0.48f, 0.94f), 0.71f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 1.45f, 0.16f), -45f,
                    new Color(1f, 0.9f, 0.48f, 0.94f), 0.71f);
            }
            else
            {
                float angle = item.Id * 37f + item.Position.x * 0.4f;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    new Vector2(item.Radius * 2.25f, item.Radius * 0.62f), angle,
                    new Color(0.34f, 0.22f, 0.12f, 0.98f), 0.69f);
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                    position + new Vector2(0.18f, 0.14f),
                    new Vector2(item.Radius * 1.65f, item.Radius * 0.34f), angle + 67f,
                    new Color(0.5f, 0.34f, 0.18f, 0.95f), 0.7f);
            }
            if (debugOverlay)
                AddVector(dynamicVertices, dynamicColors, dynamicTriangles, position,
                    item.Velocity * 1.4f, new Color(1f, 0.74f, 0.28f, 0.72f), 0.73f, 0.08f);
        }

        private void AddBathymetry()
        {
            Vector2 half = simulation.Config.WorldHalfExtents;
            int cellsX = Mathf.CeilToInt(half.x * 0.5f);
            int cellsY = Mathf.CeilToInt(half.y * 0.5f);
            float width = half.x * 2f / cellsX;
            float height = half.y * 2f / cellsY;
            for (int y = 0; y < cellsY; y++)
            {
                float centerY = -half.y + (y + 0.5f) * height;
                for (int x = 0; x < cellsX; x++)
                {
                    float centerX = -half.x + (x + 0.5f) * width;
                    Vector2 center = new Vector2(centerX, centerY);
                    AddQuad(staticVertices, staticColors, staticTriangles, center,
                        new Vector2(width + 0.04f, height + 0.04f), 0f,
                        DepthColor(simulation.Environment.SampleDepth(center)), 2f);
                }
            }
        }

        private static Color DepthColor(float depth)
        {
            if (depth <= 0.24f) return new Color(0.12f, 0.17f, 0.125f);
            if (depth < 0.72f) return Color.Lerp(new Color(0.36f, 0.42f, 0.28f),
                new Color(0.08f, 0.46f, 0.42f), depth / 0.72f);
            if (depth < 3.5f) return Color.Lerp(new Color(0.07f, 0.5f, 0.45f),
                new Color(0.025f, 0.31f, 0.4f), (depth - 0.72f) / 2.78f);
            if (depth < 6.5f) return Color.Lerp(new Color(0.025f, 0.3f, 0.4f),
                new Color(0.018f, 0.18f, 0.31f), Mathf.InverseLerp(3.5f, 6.5f, depth));
            return Color.Lerp(new Color(0.018f, 0.18f, 0.31f),
                new Color(0.012f, 0.075f, 0.15f), Mathf.InverseLerp(6.5f, 12f, depth));
        }

        private void AddWave(WaveData wave)
        {
            WaveSegmentCollection segments = wave.Segments;
            if (segments.Count == 0) return;
            float nominalSpacing = segments.Length == 1
                ? wave.CrestLength : wave.CrestLength / (segments.Length - 1f);
            float maximumLink = nominalSpacing * simulation.Config.WaveSegmentLinkBreakMultiplier;
            for (int index = 0; index < segments.Length; index++)
            {
                WaveSegmentData segment = segments[index];
                if (!segment.Active) continue;
                Vector2 position = InterpolatedSegmentPosition(segment, interpolationAlpha);
                bool linkLeft = index > 0 && segments[index - 1].Active &&
                    Vector2.Distance(position, InterpolatedSegmentPosition(
                        segments[index - 1], interpolationAlpha)) <= maximumLink;
                bool linkRight = index + 1 < segments.Length && segments[index + 1].Active &&
                    Vector2.Distance(position, InterpolatedSegmentPosition(
                        segments[index + 1], interpolationAlpha)) <= maximumLink;
                Vector2 left = linkLeft ? InterpolatedSegmentPosition(
                    segments[index - 1], interpolationAlpha) : position;
                Vector2 right = linkRight ? InterpolatedSegmentPosition(
                    segments[index + 1], interpolationAlpha) : position;
                Vector2 tangent = linkLeft && linkRight ? right - left
                    : linkLeft ? position - left
                    : linkRight ? right - position
                    : new Vector2(-segment.TravelDirection.y, segment.TravelDirection.x);
                if (tangent.sqrMagnitude < 0.0001f)
                    tangent = new Vector2(-segment.TravelDirection.y, segment.TravelDirection.x);
                tangent.Normalize();

                float leftExtent = linkLeft ? Vector2.Distance(position, left) * 0.5f
                    : (index == 0 ? nominalSpacing * 0.5f : nominalSpacing * 0.24f);
                float rightExtent = linkRight ? Vector2.Distance(position, right) * 0.5f
                    : (index == segments.Length - 1 ? nominalSpacing * 0.5f : nominalSpacing * 0.24f);
                if (segments.Length == 1) leftExtent = rightExtent = wave.CrestLength * 0.5f;
                float visualSpan = Mathf.Max(0.8f, leftExtent + rightExtent);
                Vector2 center = position + tangent * ((rightExtent - leftExtent) * 0.5f);

                WaveDerived derived = new WaveDerived(segment.Energy,
                    segment.SampledDepth, wave.PacketLength);
                float energy01 = Mathf.InverseLerp(0.08f, 3.2f, segment.Energy);
                Color color = SegmentColor(segment.State, energy01);
                float thickness = Mathf.Clamp(wave.PacketLength * 0.22f +
                    derived.Amplitude * 0.16f, 0.58f, 1.85f);
                float tangentAngle = Mathf.Atan2(-tangent.x, tangent.y) * Mathf.Rad2Deg;
                AddQuad(dynamicVertices, dynamicColors, dynamicTriangles, center,
                    new Vector2(thickness, visualSpan), tangentAngle, color, 0.52f);

                float shoaling = Mathf.InverseLerp(6.5f, 1.1f, segment.SampledDepth);
                if (segment.State == WaveState.Traveling && shoaling > 0.05f)
                    AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                        center - segment.TravelDirection * thickness * 0.72f,
                        new Vector2(thickness * 0.3f, visualSpan * 0.88f), tangentAngle,
                        new Color(0.72f, 0.96f, 0.91f, shoaling * 0.28f), 0.525f);

                float foam01 = Mathf.InverseLerp(simulation.Config.MinimumFoamEnergy,
                    0.45f, segment.FoamEnergy);
                if (segment.State == WaveState.Breaking)
                {
                    AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                        center - segment.TravelDirection * thickness * 0.9f,
                        new Vector2(thickness * 0.72f, visualSpan * 0.82f), tangentAngle,
                        new Color(0.94f, 1f, 0.94f,
                            0.3f + segment.BreakingIntensity * 0.38f), 0.53f);
                    AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                        center - segment.TravelDirection * thickness * 1.65f,
                        new Vector2(thickness * 0.52f, visualSpan * 0.68f), tangentAngle,
                        new Color(0.82f, 0.98f, 0.94f,
                            0.12f + Mathf.Max(segment.BreakingIntensity, foam01) * 0.28f), 0.54f);
                }
                else if (segment.FoamEnergy >= simulation.Config.MinimumFoamEnergy)
                    AddQuad(dynamicVertices, dynamicColors, dynamicTriangles,
                        center - segment.TravelDirection * thickness * 0.7f,
                        new Vector2(thickness * 0.34f, visualSpan * 0.7f), tangentAngle,
                        new Color(0.92f, 1f, 0.97f, 0.08f + foam01 * 0.26f), 0.54f);

                if (debugOverlay)
                    AddVector(dynamicVertices, dynamicColors, dynamicTriangles, position,
                        segment.TravelDirection * (1.2f + energy01 * 2.4f),
                        new Color(1f, 0.76f, 0.18f, 0.65f), 0.42f, 0.075f);
            }
        }

        private static Vector2 InterpolatedSegmentPosition(WaveSegmentData segment, float alpha)
            => Vector2.Lerp(segment.PreviousPosition, segment.Position, alpha);

        private Color SegmentColor(WaveState state, float energy01)
        {
            if (state == WaveState.Breaking)
                return Color.Lerp(new Color(0.72f, 0.93f, 0.92f, 0.82f),
                    new Color(1f, 0.98f, 0.82f, 0.98f), energy01);
            if (state == WaveState.Spent) return new Color(0.92f, 1f, 0.97f, 0.3f);
            if (debugOverlay)
                return Color.Lerp(new Color(0.12f, 0.62f, 0.82f, 0.55f),
                    new Color(1f, 0.27f, 0.1f, 0.94f), energy01);
            return Color.Lerp(new Color(0.18f, 0.67f, 0.82f, 0.52f),
                new Color(0.66f, 0.94f, 1f, 0.92f), energy01);
        }

        private void AddBoat(BoatData boat, bool player)
        {
            VesselProfileDefinition profile = simulation.Config.GetVesselProfile(boat.Profile);
            float a = boat.Heading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 side = new Vector2(-forward.y, forward.x);
            Color hull = boat.Health > 35f
                ? (player ? new Color(1f, 0.55f, 0.08f)
                    : boat.Profile == VesselProfileId.HeavyCutter
                        ? new Color(0.48f, 0.58f, 0.59f)
                        : new Color(0.78f, 0.66f, 0.38f))
                : new Color(0.9f, 0.12f, 0.08f);
            AddTriangle(dynamicVertices, dynamicColors, dynamicTriangles,
                boat.Position + forward * (profile.HullLength * 0.593f),
                boat.Position - forward * (profile.HullLength * 0.407f) +
                    side * (profile.HullBeam * 0.5f),
                boat.Position - forward * (profile.HullLength * 0.407f) -
                    side * (profile.HullBeam * 0.5f),
                hull, 0.025f);
            Color sail = player ? new Color(1f, 0.92f, 0.72f, 0.95f)
                : new Color(0.78f, 0.84f, 0.78f, 0.86f);
            AddTriangle(dynamicVertices, dynamicColors, dynamicTriangles,
                boat.Position + forward * (profile.HullLength * 0.322f),
                boat.Position - forward * (profile.HullLength * 0.244f) +
                    side * (profile.HullBeam * 0.11f),
                boat.Position - forward * (profile.HullLength * 0.231f) +
                    side * (profile.HullBeam * 0.659f),
                sail, 0.015f);

            if (!debugOverlay) return;
            Color sampleColor = new Color(1f, 0.3f, 0.12f, 0.78f);
            for (int sample = 0; sample < Mathf.Max(1, profile.HullSampleCount); sample++)
            {
                Vector2 samplePosition = VesselProfiles.GetHullSampleWorldPosition(
                    boat, profile, sample);
                AddCircle(dynamicVertices, dynamicColors, dynamicTriangles, samplePosition,
                    0.19f, sampleColor, 0.32f, 8);
            }
        }

        private void ApplyWorldMeshBounds(Mesh targetMesh)
        {
            Vector2 half = simulation.Config.WorldHalfExtents;
            targetMesh.bounds = new Bounds(Vector3.zero,
                new Vector3(half.x * 2f + 12f, half.y * 2f + 12f, 8f));
        }

        private static void AddTriangle(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 a, Vector2 b, Vector2 d, Color color, float z)
        {
            int start = v.Count;
            Color32 value = color;
            v.Add((Vector3)a + Vector3.forward * z); c.Add(value);
            v.Add((Vector3)b + Vector3.forward * z); c.Add(value);
            v.Add((Vector3)d + Vector3.forward * z); c.Add(value);
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
        }

        private static void AddVector(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 start, Vector2 vector, Color color, float z, float width = 0.14f)
        {
            float length = vector.magnitude;
            if (length < 0.08f) return;
            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            AddQuad(v, c, t, start + vector * 0.5f,
                new Vector2(length, width), angle, color, z);
        }

        private static void AddArrow(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 start, Vector2 vector, Color color, float z, float width)
        {
            float length = vector.magnitude;
            if (length < 0.2f) return;
            Vector2 direction = vector / length;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float headLength = Mathf.Min(1.35f, length * 0.32f);
            Vector2 tip = start + vector;
            AddVector(v, c, t, start, direction * (length - headLength * 0.45f),
                color, z, width);
            AddTriangle(v, c, t, tip,
                tip - direction * headLength + side * headLength * 0.55f,
                tip - direction * headLength - side * headLength * 0.55f,
                color, z - 0.001f);
        }

        private static void AddQuad(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 center, Vector2 size, float degrees, Color color, float z)
        {
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * size.x * 0.5f;
            Vector2 up = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians)) * size.y * 0.5f;
            int start = v.Count;
            Color32 value = color;
            v.Add((Vector3)(center - right - up) + Vector3.forward * z); c.Add(value);
            v.Add((Vector3)(center + right - up) + Vector3.forward * z); c.Add(value);
            v.Add((Vector3)(center + right + up) + Vector3.forward * z); c.Add(value);
            v.Add((Vector3)(center - right + up) + Vector3.forward * z); c.Add(value);
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }

        private static void AddRing(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 center, float radius, float thickness, Color color, float z, int segments)
        {
            float segmentLength = Mathf.Max(thickness,
                2f * Mathf.PI * radius / segments * 1.08f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                float radians = angle * Mathf.Deg2Rad;
                Vector2 position = center +
                    new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                AddQuad(v, c, t, position, new Vector2(segmentLength, thickness),
                    angle + 90f, color, z);
            }
        }

        private static void AddCircle(List<Vector3> v, List<Color32> c, List<int> t,
            Vector2 center, float radius, Color color, float z, int segments)
        {
            int start = v.Count;
            Color32 value = color;
            v.Add((Vector3)center + Vector3.forward * z); c.Add(value);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add((Vector3)(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius) + Vector3.forward * z); c.Add(value);
            }
            for (int i = 0; i < segments; i++)
            {
                t.Add(start); t.Add(start + i + 2); t.Add(start + i + 1);
            }
        }
    }
}

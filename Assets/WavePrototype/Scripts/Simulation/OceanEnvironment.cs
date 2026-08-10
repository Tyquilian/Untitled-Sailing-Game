using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace WavePrototype.Simulation
{
    public interface IOceanEnvironment
    {
        IReadOnlyList<RockData> Rocks { get; }
        float SampleDepth(Vector2 position);
        bool IsLand(Vector2 position);
        Vector2 SampleDepthGradient(Vector2 position);
        int FindRock(Vector2 position, float extraRadius);
    }

    public interface IOceanEnvironmentFactory
    {
        IOceanEnvironment Create(Vector2 worldHalfExtents, int seed);
    }

    /// <summary>
    /// Optional deterministic static-rock broadphase. Environments that do not implement
    /// this interface retain the exact brute-force swept-contact fallback.
    /// </summary>
    public interface IRockSpatialQuery
    {
        float MaximumRockRadius { get; }
        int OccupiedRockCellCount { get; }
        void QueryRockIndices(Vector2 minimum, Vector2 maximum, List<int> results);
    }

    public sealed class OceanEnvironmentFactory : IOceanEnvironmentFactory
    {
        public IOceanEnvironment Create(Vector2 worldHalfExtents, int seed)
            => new OceanEnvironment(worldHalfExtents, seed);
    }

    public readonly struct RockData
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public RockData(Vector2 position, float radius) { Position = position; Radius = radius; }
    }

    public sealed class OceanEnvironment : IOceanEnvironment, IRockSpatialQuery
    {
        private const float RockGridCellSize = 8f;
        private const float DepthGridCellSize = 2f;
        private readonly List<RockData> rocks = new List<RockData>();
        private readonly Dictionary<Vector2Int, List<int>> rockGrid = new Dictionary<Vector2Int, List<int>>();
        private readonly Vector2 halfExtents;
        private readonly float[] depthGrid;
        private readonly int depthGridWidth;
        private readonly int depthGridHeight;
        private readonly ReadOnlyCollection<RockData> rockView;
        public IReadOnlyList<RockData> Rocks => rockView;
        public float MaximumRockRadius { get; private set; }
        public int OccupiedRockCellCount => rockGrid.Count;

        public OceanEnvironment(Vector2 worldHalfExtents, int seed)
        {
            halfExtents = worldHalfExtents;
            rockView = rocks.AsReadOnly();
            depthGridWidth = Mathf.CeilToInt(halfExtents.x * 2f / DepthGridCellSize) + 1;
            depthGridHeight = Mathf.CeilToInt(halfExtents.y * 2f / DepthGridCellSize) + 1;
            depthGrid = new float[depthGridWidth * depthGridHeight];
            BuildDepthGrid();
            GenerateRocks(seed);
            BuildRockGrid();
        }

        // Broad deterministic bathymetry. Deep water is intentionally quiet: the useful
        // structure is a continental margin, insular shelves, and a few navigational shoals.
        public float SampleDepth(Vector2 position)
        {
            float gridX = Mathf.Clamp((position.x + halfExtents.x) / DepthGridCellSize,
                0f, depthGridWidth - 1f);
            float gridY = Mathf.Clamp((position.y + halfExtents.y) / DepthGridCellSize,
                0f, depthGridHeight - 1f);
            int x0 = Mathf.FloorToInt(gridX);
            int y0 = Mathf.FloorToInt(gridY);
            int x1 = Mathf.Min(x0 + 1, depthGridWidth - 1);
            int y1 = Mathf.Min(y0 + 1, depthGridHeight - 1);
            float tx = gridX - x0;
            float ty = gridY - y0;
            float lower = Mathf.Lerp(depthGrid[y0 * depthGridWidth + x0],
                depthGrid[y0 * depthGridWidth + x1], tx);
            float upper = Mathf.Lerp(depthGrid[y1 * depthGridWidth + x0],
                depthGrid[y1 * depthGridWidth + x1], tx);
            return Mathf.Lerp(lower, upper, ty);
        }

        private float EvaluateDepth(Vector2 position)
        {
            // Authoring coordinates are normalized to the proven Batch 6 shelf layout.
            // Enlarging the world therefore stretches coherent regions instead of exposing
            // a noisy procedural fringe or changing their basic navigational character.
            Vector2 point = new Vector2(
                position.x * 180f / Mathf.Max(1f, halfExtents.x),
                position.y * 100f / Mathf.Max(1f, halfExtents.y));
            float depth = 11.2f;

            // A large eastern continental margin. The irregularity is broad enough to read
            // as coastline rather than procedural noise; the shelf reaches far into the sea.
            float coastX = 180f - 28f
                + Mathf.Sin(point.y * 0.032f) * 8f
                + Mathf.Sin(point.y * 0.081f + 0.7f) * 3.5f;
            depth = Mathf.Min(depth, ContinentalShelfDepth(coastX - point.x));

            // Insular shelves are larger than their exposed islands. Overlapping ellipses
            // form recognizable groups while leaving a mostly open central basin.
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(-82f, 34f), 19f, 11f, -12f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(-105f, 48f), 9f, 6f, 18f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(-59f, 54f), 8f, 5.5f, -24f));

            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(6f, -28f), 21f, 12f, 17f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(31f, -18f), 8.5f, 5.5f, -8f));

            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(72f, 55f), 14f, 9f, -20f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(94f, 66f), 7f, 5f, 11f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(102f, -53f), 16f, 9f, 24f));
            depth = Mathf.Min(depth, IslandShelfDepth(point, new Vector2(-37f, 69f), 10f, 6.5f, 5f));

            // Two submerged shelf ridges provide shoaling corridors without adding more land.
            depth = Mathf.Min(depth, 3.6f + (1f - Gaussian(point,
                new Vector2(-25f, 15f), 48f, 8f, 22f)) * 7.6f);
            depth = Mathf.Min(depth, 4.7f + (1f - Gaussian(point,
                new Vector2(68f, -68f), 39f, 7f, -9f)) * 6.5f);

            return Mathf.Clamp(depth, 0.08f, 12f);
        }

        private void BuildDepthGrid()
        {
            for (int y = 0; y < depthGridHeight; y++)
            {
                float worldY = Mathf.Min(halfExtents.y,
                    -halfExtents.y + y * DepthGridCellSize);
                for (int x = 0; x < depthGridWidth; x++)
                {
                    float worldX = Mathf.Min(halfExtents.x,
                        -halfExtents.x + x * DepthGridCellSize);
                    depthGrid[y * depthGridWidth + x] = EvaluateDepth(new Vector2(worldX, worldY));
                }
            }
        }

        public bool IsLand(Vector2 position) => SampleDepth(position) <= 0.24f;

        public Vector2 SampleDepthGradient(Vector2 position)
        {
            const float offset = 0.6f;
            float dx = SampleDepth(position + Vector2.right * offset) - SampleDepth(position - Vector2.right * offset);
            float dy = SampleDepth(position + Vector2.up * offset) - SampleDepth(position - Vector2.up * offset);
            return new Vector2(dx, dy) / (offset * 2f);
        }

        public int FindRock(Vector2 position, float extraRadius)
        {
            Vector2Int center = RockCell(position);
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (!rockGrid.TryGetValue(center + new Vector2Int(x, y), out List<int> indices)) continue;
                for (int item = 0; item < indices.Count; item++)
                {
                    int i = indices[item];
                    float radius = rocks[i].Radius + extraRadius;
                    if ((position - rocks[i].Position).sqrMagnitude <= radius * radius) return i;
                }
            }
            return -1;
        }

        public void QueryRockIndices(Vector2 minimum, Vector2 maximum, List<int> results)
        {
            results.Clear();
            Vector2Int first = RockCell(Vector2.Min(minimum, maximum));
            Vector2Int last = RockCell(Vector2.Max(minimum, maximum));
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                if (!rockGrid.TryGetValue(new Vector2Int(x, y),
                        out List<int> indices)) continue;
                for (int item = 0; item < indices.Count; item++) results.Add(indices[item]);
            }
            if (results.Count > 1) results.Sort();
        }

        private void GenerateRocks(int seed)
        {
            var random = new DeterministicRandom(seed ^ 0x5A17);
            var centers = new List<Vector2>(48);
            float mapScale = Mathf.Sqrt((halfExtents.x * halfExtents.y) / (180f * 100f));

            for (int attempt = 0; attempt < 6800 && centers.Count < 46; attempt++)
            {
                Vector2 candidate = new Vector2(
                    random.Range(-halfExtents.x + 7f, halfExtents.x - 7f),
                    random.Range(-halfExtents.y + 7f, halfExtents.y - 7f));
                float depth = SampleDepth(candidate);
                float slope = SampleDepthGradient(candidate).magnitude;
                if (depth < 0.28f || depth > 3.35f || slope < 0.032f) continue;
                bool separated = true;
                for (int i = 0; i < centers.Count; i++)
                    if ((centers[i] - candidate).sqrMagnitude < 92f * mapScale * mapScale) { separated = false; break; }
                if (separated) centers.Add(candidate);
            }

            for (int centerIndex = 0; centerIndex < centers.Count; centerIndex++)
            {
                int count = 7 + (int)(random.Value() * 10f);
                float spread = random.Range(3f, 7.4f) * mapScale;
                for (int i = 0; i < count; i++)
                {
                    Vector2 candidate = centers[centerIndex] + random.InsideUnitCircle() * spread;
                    float depth = SampleDepth(candidate);
                    if (depth <= 0.25f || depth > 3.6f) continue;
                    float radius = random.Range(0.5f, 1.62f) * Mathf.Lerp(1.22f, 0.78f, depth / 3.6f);
                    AddRockIfSeparated(candidate, radius);
                }
            }

            // A sparse contour sweep joins some clusters into shelf-edge reef lines.
            for (float y = -halfExtents.y + 3f; y < halfExtents.y - 3f && rocks.Count < 320; y += 3.4f)
            {
                for (float x = -halfExtents.x + 3f; x < halfExtents.x - 3f && rocks.Count < 320; x += 3.4f)
                {
                    Vector2 candidate = new Vector2(x, y) + random.InsideUnitCircle() * 1.05f;
                    float depth = SampleDepth(candidate);
                    float slope = SampleDepthGradient(candidate).magnitude;
                    if (depth > 0.28f && depth < 3.5f && slope > 0.026f && random.Value() < 0.34f)
                        AddRockIfSeparated(candidate, random.Range(0.4f, 1.08f));
                }
            }
        }

        private void BuildRockGrid()
        {
            rockGrid.Clear();
            MaximumRockRadius = 0f;
            for (int i = 0; i < rocks.Count; i++)
            {
                MaximumRockRadius = Mathf.Max(MaximumRockRadius, rocks[i].Radius);
                Vector2Int cell = RockCell(rocks[i].Position);
                if (!rockGrid.TryGetValue(cell, out List<int> indices))
                {
                    indices = new List<int>(8);
                    rockGrid.Add(cell, indices);
                }
                indices.Add(i);
            }
        }

        private static Vector2Int RockCell(Vector2 position)
            => new Vector2Int(Mathf.FloorToInt(position.x / RockGridCellSize),
                Mathf.FloorToInt(position.y / RockGridCellSize));

        private void AddRockIfSeparated(Vector2 position, float radius)
        {
            for (int i = 0; i < rocks.Count; i++)
            {
                float minimum = rocks[i].Radius + radius + 0.08f;
                if ((rocks[i].Position - position).sqrMagnitude < minimum * minimum) return;
            }
            rocks.Add(new RockData(position, radius));
        }

        private static float Gaussian(Vector2 point, Vector2 center, float radiusX, float radiusY, float degrees)
        {
            Vector2 delta = point - center;
            float radians = -degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            float x = delta.x * cosine - delta.y * sine;
            float y = delta.x * sine + delta.y * cosine;
            float normalized = x * x / (radiusX * radiusX) + y * y / (radiusY * radiusY);
            return Mathf.Exp(-normalized * 1.65f);
        }

        private static float ContinentalShelfDepth(float oceanDistance)
        {
            if (oceanDistance <= 0f) return 0.08f;
            if (oceanDistance < 12f)
                return Mathf.Lerp(0.08f, 0.9f, Smooth01(oceanDistance / 12f));
            if (oceanDistance < 72f)
                return Mathf.Lerp(0.9f, 2.8f, Smooth01((oceanDistance - 12f) / 60f));
            return Mathf.Lerp(2.8f, 11.2f, Smooth01((oceanDistance - 72f) / 60f));
        }

        private static float IslandShelfDepth(Vector2 point, Vector2 center,
            float radiusX, float radiusY, float degrees)
        {
            Vector2 delta = point - center;
            float radians = -degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            float x = delta.x * cosine - delta.y * sine;
            float y = delta.x * sine + delta.y * cosine;
            float radius = Mathf.Sqrt(x * x / (radiusX * radiusX) + y * y / (radiusY * radiusY));
            if (radius <= 0.78f) return 0.08f;
            if (radius < 1.08f)
                return Mathf.Lerp(0.08f, 0.9f, Smooth01((radius - 0.78f) / 0.3f));
            if (radius < 1.75f)
                return Mathf.Lerp(0.9f, 3.2f, Smooth01((radius - 1.08f) / 0.67f));
            return Mathf.Lerp(3.2f, 11.2f, Smooth01((radius - 1.75f) / 1.05f));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}

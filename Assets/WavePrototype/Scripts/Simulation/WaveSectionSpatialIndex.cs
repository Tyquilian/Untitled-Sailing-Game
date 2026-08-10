using System.Collections.Generic;
using UnityEngine;

namespace WavePrototype.Simulation
{
    internal readonly struct WaveSectionReference
    {
        public readonly int WaveIndex;
        public readonly int SegmentIndex;

        public WaveSectionReference(int waveIndex, int segmentIndex)
        {
            WaveIndex = waveIndex;
            SegmentIndex = segmentIndex;
        }
    }

    /// <summary>
    /// Reusable deterministic grid over predicted wave-section positions. Cell lookup only
    /// removes impossible candidates; consumers retain their original exact equations and
    /// authoritative wave/segment ordering.
    /// </summary>
    internal sealed class WaveSectionSpatialIndex
    {
        private sealed class ReferenceComparer : IComparer<WaveSectionReference>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public int Compare(WaveSectionReference left, WaveSectionReference right)
            {
                int wave = left.WaveIndex.CompareTo(right.WaveIndex);
                return wave != 0 ? wave : left.SegmentIndex.CompareTo(right.SegmentIndex);
            }
        }

        private readonly List<WaveSectionReference>[] cells;
        private readonly List<int> occupiedCellIndices = new List<int>(512);
        private readonly float cellSize;
        private readonly Vector2 minimum;
        private readonly int width;
        private readonly int height;

        public int IndexedSectionCount { get; private set; }
        public int OccupiedCellCount { get; private set; }
        public int QueryCount { get; private set; }
        public int CandidateReferenceCount { get; private set; }
        public float MaximumBoatContactRadius { get; private set; }
        public float MaximumDecisionOffset { get; private set; }

        public WaveSectionSpatialIndex(float cellSize, Vector2 worldHalfExtents)
        {
            this.cellSize = Mathf.Max(4f, cellSize);
            minimum = -worldHalfExtents;
            width = Mathf.Max(1, Mathf.CeilToInt(worldHalfExtents.x * 2f /
                this.cellSize) + 1);
            height = Mathf.Max(1, Mathf.CeilToInt(worldHalfExtents.y * 2f /
                this.cellSize) + 1);
            cells = new List<WaveSectionReference>[width * height];
        }

        public void Build(IReadOnlyList<WaveData> waves,
            IReadOnlyList<WaveDecision> decisions, SimulationConfig config)
        {
            for (int occupied = 0; occupied < occupiedCellIndices.Count; occupied++)
                cells[occupiedCellIndices[occupied]].Clear();
            occupiedCellIndices.Clear();

            IndexedSectionCount = 0;
            OccupiedCellCount = 0;
            QueryCount = 0;
            CandidateReferenceCount = 0;
            MaximumBoatContactRadius = 0f;
            MaximumDecisionOffset = 0f;

            int waveCount = Mathf.Min(waves.Count, decisions.Count);
            for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
            {
                WaveData wave = waves[waveIndex];
                WaveSegmentData[] authoritative = wave.MutableSegments;
                WaveSegmentDecision[] predicted = decisions[waveIndex].Segments;
                if (authoritative == null || predicted == null) continue;
                int segmentCount = Mathf.Min(authoritative.Length, predicted.Length);
                float segmentSpan = authoritative.Length <= 1
                    ? wave.CrestLength
                    : wave.CrestLength / (authoritative.Length - 1f);

                for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    WaveSegmentDecision segment = predicted[segmentIndex];
                    if (!segment.Active) continue;
                    Vector2 interactionPosition = Vector2.Lerp(
                        authoritative[segmentIndex].Position, segment.Position, 0.5f);
                    int cellIndex = CellIndex(interactionPosition);
                    List<WaveSectionReference> references = cells[cellIndex];
                    if (references == null)
                    {
                        references = new List<WaveSectionReference>(32);
                        cells[cellIndex] = references;
                    }
                    if (references.Count == 0)
                    {
                        occupiedCellIndices.Add(cellIndex);
                        OccupiedCellCount++;
                    }
                    references.Add(new WaveSectionReference(waveIndex, segmentIndex));
                    IndexedSectionCount++;

                    bool breaking = segment.State == WaveState.Breaking;
                    float alongRadius = breaking
                        ? wave.PacketLength * 0.62f + config.BoatInteractionRadius
                        : wave.PacketLength * config.TravelingLongitudinalScale +
                          config.TravelingLongitudinalPadding;
                    float acrossRadius = segmentSpan * 0.62f + config.BoatInteractionRadius;
                    MaximumBoatContactRadius = Mathf.Max(MaximumBoatContactRadius,
                        Mathf.Max(alongRadius, acrossRadius));
                    MaximumDecisionOffset = Mathf.Max(MaximumDecisionOffset,
                        Vector2.Distance(interactionPosition, segment.Position));
                }
            }
        }

        public void ClearForDisabledMode()
        {
            IndexedSectionCount = 0;
            OccupiedCellCount = 0;
            QueryCount = 0;
            CandidateReferenceCount = 0;
            MaximumBoatContactRadius = 0f;
            MaximumDecisionOffset = 0f;
        }

        public void Query(Vector2 position, float radius, List<WaveSectionReference> results)
        {
            results.Clear();
            QueryCount++;
            float safeRadius = Mathf.Max(0f, radius);
            Vector2Int first = Cell(position - Vector2.one * safeRadius);
            Vector2Int last = Cell(position + Vector2.one * safeRadius);
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                List<WaveSectionReference> references = cells[y * width + x];
                if (references == null || references.Count == 0) continue;
                for (int index = 0; index < references.Count; index++)
                    results.Add(references[index]);
            }
            if (results.Count > 1) results.Sort(ReferenceComparer.Instance);
            CandidateReferenceCount += results.Count;
        }

        private int CellIndex(Vector2 position)
        {
            Vector2Int cell = Cell(position);
            return cell.y * width + cell.x;
        }

        private Vector2Int Cell(Vector2 position)
            => new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((position.x - minimum.x) / cellSize),
                    0, width - 1),
                Mathf.Clamp(Mathf.FloorToInt((position.y - minimum.y) / cellSize),
                    0, height - 1));
    }
}

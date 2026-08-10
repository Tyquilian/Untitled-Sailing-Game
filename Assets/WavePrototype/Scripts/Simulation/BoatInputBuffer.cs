using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WavePrototype.Simulation
{
    /// <summary>
    /// Stores boat controls by simulation tick. Commands become held controls when their
    /// tick is applied, which makes a recorded command stream independent of render rate.
    /// </summary>
    internal sealed class BoatInputBuffer
    {
        private readonly List<BoatControlCommand> pending = new List<BoatControlCommand>(512);
        private readonly List<BoatControlCommand> applied = new List<BoatControlCommand>(512);
        private readonly Dictionary<int, BoatControl> active = new Dictionary<int, BoatControl>(8);
        private readonly ReadOnlyCollection<BoatControlCommand> appliedView;
        private readonly bool recordHistory;
        private readonly int maximumRecordedCommands;
        private readonly int compactionThreshold;
        private int pendingCursor;

        public IReadOnlyList<BoatControlCommand> AppliedCommands => appliedView;
        internal IReadOnlyList<BoatControlCommand> PendingCommands => pending;
        internal int PendingCursor => pendingCursor;

        public BoatInputBuffer(bool recordHistory, int maximumRecordedCommands,
            int compactionThreshold)
        {
            this.recordHistory = recordHistory;
            this.maximumRecordedCommands = maximumRecordedCommands;
            this.compactionThreshold = System.Math.Max(1, compactionThreshold);
            appliedView = applied.AsReadOnly();
        }

        public void Reset()
        {
            pending.Clear();
            applied.Clear();
            active.Clear();
            pendingCursor = 0;
        }

        public bool Queue(BoatControlCommand command, ulong currentTick)
        {
            if (command.BoatId <= 0 || command.Tick < currentTick) return false;

            int insertIndex = pending.Count;
            for (int i = pendingCursor; i < pending.Count; i++)
            {
                BoatControlCommand existing = pending[i];
                if (existing.Tick == command.Tick && existing.BoatId == command.BoatId)
                {
                    pending[i] = command;
                    return true;
                }

                if (existing.Tick > command.Tick ||
                    (existing.Tick == command.Tick && existing.BoatId > command.BoatId))
                {
                    insertIndex = i;
                    break;
                }
            }

            pending.Insert(insertIndex, command);
            return true;
        }

        public void BeginTick(ulong tick)
        {
            while (pendingCursor < pending.Count && pending[pendingCursor].Tick <= tick)
            {
                BoatControlCommand command = pending[pendingCursor++];
                active[command.BoatId] = command.Control;
                if (recordHistory) applied.Add(command);
            }

            if (maximumRecordedCommands > 0 && applied.Count > maximumRecordedCommands)
                applied.RemoveRange(0, applied.Count - maximumRecordedCommands);

            if (pendingCursor >= compactionThreshold && pendingCursor * 2 >= pending.Count)
            {
                pending.RemoveRange(0, pendingCursor);
                pendingCursor = 0;
            }
        }

        public BoatControl GetControl(int boatId)
            => active.TryGetValue(boatId, out BoatControl control) ? control : default;
    }
}

using Sim.Engine;
using Sim.Snapshot;

namespace Program
{
    public interface ISnapshotFormatter
    {
        string Format(TickSnapshot r, int entityId);
    }

    public interface IEntityPositionReader
    {
        bool TryGetEntityPos(GameSnapshot snap, int entityId, out int x, out int y);
    }

    /// <summary>
    /// Pure formatting (no IO).
    /// Keeps printing policy separate from the harness loop.
    /// </summary>
    public sealed class SnapshotFormatter(IEntityPositionReader pos) : ISnapshotFormatter
    {
        private readonly IEntityPositionReader _pos = pos ?? throw new ArgumentNullException(nameof(pos));

        public string Format(TickSnapshot r, int entityId)
        {
            GameSnapshot snap = r.Snapshot;
            TickFrame frame = r.Frame;

            FlowSnapshot flow = snap.Flow;

            if (_pos.TryGetEntityPos(snap, entityId, out int x, out int y))
            {
                return
                    $"Tick={frame.Tick} TimeMs={frame.ServerTimeMs} " +
                    $"Entity{entityId}=({x},{y}) " +
                    $"Flow={flow.FlowState} L{flow.LevelIndex} R{flow.RoundIndex} " +
                    $"RoundState={flow.RoundState} Score={flow.CumulativeScore}/{flow.TargetScore} " +
                    $"CookSeq={flow.CookResultSeq}";
            }

            return
                $"Tick={frame.Tick} TimeMs={frame.ServerTimeMs} " +
                $"Entity{entityId}=<not found> Flow={flow.FlowState}";
        }
    }

    /// <summary>
    /// Pure data read helper (no formatting, no IO).
    /// </summary>
    public sealed class EntityPositionReader : IEntityPositionReader
    {
        public bool TryGetEntityPos(GameSnapshot snap, int entityId, out int x, out int y)
        {
            SampleEntityPos[]? arr = snap.Entities;
            if (arr == null || arr.Length == 0)
            {
                x = 0;
                y = 0;
                return false;
            }

            int i = 0;
            while (i < arr.Length)
            {
                SampleEntityPos p = arr[i];
                if (p.EntityId == entityId)
                {
                    x = p.X;
                    y = p.Y;
                    return true;
                }
                i++;
            }

            x = 0;
            y = 0;
            return false;
        }
    }
}

using System;
using Game;
using Sim;

namespace Program
{
    public interface ISnapshotFormatter
    {
        string Format(EngineTickResult r, int entityId);
    }

    public interface IEntityPositionReader
    {
        bool TryGetEntityPos(SampleWorldSnapshot snap, int entityId, out int x, out int y);
    }

    /// <summary>
    /// Pure formatting (no IO).
    /// Keeps printing policy separate from the harness loop.
    /// </summary>
    public sealed class SnapshotFormatter(IEntityPositionReader pos) : ISnapshotFormatter
    {
        private readonly IEntityPositionReader _pos = pos ?? throw new ArgumentNullException(nameof(pos));

        public string Format(EngineTickResult r, int entityId)
        {
            SampleWorldSnapshot? snap = r.Snapshot;
            if (snap == null)
                return $"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} Snapshot=<null>";

            FlowSnapshot flow = snap.Flow;

            if (_pos.TryGetEntityPos(snap, entityId, out int x, out int y))
            {
                return
                    $"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} " +
                    $"Entity{entityId}=({x},{y}) " +
                    $"Flow={flow.FlowState} L{flow.LevelIndex} R{flow.RoundIndex} " +
                    $"RoundState={flow.RoundState} Score={flow.CumulativeScore}/{flow.TargetScore} " +
                    $"CookSeq={flow.CookResultSeq}";
            }

            return
                $"Tick={r.ServerTick} TimeMs={r.ServerTimeMs} " +
                $"Entity{entityId}=<not found> Flow={flow.FlowState}";
        }
    }

    /// <summary>
    /// Pure data read helper (no formatting, no IO).
    /// </summary>
    public sealed class EntityPositionReader : IEntityPositionReader
    {
        public bool TryGetEntityPos(SampleWorldSnapshot snap, int entityId, out int x, out int y)
        {
            SampleEntityPos[]? arr = snap.Entities;
            if (arr == null || arr.Length == 0)
            {
                x = 0;
                y = 0;
                return false;
            }

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].EntityId == entityId)
                {
                    x = arr[i].X;
                    y = arr[i].Y;
                    return true;
                }
            }

            x = 0;
            y = 0;
            return false;
        }
    }
}

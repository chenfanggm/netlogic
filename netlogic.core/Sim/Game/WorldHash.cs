using Sim.Hashing;
using Sim.Snapshot;

namespace Sim.Game
{
    /// <summary>
    /// Deterministic hash of authoritative game state.
    /// Used for desync detection and replay verification.
    /// Keep stable across versions if you want backward-compatible replays.
    /// </summary>
    public static class WorldHash
    {
        /// <summary>
        /// Computes a deterministic hash of ALL authoritative state that matters for future simulation.
        /// This should change if and only if the authoritative state differs.
        /// </summary>
        public static uint Compute(Game world)
        {
            Fnv1a32 h = Fnv1a32.Start();

            // 1) Flow / run state (authoritative, affects progression and future sim)
            FlowSnapshot flow = world.BuildFlowSnapshot();

            h.Add((int)flow.FlowState);
            h.Add(flow.LevelIndex);
            h.Add(flow.RoundIndex);
            h.Add(flow.SelectedChefHatId);
            h.Add(flow.TargetScore);
            h.Add(flow.CumulativeScore);
            h.Add(flow.CookAttemptsUsed);
            h.Add((int)flow.RoundState);
            h.Add(flow.CookResultSeq);
            h.Add(flow.LastCookScoreDelta);
            h.Add(flow.LastCookMetTarget);

            // 2) Entities (must be stable iteration order; Step 2 guarantees this)
            foreach (Entity e in world.Entities)
            {
                h.Add(e.Id);
                h.Add(e.X);
                h.Add(e.Y);
                h.Add(e.Hp);
            }

            return h.Finish();
        }
    }
}

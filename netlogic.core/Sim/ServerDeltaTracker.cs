using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Tracks last sent entity state per client and builds delta updates containing only changes.
    /// </summary>
    public sealed class ServerDeltaTracker
    {
        private readonly Dictionary<int, EntityState> _lastSent;

        public ServerDeltaTracker()
        {
            _lastSent = new Dictionary<int, EntityState>(512);
        }

        public void ResetWithFullSnapshot(EntityState[] full)
        {
            _lastSent.Clear();

            int i = 0;
            while (i < full.Length)
            {
                EntityState s = full[i];
                _lastSent[s.Id] = s;
                i++;
            }
        }

        public DeltaMsg BuildDelta(int tick, EntityState[] current)
        {
            // Build quick index of current
            Dictionary<int, EntityState> currentMap = new Dictionary<int, EntityState>(current.Length);
            int i = 0;
            while (i < current.Length)
            {
                EntityState s = current[i];
                currentMap[s.Id] = s;
                i++;
            }

            // Removed: in _lastSent but not in current
            List<int> removed = new List<int>(16);
            foreach (KeyValuePair<int, EntityState> kv in _lastSent)
            {
                int id = kv.Key;
                if (!currentMap.ContainsKey(id))
                    removed.Add(id);
            }

            // Added/Changed: compare current against last
            List<EntityState> added = new List<EntityState>(16);
            List<EntityState> changed = new List<EntityState>(32);

            int j = 0;
            while (j < current.Length)
            {
                EntityState now = current[j];

                EntityState prev;
                bool hadPrev = _lastSent.TryGetValue(now.Id, out prev);

                if (!hadPrev)
                {
                    added.Add(now);
                }
                else
                {
                    if (!EqualsState(prev, now))
                        changed.Add(now);
                }

                j++;
            }

            // Apply delta to baseline
            int r = 0;
            while (r < removed.Count)
            {
                _lastSent.Remove(removed[r]);
                r++;
            }

            int a = 0;
            while (a < added.Count)
            {
                EntityState s = added[a];
                _lastSent[s.Id] = s;
                a++;
            }

            int c = 0;
            while (c < changed.Count)
            {
                EntityState s = changed[c];
                _lastSent[s.Id] = s;
                c++;
            }

            int[] removedArray = removed.Count == 0 ? Array.Empty<int>() : removed.ToArray();
            EntityState[] addedArray = added.Count == 0 ? Array.Empty<EntityState>() : added.ToArray();
            EntityState[] changedArray = changed.Count == 0 ? Array.Empty<EntityState>() : changed.ToArray();

            return new DeltaMsg(tick, changedArray, removedArray, addedArray);
        }

        private static bool EqualsState(EntityState a, EntityState b)
        {
            return a.Id == b.Id && a.X == b.X && a.Y == b.Y && a.Hp == b.Hp;
        }
    }
}

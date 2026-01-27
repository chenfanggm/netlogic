using System;
using System.Collections.Generic;
using Net;

namespace Sim.Client.State
{
    /// <summary>
    /// Maintains authoritative entity state on client by applying full snapshots and delta updates.
    /// </summary>
    public sealed class ClientAuthoritativeState
    {
        private readonly Dictionary<int, EntityState> _entities;

        public ClientAuthoritativeState()
        {
            _entities = new Dictionary<int, EntityState>(512);
        }

        public void ApplyFullSnapshot(SnapshotMsg snap)
        {
            _entities.Clear();

            EntityState[] arr = snap.Entities;
            int i = 0;
            while (i < arr.Length)
            {
                EntityState s = arr[i];
                _entities[s.Id] = s;
                i++;
            }
        }

        public void ApplyDelta(DeltaMsg delta)
        {
            int[] removed = delta.RemovedIds;
            int i = 0;
            while (i < removed.Length)
            {
                _entities.Remove(removed[i]);
                i++;
            }

            EntityState[] added = delta.Added;
            int a = 0;
            while (a < added.Length)
            {
                EntityState s = added[a];
                _entities[s.Id] = s;
                a++;
            }

            EntityState[] changed = delta.Changed;
            int c = 0;
            while (c < changed.Length)
            {
                EntityState s = changed[c];
                _entities[s.Id] = s;
                c++;
            }
        }

        public EntityState[] ToEntityArrayUnordered()
        {
            EntityState[] result = new EntityState[_entities.Count];

            int idx = 0;
            foreach (KeyValuePair<int, EntityState> kv in _entities)
            {
                result[idx] = kv.Value;
                idx++;
            }

            return result;
        }

        public int Count
        {
            get { return _entities.Count; }
        }

        public void ApplyPositionAt(int id, int x, int y)
        {
            if (_entities.TryGetValue(id, out EntityState existing))
            {
                _entities[id] = new EntityState(id, x, y, existing.Hp);
            }
            else
            {
                // If we haven't seen this id yet, create it with hp=0 (hp not replicated on sample lane here).
                _entities[id] = new EntityState(id, x, y, 0);
            }
        }
    }
}

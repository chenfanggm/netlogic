using System.Collections.Generic;
using com.aqua.netlogic.net;

namespace com.aqua.netlogic.sim.game.entity
{
    internal sealed class EntityManager
    {
        private int _nextEntityId = 1;

        private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>(128);

        // Deterministic iteration order without per-iteration sorting/allocations.
        private readonly List<int> _sortedIds = new List<int>(128);

        public IEnumerable<Entity> Entities
        {
            get
            {
                // Iterate stable sorted ids to preserve determinism.
                for (int i = 0; i < _sortedIds.Count; i++)
                {
                    int id = _sortedIds[i];
                    // If you later add removals, swap this to TryGetValue.
                    yield return _entities[id];
                }
            }
        }

        public Entity CreateEntityAt(int x, int y)
        {
            int id = _nextEntityId++;
            Entity e = new Entity(id, x, y);
            _entities.Add(id, e);
            InsertSortedId(id);
            return e;
        }

        public Entity CreateEntityAt(int entityId, int x, int y)
        {
            if (_entities.TryGetValue(entityId, out Entity? existing) && existing != null)
                return existing;

            if (_nextEntityId <= entityId)
                _nextEntityId = entityId + 1;

            Entity e = new Entity(entityId, x, y);
            _entities.Add(entityId, e);
            InsertSortedId(entityId);
            return e;
        }

        public bool TryGetEntity(int id, out Entity entity)
        {
            return _entities.TryGetValue(id, out entity!);
        }

        public bool TryRemoveEntity(int id)
        {
            if (!_entities.Remove(id))
                return false;

            int idx = _sortedIds.BinarySearch(id);
            if (idx >= 0)
                _sortedIds.RemoveAt(idx);

            return true;
        }

        public EntityState[] ToSnapshot()
        {
            // Deterministic and allocation-lean (no LINQ, no sorting per call)
            List<EntityState> list = new List<EntityState>(_sortedIds.Count);

            for (int i = 0; i < _sortedIds.Count; i++)
            {
                int id = _sortedIds[i];
                Entity e = _entities[id];
                list.Add(new EntityState(e.Id, e.X, e.Y, e.Hp));
            }

            return list.ToArray();
        }

        public bool TryMoveEntityBy(int entityId, int dx, int dy, out int newX, out int newY)
        {
            if (!_entities.TryGetValue(entityId, out Entity? entity) || entity == null)
            {
                newX = 0;
                newY = 0;
                return false;
            }

            entity.X += dx;
            entity.Y += dy;

            newX = entity.X;
            newY = entity.Y;
            return true;
        }

        private void InsertSortedId(int id)
        {
            // Maintain _sortedIds in ascending order. O(log n) search + O(n) shift,
            // but insertions are rare compared to reads (hashing/snapshotting per tick).
            int idx = _sortedIds.BinarySearch(id);
            if (idx >= 0)
                return; // already present (should not happen in current flow)
            idx = ~idx;
            _sortedIds.Insert(idx, id);
        }
    }
}

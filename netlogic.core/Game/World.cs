using System.Collections.Generic;
using System.Linq;
using Net;
using Sim;

namespace Game
{
    /// <summary>
    /// Game world that manages entities and provides deterministic simulation state.
    /// </summary>
    public sealed class World
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>(128);

        public IEnumerable<Entity> Entities
        {
            get
            {
                // Stable iteration order for hashing + deterministic behavior
                IEnumerable<int> keys = _entities.Keys.OrderBy(x => x);
                foreach (int k in keys)
                    yield return _entities[k];
            }
        }

        public Entity CreateEntityAt(int x, int y)
        {
            int id = _nextEntityId++;
            Entity e = new Entity(id, x, y);
            _entities.Add(id, e);
            return e;
        }

        public Entity CreateEntityAt(int entityId, int x, int y)
        {
            if (_entities.ContainsKey(entityId))
                return _entities[entityId];

            if (_nextEntityId <= entityId)
                _nextEntityId = entityId + 1;

            Entity e = new Entity(entityId, x, y);
            _entities.Add(entityId, e);
            return e;
        }

        public bool TryGetEntity(int id, out Entity entity)
        {
            return _entities.TryGetValue(id, out entity!);
        }

        public EntityState[] ToSnapshot()
        {
            // stable order
            List<EntityState> list = new List<EntityState>(_entities.Count);

            IEnumerable<int> keys = _entities.Keys.OrderBy(x => x);
            foreach (int id in keys)
            {
                Entity e = _entities[id];
                list.Add(new EntityState(e.Id, e.X, e.Y, e.Hp));
            }

            return list.ToArray();
        }

        public bool TryMoveEntityBy(int entityId, int dx, int dy)
        {
            if (!_entities.TryGetValue(entityId, out Entity? entity) || entity == null)
                return false;

            // TODO: authoritative collision here (grid/tile checks)
            entity.X += dx;
            entity.Y += dy;

            return true;
        }

        // Compatibility methods for existing ServerSim code
        public Entity Spawn(int x, int y)
        {
            return CreateEntityAt(x, y);
        }

        public bool TryGet(int id, out Entity e)
        {
            return TryGetEntity(id, out e);
        }

        public void Advance(int tick)
        {
            // Put deterministic per-tick world logic here (regen, ai, projectiles, etc.)
            // This is called by the ServerEngine after the systems have executed
            // and before the snapshot is built.
            // The tick parameter is the current tick of the server.
        }

        public SampleEntityPos[] BuildSnapshot()
        {
            List<SampleEntityPos> list = new List<SampleEntityPos>(128);
            foreach (Entity e in Entities)
                list.Add(new SampleEntityPos(e.Id, e.X, e.Y));
            return list.ToArray();
        }
    }

    /// <summary>
    /// Game entity with position, health, and movement capabilities.
    /// </summary>
    public sealed class Entity
    {
        public int Id { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; private set; }

        public Entity(int id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = 100;
        }

        public void MoveBy(int dx, int dy)
        {
            X += dx;
            Y += dy;
        }

        public void Damage(int amount)
        {
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }
    }
}

using System.Collections.Generic;
using Net;

namespace Game
{
    /// <summary>
    /// Game world that manages entities and provides deterministic simulation state.
    /// </summary>
    public sealed class World
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Entity> _entities = new();

        public IEnumerable<Entity> Entities => _entities.Values;

        public Entity CreateEntityAt(int x, int y)
        {
            int id = _nextEntityId++;
            Entity e = new Entity(id, x, y);
            _entities.Add(id, e);
            return e;
        }

        // Overload for demo/testing: create entity with fixed id
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
            EntityState[] states = new EntityState[_entities.Count];
            int i = 0;
            foreach (KeyValuePair<int, Entity> kv in _entities)
            {
                Entity e = kv.Value;
                states[i] = new EntityState(e.Id, e.X, e.Y, e.Hp);
                i++;
            }

            return states;
        }

        public void ApplyMove(int entityId, int dx, int dy)
        {
            if (!_entities.TryGetValue(entityId, out Entity? entity) || entity == null)
                return;

            entity.X += dx;
            entity.Y += dy;
        }

        public bool TryMoveEntityBy(int entityId, int dx, int dy)
        {
            if (!_entities.TryGetValue(entityId, out Entity? entity) || entity == null)
                return false;

            // TODO: server-side collision rules here (grid/tile checks)
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

        public void StepFixed()
        {
            // Put deterministic per-tick world logic here (regen, ai, projectiles, etc.)
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

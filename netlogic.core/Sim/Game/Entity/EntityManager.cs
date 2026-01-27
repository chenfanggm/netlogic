using Net;
using System.Collections.Generic;
using System.Linq;

namespace Sim.Game
{
    internal sealed class EntityManager
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Entity> _entities = new Dictionary<int, Entity>(128);

        public IEnumerable<Entity> Entities
        {
            get
            {
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
            List<EntityState> list = new List<EntityState>(_entities.Count);

            IEnumerable<int> keys = _entities.Keys.OrderBy(x => x);
            foreach (int id in keys)
            {
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
    }
}

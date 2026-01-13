using System.Collections.Generic;
using Net;

namespace Game
{
    public sealed class World
    {
        private int _nextEntityId = 1;
        private readonly Dictionary<int, Entity> _entities = new();

        public IEnumerable<Entity> Entities => _entities.Values;

        public Entity Spawn(int x, int y)
        {
            Entity e = new(_nextEntityId++, x, y, hp: 100);
            _entities.Add(e.Id, e);
            return e;
        }

        public bool TryGet(int id, out Entity e) => _entities.TryGetValue(id, out e!);

        public bool Remove(int id) => _entities.Remove(id);

        public void StepFixed()
        {
            // Put deterministic per-tick world logic here (regen, ai, projectiles, etc.)
        }

        public EntityState[] ToSnapshot()
        {
            List<EntityState> list = new(_entities.Count);
            foreach (Entity e in _entities.Values)
                list.Add(new EntityState(e.Id, e.X, e.Y, e.Hp));
            return list.ToArray();
        }
    }

    public sealed class Entity
    {
        public int Id { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Hp { get; private set; }

        public Entity(int id, int x, int y, int hp)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = hp;
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

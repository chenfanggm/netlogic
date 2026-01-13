using System;
using System.Collections.Generic;
using Game;

namespace Net
{
    public static class StateHash
    {
        // FNV-1a 32-bit
        public static uint ComputeWorldHash(World world)
        {
            uint h = 2166136261u;

            foreach (Entity e in world.Entities)
            {
                h = Mix(h, (uint)e.Id);
                h = Mix(h, (uint)e.X);
                h = Mix(h, (uint)e.Y);
                h = Mix(h, (uint)e.Hp);
            }

            return h;
        }

        public static uint ComputeEntitiesHash(EntityState[] entities)
        {
            uint h = 2166136261u;

            int i = 0;
            while (i < entities.Length)
            {
                EntityState e = entities[i];
                h = Mix(h, (uint)e.Id);
                h = Mix(h, (uint)e.X);
                h = Mix(h, (uint)e.Y);
                h = Mix(h, (uint)e.Hp);
                i++;
            }

            return h;
        }

        private static uint Mix(uint h, uint v)
        {
            h ^= v;
            h *= 16777619u;
            return h;
        }
    }
}

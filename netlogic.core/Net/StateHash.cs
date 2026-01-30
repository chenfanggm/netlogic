using System;
using System.Collections.Generic;
using com.aqua.netlogic.sim.game;

namespace com.aqua.netlogic.net
{
    public static class StateHash
    {
        // FNV-1a 32-bit
        public static uint ComputeWorldHash(ServerModel world)
        {
            return ServerModelHash.Compute(world);
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

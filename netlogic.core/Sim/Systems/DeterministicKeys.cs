using System.Collections.Generic;

namespace Sim.Systems
{
    public static class DeterministicKeys
    {
        public static int[] GetSortedKeys<T>(Dictionary<int, T> dict)
        {
            int[] keys = new int[dict.Count];
            int idx = 0;

            foreach (int k in dict.Keys)
            {
                keys[idx] = k;
                idx++;
            }

            System.Array.Sort(keys);
            return keys;
        }
    }
}

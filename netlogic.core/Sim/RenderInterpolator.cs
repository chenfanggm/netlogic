using System;
using System.Collections.Generic;
using Net;

namespace Sim
{
    /// <summary>
    /// Interpolates entity positions between two snapshots for smooth client-side rendering.
    /// </summary>
    public sealed class RenderInterpolator
    {
        private readonly Dictionary<int, EntityState> _indexB;

        public RenderInterpolator()
        {
            _indexB = new Dictionary<int, EntityState>(256);
        }

        public EntityState[] Interpolate(SnapshotMsg a, SnapshotMsg b, double t)
        {
            _indexB.Clear();

            EntityState[] entitiesB = b.Entities;
            int countB = entitiesB.Length;
            for (int iB = 0; iB < countB; iB++)
            {
                EntityState stateB = entitiesB[iB];
                _indexB[stateB.Id] = stateB;
            }

            EntityState[] entitiesA = a.Entities;
            int countA = entitiesA.Length;

            EntityState[] result = new EntityState[countA];

            for (int iA = 0; iA < countA; iA++)
            {
                EntityState stateA = entitiesA[iA];

                bool hasB = _indexB.TryGetValue(stateA.Id, out EntityState stateB2);

                if (!hasB)
                {
                    result[iA] = stateA;
                    continue;
                }

                int x = LerpInt(stateA.X, stateB2.X, t);
                int y = LerpInt(stateA.Y, stateB2.Y, t);
                int hp = stateB2.Hp; // hp usually snaps; could lerp too if you want

                result[iA] = new EntityState(stateA.Id, x, y, hp);
            }

            return result;
        }

        private static int LerpInt(int a, int b, double t)
        {
            double v = a + (b - a) * t;
            int r = (int)Math.Round(v);
            return r;
        }
    }
}

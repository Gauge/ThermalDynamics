using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Sun visibility worked out the slow, obvious way: intersect the ray with every cell's cube.
    /// Analytic rather than sampled, because a sampled walk rounds at cell boundaries and cannot
    /// tell a ray passing through a cube from one grazing its corner.
    /// </summary>
    public static class Reference
    {

        public static bool Lit(GridModel grid, Vector3I from, Vector3 sun)
        {
            sun = Vector3.Normalize(sun);

            Vector3 origin = new Vector3(from.X, from.Y, from.Z);

            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                Vector3I[] cells = blocks[i].Cells;
                for (int c = 0; c < cells.Length; c++)
                {
                    if (cells[c] == from) continue;
                    if (Penetrates(origin, sun, cells[c])) return false;
                }
            }

            return true;
        }


        private static bool Penetrates(Vector3 origin, Vector3 direction, Vector3I cell)
        {
            const float Epsilon = 1e-3f;

            float enter = 0f;
            float exit = float.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
                float d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
                float centre = axis == 0 ? cell.X : axis == 1 ? cell.Y : cell.Z;

                if (Math.Abs(d) < 1e-9f)
                {
                    if (o < centre - 0.5f || o > centre + 0.5f) return false;
                    continue;
                }

                float t1 = (centre - 0.5f - o) / d;
                float t2 = (centre + 0.5f - o) / d;
                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
            }

            return exit - enter > Epsilon && exit > Epsilon;
        }
    }
}

using VRageMath;

namespace Thermodynamics.Core
{
    public static class ShapeNormal
    {
        public const int Radius = 1;


        public static Vector3 Of(CellBitset occupancy, BlockInstance block)
        {

            return Of(occupancy, block, Radius);
        }


        public static Vector3 Of(CellBitset occupancy, BlockInstance block, int radius)
        {
            if (occupancy == null || block == null || radius < 1) return Vector3.Zero;

            if (radius == 1 && block.CellCount == 1) return OneCell(occupancy, block.Min);


            return Walking(occupancy, block, radius);
        }


        public static Vector3 Walking(CellBitset occupancy, BlockInstance block, int radius)
        {
            if (occupancy == null || block == null || radius < 1) return Vector3.Zero;

            Vector3I min = block.Min;
            Vector3I maxExclusive = block.MaxExclusive;


            Vector3 centre = new Vector3(
                (min.X + maxExclusive.X - 1) * 0.5f,
                (min.Y + maxExclusive.Y - 1) * 0.5f,
                (min.Z + maxExclusive.Z - 1) * 0.5f);

            Vector3 sum = Vector3.Zero;

            for (int x = min.X - radius; x < maxExclusive.X + radius; x++)
            {
                for (int y = min.Y - radius; y < maxExclusive.Y + radius; y++)
                {
                    for (int z = min.Z - radius; z < maxExclusive.Z + radius; z++)
                    {
                        if (x >= min.X && x < maxExclusive.X
                            && y >= min.Y && y < maxExclusive.Y
                            && z >= min.Z && z < maxExclusive.Z)
                        {
                            continue;
                        }

                        if (!occupancy.Contains(new Vector3I(x, y, z))) continue;


                        Vector3 offset = new Vector3(x - centre.X, y - centre.Y, z - centre.Z);
                        float length = offset.Length();
                        if (length <= 0f) continue;

                        sum -= offset / length;
                    }
                }
            }

            float magnitude = sum.Length();
            if (magnitude <= Epsilon) return Vector3.Zero;

            return sum / magnitude;
        }

        private const float Epsilon = 1e-4f;


        private static readonly Vector3I[] OneCellOffsets = BuildOneCellOffsets();


        private static readonly Vector3[] OneCellUnits = BuildOneCellUnits();


        private static Vector3I[] BuildOneCellOffsets()
        {
            Vector3I[] offsets = new Vector3I[26];
            int i = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;

                        offsets[i++] = new Vector3I(x, y, z);
                    }
                }
            }

            return offsets;
        }


        private static Vector3[] BuildOneCellUnits()
        {

            Vector3I[] offsets = BuildOneCellOffsets();
            Vector3[] units = new Vector3[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {

                Vector3 offset = new Vector3(offsets[i].X, offsets[i].Y, offsets[i].Z);
                units[i] = offset / offset.Length();
            }

            return units;
        }


        private static Vector3 OneCell(CellBitset occupancy, Vector3I min)
        {
            Vector3 sum = Vector3.Zero;

            for (int i = 0; i < OneCellOffsets.Length; i++)
            {
                if (!occupancy.Contains(min + OneCellOffsets[i])) continue;

                sum -= OneCellUnits[i];
            }

            float magnitude = sum.Length();
            if (magnitude <= Epsilon) return Vector3.Zero;

            return sum / magnitude;
        }
    }
}

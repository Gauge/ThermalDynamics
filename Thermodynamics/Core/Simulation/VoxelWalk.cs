using System;
using VRageMath;

namespace Thermodynamics.Core
{
    public static class VoxelWalk
    {
        /// <summary>A resumable form of the same ray walk; each Step checks at most one cell.</summary>
        public struct Cursor
        {
            private GridModel grid;
            private Vector3I min, max;
            private int x, y, z, stepX, stepY, stepZ, remaining;
            private double tMaxX, tMaxY, tMaxZ, tDeltaX, tDeltaY, tDeltaZ, span;
            private bool first;

            public bool Complete { get; private set; }
            public bool Blocked { get; private set; }

            /// <summary>Starts a ray using caller-supplied bounds, without asking the grid to rebuild them.</summary>
            public void Start(GridModel model, Vector3D origin, Vector3D direction,
                Vector3I minimum, Vector3I maximum, double startOffset = 0d)
            {
                grid = model;
                min = minimum;
                max = maximum;
                Complete = true;
                Blocked = false;
                if (grid == null || grid.BlockCount == 0 || direction.LengthSquared() < 1e-12) return;
                direction = Vector3D.Normalize(direction);
                double enter, exit;
                if (!BoxRange(origin, direction, min, max, out enter, out exit) || exit <= startOffset) return;
                double travelled = Math.Max(enter, startOffset);
                Vector3D point = origin + direction * travelled;
                x = (int)Math.Round(point.X);
                y = (int)Math.Round(point.Y);
                z = (int)Math.Round(point.Z);
                stepX = Sign(direction.X); stepY = Sign(direction.Y); stepZ = Sign(direction.Z);
                tMaxX = FirstBoundary(point.X, x, direction.X);
                tMaxY = FirstBoundary(point.Y, y, direction.Y);
                tMaxZ = FirstBoundary(point.Z, z, direction.Z);
                tDeltaX = Delta(direction.X); tDeltaY = Delta(direction.Y); tDeltaZ = Delta(direction.Z);
                span = exit - travelled;
                remaining = (2 * ((max.X - min.X) + (max.Y - min.Y) + (max.Z - min.Z))) + 8;
                first = true;
                Complete = false;
            }

            /// <summary>Advances one occupancy check or boundary step. Completed cursors do no work.</summary>
            public void Step()
            {
                if (Complete) return;
                if (first)
                {
                    first = false;
                    if (Inside(x, y, z, min, max) && grid.IsOccupied(new Vector3I(x, y, z)))
                    {
                        Blocked = true;
                        Complete = true;
                    }
                    return;
                }
                if (remaining-- <= 0) { Complete = true; return; }
                double t;
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    t = tMaxX; x += stepX; tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxZ)
                {
                    t = tMaxY; y += stepY; tMaxY += tDeltaY;
                }
                else
                {
                    t = tMaxZ; z += stepZ; tMaxZ += tDeltaZ;
                }
                if (t > span) { Complete = true; return; }
                if (grid.IsOccupied(new Vector3I(x, y, z))) { Blocked = true; Complete = true; }
            }
        }

        public static bool Blocked(GridModel grid, Vector3D origin, Vector3D direction, double startOffset = 0d)
        {
            if (grid == null) return false;
            if (direction.LengthSquared() < 1e-12) return false;

            direction = Vector3D.Normalize(direction);

            Vector3I min = grid.Min;
            Vector3I max = grid.Max;

            double enter, exit;
            if (!BoxRange(origin, direction, min, max, out enter, out exit)) return false;
            if (exit <= startOffset) return false;

            double travelled = Math.Max(enter, startOffset);
            Vector3D point = origin + (direction * travelled);

            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);
            int z = (int)Math.Round(point.Z);

            if (Inside(x, y, z, min, max) && grid.IsOccupied(new Vector3I(x, y, z))) return true;


            int stepX = Sign(direction.X), stepY = Sign(direction.Y), stepZ = Sign(direction.Z);


            double tMaxX = FirstBoundary(point.X, x, direction.X);

            double tMaxY = FirstBoundary(point.Y, y, direction.Y);

            double tMaxZ = FirstBoundary(point.Z, z, direction.Z);


            double tDeltaX = Delta(direction.X);

            double tDeltaY = Delta(direction.Y);

            double tDeltaZ = Delta(direction.Z);

            double span = exit - travelled;

            int limit = (2 * ((max.X - min.X) + (max.Y - min.Y) + (max.Z - min.Z))) + 8;

            for (int i = 0; i < limit; i++)
            {
                double t;

                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    t = tMaxX;
                    x += stepX;
                    tMaxX += tDeltaX;
                }

                else if (tMaxY <= tMaxZ)
                {
                    t = tMaxY;
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    t = tMaxZ;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }

                if (t > span) return false;
                if (grid.IsOccupied(new Vector3I(x, y, z))) return true;
            }

            return false;
        }


        private static bool BoxRange(
            Vector3D origin, Vector3D direction, Vector3I min, Vector3I max,
            out double enter, out double exit)
        {
            enter = 0d;
            exit = double.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {

                double o = Component(origin, axis);

                double d = Component(direction, axis);

                double low = BoxGeometry.Component(min, axis) - 0.5d;
                double high = BoxGeometry.Component(max, axis) + 0.5d;

                if (Math.Abs(d) < 1e-9)
                {
                    if (o < low || o > high) return false;
                    continue;
                }

                double t1 = (low - o) / d;
                double t2 = (high - o) / d;

                if (t1 > t2)
                {
                    double swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
            }

            return exit >= enter && exit > 0d;
        }


        private static bool Inside(int x, int y, int z, Vector3I min, Vector3I max)
        {
            return x >= min.X && x <= max.X
                && y >= min.Y && y <= max.Y
                && z >= min.Z && z <= max.Z;
        }


        private static double FirstBoundary(double position, int cell, double direction)
        {
            double magnitude = Math.Abs(direction);
            if (magnitude < 1e-9) return double.MaxValue;

            double offset = position - cell;
            double remaining = direction > 0 ? 0.5d - offset : offset + 0.5d;

            if (remaining < 0d) remaining = 0d;
            return remaining / magnitude;
        }


        private static double Delta(double direction)
        {
            double magnitude = Math.Abs(direction);
            return magnitude < 1e-9 ? double.MaxValue : 1d / magnitude;
        }


        private static double Component(Vector3D vector, int axis)
        {
            return axis == 0 ? vector.X : axis == 1 ? vector.Y : vector.Z;
        }


        private static int Sign(double value)
        {
            if (value > 0d) return 1;
            if (value < 0d) return -1;
            return 0;
        }
    }
}

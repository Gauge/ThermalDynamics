using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>One cell face the air meets: its area projected across the flow, how squarely its surface faces the flow, and where it is.</summary>
    public struct AeroElement
    {
        /// <summary>Area projected onto the plane across the flow, m².</summary>
        public float Area;

        /// <summary>Cosine between the reconstructed surface normal and the flow axis: 1 is square to the flow.</summary>
        public float Incidence;

        /// <summary>The face's centre in grid-local metres.</summary>
        public Vector3 Centre;
    }

    /// <summary>
    /// What the air meets on a hull from one direction: the windward surface it reaches, the leeward
    /// surface it leaves, the wetted skin, and the hull's extent along the flow. Geometry only; the
    /// forces are <see cref="AeroDrag"/>'s. See aerodynamics.md, AR3 and AR4.
    /// </summary>
    public sealed class AeroView
    {
        /// <summary>Unit direction the air comes from, in grid axes.</summary>
        public Vector3 Upstream;

        /// <summary>Windward faces the air reaches without passing through the hull first.</summary>
        public readonly List<AeroElement> Front = new List<AeroElement>();

        /// <summary>Leeward faces open to the wake downstream.</summary>
        public readonly List<AeroElement> Base = new List<AeroElement>();

        /// <summary>The silhouette: the projected area of <see cref="Front"/>, m².</summary>
        public float Silhouette;

        /// <summary>The hull's extent along the flow, m.</summary>
        public float Length;

        /// <summary>Exposed skin open to the outside air, m².</summary>
        public float Wetted;

        /// <summary>Centre of the wetted skin in grid-local metres.</summary>
        public Vector3 WettedCentre;
    }

    /// <summary>Builds an <see cref="AeroView"/> from a grid's occupancy, one ray per exposed face.</summary>
    public static class AeroGeometry
    {
        private const float Facing = 1e-4f;

        /// <summary>The view of <paramref name="grid"/> from <paramref name="upstream"/>, which need not be normalised.</summary>
        public static AeroView View(GridModel grid, Vector3 upstream)
        {
            AeroView view = new AeroView();
            if (grid == null || grid.BlockCount == 0 || upstream.LengthSquared() < 1e-12f) return view;

            Vector3 u = Vector3.Normalize(upstream);
            view.Upstream = u;

            float size = grid.GridSize;
            float faceArea = grid.CellFaceArea;
            CellBitset occupancy = grid.Occupancy();

            float low = float.MaxValue, high = float.MinValue;
            Vector3 wettedMoment = Vector3.Zero;

            IList<BlockInstance> blocks = grid.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                BlockInstance block = blocks[b];
                Vector3I[] cells = block.Cells;
                for (int c = 0; c < cells.Length; c++)
                {
                    Vector3I cell = cells[c];
                    float along = Vector3.Dot(new Vector3(cell.X, cell.Y, cell.Z), u) * size;
                    if (along < low) low = along;
                    if (along > high) high = along;

                    for (int f = 0; f < Face.Count; f++)
                    {
                        Vector3I outside = cell + Face.Offsets[f];
                        if (grid.IsOccupied(outside)) continue;

                        Vector3 faceNormal = Face.Normals[f];
                        Vector3D from = new Vector3D(outside.X, outside.Y, outside.Z);
                        Vector3 centre = (new Vector3(cell.X, cell.Y, cell.Z) + (faceNormal * 0.5f)) * size;

                        if (!VoxelWalk.Blocked(grid, from, faceNormal))
                        {
                            view.Wetted += faceArea;
                            wettedMoment += centre * faceArea;
                        }

                        float dot = Vector3.Dot(faceNormal, u);
                        if (dot > Facing)
                        {
                            if (VoxelWalk.Blocked(grid, from, u)) continue;
                            float incidence = Math.Max(0f, Vector3.Dot(SurfaceNormal(occupancy, cell, f), u));
                            view.Front.Add(new AeroElement { Area = faceArea * dot, Incidence = incidence, Centre = centre });
                            view.Silhouette += faceArea * dot;
                        }
                        else if (dot < -Facing)
                        {
                            if (VoxelWalk.Blocked(grid, from, -u)) continue;
                            float incidence = Math.Max(0f, -Vector3.Dot(SurfaceNormal(occupancy, cell, f), u));
                            view.Base.Add(new AeroElement { Area = -faceArea * dot, Incidence = incidence, Centre = centre });
                        }
                    }
                }
            }

            float cellDepth = size * (Math.Abs(u.X) + Math.Abs(u.Y) + Math.Abs(u.Z));
            view.Length = (high - low) + cellDepth;
            if (view.Wetted > 0f) view.WettedCentre = wettedMoment / view.Wetted;
            return view;
        }

        /// <summary>
        /// How far in or out a neighbouring column's surface is looked for, in cells, when reading a slope.
        /// A column with no surface this close is an edge; one with a surface further than a step away is
        /// a steep drop, and reads as one.
        /// </summary>
        public const int SurfaceReach = 6;

        /// <summary>
        /// The surface normal at an exposed face, read from the surface around it: the depth of the
        /// surface along the face's normal in the 3×3 columns around the face, a plane fitted to the
        /// depths found. A column past an edge holds no surface and is skipped rather than read as a
        /// slope, so a flat face is flat to its rim. See aerodynamics.md, Round 6.
        /// </summary>
        public static Vector3 SurfaceNormal(CellBitset occupancy, Vector3I cell, int face)
        {
            return SurfaceNormal(null, occupancy, cell, face);
        }

        /// <summary>Reads the same local surface without constructing a whole-grid occupancy bitset.</summary>
        public static Vector3 SurfaceNormal(GridModel grid, Vector3I cell, int face)
        {
            return SurfaceNormal(grid, null, cell, face);
        }

        private static Vector3 SurfaceNormal(GridModel grid, CellBitset occupancy, Vector3I cell, int face)
        {
            Vector3I n = Face.Offsets[face];
            Vector3I uAxis, vAxis;
            if (n.X != 0) { uAxis = new Vector3I(0, 1, 0); vAxis = new Vector3I(0, 0, 1); }
            else if (n.Y != 0) { uAxis = new Vector3I(1, 0, 0); vAxis = new Vector3I(0, 0, 1); }
            else { uAxis = new Vector3I(1, 0, 0); vAxis = new Vector3I(0, 1, 0); }

            double suu = 0, svv = 0, suv = 0, su = 0, sv = 0, s1 = 0, shu = 0, shv = 0, sh = 0;
            for (int du = -1; du <= 1; du++)
            {
                for (int dv = -1; dv <= 1; dv++)
                {
                    int h;
                    if (!Height(grid, occupancy, cell + (uAxis * du) + (vAxis * dv), n, out h)) continue;
                    suu += du * du; svv += dv * dv; suv += du * dv;
                    su += du; sv += dv; s1 += 1;
                    shu += h * du; shv += h * dv; sh += h;
                }
            }

            double gu, gv;
            if (!Plane(suu, svv, suv, su, sv, s1, shu, shv, sh, out gu, out gv)) return Face.Normals[face];

            Vector3 normal = Face.Normals[face] - ((Vector3)uAxis * (float)gu) - ((Vector3)vAxis * (float)gv);
            return Vector3.Normalize(normal);
        }

        private static bool Contains(GridModel grid, CellBitset occupancy, Vector3I cell)
        {
            return occupancy != null ? occupancy.Contains(cell) : grid.IsOccupied(cell);
        }

        private static bool Height(GridModel grid, CellBitset occupancy, Vector3I column, Vector3I n, out int height)
        {
            height = 0;
            if (Contains(grid, occupancy, column))
            {
                for (int k = 0; k <= SurfaceReach; k++)
                {
                    if (!Contains(grid, occupancy, column + (n * (k + 1))))
                    {
                        height = k;
                        return true;
                    }
                }
                return false;
            }

            for (int k = 1; k <= SurfaceReach; k++)
            {
                if (Contains(grid, occupancy, column - (n * k)))
                {
                    height = -k;
                    return true;
                }
            }
            return false;
        }

        private static bool Plane(double suu, double svv, double suv, double su, double sv, double s1,
            double shu, double shv, double sh, out double gu, out double gv)
        {
            gu = 0;
            gv = 0;
            double det = Det(suu, suv, su, suv, svv, sv, su, sv, s1);
            if (Math.Abs(det) > 1e-9)
            {
                gu = Det(shu, suv, su, shv, svv, sv, sh, sv, s1) / det;
                gv = Det(suu, shu, su, suv, shv, sv, su, sh, s1) / det;
                return true;
            }

            double uSpread = suu - (s1 > 0 ? su * su / s1 : 0);
            double vSpread = svv - (s1 > 0 ? sv * sv / s1 : 0);
            if (uSpread > 1e-9) gu = (shu - (su * sh / s1)) / uSpread;
            if (vSpread > 1e-9) gv = (shv - (sv * sh / s1)) / vSpread;
            return uSpread > 1e-9 || vSpread > 1e-9;
        }

        private static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i)
        {
            return (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));
        }
    }
}

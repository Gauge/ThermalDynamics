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
                Vector3 normal = ShapeNormal.Of(occupancy, block);
                bool reconstructed = normal.LengthSquared() > 0.25f;

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
                            float incidence = reconstructed ? Math.Max(0f, Vector3.Dot(normal, u)) : dot;
                            view.Front.Add(new AeroElement { Area = faceArea * dot, Incidence = incidence, Centre = centre });
                            view.Silhouette += faceArea * dot;
                        }
                        else if (dot < -Facing)
                        {
                            if (VoxelWalk.Blocked(grid, from, -u)) continue;
                            float incidence = reconstructed ? Math.Max(0f, -Vector3.Dot(normal, u)) : -dot;
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
    }
}

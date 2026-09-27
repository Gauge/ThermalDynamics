using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// Builds a profile in bounded work slices, including individual ray steps. The caller's revision
    /// covers mass changes; topology is also checked against GridModel.Version. See aerodynamics.md.
    /// </summary>
    public sealed class AeroProfileBuilder
    {
        private struct Body
        {
            public Vector3 Centre;
            public Vector3 Size;
            public float Mass;
        }

        private struct Surface
        {
            public Vector3I Outside;
            public Vector3 Centre;
            public Vector3 Normal;
            public Vector3 Reconstructed;
        }

        private readonly GridModel grid;
        private readonly int topology;
        private readonly long revision;
        private readonly AeroProfile profile;
        private readonly Vector3[] directions;
        private readonly float[] low, high;
        private readonly List<Body> bodies = new List<Body>();
        private readonly List<Surface> surfaces = new List<Surface>();
        private Vector3I minimum = Vector3I.MaxValue, maximum = Vector3I.MinValue;
        private float mass, wetted;
        private Vector3 massMoment, inertia, wettedMoment;
        private int stage, block, cell, face, direction, surface;
        private bool rayPending;
        private VoxelWalk.Cursor ray;
        private Surface pendingSurface;
        private AeroElement pendingElement;
        private bool pendingFront;
        private AeroSummary summary;

        /// <summary>Count of charged block, face, ray and phase-transition work units.</summary>
        public long Work { get; private set; }

        public bool Complete { get { return stage == 4 && !Invalidated; } }
        public bool Invalidated { get { return stage == 5 || (grid != null && grid.Version != topology); } }

        /// <summary>Creates a job without traversing the hull. Revision must change whenever an input mass changes.</summary>
        public AeroProfileBuilder(GridModel grid, int directionCount, long revision)
        {
            this.grid = grid;
            this.revision = revision;
            topology = grid != null ? grid.Version : 0;
            profile = new AeroProfile(directionCount);
            directions = AeroProfile.Shell(directionCount > AeroProfile.CoarseDirections ? 2 : 1);
            low = new float[directions.Length];
            high = new float[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                directions[i] = Vector3.Normalize(directions[i]);
                low[i] = float.MaxValue;
                high[i] = float.MinValue;
            }
            if (grid == null || grid.BlockCount == 0) stage = 4;
        }

        /// <summary>Consumes at most budget work units; zero or negative budgets do no construction work.</summary>
        public int Advance(int budget, long currentRevision)
        {
            if (currentRevision != revision || Invalidated) { stage = 5; return 0; }
            int used = 0;
            while (used < budget && stage < 4)
            {
                if (stage == 0) MeasureBody();
                else if (stage == 1) MeasureInertia();
                else if (stage == 2) ReadSurface();
                else ReadDirection();
                used++;
                Work++;
            }
            return used;
        }

        /// <summary>Publishes only a complete result matching both the caller's revision and the grid topology.</summary>
        public bool TryGetResult(long currentRevision, out AeroProfile result)
        {
            result = Complete && currentRevision == revision ? profile : null;
            return result != null;
        }

        private void MeasureBody()
        {
            if (block == grid.BlockCount) { block = 0; stage = 1; return; }
            BlockInstance b = grid.Blocks[block++];
            Vector3 centre = (((Vector3)b.Min + (Vector3)b.MaxExclusive) * 0.5f - new Vector3(0.5f)) * grid.GridSize;
            mass += b.Mass;
            massMoment += centre * b.Mass;
            bodies.Add(new Body { Centre = centre, Size = (Vector3)(b.MaxExclusive - b.Min) * grid.GridSize, Mass = b.Mass });
            Vector3I last = b.MaxExclusive - Vector3I.One;
            minimum = Vector3I.Min(minimum, b.Min);
            maximum = Vector3I.Max(maximum, last);
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 u = directions[i];
                Vector3 near = new Vector3(u.X < 0f ? last.X : b.Min.X, u.Y < 0f ? last.Y : b.Min.Y, u.Z < 0f ? last.Z : b.Min.Z);
                Vector3 far = new Vector3(u.X < 0f ? b.Min.X : last.X, u.Y < 0f ? b.Min.Y : last.Y, u.Z < 0f ? b.Min.Z : last.Z);
                low[i] = Math.Min(low[i], Vector3.Dot(near, u) * grid.GridSize);
                high[i] = Math.Max(high[i], Vector3.Dot(far, u) * grid.GridSize);
            }
        }

        private void MeasureInertia()
        {
            Vector3 centre = mass > 0f ? massMoment / mass : Vector3.Zero;
            if (block == bodies.Count)
            {
                profile.SetMassProperties(mass, centre, inertia, grid.CellFaceArea);
                block = 0;
                stage = 2;
                return;
            }
            Body b = bodies[block++];
            Vector3 r = b.Centre - centre;
            Vector3 squared = b.Size * b.Size;
            inertia.X += b.Mass * ((r.Y * r.Y) + (r.Z * r.Z) + ((squared.Y + squared.Z) / 12f));
            inertia.Y += b.Mass * ((r.X * r.X) + (r.Z * r.Z) + ((squared.X + squared.Z) / 12f));
            inertia.Z += b.Mass * ((r.X * r.X) + (r.Y * r.Y) + ((squared.X + squared.Y) / 12f));
        }

        private void ReadSurface()
        {
            if (rayPending)
            {
                if (!ray.Complete) { ray.Step(); return; }
                if (!ray.Blocked)
                {
                    wetted += grid.CellFaceArea;
                    wettedMoment += pendingSurface.Centre * grid.CellFaceArea;
                    profile.AddSkin(pendingSurface.Centre, pendingSurface.Normal);
                }
                surfaces.Add(pendingSurface);
                rayPending = false;
                return;
            }
            if (block == grid.BlockCount) { stage = 3; return; }
            Vector3I[] cells = grid.Blocks[block].Cells;
            if (cell == cells.Length) { block++; cell = 0; return; }
            if (face == Face.Count) { cell++; face = 0; return; }
            Vector3I at = cells[cell];
            int f = face++;
            Vector3I outside = at + Face.Offsets[f];
            if (grid.IsOccupied(outside)) return;
            Vector3 normal = Face.Normals[f];
            pendingSurface = new Surface
            {
                Outside = outside,
                Centre = (new Vector3(at.X, at.Y, at.Z) + normal * 0.5f) * grid.GridSize,
                Normal = normal,
                Reconstructed = AeroGeometry.SurfaceNormal(grid, at, f)
            };
            ray.Start(grid, (Vector3D)outside, normal, minimum, maximum);
            rayPending = true;
        }

        private void ReadDirection()
        {
            if (rayPending)
            {
                if (!ray.Complete) { ray.Step(); return; }
                if (!ray.Blocked)
                {
                    if (pendingFront) AeroDrag.AddFront(ref summary, pendingElement);
                    else AeroDrag.AddBase(ref summary, pendingElement);
                }
                rayPending = false;
                return;
            }
            if (direction == directions.Length) { stage = 4; return; }
            Vector3 u = directions[direction];
            if (surface == surfaces.Count)
            {
                if (summary.Silhouette > 0f)
                {
                    summary.Wetted = wetted;
                    summary.WettedMoment = wetted > 0f ? (wettedMoment / wetted) * wetted : Vector3.Zero;
                    float length = (high[direction] - low[direction])
                        + grid.GridSize * (Math.Abs(u.X) + Math.Abs(u.Y) + Math.Abs(u.Z));
                    summary.Fineness = length / (float)Math.Sqrt(summary.Silhouette);
                }
                else summary = new AeroSummary();
                profile.SetSummary(direction++, summary);
                summary = new AeroSummary();
                surface = 0;
                return;
            }
            Surface s = surfaces[surface++];
            float dot = Vector3.Dot(s.Normal, u);
            if (Math.Abs(dot) <= 1e-4f) return;
            pendingFront = dot > 0f;
            float incidence = Vector3.Dot(s.Reconstructed, u);
            pendingElement = new AeroElement
            {
                Area = grid.CellFaceArea * (pendingFront ? dot : -dot),
                Incidence = Math.Max(0f, pendingFront ? incidence : -incidence),
                Centre = s.Centre
            };
            ray.Start(grid, (Vector3D)s.Outside, pendingFront ? u : -u, minimum, maximum);
            rayPending = true;
        }
    }
}

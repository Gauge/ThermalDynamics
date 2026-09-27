using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// A grid's aerodynamics as geometry: the drag model's sums for a table of wind directions, the
    /// hull's rotational damping moments and its mass distribution. Rebuilt when the hull changes and
    /// evaluated every physics tick in constant time; see aerodynamics.md, AR19, AR5, AR6 and AR22.
    /// </summary>
    public sealed class AeroProfile
    {
        /// <summary>The coarse table: the 26 directions to a cell's neighbours.</summary>
        public const int CoarseDirections = 26;

        /// <summary>The fine table: the 98 directions to the shell two cells out.</summary>
        public const int FineDirections = 98;

        /// <summary>Drag coefficient of a surface element sweeping through the air as the hull turns.</summary>
        public const float RotationalDrag = 1.17f;

        private readonly Vector3[] directions;
        private readonly AeroSummary[] summaries;
        private readonly float sharpness;

        private readonly List<Vector3> faceCentres = new List<Vector3>();
        private readonly List<Vector3> faceNormals = new List<Vector3>();
        private float faceArea;

        private Vector3 dampingCentre = new Vector3(float.NaN);
        private readonly float[] damping = new float[6];

        /// <summary>The hull's mass, kg.</summary>
        public float Mass { get; private set; }

        /// <summary>The hull's centre of mass in grid-local metres.</summary>
        public Vector3 CentreOfMass { get; private set; }

        /// <summary>Moments of inertia about the centre of mass along the grid's axes, kg·m².</summary>
        public Vector3 Inertia { get; private set; }

        /// <summary>The number of wind directions tabled.</summary>
        public int Directions { get { return directions.Length; } }

        private AeroProfile(Vector3[] directions)
        {
            this.directions = directions;
            summaries = new AeroSummary[directions.Length];
            sharpness = directions.Length > CoarseDirections ? 24f : 10f;
        }

        internal AeroProfile(int directionCount)
            : this(Shell(directionCount > CoarseDirections ? 2 : 1)) { }

        /// <summary>Builds synchronously; tick callers should instead advance an AeroProfileBuilder.</summary>
        public static AeroProfile Build(GridModel grid, int directionCount)
        {
            AeroProfileBuilder builder = new AeroProfileBuilder(grid, directionCount, 0);
            while (!builder.Complete && !builder.Invalidated) builder.Advance(4096, 0);
            AeroProfile profile;
            if (!builder.TryGetResult(0, out profile)) throw new InvalidOperationException("Hull changed during profile construction.");
            return profile;
        }

        /// <summary>Unit directions to every cell on the cube shell <paramref name="radius"/> out.</summary>
        public static Vector3[] Shell(int radius)
        {
            List<Vector3> list = new List<Vector3>();
            for (int x = -radius; x <= radius; x++)
                for (int y = -radius; y <= radius; y++)
                    for (int z = -radius; z <= radius; z++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))) != radius) continue;
                        list.Add(Vector3.Normalize(new Vector3(x, y, z)));
                    }
            return list.ToArray();
        }

        /// <summary>The drag model's sums for air arriving from <paramref name="upstream"/>, interpolated from the table.</summary>
        public AeroSummary Summary(Vector3 upstream)
        {
            AeroSummary blended = new AeroSummary();
            float total = 0f;
            float fineness = 0f;

            for (int i = 0; i < directions.Length; i++)
            {
                float dot = Vector3.Dot(upstream, directions[i]);
                if (dot <= 0f) continue;
                float w = (float)Math.Pow(dot, sharpness);
                if (w < 1e-6f) continue;

                AeroSummary s = summaries[i];
                blended.Silhouette += s.Silhouette * w;
                blended.SilhouetteMoment += s.SilhouetteMoment * w;
                blended.Facing += s.Facing * w;
                blended.FacingMoment += s.FacingMoment * w;
                blended.Base += s.Base * w;
                blended.BaseMoment += s.BaseMoment * w;
                blended.Wetted += s.Wetted * w;
                blended.WettedMoment += s.WettedMoment * w;
                fineness += s.Fineness * w;
                total += w;
            }

            if (total <= 0f) return blended;
            float inverse = 1f / total;
            blended.Silhouette *= inverse;
            blended.SilhouetteMoment *= inverse;
            blended.Facing *= inverse;
            blended.FacingMoment *= inverse;
            blended.Base *= inverse;
            blended.BaseMoment *= inverse;
            blended.Wetted *= inverse;
            blended.WettedMoment *= inverse;
            blended.Fineness = fineness * inverse;
            return blended;
        }

        /// <summary>
        /// The aerodynamic force and its torque about the centre of mass, and the damping torque of the
        /// hull turning, all in grid axes, for air moving past the hull at <paramref name="airVelocity"/>
        /// (grid axes, m/s) of <paramref name="density"/> kg/m³, while the hull turns at
        /// <paramref name="angularVelocity"/> (grid axes, rad/s). The point the drag acts through is
        /// returned as well. The two torques are apart because only the damping is guarded.
        /// </summary>
        public void Evaluate(Vector3 airVelocity, float density, Vector3 angularVelocity, AeroCoefficients c,
            float kinematicViscosity, out Vector3 force, out Vector3 torque, out Vector3 damping,
            out Vector3 centreOfPressure)
        {
            force = Vector3.Zero;
            torque = Vector3.Zero;
            damping = Vector3.Zero;
            centreOfPressure = CentreOfMass;
            if (!(density > 0f)) return;

            if (airVelocity.LengthSquared() > 1e-6f)
            {
                Vector3 local = airVelocity;
                for (int pass = 0; pass < 2; pass++)
                {
                    float speed = local.Length();
                    if (speed <= 1e-3f) break;

                    Vector3 flow = local / speed;
                    AeroSummary summary = Summary(-flow);
                    float length = summary.Silhouette > 0f ? summary.Fineness * (float)Math.Sqrt(summary.Silhouette) : 0f;
                    float friction = AeroDrag.SkinFriction(kinematicViscosity > 0f ? speed * length / kinematicViscosity : 0f);

                    float area = AeroDrag.DragArea(summary, c, friction, out centreOfPressure);
                    if (area <= 0f)
                    {
                        centreOfPressure = CentreOfMass;
                        force = Vector3.Zero;
                        break;
                    }

                    force = flow * (0.5f * density * speed * speed * area);

                    // The second pass reads the air where the drag acts, which the hull's own turning
                    // moves: that is the damping of a turn in flight (AR6).
                    local = airVelocity - Vector3.Cross(angularVelocity, centreOfPressure - CentreOfMass);
                }

                torque = Vector3.Cross(centreOfPressure - CentreOfMass, force);
            }

            damping = Damping(angularVelocity, density);
        }

        /// <summary>
        /// The air's resistance to the hull turning, about the centre of mass: each exposed face sweeping
        /// into the air at `ω·r` meets `½ρ·C·(ωr)²` on its projected area, at arm `r`.
        /// </summary>
        public Vector3 Damping(Vector3 angularVelocity, float density)
        {
            if (!(density > 0f) || faceNormals.Count == 0) return Vector3.Zero;
            if (Vector3.DistanceSquared(dampingCentre, CentreOfMass) > 1e-6f || float.IsNaN(dampingCentre.X)) MeasureDamping();
            float half = 0.5f * density * RotationalDrag;
            Vector3 result = Vector3.Zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float w = axis == 0 ? angularVelocity.X : axis == 1 ? angularVelocity.Y : angularVelocity.Z;
                if (w == 0f) continue;
                float k = damping[(axis * 2) + (w > 0f ? 0 : 1)];
                float t = -half * k * w * Math.Abs(w);
                if (axis == 0) result.X = t;
                else if (axis == 1) result.Y = t;
                else result.Z = t;
            }
            return result;
        }

        /// <summary>
        /// The numerical guard, not physics: within one tick of <paramref name="seconds"/>, the force may
        /// bring the air's speed past the hull to zero along the flow and no further, and the damping may
        /// stop the turn about each axis and no further. A restoring moment is not guarded, because it
        /// reverses a turn by right. True where it bound, which a resolved case never does; see
        /// aerodynamics.md, AR22.
        /// </summary>
        public static bool Guard(ref Vector3 force, ref Vector3 damping, Vector3 airVelocity, Vector3 angularVelocity,
            float mass, Vector3 inertia, float seconds)
        {
            bool bound = false;
            if (seconds <= 0f) return false;

            float speed = airVelocity.Length();
            float magnitude = force.Length();
            if (mass > 0f && magnitude > 0f && magnitude * seconds > mass * speed)
            {
                force *= (mass * speed) / (magnitude * seconds);
                bound = true;
            }

            bound |= Cap(ref damping.X, angularVelocity.X, inertia.X, seconds);
            bound |= Cap(ref damping.Y, angularVelocity.Y, inertia.Y, seconds);
            bound |= Cap(ref damping.Z, angularVelocity.Z, inertia.Z, seconds);
            return bound;
        }

        private static bool Cap(ref float torque, float omega, float inertia, float seconds)
        {
            if (inertia <= 0f || torque == 0f || torque * omega >= 0f) return false;
            float limit = inertia * Math.Abs(omega) / seconds;
            if (Math.Abs(torque) <= limit) return false;
            torque = Math.Sign(torque) * limit;
            return true;
        }

        /// <summary>Moves the centre; the next damping read refreshes moments in one pass over exposed faces.</summary>
        public void SetCentreOfMass(Vector3 centre)
        {
            CentreOfMass = centre;
        }

        internal void SetMassProperties(float mass, Vector3 centre, Vector3 inertia, float area)
        {
            Mass = mass;
            CentreOfMass = centre;
            Inertia = inertia;
            faceArea = area;
            dampingCentre = centre;
        }

        internal void SetSummary(int index, AeroSummary summary) { summaries[index] = summary; }

        internal void AddSkin(Vector3 centre, Vector3 normal)
        {
            faceCentres.Add(centre);
            faceNormals.Add(normal);
            AccumulateDamping(centre - CentreOfMass, normal);
        }

        private void MeasureDamping()
        {
            Array.Clear(damping, 0, damping.Length);
            for (int i = 0; i < faceCentres.Count; i++) AccumulateDamping(faceCentres[i] - CentreOfMass, faceNormals[i]);
            dampingCentre = CentreOfMass;
        }

        private void AccumulateDamping(Vector3 r, Vector3 n)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 basis = axis == 0 ? Vector3.UnitX : axis == 1 ? Vector3.UnitY : Vector3.UnitZ;
                Vector3 motion = Vector3.Cross(basis, r);
                float arm = motion.Length();
                if (arm < 1e-4f) continue;
                float facing = Vector3.Dot(n, motion) / arm;
                float term = faceArea * Math.Abs(facing) * arm * arm * arm;
                if (facing > 0f) damping[axis * 2] += term;
                else if (facing < 0f) damping[(axis * 2) + 1] += term;
            }
        }
    }
}

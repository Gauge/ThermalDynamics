using System;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>The world-space wrench delivered to the engine; force is in N and moments in N·m.</summary>
    public struct AeroWrench
    {
        public Vector3 Force;
        public Vector3 Restoring;
        public Vector3 Damping;
        public bool Guarded;
    }

    /// <summary>Current-frame kinematics and coordinate conversion, independent of thermal solver state.</summary>
    public static class AeroForces
    {
        public const float SeaLevelDensity = 1.225f;
        public const float KinematicViscosity = 1.5e-5f;

        public static AeroWrench Evaluate(AeroProfile profile, Matrix orientation, Vector3 airVelocityWorld,
            Vector3 angularVelocityWorld, float density, float mass, float seconds)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (!Finite(orientation.Right) || !Finite(orientation.Up) || !Finite(orientation.Backward)
                || !Finite(airVelocityWorld) || !Finite(angularVelocityWorld) || !Finite(density)
                || !Finite(mass) || !Finite(seconds) || mass <= 0f || seconds <= 0f || density < 0f)
                throw new ArgumentException("Aerodynamic inputs must be finite, with positive mass and timestep.");
            Matrix inverse = Matrix.Transpose(orientation);
            Vector3 air = Vector3.TransformNormal(airVelocityWorld, inverse);
            Vector3 spin = Vector3.TransformNormal(angularVelocityWorld, inverse);
            Vector3 force, restoring, damping, pressure;
            profile.Evaluate(air, density, spin, AeroCoefficients.Fitted, KinematicViscosity,
                out force, out restoring, out damping, out pressure);
            if (!Finite(force) || !Finite(restoring) || !Finite(damping))
                throw new InvalidOperationException("Aerodynamic evaluation produced a nonfinite wrench.");
            bool guarded = AeroProfile.Guard(ref force, ref damping, air, spin, mass, profile.Inertia, seconds);
            AeroWrench result = new AeroWrench
            {
                Force = Vector3.TransformNormal(force, orientation),
                Restoring = Vector3.TransformNormal(restoring, orientation),
                Damping = Vector3.TransformNormal(damping, orientation),
                Guarded = guarded
            };
            if (!Finite(result.Force) || !Finite(result.Restoring) || !Finite(result.Damping)
                || !Finite(result.Restoring + result.Damping))
                throw new InvalidOperationException("World conversion produced a nonfinite wrench.");
            return result;
        }

        private static bool Finite(Vector3 v) { return Finite(v.X) && Finite(v.Y) && Finite(v.Z); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}

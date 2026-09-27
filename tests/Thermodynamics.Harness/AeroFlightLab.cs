using System;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>
    /// A rigid body flown on an <see cref="AeroProfile"/> at 60 Hz: the step 1 rigs for moments,
    /// damping and the numerical guard (aerodynamics.md, S5 to S8). The hull's speed is held and only
    /// its attitude is free, which is a hull whose thrust holds its speed while the air turns it.
    /// </summary>
    public static class AeroFlightLab
    {
        /// <summary>The physics tick.</summary>
        public const float Tick = 1f / 60f;

        /// <summary>Kinematic viscosity of sea-level air, m²/s.</summary>
        public const float Viscosity = 1.5e-5f;

        /// <summary>What one flight did.</summary>
        public sealed class Flight
        {
            public float FinalError;
            public float WorstLateError;
            public float MaxError;
            public int GuardBound;
            public int Steps;

            /// <summary>The last time, in seconds, the heading was more than 5° off the flight path.</summary>
            public float LastOutsideFive;
        }

        /// <summary>
        /// The shuttlecock: a one-cell heavy-armour boom twelve cells long with a seven-by-seven plate of
        /// light armour across its tail, so the frontal area sits behind the centre of mass when it flies
        /// boom-first, toward +Z.
        /// </summary>
        public static GridModel Shuttlecock()
        {
            GridModel grid = new GridModel(2.5f);
            for (int z = 1; z <= 12; z++) grid.Add(Catalog.HeavyArmor(), new Vector3I(0, 0, z));
            for (int x = -3; x <= 3; x++)
                for (int y = -3; y <= 3; y++)
                    grid.Add(Catalog.LightArmor(), new Vector3I(x, y, 0));
            return grid;
        }

        /// <summary>
        /// Flies <paramref name="profile"/> at <paramref name="speed"/> along its heading
        /// <paramref name="heading"/> (grid axes), kicked <paramref name="kickDegrees"/> in yaw, for
        /// <paramref name="seconds"/>, and reports the angle between the heading and the flight path.
        /// </summary>
        public static Flight Fly(AeroProfile profile, Vector3 heading, float speed, float density,
            float kickDegrees, float seconds, float tick = Tick)
        {
            ValidateProfile(profile);
            int steps = StepCount(seconds, tick);
            if (!Finite(heading) || !Finite(heading.LengthSquared()) || heading.LengthSquared() <= 0f
                || !Finite(speed) || speed < 0f || !Finite(density) || density < 0f || !Finite(kickDegrees))
                throw new ArgumentException("Flight inputs must be finite, with a nonzero heading and nonnegative speed and density.");

            Flight flight = new Flight();
            Vector3 path = Vector3.Normalize(heading);
            Matrix attitude = Matrix.CreateRotationY(kickDegrees * (float)Math.PI / 180f);
            Vector3 omega = Vector3.Zero;
            Vector3 inertia = profile.Inertia;
            flight.MaxError = Error(attitude, heading, path);

            for (int i = 0; i < steps; i++)
            {
                Matrix toGrid = Matrix.Transpose(attitude);
                Vector3 air = Vector3.TransformNormal(-path * speed, toGrid);

                Vector3 force, torque, damping, centre;
                profile.Evaluate(air, density, omega, AeroCoefficients.Fitted, Viscosity, out force, out torque, out damping, out centre);
                CheckForces(force, torque, damping, centre);
                if (AeroProfile.Guard(ref force, ref damping, air, omega, profile.Mass, inertia, tick)) flight.GuardBound++;
                torque += damping;

                Vector3 spin = inertia * omega;
                Vector3 accel = (torque - Vector3.Cross(omega, spin)) / inertia;
                omega += accel * tick;
                if (!Finite(omega)) throw new InvalidOperationException("The flight's angular velocity became nonfinite.");

                float angle = omega.Length() * tick;
                if (angle > 0f)
                {
                    Matrix turn = Matrix.CreateFromAxisAngle(Vector3.Normalize(omega), angle);
                    attitude = Matrix.Normalize(turn * attitude);
                }

                float error = Error(attitude, heading, path);
                if (!Finite(error)) throw new InvalidOperationException("The flight's attitude became nonfinite.");
                if (error > flight.MaxError) flight.MaxError = error;
                if (error > 5f) flight.LastOutsideFive = (i + 1) * tick;
                if ((i + 1) * tick >= seconds - 10f && error > flight.WorstLateError) flight.WorstLateError = error;
                flight.FinalError = error;
                flight.Steps++;
            }
            return flight;
        }

        /// <summary>Seconds for a hull spun at <paramref name="rate"/> about Y in still air to halve its spin.</summary>
        public static float SpinHalfLife(AeroProfile profile, float density, float rate, float limit, out int guardBound,
            float tick = Tick)
        {
            ValidateProfile(profile);
            int steps = StepCount(limit, tick);
            if (!Finite(density) || density < 0f || !Finite(rate) || rate == 0f)
                throw new ArgumentException("Spin inputs must have finite nonnegative density and a finite nonzero rate.");
            guardBound = 0;
            Vector3 omega = new Vector3(0f, rate, 0f);
            Vector3 inertia = profile.Inertia;
            for (int i = 0; i < steps; i++)
            {
                Vector3 force, torque, damping, centre;
                profile.Evaluate(Vector3.Zero, density, omega, AeroCoefficients.Fitted, Viscosity, out force, out torque, out damping, out centre);
                CheckForces(force, torque, damping, centre);
                if (AeroProfile.Guard(ref force, ref damping, Vector3.Zero, omega, profile.Mass, inertia, tick)) guardBound++;
                torque += damping;
                omega += (torque / inertia) * tick;
                if (!Finite(omega)) throw new InvalidOperationException("The spin's angular velocity became nonfinite.");
                if (Math.Abs(omega.Y) <= Math.Abs(rate) / 2f) return (i + 1) * tick;
            }
            return float.PositiveInfinity;
        }

        private static int StepCount(float seconds, float tick)
        {
            double count = Math.Round((double)seconds / tick);
            if (!Finite(seconds) || seconds <= 0f || !Finite(tick) || tick <= 0f || count < 1d || count > int.MaxValue)
                throw new ArgumentException("The duration must contain at least one finite positive tick and fit the step counter.");
            return (int)count;
        }

        private static void ValidateProfile(AeroProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            Vector3 inertia = profile.Inertia;
            if (!Finite(profile.Mass) || profile.Mass <= 0f || !Finite(profile.CentreOfMass)
                || !Finite(inertia) || inertia.X <= 0f || inertia.Y <= 0f || inertia.Z <= 0f)
                throw new ArgumentException("The flight rig needs finite positive mass and inertia.", "profile");
        }

        private static void CheckForces(Vector3 force, Vector3 torque, Vector3 damping, Vector3 centre)
        {
            if (!Finite(force) || !Finite(torque) || !Finite(damping) || !Finite(centre))
                throw new InvalidOperationException("The aerodynamic model returned a nonfinite force, torque or centre.");
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Finite(Vector3 value)
        {
            return Finite(value.X) && Finite(value.Y) && Finite(value.Z);
        }

        private static float Error(Matrix attitude, Vector3 heading, Vector3 path)
        {
            Vector3 nose = Vector3.TransformNormal(Vector3.Normalize(heading), attitude);
            float dot = MathHelper.Clamp(Vector3.Dot(nose, path), -1f, 1f);
            return (float)(Math.Acos(dot) * 180d / Math.PI);
        }

        /// <summary>The step 1 moment rigs, as a table.</summary>
        public static string Report()
        {
            StringBuilder sb = new StringBuilder();
            AeroProfile profile = AeroProfile.Build(Shuttlecock(), AeroProfile.FineDirections);
            Flight forward = Fly(profile, Vector3.Backward, 50f, 1.225f, 20f, 60f);
            Flight backward = Fly(profile, Vector3.Forward, 50f, 1.225f, 20f, 60f);

            AeroProfile cube = AeroProfile.Build(AeroCalibrationLab.Box(8, 8, 8), AeroProfile.FineDirections);
            int thick, thin;
            float dense = SpinHalfLife(cube, 1f, 1f, 3600f, out thick);
            float sparse = SpinHalfLife(cube, 0.5f, 1f, 3600f, out thin);

            sb.AppendLine("Step 1 moment rigs, 60 Hz, sea-level air, 50 m/s");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  S5 shuttlecock boom-first, kicked 20°: last outside 5° at {0:0.0} s, worst of the last 10 s {1:0.00}°, after 60 s {2:0.00}°, guard bound {3}",
                forward.LastOutsideFive, forward.WorstLateError, forward.FinalError, forward.GuardBound));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  S6 shuttlecock plate-first, kicked 20°: max {0:0.0}°, after 60 s {1:0.0}°, guard bound {2}",
                backward.MaxError, backward.FinalError, backward.GuardBound));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  S7 cube spun at 1 rad/s: half-life {0:0.0} s at density 1, {1:0.0} s at 0.5, guard bound {2} and {3}",
                dense, sparse, thick, thin));
            return sb.ToString();
        }
    }
}

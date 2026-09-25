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
            float kickDegrees, float seconds)
        {
            Flight flight = new Flight();
            Vector3 path = Vector3.Normalize(heading);
            Matrix attitude = Matrix.CreateRotationY(kickDegrees * (float)Math.PI / 180f);
            Vector3 omega = Vector3.Zero;
            Vector3 inertia = profile.Inertia;
            int steps = (int)Math.Round(seconds / Tick);

            for (int i = 0; i < steps; i++)
            {
                Matrix toGrid = Matrix.Transpose(attitude);
                Vector3 air = Vector3.TransformNormal(-path * speed, toGrid);

                Vector3 force, torque, damping, centre;
                profile.Evaluate(air, density, omega, AeroCoefficients.Fitted, Viscosity, out force, out torque, out damping, out centre);
                if (AeroProfile.Guard(ref force, ref damping, air, omega, profile.Mass, inertia, Tick)) flight.GuardBound++;
                torque += damping;

                Vector3 spin = inertia * omega;
                Vector3 accel = (torque - Vector3.Cross(omega, spin)) / inertia;
                omega += accel * Tick;

                float angle = omega.Length() * Tick;
                if (angle > 0f)
                {
                    Matrix turn = Matrix.CreateFromAxisAngle(Vector3.Normalize(omega), angle);
                    attitude = Matrix.Normalize(turn * attitude);
                }

                float error = Error(attitude, heading, path);
                if (error > flight.MaxError) flight.MaxError = error;
                if (i * Tick >= seconds - 10f && error > flight.WorstLateError) flight.WorstLateError = error;
                flight.FinalError = error;
                flight.Steps++;
            }
            return flight;
        }

        /// <summary>Seconds for a hull spun at <paramref name="rate"/> about Y in still air to halve its spin.</summary>
        public static float SpinHalfLife(AeroProfile profile, float density, float rate, float limit, out int guardBound)
        {
            guardBound = 0;
            Vector3 omega = new Vector3(0f, rate, 0f);
            Vector3 inertia = profile.Inertia;
            for (int i = 0; i * Tick < limit; i++)
            {
                Vector3 force, torque, damping, centre;
                profile.Evaluate(Vector3.Zero, density, omega, AeroCoefficients.Fitted, Viscosity, out force, out torque, out damping, out centre);
                if (AeroProfile.Guard(ref force, ref damping, Vector3.Zero, omega, profile.Mass, inertia, Tick)) guardBound++;
                torque += damping;
                omega += (torque / inertia) * Tick;
                if (omega.Y <= rate / 2f) return i * Tick;
            }
            return float.PositiveInfinity;
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
                "  S5 shuttlecock boom-first, kicked 20°: max {0:0.0}°, worst of the last 10 s {1:0.00}°, after 60 s {2:0.00}°, guard bound {3}",
                forward.MaxError, forward.WorstLateError, forward.FinalError, forward.GuardBound));
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

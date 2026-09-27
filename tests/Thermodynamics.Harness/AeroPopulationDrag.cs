using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>Static straight-flight profile drag and bounded terminal speeds; no native control or hover claim.</summary>
    public static class AeroPopulationDrag
    {
        public const double CeilingCap = 128;
        public const double SpeedTolerance = 0.001;

        public struct Bracket
        {
            public double Lower;
            public double? Upper;
        }

        public static Bracket Terminal(Func<double, double> drag, double thrust)
        {
            if (!double.IsFinite(thrust) || thrust < 0) throw new ArgumentException("Invalid thrust");
            if (thrust == 0) return new Bracket { Lower = 0, Upper = 0 };
            double last = -1;
            for (int i = 0; i <= 32; i++)
            {
                double f = drag(CeilingCap * i / 32);
                if (!double.IsFinite(f) || f < 0 || f < last - Math.Max(1, last) * 1e-6)
                    throw new InvalidDataException("Nonfinite, negative or nonmonotone drag");
                last = f;
            }
            if (last < thrust) return new Bracket { Lower = CeilingCap, Upper = null };
            double low = 0, high = CeilingCap;
            while (high - low > SpeedTolerance)
            {
                double mid = (low + high) / 2;
                double force = drag(mid);
                if (!double.IsFinite(force) || force < 0) throw new InvalidDataException("Invalid drag during bisection");
                if (force <= thrust) low = mid;
                else high = mid;
            }
            return new Bracket { Lower = low, Upper = high };
        }

        public static Func<double, double> Force(IList<AeroProfile> profiles, IList<MatrixD> frames, Vector3D heading)
        {
            if (profiles.Count == 0 || profiles.Count != frames.Count || !heading.IsValid() || heading.LengthSquared() < 1e-12)
                throw new ArgumentException("Expected profiles, frames and a finite heading");
            heading.Normalize();
            Vector3[] local = new Vector3[profiles.Count];
            for (int i = 0; i < profiles.Count; i++)
            {
                local[i] = Vector3.Normalize((Vector3)Vector3D.TransformNormal(heading, MatrixD.Transpose(frames[i].GetOrientation())));
                Vector3 ignored;
                float pressure = AeroDrag.DragArea(profiles[i].Summary(local[i]), AeroCoefficients.Fitted, 0, out ignored);
                if (!float.IsFinite(pressure) || pressure < 0)
                    throw new InvalidDataException("Negative pressure-only drag area: monotone ceiling is unresolved");
            }
            return speed =>
            {
                if (!double.IsFinite(speed) || speed < 0 || speed > CeilingCap) throw new ArgumentException("Speed outside the registered bracket");
                double total = 0;
                for (int i = 0; i < profiles.Count; i++)
                {
                    Vector3 force, restoring, damping, centre;
                    profiles[i].Evaluate(-local[i] * (float)speed, AeroForces.SeaLevelDensity, Vector3.Zero,
                        AeroCoefficients.Fitted, AeroForces.KinematicViscosity, out force, out restoring, out damping, out centre);
                    if (!force.IsValid()) throw new InvalidDataException("Nonfinite profile force");
                    total += force.Length();
                }
                return total;
            };
        }

        public static string Report(string path, string thrustFile)
        {
            using (JsonDocument inputs = JsonDocument.Parse(AeroPopulationInputs.Json(path)))
            using (JsonDocument thrust = JsonDocument.Parse(File.ReadAllText(thrustFile)))
            {
                JsonElement data = inputs.RootElement, root = thrust.RootElement;
                if (root.GetProperty("schema").GetInt32() != 1) throw new InvalidDataException("Unknown thrust schema");
                foreach (string key in new[] { "blueprint_sha256", "harness_mvid", "core_mvid" })
                    if (root.GetProperty(key).GetString() != data.GetProperty(key).GetString())
                        throw new InvalidDataException("Thrust provenance mismatch: " + key);
                double rating = root.GetProperty("capacity_n").GetDouble();
                double uncertainty = root.GetProperty("tolerance_n").GetDouble();
                if (!double.IsFinite(rating) || rating <= 0 || !double.IsFinite(uncertainty) || uncertainty < 0)
                    throw new InvalidDataException("Invalid thrust or uncertainty");
                var vector = root.GetProperty("direction");
                Vector3D direction = new Vector3D(vector[0].GetDouble(), vector[1].GetDouble(), vector[2].GetDouble());
                var ships = Blueprints.Read(path);
                if (ships.Count != 1) throw new InvalidDataException("Blueprint changed while reading");
                List<AeroProfile> profiles = new List<AeroProfile>();
                List<MatrixD> frames = new List<MatrixD>();
                foreach (Blueprints.Grid grid in ships[0].Grids)
                {
                    if (grid.Frame == null || !grid.Frame.Valid) throw new InvalidDataException("Invalid grid frame");
                    profiles.Add(AeroProfile.Build(grid.Builder.Grid, AeroProfile.FineDirections));
                    frames.Add(grid.Frame.World);
                }
                var force = Force(profiles, frames, direction);
                Bracket lower = Terminal(v => { double f = force(v); return f + Math.Max(1, f) * 2e-6; }, Math.Max(0, rating - uncertainty));
                Bracket upper = Terminal(v => { double f = force(v); return Math.Max(0, f - Math.Max(1, f) * 2e-6); }, rating + uncertainty);
                double at100 = force(100);
                double margin = uncertainty + Math.Max(1, at100) * 2e-6;
                string p1 = at100 > rating + margin ? "overpowered" : at100 < rating - margin ? "holds" : "unresolved";
                return JsonSerializer.Serialize(new
                {
                    schema = 1, kind = "aero-population-drag", drag_60_n = force(60), drag_100_n = at100,
                    thrust_n = rating, force_tolerance_n = margin, p1,
                    ceiling_lower_mps = lower.Lower, ceiling_upper_mps = upper.Upper,
                    right_censored = upper.Upper == null, profiles = profiles.Count,
                    blueprint_sha256 = data.GetProperty("blueprint_sha256").GetString(),
                    harness_mvid = data.GetProperty("harness_mvid").GetString(),
                    core_mvid = data.GetProperty("core_mvid").GetString()
                });
            }
        }
    }
}

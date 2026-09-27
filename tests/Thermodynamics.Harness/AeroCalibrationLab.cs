using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>
    /// Fits the subsonic drag model to White's Table 7.3, Hoerner and the hemispheres, and predicts each
    /// body from a fit that did not see it (leave-one-out). The bodies are built from cells, flown with
    /// the air coming from +Z. See aerodynamics.md, Round 5.
    /// </summary>
    public static class AeroCalibrationLab
    {
        /// <summary>Default radius of the round bodies, in cells.</summary>
        public const int Radius = 8;

        /// <summary>Round 5's radius, where the reconstructed normal has converged on blunt bodies.</summary>
        public const int CalibrationRadius = 24;

        /// <summary>A body, the direction the air comes from, and its reference coefficient.</summary>
        public sealed class Rig
        {
            public string Name;
            public float Reference;
            public bool Scored;
            public AeroSummary Summary;
        }

        /// <summary>The in-sample fit, and each body predicted without itself.</summary>
        public sealed class Result
        {
            public AeroCoefficients Coefficients;
            public float LabFriction;
            public double Residual;
            public readonly List<Rig> Rigs = new List<Rig>();
            public readonly Dictionary<string, float> InSample = new Dictionary<string, float>();
            public readonly Dictionary<string, float> OutOfSample = new Dictionary<string, float>();

            /// <summary>Scored bodies predicted out of sample within <paramref name="band"/> of their reference.</summary>
            public int Within(float band)
            {
                int n = 0;
                foreach (Rig rig in Rigs)
                {
                    if (!rig.Scored) continue;
                    if (Math.Abs((OutOfSample[rig.Name] / rig.Reference) - 1f) <= band) n++;
                }
                return n;
            }

            /// <summary>The number of scored bodies.</summary>
            public int Scored
            {
                get { return Rigs.FindAll(r => r.Scored).Count; }
            }
        }

        private static readonly Vector3 Upstream = Vector3.Backward;

        private static readonly float[] ConeAngles = { 10f, 20f, 30f, 40f, 60f, 75f };
        private static readonly float[] ConeDrag = { 0.30f, 0.40f, 0.55f, 0.65f, 0.80f, 1.05f };
        private static readonly float[] CylinderRatios = { 0.5f, 1f, 2f, 4f, 8f };
        private static readonly float[] CylinderDrag = { 1.15f, 0.90f, 0.85f, 0.87f, 0.99f };
        private static readonly float[] EllipsoidRatios = { 0.75f, 1f, 2f, 4f, 8f };
        private static readonly float[] EllipsoidSubcritical = { 0.5f, 0.47f, 0.27f, 0.25f, 0.2f };

        /// <summary>Round 5's bodies at <paramref name="radius"/> cells, summaries built.</summary>
        public static List<Rig> Rigs(int radius)
        {
            List<Rig> rigs = new List<Rig>();
            for (int i = 0; i < ConeAngles.Length; i++)
            {
                rigs.Add(Make("cone " + ConeAngles[i].ToString("0", CultureInfo.InvariantCulture) + "°",
                    ConeDrag[i], Cone(ConeAngles[i], radius), Upstream));
            }
            rigs.Add(Make("disk", 1.17f, Cylinder(0.5f / radius, radius), Upstream));
            for (int i = 0; i < CylinderRatios.Length; i++)
            {
                rigs.Add(Make("cylinder L/d " + CylinderRatios[i].ToString("0.#", CultureInfo.InvariantCulture),
                    CylinderDrag[i], Cylinder(CylinderRatios[i], radius), Upstream));
            }

            int side = radius;
            rigs.Add(Make("cube face-on", 1.07f, Box(side, side, side), Upstream));
            rigs.Add(Make("cube edge-on", 0.81f, Box(side, side, side), new Vector3(1f, 0f, 1f)));
            rigs.Add(Make("square plate", 1.18f, Box(side, side, 1), Upstream));

            for (int i = 0; i < EllipsoidRatios.Length; i++)
            {
                rigs.Add(Make("ellipsoid L/d " + EllipsoidRatios[i].ToString("0.##", CultureInfo.InvariantCulture),
                    EllipsoidSubcritical[i], Ellipsoid(EllipsoidRatios[i], radius), Upstream));
            }
            rigs.Add(Make("hemisphere, curve first", 0.42f, Hemisphere(radius), Upstream));
            rigs.Add(Make("hemisphere, flat first", 1.17f, Hemisphere(radius), -Upstream));

            rigs.Add(Make("W3 blunt base", 0f, Cylinder(2f, radius), Upstream));
            rigs.Add(Make("W3 tapered tail", 0f, BoatTailed(2f, radius), Upstream));
            return rigs;
        }

        /// <summary>Round 5: fit every scored body, then predict each without itself.</summary>
        public static Result Fit(List<Rig> rigs)
        {
            Result result = new Result();
            result.Rigs.AddRange(rigs);
            List<Rig> scored = rigs.FindAll(r => r.Scored);

            double residual;
            double[] all = FitTo(scored, out residual);
            result.Coefficients = FromParameters(all);
            result.LabFriction = (float)Math.Exp(all[5]);
            result.Residual = residual;
            foreach (Rig rig in rigs)
            {
                result.InSample[rig.Name] = AeroDrag.Coefficient(rig.Summary, result.Coefficients, result.LabFriction);
            }

            foreach (Rig left in scored)
            {
                List<Rig> others = scored.FindAll(r => r != left);
                double ignored;
                double[] p = FitTo(others, out ignored);
                result.OutOfSample[left.Name] = AeroDrag.Coefficient(left.Summary, FromParameters(p), (float)Math.Exp(p[5]));
            }
            foreach (Rig rig in rigs)
            {
                if (!rig.Scored) result.OutOfSample[rig.Name] = result.InSample[rig.Name];
            }
            return result;
        }

        private static double[] FitTo(List<Rig> set, out double residual)
        {
            Func<double[], double> objective = p =>
            {
                AeroCoefficients c = FromParameters(p);
                float friction = (float)Math.Exp(p[5]);
                double sum = 0d;
                for (int i = 0; i < set.Count; i++)
                {
                    float model = AeroDrag.Coefficient(set[i].Summary, c, friction);
                    if (!(model > 0f)) return 1e9;
                    double error = Math.Log(model / set[i].Reference);
                    sum += error * error;
                }
                return sum;
            };

            double[] start =
            {
                0.0, Math.Log(0.9), Math.Log(0.15), Math.Log(0.4), Math.Log(1.1), Math.Log(0.005),
            };
            double best = double.MaxValue;
            double[] found = start;
            foreach (double step in new[] { 0.4, 1.0 })
            {
                double value;
                double[] p = NelderMead.Minimise(objective, found, step, 6000, out value);
                if (value < best) { best = value; found = p; }
            }
            residual = best;
            return found;
        }

        private static AeroCoefficients FromParameters(double[] p)
        {
            return new AeroCoefficients
            {
                Offset = (float)p[0],
                Stagnation = (float)Math.Exp(p[1]),
                BaseAttached = (float)Math.Exp(p[2]),
                BaseSeparated = (float)Math.Exp(p[3]),
                ReattachLength = (float)Math.Exp(p[4]),
            };
        }

        /// <summary>The fit as a table.</summary>
        public static string Report(Result result)
        {
            StringBuilder sb = new StringBuilder();
            AeroCoefficients c = result.Coefficients;
            sb.AppendLine("Subsonic drag model: every sourced body, each predicted without itself");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  offset {0:0.00000}  stagnation {1:0.00000}  base attached {2:0.00000}  base separated {3:0.00000}  reattach {4:0.00000}  lab friction {5:0.00000}  residual {6:0.0000}",
                c.Offset, c.Stagnation, c.BaseAttached, c.BaseSeparated, c.ReattachLength, result.LabFriction, result.Residual));
            sb.AppendLine();
            sb.AppendLine(string.Format("  {0,-26} {1,6} {2,9} {3,8} {4,12} {5,8}", "body", "ref", "in-sample", "error", "out-of-sample", "error"));
            foreach (Rig rig in result.Rigs)
            {
                float inside = result.InSample[rig.Name];
                float outside = result.OutOfSample[rig.Name];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-26} {1,6} {2,9:0.000} {3,8} {4,12:0.000} {5,8}",
                    rig.Name,
                    rig.Scored ? rig.Reference.ToString("0.00", CultureInfo.InvariantCulture) : "—",
                    inside, Error(rig, inside), rig.Scored ? outside.ToString("0.000", CultureInfo.InvariantCulture) : "", Error(rig, outside)));
            }
            sb.AppendLine();
            sb.AppendLine(string.Format("  W1: {0} of {1} within ±25 % out of sample; W2: {2} of {1} within ±50 %",
                result.Within(0.25f), result.Scored, result.Within(0.5f)));
            sb.AppendLine();
            sb.AppendLine("  X4 resolution invariance, fitted coefficients: C_D at radius 4, 8, 24");
            foreach (KeyValuePair<string, float[]> row in Invariance(result.Coefficients, result.LabFriction))
            {
                float at24 = row.Value[2];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "    {0,-22} {1:0.000} ({2:+0.0%;-0.0%;0.0%})  {3:0.000} ({4:+0.0%;-0.0%;0.0%})  {5:0.000}",
                    row.Key, row.Value[0], (row.Value[0] / at24) - 1f, row.Value[1], (row.Value[1] / at24) - 1f, at24));
            }
            return sb.ToString();
        }

        /// <summary>X4: four bodies at radius 4, 8 and 24, read with the same coefficients.</summary>
        public static List<KeyValuePair<string, float[]>> Invariance(AeroCoefficients c, float friction)
        {
            List<KeyValuePair<string, float[]>> rows = new List<KeyValuePair<string, float[]>>();
            int[] radii = { 4, 8, CalibrationRadius };
            string[] names = { "square plate", "cube face-on", "cone 30°", "hemisphere" };
            for (int n = 0; n < names.Length; n++)
            {
                float[] values = new float[radii.Length];
                for (int i = 0; i < radii.Length; i++)
                {
                    int r = radii[i];
                    GridModel body = n == 0 ? Box(r, r, 1) : n == 1 ? Box(r, r, r) : n == 2 ? Cone(30f, r) : Hemisphere(r);
                    values[i] = AeroDrag.Coefficient(AeroGeometry.View(body, Upstream), c, friction);
                }
                rows.Add(new KeyValuePair<string, float[]>(names[n], values));
            }
            return rows;
        }

        private static string Error(Rig rig, float value)
        {
            return rig.Scored
                ? ((value / rig.Reference) - 1f).ToString("+0.0%;-0.0%", CultureInfo.InvariantCulture)
                : "";
        }

        private static Rig Make(string name, float reference, GridModel grid, Vector3 upstream)
        {
            return new Rig
            {
                Name = name,
                Reference = reference,
                Scored = reference > 0f,
                Summary = AeroDrag.Summarise(AeroGeometry.View(grid, upstream)),
            };
        }

        private static GridModel Grid()
        {
            return new GridModel(2.5f);
        }

        private static void Add(GridModel grid, int x, int y, int z)
        {
            grid.Add(Catalog.HeavyArmor(), new Vector3I(x, y, z));
        }

        private static bool InDisk(int x, int y, float radius)
        {
            return (x * x) + (y * y) <= radius * radius;
        }

        /// <summary>A cone of half-angle <paramref name="degrees"/>, apex upstream, flat base downstream.</summary>
        public static GridModel Cone(float degrees)
        {
            return Cone(degrees, Radius);
        }

        /// <summary>The same body at a chosen radius in cells.</summary>
        public static GridModel Cone(float degrees, int radius)
        {
            GridModel grid = Grid();
            double length = radius / Math.Tan(degrees * Math.PI / 180d);
            int cells = Math.Max(1, (int)Math.Round(length));
            for (int z = 0; z < cells; z++)
            {
                float r = radius * (float)(1d - ((z + 0.5d) / length));
                if (r <= 0f) continue;
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                        if (InDisk(x, y, r)) Add(grid, x, y, z);
            }
            return grid;
        }

        /// <summary>A flat-faced cylinder of length-to-diameter <paramref name="ratio"/>, axis along the flow.</summary>
        public static GridModel Cylinder(float ratio)
        {
            return Cylinder(ratio, Radius);
        }

        /// <summary>The same body at a chosen radius in cells.</summary>
        public static GridModel Cylinder(float ratio, int radius)
        {
            GridModel grid = Grid();
            int cells = Math.Max(1, (int)Math.Round(ratio * 2 * radius));
            for (int z = 0; z < cells; z++)
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                        if (InDisk(x, y, radius)) Add(grid, x, y, z);
            return grid;
        }

        /// <summary>
        /// The same cylinder with a tail tapering at 15° to half its radius behind it, so the frontal
        /// area is the cylinder's and the base is a quarter of it.
        /// </summary>
        public static GridModel BoatTailed(float ratio)
        {
            return BoatTailed(ratio, Radius);
        }

        /// <summary>The same body at a chosen radius in cells.</summary>
        public static GridModel BoatTailed(float ratio, int radius)
        {
            GridModel grid = Cylinder(ratio, radius);
            double taper = Math.Tan(15d * Math.PI / 180d);
            int tail = (int)Math.Round((radius / 2d) / taper);
            for (int k = 1; k <= tail; k++)
            {
                float r = radius - (float)(k * taper);
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                        if (InDisk(x, y, r)) Add(grid, x, y, -k);
            }
            return grid;
        }

        /// <summary>A hemisphere, curved side toward +Z and flat side toward -Z.</summary>
        public static GridModel Hemisphere()
        {
            return Hemisphere(Radius);
        }

        /// <summary>The same body at a chosen radius in cells.</summary>
        public static GridModel Hemisphere(int radius)
        {
            GridModel grid = Grid();
            for (int z = 0; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                        if ((x * x) + (y * y) + (z * z) <= radius * radius) Add(grid, x, y, z);
            return grid;
        }

        /// <summary>A prolate ellipsoid, axis along the flow, of length-to-diameter <paramref name="ratio"/>.</summary>
        public static GridModel Ellipsoid(float ratio)
        {
            return Ellipsoid(ratio, Radius);
        }

        /// <summary>The same body at a chosen radius in cells.</summary>
        public static GridModel Ellipsoid(float ratio, int radius)
        {
            GridModel grid = Grid();
            double half = ratio * radius;
            int reach = (int)Math.Ceiling(half);
            for (int z = -reach; z <= reach; z++)
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        double q = (((double)x * x) + ((double)y * y)) / ((double)radius * radius)
                            + ((double)z * z) / (half * half);
                        if (q <= 1d) Add(grid, x, y, z);
                    }
            return grid;
        }

        /// <summary>A solid box of cells.</summary>
        public static GridModel Box(int x, int y, int z)
        {
            GridModel grid = Grid();
            for (int i = 0; i < x; i++)
                for (int j = 0; j < y; j++)
                    for (int k = 0; k < z; k++)
                        Add(grid, i, j, k);
            return grid;
        }
    }

    /// <summary>Nelder-Mead simplex minimisation, for fitting a handful of coefficients.</summary>
    public static class NelderMead
    {
        /// <summary>Minimises <paramref name="f"/> from <paramref name="start"/>.</summary>
        public static double[] Minimise(Func<double[], double> f, double[] start, double step, int iterations,
            out double value)
        {
            int n = start.Length;
            double[][] simplex = new double[n + 1][];
            double[] values = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                simplex[i] = (double[])start.Clone();
                if (i > 0) simplex[i][i - 1] += step;
                values[i] = f(simplex[i]);
            }

            for (int it = 0; it < iterations; it++)
            {
                Array.Sort(values, simplex);
                if (Math.Abs(values[n] - values[0]) < 1e-12) break;

                double[] centroid = new double[n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        centroid[j] += simplex[i][j] / n;

                double[] reflected = Blend(centroid, simplex[n], -1d);
                double r = f(reflected);
                if (r < values[0])
                {
                    double[] expanded = Blend(centroid, simplex[n], -2d);
                    double e = f(expanded);
                    if (e < r) { simplex[n] = expanded; values[n] = e; }
                    else { simplex[n] = reflected; values[n] = r; }
                }
                else if (r < values[n - 1])
                {
                    simplex[n] = reflected; values[n] = r;
                }
                else
                {
                    double[] contracted = Blend(centroid, simplex[n], 0.5d);
                    double c = f(contracted);
                    if (c < values[n]) { simplex[n] = contracted; values[n] = c; }
                    else
                    {
                        for (int i = 1; i <= n; i++)
                        {
                            simplex[i] = Blend(simplex[0], simplex[i], 0.5d);
                            values[i] = f(simplex[i]);
                        }
                    }
                }
            }

            Array.Sort(values, simplex);
            value = values[0];
            return simplex[0];
        }

        private static double[] Blend(double[] centroid, double[] point, double t)
        {
            double[] result = new double[centroid.Length];
            for (int i = 0; i < centroid.Length; i++) result[i] = centroid[i] + (t * (point[i] - centroid[i]));
            return result;
        }
    }
}

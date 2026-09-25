using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>
    /// Fits the subsonic drag model to White's Table 7.3 and predicts the bodies it was not fitted to.
    /// The bodies are built from cells, flown with the air coming from +Z. Calibration is the cones
    /// from 10° to 75° and the flat-faced cylinders; everything else is held out. See aerodynamics.md,
    /// Step 1's criteria.
    /// </summary>
    public static class AeroCalibrationLab
    {
        /// <summary>Radius of every round body, in cells.</summary>
        public const int Radius = 8;

        /// <summary>A body, the direction the air comes from, and White's coefficient for it.</summary>
        public sealed class Rig
        {
            public string Name;
            public float Reference;
            public bool Calibration;
            public AeroView View;
        }

        /// <summary>The fit and what it predicts.</summary>
        public sealed class Result
        {
            public AeroCoefficients Coefficients;
            public float LabFriction;
            public double Residual;
            public readonly List<Rig> Rigs = new List<Rig>();
            public readonly Dictionary<string, float> Predicted = new Dictionary<string, float>();
        }

        private static readonly Vector3 Upstream = Vector3.Backward;

        private static readonly float[] ConeAngles = { 10f, 20f, 30f, 40f, 60f, 75f };
        private static readonly float[] ConeDrag = { 0.30f, 0.40f, 0.55f, 0.65f, 0.80f, 1.05f };
        private static readonly float[] CylinderRatios = { 0.5f, 1f, 2f, 4f, 8f };
        private static readonly float[] CylinderDrag = { 1.15f, 0.90f, 0.85f, 0.87f, 0.99f };

        /// <summary>Every rig, views built.</summary>
        public static List<Rig> Rigs()
        {
            List<Rig> rigs = new List<Rig>();
            for (int i = 0; i < ConeAngles.Length; i++)
            {
                rigs.Add(Make("cone " + ConeAngles[i].ToString("0", CultureInfo.InvariantCulture) + "°",
                    ConeDrag[i], true, Cone(ConeAngles[i]), Upstream));
            }
            for (int i = 0; i < CylinderRatios.Length; i++)
            {
                rigs.Add(Make("cylinder L/d " + CylinderRatios[i].ToString("0.#", CultureInfo.InvariantCulture),
                    CylinderDrag[i], true, Cylinder(CylinderRatios[i]), Upstream));
            }

            rigs.Add(Make("S1 cube face-on", 1.07f, false, Box(8, 8, 8), Upstream));
            rigs.Add(Make("S2 plate", 1.18f, false, Box(8, 8, 1), Upstream));
            rigs.Add(Make("S3 cube edge-on", 0.81f, false, Box(8, 8, 8), new Vector3(1f, 0f, 1f)));
            rigs.Add(Make("S4 blunt base", 0f, false, Cylinder(2f), Upstream));
            rigs.Add(Make("S4 tapered tail", 0f, false, BoatTailed(2f), Upstream));
            return rigs;
        }

        /// <summary>Fits the calibration rigs and predicts the held-out ones.</summary>
        public static Result Fit(List<Rig> rigs)
        {
            List<Rig> calibration = rigs.FindAll(r => r.Calibration);

            Func<double[], double> objective = p =>
            {
                AeroCoefficients c = FromParameters(p);
                float friction = (float)Math.Exp(p[5]);
                double sum = 0d;
                for (int i = 0; i < calibration.Count; i++)
                {
                    float model = AeroDrag.Coefficient(calibration[i].View, c, friction);
                    if (!(model > 0f)) return 1e9;
                    double error = Math.Log(model / calibration[i].Reference);
                    sum += error * error;
                }
                return sum;
            };

            double[] start =
            {
                Math.Log(0.8), Math.Log(2.0), Math.Log(0.4), Math.Log(0.1), Math.Log(1.0), Math.Log(0.005),
            };
            double residual;
            double[] best = NelderMead.Minimise(objective, start, 0.4, 4000, out residual);

            Result result = new Result();
            result.Coefficients = FromParameters(best);
            result.LabFriction = (float)Math.Exp(best[5]);
            result.Residual = residual;
            result.Rigs.AddRange(rigs);
            for (int i = 0; i < rigs.Count; i++)
            {
                result.Predicted[rigs[i].Name] = AeroDrag.Coefficient(rigs[i].View, result.Coefficients, result.LabFriction);
            }
            return result;
        }

        private static AeroCoefficients FromParameters(double[] p)
        {
            return new AeroCoefficients
            {
                Forebody = (float)Math.Exp(p[0]),
                Falloff = (float)Math.Exp(p[1]),
                BaseNear = (float)Math.Exp(p[2]),
                BaseFar = (float)Math.Exp(p[3]),
                BaseLength = (float)Math.Exp(p[4]),
            };
        }

        /// <summary>The fit as a table.</summary>
        public static string Report(Result result)
        {
            StringBuilder sb = new StringBuilder();
            AeroCoefficients c = result.Coefficients;
            sb.AppendLine("Subsonic drag model fitted to White, Fluid Mechanics, Table 7.3 (Re >= 1e4)");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  forebody {0:0.000}  falloff {1:0.000}  base near {2:0.000}  base far {3:0.000}  base length {4:0.000}  lab friction {5:0.00000}  residual {6:0.0000}",
                c.Forebody, c.Falloff, c.BaseNear, c.BaseFar, c.BaseLength, result.LabFriction, result.Residual));
            sb.AppendLine();
            sb.AppendLine(string.Format("  {0,-22} {1,6} {2,8} {3,8} {4,9} {5,9}", "body", "set", "White", "model", "error", "fineness"));
            foreach (Rig rig in result.Rigs)
            {
                float model = result.Predicted[rig.Name];
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-22} {1,6} {2,8} {3,8:0.000} {4,9} {5,9:0.00}",
                    rig.Name, rig.Calibration ? "fit" : "held",
                    rig.Reference > 0f ? rig.Reference.ToString("0.00", CultureInfo.InvariantCulture) : "—",
                    model,
                    rig.Reference > 0f ? ((model / rig.Reference) - 1f).ToString("+0.0%;-0.0%", CultureInfo.InvariantCulture) : "",
                    AeroDrag.Fineness(rig.View)));
            }
            return sb.ToString();
        }

        private static Rig Make(string name, float reference, bool calibration, GridModel grid, Vector3 upstream)
        {
            return new Rig
            {
                Name = name,
                Reference = reference,
                Calibration = calibration,
                View = AeroGeometry.View(grid, upstream),
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
            GridModel grid = Grid();
            double length = Radius / Math.Tan(degrees * Math.PI / 180d);
            int cells = Math.Max(1, (int)Math.Round(length));
            for (int z = 0; z < cells; z++)
            {
                float r = Radius * (float)(1d - ((z + 0.5d) / length));
                if (r <= 0f) continue;
                for (int x = -Radius; x <= Radius; x++)
                    for (int y = -Radius; y <= Radius; y++)
                        if (InDisk(x, y, r)) Add(grid, x, y, z);
            }
            return grid;
        }

        /// <summary>A flat-faced cylinder of length-to-diameter <paramref name="ratio"/>, axis along the flow.</summary>
        public static GridModel Cylinder(float ratio)
        {
            GridModel grid = Grid();
            int cells = Math.Max(1, (int)Math.Round(ratio * 2 * Radius));
            for (int z = 0; z < cells; z++)
                for (int x = -Radius; x <= Radius; x++)
                    for (int y = -Radius; y <= Radius; y++)
                        if (InDisk(x, y, Radius)) Add(grid, x, y, z);
            return grid;
        }

        /// <summary>
        /// The same cylinder with a tail tapering at 15° to half its radius behind it, so the frontal
        /// area is the cylinder's and the base is a quarter of it.
        /// </summary>
        public static GridModel BoatTailed(float ratio)
        {
            GridModel grid = Cylinder(ratio);
            double taper = Math.Tan(15d * Math.PI / 180d);
            int tail = (int)Math.Round((Radius / 2d) / taper);
            for (int k = 1; k <= tail; k++)
            {
                float r = Radius - (float)(k * taper);
                for (int x = -Radius; x <= Radius; x++)
                    for (int y = -Radius; y <= Radius; y++)
                        if (InDisk(x, y, r)) Add(grid, x, y, -k);
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

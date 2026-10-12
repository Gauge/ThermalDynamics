using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class OcclusionLadderLab
    {
        public const double PlanetRadius = 60000d;

        public const double Altitude = 5000d;

        public const double Speed = 100d;

        public class Rung
        {
            public double LengthMetres;

            public int Samples;

            public double SurplusSeconds;

            public double WorstError;

            public double PartialTests;
        }


        public static List<Rung> Sweep(double[] lengths, int[] samples,
            int occlusionInterval, float frequency)
        {

            List<Rung> rungs = new List<Rung>();

            double testSeconds = occlusionInterval / (double)frequency;

            foreach (double length in lengths)
            {
                double ignoredError;
                int ignoredPartial;


                double truth = Energy(length, SolarOcclusionSampler.MaxSamples, Tick, testSeconds,
                    0d, out ignoredError, out ignoredPartial);

                foreach (int count in samples)
                {
                    Rung rung = new Rung
                    {
                        LengthMetres = length,
                        Samples = count,
                    };

                    double total = 0d;

                    for (int p = 0; p < Phases; p++)
                    {
                        double phase = testSeconds * p / Phases;

                        double worst;
                        int partial;

                        total += Energy(length, count, testSeconds, testSeconds, phase,
                            out worst, out partial);

                        rung.WorstError += worst;
                        rung.PartialTests += partial;
                    }

                    rung.WorstError /= Phases;
                    rung.PartialTests /= Phases;

                    double reported = total / Phases;
                    rung.SurplusSeconds = reported - truth;
                    rungs.Add(rung);
                }
            }

            return rungs;
        }

        private const double Tick = 0.05d;

        private const int Phases = 24;


        private static double Energy(double length, int samples, double testSeconds,
            double spanSeconds, double phase, out double worstError, out int partialTests)
        {
            worstError = 0d;
            partialTests = 0;

            double radius = PlanetRadius + Altitude;
            double omega = Speed / radius;


            double terminator = Terminator(radius);

            double halfSpan = ((length / radius) * 0.5d) + (omega * spanSeconds * 6d);

            double energy = 0d;
            double reported = double.NaN;

            double sinceTest = double.PositiveInfinity;
            bool first = true;

            for (double angle = terminator - halfSpan; angle <= terminator + halfSpan; angle += omega * Tick)
            {
                if (first)
                {
                    first = false;

                    reported = 1d - OccludedShare(angle, length, samples);
                    sinceTest = phase;
                    if (reported > 0d && reported < 1d) partialTests++;
                }

                else if (sinceTest >= testSeconds)
                {

                    reported = 1d - OccludedShare(angle, length, samples);
                    sinceTest = 0d;
                    if (reported > 0d && reported < 1d) partialTests++;
                }


                double actual = 1d - OccludedShare(angle, length, SolarOcclusionSampler.MaxSamples);
                double error = Math.Abs(reported - actual);
                if (error > worstError) worstError = error;

                energy += reported * Tick;
                sinceTest += Tick;
            }

            return energy;
        }


        public static double Terminator(double radius)
        {
            double lo = 0d;
            double hi = Math.PI;

            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5d * (lo + hi);
                if (IsOccluded(mid, radius)) hi = mid; else lo = mid;
            }

            return 0.5d * (lo + hi);
        }


        private static bool IsOccluded(double angle, double radius)
        {

            Vector3D point = new Vector3D(Math.Cos(angle) * radius, 0d, Math.Sin(angle) * radius);
            return OcclusionMath.IsOccludedBySphere(point, Vector3D.Zero, PlanetRadius,

                new Vector3(1f, 0f, 0f));
        }


        private static double OccludedShare(double angle, double length, int samples)
        {
            double radius = PlanetRadius + Altitude;
            double halfAngle = (length / radius) * 0.5d;


            Vector3D lead = Point(angle - halfAngle, radius);

            Vector3D tail = Point(angle + halfAngle, radius);

            double girth = length * 0.05d;

            BoundingBoxD bounds = BoundingBoxD.CreateInvalid();
            bounds.Include(lead - new Vector3D(0d, girth, 0d));
            bounds.Include(lead + new Vector3D(0d, girth, 0d));
            bounds.Include(tail - new Vector3D(0d, girth, 0d));
            bounds.Include(tail + new Vector3D(0d, girth, 0d));


            List<Vector3D> points = new List<Vector3D>();
            SolarOcclusionSampler.Points(bounds, samples, points);


            Vector3 sun = new Vector3(1f, 0f, 0f);

            int occluded = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (OcclusionMath.IsOccludedBySphere(points[i], Vector3D.Zero, PlanetRadius, sun))
                {
                    occluded++;
                }
            }

            return points.Count == 0 ? 0d : occluded / (double)points.Count;
        }


        private static Vector3D Point(double angle, double radius)
        {
            return new Vector3D(Math.Cos(angle) * radius, 0d, Math.Sin(angle) * radius);
        }

        public static readonly double[] Lengths = { 25d, 75d, 150d, 300d, 600d, 1200d, 2500d };

        public static readonly int[] Samples = { 1, 3, 5, 9 };

        public static readonly int[] Intervals = { 1, 2, 4, 8, 12, 24, 48 };


        public static string IntervalReport(float frequency)
        {

            StringBuilder sb = new StringBuilder();

            const double Length = 600d;

            sb.AppendLine("  THE INTERVAL, WHICH IS THE DIAL THAT MOVES THE ENERGY");
            sb.AppendLine();
            sb.Append("  a ").Append(Length.ToString("n0"))
              .AppendLine(" m hull at nine samples, varying only how often it is tested");
            sb.AppendLine();
            sb.AppendLine("  interval    every        surplus sunlight   tests a crossing");

            foreach (int interval in Intervals)
            {

                List<Rung> one = Sweep(new[] { Length },
                    new[] { SolarOcclusionSampler.MaxSamples }, interval, frequency);

                double seconds = interval / (double)frequency;

                sb.Append("  ").Append((interval + " steps").PadRight(12))
                  .Append((seconds.ToString("n2") + " s").PadLeft(8)).Append("   ")
                  .Append(one[0].SurplusSeconds.ToString("n2").PadLeft(14)).Append(" s")
                  .Append(one[0].PartialTests.ToString("n2").PadLeft(19))
                  .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("  The surplus is the cadence's own lag: a test that ran before the crossing");
            sb.AppendLine("  keeps saying lit until the next one. Halving the interval halves it, which");
            sb.AppendLine("  no sample count does.");

            return sb.ToString();
        }

        public class Extremity
        {
            public double LengthMetres;

            public double SurplusSeconds;

            public double DeficitSeconds;

            public double SurplusKelvin;
            public double DeficitKelvin;
        }


        public static double KelvinPerLitSecond(ThermalSettings settings)
        {
            const double LargeGridCell = 2.5d;
            const double ArmourKilograms = 500d;

            BlockMaterial steel = BlockMaterials.Steel;

            double face = LargeGridCell * LargeGridCell;
            double capacity = ArmourKilograms * steel.SpecificHeat / settings.HeatTimeScale;
            return capacity <= 0d
                ? 0d
                : settings.SolarEnergy * face * steel.Emissivity / capacity;
        }


        public static List<Extremity> Extremities(double[] lengths, int samples,
            int occlusionInterval, float frequency, double kelvinPerSecond)
        {

            List<Extremity> found = new List<Extremity>();
            double testSeconds = occlusionInterval / (double)frequency;

            foreach (double length in lengths)
            {
                double surplus = 0d;
                double deficit = 0d;

                for (int p = 0; p < Phases; p++)
                {
                    double phase = testSeconds * p / Phases;

                    double high, low;
                    BlockError(length, samples, testSeconds, phase, out high, out low);
                    surplus += high;
                    deficit += low;
                }


                Extremity row = new Extremity();
                row.LengthMetres = length;
                row.SurplusSeconds = surplus / Phases;
                row.DeficitSeconds = deficit / Phases;
                row.SurplusKelvin = row.SurplusSeconds * kelvinPerSecond;
                row.DeficitKelvin = row.DeficitSeconds * kelvinPerSecond;
                found.Add(row);
            }

            return found;
        }


        private static void BlockError(double length, int samples, double testSeconds, double phase,
            out double worstSurplus, out double worstDeficit)
        {
            const int Positions = 9;

            double radius = PlanetRadius + Altitude;
            double omega = Speed / radius;

            double terminator = Terminator(radius);
            double halfSpan = ((length / radius) * 0.5d) + (omega * testSeconds * 6d);

            double[] told = new double[Positions];
            double[] met = new double[Positions];

            double reported = double.NaN;
            double sinceTest = double.PositiveInfinity;
            bool first = true;

            for (double angle = terminator - halfSpan; angle <= terminator + halfSpan; angle += omega * Tick)
            {
                if (first)
                {
                    first = false;

                    reported = 1d - OccludedShare(angle, length, samples);
                    sinceTest = phase;
                }

                else if (sinceTest >= testSeconds)
                {

                    reported = 1d - OccludedShare(angle, length, samples);
                    sinceTest = 0d;
                }

                for (int i = 0; i < Positions; i++)
                {
                    double offset = ((i / (double)(Positions - 1)) - 0.5d) * (length / radius);

                    double lit = IsOccluded(angle + offset, radius) ? 0d : 1d;

                    told[i] += reported * Tick;
                    met[i] += lit * Tick;
                }

                sinceTest += Tick;
            }

            worstSurplus = 0d;
            worstDeficit = 0d;

            for (int i = 0; i < Positions; i++)
            {
                double error = told[i] - met[i];
                if (error > worstSurplus) worstSurplus = error;
                if (error < worstDeficit) worstDeficit = error;
            }

            worstDeficit = -worstDeficit;
        }


        public static string ExtremityReport(int occlusionInterval, float frequency)
        {

            StringBuilder sb = new StringBuilder();


            ThermalSettings settings = new HistoricalThermalSettings();
            settings.Derive();


            double perSecond = KelvinPerLitSecond(settings);

            sb.AppendLine("  WHAT THE WHOLE-GRID ANSWER COSTS ONE BLOCK, WHICH IS THE TOP RUNG");
            sb.AppendLine();
            sb.Append("  nine samples at ").Append(occlusionInterval)
              .AppendLine(" steps: the shipped ladder, read per block rather than per hull");
            sb.Append("  one lit face of a 500 kg steel-plate block gains ")
              .Append(perSecond.ToString("n3")).AppendLine(" K a second of sunlight");
            sb.AppendLine();
            sb.AppendLine("  grid        told and did not get   told and was denied      worst block");


            List<Extremity> rows = Extremities(Lengths, SolarOcclusionSampler.MaxSamples,
                occlusionInterval, frequency, perSecond);

            foreach (Extremity row in rows)
            {
                double worst = Math.Max(row.SurplusKelvin, row.DeficitKelvin);

                sb.Append("  ").Append((row.LengthMetres.ToString("n0") + " m").PadRight(12))
                  .Append((row.SurplusSeconds.ToString("n2") + " s").PadLeft(14))
                  .Append((row.DeficitSeconds.ToString("n2") + " s").PadLeft(22))
                  .Append((worst.ToString("n2") + " K").PadLeft(17))
                  .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("  and the same hulls tested every step, which leaves only the spatial half");
            sb.AppendLine();
            sb.AppendLine("  grid        told and did not get   told and was denied      worst block");


            List<Extremity> tight = Extremities(Lengths, SolarOcclusionSampler.MaxSamples,
                1, frequency, perSecond);

            foreach (Extremity row in tight)
            {
                double worst = Math.Max(row.SurplusKelvin, row.DeficitKelvin);

                sb.Append("  ").Append((row.LengthMetres.ToString("n0") + " m").PadRight(12))
                  .Append((row.SurplusSeconds.ToString("n2") + " s").PadLeft(14))
                  .Append((row.DeficitSeconds.ToString("n2") + " s").PadLeft(22))
                  .Append((worst.ToString("n2") + " K").PadLeft(17))
                  .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("  The second table is what resolving the planet per face would remove and");
            sb.AppendLine("  the interval cannot: at one step it is all the error there is left, and it");
            sb.AppendLine("  is a property of the hull's length rather than of the cadence.");
            sb.AppendLine();
            sb.AppendLine("  The hull's own surplus cancels and a block's does not: the leading end is");
            sb.AppendLine("  told it is lit while it is dark and the trailing end the reverse, so the");
            sb.AppendLine("  error the ladder above reports as near zero is two errors of opposite sign");
            sb.AppendLine("  sitting at the two ends. Resolving the planet per face is what removes it.");

            return sb.ToString();
        }


        public static string Report(int occlusionInterval = 12, float frequency = 4f)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("SOLAR OCCLUSION LADDER");
            sb.AppendLine();
            sb.Append("  planet ").Append(PlanetRadius / 1000d).Append(" km, altitude ")
              .Append(Altitude / 1000d).Append(" km, speed ").Append(Speed).AppendLine(" m/s");
            sb.Append("  re-tested every ").Append(occlusionInterval).Append(" steps at Frequency ")
              .Append(frequency).Append(" = every ")
              .Append((occlusionInterval / frequency).ToString("n2")).AppendLine(" s of play");
            sb.Append("  which is ").Append((Speed * occlusionInterval / frequency).ToString("n0"))
              .AppendLine(" m of travel between one test and the next");
            sb.Append("  sunset lands at ")
              .Append((Terminator(PlanetRadius + Altitude) * 180d / Math.PI).ToString("n1"))
              .AppendLine(" degrees from the sub-solar point, not 90: OcclusionThreshold is a fitted curve");
            sb.AppendLine();


            List<Rung> rungs = Sweep(Lengths, Samples, occlusionInterval, frequency);

            sb.AppendLine("  grid        samples   surplus sunlight   worst error   partial tests");
            foreach (Rung rung in rungs)
            {
                sb.Append("  ").Append((rung.LengthMetres.ToString("n0") + " m").PadRight(12))
                  .Append(rung.Samples.ToString().PadLeft(4)).Append("   ")
                  .Append(rung.SurplusSeconds.ToString("n2").PadLeft(14)).Append(" s")
                  .Append(rung.WorstError.ToString("n3").PadLeft(14))
                  .Append(rung.PartialTests.ToString("n2").PadLeft(16))
                  .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("  Surplus sunlight barely moves with the sample count, and the reason is that");
            sb.AppendLine("  one ray from the centre is unbiased: it reports light after the leading end");
            sb.AppendLine("  is dark and dark before the trailing end is, and the two cancel. What the");
            sb.AppendLine("  extra samples fix is the worst error column, which is what a player sees at");
            sb.AppendLine("  a moment rather than what the hull absorbs over a crossing.");
            sb.AppendLine();

            sb.AppendLine(IntervalReport(frequency));
            sb.AppendLine(ExtremityReport(occlusionInterval, frequency));

            return sb.ToString();
        }
    }
}

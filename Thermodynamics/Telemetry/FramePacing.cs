using System;
using System.Collections.Generic;
using System.Text;

namespace Thermodynamics
{
    /// <summary>
    /// The game's frame gaps, measured between draw calls, with this mod's own time inside each one.
    /// The measured share covers simulation and draw callbacks, not every mod callback or
    /// deferred engine cost. It helps locate hitches but does not establish their cause.
    /// The spacing between hitches is kept too, because a steady hitch has a period.
    /// </summary>
    public class FramePacing
    {
        public const double HitchMilliseconds = 50d;

        public const int WorstKept = 12;

        private const int SpacingKept = 4096;

        public struct Hitch
        {
            public double AtSeconds;
            public double GapMilliseconds;
            public double SimulationMilliseconds;
            public double DrawMilliseconds;

            public double OursMilliseconds
            {
                get { return SimulationMilliseconds + DrawMilliseconds; }
            }
        }

        public long Frames;
        public long Hitches;
        public double TotalGapMilliseconds;
        public double HitchGapMilliseconds;
        public double HitchOursMilliseconds;

        public readonly List<Hitch> Worst = new List<Hitch>(WorstKept + 1);

        private readonly Histogram gaps = new Histogram(
            new float[] { 8, 12, 17, 20, 25, 33.4f, 50, 100, 250, 1000 });

        private readonly List<double> hitchTimes = new List<double>();

        private double simulationSinceDraw;


        public void AddSimulation(double milliseconds)
        {
            simulationSinceDraw += milliseconds;
        }


        /// <summary>One frame: the gap since the previous draw began, and the draw that opened it.</summary>
        public void Record(double atSeconds, double gapMilliseconds, double previousDrawMilliseconds)
        {
            double simulation = simulationSinceDraw;
            simulationSinceDraw = 0d;

            if (!(gapMilliseconds >= 0d) || double.IsInfinity(gapMilliseconds)) return;

            Frames++;
            TotalGapMilliseconds += gapMilliseconds;
            gaps.Add((float)gapMilliseconds);

            if (gapMilliseconds < HitchMilliseconds) return;

            Hitch hitch = new Hitch
            {
                AtSeconds = atSeconds,
                GapMilliseconds = gapMilliseconds,
                SimulationMilliseconds = simulation,
                DrawMilliseconds = previousDrawMilliseconds,
            };

            Hitches++;
            HitchGapMilliseconds += gapMilliseconds;
            HitchOursMilliseconds += hitch.OursMilliseconds;
            if (hitchTimes.Count < SpacingKept) hitchTimes.Add(atSeconds);

            int at = Worst.Count;
            while (at > 0 && Worst[at - 1].GapMilliseconds < gapMilliseconds) at--;
            if (at >= WorstKept) return;
            Worst.Insert(at, hitch);
            if (Worst.Count > WorstKept) Worst.RemoveAt(WorstKept);
        }


        /// <summary>Median seconds between consecutive hitches, or -1 with fewer than three.</summary>
        public double MedianSpacingSeconds()
        {
            if (hitchTimes.Count < 3) return -1d;

            double[] spacing = new double[hitchTimes.Count - 1];
            for (int i = 1; i < hitchTimes.Count; i++) spacing[i - 1] = hitchTimes[i] - hitchTimes[i - 1];
            Array.Sort(spacing);

            int middle = spacing.Length / 2;
            return spacing.Length % 2 == 1
                ? spacing[middle]
                : (spacing[middle - 1] + spacing[middle]) * 0.5d;
        }


        public void Write(StringBuilder sb)
        {
            sb.Append("  Gaps between the game's draw calls. \"ours\" is this mod's simulation and draw\n");
            sb.Append("  callback time inside the gap; remaining time is unattributed.\n\n");

            if (Frames == 0)
            {
                sb.Append("  no frames drawn\n");
                return;
            }

            sb.Append("  frames drawn                  ").Append(Frames.ToString("n0")).Append('\n');
            sb.Append("  mean gap                      ")
              .Append((TotalGapMilliseconds / Frames).ToString("n2")).Append(" ms\n");
            sb.Append("  hitches (gap >= ").Append(HitchMilliseconds.ToString("0")).Append(" ms)        ")
              .Append(Hitches.ToString("n0")).Append('\n');

            if (Hitches > 0)
            {
                sb.Append("  ours inside hitches           ")
                  .Append(HitchOursMilliseconds.ToString("n1")).Append(" of ")
                  .Append(HitchGapMilliseconds.ToString("n1")).Append(" ms (")
                  .Append((100d * HitchOursMilliseconds / HitchGapMilliseconds).ToString("n1")).Append(" %)\n");

                double spacing = MedianSpacingSeconds();
                sb.Append("  median spacing                ")
                  .Append(spacing < 0d ? "-" : spacing.ToString("n2") + " s").Append('\n');
            }

            sb.Append("\n  gap distribution:\n");
            gaps.Write(sb, "    ", "ms");

            if (Worst.Count == 0) return;

            sb.Append("\n  worst gaps:              at s      gap ms     ours ms   (sim / draw)\n");
            for (int i = 0; i < Worst.Count; i++)
            {
                Hitch h = Worst[i];
                sb.Append("    ")
                  .Append(h.AtSeconds.ToString("n1").PadLeft(24))
                  .Append(h.GapMilliseconds.ToString("n1").PadLeft(12))
                  .Append(h.OursMilliseconds.ToString("n2").PadLeft(12))
                  .Append("   (").Append(h.SimulationMilliseconds.ToString("n2"))
                  .Append(" / ").Append(h.DrawMilliseconds.ToString("n2")).Append(")\n");
            }
        }
    }
}

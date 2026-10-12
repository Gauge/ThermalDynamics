using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class CoolantBenchmarks
    {
        public class Row
        {
            public int Rings;
            public int Pipes;
            public int Loops;
            public int Blocks;
            public int Links;

            public double SegmentedMsPerStep;
            public double MixedMsPerStep;

            public float SegmentedSubsteps;
            public float MixedSubsteps;

            public float Mixing;

            public double Ratio
            {
                get { return MixedMsPerStep <= 0d ? 0d : SegmentedMsPerStep / MixedMsPerStep; }
            }
        }


        public static Row Measure(int hullSize, int rings, int steps)
        {

            return Measure(hullSize, rings, steps, 0f);
        }


        public static Row Measure(int hullSize, int rings, int steps, float flowOverride)
        {

            Row row = new Row();
            row.Rings = rings;


            ThermalSimulation segmented = Build(hullSize, rings, false, flowOverride);

            ThermalSimulation mixed = Build(hullSize, rings, true, flowOverride);

            row.Blocks = segmented.Solver.Nodes.Count;
            row.Links = segmented.Solver.LinkCount;
            row.Loops = segmented.Solver.Loops.Count;

            int pipes = 0;
            for (int i = 0; i < segmented.Solver.Loops.Count; i++)
            {
                pipes += segmented.Solver.Loops[i].PipeCount;
            }
            row.Pipes = pipes;

            if (segmented.Solver.Loops.Count > 0)
            {
                CoolantLoop first = segmented.Solver.Loops[0];
                row.Mixing = first.MixingFraction(
                    segmented.Settings.StepSeconds / Math.Max(1, segmented.Solver.LastSubsteps));
            }

            row.SegmentedMsPerStep = double.MaxValue;
            row.MixedMsPerStep = double.MaxValue;

            for (int pass = 0; pass < Repeats; pass++)
            {
                float substeps;


                double a = TimeSteps(segmented, steps, out substeps);
                if (a < row.SegmentedMsPerStep) row.SegmentedMsPerStep = a;
                row.SegmentedSubsteps = substeps;


                double b = TimeSteps(mixed, steps, out substeps);
                if (b < row.MixedMsPerStep) row.MixedMsPerStep = b;
                row.MixedSubsteps = substeps;
            }

            return row;
        }

        public const int Repeats = 5;


        private static ThermalSimulation Build(int hullSize, int rings, bool wellMixed, float flowOverride)
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            settings.WellMixedCoolant = wellMixed;

            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            HashSet<Vector3I> cells = LoadShapes.Build("ship", hullSize);
            builder.PlaceCensus(cells);

            Vector3I max = Vector3I.Zero;
            foreach (Vector3I cell in cells)
            {
                if (cell.X > max.X) max.X = cell.X;
                if (cell.Y > max.Y) max.Y = cell.Y;
                if (cell.Z > max.Z) max.Z = cell.Z;
            }

            for (int r = 0; r < rings; r++)
            {

                Vector3I origin = new Vector3I(max.X + 3, max.Y - (r * 3), max.Z + 3);
                List<Vector3I> ring = PipeFitter.RectangleXZ(origin, 8, 6);

                Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
                for (int i = 2; i < ring.Count; i += 3) sinks[i] = Vector3I.Down;

                try
                {
                    PipeFitter.BuildRing(builder, ring, -1, sinks);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (KeyValuePair<int, Vector3I> sink in sinks)
                {
                    Vector3I at = ring[sink.Key] + Vector3I.Down;
                    if (builder.Grid.GetAtCell(at) != null) continue;
                    builder.Place(Catalog.HeavyArmor(), at);
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Temperature = 250f + ((i * 37) % 500);
            }
            for (int i = 0; i < simulation.Solver.Loops.Count; i++)
            {
                CoolantLoop loop = simulation.Solver.Loops[i];
                loop.Temperature = 300f + (i * 11);

                if (flowOverride <= 0f) continue;
                loop.Properties.LargeGridFlowRate = flowOverride * loop.ParcelLengthMetres;
                loop.Properties.SmallGridFlowRate = loop.Properties.LargeGridFlowRate;
                loop.RefreshFlow();
            }

            return simulation;
        }


        private static double TimeSteps(ThermalSimulation simulation, int steps, out float substeps)
        {
            EnvironmentSample sample = Worlds.Shadow();

            simulation.StepExact(4, sample);

            Stopwatch clock = Stopwatch.StartNew();
            simulation.StepExact(steps, sample);
            clock.Stop();

            substeps = simulation.Solver.LastSubsteps;
            return clock.Elapsed.TotalMilliseconds / steps;
        }


        public static string Table(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("  rings  pipes  loops   blocks    links   segmented   well-mixed   ratio  substeps   mixing\n");
            sb.Append("  -----  -----  -----   ------    -----   ---------   ----------   -----  --------   ------\n");

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                sb.Append("  ").Append(row.Rings.ToString().PadLeft(5))
                  .Append("  ").Append(row.Pipes.ToString().PadLeft(5))
                  .Append("  ").Append(row.Loops.ToString().PadLeft(5))
                  .Append("   ").Append(row.Blocks.ToString("n0").PadLeft(6))
                  .Append("   ").Append(row.Links.ToString("n0").PadLeft(6))
                  .Append("   ").Append(row.SegmentedMsPerStep.ToString("n4").PadLeft(9))
                  .Append("   ").Append(row.MixedMsPerStep.ToString("n4").PadLeft(10))
                  .Append("   ").Append(row.Ratio.ToString("n2").PadLeft(5))
                  .Append("  ").Append((row.SegmentedSubsteps + " / " + row.MixedSubsteps).PadLeft(8))
                  .Append("   ").Append(row.Mixing.ToString("n2").PadLeft(6))
                  .Append('\n');
            }

            return sb.ToString();
        }


        public static string Csv(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("rings,pipes,loops,blocks,links,segmented_ms,mixed_ms,ratio,segmented_substeps,mixed_substeps\n");

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                sb.Append(row.Rings).Append(',').Append(row.Pipes).Append(',').Append(row.Loops)
                  .Append(',').Append(row.Blocks).Append(',').Append(row.Links)
                  .Append(',').Append(row.SegmentedMsPerStep.ToString("n6"))
                  .Append(',').Append(row.MixedMsPerStep.ToString("n6"))
                  .Append(',').Append(row.Ratio.ToString("n4"))
                  .Append(',').Append(row.SegmentedSubsteps)
                  .Append(',').Append(row.MixedSubsteps)
                  .Append('\n');
            }

            return sb.ToString();
        }
    }
}

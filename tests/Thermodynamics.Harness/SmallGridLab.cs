using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class SmallGridLab
    {
        public static readonly int[] DefaultSizes = { 1, 3, 8, 32, 128, 512, 2048 };

        public class Row
        {
            public int Grids;
            public int BlocksPerGrid;
            public int Blocks;

            public double FleetStepMs;

            public double PacedStepMs;

            public double AdvancesPerStep;

            public double VisitMs;

            public double VisitShare
            {
                get
                {
                    double overhead = PacedStepMs - FleetStepMs;
                    return overhead <= 0d ? 0d : VisitMs / overhead;
                }
            }

            public double PacingOverhead
            {
                get { return FleetStepMs <= 0d ? 0d : (PacedStepMs - FleetStepMs) / FleetStepMs; }
            }

            public double MicrosecondsPerGridStep
            {
                get { return Grids <= 0 ? 0d : FleetStepMs * 1000d / Grids; }
            }

            public double NanosecondsPerBlockStep
            {
                get { return Blocks <= 0 ? 0d : FleetStepMs * 1000000d / Blocks; }
            }

            public double Substeps;

            public double PacedSubsteps;

            public double PacedSteps;
            public int RequestedSteps;
        }


        public static List<Row> Run(int grids, IList<int> sizes, int steps, Action<string> log)
        {

            List<Row> rows = new List<Row>();

            for (int i = 0; i < sizes.Count; i++)
            {
                if (log != null) log(grids + " grids of " + sizes[i]);
                rows.Add(Measure(grids, sizes[i], steps));
            }

            return rows;
        }


        private static Row Measure(int grids, int blocksPerGrid, int steps)
        {

            Row row = new Row();
            row.Grids = grids;
            row.BlocksPerGrid = blocksPerGrid;


            List<ThermalSimulation> fleet = new List<ThermalSimulation>(grids);
            for (int i = 0; i < grids; i++)
            {

                ThermalSimulation simulation = Build(blocksPerGrid);
                fleet.Add(simulation);
                row.Blocks += simulation.Solver.Nodes.Count;
            }

            EnvironmentSample sample = Worlds.PlanetSurface(1f, 0.5f);

            float frame = 1f / 60f;
            int framesPerStep = (int)Math.Round(60f / fleet[0].Settings.StepsPerSecond);
            int frames = steps * framesPerStep;

            for (int i = 0; i < fleet.Count; i++) fleet[i].StepExact(3, sample);
            for (int i = 0; i < fleet.Count; i++) fleet[i].Update(frame, sample);

            double best = double.MaxValue;
            double pacedBest = double.MaxValue;
            double visitBest = double.MaxValue;

            Stopwatch watch = new Stopwatch();

            for (int repeat = 0; repeat < 4; repeat++)
            {
                bool wholeFirst = (repeat % 2) == 0;

                if (wholeFirst) TimeWhole(fleet, sample, steps, watch, row, ref best);
                TimePaced(fleet, sample, frame, frames, steps, watch, row, ref pacedBest);
                if (!wholeFirst) TimeWhole(fleet, sample, steps, watch, row, ref best);

                TimeVisits(fleet, sample, frames, steps, watch, ref visitBest);
            }

            row.FleetStepMs = best;
            row.PacedStepMs = pacedBest;
            row.VisitMs = visitBest;
            return row;
        }



        private static void TimeWhole(List<ThermalSimulation> fleet, EnvironmentSample sample,
            int steps, Stopwatch watch, Row row, ref double best)
        {
            Seed(fleet);

            watch.Restart();
            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < fleet.Count; i++) fleet[i].StepExact(1, sample);
            }
            watch.Stop();

            double whole = watch.Elapsed.TotalMilliseconds / steps;
            if (whole >= best) return;

            best = whole;
            row.Substeps = fleet[0].Solver.LastSubsteps;
        }


        private static void TimePaced(List<ThermalSimulation> fleet, EnvironmentSample sample,
            float frame, int frames, int steps, Stopwatch watch, Row row, ref double best)
        {
            Seed(fleet);
            for (int i = 0; i < fleet.Count; i++) fleet[i].Work.Reset();

            watch.Restart();
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < fleet.Count; i++) fleet[i].Update(frame, sample);
            }
            watch.Stop();

            long advances = 0;
            long completed = 0;
            for (int i = 0; i < fleet.Count; i++)
            {
                advances += fleet[i].Work.StepAdvances;
                completed += fleet[i].Work.SolverSteps;
            }

            double perGridSteps = completed <= 0 ? steps : completed / (double)fleet.Count;
            double paced = watch.Elapsed.TotalMilliseconds / perGridSteps;
            if (paced >= best) return;

            best = paced;
            row.PacedSteps = perGridSteps;
            row.RequestedSteps = steps;
            row.PacedSubsteps = fleet[0].Solver.LastSubsteps;
            row.AdvancesPerStep = completed <= 0 ? 0d : advances / (double)completed;
        }


        private static void TimeVisits(List<ThermalSimulation> fleet, EnvironmentSample sample,
            int frames, int steps, Stopwatch watch, ref double best)
        {
            watch.Restart();
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < fleet.Count; i++) fleet[i].Update(0f, sample);
            }
            watch.Stop();

            double visits = watch.Elapsed.TotalMilliseconds / steps;
            if (visits < best) best = visits;
        }


        private static void Seed(List<ThermalSimulation> fleet)
        {
            for (int i = 0; i < fleet.Count; i++) LoadBenchmarks.SeedSpread(fleet[i]);
        }


        private static ThermalSimulation Build(int blocks)
        {
            if (blocks < 1) blocks = 1;

            int side = 1;
            while (side * side * side < blocks) side++;


            List<Vector3I> cells = new List<Vector3I>(blocks);
            for (int x = 0; x < side && cells.Count < blocks; x++)
            {
                for (int y = 0; y < side && cells.Count < blocks; y++)
                {
                    for (int z = 0; z < side && cells.Count < blocks; z++)
                    {
                        cells.Add(new Vector3I(x, y, z));
                    }
                }
            }

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(cells);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings());
            simulation.RebuildAll();

            while (simulation.HasPendingWork)
            {
                simulation.Update(1f / 60f, Worlds.PlanetSurface(1f, 0.5f));
            }

            return simulation;
        }


        public static string Table(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("  grids  blocks/grid    blocks  subs w/p    whole ms"
                + "    paced ms   pacing   advances   steps p/w   us/grid/step"
                + "    visit ms   visit share");

            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                sb.Append(r.Grids.ToString("n0").PadLeft(7));
                sb.Append(r.BlocksPerGrid.ToString("n0").PadLeft(13));
                sb.Append(r.Blocks.ToString("n0").PadLeft(10));
                sb.Append((r.Substeps.ToString("n0") + "/" + r.PacedSubsteps.ToString("n0")).PadLeft(10));
                sb.Append(r.FleetStepMs.ToString("n3").PadLeft(12));
                sb.Append(r.PacedStepMs.ToString("n3").PadLeft(12));
                sb.Append((r.PacingOverhead * 100d).ToString("n0").PadLeft(8) + "%");
                sb.Append(r.AdvancesPerStep.ToString("n1").PadLeft(11));
                sb.Append((r.PacedSteps.ToString("n1") + "/" + r.RequestedSteps).PadLeft(12));
                sb.Append(r.MicrosecondsPerGridStep.ToString("n3").PadLeft(15));
                sb.Append(r.VisitMs.ToString("n4").PadLeft(12));
                sb.Append((r.VisitShare * 100d).ToString("n0").PadLeft(13) + "%");
                sb.AppendLine();
            }

            return sb.ToString();
        }


        public static string Csv(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("grids,blocks_per_grid,blocks,substeps,fleet_step_ms,paced_step_ms,"
                + "pacing_overhead,advances_per_step,us_per_grid_step,ns_per_block_step,"
                + "visit_ms,visit_share");

            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                sb.Append(r.Grids).Append(',');
                sb.Append(r.BlocksPerGrid).Append(',');
                sb.Append(r.Blocks).Append(',');
                sb.Append(r.Substeps.ToString("r")).Append(',');
                sb.Append(r.FleetStepMs.ToString("r")).Append(',');
                sb.Append(r.PacedStepMs.ToString("r")).Append(',');
                sb.Append(r.PacingOverhead.ToString("r")).Append(',');
                sb.Append(r.AdvancesPerStep.ToString("r")).Append(',');
                sb.Append(r.MicrosecondsPerGridStep.ToString("r")).Append(',');
                sb.Append(r.NanosecondsPerBlockStep.ToString("r")).Append(',');
                sb.Append(r.VisitMs.ToString("r")).Append(',');
                sb.Append(r.VisitShare.ToString("r"));
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class StaggerLab
    {
        public const int Repeats = 5;

        public const int GridStepsPerRepeat = 512;


        public static int RoundsFor(int grids)
        {
            if (grids <= 0) return 1;
            int rounds = GridStepsPerRepeat / grids;
            return rounds < 1 ? 1 : rounds;
        }

        public class Row
        {
            public int Grids;
            public int NodesEach;

            public int Slices;

            public int Rounds;

            public double StaggeredMs;
            public double SpreadMs;

            public double StaggeredSpread;
            public double SpreadSpread;

            public double Penalty
            {
                get { return StaggeredMs <= 0d ? 0d : SpreadMs / StaggeredMs; }
            }

            public double LumpMs
            {
                get { return Grids <= 0 ? 0d : StaggeredMs / Grids; }
            }

            public double LumpWork;
        }


        public static List<Row> Run(IList<int> fleetSizes, int nodesEach, int slices,
            Action<string> log = null)
        {

            List<Row> rows = new List<Row>();


            ThermalSettings settings = new HistoricalThermalSettings();
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            for (int f = 0; f < fleetSizes.Count; f++)
            {
                int grids = fleetSizes[f];
                if (log != null) log(grids + " grids of " + nodesEach);

                List<ThermalSimulation> fleet = FleetParallelLab.Fleet(grids, nodesEach, settings);
                EnvironmentState state =
                    EnvironmentSolver.Solve(settings, fleet[0].Planet, Worlds.Shadow());


                Row row = new Row();
                row.Grids = grids;
                row.NodesEach = fleet[0].Solver.Nodes.Count;
                row.Slices = slices;


                int rounds = RoundsFor(grids);
                row.Rounds = rounds;

                Staggered(fleet, settings, state, 1);
                long work = 0;
                for (int i = 0; i < fleet.Count; i++) work += fleet[i].Solver.StepWorkUnits;
                row.LumpWork = work / (double)grids;

                Staggered(fleet, settings, state, 2);
                Spread(fleet, settings, state, 2, slices);

                double fastest, slowest;
                Time(delegate { Staggered(fleet, settings, state, rounds); },
                    out fastest, out slowest);
                row.StaggeredMs = fastest / rounds;
                row.StaggeredSpread = fastest <= 0d ? 0d : slowest / fastest;

                Time(delegate { Spread(fleet, settings, state, rounds, slices); },
                    out fastest, out slowest);
                row.SpreadMs = fastest / rounds;
                row.SpreadSpread = fastest <= 0d ? 0d : slowest / fastest;

                rows.Add(row);
            }

            return rows;
        }


        public static void Staggered(IList<ThermalSimulation> fleet, ThermalSettings settings,
            EnvironmentState state, int rounds)
        {
            for (int r = 0; r < rounds; r++)
            {
                for (int i = 0; i < fleet.Count; i++)
                {
                    fleet[i].Solver.Step(settings.StepSeconds, state);
                }
            }
        }


        public static void Spread(IList<ThermalSimulation> fleet, ThermalSettings settings,
            EnvironmentState state, int rounds, int slices)
        {
            for (int r = 0; r < rounds; r++)
            {
                for (int i = 0; i < fleet.Count; i++)
                {
                    fleet[i].Solver.BeginStep(settings.StepSeconds, state);
                }

                for (int s = 0; s < slices; s++)
                {
                    for (int i = 0; i < fleet.Count; i++)
                    {
                        ThermalSolver solver = fleet[i].Solver;
                        long budget = (solver.StepWorkUnits / slices) + 1;
                        solver.AdvanceStep(budget);
                    }
                }

                for (int i = 0; i < fleet.Count; i++)
                {
                    while (!fleet[i].Solver.AdvanceStep(long.MaxValue)) { }
                }
            }
        }


        private static void Time(Action action, out double fastest, out double slowest)
        {
            LabTiming.FastestOf(Repeats, action, out fastest, out slowest);
        }


        public static string Table(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("grids".PadLeft(7))
              .Append("nodes ea".PadLeft(10))
              .Append("slices".PadLeft(8))
              .Append("rounds".PadLeft(8))
              .Append("staggered".PadLeft(11))
              .Append("spread".PadLeft(10))
              .Append("spreading costs".PadLeft(17))
              .Append("lump".PadLeft(9))
              .Append("lump work".PadLeft(12))
              .Append("noise".PadLeft(14))
              .Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                sb.Append(row.Grids.ToString("n0").PadLeft(7))
                  .Append(row.NodesEach.ToString("n0").PadLeft(10))
                  .Append(row.Slices.ToString("n0").PadLeft(8))
                  .Append(row.Rounds.ToString("n0").PadLeft(8))
                  .Append((row.StaggeredMs.ToString("n3") + " ms").PadLeft(11))
                  .Append((row.SpreadMs.ToString("n3") + " ms").PadLeft(10))
                  .Append(((row.Penalty - 1d) * 100d).ToString("n1").PadLeft(16))
                  .Append("%")
                  .Append((row.LumpMs.ToString("n3") + " ms").PadLeft(9))
                  .Append(row.LumpWork.ToString("n0").PadLeft(12))
                  .Append((row.StaggeredSpread.ToString("n2") + "/"
                      + row.SpreadSpread.ToString("n2") + "x").PadLeft(14))
                  .Append('\n');
            }

            return sb.ToString();
        }
    }
}

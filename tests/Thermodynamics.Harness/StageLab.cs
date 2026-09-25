using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public static class StageLab
    {
        public static Action<string> TraceRepeat;

        public static int Repeats = 100;

        public static int ConfirmingRepeats = 5;

        public static double ConfirmingBand = 1.02;

        public static int MaxRepeats = 400;

        public const int SolverStepsPerRepeat = 20;

        public class Row
        {
            public string Stage;
            public int Blocks;

            public long AllocatedBytes;

            public int Repeats;
            public int RepeatsSinceBest;
            public int ConfirmedBest;

            public string Stop = Fixed;


            public readonly List<double> Samples = new List<double>();

            public double BestMs;
            public double WorstMs;

            public long Work;
            public string WorkUnit;

            public double SpreadPercent
            {
                get { return BestMs <= 0d ? 0d : 100d * (WorstMs - BestMs) / BestMs; }
            }

            public string Note = "";

            public const string Confirmed = "confirmed";
            public const string Capped = "capped";
            public const string Fixed = "fixed";

            public double MedianMs;

            public double FastModeShare
            {
                get { return MedianMs <= 0d ? 0d : BestMs / MedianMs; }
            }

            public double NsPerWork
            {
                get { return Work <= 0 ? 0d : BestMs * 1e6d / Work; }
            }
        }

        public static readonly string[] Stages = { "place", "register", "surfaces", "links", "rooms", "exposure", "roomair", "solver" };

        public static readonly string[] ExtraStages = { "syncdirty", "syncclean", "shapenormals" };


        public static List<Row> Run(string shape, int blocks, IList<string> stages, Action<string> log = null)
        {

            List<Row> rows = new List<Row>();

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build(shape, blocks));

            for (int i = 0; i < stages.Count; i++)
            {
                if (log != null) log(stages[i] + ", " + blocks.ToString("n0") + " blocks");

                Settle();

                Row row = Measure(stages[i], builder);
                Summarise(row);
                rows.Add(row);
            }

            return rows;
        }


        internal static ThermalSimulation Registered(GridBuilder builder)
        {

            ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings().Derive(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            return simulation;
        }


        internal static Row Measure(string stage, GridBuilder builder)
        {
            switch (stage)
            {

                case "place": return Place(builder);

                case "register": return Register(builder);

                case "surfaces": return Surfaces(builder);

                case "links": return Links(builder);

                case "rooms": return Rooms(builder);

                case "exposure": return Exposure(builder);

                case "shapenormals": return ShapeNormals(builder);

                case "syncdirty": return NodeSync(builder, true);

                case "syncclean": return NodeSync(builder, false);

                case "roomair": return RoomAir(builder);

                case "solver": return Solver(builder);

                default: throw new ArgumentException("Unknown stage: " + stage);
            }
        }


        private static Row NewRow(string stage, ThermalSimulation simulation, string unit)
        {

            Row row = new Row();
            row.Stage = stage;
            row.Blocks = simulation.Solver.Nodes.Count;
            row.BestMs = double.MaxValue;
            row.WorkUnit = unit;
            return row;
        }


        internal static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }


        internal static long Allocated()
        {
            return GC.GetAllocatedBytesForCurrentThread();
        }


        internal static void Take(Row row, double ms)
        {
            if (TraceRepeat != null) TraceRepeat(row.Stage + " " + ms.ToString("n3", CultureInfo.InvariantCulture));
            row.Samples.Add(ms);

            if (ms < row.BestMs)
            {
                row.BestMs = ms;
                row.RepeatsSinceBest = 0;
                row.ConfirmedBest = 1;
            }
            else
            {
                row.RepeatsSinceBest++;
                if (ms <= row.BestMs * ConfirmingBand) row.ConfirmedBest++;
            }

            row.Repeats++;
            if (ms > row.WorstMs) row.WorstMs = ms;
        }


        internal static bool Settled(Row row)
        {
            if (row == null) return false;
            if (row.Repeats < Repeats) return false;

            if (row.ConfirmedBest >= ConfirmingRepeats)
            {
                row.Stop = Row.Confirmed;
                return true;
            }

            if (row.Repeats >= MaxRepeats)
            {
                row.Stop = Row.Capped;
                return true;
            }

            return false;
        }


        internal static void Summarise(Row row)
        {
            if (row == null || row.Samples.Count == 0) return;

            double[] sorted = row.Samples.ToArray();
            Array.Sort(sorted);
            row.MedianMs = sorted[sorted.Length / 2];
        }


        internal static void Work(Row row, long work, int repeat)
        {
            if (repeat == 0) { row.Work = work; return; }
            if (row.Work != work)
            {
                throw new InvalidOperationException(row.Stage + " did " + work + " " + row.WorkUnit
                    + " on repeat " + repeat + " and " + row.Work + " on the first, so its readings are of different walks");
            }
        }


        private static Row Place(GridBuilder builder)
        {
            IList<BlockInstance> dealt = builder.Placed;
            Row row = null;

            for (int r = 0; !Settled(row); r++)
            {

                GridModel grid = new GridModel(builder.Grid.GridSize);


                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < dealt.Count; i++)
                {
                    grid.Add(new BlockInstance(dealt[i].Model, dealt[i].Min, dealt[i].Orientation));
                }
                watch.Stop();

                if (row == null)
                {

                    row = new Row();
                    row.Stage = "place";
                    row.Blocks = grid.BlockCount;
                    row.BestMs = double.MaxValue;
                    row.WorkUnit = "blocks";
                }
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, grid.BlockCount, r);
            }

            return row;
        }


        private static Row Register(GridBuilder builder)
        {
            Row row = null;

            for (int r = 0; !Settled(row); r++)
            {

                ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings().Derive(), builder.Grid);


                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < builder.Placed.Count; i++)
                {
                    simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
                }
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;

                if (row == null) row = NewRow("register", simulation, "blocks");
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Solver.Nodes.Count, r);
            }

            return row;
        }


        private static Row Surfaces(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);

            Row row = NewRow("surfaces", simulation, "cells");
            for (int r = 0; !Settled(row); r++)
            {

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Surfaces.Rebuild(simulation.Grid);
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Surfaces.CellCount, r);
            }

            return row;
        }


        private static Row Links(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);
            simulation.Surfaces.Rebuild(simulation.Grid);

            Row row = NewRow("links", simulation, "links");
            for (int r = 0; !Settled(row); r++)
            {

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Solver.RebuildLinks();
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Solver.LinkCount, r);
            }

            return row;
        }


        private static Row Rooms(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);
            simulation.Surfaces.Rebuild(simulation.Grid);

            Row row = NewRow("rooms", simulation, "cells visited");

            const int WarmRepeat = 3;
            for (int r = 0; r <= WarmRepeat || !Settled(row); r++)
            {
                long before = simulation.Work.RoomCellsVisited;

                long allocated = r == WarmRepeat ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Rooms.RequestRestart(simulation.Grid);
                if (!simulation.Rooms.RunToCompletion())
                {
                    throw new InvalidOperationException("the room pass did not finish, so there is no stage to time");
                }
                watch.Stop();
                if (r == WarmRepeat) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Work.RoomCellsVisited - before, r);
            }

            return row;
        }


        private static Row RoomAir(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);
            simulation.Surfaces.Rebuild(simulation.Grid);
            simulation.Rooms.RequestRestart(simulation.Grid);
            simulation.Rooms.RunToCompletion();

            RoomMap map = simulation.Rooms.Map;
            simulation.Solver.RebuildRoomAir(map);

            int filled = 0;
            for (int r = 0; r < map.RoomCount; r++)
            {
                if (map.CellsInRoom(r) == 0) continue;
                if (simulation.Solver.SetRoomPressure(map, map.CellsOf(r)[0], 1f)) filled++;
            }

            if (filled == 0)
            {
                throw new InvalidOperationException(
                    "no room took air on a hull with " + map.RoomCount
                    + " rooms, so this stage would time an outer loop and nothing else");
            }


            Row row = NewRow("roomair", simulation, "face probes");
            for (int r = 0; !Settled(row); r++)
            {
                long before = simulation.Work.RoomAirFaceProbes;
                long hitsBefore = simulation.Work.RoomAirFaceHits;

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Solver.RebuildRoomAir(map);
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Work.RoomAirFaceProbes - before, r);

                if (r == 0) row.Note = (simulation.Work.RoomAirFaceHits - hitsBefore).ToString("n0") + " hit";
            }

            return row;
        }


        private static Row Exposure(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);
            simulation.Surfaces.Rebuild(simulation.Grid);
            simulation.Rooms.RequestRestart(simulation.Grid);
            simulation.Rooms.RunToCompletion();

            Row row = NewRow("exposure", simulation, "nodes");
            for (int r = 0; !Settled(row); r++)
            {
                long before = simulation.Work.ExposureNodeVisits;

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Solver.RefreshExposure(simulation.Rooms.Map);
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, simulation.Work.ExposureNodeVisits - before, r);
            }

            return row;
        }


        private static Row ShapeNormals(GridBuilder builder)
        {

            ThermalSimulation simulation = Registered(builder);
            simulation.Surfaces.Rebuild(simulation.Grid);


            Row row = NewRow("shapenormals", simulation, "nodes");
            IList<ThermalNode> all = simulation.Solver.Nodes;
            int nodes = all.Count;
            CellBitset occupancy = simulation.Grid.Occupancy();
            float sink = 0f;

            for (int r = 0; !Settled(row); r++)
            {

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < nodes; i++) sink += ShapeNormal.Of(occupancy, all[i].Block).X;
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, nodes, r);
            }

            if (float.IsNaN(sink)) row.AllocatedBytes = -1;
            return row;
        }


        private static Row NodeSync(GridBuilder builder, bool dirty)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 300f));
            for (int i = 0; i < 3; i++) simulation.Solver.Step(simulation.Settings.StepSeconds, state);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;


            Row row = NewRow(dirty ? "syncdirty" : "syncclean", simulation, "nodes");
            for (int r = 0; !Settled(row); r++)
            {
                if (dirty)
                {
                    for (int i = 0; i < nodes.Count; i++) nodes[i].StateDirty = true;
                }


                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                simulation.Solver.SyncNodeState();
                watch.Stop();
                if (r == 1) row.AllocatedBytes = Allocated() - allocated;
                Take(row, watch.Elapsed.TotalMilliseconds);
                Work(row, nodes.Count, r);
            }

            return row;
        }


        public static List<Row> StepPhases(string shape, int blocks, Action<string> log = null)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build(shape, blocks));

            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 300f));
            float step = simulation.Settings.StepSeconds;

            if (log != null) log("step phases, " + blocks.ToString("n0") + " blocks");

            for (int i = 0; i < SolverStepsPerRepeat; i++) simulation.Solver.Step(step, state);

            Settle();
            simulation.Solver.ProfileStepPhases = true;

            int repeats = Math.Max(1, Repeats);
            double[] best = new double[ThermalSolver.StepPhaseProfile.PhaseCount];
            double[] worst = new double[ThermalSolver.StepPhaseProfile.PhaseCount];

            List<double>[] samples = new List<double>[ThermalSolver.StepPhaseProfile.PhaseCount];
            for (int p = 0; p < samples.Length; p++) samples[p] = new List<double>();
            long[] visits = new long[ThermalSolver.StepPhaseProfile.PhaseCount];
            long[] slices = new long[ThermalSolver.StepPhaseProfile.PhaseCount];
            for (int p = 0; p < best.Length; p++) best[p] = double.MaxValue;

            for (int r = 0; r < repeats; r++)
            {
                simulation.Solver.StepPhases.Reset();
                for (int i = 0; i < SolverStepsPerRepeat; i++) simulation.Solver.Step(step, state);

                for (int p = 0; p < best.Length; p++)
                {
                    double ms = simulation.Solver.StepPhases.MillisecondsOf(p) / SolverStepsPerRepeat;
                    if (ms < best[p]) best[p] = ms;
                    if (ms > worst[p]) worst[p] = ms;
                    samples[p].Add(ms);

                    long v = simulation.Solver.StepPhases.Visits[p] / SolverStepsPerRepeat;
                    if (r > 0 && v != visits[p])
                    {
                        throw new InvalidOperationException(
                            "the " + ThermalSolver.StepPhaseProfile.Names[p] + " stage charged "
                            + v + " visits this repeat and " + visits[p] + " last, so these are"
                            + " readings of two different walks");
                    }

                    visits[p] = v;
                    slices[p] = simulation.Solver.StepPhases.Slices[p] / SolverStepsPerRepeat;
                }
            }

            simulation.Solver.ProfileStepPhases = false;


            List<Row> rows = new List<Row>();
            for (int p = 0; p < best.Length; p++)
            {

                Row row = new Row();
                row.Stage = ThermalSolver.StepPhaseProfile.Names[p];
                row.Blocks = simulation.Solver.Nodes.Count;
                row.BestMs = best[p];
                row.WorstMs = worst[p];
                row.Work = visits[p];
                row.WorkUnit = "visits";

                row.Repeats = repeats;
                row.Stop = Row.Fixed;
                row.Note = slices[p] + " slices";
                row.Samples.AddRange(samples[p]);
                Summarise(row);
                rows.Add(row);
            }

            return rows;
        }


        private static Row Solver(GridBuilder builder)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 300f));
            float step = simulation.Settings.StepSeconds;

            for (int i = 0; i < 3; i++) simulation.Solver.Step(step, state);


            Row row = NewRow("solver", simulation, "substeps");
            for (int r = 0; !Settled(row); r++)
            {
                long before = simulation.Work.SolverSubsteps;

                long allocated = r == 1 ? Allocated() : 0;
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < SolverStepsPerRepeat; i++) simulation.Solver.Step(step, state);
                watch.Stop();
                if (r == 1) row.AllocatedBytes = (Allocated() - allocated) / SolverStepsPerRepeat;
                Take(row, watch.Elapsed.TotalMilliseconds / SolverStepsPerRepeat);
                Work(row, simulation.Work.SolverSubsteps - before, r);
            }

            return row;
        }


        public static string Table(IList<Row> rows)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("  stage        blocks       best ms    median ms   best/med      worst ms   spread  repeats  stopped          work  unit              ns/unit      alloc KB  note");
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,-10} {1,9:n0}  {2,12:n3}  {3,11:n3}  {4,8:n2}  {5,12:n3}  {6,6:n0}%  {7,7:n0}  {8,-9}  {9,12:n0}  {10,-16}  {11,8:n1}  {12,12:n0}  {13}",
                    row.Stage, row.Blocks, row.BestMs, row.MedianMs, row.FastModeShare,
                    row.WorstMs, row.SpreadPercent,
                    row.Repeats, row.Stop,
                    row.Work, row.WorkUnit, row.NsPerWork, row.AllocatedBytes / 1024, row.Note));
            }
            return text.ToString();
        }

        public static readonly string TakenUtc =
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        public static readonly string Host = Environment.MachineName;


        public static string Csv(IList<Row> rows)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("stage,blocks,best_ms,median_ms,fast_mode_share,worst_ms,spread_percent,repeats,confirmed_best,stopped,work,work_unit,ns_per_unit,allocated_bytes,taken_utc,host");
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                text.AppendLine(string.Join(",",
                    row.Stage,
                    row.Blocks.ToString(CultureInfo.InvariantCulture),
                    row.BestMs.ToString("r", CultureInfo.InvariantCulture),
                    row.MedianMs.ToString("r", CultureInfo.InvariantCulture),
                    row.FastModeShare.ToString("r", CultureInfo.InvariantCulture),
                    row.WorstMs.ToString("r", CultureInfo.InvariantCulture),
                    row.SpreadPercent.ToString("r", CultureInfo.InvariantCulture),
                    row.Repeats.ToString(CultureInfo.InvariantCulture),
                    row.ConfirmedBest.ToString(CultureInfo.InvariantCulture),
                    row.Stop,
                    row.Work.ToString(CultureInfo.InvariantCulture),
                    row.WorkUnit,
                    row.NsPerWork.ToString("r", CultureInfo.InvariantCulture),
                    row.AllocatedBytes.ToString(CultureInfo.InvariantCulture),
                    TakenUtc,
                    Host));
            }
            return text.ToString();
        }


        public static List<Row> ReadCsv(string path)
        {

            List<Row> rows = new List<Row>();
            string[] lines = File.ReadAllLines(path);

            if (lines.Length < 2)
            {
                throw new InvalidOperationException(path + " holds no rows, so the stage it was"
                    + " supposed to time reported nothing");
            }

            string[] header = lines[0].Split(',');

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length == 0) continue;

                string[] parts = lines[i].Split(',');

                Row row = new Row();

                row.Stage = Field(header, parts, "stage", path);
                row.Blocks = int.Parse(Field(header, parts, "blocks", path), CultureInfo.InvariantCulture);
                row.BestMs = double.Parse(Field(header, parts, "best_ms", path), CultureInfo.InvariantCulture);
                row.MedianMs = double.Parse(Field(header, parts, "median_ms", path), CultureInfo.InvariantCulture);
                row.WorstMs = double.Parse(Field(header, parts, "worst_ms", path), CultureInfo.InvariantCulture);
                row.Repeats = int.Parse(Field(header, parts, "repeats", path), CultureInfo.InvariantCulture);
                row.ConfirmedBest = int.Parse(Field(header, parts, "confirmed_best", path), CultureInfo.InvariantCulture);

                row.Stop = Field(header, parts, "stopped", path);
                row.Work = long.Parse(Field(header, parts, "work", path), CultureInfo.InvariantCulture);

                row.WorkUnit = Field(header, parts, "work_unit", path);
                row.AllocatedBytes = long.Parse(Field(header, parts, "allocated_bytes", path), CultureInfo.InvariantCulture);
                rows.Add(row);
            }

            return rows;
        }


        private static string Field(string[] header, string[] parts, string column, string path)
        {
            int index = Array.IndexOf(header, column);
            if (index < 0 || index >= parts.Length)
            {
                throw new InvalidOperationException(path + " has no \"" + column + "\" column, so it"
                    + " was not written by this lab");
            }
            return parts[index];
        }


        public static string SamplesCsv(IList<Row> rows)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("stage,blocks,repeat,ms,taken_utc");
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                for (int r = 0; r < row.Samples.Count; r++)
                {
                    text.AppendLine(string.Join(",",
                        row.Stage,
                        row.Blocks.ToString(CultureInfo.InvariantCulture),
                        r.ToString(CultureInfo.InvariantCulture),
                        row.Samples[r].ToString("r", CultureInfo.InvariantCulture),
                        TakenUtc));
                }
            }
            return text.ToString();
        }
    }
}

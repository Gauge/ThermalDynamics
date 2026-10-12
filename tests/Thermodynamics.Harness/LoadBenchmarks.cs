using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public class ScaleRow
    {
        public string Shape;
        public int TargetBlocks;
        public int Blocks;
        public int Links;
        public long BoundingVolume;
        public int ExposedBlocks;

        public double BuildMs;

        public double TopologyMs;

        public double RoomMapMs;

        public double ExposureMs;

        public double SolverStepMs;

        public double BoundedStepMs;

        public int SubstepBudget;

        public int Substeps;
        public double ResidentMb;

        public double NsPerLinkVisit
        {
            get
            {
                long visits = (long)Links * Math.Max(1, Substeps);
                return visits == 0 ? 0d : SolverStepMs * 1e6d / visits;
            }
        }

        public double MsPerSimulatedSecond;

        public double SpikeAfterOneBlockMs;

        public int SettleTicks;
        public double SettleTotalMs;
    }

    public class HitchResult
    {
        public string Name;
        public int Blocks;
        public int Links;
        public double BuildMs;
        public FrameTrace Trace;
        public string Notes = "";

        public double TopologyMs;
        public double RoomMappingMs;
        public double ExposureMs;
        public double SolverMs;
        public int TopologyTick = -1;
        public int RoomMappingTick = -1;
        public int ExposureTick = -1;
        public int SolverTick = -1;

        public int Gen0;
        public int Gen1;
        public int Gen2;
        public double AllocatedMb;


        public string DescribeGc()
        {
            return "GC during the run: " + Gen0 + "/" + Gen1 + "/" + Gen2
                + " collections, " + AllocatedMb.ToString("n0") + " MB allocated.";
        }


        public string DescribeStages()
        {
            return "worst call per stage: topology " + TopologyMs.ToString("n1")
                + " (tick " + TopologyTick + "), rooms " + RoomMappingMs.ToString("n1")
                + " (tick " + RoomMappingTick + "), exposure " + ExposureMs.ToString("n1")
                + " (tick " + ExposureTick + "), solver " + SolverMs.ToString("n1")
                + " (tick " + SolverTick + ") ms.";
        }
    }

    public static class LoadBenchmarks
    {
        public const float FrameSeconds = 1f / 60f;

        public const float TickSeconds = 10f / 60f;

        public static readonly int[] DefaultSizes = { 8000, 32000, 125000, 500000, 1000000 };

        public static readonly string[] Names =
            { "scale", "hitch", "weld", "load", "spike", "firststep", "pace", "reach", "memory", "floor" };



        public static List<ScaleRow> Scale(string shape, IList<int> sizes, Action<string> log = null)
        {

            List<ScaleRow> rows = new List<ScaleRow>();

            for (int i = 0; i < sizes.Count; i++)
            {
                if (log != null) log(shape + " " + sizes[i].ToString("n0") + " ...");
                rows.Add(MeasureScale(shape, sizes[i]));
            }

            return rows;
        }


        private static ScaleRow MeasureScale(string shape, int targetCells)
        {
            HashSet<Vector3I> cells = LoadShapes.Build(shape, targetCells);

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(cells);


            return MeasureBuilt(builder, shape, targetCells);
        }


        public static ScaleRow MeasureBuilt(GridBuilder builder, string shape = "franken",
            int targetCells = 0)
        {

            ScaleRow row = new ScaleRow();
            row.Shape = shape;
            row.TargetBlocks = targetCells > 0 ? targetCells : builder.Placed.Count;

            long before = GC.GetTotalMemory(true);

            Stopwatch build = Stopwatch.StartNew();


            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }

            simulation.RebuildAll();
            build.Stop();
            row.BuildMs = build.Elapsed.TotalMilliseconds;

            row.ResidentMb = Math.Max(0d, (GC.GetTotalMemory(false) - before) / (1024d * 1024d));

            row.Blocks = simulation.Solver.Nodes.Count;
            row.Links = simulation.Solver.Links.Count;

            Vector3I extents = (simulation.Grid.Max - simulation.Grid.Min) + Vector3I.One;
            row.BoundingVolume = (long)extents.X * extents.Y * extents.Z;

            for (int i = 0; i < row.Blocks; i++)
            {
                if (simulation.Solver.Nodes[i].TotalExposedFaces > 0) row.ExposedBlocks++;
            }


            Stopwatch watch = Stopwatch.StartNew();
            simulation.Solver.RebuildLinks();
            watch.Stop();
            row.TopologyMs = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            simulation.Rooms.RequestRestart(simulation.Grid);
            if (!simulation.Rooms.RunToCompletion())
            {
                throw new InvalidOperationException(
                    "the room map did not finish on " + shape + " at " + targetCells
                    + " blocks, so every figure from this rung would describe an unmapped hull");
            }
            watch.Stop();
            row.RoomMapMs = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            simulation.Solver.RefreshExposure(simulation.Rooms.Map);
            watch.Stop();
            row.ExposureMs = watch.Elapsed.TotalMilliseconds;


            SeedSpread(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Space(new Vector3(0f, 1f, 0f)));

            float step = simulation.Settings.StepSeconds;
            for (int i = 0; i < 3; i++) simulation.Solver.Step(step, state);

            int measured = row.Blocks > 200000 ? 3 : (row.Blocks > 50000 ? 10 : 40);

            watch.Restart();
            for (int i = 0; i < measured; i++) simulation.Solver.Step(step, state);
            watch.Stop();

            row.SolverStepMs = watch.Elapsed.TotalMilliseconds / measured;
            row.Substeps = simulation.Solver.LastSubsteps;
            row.MsPerSimulatedSecond = row.SolverStepMs * simulation.Settings.StepsPerSecond;

            row.SubstepBudget = simulation.SubstepBudget;

            float affordable = simulation.AffordableStepSeconds(step);
            for (int i = 0; i < 2; i++) simulation.Solver.Step(affordable, state);

            watch.Restart();
            for (int i = 0; i < measured; i++) simulation.Solver.Step(affordable, state);
            watch.Stop();

            row.BoundedStepMs = watch.Elapsed.TotalMilliseconds / measured;



            Vector3I spare = simulation.Grid.Max + new Vector3I(0, 0, 1);
            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), spare, BlockOrientation.Identity), 293.15f);

            double worst = 0d;
            double total = 0d;
            int settleTicks = 0;

            do
            {
                watch.Restart();
                simulation.Update(TickSeconds, Worlds.Shadow());
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds;
                if (ms > worst) worst = ms;
                total += ms;
                settleTicks++;
            }
            while (simulation.HasPendingWork && settleTicks < 100000);

            row.SpikeAfterOneBlockMs = worst;
            row.SettleTicks = settleTicks;
            row.SettleTotalMs = total;

            return row;
        }


        public static bool CollectDiagnostics;


        public static HitchResult Hitch(string shape, int targetCells, int ticks = 400)
        {

            HitchResult result = new HitchResult();
            result.Name = "hitch " + shape + " " + targetCells.ToString("n0");

            Stopwatch build = Stopwatch.StartNew();

            ThermalSimulation simulation = BuildSettled(shape, targetCells);
            build.Stop();

            result.BuildMs = build.Elapsed.TotalMilliseconds;
            result.Blocks = simulation.Solver.Nodes.Count;
            result.Links = simulation.Solver.Links.Count;

            SeedSpread(simulation);
            SettleMemory();
            simulation.Work.Reset();

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes(false);


            FrameTrace trace = new FrameTrace(result.Name);
            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));

            BlockModel armour = Catalog.HeavyArmor();

            Vector3I weldAt = simulation.Grid.Max + new Vector3I(0, 0, 2);
            const float frame = FrameSeconds;


            Stopwatch watch = new Stopwatch();

            for (int tick = 0; tick < ticks; tick++)
            {

                StageTimings timings = new StageTimings();
                simulation.Profiler = timings;

                string what = "steady";

                if (tick == ticks / 4)
                {
                    simulation.AddBlock(new BlockInstance(armour, weldAt, BlockOrientation.Identity), 293.15f);
                    what = "one block placed";
                }

                else if (tick == ticks / 2)
                {
                    BlockInstance placed = simulation.Grid.GetAtCell(weldAt);
                    if (placed != null)
                    {
                        simulation.RemoveBlock(placed);
                        what = "one block removed";
                    }
                }

                watch.Restart();
                simulation.Update(frame, sample);
                watch.Stop();

                trace.Add(watch.Elapsed.TotalMilliseconds,
                    what + ", " + simulation.Solver.LastSubsteps + " substeps, "
                    + simulation.Work.SolverSteps + " steps so far");
                RecordStages(result, timings, tick);
            }

            simulation.Profiler = null;
            result.Trace = trace;
            RecordGc(result, gen0, gen1, gen2, allocated);
            return result;
        }


        public static HitchResult Weld(string shape, int targetCells, int ticks = 120)
        {

            HitchResult result = new HitchResult();
            result.Name = "weld " + shape + " " + targetCells.ToString("n0");

            Stopwatch build = Stopwatch.StartNew();

            ThermalSimulation simulation = BuildSettled(shape, targetCells);
            build.Stop();

            result.BuildMs = build.Elapsed.TotalMilliseconds;
            result.Blocks = simulation.Solver.Nodes.Count;
            result.Links = simulation.Solver.Links.Count;

            SettleMemory();

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes(false);


            FrameTrace trace = new FrameTrace(result.Name);
            EnvironmentSample sample = Worlds.Shadow();
            BlockModel armour = Catalog.HeavyArmor();


            Vector3I start = simulation.Grid.Min - new Vector3I(2, 0, 0);

            Stopwatch watch = new Stopwatch();

            for (int tick = 0; tick < ticks; tick++)
            {

                StageTimings timings = new StageTimings();
                simulation.Profiler = timings;


                Vector3I at = start + new Vector3I(0, 0, tick);
                simulation.AddBlock(new BlockInstance(armour, at, BlockOrientation.Identity), 293.15f);

                watch.Restart();
                simulation.Update(FrameSeconds, sample);
                watch.Stop();

                trace.Add(watch.Elapsed.TotalMilliseconds, "block " + tick + " welded");
                RecordStages(result, timings, tick);
            }

            simulation.Profiler = null;
            result.Trace = trace;
            RecordGc(result, gen0, gen1, gen2, allocated);
            result.Notes = ticks + " blocks welded, one per tick";
            return result;
        }


        public static HitchResult Load(string shape, int targetCells)
        {

            HitchResult result = new HitchResult();
            result.Name = "load " + shape + " " + targetCells.ToString("n0");


            FrameTrace trace = new FrameTrace(result.Name);
            HashSet<Vector3I> cells = LoadShapes.Build(shape, targetCells);

            Stopwatch watch = Stopwatch.StartNew();

            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.HeavyArmor();
            foreach (Vector3I cell in cells) builder.Place(armour, cell);


            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            double addMs = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            simulation.RebuildAll();
            watch.Stop();

            trace.Add(watch.Elapsed.TotalMilliseconds, "RebuildAll");

            result.Blocks = simulation.Solver.Nodes.Count;
            result.Links = simulation.Solver.Links.Count;
            result.BuildMs = addMs + watch.Elapsed.TotalMilliseconds;
            result.Trace = trace;
            result.Notes = "adding blocks " + addMs.ToString("n0") + " ms, RebuildAll "
                + watch.Elapsed.TotalMilliseconds.ToString("n0") + " ms";
            return result;
        }

        public class StabilityRow
        {
            public string Label;
            public float HeatTimeScale;
            public int MaxSubsteps;

            public float MinTemperature;
            public float MaxTemperature;
            public float FinalSpread;
            public long ClampedSteps;
            public long Steps;
            public bool WentBad;
        }


        public static StabilityRow Stability(string label, int frequency, float heatTimeScale,
            int maxSubsteps, float realSeconds)
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            settings.Frequency = frequency;
            settings.HeatTimeScale = heatTimeScale;
            settings.MaxSubsteps = maxSubsteps;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            return Stability(label, settings, realSeconds);
        }


        public static StabilityRow Stability(string label, ThermalSettings settings, float realSeconds)
        {
            float heatTimeScale = settings.HeatTimeScale;
            int maxSubsteps = settings.MaxSubsteps;

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(GridShapes.Ship(fuselageLength: 20, fuselageWidth: 7, bulkheadSpacing: 6));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            while (simulation.HasPendingWork) simulation.Update(FrameSeconds, Worlds.Shadow());

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++) nodes[i].Temperature = 293.15f;
            nodes[nodes.Count / 2].Temperature = 1200f;


            StabilityRow row = new StabilityRow();
            row.Label = label;
            row.HeatTimeScale = heatTimeScale;
            row.MaxSubsteps = maxSubsteps;
            row.MinTemperature = float.MaxValue;
            row.MaxTemperature = float.MinValue;

            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));
            int frames = (int)(realSeconds / FrameSeconds);

            for (int f = 0; f < frames; f++)
            {
                simulation.Update(FrameSeconds, sample);

                if (simulation.Solver.LastStepWasClamped && simulation.Work.SolverSteps > row.Steps)
                {
                    row.ClampedSteps++;
                }
                row.Steps = simulation.Work.SolverSteps;

                for (int i = 0; i < nodes.Count; i++)
                {
                    float t = nodes[i].Temperature;
                    if (float.IsNaN(t) || float.IsInfinity(t)) row.WentBad = true;
                    if (t < row.MinTemperature) row.MinTemperature = t;
                    if (t > row.MaxTemperature) row.MaxTemperature = t;
                }
            }

            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float t = nodes[i].Temperature;
                if (t < low) low = t;
                if (t > high) high = t;
            }
            row.FinalSpread = high - low;

            return row;
        }


        public static string StabilityTable(IList<StabilityRow> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("case".PadRight(18)).Append("heatScale".PadLeft(11))
              .Append("maxSub".PadLeft(8)).Append("min K".PadLeft(10))
              .Append("max K".PadLeft(12)).Append("end spread".PadLeft(12))
              .Append("clamped".PadLeft(9)).Append("bad".PadLeft(6)).Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                StabilityRow r = rows[i];
                sb.Append(r.Label.PadRight(18))
                  .Append(r.HeatTimeScale.ToString("n0").PadLeft(11))
                  .Append(r.MaxSubsteps.ToString().PadLeft(8))
                  .Append(r.MinTemperature.ToString("n1").PadLeft(10))
                  .Append(r.MaxTemperature.ToString("n1").PadLeft(12))
                  .Append(r.FinalSpread.ToString("n1").PadLeft(12))
                  .Append((r.Steps == 0 ? "-" : (100.0 * r.ClampedSteps / r.Steps).ToString("n0") + "%").PadLeft(9))
                  .Append((r.WentBad ? "YES" : "no").PadLeft(6))
                  .Append('\n');
            }
            return sb.ToString();
        }

        public class ReachRow
        {
            public string Label;
            public int Frequency;
            public float Speed;
            public float HeatTimeScale;
            public int MaxSubsteps;

            public int BlocksReached;

            public float RealSeconds;

            public double BlocksPerRealSecond
            {
                get { return RealSeconds <= 0f ? 0d : BlocksReached / (double)RealSeconds; }
            }

            public long Substeps;
            public double SubstepsPerRealSecond;

            public double WorkPerRealSecond;

            public double MillisecondsPerRealSecond;

            public long ClampedSteps;
            public long Steps;
        }


        public static ReachRow Reach(string label, int frequency, float speed, float heatTimeScale,
            int maxSubsteps, float realSeconds, int length)
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            settings.Frequency = frequency;
            settings.SimulationSpeed = speed;
            settings.HeatTimeScale = heatTimeScale;
            settings.MaxSubsteps = maxSubsteps;

            return Reach(label, settings, realSeconds, length);
        }


        public static ReachRow Reach(string label, ThermalSettings settings, float realSeconds, int length)
        {
            int frequency = settings.Frequency;
            float speed = settings.SimulationSpeed;
            float heatTimeScale = settings.HeatTimeScale;
            int maxSubsteps = settings.MaxSubsteps;

            settings.EnableEnvironment = false;
            settings.EnableRadiation = false;
            settings.EnableConvection = false;
            settings.EnableSolarHeat = false;
            settings.EnableWasteHeat = false;
            settings.EnableRoomAir = false;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.HeavyArmor();
            for (int i = 0; i < length; i++)
            {
                builder.Place(armour, new Vector3I(0, 0, i));
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            while (simulation.HasPendingWork) simulation.Update(FrameSeconds, Worlds.Shadow());

            ThermalNode[] run = new ThermalNode[length];
            for (int i = 0; i < length; i++)
            {
                run[i] = simulation.Solver.GetNodeAt(new Vector3I(0, 0, i));
                run[i].Temperature = 300f;
            }


            ReachRow row = new ReachRow();
            row.Label = label;
            row.Frequency = frequency;
            row.Speed = speed;
            row.HeatTimeScale = heatTimeScale;
            row.MaxSubsteps = maxSubsteps;
            row.RealSeconds = realSeconds;

            simulation.Work.Reset();
            EnvironmentSample sample = Worlds.Shadow();
            int frames = (int)(realSeconds / FrameSeconds);

            SettleMemory();
            Stopwatch watch = Stopwatch.StartNew();

            for (int f = 0; f < frames; f++)
            {
                run[0].Temperature = 1000f;
                simulation.Update(FrameSeconds, sample);
                if (simulation.Solver.LastStepWasClamped && simulation.Work.SolverSteps > row.Steps)
                {
                    row.ClampedSteps++;
                }
                row.Steps = simulation.Work.SolverSteps;
            }

            watch.Stop();

            for (int i = length - 1; i >= 0; i--)
            {
                if (run[i].Temperature < 370f) continue;
                row.BlocksReached = i;
                break;
            }

            row.Substeps = simulation.Work.SolverSubsteps;
            row.SubstepsPerRealSecond = row.Substeps / (double)realSeconds;
            row.WorkPerRealSecond = row.Substeps
                * (double)(simulation.Solver.Nodes.Count + simulation.Solver.LinkCount) / realSeconds;
            row.MillisecondsPerRealSecond = watch.Elapsed.TotalMilliseconds / realSeconds;

            return row;
        }


        public static string ReachTable(IList<ReachRow> rows)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("profile".PadRight(14)).Append("freq".PadLeft(6))
              .Append("speed".PadLeft(7)).Append("heatScale".PadLeft(11))
              .Append("maxSub".PadLeft(8)).Append("blocks/s".PadLeft(10))
              .Append("substep/s".PadLeft(11)).Append("work/s".PadLeft(12))
              .Append("ms/s".PadLeft(8)).Append("clamped".PadLeft(9)).Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                ReachRow r = rows[i];
                sb.Append(r.Label.PadRight(14))
                  .Append(r.Frequency.ToString().PadLeft(6))
                  .Append(r.Speed.ToString("n2").PadLeft(7))
                  .Append(r.HeatTimeScale.ToString("n0").PadLeft(11))
                  .Append(r.MaxSubsteps.ToString().PadLeft(8))
                  .Append(r.BlocksPerRealSecond.ToString("n1").PadLeft(10))
                  .Append(r.SubstepsPerRealSecond.ToString("n1").PadLeft(11))
                  .Append(r.WorkPerRealSecond.ToString("n0").PadLeft(12))
                  .Append(r.MillisecondsPerRealSecond.ToString("n2").PadLeft(8))
                  .Append((r.Steps == 0 ? "-" : (100.0 * r.ClampedSteps / r.Steps).ToString("n0") + "%")
                      .PadLeft(9))
                  .Append('\n');
            }

            return sb.ToString();
        }

        public class PaceRow
        {
            public float Speed;
            public float HeatTimeScale;
            public int Frequency;

            public long Steps;
            public long Substeps;
            public long WorkUnits;
            public double Milliseconds;

            public double SimulatedPerReal;

            public float TemperatureChange;
        }


        public static List<PaceRow> Pace(string shape, int targetCells, float realSeconds)
        {

            List<PaceRow> rows = new List<PaceRow>();

            float[] speeds = { 1f, 0.5f, 0.25f };
            float[] scales = { 225f, 450f, 900f };

            for (int i = 0; i < speeds.Length; i++)
            {
                rows.Add(MeasurePace(shape, targetCells, realSeconds, speeds[i], scales[i], 4));
            }

            rows.Add(MeasurePace(shape, targetCells, realSeconds, 1f, 225f, 1));
            rows.Add(MeasurePace(shape, targetCells, realSeconds, 1f, 225f, 16));

            return rows;
        }


        private static PaceRow MeasurePace(string shape, int targetCells, float realSeconds,
            float speed, float heatTimeScale, int frequency)
        {

            PaceRow row = new PaceRow();
            row.Speed = speed;
            row.HeatTimeScale = heatTimeScale;
            row.Frequency = frequency;


            ThermalSimulation simulation = BuildSettled(shape, targetCells);
            while (simulation.HasPendingWork) simulation.Update(TickSeconds, Worlds.Shadow());

            ThermalSettings settings = simulation.Settings;
            settings.SimulationSpeed = speed;
            settings.HeatTimeScale = heatTimeScale;
            settings.Frequency = frequency;

            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            SeedSpread(simulation);

            ThermalNode tracked = simulation.Solver.Nodes[simulation.Solver.Nodes.Count / 2];
            float before = tracked.Temperature;

            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));
            int frames = (int)(realSeconds / FrameSeconds);

            simulation.Work.Reset();
            SettleMemory();

            Stopwatch watch = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                simulation.Update(FrameSeconds, sample);
            }
            watch.Stop();

            row.Milliseconds = watch.Elapsed.TotalMilliseconds;
            row.Steps = simulation.Work.SolverSteps;
            row.Substeps = simulation.Work.SolverSubsteps;
            row.WorkUnits = simulation.Work.SolverSubsteps
                * (simulation.Solver.Nodes.Count + simulation.Solver.LinkCount);
            row.SimulatedPerReal = simulation.SimulatedSecondsRun / realSeconds;
            row.TemperatureChange = tracked.Temperature - before;

            return row;
        }


        public static string PaceTable(IList<PaceRow> rows, float realSeconds)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("speed".PadLeft(7)).Append("heatScale".PadLeft(11))
              .Append("freq".PadLeft(6)).Append("steps".PadLeft(8))
              .Append("substeps".PadLeft(10)).Append("work/s".PadLeft(14))
              .Append("ms/real s".PadLeft(11)).Append("sim s/real s".PadLeft(14))
              .Append("dT over run".PadLeft(13)).Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                PaceRow r = rows[i];
                sb.Append(r.Speed.ToString("n2").PadLeft(7))
                  .Append(r.HeatTimeScale.ToString("n0").PadLeft(11))
                  .Append(r.Frequency.ToString().PadLeft(6))
                  .Append(r.Steps.ToString("n0").PadLeft(8))
                  .Append(r.Substeps.ToString("n0").PadLeft(10))
                  .Append((r.WorkUnits / realSeconds).ToString("n0").PadLeft(14))
                  .Append((r.Milliseconds / realSeconds).ToString("n1").PadLeft(11))
                  .Append(r.SimulatedPerReal.ToString("n2").PadLeft(14))
                  .Append(r.TemperatureChange.ToString("n2").PadLeft(13))
                  .Append('\n');
            }

            return sb.ToString();
        }

        public class MemoryRow
        {
            public string Stage;
            public double Megabytes;
            public long Entries;
            public string Note = "";

            public double BytesPerBlock;
        }


        public static List<MemoryRow> Memory(string shape, int targetCells)
        {

            List<MemoryRow> rows = new List<MemoryRow>();

            HashSet<Vector3I> cells = LoadShapes.Build(shape, targetCells);
            BlockModel[] tiers = Census.Models();


            long baseline = Settled();

            GridBuilder builder = GridBuilder.Large();

            List<BlockInstance> instances = new List<BlockInstance>();
            int index = 0;
            foreach (Vector3I cell in cells)
            {

                BlockInstance block = new BlockInstance(
                    tiers[Census.TierAt(index++)], cell, BlockOrientation.Identity);
                instances.Add(block);
            }


            long afterInstances = Settled();
            int blocks = instances.Count;
            rows.Add(Row("BlockInstance", afterInstances - baseline, blocks, blocks,
                "one object per block, plus its cell and surface arrays"));

            GridModel grid = builder.Grid;
            for (int i = 0; i < instances.Count; i++) grid.Add(instances[i]);


            long afterGrid = Settled();
            rows.Add(Row("GridModel indexes", afterGrid - afterInstances, blocks, blocks,
                "by cell, by slot, and the flat list"));


            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), grid);
            simulation.Surfaces.Rebuild(grid);


            long afterSurfaces = Settled();
            rows.Add(Row("SurfaceMap", afterSurfaces - afterGrid, simulation.Surfaces.CellCount, blocks,
                "one packed entry per occupied cell, holding both surface layers"));

            for (int i = 0; i < instances.Count; i++)
            {
                simulation.Solver.AddBlock(instances[i], 293.15f);
            }
            simulation.Solver.RebuildLinks();


            long afterSolver = Settled();
            rows.Add(Row("Solver", afterSolver - afterSurfaces, simulation.Solver.LinkCount, blocks,
                "nodes, mirrored arrays, links and their chains"));

            Vector3I extents = (grid.Max - grid.Min) + Vector3I.One;
            long volume = (long)extents.X * extents.Y * extents.Z;

            simulation.Rooms.RequestRestart(grid);

            long peak = afterSolver;
            while (simulation.Rooms.HasWorkPending)
            {
                simulation.Rooms.Step(65536);
                long now = GC.GetTotalMemory(false);
                if (now > peak) peak = now;
            }


            long afterRooms = Settled();

            RoomMap map = simulation.Rooms.Map;
            rows.Add(Row("RoomMap retained", afterRooms - afterSolver, volume, blocks,
                "stored: " + map.SolidCellCount.ToString("n0") + " solid + "
                + map.RoomCellCount.ToString("n0") + " cells in " + map.RoomCount.ToString("n0")

                + " rooms (" + (map.RoomCellCapacity - map.RoomCellCount).ToString("n0")
                + " cells of slack); counted, not stored: "
                + map.ExternalCellCount.ToString("n0") + " external"));
            rows.Add(Row("RoomMapper peak", peak - afterSolver, volume, blocks,
                "allocated and not yet collected while a pass runs; live is a bit and a byte a cell"));

            rows.Add(Row("TOTAL retained", afterRooms - baseline, blocks, blocks, ""));
            rows.Add(Row("TOTAL peak", peak - baseline, blocks, blocks,
                "what the process actually has to hold"));

            GC.KeepAlive(simulation);
            GC.KeepAlive(instances);
            GC.KeepAlive(grid);
            GC.KeepAlive(cells);

            return rows;
        }


        private static MemoryRow Row(string stage, long bytes, long entries, int blocks, string note)
        {

            MemoryRow row = new MemoryRow();
            row.Stage = stage;
            row.Megabytes = bytes / (1024d * 1024d);
            row.Entries = entries;
            row.Note = note;
            row.BytesPerBlock = blocks == 0 ? 0d : bytes / (double)blocks;
            return row;
        }


        private static long Settled()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return GC.GetTotalMemory(true);
        }


        public static string MemoryTable(IList<MemoryRow> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("stage".PadRight(24)).Append("MB".PadLeft(10))
              .Append("B/block".PadLeft(10)).Append("entries".PadLeft(14))
              .Append("  what holds it").Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                MemoryRow r = rows[i];
                sb.Append(r.Stage.PadRight(24))
                  .Append(r.Megabytes.ToString("n1").PadLeft(10))
                  .Append(r.BytesPerBlock.ToString("n0").PadLeft(10))
                  .Append(r.Entries.ToString("n0").PadLeft(14))
                  .Append("  ").Append(r.Note).Append('\n');
            }
            return sb.ToString();
        }


        public class SpikeReport
        {
            public int Blocks;
            public int Links;

            public double WorstTickMs;

            public double TopologyMs;
            public double RoomMappingMs;
            public double ExposureMs;
            public double SolverMs;

            public int TopologyTick = -1;
            public int RoomMappingTick = -1;
            public int ExposureTick = -1;
            public int SolverTick = -1;

            public int Ticks;

            public SimulationWork Work = new SimulationWork();

            public int Gen0;
            public int Gen1;
            public int Gen2;
            public double AllocatedMb;


            public string Describe()
            {
                return "worst tick " + WorstTickMs.ToString("n1") + " ms over " + Ticks
                    + " ticks to settle. Worst call per stage: topology "
                    + TopologyMs.ToString("n1") + " (tick " + TopologyTick + "), rooms "
                    + RoomMappingMs.ToString("n1") + " (tick " + RoomMappingTick + "), exposure "
                    + ExposureMs.ToString("n1") + " (tick " + ExposureTick + "), solver "
                    + SolverMs.ToString("n1") + " (tick " + SolverTick + ") ms."
                    + " GC: " + Gen0 + "/" + Gen1 + "/" + Gen2 + " collections, "
                    + AllocatedMb.ToString("n0") + " MB allocated."
                    + " Ran: " + Work.TopologyRebuilds + " topology rebuilds, "
                    + Work.ExposureRefreshes + " exposure refreshes, "
                    + Work.RoomPassesBegun + " room passes, "
                    + Work.SolverSteps + " solver steps."
                    + " Touched: " + Work.TopologyNodeVisits.ToString("n0") + " nodes for topology, "
                    + Work.ExposureNodeVisits.ToString("n0") + " for exposure, "
                    + Work.LoopSearchCells.ToString("n0") + " for coolant loops, "
                    + Work.HeatPumpNodeVisits.ToString("n0") + " for heat pumps, "
                    + Work.RoomCellsVisited.ToString("n0") + " cells flooded.";
            }
        }


        public static SpikeReport Spike(string shape, int targetCells)
        {

            ThermalSimulation simulation = BuildSettled(shape, targetCells);
            while (simulation.HasPendingWork)
            {
                simulation.Update(TickSeconds, Worlds.Shadow());
            }

            SeedSpread(simulation);

            StageTimings timings = null;
            simulation.Work.Reset();


            SpikeReport report = new SpikeReport();

            report.Blocks = simulation.Solver.Nodes.Count;
            report.Links = simulation.Solver.LinkCount;

            BlockModel armour = Catalog.HeavyArmor();

            Vector3I at = simulation.Grid.Max + new Vector3I(0, 0, 2);
            simulation.AddBlock(new BlockInstance(armour, at, BlockOrientation.Identity), 293.15f);

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes(false);


            Stopwatch watch = new Stopwatch();

            do
            {

                timings = new StageTimings();
                simulation.Profiler = timings;

                watch.Restart();
                simulation.Update(TickSeconds, Worlds.Shadow());
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds;
                if (ms > report.WorstTickMs) report.WorstTickMs = ms;

                Record(report, timings, SimulationPhase.Topology, report.Ticks);
                Record(report, timings, SimulationPhase.RoomMapping, report.Ticks);
                Record(report, timings, SimulationPhase.Exposure, report.Ticks);
                Record(report, timings, SimulationPhase.Solver, report.Ticks);

                report.Ticks++;
            }
            while (simulation.HasPendingWork && report.Ticks < 100000);

            report.Work = simulation.Work.Snapshot();
            report.Gen0 = GC.CollectionCount(0) - gen0;
            report.Gen1 = GC.CollectionCount(1) - gen1;
            report.Gen2 = GC.CollectionCount(2) - gen2;
            report.AllocatedMb = (GC.GetTotalAllocatedBytes(false) - allocated) / (1024d * 1024d);

            simulation.Profiler = null;
            return report;
        }


        private static void RecordGc(HitchResult result, int gen0, int gen1, int gen2, long allocated)
        {
            result.Gen0 = GC.CollectionCount(0) - gen0;
            result.Gen1 = GC.CollectionCount(1) - gen1;
            result.Gen2 = GC.CollectionCount(2) - gen2;
            result.AllocatedMb = (GC.GetTotalAllocatedBytes(false) - allocated) / (1024d * 1024d);
        }


        private static void RecordStages(HitchResult result, StageTimings timings, int tick)
        {
            double topology = timings.WorstMs(SimulationPhase.Topology);
            if (topology > result.TopologyMs) { result.TopologyMs = topology; result.TopologyTick = tick; }

            double rooms = timings.WorstMs(SimulationPhase.RoomMapping);
            if (rooms > result.RoomMappingMs) { result.RoomMappingMs = rooms; result.RoomMappingTick = tick; }

            double exposure = timings.WorstMs(SimulationPhase.Exposure);
            if (exposure > result.ExposureMs) { result.ExposureMs = exposure; result.ExposureTick = tick; }

            double solver = timings.WorstMs(SimulationPhase.Solver);
            if (solver > result.SolverMs) { result.SolverMs = solver; result.SolverTick = tick; }
        }


        private static void Record(SpikeReport report, StageTimings timings, SimulationPhase phase, int tick)
        {
            double ms = timings.WorstMs(phase);

            switch (phase)
            {
                case SimulationPhase.Topology:
                    if (ms > report.TopologyMs) { report.TopologyMs = ms; report.TopologyTick = tick; }
                    break;
                case SimulationPhase.RoomMapping:
                    if (ms > report.RoomMappingMs) { report.RoomMappingMs = ms; report.RoomMappingTick = tick; }
                    break;
                case SimulationPhase.Exposure:
                    if (ms > report.ExposureMs) { report.ExposureMs = ms; report.ExposureTick = tick; }
                    break;
                default:
                    if (ms > report.SolverMs) { report.SolverMs = ms; report.SolverTick = tick; }
                    break;
            }
        }



        private static void SettleMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static bool UseCensus = true;


        public static ThermalSimulation BuildSettled(string shape, int targetCells)
        {
            HashSet<Vector3I> cells = LoadShapes.Build(shape, targetCells);

            GridBuilder builder = GridBuilder.Large();

            if (UseCensus)
            {
                builder.PlaceCensus(cells);
            }
            else
            {

                int index = 0;
                foreach (Vector3I cell in cells)
                {
                    builder.Place(Census.Models()[Census.TierAt(index++)], cell);
                }
            }


            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }

            simulation.RebuildAll();
            simulation.Solver.CollectDiagnostics = CollectDiagnostics;
            return simulation;
        }


        public static void SeedSpread(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            uint state = 0x9E3779B9u;

            for (int i = 0; i < nodes.Count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                nodes[i].Temperature = 250f + (state % 500u);
            }
        }



        public class FloorRow
        {
            public int Cap;
            public float RequiredSubsteps;
            public double Milliseconds;
            public int Nodes;
            public int Floored;
            public float MaxError;
            public double RmsError;
            public float PeakTemperature;
            public float PeakError;

            public float StepSeconds;

            public float AirSpeed;
        }


        public static List<FloorRow> SubstepFloor(string shape, int size, int steps,
            IList<int> caps, Action<string> log = null, bool driven = false, int frequency = 0,
            float airSpeed = 0f)
        {

            List<FloorRow> rows = new List<FloorRow>();
            float[] reference = null;
            float referencePeak = 0f;

            RunFloor(shape, Math.Min(size, 2000), 2, 0, null, driven, frequency, airSpeed);

            for (int i = 0; i < caps.Count; i++)
            {
                if (log != null) log("cap " + caps[i]);


                FloorRow row = RunFloor(shape, size, steps, caps[i], reference, driven, frequency,
                    airSpeed);
                if (reference == null)
                {
                    reference = lastTemperatures;
                    referencePeak = row.PeakTemperature;
                }

                row.PeakError = row.PeakTemperature - referencePeak;
                rows.Add(row);
            }

            return rows;
        }

        private static float[] lastTemperatures;


        private static FloorRow RunFloor(string shape, int size, int steps, int cap,
            float[] reference, bool driven, int frequency, float airSpeed)
        {
            HashSet<Vector3I> cells = LoadShapes.Build(shape, size);

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(cells);


            ThermalSettings settings = new HistoricalThermalSettings();
            settings.MaxSubstepsPerBlock = cap;

            if (frequency > 0) settings.Frequency = frequency;

            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();


            ThermalSimulation simulation = new ThermalSimulation(settings, builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            simulation.RebuildAll();

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int count = nodes.Count;

            if (driven)
            {
                Census.DriveCensus(simulation);
            }
            else
            {
                SeedSpread(simulation);
            }


            EnvironmentSample sample = airSpeed > 0f
                ? Worlds.Flight(1f, airSpeed)
                : Worlds.Space(new Vector3(0f, 1f, 0f));


            FloorRow row = new FloorRow();
            row.Cap = cap;
            row.AirSpeed = airSpeed;
            row.StepSeconds = settings.StepSeconds;
            row.Nodes = count;
            simulation.StepExact(1, sample);

            row.RequiredSubsteps = simulation.Solver.LastRequiredSubsteps;
            row.Floored = cap > 0 ? simulation.Solver.FlooredNodes : 0;

            Stopwatch watch = Stopwatch.StartNew();
            simulation.StepExact(steps, sample);
            watch.Stop();
            row.Milliseconds = watch.Elapsed.TotalMilliseconds;

            float[] result = new float[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = nodes[i].Temperature;
                if (result[i] > row.PeakTemperature) row.PeakTemperature = result[i];
            }
            lastTemperatures = result;

            if (reference != null && reference.Length == count)
            {
                double sumSquares = 0;
                for (int i = 0; i < count; i++)
                {
                    float error = Math.Abs(result[i] - reference[i]);
                    sumSquares += (double)error * error;
                    if (error > row.MaxError) row.MaxError = error;

                }

                row.RmsError = Math.Sqrt(sumSquares / count);
            }

            return row;
        }


        public static string FloorTable(IList<FloorRow> rows)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("cap".PadLeft(6))
              .Append("substeps".PadLeft(10))
              .Append("ms".PadLeft(10))
              .Append("x uncapped".PadLeft(12))
              .Append("floored".PadLeft(11))
              .Append("of".PadLeft(11))
              .Append("peak K".PadLeft(10))
              .Append("peakErr".PadLeft(10))
              .Append("maxErr K".PadLeft(11))
              .Append("rmsErr K".PadLeft(11))
              .Append('\n');

            if (rows.Count > 0)
            {
                sb.Append("  step ")
                  .Append(rows[0].StepSeconds.ToString("n4"))
                  .Append(" s (Frequency ")
                  .Append((1f / rows[0].StepSeconds).ToString("n0"))
                  .Append("), ")
                  .Append(rows[0].AirSpeed > 0f
                      ? "thick air at " + rows[0].AirSpeed.ToString("n0") + " m/s"
                      : "vacuum")
                  .Append(", and every substep count below is proportional to the step\n");
            }

            double baseline = rows.Count > 0 ? rows[0].Milliseconds : 0;

            for (int i = 0; i < rows.Count; i++)
            {
                FloorRow row = rows[i];

                sb.Append((row.Cap <= 0 ? "off" : row.Cap.ToString()).PadLeft(6))
                  .Append(row.RequiredSubsteps.ToString("n2").PadLeft(10))
                  .Append(row.Milliseconds.ToString("n0").PadLeft(10))
                  .Append((row.Milliseconds <= 0 ? "-" : (baseline / row.Milliseconds).ToString("n1") + "x").PadLeft(12))
                  .Append(row.Floored.ToString("n0").PadLeft(11))
                  .Append(row.Nodes.ToString("n0").PadLeft(11))
                  .Append(row.PeakTemperature.ToString("n1").PadLeft(10))
                  .Append((i == 0 ? "-" : row.PeakError.ToString("n3")).PadLeft(10))
                  .Append((i == 0 ? "-" : row.MaxError.ToString("n4")).PadLeft(11))
                  .Append((i == 0 ? "-" : row.RmsError.ToString("n4")).PadLeft(11))
                  .Append('\n');
            }

            return sb.ToString();
        }


        public class CeilingRow
        {
            public int Ceiling;

            public float RequiredSubsteps;

            public double MeanGranted;

            public float Oversubscription;

            public bool Bound;

            public double Milliseconds;
            public int Nodes;

            public float MaxError;
            public double RmsError;

            public float PeakTemperature;
            public float PeakError;

            public float StepSeconds;

            public float Speed;
            public float AirDensity;

            public string Fixture;

            public int CoolantLoops;
            public int RoomsWithAir;

            public float PeakCoupledTemperature;
        }

        public static class CeilingFixtures
        {
            public const string Census = "census";
            public const string Plumbed = "plumbed";
            public const string Pressurised = "pressurised";

            public const string Rings = "rings";
        }


        public static List<CeilingRow> SubstepCeiling(string shape, int size, int steps,
            IList<int> ceilings, Action<string> log = null, bool driven = false, int frequency = 0,
            float speed = 200f, float airDensity = 1f, string fixture = CeilingFixtures.Census,
            float flow = 0f)
        {

            List<CeilingRow> rows = new List<CeilingRow>();
            float[] reference = null;
            float referencePeak = 0f;


            CeilingRow probe = RunCeiling(shape, Math.Min(size, 2000), 2, Hulls.Unbounded, null,
                driven, frequency, speed, airDensity, fixture, flow);

            if (ceilings == null || ceilings.Count == 0)
            {

                ceilings = CeilingLadder(probe.RequiredSubsteps);
            }

            for (int i = 0; i < ceilings.Count; i++)
            {
                if (log != null) log("ceiling " + ceilings[i]);


                CeilingRow row = RunCeiling(shape, size, steps, ceilings[i], reference, driven,
                    frequency, speed, airDensity, fixture, flow);
                if (reference == null)
                {
                    reference = lastTemperatures;
                    referencePeak = row.PeakTemperature;
                }

                row.PeakError = row.PeakTemperature - referencePeak;
                rows.Add(row);
            }

            return rows;
        }

        public static readonly float[] Oversubscriptions = { 1.15f, 1.5f, 2f, 3f, 4.5f, 9f };


        public static List<int> CeilingLadder(float demand)
        {

            List<int> ceilings = new List<int>();
            ceilings.Add(Hulls.Unbounded);

            for (int i = 0; i < Oversubscriptions.Length; i++)
            {
                int ceiling = (int)Math.Round(demand / Oversubscriptions[i]);
                if (ceiling < 1) ceiling = 1;
                if (!ceilings.Contains(ceiling)) ceilings.Add(ceiling);
            }

            return ceilings;
        }


        private static CeilingRow RunCeiling(string shape, int size, int steps, int ceiling,
            float[] reference, bool driven, int frequency, float speed, float airDensity,
            string fixture, float flow)
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            if (frequency > 0) settings.Frequency = frequency;

            settings.MaxSubsteps = ceiling;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            ThermalSimulation simulation;
            WorstCases.Built built = null;

            if (fixture == CeilingFixtures.Plumbed)
            {
                built = WorstCases.Plumbed(shape, size, 8, settings);
                simulation = built.Simulation;
            }

            else if (fixture == CeilingFixtures.Rings)
            {
                built = WorstCases.HeatedRings(Math.Max(1, size / 10), Census.ProducerWatts, settings,
                    flow);
                simulation = built.Simulation;
            }

            else if (fixture == CeilingFixtures.Pressurised)
            {
                built = WorstCases.Pressurised(shape, size, settings);
                simulation = built.Simulation;
            }
            else
            {
                GridBuilder builder = GridBuilder.Large();
                builder.PlaceCensus(LoadShapes.Build(shape, size));


                simulation = new ThermalSimulation(settings, builder.Grid);
                for (int i = 0; i < builder.Placed.Count; i++)
                {
                    simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
                }
                simulation.RebuildAll();
            }

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int count = nodes.Count;

            if (driven)
            {
                Census.DriveCensus(simulation);
            }
            else
            {
                SeedSpread(simulation);
            }

            EnvironmentSample sample = Worlds.Flight(airDensity, speed);


            CeilingRow row = new CeilingRow();
            row.Ceiling = ceiling;
            row.StepSeconds = settings.StepSeconds;
            row.Nodes = count;
            row.Speed = speed;
            row.AirDensity = airDensity;
            row.Fixture = fixture;
            row.CoolantLoops = built != null ? built.CoolantLoops : 0;
            row.RoomsWithAir = built != null ? built.RoomsWithAir : 0;

            simulation.StepExact(1, sample);
            row.RequiredSubsteps = simulation.Solver.LastRequiredSubsteps;

            simulation.Work.Reset();
            Stopwatch watch = Stopwatch.StartNew();
            simulation.StepExact(steps, sample);
            watch.Stop();
            row.Milliseconds = watch.Elapsed.TotalMilliseconds;

            row.MeanGranted = simulation.Work.SolverSteps > 0
                ? (double)simulation.Work.SolverSubsteps / simulation.Work.SolverSteps
                : 0d;
            row.Bound = ceiling < row.RequiredSubsteps;
            row.Oversubscription = ceiling > 0 && row.Bound
                ? row.RequiredSubsteps / ceiling
                : 1f;

            float[] result = new float[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = nodes[i].Temperature;
                if (result[i] > row.PeakTemperature) row.PeakTemperature = result[i];
            }
            lastTemperatures = result;

            IList<CoolantLoop> loops = simulation.Solver.Loops;
            for (int l = 0; l < loops.Count; l++)
            {
                float hottest = loops[l].HottestSegment;
                if (hottest > row.PeakCoupledTemperature) row.PeakCoupledTemperature = hottest;
            }

            IList<RoomAirNode> air = simulation.Solver.RoomAir;
            for (int r = 0; r < air.Count; r++)
            {
                if (!air[r].HasAir) continue;
                if (air[r].Temperature > row.PeakCoupledTemperature)
                {
                    row.PeakCoupledTemperature = air[r].Temperature;
                }
            }

            if (reference != null && reference.Length == count)
            {
                double sumSquares = 0;
                for (int i = 0; i < count; i++)
                {
                    float error = Math.Abs(result[i] - reference[i]);
                    sumSquares += (double)error * error;
                    if (error > row.MaxError) row.MaxError = error;
                }

                row.RmsError = Math.Sqrt(sumSquares / count);
            }

            return row;
        }


        public static string CeilingTable(IList<CeilingRow> rows)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("ceiling".PadLeft(9))
              .Append("demanded".PadLeft(10))
              .Append("granted".PadLeft(10))
              .Append("over".PadLeft(7))
              .Append("ms".PadLeft(10))
              .Append("x granted".PadLeft(11))
              .Append("peak K".PadLeft(10))
              .Append("coupled K".PadLeft(12))
              .Append("peakErr".PadLeft(10))
              .Append("maxErr K".PadLeft(11))
              .Append("rmsErr K".PadLeft(11))
              .Append('\n');

            if (rows.Count > 0)
            {
                sb.Append("  step ")
                  .Append(rows[0].StepSeconds.ToString("n4"))
                  .Append(" s (Frequency ")
                  .Append((1f / rows[0].StepSeconds).ToString("n0"))
                  .Append("), air ")
                  .Append(rows[0].AirDensity.ToString("n2"))
                  .Append(" at ")
                  .Append(rows[0].Speed.ToString("n0"))
                  .Append(" m/s, ")
                  .Append(rows[0].Nodes.ToString("n0"))
                  .Append(" nodes, ")
                  .Append(rows[0].Fixture ?? CeilingFixtures.Census)
                  .Append(rows[0].CoolantLoops > 0 ? ", " + rows[0].CoolantLoops + " rings" : "")
                  .Append(rows[0].RoomsWithAir > 0 ? ", " + rows[0].RoomsWithAir + " rooms of air" : "")
                  .Append('\n');
            }

            double baseline = rows.Count > 0 ? rows[0].Milliseconds : 0;

            for (int i = 0; i < rows.Count; i++)
            {
                CeilingRow row = rows[i];

                sb.Append((row.Ceiling >= Hulls.Unbounded ? "granted" : row.Ceiling.ToString()).PadLeft(9))
                  .Append(row.RequiredSubsteps.ToString("n2").PadLeft(10))
                  .Append(row.MeanGranted.ToString("n2").PadLeft(10))
                  .Append((row.Bound ? row.Oversubscription.ToString("n2") + "x" : "-").PadLeft(7))
                  .Append(row.Milliseconds.ToString("n0").PadLeft(10))
                  .Append((row.Milliseconds <= 0 ? "-" : (baseline / row.Milliseconds).ToString("n2") + "x").PadLeft(11))
                  .Append(row.PeakTemperature.ToString("n1").PadLeft(10))
                  .Append((row.PeakCoupledTemperature > 0f
                      ? row.PeakCoupledTemperature.ToString("n1") : "-").PadLeft(12))
                  .Append((i == 0 ? "-" : row.PeakError.ToString("n3")).PadLeft(10))
                  .Append((i == 0 ? "-" : row.MaxError.ToString("n4")).PadLeft(11))
                  .Append((i == 0 ? "-" : row.RmsError.ToString("n4")).PadLeft(11))
                  .Append('\n');
            }

            return sb.ToString();
        }



        public static string Table(IList<ScaleRow> rows)
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("shape".PadRight(8))
              .Append("blocks".PadLeft(10))
              .Append("links".PadLeft(11))
              .Append("bbox".PadLeft(12))
              .Append("exposed".PadLeft(9))
              .Append("build ms".PadLeft(10))
              .Append("topo ms".PadLeft(10))
              .Append("rooms ms".PadLeft(10))
              .Append("expos ms".PadLeft(10))
              .Append("step ms".PadLeft(9))
              .Append("sub".PadLeft(5))
              .Append("tick ms".PadLeft(9))
              .Append("cap".PadLeft(5))
              .Append("ns/link".PadLeft(9))
              .Append("ms/simsec".PadLeft(11))
              .Append("+1 spike ms".PadLeft(13))
              .Append("settle".PadLeft(8))
              .Append("MB".PadLeft(8))
              .Append('\n');

            for (int i = 0; i < rows.Count; i++)
            {
                ScaleRow r = rows[i];
                sb.Append(r.Shape.PadRight(8))
                  .Append(r.Blocks.ToString("n0").PadLeft(10))
                  .Append(r.Links.ToString("n0").PadLeft(11))
                  .Append(r.BoundingVolume.ToString("n0").PadLeft(12))
                  .Append(Percent(r.ExposedBlocks, r.Blocks).PadLeft(9))
                  .Append(r.BuildMs.ToString("n0").PadLeft(10))
                  .Append(r.TopologyMs.ToString("n1").PadLeft(10))
                  .Append(r.RoomMapMs.ToString("n1").PadLeft(10))
                  .Append(r.ExposureMs.ToString("n1").PadLeft(10))
                  .Append(r.SolverStepMs.ToString("n2").PadLeft(9))
                  .Append(r.Substeps.ToString().PadLeft(5))
                  .Append(r.BoundedStepMs.ToString("n2").PadLeft(9))
                  .Append((r.SubstepBudget == int.MaxValue ? "-" : r.SubstepBudget.ToString()).PadLeft(5))
                  .Append(r.NsPerLinkVisit.ToString("n1").PadLeft(9))
                  .Append(r.MsPerSimulatedSecond.ToString("n1").PadLeft(11))
                  .Append(r.SpikeAfterOneBlockMs.ToString("n1").PadLeft(13))
                  .Append((r.SettleTicks + "t").PadLeft(8))
                  .Append(r.ResidentMb.ToString("n0").PadLeft(8))
                  .Append('\n');
            }

            return sb.ToString();
        }


        private static string Percent(int part, int whole)
        {
            if (whole == 0) return "-";
            return (100d * part / whole).ToString("n0") + "%";
        }


        public static string Csv(IList<ScaleRow> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("shape,blocks,links,boundingVolume,exposed,buildMs,topologyMs,roomMapMs,")
              .Append("exposureMs,solverStepMs,substeps,boundedStepMs,substepBudget,")
              .Append("nsPerLinkVisit,msPerSimulatedSecond,")
              .Append("spikeAfterOneBlockMs,settleTicks,settleTotalMs,residentMb\n");

            for (int i = 0; i < rows.Count; i++)
            {
                ScaleRow r = rows[i];
                sb.Append(r.Shape).Append(',')
                  .Append(r.Blocks).Append(',')
                  .Append(r.Links).Append(',')
                  .Append(r.BoundingVolume).Append(',')
                  .Append(r.ExposedBlocks).Append(',')
                  .Append(r.BuildMs.ToString("f3")).Append(',')
                  .Append(r.TopologyMs.ToString("f3")).Append(',')
                  .Append(r.RoomMapMs.ToString("f3")).Append(',')
                  .Append(r.ExposureMs.ToString("f3")).Append(',')
                  .Append(r.SolverStepMs.ToString("f4")).Append(',')
                  .Append(r.Substeps).Append(',')
                  .Append(r.BoundedStepMs.ToString("f4")).Append(',')
                  .Append(r.SubstepBudget == int.MaxValue ? -1 : r.SubstepBudget).Append(',')
                  .Append(r.NsPerLinkVisit.ToString("f3")).Append(',')
                  .Append(r.MsPerSimulatedSecond.ToString("f3")).Append(',')
                  .Append(r.SpikeAfterOneBlockMs.ToString("f3")).Append(',')
                  .Append(r.SettleTicks).Append(',')
                  .Append(r.SettleTotalMs.ToString("f3")).Append(',')
                  .Append(r.ResidentMb.ToString("f1")).Append('\n');
            }

            return sb.ToString();
        }
    }
}

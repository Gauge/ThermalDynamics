using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public class ReportRow
    {
        public string Section;
        public string Case;
        public string Metric;
        public double Value;
        public string Unit;

        public bool LowerIsBetter = true;

        public string Key
        {
            get { return Section + "/" + Case + "/" + Metric; }
        }
    }

    public static class PerformanceReport
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private class Feature
        {
            public string Name;
            public Action<HistoricalThermalSettings, bool> Set;
        }

        private static readonly Feature[] Features =
        {

            new Feature { Name = "conduction",   Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableConduction = on; } },

            new Feature { Name = "radiation",    Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableRadiation = on; } },

            new Feature { Name = "convection",   Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableConvection = on; } },

            new Feature { Name = "solar",        Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableSolarHeat = on; } },

            new Feature { Name = "self shadow",  Set = delegate (HistoricalThermalSettings s, bool on) { s.SolarSelfShadowing = on; } },

            new Feature { Name = "waste heat",   Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableWasteHeat = on; } },

            new Feature { Name = "heat sources", Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableHeatSources = on; } },

            new Feature { Name = "friction",     Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableFriction = on; } },

            new Feature { Name = "damage",       Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableDamage = on; } },

            new Feature { Name = "coolant loops",Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableCoolantLoops = on; } },

            new Feature { Name = "room air",     Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableRoomAir = on; } },

            new Feature { Name = "heat pumps",   Set = delegate (HistoricalThermalSettings s, bool on) { s.EnableHeatPumps = on; } },

            new Feature { Name = "conduction clamp", Set = delegate (HistoricalThermalSettings s, bool on) { s.ClampConductionOvershoot = on; } },

            new Feature { Name = "environment clamp", Set = delegate (HistoricalThermalSettings s, bool on) { s.ClampEnvironmentOvershoot = on; } },
        };

        private static readonly int[] Caps = { 0, 16, 8, 4, 2, 1 };


        private static EnvironmentSample Worst()
        {
            return Worlds.Flight(1f, 300f);
        }

        private static readonly string[] WorldNames = { "vacuum", "atmosphere", "flight" };


        private static EnvironmentSample World(string name)
        {
            if (name == "vacuum") return Worlds.Space(new Vector3(0.3f, 0.9f, 0.2f));
            if (name == "atmosphere") return Worlds.PlanetSurface(1f, timeOfDay: 0.35f, windSpeed: 40f);
            return Worlds.Flight(1f, 300f);
        }

        private static readonly string[] Shapes = { "ship", "cube", "truss" };


        public static int Repeats = 3;


        public static List<ReportRow> Run(string shape, int size, int ticks, IList<int> ladder,
            Action<string> log = null)
        {

            List<ReportRow> rows = new List<ReportRow>();

            Measure(Configure(0, true), shape, 1000, 4);

            if (log != null) log("machine");
            Environment(rows);
            NoiseFloor(rows, shape, size, ticks, log);

            if (log != null) log("ladder");
            Ladder(rows, shape, ladder, ticks, log);

            if (log != null) log("features");
            FeatureBreakdown(rows, shape, size, ticks, log);

            if (log != null) log("configurations");
            Configurations(rows, shape, size, ticks, log);

            if (log != null) log("overshoot clamp");
            OvershootClamp(rows, shape, size, ticks, log);

            if (log != null) log("diagnostics");
            Diagnostics(rows, shape, size, ticks, log);

            if (log != null) log("environments");
            Environments(rows, ticks, log);

            if (log != null) log("scenarios");
            Scenarios(rows, ticks, log);

            if (log != null) log("fleets");
            Fleets(rows, ticks, log);

            return rows;
        }


        private static void Environment(List<ReportRow> rows)
        {
            Add(rows, "machine", "host", "processors", System.Environment.ProcessorCount, "", false);
            Add(rows, "machine", "host", "64 bit", System.Environment.Is64BitProcess ? 1 : 0, "", false);


            GridBuilder hull = Hull("ship", 4000);

            double best = double.MaxValue;

            for (int r = 0; r < (Repeats < 1 ? 1 : Repeats); r++)
            {
                Stopwatch watch = Stopwatch.StartNew();

                ThermalSimulation calibration = Build(Configure(0, true), hull);
                LoadBenchmarks.SeedSpread(calibration);

                EnvironmentState state = EnvironmentSolver.Solve(

                    calibration.Settings, calibration.Planet, Worst());

                for (int i = 0; i < 20; i++) calibration.Solver.Step(calibration.Settings.StepSeconds, state);
                watch.Stop();

                if (watch.Elapsed.TotalMilliseconds < best) best = watch.Elapsed.TotalMilliseconds;
            }

            Add(rows, "machine", "calibration", "4k ship, 20 steps", best, "ms");
        }


        private static void NoiseFloor(List<ReportRow> rows, string shape, int size, int ticks,
            Action<string> log)
        {
            if (log != null) log("noise floor");


            HistoricalThermalSettings settings = Configure(0, true);

            double least = double.MaxValue;
            double most = 0;

            for (int i = 0; i < 5; i++)
            {

                double ms = Measure(settings, shape, size, ticks).StepMs;
                if (ms < least) least = ms;
                if (ms > most) most = ms;
            }

            Add(rows, "machine", "noise", "spread of five identical runs", most - least, "ms");
            Add(rows, "machine", "noise", "as a share of a step", least <= 0 ? 0 : (most - least) / least, "");
        }

        public struct BuiltHull
        {
            public double BuildMs;

            public int Builds;

            public ThermalSimulation Simulation;
        }


        public static BuiltHull RepeatBuild(HistoricalThermalSettings settings, GridBuilder hull)
        {

            BuiltHull built = new BuiltHull();
            built.BuildMs = double.MaxValue;

            int nodes = 0;
            int links = 0;

            for (int r = 0; r < (Repeats < 1 ? 1 : Repeats); r++)
            {
                Stopwatch build = Stopwatch.StartNew();

                ThermalSimulation candidate = Build(settings, hull);
                build.Stop();

                if (r == 0)
                {
                    nodes = candidate.Solver.Nodes.Count;
                    links = candidate.Solver.Links.Count;
                }
                else if (candidate.Solver.Nodes.Count != nodes
                    || candidate.Solver.Links.Count != links)
                {
                    throw new InvalidOperationException(
                        "repeat " + r + " built " + candidate.Solver.Nodes.Count + " nodes and "
                        + candidate.Solver.Links.Count + " links against " + nodes + " and " + links
                        + " the first time, so these are readings of different builds");
                }

                if (build.Elapsed.TotalMilliseconds < built.BuildMs)
                {
                    built.BuildMs = build.Elapsed.TotalMilliseconds;
                }

                built.Builds++;

                built.Simulation = candidate;
            }

            return built;
        }


        private static void Ladder(List<ReportRow> rows, string shape, IList<int> sizes, int ticks,
            Action<string> log)
        {
            for (int i = 0; i < sizes.Count; i++)
            {
                int size = sizes[i];
                if (log != null) log("  ladder " + size.ToString("n0"));


                HistoricalThermalSettings settings = Configure(0, true);

                GridBuilder hull = Hull(shape, size);


                BuiltHull built = RepeatBuild(settings, hull);
                ThermalSimulation simulation = built.Simulation;

                string name = size.ToString("n0");

                Add(rows, "ladder", name, "blocks", simulation.Solver.Nodes.Count, "");
                Add(rows, "ladder", name, "links", simulation.Solver.Links.Count, "");
                Add(rows, "ladder", name, "build", built.BuildMs, "ms");


                Sample sample = Measure(settings, shape, size, ticks, simulation);

                Add(rows, "ladder", name, "step", sample.StepMs, "ms");
                Add(rows, "ladder", name, "substeps granted", sample.Substeps, "", false);
                Add(rows, "ladder", name, "substeps demanded", sample.Demand, "", false);
                Add(rows, "ladder", name, "per simulated second", sample.MsPerSimulatedSecond, "ms");
                Add(rows, "ladder", name, "per element visit", sample.NsPerElement, "ns");
            }
        }


        private static void FeatureBreakdown(List<ReportRow> rows, string shape, int size, int ticks,
            Action<string> log)
        {

            Sample whole = Measure(Configure(0, true), shape, size, ticks);
            Add(rows, "features", "everything on", "step", whole.StepMs, "ms");


            Sample bare = Measure(Configure(0, false), shape, size, ticks);
            Add(rows, "features", "everything off", "step", bare.StepMs, "ms");

            for (int i = 0; i < Features.Length; i++)
            {
                Feature feature = Features[i];
                if (log != null) log("  feature " + feature.Name);


                HistoricalThermalSettings without = Configure(0, true);
                feature.Set(without, false);
                without.Derive();

                Sample removed = Measure(without, shape, size, ticks);


                HistoricalThermalSettings only = Configure(0, false);
                feature.Set(only, true);
                only.Derive();

                Sample alone = Measure(only, shape, size, ticks);

                Add(rows, "features", feature.Name, "marginal", whole.StepMs - removed.StepMs, "ms");
                Add(rows, "features", feature.Name, "isolated", alone.StepMs - bare.StepMs, "ms");

                Add(rows, "features", feature.Name, "demand without it", removed.Demand, "", false);
            }
        }


        private static void Configurations(List<ReportRow> rows, string shape, int size, int ticks,
            Action<string> log)
        {
            double[] stepMs = new double[Caps.Length];
            float[] substeps = new float[Caps.Length];

            for (int i = 0; i < Caps.Length; i++)
            {
                int cap = Caps[i];
                if (log != null) log("  cap " + cap);


                Sample sample = Measure(Configure(cap, true), shape, size, ticks);
                string name = cap == 0 ? "cap off" : "cap " + cap;

                stepMs[i] = sample.StepMs;
                substeps[i] = sample.Substeps;

                Add(rows, "substep cap", name, "step", sample.StepMs, "ms");
                Add(rows, "substep cap", name, "substeps", sample.Substeps, "", false);
                Add(rows, "substep cap", name, "per simulated second", sample.MsPerSimulatedSecond, "ms");
                Add(rows, "substep cap", name, "blocks raised", sample.Floored, "", false);
                Add(rows, "substep cap", name, "clamped", sample.Clamped ? 1 : 0, "", false);
            }

            StepShape(rows, shape, size, ticks, stepMs, substeps);
        }


        private static void StepShape(List<ReportRow> rows, string shape, int size, int ticks,
            double[] stepMs, float[] substeps)
        {

            int low = IndexOfCap(4);

            int high = IndexOfCap(16);
            if (low < 0 || high < 0) return;

            double span = substeps[high] - substeps[low];
            if (span <= 0d) return;

            double perSubstep = (stepMs[high] - stepMs[low]) / span;
            double fixedMs = stepMs[low] - (perSubstep * substeps[low]);


            int uncapped = IndexOfCap(0);
            double atDefault = uncapped >= 0 && stepMs[uncapped] > 0d
                ? 100d * fixedMs / stepMs[uncapped]
                : 0d;

            Add(rows, "step shape", "cap 4 to cap 16", "fixed per step", fixedMs, "ms");
            Add(rows, "step shape", "cap 4 to cap 16", "per substep", perSubstep, "ms");
            Add(rows, "step shape", "cap 4 to cap 16", "fixed share, uncapped", atDefault, "%");

            StepTerms(rows, shape, size, ticks, stepMs, perSubstep);
        }


        private static void StepTerms(List<ReportRow> rows, string shape, int size, int ticks,
            double[] stepMs, double perSubstep)
        {

            int one = IndexOfCap(1);
            if (one < 0) return;


            HistoricalThermalSettings settings = Configure(1, true);

            ThermalSimulation simulation = Build(settings, shape, size);
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);

            ThermalSolver solver = simulation.Solver;
            EnvironmentState state = EnvironmentSolver.Solve(settings, simulation.Planet, Worst());
            float step = settings.StepSeconds;

            for (int i = 0; i < 2; i++) solver.Step(step, state);

            long nodes = solver.Nodes.Count;
            int repeats = Repeats < 1 ? 1 : Repeats;

            double prepare = double.MaxValue;
            double publish = double.MaxValue;

            for (int r = 0; r < repeats; r++)
            {
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < ticks; i++) solver.RequiredSubsteps(step, state);
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds / ticks;
                if (ms < prepare) prepare = ms;

                double total = 0d;
                for (int i = 0; i < ticks; i++)
                {
                    solver.BeginStep(step, state);
                    long upTo = solver.StepWorkUnits - nodes;
                    while (solver.StepWorkRemaining > nodes) solver.AdvanceStep(upTo);

                    Stopwatch last = Stopwatch.StartNew();
                    while (!solver.AdvanceStep(long.MaxValue)) { }
                    last.Stop();
                    total += last.Elapsed.TotalMilliseconds;
                }

                ms = total / ticks;
                if (ms < publish) publish = ms;
            }

            double firstSubstep = stepMs[one] - prepare - publish;

            Add(rows, "step shape", "terms", "prologue and estimate", prepare, "ms");
            Add(rows, "step shape", "terms", "write-back", publish, "ms");
            Add(rows, "step shape", "terms", "first substep", firstSubstep, "ms");
            Add(rows, "step shape", "terms", "later substep", perSubstep, "ms");
            Add(rows, "step shape", "terms", "row fill, first substep only, clamp live",
                firstSubstep - perSubstep, "ms");
        }


        private static int IndexOfCap(int cap)
        {
            for (int i = 0; i < Caps.Length; i++)
            {
                if (Caps[i] == cap) return i;
            }
            return -1;
        }


        private static void OvershootClamp(List<ReportRow> rows, string shape, int size, int ticks,
            Action<string> log)
        {
            Regime(rows, "resolved", 4096, shape, size, ticks, log);
            Regime(rows, "refused", 4, shape, size, ticks, log);
        }


        private static void Diagnostics(List<ReportRow> rows, string shape, int size, int ticks,
            Action<string> log)
        {
            if (log != null) log("  diagnostics");

            bool restore = LoadBenchmarks.CollectDiagnostics;
            try
            {
                LoadBenchmarks.CollectDiagnostics = false;

                Sample off = Measure(Configure(0, true), shape, size, ticks);

                LoadBenchmarks.CollectDiagnostics = true;

                Sample on = Measure(Configure(0, true), shape, size, ticks);

                Add(rows, "diagnostics", "per-mechanism watts", "step, off", off.StepMs, "ms");
                Add(rows, "diagnostics", "per-mechanism watts", "step, on", on.StepMs, "ms");
                Add(rows, "diagnostics", "per-mechanism watts", "cost of being measured",
                    on.StepMs - off.StepMs, "ms");


                Sample all = Measure(Configure(0, true), shape, size, ticks,
                    null, null, true, everySubstep: true);

                Add(rows, "diagnostics", "per-mechanism watts", "step, every substep", all.StepMs, "ms");

                Add(rows, "diagnostics", "per-mechanism watts", "written, off",
                    off.DiagnosticsPublished ? 1 : 0, "", false);
                Add(rows, "diagnostics", "per-mechanism watts", "written, on",
                    on.DiagnosticsPublished ? 1 : 0, "", false);
                Add(rows, "diagnostics", "per-mechanism watts", "written, every substep",
                    all.DiagnosticsPublished ? 1 : 0, "", false);


                HistoricalThermalSettings single = Configure(0, true);
                single.MaxSubsteps = 1;
                single.Derive();


                Sample onceLast = Measure(single, shape, size, ticks);

                Sample onceEvery = Measure(single, shape, size, ticks, null, null, true, everySubstep: true);

                Add(rows, "diagnostics", "one substep", "step, last substep only", onceLast.StepMs, "ms");
                Add(rows, "diagnostics", "one substep", "step, every substep", onceEvery.StepMs, "ms");
            }
            finally
            {
                LoadBenchmarks.CollectDiagnostics = restore;
            }
        }


        private static void Regime(List<ReportRow> rows, string name, int maxSubsteps, string shape,
            int size, int ticks, Action<string> log)
        {
            if (log != null) log("  clamp " + name);


            HistoricalThermalSettings gated = Configure(0, true);
            gated.MaxSubsteps = maxSubsteps;
            gated.Derive();


            HistoricalThermalSettings ungated = Configure(0, true);
            ungated.MaxSubsteps = maxSubsteps;
            ungated.Derive();


            Sample with = Measure(gated, shape, size, ticks, null, null, true);

            Sample without = Measure(ungated, shape, size, ticks, null, null, false);

            Add(rows, "overshoot clamp", name, "step, gated", with.StepMs, "ms");
            Add(rows, "overshoot clamp", name, "step, always clamped", without.StepMs, "ms");
            Add(rows, "overshoot clamp", name, "substeps granted", with.Substeps, "", false);
            Add(rows, "overshoot clamp", name, "clamp live", with.ClampLive ? 1 : 0, "", false);

            double change = without.StepMs <= 0.0
                ? 0.0
                : 100.0 * (with.StepMs - without.StepMs) / without.StepMs;
            Add(rows, "overshoot clamp", name, "gate change", change, "%");
        }


        private static void Environments(List<ReportRow> rows, int ticks, Action<string> log)
        {
            const int Size = 8000;

            for (int s = 0; s < Shapes.Length; s++)
            {
                for (int w = 0; w < WorldNames.Length; w++)
                {
                    if (log != null) log("  " + Shapes[s] + " in " + WorldNames[w]);


                    Sample sample = Measure(Configure(0, true), Shapes[s], Size, ticks,

                        null, World(WorldNames[w]));

                    string name = Shapes[s] + " " + WorldNames[w];
                    Add(rows, "environments", name, "step", sample.StepMs, "ms");
                    Add(rows, "environments", name, "substeps demanded", sample.Demand, "", false);
                }
            }
        }


        private static void Scenarios(List<ReportRow> rows, int ticks, Action<string> log)
        {
            const int Size = 8000;

            Measured(rows, "plain", WorstCases.Hull("ship", Size, Configure(0, true)), ticks, log);
            Measured(rows, "pressurised", WorstCases.Pressurised("ship", Size, Configure(0, true)), ticks, log);
            Measured(rows, "plumbed", WorstCases.Plumbed("ship", Size, 8, Configure(0, true)), ticks, log);
            Measured(rows, "burning", WorstCases.Burning("ship", Size, Configure(0, true)), ticks, log);

            Measured(rows, "scorched", WorstCases.Scorched("ship", Size, Configure(0, true)), ticks, log);
        }


        private static void Measured(List<ReportRow> rows, string name, WorstCases.Built built,
            int ticks, Action<string> log)
        {
            if (log != null) log("  " + name + ": " + built);

            ThermalSimulation simulation = built.Simulation;
            EnvironmentState state = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, Worst());

            float step = simulation.Settings.StepSeconds;
            for (int i = 0; i < 2; i++) simulation.Solver.Step(step, state);

            double best = double.MaxValue;
            long overheats = 0;

            for (int r = 0; r < Math.Max(1, Repeats); r++)
            {
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < ticks; i++)
                {
                    simulation.Solver.Step(step, state);
                    overheats += simulation.Solver.Overheats.Count;
                }
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds / ticks;
                if (ms < best) best = ms;
            }

            Add(rows, "scenarios", name, "step", best, "ms");
            Add(rows, "scenarios", name, "substeps demanded", simulation.Solver.LastRequiredSubsteps, "", false);

            Add(rows, "scenarios", name, "rooms with air", built.RoomsWithAir, "", false);
            Add(rows, "scenarios", name, "coolant loops", built.CoolantLoops, "", false);
            Add(rows, "scenarios", name, "heat pumps", built.HeatPumps, "", false);
            Add(rows, "scenarios", name, "overheat events", overheats / Math.Max(1, Repeats), "", false);
        }


        private static void Fleets(List<ReportRow> rows, int ticks, Action<string> log)
        {
            const int Total = 8000;
            int[] counts = { 1, 10, 100 };

            for (int c = 0; c < counts.Length; c++)
            {
                if (log != null) log("  fleet of " + counts[c]);

                List<WorstCases.Built> fleet =
                    WorstCases.Fleet("ship", Total, counts[c], Configure(0, true));


                EnvironmentSample sample = Worst();
                WorstCases.StepFleet(fleet, sample, 2);

                int nodes = 0;
                for (int i = 0; i < fleet.Count; i++) nodes += fleet[i].Nodes;

                double best = double.MaxValue;
                for (int r = 0; r < Math.Max(1, Repeats); r++)
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    WorstCases.StepFleet(fleet, sample, ticks);
                    watch.Stop();

                    double ms = watch.Elapsed.TotalMilliseconds / ticks;
                    if (ms < best) best = ms;
                }

                string name = counts[c] + " grid" + (counts[c] == 1 ? "" : "s");
                Add(rows, "fleets", name, "step, whole fleet", best, "ms");
                Add(rows, "fleets", name, "blocks", nodes, "", false);
                Add(rows, "fleets", name, "blocks per grid", fleet.Count == 0 ? 0 : nodes / fleet.Count, "", false);
                Add(rows, "fleets", name, "per thousand blocks", nodes <= 0 ? 0 : best * 1000 / nodes, "ms");
            }
        }


        private class Sample
        {
            public double StepMs;
            public double MsPerSimulatedSecond;
            public double NsPerElement;
            public float Substeps;

            public float Demand;

            public int Floored;
            public bool Clamped;

            public bool ClampLive;

            public bool DiagnosticsPublished;
        }


        private static HistoricalThermalSettings Configure(int cap, bool featuresOn)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();

            if (!featuresOn)
            {
                for (int i = 0; i < Features.Length; i++) Features[i].Set(settings, false);

                settings.ClampConductionOvershoot = true;
                settings.ClampEnvironmentOvershoot = true;
                settings.EnableEnvironment = true;
            }

            settings.MaxSubstepsPerBlock = cap;
            return settings.Derive();
        }


        private static GridBuilder Hull(string shape, int size)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build(shape, size));
            return builder;
        }


        private static ThermalSimulation Build(HistoricalThermalSettings settings, string shape, int size)
        {
            return Build(settings, Hull(shape, size));
        }


        private static ThermalSimulation Build(HistoricalThermalSettings settings, GridBuilder builder)
        {
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            simulation.Solver.CollectDiagnostics = LoadBenchmarks.CollectDiagnostics;
            return simulation;
        }


        private static Sample Measure(HistoricalThermalSettings settings, string shape, int size, int ticks,
            ThermalSimulation prepared = null, EnvironmentSample? world = null, bool gateClamp = true,
            bool everySubstep = false)
        {

            ThermalSimulation simulation = prepared ?? Build(settings, shape, size);
            simulation.Solver.GateConductionClamp = gateClamp;
            simulation.Solver.DiagnosticsOnEverySubstep = everySubstep;
            LoadBenchmarks.SeedSpread(simulation);
            Census.DriveCensus(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(

                settings, simulation.Planet, world ?? Worst());

            float step = settings.StepSeconds;

            for (int i = 0; i < 2; i++) simulation.Solver.Step(step, state);

            double best = double.MaxValue;
            int repeats = Repeats < 1 ? 1 : Repeats;

            for (int r = 0; r < repeats; r++)
            {
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < ticks; i++) simulation.Solver.Step(step, state);
                watch.Stop();

                double ms = watch.Elapsed.TotalMilliseconds / ticks;
                if (ms < best) best = ms;
            }


            Sample sample = new Sample();
            sample.StepMs = best;
            sample.MsPerSimulatedSecond = sample.StepMs * settings.StepsPerSecond;
            sample.Substeps = simulation.Solver.LastSubsteps;
            sample.Demand = simulation.Solver.LastRequiredSubsteps;
            sample.Floored = simulation.Solver.FlooredNodes;
            sample.Clamped = simulation.Solver.LastStepWasClamped;
            sample.ClampLive = simulation.Solver.ConductionClampLive;

            IList<ThermalNode> written = simulation.Solver.Nodes;
            for (int i = 0; i < written.Count; i++)
            {
                ThermalNode node = written[i];
                if (node.LastRadiationWatts == 0f && node.LastConvectionWatts == 0f
                    && node.LastSolarWatts == 0f && node.LastFrictionWatts == 0f
                    && node.LastConductionWatts == 0f)
                {
                    continue;
                }

                sample.DiagnosticsPublished = true;
                break;
            }

            double elements = simulation.Solver.Nodes.Count + simulation.Solver.Links.Count;
            double visits = elements * Math.Max(1, sample.Substeps);
            sample.NsPerElement = visits <= 0 ? 0 : sample.StepMs * 1e6 / visits;

            return sample;
        }


        private static void Add(List<ReportRow> rows, string section, string name, string metric,
            double value, string unit, bool lowerIsBetter = true)
        {
            rows.Add(new ReportRow
            {
                Section = section,
                Case = name,
                Metric = metric,
                Value = value,
                Unit = unit,
                LowerIsBetter = lowerIsBetter,
            });
        }



        public static string Table(IList<ReportRow> rows)
        {

            StringBuilder sb = new StringBuilder();
            string section = null;

            for (int i = 0; i < rows.Count; i++)
            {
                ReportRow row = rows[i];

                if (row.Section != section)
                {
                    section = row.Section;
                    sb.Append('\n').Append(section).Append('\n');
                    sb.Append(new string('-', section.Length)).Append('\n');
                }

                sb.Append("  ").Append(row.Case.PadRight(22))
                  .Append(row.Metric.PadRight(24))
                  .Append(Format(row.Value).PadLeft(14))
                  .Append(' ').Append(row.Unit)
                  .Append('\n');
            }

            return sb.ToString();
        }


        public static string Csv(IList<ReportRow> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("section,case,metric,value,unit,lower_is_better\n");

            for (int i = 0; i < rows.Count; i++)
            {
                ReportRow row = rows[i];
                sb.Append(Quote(row.Section)).Append(',')
                  .Append(Quote(row.Case)).Append(',')
                  .Append(Quote(row.Metric)).Append(',')
                  .Append(row.Value.ToString("r", Invariant)).Append(',')
                  .Append(Quote(row.Unit)).Append(',')
                  .Append(row.LowerIsBetter ? 1 : 0).Append('\n');
            }

            return sb.ToString();
        }


        public static List<ReportRow> ParseCsv(string text)
        {

            List<ReportRow> rows = new List<ReportRow>();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;


                List<string> fields = SplitCsv(lines[i]);
                if (fields.Count < 6) continue;

                double value;
                if (!double.TryParse(fields[3], NumberStyles.Float, Invariant, out value)) continue;

                rows.Add(new ReportRow
                {
                    Section = fields[0],
                    Case = fields[1],
                    Metric = fields[2],
                    Value = value,
                    Unit = fields[4],
                    LowerIsBetter = fields[5] == "1",
                });
            }

            return rows;
        }


        public static string Compare(IList<ReportRow> baseline, IList<ReportRow> current,
            double threshold = 0.05)
        {
            Dictionary<string, ReportRow> before = new Dictionary<string, ReportRow>();
            for (int i = 0; i < baseline.Count; i++) before[baseline[i].Key] = baseline[i];


            double noise = NoiseOf(current);


            List<string> regressions = new List<string>();

            List<string> improvements = new List<string>();

            List<string> appeared = new List<string>();

            for (int i = 0; i < current.Count; i++)
            {
                ReportRow row = current[i];

                ReportRow old;
                if (!before.TryGetValue(row.Key, out old))
                {
                    appeared.Add(row.Key);
                    continue;
                }

                before.Remove(row.Key);

                if (Math.Abs(old.Value) < 1e-9) continue;

                double change = (row.Value - old.Value) / Math.Abs(old.Value);
                if (Math.Abs(change) < threshold) continue;

                bool underNoise = noise > 0
                    && Math.Abs(old.Value) < noise
                    && string.Equals(row.Unit, "ms", StringComparison.Ordinal);

                string line = "  " + row.Key.PadRight(52)

                    + Format(old.Value).PadLeft(12) + " ->"

                    + Format(row.Value).PadLeft(12) + "  "
                    + (change > 0 ? "+" : "") + (100 * change).ToString("n1", Invariant) + "%"
                    + (underNoise ? "   (was inside the noise floor)" : "");

                bool worse = row.LowerIsBetter && change > 0 && !underNoise;
                if (worse) regressions.Add(line);
                else improvements.Add(line);
            }


            StringBuilder sb = new StringBuilder();

            sb.Append("\nregressions (").Append(regressions.Count).Append(")\n");
            if (regressions.Count == 0) sb.Append("  none beyond ")
                .Append((100 * threshold).ToString("n0", Invariant)).Append("%\n");
            for (int i = 0; i < regressions.Count; i++) sb.Append(regressions[i]).Append('\n');

            sb.Append("\nimprovements and neutral moves (").Append(improvements.Count).Append(")\n");
            for (int i = 0; i < improvements.Count; i++) sb.Append(improvements[i]).Append('\n');

            if (appeared.Count > 0)
            {
                sb.Append("\nnew figures, not in the baseline (").Append(appeared.Count).Append(")\n");
                for (int i = 0; i < appeared.Count; i++) sb.Append("  ").Append(appeared[i]).Append('\n');
            }

            if (before.Count > 0)
            {
                sb.Append("\ngone since the baseline (").Append(before.Count).Append(")\n");
                foreach (string key in before.Keys) sb.Append("  ").Append(key).Append('\n');
            }

            return sb.ToString();
        }


        private static double NoiseOf(IList<ReportRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Section == "machine" && rows[i].Case == "noise"
                    && rows[i].Metric == "spread of five identical runs")
                {
                    return rows[i].Value;
                }
            }

            return 0;
        }


        private static string Format(double value)
        {
            if (value == Math.Floor(value) && Math.Abs(value) < 1e9)
            {
                return value.ToString("n0", Invariant);
            }

            return Math.Abs(value) < 10
                ? value.ToString("n4", Invariant)
                : value.ToString("n2", Invariant);
        }


        private static string Quote(string value)
        {
            return CsvLine.Text(value);
        }


        private static List<string> SplitCsv(string line)
        {
            return CsvLine.Split(line);
        }
    }
}

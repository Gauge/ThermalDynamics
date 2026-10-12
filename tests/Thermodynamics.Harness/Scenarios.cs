using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public class ScenarioResult
    {
        public string Name;
        public string Summary;
        public ScenarioRunner Runner;
        public string Csv;
    }

    public static class Scenarios
    {
        public static readonly string[] Names = new string[]
        {
            "vacuum-soak",
            "reactor",
            "atmosphere",
            "daynight",
            "reentry",
            "coolant",
            "sealed-room",
            "meltdown",
            "radiator",
            "airlock",
            "coolant-failure",
            "welding",
            "first-room",
            "stiff",
            "units",
            "perf",
            "capital",
            "fleet",
            "interior",
            "solver",
            "self-shadow",
            "shadow-cost",
            "weather",
            "underground",
            "cooling-plant",
            "loop-faults",
            "loop-dry",
            "heatpump-backwards",
            "heatpump-limits",
            "cooling-runaway",
            "loop-stiffness",
            "loop-layout",
            "air-conditioning",
            "station",
        };


        public static ScenarioResult Run(string name)
        {
            switch (name)
            {

                case "vacuum-soak": return VacuumSoak();

                case "reactor": return Reactor();

                case "atmosphere": return Atmosphere();

                case "daynight": return DayNight();

                case "reentry": return Reentry();

                case "coolant": return Coolant();

                case "sealed-room": return SealedRoom();

                case "meltdown": return Meltdown();

                case "radiator": return Radiator();

                case "airlock": return Airlock();

                case "coolant-failure": return CoolantFailure();

                case "welding": return Welding();

                case "first-room": return FirstRoom();

                case "stiff": return Stiff();

                case "units": return Units();

                case "perf": return Performance();

                case "capital": return Capital();

                case "fleet": return Fleet();

                case "interior": return Interior();

                case "solver": return Solver();

                case "self-shadow": return SelfShadow();

                case "shadow-cost": return ShadowCost();

                case "weather": return Weather();

                case "underground": return Underground();

                case "cooling-plant": return CoolingPlant();

                case "loop-faults": return LoopFaults();

                case "loop-dry": return LoopDry();

                case "heatpump-backwards": return HeatPumpBackwards();

                case "heatpump-limits": return HeatPumpLimits();

                case "cooling-runaway": return CoolingRunaway();

                case "loop-stiffness": return LoopStiffness();

                case "loop-layout": return LoopLayout();

                case "air-conditioning": return AirConditioning();

                case "station": return Station();
                default:
                    throw new ArgumentException("Unknown scenario: " + name);
            }
        }


        public static ScenarioResult SelfShadow()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.SolarSelfShadowing = true;


            GridBuilder builder = Slab();
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;

            EnvironmentSample sun = Worlds.Space(new Vector3(0.9004f, 0.1619f, -0.4038f));


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => sun;
            runner.Track("sunward-face", simulation.Solver.GetNodeAt(new Vector3I(3, 3, 1)).Block);
            runner.Track("recess-floor", simulation.Solver.GetNodeAt(new Vector3I(2, 3, 1)).Block);
            runner.Track("shaded-flank", simulation.Solver.GetNodeAt(new Vector3I(0, 0, 1)).Block);
            runner.Run(1800f, 300f);

            SunShadowMap shadow = simulation.Solver.SunShadow;


            float sunward = LitShare(simulation, shadow, Face.Right);

            float top = LitShare(simulation, shadow, Face.Up);

            float flank = LitShare(simulation, shadow, Face.Forward);


            HistoricalThermalSettings cheap = new HistoricalThermalSettings();
            cheap.SolarSelfShadowing = false;


            ThermalSimulation plain = Slab().BuildSimulation(cheap, 293.15f);
            plain.Solver.CollectDiagnostics = true;


            ScenarioRunner cheapRunner = new ScenarioRunner(plain);
            cheapRunner.Environment = t => sun;
            cheapRunner.Run(1800f, 300f);

            return Result("self-shadow", runner,
                "Solid slab in sunlight, sun over the +X flank. Exposed faces lit: sunward "

                + Pct(sunward) + ", top " + Pct(top) + ", flank " + Pct(flank)

                + ", recess floor " + Pct(RecessShare(simulation, shadow))
                + ". Shadowed air cells: " + shadow.ShadowedCount

                + ". Grid solar with self-shadowing " + W(TotalSolar(simulation))

                + " against " + W(TotalSolar(plain)) + " without. Hottest "

                + C(runner.Final.HottestTemperature) + " against "

                + C(cheapRunner.Final.HottestTemperature) + ".");
        }


        public static ScenarioResult ShadowCost()
        {
            const int side = 20;

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(side, side, side));

            Vector3 sun = Vector3.Normalize(new Vector3(0.9004f, 0.1619f, -0.4038f));
            EnvironmentSample sample = Worlds.Space(sun);


            double cheap = StepCost(builder, false, sample);

            double shadowed = StepCost(builder, true, sample);

            ThermalSimulation solid = builder.BuildSimulation(Shadowing(true), 293.15f);
            solid.Update(1f / 60f, sample);

            PassCost solidPass = MeasurePass(solid, sun);

            GridBuilder hull = GridBuilder.Large();
            hull.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(30, 20, 20));
            for (int y = 4; y < 20; y += 6)
            {
                hull.Fill(Catalog.LightArmor(), new Vector3I(1, y, 1), new Vector3I(29, y + 1, 19));
            }

            ThermalSimulation ship = hull.BuildSimulation(Shadowing(true), 293.15f);
            ship.Update(1f / 60f, sample);

            PassCost shipPass = MeasurePass(ship, sun);

            int budget = solid.Solver.SunShadowBudget;

            return Result("shadow-cost", new ScenarioRunner(solid),
                side + "^3 solid grid, " + solid.Solver.Nodes.Count + " blocks: step with "

                + "self-shadowing off " + Ms(cheap) + ", on " + Ms(shadowed)
                + " (" + Overhead(cheap, shadowed) + "). One full pass " + solidPass.Cells

                + " air cells in " + Ms(solidPass.Milliseconds) + ", "

                + Slices(solidPass, budget) + ". A 30x20x20 hull with decks, "
                + ship.Solver.Nodes.Count + " blocks: pass " + shipPass.Cells + " air cells in "

                + Ms(shipPass.Milliseconds) + ", " + Slices(shipPass, budget)
                + ". A pass runs when the sun moves 2 degrees, and never between.");
        }

        private struct PassCost
        {
            public int Cells;
            public double Milliseconds;
        }


        private static PassCost MeasurePass(ThermalSimulation simulation, Vector3 sun)
        {
            SunShadowMap map = simulation.Solver.SunShadow;


            PassCost best = new PassCost();
            best.Milliseconds = double.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                Stopwatch pass = Stopwatch.StartNew();
                map.Restart(simulation.Grid, sun);
                int cells = map.PendingCells;
                map.RunToCompletion();
                pass.Stop();

                if (pass.Elapsed.TotalMilliseconds < best.Milliseconds)
                {
                    best.Milliseconds = pass.Elapsed.TotalMilliseconds;
                    best.Cells = cells;
                }
            }

            return best;
        }


        private static string Slices(PassCost pass, int budget)
        {
            int slices = Math.Max(1, (pass.Cells + budget - 1) / budget);
            return slices + " slices of " + budget + " at " + Ms(pass.Milliseconds / slices) + " each";
        }


        private static HistoricalThermalSettings Shadowing(bool on)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.SolarSelfShadowing = on;
            return settings;
        }


        private static double StepCost(GridBuilder builder, bool shadowing, EnvironmentSample sample)
        {
            double best = double.MaxValue;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                ThermalSimulation simulation = builder.BuildSimulation(Shadowing(shadowing), 293.15f);

                for (int i = 0; i < 60; i++) simulation.Update(1f / 60f, sample);

                const int measured = 400;
                Stopwatch run = Stopwatch.StartNew();
                for (int i = 0; i < measured; i++) simulation.Update(1f / 60f, sample);
                run.Stop();

                double perStep = run.Elapsed.TotalMilliseconds / measured;
                if (perStep < best) best = perStep;
            }

            return best;
        }


        private static string Ms(double milliseconds)
        {
            return milliseconds.ToString("n3") + " ms";
        }


        private static string Overhead(double baseline, double measured)
        {
            if (baseline <= 0) return "n/a";

            double percent = ((measured / baseline) - 1d) * 100d;
            return (percent >= 0 ? "+" : "") + percent.ToString("n1") + "%";
        }


        private static GridBuilder Slab()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 7, 4));

            for (int y = 2; y < 5; y++)
            {
                for (int z = 1; z < 3; z++)
                {
                    builder.Remove(new Vector3I(0, y, z));
                    builder.Remove(new Vector3I(1, y, z));
                }
            }

            return builder;
        }


        private static float LitShare(ThermalSimulation simulation, SunShadowMap shadow, int face)
        {
            int exposed = 0;
            float lit = 0f;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                int cells = nodes[i].GetExposedFaces(face);
                if (cells == 0) continue;

                exposed += cells;
                lit += cells * shadow.FaceLitFraction(nodes[i].Block, face);
            }

            return exposed == 0 ? 0f : lit / exposed;
        }


        private static float RecessShare(ThermalSimulation simulation, SunShadowMap shadow)
        {
            int cells = 0;
            float lit = 0f;

            for (int y = 2; y < 5; y++)
            {
                for (int z = 1; z < 3; z++)
                {
                    ThermalNode node = simulation.Solver.GetNodeAt(new Vector3I(2, y, z));
                    if (node == null) continue;

                    cells++;
                    lit += shadow.FaceLitFraction(node.Block, Face.Left);
                }
            }

            return cells == 0 ? 0f : lit / cells;
        }


        private static float TotalSolar(ThermalSimulation simulation)
        {
            float total = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++) total += nodes[i].LastSolarWatts;
            return total;
        }


        private static string Pct(float fraction)
        {
            return (fraction * 100f).ToString("n0") + "%";
        }


        private static string W(float watts)
        {
            return watts >= 1000f
                ? (watts / 1000f).ToString("n1") + " kW"
                : watts.ToString("n0") + " W";
        }


        public static ScenarioResult VacuumSoak()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 800f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("block", builder.Placed[0]);
            runner.Run(3600f, 300f);

            return Result("vacuum-soak", runner,
                "One heavy armour block at 800 K radiating into shadow. Ends at "

                + C(runner.Final.HottestTemperature) + " after one hour.");
        }


        public static ScenarioResult Reactor()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), new Vector3I(-2, -2, -2), new Vector3I(3, 3, 3));

            BlockInstance centre = builder.Grid.GetAtCell(Vector3I.Zero);
            builder.Grid.Remove(centre);
            builder.Placed.Remove(centre);
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(15f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", builder.Last);
            runner.Track("hull", builder.Grid.GetAtCell(new Vector3I(2, 0, 0)));
            runner.Run(7200f, 600f);

            return Result("reactor", runner,
                "15 MW reactor at the centre of a 5x5x5 light armour cube in shadow. Reactor reaches "

                + C(runner.Final.Tracked["reactor"]) + ", hull "

                + C(runner.Final.Tracked["hull"]) + ".");
        }


        public static ScenarioResult Atmosphere()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), new Vector3I(-2, -2, -2), new Vector3I(3, 3, 3));

            BlockInstance centre = builder.Grid.GetAtCell(Vector3I.Zero);
            builder.Grid.Remove(centre);
            builder.Placed.Remove(centre);
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(15f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.PlanetSurface(1f, 0.25f);
            runner.Track("reactor", builder.Last);
            runner.Track("hull", builder.Grid.GetAtCell(new Vector3I(2, 0, 0)));
            runner.Run(7200f, 600f);

            return Result("atmosphere", runner,
                "The same reactor cube at sea level. Reactor settles at "

                + C(runner.Final.Tracked["reactor"]) + " against "

                + C(runner.Final.AmbientTemperature) + " ambient.");
        }


        public static ScenarioResult DayNight()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 250f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            float dayLength = 7200f;
            runner.Environment = t => Worlds.PlanetSurface(0f, (t / dayLength) % 1f);
            runner.Track("plate", builder.Placed[0]);
            runner.Run(dayLength * 2f, dayLength / 24f);

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < runner.Samples.Count; i++)
            {
                float value = runner.Samples[i].Tracked["plate"];
                if (value < min) min = value;
                if (value > max) max = value;
            }

            return Result("daynight", runner,
                "Airless 3x1x3 plate over two rotations. Swings between "

                + C(min) + " and " + C(max) + ".");
        }


        public static ScenarioResult Reentry()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 1));

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Flight(0.8f, 300f);
            runner.Track("nose", builder.Placed[0]);
            runner.Run(600f, 60f);

            return Result("reentry",
                runner,
                "3x3 heavy armour face into 300 m/s of 0.8 density air. Nose reaches "

                + C(runner.Final.Tracked["nose"]) + ".");
        }


        public static ScenarioResult Coolant()
        {
            GridBuilder builder = GridBuilder.Large();

            List<Vector3I> ring = PipeFitter.RectangleXZ(Vector3I.Zero, 4, 2);
            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;
            sinks[2] = Vector3I.Up;

            List<BlockInstance> pipes = PipeFitter.BuildRing(builder, ring, 5, sinks);

            builder.Place(Catalog.Reactor(), new Vector3I(1, -1, 0))
                   .Wasting(1250000f);
            BlockInstance reactor = builder.Last;

            builder.Place(Catalog.LightArmor(), new Vector3I(2, 1, 0));
            BlockInstance sink = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", reactor);
            runner.Track("sink-block", sink);
            if (simulation.Solver.Loops.Count > 0)
            {
                runner.TrackLoop("coolant", simulation.Solver.Loops[0]);
            }
            runner.Run(3600f, 300f);

            string found = simulation.Solver.Loops.Count > 0
                ? simulation.Solver.Loops.Count + " loop of " + simulation.Solver.Loops[0].PipeCount + " pipes, "
                  + simulation.Solver.Loops[0].Links.Count + " links"

                : "NO CLOSED LOOP (" + pipes.Count + " pipes placed)";

            return Result("coolant", runner,
                "Pumped coolant ring between a 1.25 MW source and a plain armour block: " + found

                + ". Reactor " + C(runner.Final.Tracked["reactor"])
                + ", coolant " + (runner.Final.Tracked.ContainsKey("coolant") ? C(runner.Final.Tracked["coolant"]) : "n/a")

                + ", sink block " + C(runner.Final.Tracked["sink-block"]) + ".");
        }


        public static ScenarioResult SealedRoom()
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Wasting(500000f);
            BlockInstance reactor = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ThermalNode interior = simulation.Solver.GetNode(reactor);
            ThermalNode shell = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("interior-reactor", reactor);
            runner.Track("shell", shell.Block);
            runner.Run(3600f, 600f);

            return Result("sealed-room", runner,
                "A 500 kW source sealed inside a 3x3x3 hull. Its exposed faces: "
                + interior.TotalExposedFaces + " (expected 0), conduction links: "
                + interior.LinkCount + ", shell exposed faces: "
                + shell.TotalExposedFaces + ". Reactor "

                + C(runner.Final.Tracked["interior-reactor"]) + ", shell "

                + C(runner.Final.Tracked["shell"]) + ".");
        }


        public static ScenarioResult Meltdown()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(300f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            float totalDamage = 0f;
            float timeToCritical = -1f;


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", builder.Placed[0]);
            runner.AfterStep = s =>
            {
                for (int d = 0; d < s.Overheats.Count; d++)
                {
                    totalDamage += s.Overheats[d].Damage;
                }
                if (timeToCritical < 0f && s.Overheats.Count > 0)
                {
                    timeToCritical = runner.ElapsedSeconds;
                }
            };

            runner.Run(600f, 60f);

            return Result("meltdown", runner,
                "300 MW into a single unshielded reactor for 10 minutes: "

                + C(simulation.Solver.HottestNode().Temperature) + ", critical after "
                + (timeToCritical < 0f ? "never" : timeToCritical.ToString("n1") + " s")
                + ", cumulative damage " + totalDamage.ToString("n0") + ".");
        }


        public static ScenarioResult Performance()
        {
            const int side = 20;

            Stopwatch build = Stopwatch.StartNew();
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(side, side, side));
            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            build.Stop();

            int blocks = simulation.Solver.Nodes.Count;
            int links = simulation.Solver.Links.Count;

            EnvironmentSample environment = Worlds.Shadow();
            EnvironmentState state = EnvironmentSolver.Solve(simulation.Settings, simulation.Planet, environment);

            for (int i = 0; i < 10; i++) simulation.Solver.Step(simulation.Settings.StepSeconds, state);

            Stopwatch run = Stopwatch.StartNew();
            const int measured = 200;
            for (int i = 0; i < measured; i++)
            {
                simulation.Solver.Step(simulation.Settings.StepSeconds, state);
            }
            run.Stop();

            double perStepMs = run.Elapsed.TotalMilliseconds / measured;

            const int weldCount = 400;

            Stopwatch coalesced = Stopwatch.StartNew();

            GridModel coalescedGrid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation coalescedSim = new ThermalSimulation(new HistoricalThermalSettings(), coalescedGrid);
            for (int i = 0; i < weldCount; i++)
            {
                coalescedSim.AddBlock(new BlockInstance(Catalog.LightArmor(), CellFor(i), BlockOrientation.Identity));
            }
            coalescedSim.Update(1f / 60f, Worlds.Shadow());
            while (coalescedSim.HasPendingWork) coalescedSim.Update(1f / 60f, Worlds.Shadow());
            coalesced.Stop();

            Stopwatch perBlock = Stopwatch.StartNew();

            GridModel eagerGrid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation eagerSim = new ThermalSimulation(new HistoricalThermalSettings(), eagerGrid);
            for (int i = 0; i < weldCount; i++)
            {
                eagerSim.AddBlock(new BlockInstance(Catalog.LightArmor(), CellFor(i), BlockOrientation.Identity));
                eagerSim.Rooms.RequestRestart(eagerGrid);
                eagerSim.Rooms.RunToCompletion();
            }
            perBlock.Stop();


            StringBuilder sb = new StringBuilder();
            sb.Append(blocks.ToString("n0")).Append(" blocks, ")
              .Append(links.ToString("n0")).Append(" conduction links. Build+map ")
              .Append(build.ElapsedMilliseconds).Append(" ms. Solver ")
              .Append(perStepMs.ToString("n4")).Append(" ms/step (")
              .Append((perStepMs * simulation.Settings.StepsPerSecond).ToString("n3"))
              .Append(" ms per simulated second). Welding ").Append(weldCount)
              .Append(" blocks: ").Append(coalesced.ElapsedMilliseconds)
              .Append(" ms coalesced vs ").Append(perBlock.ElapsedMilliseconds)
              .Append(" ms remapping per block.");


            ScenarioResult result = new ScenarioResult();
            result.Name = "perf";
            result.Summary = sb.ToString();
            result.Csv = string.Empty;
            return result;
        }


        private static Vector3I CellFor(int index)
        {
            return new Vector3I(index % 10, (index / 10) % 10, index / 100);
        }



        public static ScenarioResult Radiator()
        {

            float bare = ReactorHull(RadiatorPlacement.None);

            float flush = ReactorHull(RadiatorPlacement.Flush);

            float clear = ReactorHull(RadiatorPlacement.Clear);


            GridBuilder builder = HullBuilder(RadiatorPlacement.Clear);
            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", simulation.Grid.GetAtCell(new Vector3I(1, 1, 1)));
            runner.Run(3600f, 300f);

            return Result("radiator", runner,

                "500 kW into a 3x3x3 hull in shadow. The source settles at " + C(bare)

                + " bare, " + C(flush) + " with panels bolted flat against the hull, and "

                + C(clear) + " with panels standing clear on booms.");
        }

        private enum RadiatorPlacement
        {
            None,
            Flush,
            Clear
        }


        private static GridBuilder HullBuilder(RadiatorPlacement placement)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            BlockInstance centre = builder.Grid.GetAtCell(new Vector3I(1, 1, 1));
            builder.Grid.Remove(centre);
            builder.Placed.Remove(centre);
            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1))
                   .Wasting(500000f);

            if (placement == RadiatorPlacement.Flush)
            {
                builder.Place(Catalog.Radiator(), new Vector3I(-1, 0, 0));
                builder.Place(Catalog.Radiator(), new Vector3I(3, 0, 0));
            }

            else if (placement == RadiatorPlacement.Clear)
            {
                builder.Place(Catalog.LightArmor(), new Vector3I(-1, 1, 1));
                builder.Place(Catalog.Radiator(), new Vector3I(-2, 0, 0));
                builder.Place(Catalog.LightArmor(), new Vector3I(3, 1, 1));
                builder.Place(Catalog.Radiator(), new Vector3I(4, 0, 0));
            }

            return builder;
        }


        private static float ReactorHull(RadiatorPlacement placement)
        {

            GridBuilder builder = HullBuilder(placement);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(7200f, 3600f);

            return simulation.Solver.GetNodeAt(new Vector3I(1, 1, 1)).Temperature;
        }


        public static ScenarioResult Airlock()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));

            BlockInstance wall = builder.Grid.GetAtCell(new Vector3I(2, 2, 0));
            builder.Grid.Remove(wall);
            builder.Placed.Remove(wall);

            builder.Place(Catalog.AirtightDoor(), new Vector3I(2, 2, 0));
            BlockInstance door = builder.Last;

            builder.Place(Catalog.Reactor(), new Vector3I(2, 1, 2))
                   .Wasting(250000f);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            ThermalNode interior = simulation.Solver.GetNodeAt(new Vector3I(2, 1, 2));


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("interior", simulation.Grid.GetAtCell(new Vector3I(2, 1, 2)));
            runner.Run(900f, 300f);

            int sealedFaces = interior.TotalExposedFaces;
            float sealedTemperature = interior.Temperature;

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);
            simulation.Rooms.RunToCompletion();
            simulation.Solver.RefreshExposure(simulation.Rooms.Map);

            runner.Run(900f, 300f);

            return Result("airlock", runner,

                "A 250 kW source inside a sealed 5x5x5 shell reaches " + C(sealedTemperature)
                + " with " + sealedFaces + " exposed faces. Opening the door leaves it with "

                + interior.TotalExposedFaces + " and it ends at " + C(interior.Temperature) + ".");
        }


        public static ScenarioResult CoolantFailure()
        {
            GridBuilder builder = GridBuilder.Large();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            List<BlockInstance> ring = PipeFitter.BuildRing(
                builder, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3), -1, sinks);

            builder.Place(Catalog.Reactor(), new Vector3I(1, -1, 0))
                   .Wasting(1250000f);
            BlockInstance reactor = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", reactor);
            runner.Run(1800f, 300f);

            float cooled = simulation.Solver.GetNode(reactor).Temperature;
            int loopsBefore = simulation.Solver.Loops.Count;

            BlockInstance pump = null;
            for (int i = 0; i < ring.Count; i++)
            {
                if (ring[i].Model.Coolant != null && ring[i].Model.Coolant.IsPump) pump = ring[i];
            }
            if (pump != null) simulation.RemoveBlock(pump);

            simulation.Update(1f / 6f, Worlds.Shadow());
            runner.Run(1800f, 300f);

            return Result("coolant-failure", runner,

                "A 1.25 MW source on a pumped ring holds at " + C(cooled) + " (" + loopsBefore
                + " loop). With the pump destroyed the ring stops circulating — "
                + simulation.Solver.Loops.Count + " loops — and the reactor ends at "

                + C(simulation.Solver.GetNode(reactor).Temperature) + ".");
        }


        public static ScenarioResult Welding()
        {
            float window = LabClock.Seconds(5f);

            float skeleton = WeldedRise(0.1f, window);

            float finished = WeldedRise(1f, window);

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.Reactor(), new Vector3I(0, 0, 1)).Wasting(250000f);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            ThermalNode node = simulation.Solver.GetNode(builder.Placed[0]);
            node.Block.Mass *= 0.1f;
            node.RefreshThermalMass();


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("skeleton", builder.Placed[0]);
            runner.Run(window, LabClock.Seconds(1f));

            node.Block.Mass *= 10f;
            node.RefreshThermalMass();
            runner.Run(LabClock.Seconds(55f), LabClock.Seconds(5f));

            return Result("welding", runner,
                "Five seconds of a 250 kW source next to one heavy armour block. At a tenth of its "

                + "mass the block reaches " + C(skeleton) + "; fully welded it is still at "

                + C(finished) + ". The tracked run welds it up after those five seconds and "

                + "settles at " + C(runner.Final.Tracked["skeleton"]) + ".");
        }


        public static ScenarioResult FirstRoom()
        {

            GridModel grid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), grid);

            BlockModel armour = Catalog.LightArmor();
            BlockModel doorModel = Catalog.SlideDoor();
            BlockInstance door = null;

            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;


                        Vector3I cell = new Vector3I(x, y, z);

                        bool isDoor = cell == new Vector3I(0, 0, -1);


                        BlockInstance block = new BlockInstance(
                            isDoor ? doorModel : armour, cell, BlockOrientation.Identity);
                        if (isDoor) door = block;

                        simulation.AddBlock(block);

                        simulation.Update(10f / 60f, Worlds.Shadow());
                    }
                }
            }


            BlockInstance reactor = new BlockInstance(Catalog.Reactor(), new Vector3I(0, 0, 2), BlockOrientation.Identity);
            simulation.AddBlock(reactor);
            reactor.PowerProducedWatts = 0.3f * ThermalConstants.MegawattsToWatts;

            while (simulation.HasPendingWork) simulation.Update(10f / 60f, Worlds.Shadow());

            RoomAudit closed = simulation.AuditRooms();

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);
            simulation.Rooms.RunToCompletion();
            RoomAudit opened = simulation.AuditRooms();

            door.IsSealedByDoorState = true;
            simulation.RefreshBlockSealing(door);
            simulation.Rooms.RunToCompletion();
            simulation.Solver.RefreshExposure(simulation.Rooms.Map);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", reactor);
            runner.Run(300f, 60f);

            return Result("first-room", runner,
                "A 3x3x3 shell with a door, welded a block at a time, maps as "

                + closed.RoomCount + " sealed room over " + closed.SearchVolume + " search cells ("
                + closed.ExternalCells + " external, " + closed.SolidCells + " structure, "
                + closed.RoomCells + " room, " + closed.OpenBlockCells + " block cells outdoors). "
                + "Opening the door leaves " + opened.RoomCount + " rooms and puts "
                + opened.OpenBlockCells + " block cells outdoors. The reactor bolted to the outside ends at "

                + C(reactor.PowerProducedWatts > 0 ? runner.Final.Tracked["reactor"] : 0f) + ".");
        }


        private static float WeldedRise(float massFraction, float seconds)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.Reactor(), new Vector3I(0, 0, 1)).Wasting(250000f);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ThermalNode node = simulation.Solver.GetNode(builder.Placed[0]);
            node.Block.Mass *= massFraction;
            node.RefreshThermalMass();


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(seconds, seconds);

            return node.Temperature;
        }


        public static ScenarioResult Stiff()
        {

            StringBuilder report = new StringBuilder();
            ScenarioRunner last = null;

            int[] frequencies = new int[] { 1, 4, 16 };
            for (int f = 0; f < frequencies.Length; f++)
            {

                HistoricalThermalSettings settings = new HistoricalThermalSettings();
                settings.Frequency = frequencies[f];
                settings.Derive();

                GridBuilder builder = GridBuilder.Large();
                builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

                BlockThermalProperties light = Catalog.DefaultThermal();
                BlockModel feather = BlockModel.Solid("Interior", Vector3I.One, 20f, light);
                builder.Place(feather, new Vector3I(3, 0, 0));

                ThermalSimulation simulation = builder.BuildSimulation(settings, 900f);


                ScenarioRunner runner = new ScenarioRunner(simulation);
                runner.Environment = t => Worlds.Shadow();
                runner.Track("feather", builder.Last);
                runner.Run(600f, 150f);

                int worst = 0;
                int clamped = 0;
                for (int i = 0; i < runner.Samples.Count; i++)
                {
                    if (runner.Samples[i].Substeps > worst) worst = runner.Samples[i].Substeps;
                }
                if (simulation.Solver.LastStepWasClamped) clamped++;

                report.Append("f=").Append(frequencies[f])
                      .Append(": ").Append(worst).Append(" substeps")
                      .Append(clamped > 0 ? " (clamped)" : "")
                      .Append(", feather ends ").Append(C(runner.Final.Tracked["feather"]))
                      .Append(f == frequencies.Length - 1 ? "" : "; ");

                last = runner;
            }

            return Result("stiff", last,
                "A 20 kg block bolted to a 3x3x3 heavy armour cube at 900 K. " + report);
        }


        public static ScenarioResult Units()
        {

            StringBuilder report = new StringBuilder();
            ScenarioRunner last = null;

            float[] scales = new float[] { 1f, 25f, 225f };
            for (int i = 0; i < scales.Length; i++)
            {

                HistoricalThermalSettings settings = new HistoricalThermalSettings();
                settings.HeatTimeScale = scales[i];
                settings.Derive();

                GridBuilder builder = GridBuilder.Large();
                builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

                ThermalSimulation simulation = builder.BuildSimulation(settings, 800f);

                ScenarioRunner runner = new ScenarioRunner(simulation);
                runner.Environment = t => Worlds.Shadow();
                runner.Track("block", builder.Placed[0]);
                runner.Run(3600f, 600f);

                report.Append("x").Append(scales[i].ToString("n0")).Append(": ")
                      .Append(C(runner.Final.Tracked["block"]))
                      .Append(i == scales.Length - 1 ? "" : ", ");

                last = runner;
            }

            return Result("units", last,
                "One heavy armour block at 800 K in shadow, after one hour, at three thermal "
                + "clocks. Real steel is 450 J/(kg K); HeatTimeScale divides it. " + report);
        }


        public static ScenarioResult Capital()
        {
            Stopwatch build = Stopwatch.StartNew();

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceAll(Catalog.HeavyArmor(),
                GridShapes.Ship(fuselageLength: 180, fuselageWidth: 21, bulkheadSpacing: 6));


            StageTimings timings = new StageTimings();

            ThermalSimulation simulation = new ThermalSimulation(new HistoricalThermalSettings(), builder.Grid);
            simulation.Profiler = timings;

            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }

            simulation.RebuildAll();
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());
            build.Stop();

            int cells = simulation.Solver.Nodes.Count;
            int links = simulation.Solver.Links.Count;
            int rooms = simulation.Rooms.Map.RoomCount;

            double rebuildWorst =
                timings.WorstMs(SimulationPhase.Topology)
                + timings.WorstMs(SimulationPhase.RoomMapping)
                + timings.WorstMs(SimulationPhase.Exposure);

            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Space(new Vector3(0f, 1f, 0f)));

            const int measured = 40;
            for (int i = 0; i < 5; i++) simulation.Solver.Step(simulation.Settings.StepSeconds, state);

            Stopwatch solve = Stopwatch.StartNew();
            for (int i = 0; i < measured; i++) simulation.Solver.Step(simulation.Settings.StepSeconds, state);
            solve.Stop();

            double solverPerStep = solve.Elapsed.TotalMilliseconds / measured;


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Space(new Vector3(0f, 1f, 0f));
            runner.Track("skin", builder.Placed[0]);
            runner.Run(60f, 15f);

            return Result("capital", runner,
                cells.ToString("n0") + " cells, " + links.ToString("n0") + " links, "
                + rooms + " sealed rooms. Build and first map " + build.ElapsedMilliseconds
                + " ms. Worst single rebuild " + rebuildWorst.ToString("n1")
                + " ms against " + solverPerStep.ToString("n2") + " ms per solver step: "
                + timings.Describe(SimulationPhase.Topology) + ", "
                + timings.Describe(SimulationPhase.RoomMapping) + ", "
                + timings.Describe(SimulationPhase.Exposure) + ".");
        }


        public static ScenarioResult Fleet()
        {
            const int fleetSize = 20;


            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.Derive();


            List<ThermalSimulation> fleet = new List<ThermalSimulation>();
            ScenarioRunner first = null;
            int cellsEach = 0;

            for (int i = 0; i < fleetSize; i++)
            {
                GridBuilder builder = GridBuilder.Large();
                builder.PlaceAll(Catalog.HeavyArmor(),
                    GridShapes.Ship(fuselageLength: 40, fuselageWidth: 9, bulkheadSpacing: 6));


                Vector3I engineRoom = new Vector3I(4, 4, 6);
                BlockInstance occupant = builder.Grid.GetAtCell(engineRoom);
                if (occupant != null)
                {
                    builder.Grid.Remove(occupant);
                    builder.Placed.Remove(occupant);
                }
                builder.Place(Catalog.Reactor(), engineRoom)
                       .Wasting(500000f);

                ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
                while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

                cellsEach = simulation.Solver.Nodes.Count;
                fleet.Add(simulation);

                if (i == 0)
                {

                    first = new ScenarioRunner(simulation);
                    first.Environment = t => Worlds.Shadow();
                    first.Track("reactor", builder.Last);
                }
            }

            EnvironmentState state = EnvironmentSolver.Solve(settings, fleet[0].Planet, Worlds.Shadow());

            const int frames = 100;
            int steps = frames * fleetSize;
            for (int i = 0; i < fleet.Count; i++) fleet[i].Solver.Step(settings.StepSeconds, state);

            Stopwatch one = Stopwatch.StartNew();
            for (int s = 0; s < steps; s++) fleet[0].Solver.Step(settings.StepSeconds, state);
            one.Stop();

            Stopwatch all = Stopwatch.StartNew();
            for (int s = 0; s < frames; s++)
            {
                for (int i = 0; i < fleet.Count; i++) fleet[i].Solver.Step(settings.StepSeconds, state);
            }
            all.Stop();

            double onePerStep = one.Elapsed.TotalMilliseconds / steps;
            double fleetPerStep = all.Elapsed.TotalMilliseconds / frames;

            first.Run(60f, 20f);

            return Result("fleet", first,
                fleetSize + " ships of " + cellsEach.ToString("n0") + " cells. A solver step costs "
                + onePerStep.ToString("n3") + " ms on one ship and "
                + (onePerStep <= 0d ? 0d : (fleetPerStep / fleetSize) / onePerStep).ToString("n2")
                + "x that when the same steps are spread across " + fleetSize
                + " grids, so the cost is per cell and not per grid. One frame of the whole fleet "
                + "is " + fleetPerStep.ToString("n3") + " ms, and at "
                + settings.StepsPerSecond.ToString("n0") + " steps a second that is "
                + (fleetPerStep * settings.StepsPerSecond).ToString("n1")
                + " ms of every simulated second.");
        }


        public static ScenarioResult Interior()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), new Vector3I(-3, -3, -3), new Vector3I(4, 4, 4));

            BlockInstance centre = builder.Grid.GetAtCell(Vector3I.Zero);
            builder.Grid.Remove(centre);
            builder.Placed.Remove(centre);
            builder.Place(Catalog.Battery(), Vector3I.Zero).Consuming(200000f);
            BlockInstance buried = builder.Last;

            builder.Place(Catalog.Battery(), new Vector3I(0, 4, 0)).Consuming(200000f);
            BlockInstance exposed = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("buried", buried);
            runner.Track("skin", exposed);
            runner.Track("hull", builder.Grid.GetAtCell(new Vector3I(0, 3, 0)));
            runner.Run(3600f, 600f);

            int buriedFaces = simulation.Solver.GetNode(buried).TotalExposedFaces;
            int exposedFaces = simulation.Solver.GetNode(exposed).TotalExposedFaces;

            return Result("interior", runner,

                "A 200 kW consumer at 5% waste heat, buried in heavy armour ("

                + buriedFaces + " exposed faces) and bolted to the skin (" + exposedFaces

                + "). Buried ends at " + C(runner.Final.Tracked["buried"]) + ", on the skin "

                + C(runner.Final.Tracked["skin"]) + ", the hull above it "

                + C(runner.Final.Tracked["hull"]) + ".");
        }


        public static ScenarioResult Solver()
        {
            GridBuilder builder = GridBuilder.Large();

            BlockModel armour = Catalog.HeavyArmor();
            BlockModel fitting = Catalog.Grating();

            int index = 0;
            foreach (Vector3I cell in GridShapes.Ship(
                fuselageLength: 180, fuselageWidth: 21, bulkheadSpacing: 6))
            {
                builder.Place((index++ % 8) == 0 ? fitting : armour, cell);
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

            int cells = simulation.Solver.Nodes.Count;
            int links = simulation.Solver.Links.Count;

            int exposed = 0;
            for (int i = 0; i < cells; i++)
            {
                if (simulation.Solver.Nodes[i].TotalExposedFaces > 0) exposed++;
            }

            float step = simulation.Settings.StepSeconds;


            SolverCost single = MeasureSolver(simulation, step, links);

            SolverCost stiff = MeasureSolver(simulation, step * 6f, links);


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Space(new Vector3(0f, 1f, 0f));
            runner.Track("skin", builder.Placed[0]);
            runner.Run(30f, 10f);

            return Result("solver", runner,
                cells.ToString("n0") + " cells (" + exposed.ToString("n0")
                + " exposed), " + links.ToString("n0")
                + " links, seeded across a 250-750 K spread so no link is skipped. "
                + single.Describe(simulation.Settings.StepsPerSecond) + " A step six times longer: "
                + stiff.Describe(simulation.Settings.StepsPerSecond / 6f));
        }


        private static void SeedSpread(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            uint state = 2463534242u;
            for (int i = 0; i < nodes.Count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                nodes[i].Temperature = 250f + ((state % 100000u) * 0.005f);
            }
        }

        private struct SolverCost
        {
            public double PerStepMs;
            public double PerVisitNs;
            public double MeanSubsteps;


            public string Describe(float stepsPerSecond)
            {
                return PerStepMs.ToString("n4") + " ms per step at " + MeanSubsteps.ToString("n2")
                    + " substeps, which is " + PerVisitNs.ToString("n2") + " ns per link visit and "
                    + (PerStepMs * stepsPerSecond).ToString("n2") + " ms per simulated second.";
            }
        }


        private static SolverCost MeasureSolver(ThermalSimulation simulation, float step, int links)
        {
            EnvironmentState environment = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Space(new Vector3(0f, 1f, 0f)));

            SeedSpread(simulation);
            for (int i = 0; i < 5; i++) simulation.Solver.Step(step, environment);

            long visits = 0;
            const int measured = 50;

            Stopwatch run = Stopwatch.StartNew();
            for (int i = 0; i < measured; i++)
            {
                simulation.Solver.Step(step, environment);
                visits += (long)simulation.Solver.LastSubsteps * links;
            }
            run.Stop();


            SolverCost cost = new SolverCost();
            cost.PerStepMs = run.Elapsed.TotalMilliseconds / measured;
            cost.PerVisitNs = visits <= 0 ? 0d : (run.Elapsed.TotalMilliseconds * 1e6) / visits;
            cost.MeanSubsteps = (double)visits / measured / (links <= 0 ? 1 : links);
            return cost;
        }



        public static ScenarioResult Weather()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            WeatherResponse.Weather storm = WeatherResponse.For("SnowHeavy");


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t =>
            {
                EnvironmentSample sample = Worlds.PlanetSurface(1f, 0.5f);
                sample.Weather = storm;

                sample.WeatherIntensity = Intensity(t, 300f, 600f, 900f);
                return sample;
            };
            runner.Track("plate", builder.Placed[0]);
            runner.Run(1200f, 60f);

            float clear = runner.Samples[5].AmbientTemperature;
            float worst = float.MaxValue;
            float coldest = float.MaxValue;

            for (int i = 1; i < runner.Samples.Count; i++)
            {
                float ambient = runner.Samples[i].AmbientTemperature;
                if (ambient < worst) worst = ambient;

                float plate = runner.Samples[i].Tracked["plate"];
                if (plate < coldest) coldest = plate;
            }

            EnvironmentState peak = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, StormAt(storm, 1f));
            EnvironmentState calm = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, StormAt(storm, 0f));

            return Result("weather", runner,
                "3x1x3 plate at noon, heavy snowstorm arriving at 300 s and gone by 900 s. Ambient "

                + C(clear) + " falling to " + C(worst) + ", plate down to " + C(coldest)
                + ". At the peak the sun delivers " + peak.SolarEnergy.ToString("n0")
                + " W/m2 against " + calm.SolarEnergy.ToString("n0")
                + " in clear air, and convection runs at " + peak.ConvectionCoefficient.ToString("n1")
                + " against " + calm.ConvectionCoefficient.ToString("n1") + " W/(m2 K).");
        }


        private static EnvironmentSample StormAt(WeatherResponse.Weather storm, float intensity)
        {
            EnvironmentSample sample = Worlds.PlanetSurface(1f, 0.5f);
            sample.Weather = storm;
            sample.WeatherIntensity = intensity;
            return sample;
        }


        private static float Intensity(float t, float start, float peak, float end)
        {
            if (t <= start || t >= end) return 0f;
            if (t < peak) return (t - start) / (peak - start);
            return 1f - ((t - peak) / (end - peak));
        }


        public static ScenarioResult Underground()
        {

            StringBuilder summary = new StringBuilder();
            summary.Append("3x1x3 plate over one day at five depths. ");

            ScenarioRunner last = null;

            float[] depths = { 0f, 10f, 100f, 5000f, 20000f };
            string[] labels = { "surface", "10 m", "100 m", "5 km", "20 km" };

            for (int i = 0; i < depths.Length; i++)
            {
                float depth = depths[i];

                GridBuilder builder = GridBuilder.Large();
                builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));

                ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);


                ScenarioRunner runner = new ScenarioRunner(simulation);
                float dayLength = 7200f;
                runner.Environment = t =>
                {
                    EnvironmentSample sample = Worlds.PlanetSurface(1f, (t / dayLength) % 1f);
                    if (depth <= 0f) return sample;

                    sample.IsUnderground = true;
                    sample.Depth = depth;
                    sample.Radius = Worlds.EarthlikeRadius - depth;
                    sample.Altitude = -depth;
                    return sample;
                };
                runner.Track("plate", builder.Placed[0]);
                runner.Run(dayLength, dayLength / 24f);

                float min = float.MaxValue;
                float max = float.MinValue;
                for (int j = 1; j < runner.Samples.Count; j++)
                {
                    float ambient = runner.Samples[j].AmbientTemperature;
                    if (ambient < min) min = ambient;
                    if (ambient > max) max = ambient;
                }

                summary.Append(labels[i]).Append(' ').Append(C(min)).Append(" to ").Append(C(max))
                       .Append(" (swing ").Append((max - min).ToString("n1")).Append(" K)");
                summary.Append(i == depths.Length - 1 ? ". " : ", ");

                last = runner;
            }

            GridBuilder peak = GridBuilder.Large();
            peak.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));
            ThermalSimulation mountain = peak.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            EnvironmentSample inside = Worlds.PlanetSurface(1f, 0.5f);
            inside.IsUnderground = true;
            inside.Depth = 1000f;
            inside.Radius = Worlds.EarthlikeRadius + 4000f;
            inside.Altitude = 4000f;

            EnvironmentState tunnel = EnvironmentSolver.Solve(mountain.Settings, mountain.Planet, inside);

            summary.Append("A kilometre into a peak 5 km above sea level reads ")
                   .Append(C(tunnel.AmbientTemperature))
                   .Append(", because the deadzone is measured from sea level and not from the ground.");

            return Result("underground", last, summary.ToString());
        }



        public static ScenarioResult CoolingPlant()
        {
            float withPlant, withoutPlant, loopDrawn, loopShed, pumpLift;
            float ignoredDrawn, ignoredShed, ignoredLift;


            ScenarioRunner plant = BuildCoolingPlant(true, out withPlant,
                out loopDrawn, out loopShed, out pumpLift);
            BuildCoolingPlant(false, out withoutPlant,
                out ignoredDrawn, out ignoredShed, out ignoredLift);

            return Result("cooling-plant", plant,
                "A reactor and eight hard-drawing batteries behind a pumped ring, heat pumps and "

                + "eight radiators. Hottest block " + C(withPlant) + " with the plant, "

                + C(withoutPlant) + " without it. The loop draws "
                + loopDrawn.ToString("n0") + " W and sheds " + loopShed.ToString("n0")
                + " W; the pumps lift " + pumpLift.ToString("n0") + " W.");
        }


        private static ScenarioRunner BuildCoolingPlant(bool plumbing, out float hottest,
            out float loopDrawn, out float loopShed, out float pumpLift)
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Fill(Catalog.LightArmor(), new Vector3I(0, 0, 0), new Vector3I(13, 2, 6));


            List<Vector3I> machinery = new List<Vector3I>();


            Vector3I reactorCell = new Vector3I(1, 2, 1);
            builder.Place(Catalog.Reactor(), reactorCell)
                   .Wasting(750000f);
            machinery.Add(reactorCell);

            for (int i = 0; i < 8; i++)
            {

                Vector3I cell = new Vector3I(3 + i, 2, 1);
                builder.Place(Catalog.Battery(), cell)
                       .Consuming(1f * ThermalConstants.MegawattsToWatts);
                machinery.Add(cell);
            }

            if (plumbing)
            {
                List<Vector3I> cells = PipeFitter.RectangleXZ(new Vector3I(1, 3, 1), 11, 4);

                Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
                for (int i = 0; i < cells.Count; i++)
                {
                    if (machinery.Contains(cells[i] + Vector3I.Down)) sinks[i] = Vector3I.Down;
                }

                PipeFitter.BuildRing(builder, cells, -1, sinks);

                for (int i = 0; i < cells.Count; i++)
                {
                    if (sinks.ContainsKey(i)) continue;
                    if (cells[i].Z != 4) continue;
                    if ((cells[i].X % 3) != 1) continue;

                    Vector3I pump = cells[i] + Vector3I.Up;
                    builder.Place(Catalog.HeatPump(), pump,

                        new BlockOrientation(Base6Directions.Direction.Down, Base6Directions.Direction.Forward));
                    builder.Place(Catalog.Radiator(), pump + Vector3I.Up);
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            IList<HeatPumpDevice> pumps = simulation.Solver.HeatPumps;
            for (int i = 0; i < pumps.Count; i++)
            {
                pumps[i].Enabled = true;
                pumps[i].PowerAvailable = 1f;
            }


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", simulation.Grid.GetAtCell(reactorCell));
            if (simulation.Solver.Loops.Count > 0) runner.TrackLoop("coolant", simulation.Solver.Loops[0]);
            runner.Run(7200f, 600f);

            hottest = runner.Final.HottestTemperature;

            loopDrawn = 0f;
            loopShed = 0f;
            for (int i = 0; i < simulation.Solver.Loops.Count; i++)
            {
                loopDrawn += simulation.Solver.Loops[i].LastWattsAbsorbed;
                loopShed += simulation.Solver.Loops[i].LastWattsRejected;
            }

            pumpLift = 0f;
            for (int i = 0; i < pumps.Count; i++) pumpLift += pumps[i].LastLiftedWatts;

            return runner;
        }


        public static ScenarioResult LoopFaults()
        {
            GridBuilder builder = GridBuilder.Large();

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3));

            List<Vector3I> pumpless = PipeFitter.RectangleXZ(new Vector3I(0, 10, 0), 3, 3);
            PipeFitter.BuildPumplessRing(builder, pumpless);

            builder.Place(Catalog.CoolantPump(), new Vector3I(0, 20, 0),

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));

            builder.Place(Catalog.CoolantPump(), new Vector3I(0, 25, 0),

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(0, 25, 1));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(0, 25, -1));

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            CoolantLoopDiagnostics diagnosis = simulation.DiagnoseLoops();


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(60f, 30f);


            StringBuilder faults = new StringBuilder();
            for (int i = 1; i < diagnosis.Counts.Length; i++)
            {
                if (diagnosis.Counts[i] == 0) continue;
                if (faults.Length > 0) faults.Append("; ");
                faults.Append(diagnosis.Counts[i]).Append(" x ")
                      .Append(CoolantLoopDiagnostics.Describe((CoolantFault)i));
            }

            return Result("loop-faults", runner,
                diagnosis.Loops + " loop from " + diagnosis.PipesInLoops + " pipes, "
                + diagnosis.PipesAdrift + " pipes adrift: " + faults + ".");
        }


        public static ScenarioResult LoopDry()
        {

            float plumbedOnly = RingAgainstReactor(false);

            float withSink = RingAgainstReactor(true);

            GridBuilder builder = GridBuilder.Large();
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3);
            PipeFitter.BuildRing(builder, cells);
            builder.Place(Catalog.Reactor(), new Vector3I(1, -1, 0))
                   .Wasting(500000f);

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", simulation.Grid.GetAtCell(new Vector3I(1, -1, 0)));
            runner.TrackLoop("coolant", simulation.Solver.Loops[0]);
            runner.Run(3600f, 600f);

            CoolantLoop loop = simulation.Solver.Loops[0];

            return Result("loop-dry", runner,
                "A closed pumped ring with no sink face against the reactor: 1 loop, coolant at "

                + C(loop.Temperature) + ", drawing " + loop.LastWattsAbsorbed.ToString("n0")

                + " W. Reactor " + C(plumbedOnly) + " with plumbing only against " + C(withSink)
                + " with one sink face turned to meet it.");
        }


        private static float RingAgainstReactor(bool sink)
        {
            GridBuilder builder = GridBuilder.Large();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            if (sink) sinks[1] = Vector3I.Down;

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3), -1, sinks);
            builder.Place(Catalog.Reactor(), new Vector3I(1, -1, 0))
                   .Wasting(500000f);
            BlockInstance reactor = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(3600f, 1800f);

            return simulation.Solver.GetNode(reactor).Temperature;
        }


        public static ScenarioResult HeatPumpBackwards()
        {

            float correct = PumpBetweenReactorAndRadiator(true);

            float backwards = PumpBetweenReactorAndRadiator(false);

            GridBuilder builder = GridBuilder.Large();

            ScenarioRunner runner = PumpRunner(false, builder);

            return Result("heatpump-backwards", runner,

                "A pump between a 125 kW source and a radiator. The source " + C(correct)

                + " with the cold face against it, " + C(backwards)
                + " with the pump turned around — the wrong way costs "
                + (backwards - correct).ToString("n1") + " K and the same electricity.");
        }


        private static float PumpBetweenReactorAndRadiator(bool correctWayRound)
        {
            GridBuilder builder = GridBuilder.Large();

            ScenarioRunner runner = PumpRunner(correctWayRound, builder);
            return runner.Final.Tracked["reactor"];
        }


        private static ScenarioRunner PumpRunner(bool correctWayRound, GridBuilder builder)
        {
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Wasting(125000f);
            BlockInstance reactor = builder.Last;

            Base6Directions.Direction forward = correctWayRound
                ? Base6Directions.Direction.Forward
                : Base6Directions.Direction.Backward;

            builder.Place(Catalog.HeatPump(), new Vector3I(0, 0, 1),

                new BlockOrientation(forward, Base6Directions.Direction.Up));
            builder.Place(Catalog.Radiator(), new Vector3I(0, 0, 2));

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

            IList<HeatPumpDevice> pumps = simulation.Solver.HeatPumps;
            for (int i = 0; i < pumps.Count; i++)
            {
                pumps[i].Enabled = true;
                pumps[i].PowerAvailable = 1f;
            }


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", reactor);
            runner.Run(3600f, 600f);
            return runner;
        }


        public static ScenarioResult HeatPumpLimits()
        {

            StringBuilder report = new StringBuilder();
            ScenarioRunner last = null;
            float[] hotSides = new float[] { 300f, 350f, 500f, 1200f };

            for (int i = 0; i < hotSides.Length; i++)
            {
                GridBuilder builder = GridBuilder.Large();
                builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
                BlockInstance cold = builder.Last;
                builder.Place(Catalog.HeatPump(), new Vector3I(0, 0, 1),

                    new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Up));
                builder.Place(Catalog.HeavyArmor(), new Vector3I(0, 0, 2));
                BlockInstance hot = builder.Last;


                HistoricalThermalSettings settings = new HistoricalThermalSettings();
                settings.EnableEnvironment = false;
                settings.EnableDamage = false;

                ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 290f);
                HeatPumpDevice pump = simulation.Solver.HeatPumps[0];
                pump.Enabled = true;
                pump.PowerAvailable = 1f;

                if (pump.ColdNodeIndex != simulation.Solver.GetNode(cold).Index)
                {
                    throw new InvalidOperationException("the pump's cold face is not on the cold block");
                }

                const float coldSide = 290f;
                float hotSide = hotSides[i];


                ScenarioRunner runner = new ScenarioRunner(simulation);
                runner.Environment = t => Worlds.Shadow();
                runner.AfterStep = sim =>
                {
                    sim.Solver.GetNode(hot).Temperature = hotSide;
                    sim.Solver.GetNode(cold).Temperature = coldSide;
                };
                runner.Track("cold", cold);
                runner.Run(60f, 30f);

                bool ratingBound = pump.LastLiftedWatts >= pump.RatedWatts - 1f;
                report.Append("gap ").Append((hotSide - coldSide).ToString("n0"))
                      .Append(" K: lift ").Append(pump.LastLiftedWatts.ToString("n0"))
                      .Append(" W, cop ").Append(pump.LastCoefficient.ToString("n2"))
                      .Append(ratingBound ? " (rating)" : " (Carnot)")
                      .Append(i == hotSides.Length - 1 ? "" : "; ");

                last = runner;
            }

            return Result("heatpump-limits", last,
                "One 60 kW pump against four gap widths. " + report + ".");
        }


        public static ScenarioResult CoolingRunaway()
        {

            StringBuilder report = new StringBuilder();
            ScenarioRunner last = null;
            float[] megawatts = new float[] { 0.5f, 2f, 32f };

            for (int i = 0; i < megawatts.Length; i++)
            {
                GridBuilder builder = GridBuilder.Large();

                Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
                sinks[1] = Vector3I.Down;
                sinks[5] = Vector3I.Up;

                List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, 5, 5);
                PipeFitter.BuildRing(builder, cells, -1, sinks);

                builder.Place(Catalog.Reactor(), cells[1] + Vector3I.Down)
                       .Wasting(megawatts[i] * ThermalConstants.MegawattsToWatts * 0.25f);
                BlockInstance reactor = builder.Last;
                builder.Place(Catalog.Radiator(), cells[5] + Vector3I.Up);

                ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

                ScenarioRunner runner = new ScenarioRunner(simulation);
                runner.Environment = t => Worlds.Shadow();
                runner.Track("reactor", reactor);
                runner.TrackLoop("coolant", simulation.Solver.Loops[0]);
                runner.Run(7200f, 900f);

                report.Append(megawatts[i].ToString("n1")).Append(" MW: reactor ")
                      .Append(C(runner.Final.Tracked["reactor"])).Append(", coolant ")
                      .Append(C(runner.Final.Tracked["coolant"]))
                      .Append(", ").Append(runner.Final.OverheatingBlocks).Append(" over critical")
                      .Append(i == megawatts.Length - 1 ? "" : "; ");

                last = runner;
            }

            return Result("cooling-runaway", last,
                "One radiator against three heat loads. " + report + ".");
        }


        public static ScenarioResult LoopStiffness()
        {

            StringBuilder report = new StringBuilder();
            ScenarioRunner last = null;
            int[] sides = new int[] { 3, 9, 20 };

            for (int i = 0; i < sides.Length; i++)
            {
                GridBuilder builder = GridBuilder.Large();

                Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
                sinks[1] = Vector3I.Down;

                List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, sides[i], sides[i]);
                PipeFitter.BuildRing(builder, cells, -1, sinks);
                builder.Place(Catalog.HeavyArmor(), cells[1] + Vector3I.Down);
                BlockInstance hot = builder.Last;


                HistoricalThermalSettings settings = new HistoricalThermalSettings();
                settings.EnableEnvironment = false;
                settings.EnableDamage = false;

                ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 300f);
                CoolantLoop loop = simulation.Solver.Loops[0];
                simulation.Solver.GetNode(hot).Temperature = 1200f;

                float conductance = 0f;
                for (int l = 0; l < loop.Links.Count; l++) conductance += loop.Links[l].Conductance;

                float before = simulation.Solver.TotalEnergy;


                ScenarioRunner runner = new ScenarioRunner(simulation);
                runner.Environment = t => Worlds.Shadow();
                runner.Track("sink", hot);
                runner.TrackLoop("coolant", loop);
                runner.Run(300f, 150f);

                float after = simulation.Solver.TotalEnergy;

                report.Append(loop.PipeCount).Append(" pipes: ")
                      .Append(conductance.ToString("n0")).Append(" W/K on ")
                      .Append(loop.ThermalMass.ToString("n0")).Append(" J/K, tau ")
                      .Append((loop.ThermalMass / conductance).ToString("n3")).Append(" s, ")
                      .Append(simulation.Solver.LastSubsteps).Append(" substeps for ")
                      .Append(simulation.Solver.LastRequiredSubsteps.ToString("n1"))
                      .Append(" demanded, energy x").Append((after / before).ToString("n4"))
                      .Append(i == sides.Length - 1 ? "" : "; ");

                last = runner;
            }

            return Result("loop-stiffness", last,
                "A ring at three lengths, step " + (1f / new HistoricalThermalSettings().Derive().StepsPerSecond).ToString("n4")
                + " s. " + report + ".");
        }


        public static ScenarioResult LoopLayout()
        {

            float bunched = LoopLayoutPlant(1, 4, false);

            float spread = LoopLayoutPlant(1, 4, true);
            ScenarioRunner runner;

            float small = LoopLayoutPlant(4, 1, false, out runner);

            return Result("loop-layout", runner,
                "Four 62.5 kW sources and four radiators, 32 pipes and 4 pumps, arranged three ways. "

                + "One ring with the sources bunched: " + C(bunched) + ". One ring with them spread "

                + "evenly: " + C(spread) + ". Four separate rings: " + C(small)
                + ". Splitting the ring buys nothing; spreading the sources buys "
                + (bunched - spread).ToString("n0") + " K.");
        }


        private static float LoopLayoutPlant(int rings, int pumpsPerRing, bool spreadSources)
        {
            ScenarioRunner ignored;

            return LoopLayoutPlant(rings, pumpsPerRing, spreadSources, out ignored);
        }


        private static float LoopLayoutPlant(int rings, int pumpsPerRing, bool spreadSources,
            out ScenarioRunner runner)
        {
            GridBuilder builder = GridBuilder.Large();

            List<BlockInstance> reactors = new List<BlockInstance>();

            int reactorsPerRing = rings == 1 ? 4 : 1;
            int side = rings == 1 ? 9 : 3;

            for (int r = 0; r < rings; r++)
            {
                List<Vector3I> cells = PipeFitter.RectangleXZ(new Vector3I(0, r * 14, 0), side, side);
                Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();

                for (int i = 0; i < reactorsPerRing; i++)
                {
                    int step = cells.Count / Math.Max(1, reactorsPerRing);
                    int sourceAt = spreadSources ? (i * step) + 1 : 1 + i;
                    int radiatorAt = spreadSources
                        ? (i * step) + 1 + (step / 2)
                        : (cells.Count / 2) + i;

                    sinks[sourceAt] = Vector3I.Down;
                    sinks[radiatorAt] = Vector3I.Up;
                }

                PipeFitter.BuildRing(builder, cells, -1, sinks);

                for (int i = 0; i < reactorsPerRing; i++)
                {
                    int step = cells.Count / Math.Max(1, reactorsPerRing);
                    int sourceAt = spreadSources ? (i * step) + 1 : 1 + i;
                    int radiatorAt = spreadSources
                        ? (i * step) + 1 + (step / 2)
                        : (cells.Count / 2) + i;

                    builder.Place(Catalog.Reactor(), cells[sourceAt] + Vector3I.Down)
                           .Wasting(62500f);
                    reactors.Add(builder.Last);

                    builder.Place(Catalog.Radiator(), cells[radiatorAt] + Vector3I.Up);
                }
            }


            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;

            ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 293.15f);

            IList<CoolantLoop> loops = simulation.Solver.Loops;
            for (int l = 0; l < loops.Count; l++)
            {
                while (loops[l].Pumps.Count < pumpsPerRing)
                {

                    CoolantPump extra = new CoolantPump();
                    extra.MaxPowerWatts = 20000f;
                    loops[l].Pumps.Add(extra);
                }
                loops[l].RefreshFlow();
            }


            runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("reactor", reactors[0]);
            runner.Run(10000f, 2000f);

            float hottest = 0f;
            for (int i = 0; i < reactors.Count; i++)
            {
                float t = simulation.Solver.GetNode(reactors[i]).Temperature;
                if (t > hottest) hottest = t;
            }
            return hottest;
        }


        public static ScenarioResult AirConditioning()
        {
            ScenarioRunner runner;

            float without = ConditionedCabin(false, out runner);

            float with = ConditionedCabin(true, out runner);

            return Result("air-conditioning", runner,
                "A sealed cabin with a 15 kW source in it, and a heat pump on one wall rejecting into "

                + "a radiator outside. Room air settles at " + C(without) + " with the pump off and "

                + C(with) + " with it on, a difference of " + (without - with).ToString("n0") + " K.");
        }


        private static float ConditionedCabin(bool pumpRunning, out ScenarioRunner runner)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(4, 4, 4));

            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1)).Wasting(15000f);

            builder.Place(Catalog.HeatPump(), new Vector3I(1, 1, -2),

                new BlockOrientation(Base6Directions.Direction.Backward, Base6Directions.Direction.Up));
            builder.Place(Catalog.Radiator(), new Vector3I(1, 1, -4));


            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;

            ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 320f);
            simulation.RebuildAll();


            Vector3I inside = new Vector3I(2, 1, 1);
            simulation.SetRoomPressure(inside, 1f);
            RoomAirNode air = simulation.Solver.GetRoomAir(simulation.Rooms.Map, inside);

            IList<HeatPumpDevice> pumps = simulation.Solver.HeatPumps;
            for (int i = 0; i < pumps.Count; i++)
            {
                pumps[i].Enabled = pumpRunning;
                pumps[i].PowerAvailable = 1f;
            }


            runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(5000f, 1000f);

            return air == null ? 0f : air.Temperature;
        }



        public static ScenarioResult Station()
        {

            StringBuilder summary = new StringBuilder();


            StationCase shipVacuum = RunHull(false, false, 0);

            StationCase stationVacuum = RunHull(true, false, 0);

            StationCase shipAir = RunHull(false, true, 0);

            StationCase stationAir = RunHull(true, true, 0);

            summary.Append(stationVacuum.Blocks.ToString("n0")).Append(" cells of station in ")
                   .Append(stationVacuum.PressurisedRooms).Append(" pressurised compartments against ")
                   .Append(shipVacuum.Blocks.ToString("n0")).Append(" of ship in ")
                   .Append(shipVacuum.PressurisedRooms).Append(", ")
                   .Append(stationVacuum.ExternalFaces.ToString("n0")).Append(" exposed faces against ")
                   .Append(shipVacuum.ExternalFaces.ToString("n0")).Append(" (")
                   .Append(Ratio(shipVacuum.ExternalFaces, stationVacuum.ExternalFaces))
                   .Append("x), carrying the same ")
                   .Append((StationWatts / 1000f).ToString("n0")).Append(" kW. ");

            float shipVacuumRise = shipVacuum.Mean - shipVacuum.Ambient;
            float stationVacuumRise = stationVacuum.Mean - stationVacuum.Ambient;

            summary.Append("In vacuum the station's mean block settles ")
                   .Append(stationVacuumRise.ToString("n1")).Append(" K above ambient against the ship's ")
                   .Append(shipVacuumRise.ToString("n1")).Append(" K, holding ")
                   .Append(Ratio(stationVacuum.HeatAboveAmbient, shipVacuum.HeatAboveAmbient))
                   .Append("x the heat; its mean sits at ")
                   .Append(Ratio(stationVacuum.Mean, shipVacuum.Mean))
                   .Append("x the ship's on absolute temperature, against the ")
                   .Append(Math.Pow(2.0, 0.25).ToString("n3"))
                   .Append("x a halved radiating area predicts. Hottest block ")
                   .Append(C(stationVacuum.Hottest)).Append(" against ").Append(C(shipVacuum.Hottest))
                   .Append(", which is the source's own neighbourhood and not the grid's. ");

            float shipAirRise = shipAir.Mean - shipAir.Ambient;
            float stationAirRise = stationAir.Mean - stationAir.Ambient;

            summary.Append("On a planet the station's mean settles ").Append(stationAirRise.ToString("n1"))
                   .Append(" K above ambient against the ship's ").Append(shipAirRise.ToString("n1"))
                   .Append(" K, which is ").Append(Ratio(stationAirRise, shipAirRise))
                   .Append("x on the rise against the 2x a halved convecting area predicts. ");


            StationCase stationNoAir = RunHull(true, false, 0, roomAir: false);
            float noAirRise = stationNoAir.Mean - stationNoAir.Ambient;
            summary.Append("With room air off the station's mean sits ").Append(noAirRise.ToString("n1"))
                   .Append(" K above ambient, ")
                   .Append(Math.Abs(noAirRise - stationVacuumRise).ToString("n2")).Append(" K ")
                   .Append(noAirRise > stationVacuumRise ? "hotter" : "cooler")
                   .Append(" than with it on across ").Append(stationVacuum.PressurisedRooms)
                   .Append(" compartments. ");


            StationCase shipHot = RunHull(false, true, 0, watts: StationWatts * 10f);

            StationCase stationHot = RunHull(true, true, 0, watts: StationWatts * 10f);
            float shipHotRise = shipHot.Mean - shipHot.Ambient;
            float stationHotRise = stationHot.Mean - stationHot.Ambient;

            summary.Append("At ten times the load, where the air rises are large enough to divide, ")
                   .Append(stationHotRise.ToString("n1")).Append(" K against ")
                   .Append(shipHotRise.ToString("n1")).Append(" K, a ratio of ")
                   .Append(Ratio(stationHotRise, shipHotRise))
                   .Append("x — the same ratio at a tenth of the load, so it is not two small "
                           + "numbers being divided. ");

            summary.Append("Skin against interior, station then ship: in vacuum ")
                   .Append(Gap(stationVacuum)).Append(" and ").Append(Gap(shipVacuum))
                   .Append("; in air at ten times the load ")
                   .Append(Gap(stationHot)).Append(" and ").Append(Gap(shipHot)).Append(". ");

            int[] ladder = { 0, 8, 16, 32, 64, 128, 256 };
            int reached = -1;
            float reachedRise = 0f;
            int lastCount = -1;

            summary.Append("Radiators standing on the station's roof, against the ship's unaided ")
                   .Append(shipVacuumRise.ToString("n1")).Append(" K, with the station's own "
                   + "exposed-face count beside each: ");

            for (int i = 0; i < ladder.Length; i++)
            {

                StationCase priced = RunHull(true, false, ladder[i]);
                if (priced.Radiators == lastCount) continue;
                lastCount = priced.Radiators;

                float rise = priced.Mean - priced.Ambient;

                summary.Append(priced.Radiators).Append(" -> ").Append(rise.ToString("n1"))
                       .Append(" K at ").Append(priced.ExternalFaces.ToString("n0")).Append(" faces, ");

                if (reached < 0 && rise <= shipVacuumRise)
                {
                    reached = priced.Radiators;
                    reachedRise = rise;
                }
            }

            summary.Append(reached < 0
                ? "and the roof runs out before the ship's figure is reached, so bolted-on area "
                  + "buys a station real cooling and not enough of it."
                : "and " + reached + " reach it, at " + reachedRise.ToString("n1")
                  + " K, which is what the room a base has is worth in blocks.");

            summary.Append(" Every arm was flat to ")
                   .Append(Math.Max(Math.Max(shipVacuum.SettleDrift, stationVacuum.SettleDrift),
                                    Math.Max(shipAir.SettleDrift, stationAir.SettleDrift)).ToString("n4"))
                   .Append(" K or better over its last two samples.");

            return Result("station", stationVacuum.Runner, summary.ToString());
        }

        private const float StationWatts = 600000f;

        private class StationCase
        {
            public ScenarioRunner Runner;
            public int Blocks;
            public int ExternalFaces;
            public int PressurisedRooms;
            public int Radiators;
            public float Hottest;
            public float Mean;
            public float Ambient;
            public float HeatAboveAmbient;
            public float SkinMean;
            public float InsideMean;
            public int InsideCount;
            public float SettleDrift;
        }


        private static string Gap(StationCase c)
        {
            if (c.InsideCount == 0) return "no interior";
            return C(c.SkinMean) + " skin, " + C(c.InsideMean) + " inside ("
                + (c.InsideMean - c.SkinMean).ToString("n1") + " K over " + c.InsideCount + " blocks)";
        }


        private static string Ratio(float a, float b)
        {
            return b == 0f ? "n/a" : (a / b).ToString("n3");
        }


        private static StationCase RunHull(bool station, bool planet, int radiators,
            bool roomAir = true, float watts = StationWatts)
        {
            GridBuilder builder = GridBuilder.Large();


            List<Vector3I> cells = new List<Vector3I>(station
                ? GridShapes.Station(new Vector3I(17, 15, 19), new Vector3I(3, 3, 3))
                : GridShapes.Ship(40, 9, 12));

            cells.Sort(CellOrder);

            Vector3I min = cells[0];
            Vector3I max = cells[0];
            for (int i = 1; i < cells.Count; i++)
            {
                min = Vector3I.Min(min, cells[i]);
                max = Vector3I.Max(max, cells[i]);
            }


            Vector3 centre = new Vector3(
                (min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f, (min.Z + max.Z) * 0.5f);

            BlockModel armour = Catalog.LightArmor();
            foreach (Vector3I cell in cells) builder.Place(armour, cell);

            int placedRadiators = 0;
            if (radiators > 0)
            {
                BlockModel radiator = Catalog.Radiator();

                HashSet<Vector3I> occupied = new HashSet<Vector3I>(cells, Vector3I.Comparer);

                for (int z = min.Z; z + 1 <= max.Z && placedRadiators < radiators; z += 2)
                {
                    for (int x = min.X; x <= max.X && placedRadiators < radiators; x += 1)
                    {
                        bool backed = true;
                        for (int dz = 0; dz < 2 && backed; dz++)
                            backed = occupied.Contains(new Vector3I(x, max.Y, z + dz));

                        if (!backed) continue;

                        builder.Place(radiator, new Vector3I(x, max.Y + 1, z));
                        placedRadiators++;
                    }
                }
            }


            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableRoomAir = roomAir;
            settings.EnableFriction = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;

            ThermalSimulation simulation = builder.BuildSimulation(settings.Derive(), 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

            int pressurised = 0;
            if (roomAir)
            {
                RoomMap rooms = simulation.Rooms.Map;
                for (int i = 0; i < rooms.RoomCount; i++)
                {
                    if (rooms.CellsInRoom(i) == 0) continue;
                    if (simulation.SetRoomPressure(rooms.CellsOf(i)[0], 1f)) pressurised++;
                }
            }


            List<ThermalNode> nodes = new List<ThermalNode>();
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++) nodes.Add(simulation.Solver.Nodes[i]);

            nodes.Sort(delegate (ThermalNode a, ThermalNode b)
            {
                float da = Vector3.DistanceSquared(new Vector3(a.Block.Position), centre);
                float db = Vector3.DistanceSquared(new Vector3(b.Block.Position), centre);
                int byDistance = da.CompareTo(db);
                return byDistance != 0 ? byDistance : a.Index.CompareTo(b.Index);
            });

            const int Sources = 12;
            float each = watts / Sources;
            for (int i = 0; i < Sources && i < nodes.Count; i++)
            {
                nodes[i].Block.PowerConsumedWatts = each;
                nodes[i].Block.Thermal.ConsumerWasteEnergy = 1f;
                nodes[i].RefreshHeatGeneration();
            }

            int externalFaces = 0;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                externalFaces += simulation.Solver.Nodes[i].TotalExposedFaces;
            }


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = planet
                ? (Func<float, EnvironmentSample>)(t => Worlds.PlanetSurface(1f, 0.25f))
                : (t => Worlds.Shadow());
            runner.Run(10000f, 1000f);

            float ambient = runner.Final.AmbientTemperature;
            float heat = 0f;

            double skinTotal = 0d, insideTotal = 0d;
            int skinCount = 0, insideCount = 0;

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = simulation.Solver.Nodes[i];
                heat += (node.Temperature - ambient) * node.ThermalMass;

                if (node.TotalExposedFaces > 0) { skinTotal += node.Temperature; skinCount++; }
                else { insideTotal += node.Temperature; insideCount++; }
            }

            int last = runner.Samples.Count - 1;
            float drift = last > 0
                ? Math.Abs(runner.Samples[last].MeanTemperature - runner.Samples[last - 1].MeanTemperature)
                : float.NaN;

            return new StationCase
            {
                Runner = runner,
                Blocks = simulation.Solver.Nodes.Count,
                ExternalFaces = externalFaces,
                PressurisedRooms = pressurised,
                Radiators = placedRadiators,
                Hottest = runner.Final.HottestTemperature,
                Mean = runner.Final.MeanTemperature,
                Ambient = ambient,
                HeatAboveAmbient = heat,
                SkinMean = skinCount == 0 ? float.NaN : (float)(skinTotal / skinCount),
                InsideMean = insideCount == 0 ? float.NaN : (float)(insideTotal / insideCount),
                InsideCount = insideCount,
                SettleDrift = drift,
            };
        }


        private static int CellOrder(Vector3I a, Vector3I b)
        {
            if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
            if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
            return a.X.CompareTo(b.X);
        }



        private static ScenarioResult Result(string name, ScenarioRunner runner, string summary)
        {

            ScenarioResult result = new ScenarioResult();
            result.Name = name;
            result.Runner = runner;
            result.Summary = summary;
            result.Csv = runner.ToCsv();
            return result;
        }


        private static string C(float kelvin)
        {
            return ThermalConstants.KelvinToCelsius(kelvin).ToString("n1") + " C";
        }
    }
}

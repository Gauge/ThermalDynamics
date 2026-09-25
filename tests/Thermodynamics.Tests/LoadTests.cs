using System;
using System.Collections.Generic;
using System.Diagnostics;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Keeps the timed tests off the same cores as the rest of the suite. A stopwatch reading taken
    /// while thirty other tests saturate the machine measures the machine: the first run of these
    /// reported 1.7x and then 3.2x from the same binary, all of it scheduling.
    /// </summary>
    [CollectionDefinition("load", DisableParallelization = true)]
    public class LoadCollection { }

    /// <summary>
    /// What an update costs, asserted on work counters rather than on a stopwatch — a millisecond
    /// threshold is a claim about the machine, and "placing one block must not visit every node" is a
    /// claim about the algorithm. Every test writes its figures to the test output, so a suite run is
    /// also a performance report. See load-and-hitching.md, Catching it again.
    /// </summary>
    [Collection("load")]
    [Trait("speed", "slow")]
    public class LoadTests
    {
        private const int Small = 8000;
        private const int Large = 32000;

        private const int Covered = 64000;

        private readonly ITestOutputHelper output;


        public LoadTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static ThermalSimulation Build(int blocks, string shape = "ship")
        {
            return LoadBenchmarks.BuildSettled(shape, blocks);
        }


        private static EnvironmentSample Space()
        {
            return Worlds.Space(new Vector3(0f, 1f, 0f));
        }


        [Fact]

        public void ASettledGridRebuildsNothing()
        {

            ThermalSimulation simulation = Build(Small);

            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            simulation.Work.Reset();

            for (int tick = 0; tick < 60; tick++)
            {
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
            }

            SimulationWork work = simulation.Work;
            output.WriteLine(simulation.Solver.Nodes.Count.ToString("n0")
                + " blocks, 60 settled ticks: " + work.SolverSteps + " solver steps, "
                + work.TopologyRebuilds + " topology rebuilds, "
                + work.ExposureRefreshes + " exposure refreshes, "
                + work.LoopSearches + " loop searches, "
                + work.RoomPassesBegun + " room passes.");

            Assert.True(work.SolverSteps > 0, "sixty ticks should have stepped the solver");
            Assert.Equal(0, work.TopologyRebuilds);
            Assert.Equal(0, work.ExposureRefreshes);
            Assert.Equal(0, work.LoopSearches);
            Assert.Equal(0, work.HeatPumpRebuilds);
            Assert.Equal(0, work.RoomPassesBegun);
        }

        [Fact]

        public void SolverCostPerLinkStaysProportional()
        {

            double small = BestNanosecondsPerLinkVisit(Small);

            double large = BestNanosecondsPerLinkVisit(Large);

            double ratio = small <= 0d ? 0d : large / small;
            output.WriteLine(Small.ToString("n0") + " blocks: " + small.ToString("n2")
                + " ns/link visit; " + Large.ToString("n0") + " blocks: " + large.ToString("n2")
                + " ns/link visit; ratio " + ratio.ToString("n2") + "x.");

            Assert.True(ratio < 3d,
                "cost per link visit grew " + ratio.ToString("n2") + "x over four times the blocks; "
                + "the conduction pass should be close to linear in link count");
        }


        private static double BestNanosecondsPerLinkVisit(int blocks)
        {
            double best = double.MaxValue;
            for (int i = 0; i < 3; i++)
            {

                double sample = NanosecondsPerLinkVisit(blocks);
                if (sample > 0d && sample < best) best = sample;
            }
            return best == double.MaxValue ? 0d : best;
        }


        private static double NanosecondsPerLinkVisit(int blocks)
        {

            ThermalSimulation simulation = Build(blocks);
            LoadBenchmarks.SeedSpread(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, Space());

            float step = simulation.Settings.StepSeconds;
            for (int i = 0; i < 5; i++) simulation.Solver.Step(step, state);

            const int measured = 20;
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < measured; i++) simulation.Solver.Step(step, state);
            watch.Stop();

            long visits = (long)simulation.Solver.Links.Count
                * Math.Max(1, simulation.Solver.LastSubsteps) * measured;

            return visits == 0 ? 0d : watch.Elapsed.TotalMilliseconds * 1e6d / visits;
        }


        [Fact]

        public void ManyPlacementsInOneTickCoalesceIntoOneRebuild()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            simulation.Work.Reset();

            BlockModel armour = Catalog.HeavyArmor();

            Vector3I start = simulation.Grid.Min - new Vector3I(2, 0, 0);

            const int placed = 100;
            for (int i = 0; i < placed; i++)
            {
                simulation.AddBlock(

                    new BlockInstance(armour, start + new Vector3I(0, 0, i), BlockOrientation.Identity),
                    293.15f);
            }

            int ticks = 0;
            do
            {
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
                ticks++;
            }
            while ((simulation.HasPendingWork || simulation.Work.TopologyRebuilds == 0)
                && ticks < 10000);

            output.WriteLine(placed + " blocks placed, settled over " + ticks + " ticks: "
                + simulation.Work.TopologyRebuilds + " topology rebuilds, "
                + simulation.Work.RoomPassesBegun + " room passes, "
                + simulation.Work.LoopSearches + " loop searches.");

            Assert.Equal(1, simulation.Work.TopologyRebuilds);
            Assert.Equal(1, simulation.Work.RoomPassesBegun);
            Assert.Equal(1, simulation.Work.LoopSearches);
            Assert.Equal(1, simulation.Work.HeatPumpRebuilds);
        }

        [Fact]

        public void SearchesForPlumbingSkipAGridThatHasNone()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            Assert.Equal(0, simulation.Grid.CoolantBlockCount);
            Assert.Equal(0, simulation.Grid.HeatPumpBlockCount);

            simulation.Work.Reset();

            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), simulation.Grid.Min - new Vector3I(2, 0, 0),
                    BlockOrientation.Identity),
                293.15f);

            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine(simulation.Solver.Nodes.Count.ToString("n0")
                + " blocks with no plumbing, one block placed: "
                + simulation.Work.LoopSearches + " loop searches visiting "
                + simulation.Work.LoopSearchCells + " cells, "
                + simulation.Work.HeatPumpRebuilds + " heat pump rebuilds visiting "
                + simulation.Work.HeatPumpNodeVisits + " nodes.");

            Assert.True(simulation.Work.LoopSearches > 0, "the search should still have been asked for");
            Assert.Equal(0, simulation.Work.LoopSearchCells);
            Assert.Equal(0, simulation.Work.HeatPumpNodeVisits);
        }

        [Fact]

        public void ARebuildIsBilledToTopologyAndNotToTheSolver()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());


            StageTimings timings = new StageTimings();
            simulation.Profiler = timings;

            simulation.Work.Reset();
            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), simulation.Grid.Min - new Vector3I(2, 0, 0),
                    BlockOrientation.Identity),
                293.15f);

            int topologyCallsBefore = timings.Calls(SimulationPhase.Topology);
            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine("one block placed: " + simulation.Work.TopologyRebuilds
                + " topology rebuilds during " + (timings.Calls(SimulationPhase.Topology) - topologyCallsBefore)
                + " topology stages, worst " + timings.WorstMs(SimulationPhase.Topology).ToString("n2")
                + " ms; solver worst " + timings.WorstMs(SimulationPhase.Solver).ToString("n2") + " ms.");

            Assert.Equal(1, simulation.Work.TopologyRebuilds);
            Assert.True(timings.Calls(SimulationPhase.Topology) > topologyCallsBefore,
                "the rebuild should have happened inside a topology stage");
        }

        [Fact]

        public void ReadingTheLinkCountDoesNotRebuildTheGraph()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), simulation.Grid.Min - new Vector3I(2, 0, 0),
                    BlockOrientation.Identity),
                293.15f);

            simulation.Work.Reset();

            int count = simulation.Solver.LinkCount;

            output.WriteLine("link count read on a dirty graph: " + count.ToString("n0")
                + " links, " + simulation.Work.TopologyRebuilds + " rebuilds caused.");

            Assert.True(count > 0);
            Assert.Equal(0, simulation.Work.TopologyRebuilds);
        }

        [Fact]

        public void PlacingOneBlockLinksTheBlockAndNotTheGrid()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            int blocks = simulation.Solver.Nodes.Count;
            simulation.Work.Reset();

            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), simulation.Grid.Min - new Vector3I(2, 0, 0),
                    BlockOrientation.Identity),
                293.15f);
            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine("one block placed on " + blocks.ToString("n0") + " blocks: "
                + simulation.Work.TopologyNodeVisits + " nodes visited for topology, "
                + simulation.Work.LinksBuilt + " links built.");

            Assert.Equal(1, simulation.Work.TopologyRebuilds);
            Assert.True(simulation.Work.TopologyNodeVisits < 16,
                "linking one block visited " + simulation.Work.TopologyNodeVisits
                + " nodes on a grid of " + blocks + "; it should visit the block and its neighbours");
        }

        [Fact]

        public void WeldingABurstCostsTheBurstAndNotTheGrid()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            int blocks = simulation.Solver.Nodes.Count;
            simulation.Work.Reset();

            BlockModel armour = Catalog.HeavyArmor();

            Vector3I start = simulation.Grid.Min - new Vector3I(2, 0, 0);

            const int placed = 100;
            for (int i = 0; i < placed; i++)
            {
                simulation.AddBlock(

                    new BlockInstance(armour, start + new Vector3I(0, 0, i), BlockOrientation.Identity),
                    293.15f);
            }

            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine(placed + " blocks welded onto " + blocks.ToString("n0") + ": "
                + simulation.Work.TopologyNodeVisits + " nodes visited, "
                + simulation.Work.LinksBuilt + " links built.");

            Assert.Equal(placed, (int)simulation.Work.TopologyNodeVisits);
            Assert.True(simulation.Work.LinksBuilt >= placed - 1,
                "a run of blocks laid end to end should link to each other");
        }

        [Fact]

        public void GrindingOneBlockUnpicksTheBlockAndNotTheGrid()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            int blocks = simulation.Solver.Nodes.Count;
            int links = simulation.Solver.LinkCount;

            simulation.Work.Reset();

            BlockInstance doomed = null;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].LinkCount < 4) continue;
                doomed = nodes[i].Block;
                break;
            }
            Assert.NotNull(doomed);

            simulation.RemoveBlock(doomed);
            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine("one block ground off " + blocks.ToString("n0") + " blocks: "
                + simulation.Work.NodesRemoved + " nodes removed, "
                + simulation.Work.LinksRemoved + " links unpicked, "
                + simulation.Work.TopologyRebuilds + " global rebuilds, "
                + simulation.Work.TopologyNodeVisits + " nodes visited.");

            Assert.Equal(0, simulation.Work.TopologyRebuilds);
            Assert.Equal(1, simulation.Work.NodesRemoved);
            Assert.True(simulation.Work.LinksRemoved < 16,
                "unpicking one block dropped " + simulation.Work.LinksRemoved + " links");
            Assert.Equal(blocks - 1, simulation.Solver.Nodes.Count);
            Assert.True(simulation.Solver.LinkCount < links,
                "the block's links should be gone");
        }

        [Fact]

        public void GrindingABurstCostsTheBurstAndNotTheGrid()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            int blocks = simulation.Solver.Nodes.Count;
            simulation.Work.Reset();

            const int destroyed = 100;
            for (int i = 0; i < destroyed; i++)
            {
                simulation.RemoveBlock(simulation.Solver.Nodes[0].Block);
            }

            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine(destroyed + " blocks ground off " + blocks.ToString("n0") + ": "
                + simulation.Work.NodesRemoved + " nodes removed, "
                + simulation.Work.LinksRemoved + " links unpicked, "
                + simulation.Work.TopologyRebuilds + " global rebuilds.");

            Assert.Equal(0, simulation.Work.TopologyRebuilds);
            Assert.Equal(destroyed, (int)simulation.Work.NodesRemoved);
            Assert.Equal(blocks - destroyed, simulation.Solver.Nodes.Count);
        }

        [Fact]

        public void PressurisingEveryRoomCostsOnePassNotOnePerRoom()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            IList<RoomAirNode> air = simulation.RoomAir;
            Assert.True(air.Count > 4, "the hull should have mapped several compartments, got " + air.Count);

            simulation.Work.Reset();

            for (int i = 0; i < air.Count; i++)
            {
                simulation.SetRoomPressure(air[i].Anchor, 1f);
            }

            simulation.Update(LoadBenchmarks.TickSeconds, Space());
            simulation.Update(LoadBenchmarks.TickSeconds, Space());

            output.WriteLine(air.Count + " rooms pressurised: "
                + simulation.Work.ConductanceRecomputes + " passes over the links.");

            Assert.True(simulation.Work.ConductanceRecomputes <= 2,
                air.Count + " rooms pressurised caused "
                + simulation.Work.ConductanceRecomputes + " passes over every link");
        }


        [Fact]

        public void RoomMappingNeverExceedsItsBudgetInOneTick()
        {

            ThermalSimulation simulation = Build(Large);

            Vector3I extents = (simulation.Grid.Max - simulation.Grid.Min) + Vector3I.One;
            int volume = Math.Max(1, extents.X * extents.Y * extents.Z);
            int budget = SimulationScheduler.RoomMappingBudget(volume);

            simulation.MarkTopologyDirty();

            long worst = 0;
            int ticks = 0;

            do
            {
                simulation.Work.Reset();
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
                if (simulation.Work.RoomCellsVisited > worst) worst = simulation.Work.RoomCellsVisited;
                ticks++;
            }
            while (simulation.HasPendingWork && ticks < 100000);

            output.WriteLine("bounding volume " + volume.ToString("n0") + ", budget " + budget
                + " cells/tick, worst tick visited " + worst.ToString("n0")
                + " over " + ticks + " ticks.");

            Assert.True(worst <= budget,
                "one tick flooded " + worst + " cells against a budget of " + budget);
        }

        [Fact]

        public void RoomMapConvergenceIsTheBoxDividedByItsBudget()
        {

            ThermalSimulation simulation = Build(Large);

            Vector3I extents = (simulation.Grid.Max - simulation.Grid.Min) + Vector3I.One;
            int volume = Math.Max(1, extents.X * extents.Y * extents.Z);
            int budget = SimulationScheduler.RoomMappingBudget(volume);

            simulation.MarkTopologyDirty();

            int ticks = 0;
            do
            {
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
                ticks++;
            }
            while (simulation.HasPendingWork && ticks < 100000);

            double quotient = volume / (double)budget;
            output.WriteLine("bounding volume " + volume.ToString("n0") + " over budget " + budget
                + " is " + quotient.ToString("n0") + " ticks; converged in " + ticks
                + " (" + (ticks / quotient).ToString("n2") + "x the quotient).");

            Assert.True(ticks >= quotient * 0.95,
                "converged in " + ticks + " ticks against a budget that allows no fewer than "
                + quotient.ToString("n0") + " — either the budget is not being respected or the box"
                + " is not being walked");
            Assert.True(ticks <= quotient * 3.0,
                "converged in " + ticks + " ticks against a quotient of " + quotient.ToString("n0")
                + ", so the walk is costing far more than the cells it has to visit");
        }

        [Fact]

        public void ExposureRefreshNeverExceedsItsBudgetInOneTick()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            int budget = SimulationScheduler.ExposureBudget(simulation.Solver.Nodes.Count);

            simulation.MarkTopologyDirty();

            long worst = 0;
            long total = 0;
            int ticks = 0;

            do
            {
                simulation.Work.Reset();
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
                if (simulation.Work.ExposureNodeVisits > worst) worst = simulation.Work.ExposureNodeVisits;
                total += simulation.Work.ExposureNodeVisits;
                ticks++;
            }
            while (simulation.HasPendingWork && ticks < 100000);

            output.WriteLine(simulation.Solver.Nodes.Count.ToString("n0") + " blocks, budget "
                + budget + " nodes/tick, worst tick refreshed " + worst.ToString("n0")
                + ", " + total.ToString("n0") + " over " + ticks + " ticks.");

            Assert.True(worst <= budget,
                "one tick refreshed " + worst + " nodes against a budget of " + budget);
            Assert.True(total >= simulation.Solver.Nodes.Count,
                "the pass should still have visited every node: " + total + " of "
                + simulation.Solver.Nodes.Count);
        }


        [Fact]

        public void AStepNeverExceedsItsLinkVisitBudget()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            simulation.Settings.MaxElementVisitsPerStep = simulation.Solver.LinkCount * 2;
            simulation.Settings.Derive();

            LoadBenchmarks.SeedSpread(simulation);

            long worst = 0;
            for (int tick = 0; tick < 200; tick++)
            {
                simulation.Work.Reset();
                simulation.Update(LoadBenchmarks.TickSeconds, Space());

                long visits = simulation.Work.SolverSubsteps * simulation.Solver.LinkCount;
                if (visits > worst) worst = visits;
            }

            output.WriteLine(simulation.Solver.LinkCount.ToString("n0") + " links, budget "
                + simulation.Settings.MaxElementVisitsPerStep.ToString("n0")
                + " visits/step, worst step made " + worst.ToString("n0")
                + ". Simulation rate " + (100d * simulation.SimulationRate).ToString("n1") + "%.");

            Assert.True(worst <= simulation.Settings.MaxElementVisitsPerStep,
                "a step made " + worst + " link visits against a budget of "
                + simulation.Settings.MaxElementVisitsPerStep);
        }

        [Fact]

        public void AShortenedStepLosesTimeAndNotAccuracy()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());

            LoadBenchmarks.SeedSpread(simulation);

            simulation.Settings.MaxElementVisitsPerStep = simulation.Solver.LinkCount;
            simulation.Settings.Derive();

            for (int tick = 0; tick < 60; tick++)
            {
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
            }

            output.WriteLine("budget one substep: " + simulation.Solver.LastSubsteps
                + " substeps on the last step, clamped " + simulation.Solver.LastStepWasClamped
                + ", skipped " + simulation.SimulatedSecondsSkipped.ToString("n2")
                + " s of " + (simulation.SimulatedSecondsRun + simulation.SimulatedSecondsSkipped).ToString("n2")
                + " (rate " + (100d * simulation.SimulationRate).ToString("n1") + "%).");

            Assert.Equal(1, simulation.Solver.LastSubsteps);
            Assert.False(simulation.Solver.LastStepWasClamped,
                "the step should have been shortened to fit, not clamped to fit");
            Assert.True(simulation.SimulatedSecondsSkipped > 0d,
                "a budget this tight should have cost simulated time");
            Assert.True(simulation.SimulationRate < 1d);
        }

        [Fact]

        public void AGridUnderTheBudgetIsUnaffected()
        {

            ThermalSimulation simulation = Build(Small);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());
            LoadBenchmarks.SeedSpread(simulation);

            simulation.Settings.MaxElementVisitsPerStep = (int)(simulation.SubstepCost * 200);
            simulation.Settings.Derive();

            for (int tick = 0; tick < 60; tick++)
            {
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
            }

            output.WriteLine(simulation.Solver.Nodes.Count.ToString("n0") + " nodes, "
                + simulation.Solver.LinkCount.ToString("n0") + " links, substep cost "
                + simulation.SubstepCost.ToString("n0") + ", demand "
                + simulation.Solver.LastRequiredSubsteps.ToString("n2") + ", budget "
                + simulation.SubstepBudget.ToString("n0") + " against the default "
                + simulation.Settings.MaxElementVisitsPerStep.ToString("n0")
                + " visit allowance: rate " + (100d * simulation.SimulationRate).ToString("n1") + "%.");

            Assert.Equal(0d, simulation.SimulatedSecondsSkipped, 6);
            Assert.Equal(1d, simulation.SimulationRate, 6);
        }

        [Fact]

        public void TheShippedAllowanceFitsAGridAndAHalvedOneDoesNot()
        {

            ThermalSimulation shipped = Build(Covered);
            while (shipped.HasPendingWork) shipped.Update(LoadBenchmarks.TickSeconds, Space());
            LoadBenchmarks.SeedSpread(shipped);

            shipped.Settings.MaxSubsteps = 4096;
            shipped.Settings.Derive();

            for (int tick = 0; tick < 60; tick++)
            {
                shipped.Update(LoadBenchmarks.TickSeconds, Space());
            }

            output.WriteLine("shipped: cost " + shipped.SubstepCost.ToString("n0")
                + ", demand " + shipped.Solver.LastRequiredSubsteps.ToString("n2")
                + ", budget " + shipped.SubstepBudget.ToString("n0")
                + ", rate " + (100d * shipped.SimulationRate).ToString("n1") + "%.");

            Assert.True(shipped.SubstepCost > shipped.Solver.LinkCount * 2);
            Assert.Equal(1d, shipped.SimulationRate, 6);


            ThermalSimulation tighter = Build(Covered);
            while (tighter.HasPendingWork) tighter.Update(LoadBenchmarks.TickSeconds, Space());
            LoadBenchmarks.SeedSpread(tighter);

            tighter.Settings.MaxElementVisitsPerStep = shipped.Settings.MaxElementVisitsPerStep / 2;
            tighter.Settings.MaxSubsteps = 4096;
            tighter.Settings.Derive();

            for (int tick = 0; tick < 60; tick++)
            {
                tighter.Update(LoadBenchmarks.TickSeconds, Space());
            }

            output.WriteLine("half the allowance: demand "
                + tighter.Solver.LastRequiredSubsteps.ToString("n2")
                + ", budget " + tighter.SubstepBudget.ToString("n0")
                + ", rate " + (100d * tighter.SimulationRate).ToString("n1") + "%.");

            Assert.False(tighter.Solver.LastStepWasClamped);
            Assert.True(tighter.SimulationRate < 1d);
            Assert.True(tighter.SimulationRate > 0.5d);
        }


        [Fact]

        public void ASettledTickFitsInAFrame()
        {

            ThermalSimulation simulation = Build(Large);
            while (simulation.HasPendingWork) simulation.Update(LoadBenchmarks.TickSeconds, Space());
            LoadBenchmarks.SeedSpread(simulation);


            FrameTrace trace = new FrameTrace("settled " + Large.ToString("n0"));

            Stopwatch watch = new Stopwatch();

            for (int tick = 0; tick < 120; tick++)
            {
                watch.Restart();
                simulation.Update(LoadBenchmarks.TickSeconds, Space());
                watch.Stop();
                trace.Add(watch.Elapsed.TotalMilliseconds);
            }

            output.WriteLine(simulation.Solver.Nodes.Count.ToString("n0") + " blocks, "
                + simulation.Solver.Links.Count.ToString("n0") + " links. " + trace.Describe());

            const double ceiling = 8d * FrameTrace.FrameBudgetMs;
            Assert.True(trace.Percentile(0.95) < ceiling,
                "p95 settled tick was " + trace.Percentile(0.95).ToString("n2")
                + " ms against a ceiling of " + ceiling.ToString("n1") + " ms");
        }
    }
}

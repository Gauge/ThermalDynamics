using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The step prologue runs on the tick that rebuilds the topology, not on the first step after
    /// it (`D4`): a rebuilt grid's first step must find the node mirror current, the link mass
    /// factors filled and every buffer sized, so it pays a step's cost rather than the rebuild's.
    ///
    /// <para>
    /// Three claims: a step after a rebuild performs no full resync (the mirror was taken at
    /// rebuild time); the first step allocates like a later one (the link-mass array used to be
    /// bought and doubled inside it); and preparing early changes no temperature anywhere — the
    /// prologue is the same code the step would run, so stepping a prepared grid and an
    /// unprepared one must agree to the last bit (`D8`).
    /// </para>
    /// </summary>
    public class StepPrologueTests
    {

        private static ThermalSimulation Census(int blocks)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));

            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++) simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void AStepAfterARebuildPerformsNoFullResync()
        {

            ThermalSimulation simulation = Census(2000);

            long full = simulation.Work.FullNodeResyncs;
            Assert.True(full > 0, "the rebuild performed no full resync at all, so the step below has nothing to be spared");
            long syncs = simulation.Work.NodeStateSyncs;

            simulation.StepExact(1, Worlds.Shadow());

            Assert.True(simulation.Work.NodeStateSyncs > syncs, "the step ran no sync at all, so this measured nothing");
            Assert.True(simulation.Work.FullNodeResyncs == full,
                "the first step after a rebuild performed a full resync; the prologue on the rebuild tick did not take it");
        }

        [Fact]

        public void TheFirstStepAllocatesLikeALaterOne()
        {

            ThermalSimulation simulation = Census(32000);

            Assert.True(simulation.Solver.LinkCount * 8L > 500_000,
                "the fixture has too few links for the old allocation to have been visible");


            long first = AllocatedBy(simulation);

            long second = AllocatedBy(simulation);

            Assert.True(first < 100_000,
                "the first step allocated " + first + " B; the prologue is no longer landing on the rebuild tick");
            Assert.True(first < second + 100_000,
                "the first step allocated " + first + " B against a later step's " + second + " B");
        }

        [Fact]

        public void PreparingEarlyMovesNoTemperature()
        {

            ThermalSimulation direct = Census(2000);

            ThermalSimulation prepared = Census(2000);
            LoadBenchmarks.SeedSpread(direct);
            LoadBenchmarks.SeedSpread(prepared);

            BlockInstance victimA = direct.Solver.Nodes[100].Block;
            BlockInstance victimB = prepared.Solver.Nodes[100].Block;
            Assert.True(victimA.Min == victimB.Min, "the two fixtures diverged before the comparison started");

            direct.RemoveBlock(victimA);
            prepared.RemoveBlock(victimB);
            prepared.Solver.PrepareForSteps();

            direct.StepExact(3, Worlds.Shadow());
            prepared.StepExact(3, Worlds.Shadow());

            int compared = 0;
            for (int i = 0; i < direct.Solver.Nodes.Count; i++)
            {
                float a = direct.Solver.Nodes[i].Temperature;
                float b = prepared.Solver.Nodes[i].Temperature;
                Assert.True(a == b, "node " + i + " reads " + b + " prepared and " + a + " direct");
                compared++;
            }
            Assert.True(compared > 1000, "only " + compared + " nodes compared, which proves nothing");

            float spread = 0f;
            for (int i = 0; i < direct.Solver.Nodes.Count; i++)
            {
                spread = Math.Max(spread, Math.Abs(direct.Solver.Nodes[i].Temperature - 293.15f));
            }
            Assert.True(spread > 10f, "every temperature is still ambient, so agreement proves nothing (`E8`)");
        }


        private static long AllocatedBy(ThermalSimulation simulation)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            simulation.StepExact(1, Worlds.Shadow());
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}

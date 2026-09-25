using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The block carries its node's index, and the dictionary that used to is the oracle.**
    ///
    /// <para>
    /// The solver kept a `Dictionary&lt;long, ThermalNode&gt;` from block key to node — about 49
    /// bytes a block, measured, for an answer the block can hold in four
    /// (backlog.md `E1`). A change whose whole purpose is cost has to be
    /// pinned against the code it replaced (`D8`), and here that code is reconstructible: the
    /// dictionary is exactly `node.Block.Key → node` over the live node list, so these build it and
    /// require the index to agree with it.
    /// </para>
    ///
    /// <para>
    /// **The agreement has to survive index motion**, which is where a hand-maintained index fails:
    /// a removal moves the last node into the hole through one of two paths, and both are exercised
    /// here. The fixture asserts its own preconditions for the reason `D8` gives — two lookups over
    /// a grid that built nothing agree perfectly.
    /// </para>
    /// </summary>
    public class NodeIndexTests
    {
        private readonly ITestOutputHelper output;


        public NodeIndexTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static GridBuilder Hull(int side)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(side, side, side));
            return builder;
        }


        private static Dictionary<long, ThermalNode> ByKey(ThermalSimulation simulation)
        {
            Dictionary<long, ThermalNode> byKey = new Dictionary<long, ThermalNode>();
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                byKey[nodes[i].Block.Key] = nodes[i];
            }

            return byKey;
        }


        private void RequireAgreement(GridBuilder builder, ThermalSimulation simulation, string when)
        {

            Dictionary<long, ThermalNode> byKey = ByKey(simulation);
            int judged = 0;

            for (int i = 0; i < builder.Placed.Count; i++)
            {
                BlockInstance block = builder.Placed[i];

                ThermalNode expected;
                if (!byKey.TryGetValue(block.Key, out expected)) expected = null;

                if (expected != null && !ReferenceEquals(expected.Block, block)) expected = null;

                ThermalNode actual = simulation.Solver.GetNode(block);
                judged++;

                Assert.True(ReferenceEquals(expected, actual),
                    when + ": block at " + block.Position + " resolves to "
                    + (actual == null ? "nothing" : "node " + actual.Index)
                    + " and the dictionary says "
                    + (expected == null ? "nothing" : "node " + expected.Index));
            }

            Assert.True(judged > 0, when + ": no block was judged (`E8`)");
            output.WriteLine("{0}: {1} blocks agree over {2} nodes", when, judged, simulation.Solver.Nodes.Count);
        }

        [Fact]

        public void EveryBlockResolvesToTheNodeTheDictionaryWouldHaveGiven()
        {

            GridBuilder builder = Hull(4);
            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            Assert.True(simulation.Solver.Nodes.Count > 50,
                "the hull built " + simulation.Solver.Nodes.Count + " nodes, which judges too little");

            RequireAgreement(builder, simulation, "as built");
        }

        [Fact]

        public void TheIndexFollowsANodeMovedByAnIncrementalRemoval()
        {

            GridBuilder builder = Hull(4);
            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            simulation.StepExact(2, Worlds.Shadow());

            int before = simulation.Solver.Nodes.Count;

            for (int i = 0; i < 5; i++)
            {
                BlockInstance victim = builder.Placed[0];
                builder.Placed.RemoveAt(0);
                Assert.NotNull(simulation.Solver.GetNode(victim));
                simulation.RemoveBlock(victim);

                Assert.Null(simulation.Solver.GetNode(victim));

                RequireAgreement(builder, simulation, "after removal " + (i + 1));
            }

            Assert.Equal(before - 5, simulation.Solver.Nodes.Count);
        }

        [Fact]

        public void TheIndexFollowsTheShiftWhenARebuildIsAlreadyDue()
        {

            GridBuilder builder = Hull(4);
            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);

            BlockInstance victim = builder.Placed[0];
            builder.Placed.RemoveAt(0);
            Assert.NotNull(simulation.Solver.GetNode(victim));
            simulation.RemoveBlock(victim);

            RequireAgreement(builder, simulation, "after a dirty-graph removal");

            simulation.RebuildAll();
            RequireAgreement(builder, simulation, "after the rebuild");
        }

        [Fact]

        public void AnInstanceSharedWithASecondSolverResolvesToNothingRatherThanToTheWrongNode()
        {

            GridBuilder builder = Hull(3);
            ThermalSimulation first = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            first.RebuildAll();

            BlockInstance shared = builder.Placed[0];
            Assert.NotNull(first.Solver.GetNode(shared));

            ThermalSimulation second = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            second.RebuildAll();

            ThermalNode inSecond = second.Solver.GetNode(shared);
            Assert.NotNull(inSecond);
            Assert.True(ReferenceEquals(inSecond.Block, shared));

            ThermalNode inFirst = first.Solver.GetNode(shared);
            if (inFirst != null)
            {
                Assert.True(ReferenceEquals(inFirst.Block, shared),
                    "the first solver resolved a shared instance to a different block's node");
            }
        }
    }
}

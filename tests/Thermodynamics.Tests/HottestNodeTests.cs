using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The hottest-block readout, which is now a by-product of the step rather than a pass of its
    /// own.
    ///
    /// It feeds the cockpit summary, the crosshair readout and the telemetry report, and it was
    /// being recomputed on a cadence by walking every node — for every client, on every grid. The
    /// step's write-back already has every temperature in its hands, so the answer costs one
    /// comparison per node instead of a second pass.
    ///
    /// What that buys has to be paid for in care: a cached index is only as good as the list it
    /// indexes, and the list changes under it.
    /// </summary>
    public class HottestNodeTests
    {

        private static ThermalSimulation Build()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));
            return builder.BuildSimulation(new ThermalSettings(), 293.15f);
        }


        private static ThermalNode HottestByWalking(ThermalSimulation simulation)
        {
            ThermalNode found = null;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = simulation.Solver.Nodes[i];
                if (found == null || node.Temperature > found.Temperature) found = node;
            }
            return found;
        }

        [Fact]

        public void ItIsTheHottestBlockAfterAStep()
        {

            ThermalSimulation simulation = Build();

            simulation.Solver.GetNodeAt(new Vector3I(2, 2, 2)).Temperature = 900f;
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(HottestByWalking(simulation), simulation.Solver.HottestNode());
        }

        [Fact]

        public void ItIsRightBeforeAnythingHasStepped()
        {

            ThermalSimulation simulation = Build();
            simulation.Solver.GetNodeAt(new Vector3I(1, 0, 3)).Temperature = 750f;

            Assert.Equal(HottestByWalking(simulation), simulation.Solver.HottestNode());
        }

        [Fact]

        public void ItSurvivesTheHottestBlockBeingDestroyed()
        {

            ThermalSimulation simulation = Build();

            BlockInstance doomed = simulation.Grid.GetAtCell(new Vector3I(2, 2, 2));
            simulation.Solver.GetNode(doomed).Temperature = 1200f;
            simulation.Solver.GetNodeAt(new Vector3I(0, 0, 0)).Temperature = 800f;
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(doomed, simulation.Solver.HottestNode().Block);

            simulation.RemoveBlock(doomed);

            ThermalNode hottest = simulation.Solver.HottestNode();
            Assert.NotNull(hottest);
            Assert.NotEqual(doomed, hottest.Block);
            Assert.Equal(HottestByWalking(simulation), hottest);
        }

        [Fact]

        public void ItFollowsTheHeatAsItMoves()
        {

            ThermalSimulation simulation = Build();

            BlockInstance reactor = simulation.Grid.GetAtCell(new Vector3I(0, 0, 0));
            reactor.PowerProducedWatts = 5f * ThermalConstants.MegawattsToWatts;
            simulation.Solver.GetNode(reactor).RefreshHeatGeneration();

            simulation.Solver.GetNodeAt(new Vector3I(3, 3, 3)).Temperature = 600f;

            for (int step = 0; step < 40; step++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                Assert.Equal(HottestByWalking(simulation), simulation.Solver.HottestNode());
            }
        }
    }
}

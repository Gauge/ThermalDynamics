using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Waste heat, solar gain and friction are all fixed for the length of a step — none of them
    /// depends on a node's temperature — so the environment pass carries their sum as one row and
    /// reads that instead of three arrays per node per substep. The pass is bound by how many node
    /// arrays it streams, and the three separate rows survive only for the diagnostics substep.
    ///
    /// <para>
    /// The risk that buys is a row that disagrees with its parts: the reported heat gain would
    /// then be a different number from the four figures a player sees beside it, and nothing else
    /// in the model would notice. These pin the two together, on a hull where all three terms are
    /// live and on one where only waste heat is.
    /// </para>
    /// </summary>
    public class FixedSourceRowTests
    {

        private static ThermalSimulation Hull()
        {
            ThermalSimulation simulation = Hulls.Driven();
            simulation.Solver.CollectDiagnostics = true;
            return simulation;
        }


        private static float SumOfParts(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            float total = 0f;

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];
                total += node.HeatGenerationWatts
                    + node.LastSolarWatts
                    + node.LastFrictionWatts
                    + node.LastHeatSourceWatts;
            }

            return total;
        }

        [Fact]

        public void TheReportedHeatGainIsTheSumOfTheFiguresBesideIt()
        {

            ThermalSimulation simulation = Hull();

            simulation.StepExact(6,
                Worlds.PlanetSurface(0.8f, timeOfDay: 0.35f, windSpeed: 140f));


            float parts = SumOfParts(simulation);

            Assert.True(parts > 0f, "the hull gained nothing, so this test asserts nothing");
            Assert.Equal(parts, simulation.HeatGainWatts, 1);
        }

        [Fact]

        public void ABuriedProducerStillReportsItsWasteHeat()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            builder.Place(Catalog.Reactor(), Vector3I.One)
                   .Producing(4f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();
            simulation.Solver.CollectDiagnostics = true;

            ThermalNode reactor = simulation.Solver.Nodes[simulation.Solver.Nodes.Count - 1];
            Assert.Equal(0, reactor.TotalExposedFaces);

            simulation.StepExact(4, Worlds.Space(new Vector3(0f, 1f, 0f)));

            Assert.Equal(0f, reactor.LastSolarWatts);
            Assert.Equal(0f, reactor.LastFrictionWatts);
            Assert.Equal(SumOfParts(simulation), simulation.HeatGainWatts, 1);
            Assert.True(reactor.HeatGenerationWatts > 0f);
        }

        [Fact]

        public void SwitchingSolarAndFrictionOffEmptiesTheirShareOfTheRow()
        {

            ThermalSimulation simulation = Hull();
            EnvironmentSample sample = Worlds.PlanetSurface(0.8f, timeOfDay: 0.35f, windSpeed: 140f);

            simulation.StepExact(4, sample);
            float lit = simulation.HeatGainWatts;

            simulation.Settings.EnableSolarHeat = false;
            simulation.Settings.EnableFriction = false;
            simulation.Settings.Derive();

            simulation.StepExact(4, sample);

            Assert.True(simulation.HeatGainWatts < lit,
                "gain did not fall when solar and friction were switched off");
            Assert.Equal(SumOfParts(simulation), simulation.HeatGainWatts, 1);
        }
    }
}

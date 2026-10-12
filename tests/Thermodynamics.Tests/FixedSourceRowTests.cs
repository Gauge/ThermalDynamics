using Thermodynamics.Fixtures;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Waste heat and solar gain are fixed for a step and share a cached source row.
    /// Provider heating is evaluated separately each substep because it can depend on temperature.
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
        [Fact]
        public void SmallProviderHeatIsNotCancelledByExposedArea()
        {
            const float heatingWatts = 0.00000001f;
            ThermalSettings settings = new ThermalSettings
            {
                EnableEnvironment = false,
                EnableSolarHeat = false,
                EnableConduction = false,
                EnableDamage = false,
                EnableWasteHeat = false,
                SurfaceHeating = (area, density, speed, ambient, temperature, capacity, seconds) => heatingWatts,
            };
            settings.Derive();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(4, Worlds.Flight(1f, 200f));

            Assert.True(simulation.FrictionWatts > 0f);
            Assert.Equal(simulation.FrictionWatts, simulation.HeatGainWatts);
        }

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

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
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
            simulation.Settings.SurfaceHeating = null;
            simulation.Settings.Derive();

            simulation.StepExact(4, sample);

            Assert.True(simulation.HeatGainWatts < lit,
                "gain did not fall when solar and friction were switched off");
            Assert.Equal(SumOfParts(simulation), simulation.HeatGainWatts, 1);
        }
    }
}

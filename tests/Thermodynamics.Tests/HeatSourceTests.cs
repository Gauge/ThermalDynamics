using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Point heat sources: the sun's maths applied to something a mod owns. What a source is
    /// bound to, where it is and whether anything is in the way are all the host's business; by
    /// the time the simulation sees one it is a direction and an irradiance.
    /// </summary>
    public class HeatSourceTests
    {

        private static EnvironmentSample WithSource(Vector3 direction, float irradiance)
        {
            EnvironmentSample sample = Worlds.Shadow();

            sample.HeatSources = new HeatSourceState[] { new HeatSourceState(direction, irradiance) };
            sample.HeatSourceCount = 1;
            return sample;
        }


        private static ThermalSimulation OneBlock(ThermalSettings settings)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            return builder.BuildSimulation(settings, 200f);
        }

        [Fact]

        public void ASourceHeatsAnExposedBlock()
        {

            ThermalSimulation simulation = OneBlock(new ThermalSettings());
            simulation.StepExact(20, WithSource(Vector3.Right, 5000f));

            Assert.True(simulation.Solver.Nodes[0].Temperature > 200f);
        }

        [Fact]

        public void NoSourceMeansNoGain()
        {

            ThermalSimulation lit = OneBlock(new ThermalSettings());
            lit.StepExact(20, WithSource(Vector3.Right, 5000f));


            ThermalSimulation dark = OneBlock(new ThermalSettings());
            dark.StepExact(20, Worlds.Shadow());

            Assert.True(lit.Solver.Nodes[0].Temperature > dark.Solver.Nodes[0].Temperature);
        }

        [Fact]

        public void TheSwitchTurnsThemOff()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableHeatSources = false;
            settings.Derive();


            ThermalSimulation off = OneBlock(settings);
            off.StepExact(20, WithSource(Vector3.Right, 5000f));


            ThermalSimulation none = OneBlock(new ThermalSettings());
            none.StepExact(20, Worlds.Shadow());

            Assert.Equal(none.Solver.Nodes[0].Temperature, off.Solver.Nodes[0].Temperature, 4);
        }

        [Fact]

        public void BrighterSourcesDeliverMore()
        {

            ThermalSimulation dim = OneBlock(new ThermalSettings());
            dim.StepExact(20, WithSource(Vector3.Right, 1000f));


            ThermalSimulation bright = OneBlock(new ThermalSettings());
            bright.StepExact(20, WithSource(Vector3.Right, 8000f));

            Assert.True(bright.Solver.Nodes[0].Temperature > dim.Solver.Nodes[0].Temperature);
        }

        [Fact]

        public void SourcesAddToOneAnother()
        {
            EnvironmentSample two = Worlds.Shadow();
            two.HeatSources = new HeatSourceState[]
            {

                new HeatSourceState(Vector3.Right, 4000f),

                new HeatSourceState(Vector3.Left, 4000f),
            };
            two.HeatSourceCount = 2;


            ThermalSimulation pair = OneBlock(new ThermalSettings());
            pair.StepExact(20, two);


            ThermalSimulation single = OneBlock(new ThermalSettings());
            single.StepExact(20, WithSource(Vector3.Right, 4000f));

            Assert.True(pair.Solver.Nodes[0].Temperature > single.Solver.Nodes[0].Temperature);
        }

        [Fact]

        public void CountBoundsTheBufferSoAHostCanReuseIt()
        {
            EnvironmentSample sample = Worlds.Shadow();
            sample.HeatSources = new HeatSourceState[]
            {

                new HeatSourceState(Vector3.Right, 4000f),

                new HeatSourceState(Vector3.Left, 100000f),
            };
            sample.HeatSourceCount = 1;


            ThermalSimulation bounded = OneBlock(new ThermalSettings());
            bounded.StepExact(20, sample);


            ThermalSimulation reference = OneBlock(new ThermalSettings());
            reference.StepExact(20, WithSource(Vector3.Right, 4000f));

            Assert.Equal(reference.Solver.Nodes[0].Temperature, bounded.Solver.Nodes[0].Temperature, 4);
        }

        [Fact]

        public void ABuriedBlockWithNoExposedFaceGainsNothing()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 200f);
            ThermalNode centre = simulation.Solver.GetNodeAt(new Vector3I(1, 1, 1));
            Assert.Equal(0, centre.TotalExposedFaces);

            simulation.StepExact(5, WithSource(Vector3.Right, 20000f));
            Assert.Equal(0f, centre.LastHeatSourceWatts, 5);
        }
    }
}

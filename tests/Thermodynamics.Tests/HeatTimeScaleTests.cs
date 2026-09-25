using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Specific heat is stated in real J/(kg K), and the game's pace comes from one global
    /// factor that divides every heat capacity.
    ///
    /// The claim that makes that honest is that dividing capacity is <em>exactly</em> running
    /// thermal time faster: nothing about the physics changes, only the clock. These tests hold
    /// that claim to account, because if it is wrong the setting is not a time scale, it is a
    /// silent rebalance.
    /// </summary>
    public class HeatTimeScaleTests
    {

        private static ThermalSettings Settings(float scale, int frequency = 4)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.HeatTimeScale = scale;
            settings.Frequency = frequency;
            return settings.Derive();
        }


        private static ThermalSimulation Cube(ThermalSettings settings, float temperature)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            ThermalSimulation simulation = builder.BuildSimulation(settings, temperature);
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void CapacityIsDividedByTheScale()
        {

            ThermalSimulation physical = Cube(Settings(1f), 300f);

            ThermalSimulation accelerated = Cube(Settings(225f), 300f);

            float a = physical.Solver.Nodes[0].ThermalMass;
            float b = accelerated.Solver.Nodes[0].ThermalMass;

            Assert.Equal(225f, a / b, 2);
        }

        [Fact]

        public void ScalingCapacityIsTheSameAsRunningTimeFaster()
        {
            const int scale = 20;


            ThermalSimulation fast = Cube(Settings(scale), 800f);

            ThermalSimulation slow = Cube(Settings(1f), 800f);

            fast.StepExact(60 * 4, Worlds.Shadow());
            slow.StepExact(60 * 4 * scale, Worlds.Shadow());

            IList<ThermalNode> a = fast.Solver.Nodes;
            IList<ThermalNode> b = slow.Solver.Nodes;

            for (int i = 0; i < a.Count; i++)
            {
                float relative = Math.Abs(a[i].Temperature - b[i].Temperature) / b[i].Temperature;
                Assert.True(relative < 0.005f,
                    "node " + i + ": " + a[i].Temperature + " vs " + b[i].Temperature);
            }
        }

        [Fact]

        public void EquilibriumDoesNotDependOnTheScale()
        {
            float[] scales = new float[] { 50f, 225f, 1000f };
            float first = 0f;

            for (int s = 0; s < scales.Length; s++)
            {
                GridBuilder builder = GridBuilder.Large();
                builder.Fill(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(3, 3, 3));

                BlockInstance centre = builder.Grid.GetAtCell(Vector3I.Zero);
                builder.Grid.Remove(centre);
                builder.Placed.Remove(centre);
                builder.Place(Catalog.Reactor(), Vector3I.Zero)
                       .Producing(5f * ThermalConstants.MegawattsToWatts);

                ThermalSimulation simulation = builder.BuildSimulation(Settings(scales[s]), 293.15f);
                simulation.RebuildAll();

                simulation.StepExact(30000, Worlds.Shadow());

                float reactor = simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature;

                if (s == 0) first = reactor;
                else Assert.Equal(first, reactor, 0);
            }
        }

        [Fact]

        public void AHigherScaleCostsSubstepsNotAccuracy()
        {

            ThermalSimulation gentle = Cube(Settings(1f), 800f);

            ThermalSimulation harsh = Cube(Settings(2000f), 800f);

            gentle.StepExact(40, Worlds.Shadow());
            harsh.StepExact(40, Worlds.Shadow());

            Assert.True(harsh.Solver.LastSubsteps >= gentle.Solver.LastSubsteps);

            foreach (ThermalNode node in harsh.Solver.Nodes)
            {
                Assert.False(float.IsNaN(node.Temperature));
                Assert.True(node.Temperature >= ThermalConstants.MinimumTemperature);
                Assert.True(node.Temperature <= 800f, "nothing may heat itself by being accelerated");
            }
        }

        [Fact]

        public void CoolantRunsOnTheSameClockAsTheBlocksItCools()
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();
            properties.SpecificHeat = 3400f;
            properties.CoolantMassPerPipe = 500f;


            CoolantLoop physical = new CoolantLoop(properties, 300f, 1f);

            CoolantLoop accelerated = new CoolantLoop(properties, 300f, new ThermalSettings().HeatTimeScale);

            Assert.Equal(new ThermalSettings().HeatTimeScale,
                physical.ThermalMass / accelerated.ThermalMass, 2);
        }

        [Fact]

        public void AnInvalidScaleFallsBackToPhysical()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.HeatTimeScale = 0f;
            settings.Derive();

            Assert.Equal(1f, settings.HeatTimeScale);


            ThermalSettings negative = new ThermalSettings();
            negative.HeatTimeScale = -5f;
            negative.Derive();

            Assert.Equal(1f, negative.HeatTimeScale);
        }

        [Fact]

        public void TheShippedClockIsSlowerThanTheOneTheConversionReproduced()
        {

            ThermalSettings settings = new ThermalSettings();

            const float steel = 450f;
            const float legacyValue = 2f;
            const float conversionClock = 225f;

            Assert.Equal(legacyValue, steel / conversionClock, 3);
            Assert.Equal(90f, settings.HeatTimeScale, 3);
            Assert.Equal(2.5f, conversionClock / settings.HeatTimeScale, 3);
        }
    }
}

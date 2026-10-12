using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Production defaults keep thermal mechanisms enabled, bound work per frame and install
    /// no aerodynamic heating provider. Historical heating rigs select their provider explicitly.
    /// Physical bounds and the deliberately constrained six-neighbour case remain checked here.
    /// </summary>
    [Trait("speed", "slow")]
    public class DefaultSettingsTests
    {
        private readonly ITestOutputHelper output;


        public DefaultSettingsTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static ThermalSimulation BuildDefault()
        {

            ThermalSettings settings = new ThermalSettings();

            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            int index = 0;
            foreach (Vector3I cell in GridShapes.Ship(fuselageLength: 18, fuselageWidth: 7, bulkheadSpacing: 6))
            {
                builder.Place((index++ % 8) == 0 ? Catalog.Grating() : Catalog.HeavyArmor(), cell);
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());
            return simulation;
        }

        [Fact]

        public void TheDefaultsAreTheMostFaithfulConfiguration()
        {

            ThermalSettings shipped = new ThermalSettings();

            Assert.True(shipped.EnableEnvironment);
            Assert.True(shipped.EnableConduction);
            Assert.True(shipped.EnableRadiation);
            Assert.True(shipped.EnableConvection);
            Assert.True(shipped.EnableSolarHeat);
            Assert.True(shipped.EnableHeatSources);
            Assert.True(shipped.EnableWasteHeat);
            Assert.True(shipped.EnablePlanets);
            Assert.Null(shipped.SurfaceHeating);
            Assert.False(shipped.AcceptSurfaceHeating(200f));
            Assert.True(shipped.EnableDamage);
            Assert.True(shipped.EnableCoolantLoops);
            Assert.True(shipped.EnableRoomAir);
            Assert.True(shipped.EnableHeatPumps);

            Assert.True(shipped.SolarSelfShadowing);
            Assert.False(shipped.WellMixedCoolant);
            Assert.True(shipped.DamageIsPerSecond);

            Assert.Equal(0, shipped.MaxSubstepsPerBlock);

            Assert.True(shipped.MaxSubsteps >= 64,
                "MaxSubsteps of " + shipped.MaxSubsteps + " refuses the stability estimate in more"
                + " than the thick-air ceiling this is priced against");

            Assert.True(shipped.ClampConductionOvershoot);
            Assert.True(shipped.ClampEnvironmentOvershoot);

            Assert.Equal(1f, shipped.SimulationSpeed);
            Assert.Equal(4, shipped.Frequency);

            output.WriteLine("shipped: Frequency " + shipped.Frequency
                + ", SimulationSpeed " + shipped.SimulationSpeed
                + ", HeatTimeScale " + shipped.HeatTimeScale.ToString("n0")
                + ", MaxSubsteps " + shipped.MaxSubsteps
                + ", MaxSubstepsPerBlock " + shipped.MaxSubstepsPerBlock);
        }

        [Fact]

        public void TheDefaultIsFrameBounded()
        {

            ThermalSettings shipped = new ThermalSettings();

            Assert.True(shipped.MaxElementVisitsPerStep > 0,
                "the shipped default must bound a step's cost");
        }

        [Fact]

        public void TheDefaultsStayPhysicalWithTheEnvironmentOn()
        {

            ThermalSimulation simulation = BuildDefault();

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++) nodes[i].Temperature = 293.15f;
            nodes[nodes.Count / 2].Temperature = 1200f;

            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));

            float lowest = float.MaxValue;
            float highest = float.MinValue;

            for (int frame = 0; frame < 60 * 60; frame++)
            {
                simulation.Update(1f / 60f, sample);

                for (int i = 0; i < nodes.Count; i++)
                {
                    float t = nodes[i].Temperature;
                    Assert.False(float.IsNaN(t) || float.IsInfinity(t),
                        "block " + i + " reached " + t + " after " + frame + " frames");

                    if (t < lowest) lowest = t;
                    if (t > highest) highest = t;
                }
            }

            output.WriteLine("defaults: " + lowest.ToString("n1") + " K to "
                + highest.ToString("n1") + " K over a minute.");

            Assert.True(lowest > 1f, "a block was driven to " + lowest + " K");
            Assert.True(highest <= 1201f, "heat was invented, reaching " + highest + " K");
        }

        [Fact]

        public void ANodeWithSixNeighboursStaysBoundedWhenItsSubstepsAreRefused()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Frequency = 4;
            settings.HeatTimeScale = 100000f;
            settings.MaxSubsteps = 1;
            settings.MaxElementVisitsPerStep = 0;
            settings.EnableEnvironment = false;
            settings.EnableRadiation = false;
            settings.EnableConvection = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.HeavyArmor();

            builder.Place(armour, Vector3I.Zero);
            for (int face = 0; face < Face.Count; face++)
            {
                builder.Place(armour, Face.Offsets[face]);
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 1000f;

            Assert.Equal(6, simulation.Solver.GetNodeAt(Vector3I.Zero).LinkCount);

            float before = simulation.Solver.TotalEnergy;
            simulation.StepExact(200, Worlds.Shadow());
            float after = simulation.Solver.TotalEnergy;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                float t = nodes[i].Temperature;
                Assert.False(float.IsNaN(t) || float.IsInfinity(t), "block " + i + " diverged");
                Assert.InRange(t, 1f, 1001f);
            }

            output.WriteLine("six neighbours, one substep, transfer x100,000: energy "
                + before.ToString("n0") + " -> " + after.ToString("n0"));

            float drift = Math.Abs(after - before) / Math.Max(1f, Math.Abs(before));
            Assert.True(drift < 1e-3f,
                "scaling an exchange must keep it equal and opposite; energy moved by " + drift);
        }

    }
}

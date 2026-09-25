using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Shared setup for solver tests.</summary>
    public static class Fixture
    {

        public static ThermalSettings ConductionOnly(int frequency = 4)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.Frequency = frequency;
            return settings.Derive();
        }


        public static ThermalSettings EnvironmentOnly(int frequency = 4)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.Frequency = frequency;
            return settings.Derive();
        }


        public static BlockModel Foil(float mass = 10f)
        {
            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.Conductivity = 83.333f;
            return BlockModel.Solid("Foil", Vector3I.One, mass, thermal);
        }
    }

    /// <summary>
    /// Heat between two touching blocks: the conductance, and the four properties of the flow it produces.
    ///
    /// <para>
    /// Symmetry, conservation and order independence are asserted rather than assumed, because the
    /// original model had none of the three and every one of its failures looked like a plausible
    /// temperature. The last two cases run the original formulas from LegacyFormulas to show exactly
    /// what changed.
    /// </para>
    /// </summary>
    public class ConductionTests
    {
        [Fact]

        public void ConductanceMatchesTheSeriesFormula()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());

            Assert.Single(simulation.Solver.Links);
            ThermalLink link = simulation.Solver.Links[0];

            float k = Catalog.LightArmor().Thermal.Conductivity * ThermalConstants.ConductionScale;
            float expected = 6.25f / ((1.25f / k) * 2f);
            Assert.Equal(expected, link.Conductance, 2);
            Assert.Equal(1, link.ContactFaces);
        }

        [Fact]

        public void ConductanceIsSymmetricWhicheverWayItIsBuilt()
        {
            GridBuilder forwards = GridBuilder.Large();
            forwards.Place(Catalog.LightArmor(), Vector3I.Zero);
            forwards.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));

            GridBuilder backwards = GridBuilder.Large();
            backwards.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));
            backwards.Place(Catalog.LightArmor(), Vector3I.Zero);

            float a = forwards.BuildSimulation(Fixture.ConductionOnly()).Solver.Links[0].Conductance;
            float b = backwards.BuildSimulation(Fixture.ConductionOnly()).Solver.Links[0].Conductance;

            Assert.Equal(a, b, 3);
        }

        [Fact]

        public void ContactAreaScalesWithSharedFaces()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel wall = BlockModel.Solid("Wall", new Vector3I(1, 3, 1), 1500f, Catalog.DefaultThermal());
            builder.Place(wall, Vector3I.Zero);
            builder.Place(wall, new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
            ThermalLink link = simulation.Solver.Links[0];

            Assert.Equal(3, link.ContactFaces);
        }

        [Fact]

        public void BlocksWithoutMountSurfacesDoNotConduct()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Radiator(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());

            Assert.Empty(simulation.Solver.Links);
        }

        [Fact]

        public void HeatFlowsFromHotToColdAndStops()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
            ThermalNode hot = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode cold = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));

            hot.Temperature = 400f;
            cold.Temperature = 300f;

            simulation.StepExact(200, Worlds.Shadow());

            Assert.Equal(350f, hot.Temperature, 1);
            Assert.Equal(350f, cold.Temperature, 1);
        }

        [Fact]

        public void EquilibriumIsWeightedByThermalMass()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
            ThermalNode light = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode heavy = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));

            light.Temperature = 400f;
            heavy.Temperature = 300f;

            float expected = ((400f * light.ThermalMass) + (300f * heavy.ThermalMass))
                / (light.ThermalMass + heavy.ThermalMass);

            simulation.StepExact(600, Worlds.Shadow());

            Assert.Equal(expected, light.Temperature, 1);
            Assert.Equal(expected, heavy.Temperature, 1);
        }

        [Fact]

        public void ConductionConservesEnergyExactly()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());

            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 900f;

            float before = simulation.Solver.TotalEnergy;
            simulation.StepExact(400, Worlds.Shadow());
            float after = simulation.Solver.TotalEnergy;

            Assert.Equal(1f, after / before, 3);
        }

        [Fact]

        public void EnergyIsNotConservedWhenTheBlockPopulationChanges()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());

            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 900f;
            simulation.StepExact(40, Worlds.Shadow());

            ThermalNode leaving = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode neighbour = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));

            float before = simulation.Solver.TotalEnergy;
            float carriedOff = leaving.Energy;
            float neighbourBefore = neighbour.Temperature;

            Assert.True(carriedOff > 0f, "the departing block holds no energy, so this proves nothing");

            simulation.RemoveBlock(leaving.Block);

            Assert.Equal(before - carriedOff, simulation.Solver.TotalEnergy, 0);

            Assert.Equal(neighbourBefore,
                simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0)).Temperature, 4);

            float beforeWeld = simulation.Solver.TotalEnergy;

            BlockInstance welded = new BlockInstance(
                Catalog.LightArmor(), Vector3I.Zero, BlockOrientation.Identity);
            ThermalNode arriving = simulation.AddBlock(welded);

            Assert.Equal(simulation.DefaultTemperature, arriving.Temperature, 4);
            Assert.Equal(beforeWeld + arriving.Energy, simulation.Solver.TotalEnergy, 0);
        }

        [Fact]

        public void ConductionResultDoesNotDependOnBlockOrder()
        {
            float[] results = new float[2];

            for (int pass = 0; pass < 2; pass++)
            {
                GridBuilder builder = GridBuilder.Large();
                if (pass == 0)
                {
                    for (int x = 0; x < 6; x++) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));
                }
                else
                {
                    for (int x = 5; x >= 0; x--) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));
                }

                ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
                simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 800f;
                simulation.StepExact(40, Worlds.Shadow());

                results[pass] = simulation.Solver.GetNodeAt(new Vector3I(5, 0, 0)).Temperature;
            }

            Assert.Equal(results[0], results[1], 4);
        }

        [Fact]

        public void OriginalConductionIgnoredThermalMass()
        {
            LegacyFormulas.Cell light = new LegacyFormulas.Cell
            {
                Conductivity = 0.6f, SpecificHeat = 2f, Mass = 500f, GridSize = 2.5f, Temperature = 300f
            };
            LegacyFormulas.Cell heavy = new LegacyFormulas.Cell
            {
                Conductivity = 0.6f, SpecificHeat = 2f, Mass = 50000f, GridSize = 2.5f, Temperature = 300f
            };
            LegacyFormulas.Cell source = new LegacyFormulas.Cell
            {
                Conductivity = 0.6f, SpecificHeat = 2f, Mass = 500f, GridSize = 2.5f, Temperature = 400f
            };

            float lightDelta = LegacyFormulas.ConductionDelta(light, source, 1, 0.25f);
            float heavyDelta = LegacyFormulas.ConductionDelta(heavy, source, 1, 0.25f);

            Assert.Equal(lightDelta, heavyDelta, 6);

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));
            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());

            ThermalNode a = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode b = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));
            a.Temperature = 400f;
            b.Temperature = 300f;

            simulation.StepExact(1, Worlds.Shadow());

            Assert.True(Math.Abs(a.LastDeltaTemperature) > Math.Abs(b.LastDeltaTemperature),
                "the lighter block must move further per joule");
        }

        [Fact]

        public void OriginalConductionDidNotConserveEnergy()
        {
            LegacyFormulas.Cell small = new LegacyFormulas.Cell
            {
                Conductivity = 1f, SpecificHeat = 2f, Mass = 500f, GridSize = 2.5f,
                Extents = Vector3I.One, Temperature = 300f
            };
            LegacyFormulas.Cell large = new LegacyFormulas.Cell
            {
                Conductivity = 1f, SpecificHeat = 2f, Mass = 10000f, GridSize = 2.5f,

                Extents = new Vector3I(3, 3, 4), Temperature = 400f
            };

            float smallGain = LegacyFormulas.ConductionDelta(small, large, 1, 0.25f) * small.SpecificHeat * small.Mass;
            float largeLoss = -LegacyFormulas.ConductionDelta(large, small, 1, 0.25f) * large.SpecificHeat * large.Mass;

            float mismatch = Math.Abs(smallGain - largeLoss) / Math.Abs(largeLoss);
            Assert.True(mismatch > 0.2f,
                "expected a large mismatch, got " + smallGain + " vs " + largeLoss);

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(BlockModel.Solid("Bulk", new Vector3I(3, 3, 4), 10000f, Catalog.DefaultThermal()),

                new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
            ThermalNode a = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode b = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 0));
            a.Temperature = 300f;
            b.Temperature = 400f;

            float before = simulation.Solver.TotalEnergy;
            simulation.StepExact(1, Worlds.Shadow());
            float after = simulation.Solver.TotalEnergy;

            Assert.Equal(1f, after / before, 5);
        }
    }

    /// <summary>
    /// The integrator under step sizes it was never meant to see.
    ///
    /// <para>
    /// An explicit scheme on a stiff grid diverges rather than degrading, so the substep estimate and
    /// the overshoot clamp are the two things standing between a slow server and a ship at 10^30 K.
    /// WithoutTheClampAnAbsurdStepBlowsUp exists so the clamp cannot be removed as an optimisation
    /// without something saying what it was for.
    /// </para>
    /// </summary>
    public class StabilityTests
    {
        [Fact]

        public void ASmallStepNeedsNoSubstepping()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly(4));
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(1, simulation.Solver.LastSubsteps);
        }

        [Fact]

        public void AStiffGridAutomaticallySubsteps()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Fixture.Foil(), Vector3I.Zero);
            builder.Place(Fixture.Foil(), new Vector3I(1, 0, 0));

            ThermalSettings settings = Fixture.ConductionOnly(1);
            ThermalSimulation simulation = builder.BuildSimulation(settings);

            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 900f;
            simulation.StepExact(1, Worlds.Shadow());

            Assert.True(simulation.Solver.LastSubsteps > 1);
        }

        [Fact]

        public void AnAbsurdStepStaysBoundedAndConservesEnergy()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 8; x++) builder.Place(Fixture.Foil(1f), new Vector3I(x, 0, 0));

            ThermalSettings settings = Fixture.ConductionOnly(1);
            ThermalSimulation simulation = builder.BuildSimulation(settings);

            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 1000f;
            float before = simulation.Solver.TotalEnergy;

            for (int i = 0; i < 50; i++)
            {
                simulation.Solver.Step(10f, EnvironmentState.Vacuum(2.7f));
            }

            float after = simulation.Solver.TotalEnergy;
            Assert.Equal(1f, after / before, 2);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.False(float.IsNaN(nodes[i].Temperature));
                Assert.InRange(nodes[i].Temperature, 0f, 1000f);
            }
        }

        [Fact]

        public void WithoutTheClampAnAbsurdStepBlowsUp()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 8; x++) builder.Place(Fixture.Foil(1f), new Vector3I(x, 0, 0));

            ThermalSettings settings = Fixture.ConductionOnly(1);
            settings.ClampConductionOvershoot = false;

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            simulation.Settings.MaxSubsteps = 1;
            simulation.Settings.Derive();
            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 1000f;

            for (int i = 0; i < 30; i++)
            {
                simulation.Solver.Step(10f, EnvironmentState.Vacuum(2.7f));
            }

            float hottest = simulation.Solver.HottestNode().Temperature;
            Assert.True(hottest > 100000f || float.IsInfinity(hottest) || float.IsNaN(hottest),
                "expected divergence without the clamp, got " + hottest);
        }
    }
}

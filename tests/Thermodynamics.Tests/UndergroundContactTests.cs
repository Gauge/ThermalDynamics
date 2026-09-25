using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a buried grid is actually against.
    ///
    /// <para>
    /// It used to be air. A grid under the surface took the planet's ambient from the rock — which
    /// is right — and exchanged with it at the planet's *convection* coefficient, which is the
    /// figure for moving air. Rock contact was not modelled, so 50 W/(m² K) was the nearest
    /// available answer. backlog.md `A16`.
    /// </para>
    ///
    /// <para>
    /// **It was wrong by a factor of twenty-five and in the flattering direction**: digging in was
    /// the best cooling in the game. For a body of size D buried far from a surface the conduction
    /// shape factor gives `h = 2k/D`, and rock at 2.5 W/(m K) over a 2.5 m block is 2 W/(m² K), not
    /// 50.
    /// </para>
    /// </summary>
    public class UndergroundContactTests
    {

        private static EnvironmentState At(float depth, float windSpeed = 0f)
        {
            EnvironmentSample sample = Worlds.PlanetSurface(1f, 0.5f, windSpeed);
            sample.Depth = depth;
            sample.IsUnderground = depth > 0f;

            return EnvironmentSolver.Solve(

                new ThermalSettings().Derive(), PlanetThermalProperties.Default(), sample);
        }

        [Fact]

        public void RockShedsFarLessThanAir()
        {

            float air = At(0f).ConvectionCoefficient;

            float rock = At(50f).ConvectionCoefficient;

            Assert.True(air > 0f, "the surface case has to be exchanging with something");
            Assert.True(rock < air / 10f,
                "burying a grid should cost it most of its cooling, not leave it unchanged: "
                + rock.ToString("n1") + " against " + air.ToString("n1") + " W/(m^2 K)");

            PlanetThermalProperties planet = PlanetThermalProperties.Default();
            Assert.Equal(planet.UndergroundConvectionCoefficient, rock, 2);
        }

        [Fact]

        public void TheCrossoverIsAGradientRatherThanAStep()
        {

            float surface = At(0f).ConvectionCoefficient;

            float deep = At(ThermalConstants.UndergroundContactDepth * 4f).ConvectionCoefficient;
            float range = surface - deep;

            Assert.True(range > 0f, "there has to be a crossover to measure");

            float previous = surface;
            bool moved = false;

            for (float depth = 0.5f; depth <= ThermalConstants.UndergroundContactDepth * 2f;
                 depth += 0.5f)
            {

                float current = At(depth).ConvectionCoefficient;

                Assert.True(current <= previous + 1e-3f,
                    "going deeper must not improve cooling: " + current + " at " + depth + " m");

                float step = Math.Abs(previous - current);
                Assert.True(step < range * 0.2f,
                    "the coefficient stepped by " + step.ToString("n1") + " W/(m^2 K) at "
                    + depth + " m, a fifth of the whole crossover in half a metre");

                if (step > 1e-3f) moved = true;
                previous = current;
            }

            Assert.True(moved, "nothing changed with depth, so this test is asserting nothing");
            Assert.Equal(PlanetThermalProperties.Default().UndergroundConvectionCoefficient,
                previous, 2);
        }

        [Fact]

        public void NeitherWindNorWeatherReachesABuriedGrid()
        {

            float still = At(50f).ConvectionCoefficient;

            float gale = At(50f, 60f).ConvectionCoefficient;

            Assert.Equal(still, gale, 3);

            Assert.True(At(0f, 60f).ConvectionCoefficient > At(0f).ConvectionCoefficient,
                "wind should raise the surface coefficient, or this test is blind");
        }

        [Fact]

        public void TheSameHullRunsHotterBuriedThanInTheOpen()
        {
            Assert.True(Settled(50f) > Settled(0f) + 5f,
                "a buried hull should run hotter than one in the open: "

                + Settled(50f).ToString("n1") + " K against " + Settled(0f).ToString("n1") + " K");
        }


        private static float Settled(float depth)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = false;
            settings.EnableSolarHeat = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), VRageMath.Vector3I.Zero, new VRageMath.Vector3I(4, 3, 6));
            builder.Remove(new VRageMath.Vector3I(1, 1, 1));
            builder.Place(Catalog.Reactor(), new VRageMath.Vector3I(1, 1, 1)).Wasting(125000f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            EnvironmentSample world = Worlds.PlanetSurface(1f, 0.5f);
            world.Depth = depth;
            world.IsUnderground = depth > 0f;

            int steps = (int)Math.Round(1800f / settings.StepSeconds);
            for (int i = 0; i < steps; i++) simulation.StepExact(1, world);

            double total = 0d;
            int count = simulation.Solver.Nodes.Count;
            for (int i = 0; i < count; i++) total += simulation.Solver.Nodes[i].Temperature;

            return count == 0 ? 0f : (float)(total / count);
        }
    }
}

using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Lift: the transverse half of the pressure sum, and the two halves of `K23`'s criterion a
    /// rig can answer.**
    ///
    /// <para>
    /// Registered before any of this was measured: a hull symmetric about the flow must make *no*
    /// lift, or the sum is reading an artefact rather than a shape; and lift on a ramp must point
    /// **away from the sloped face**, or the sign is wrong and every ship flies into the ground.
    /// The other two halves are population figures and belong to the corpus.
    /// </para>
    /// </summary>
    public class LiftTests
    {
        private const float ThickAir = 1f;
        private const float Speed = 200f;


        private static ThermalSettings Settings(bool lift, bool shape = true)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableShapeDrag = shape;
            settings.EnableLift = lift;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Built(GridBuilder builder, bool lift, bool shape = true)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Settings(lift, shape), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(1, Worlds.Flight(ThickAir, Speed));
            return simulation;
        }


        private static ThermalSimulation Cube(bool lift)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(6, 6, 6));

            return Built(builder, lift);
        }


        private static ThermalSimulation Ramp(bool lift)
        {
            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    for (int z = 0; z < 6 - y; z++)
                    {
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));
                    }
                }
            }


            return Built(builder, lift);
        }


        private static Vector3 Wind()
        {
            return Vector3.Backward * Speed;
        }


        private static Vector3 Lift(ThermalSimulation simulation)
        {
            return LiftForce.Vector(simulation.Solver.LastPressureWatts, Wind(),
                simulation.Solver.Settings);
        }

        [Fact]

        public void ASymmetricHullMakesNoLift()
        {

            Vector3 lift = Lift(Cube(true));

            Assert.True(lift.Length() < 1f,
                "a cube is symmetric about the flow and produced " + lift.Length()
                    + " N of lift, so the transverse sum is reading an artefact");
        }

        [Fact]

        public void ARampIsPushedAwayFromItsSlopedFace()
        {

            Vector3 lift = Lift(Ramp(true));

            Assert.True(lift.Length() > 1f,
                "the ramp produced no lift at all, so this checks nothing");
            Assert.True(lift.Y < 0f,
                "the ramp's slope faces +Y so the pressure on it must push -Y, and this pushed "
                    + lift.Y);
        }

        [Fact]

        public void LiftCarriesNoComponentAlongTheFlow()
        {

            Vector3 lift = Lift(Ramp(true));
            Vector3 flow = Vector3.Normalize(Wind());

            Assert.True(Math.Abs(Vector3.Dot(lift, flow)) < 1e-2f * lift.Length() + 1e-3f,
                "lift has a component along the flow, so switching it on changes drag");
        }

        [Fact]

        public void LiftDoesNotChangeDrag()
        {

            float without = Ramp(false).Solver.LastFrictionWatts;

            float with = Ramp(true).Solver.LastFrictionWatts;

            Assert.Equal(without, with, 4);
        }

        [Fact]

        public void LiftIsRefusedWithoutTheShapeTerm()
        {

            ThermalSimulation ramp = Built(RampBuilder(), true, false);

            Assert.Equal(Vector3.Zero, ramp.Solver.LastPressureWatts);
            Assert.Equal(Vector3.Zero, Lift(ramp));
        }

        [Fact]

        public void TheCoefficientScalesTheForce()
        {

            ThermalSimulation ramp = Ramp(true);

            ThermalSettings half = ramp.Solver.Settings;

            float full = Lift(ramp).Length();

            half.LiftCoefficient = 0.5f;
            float halved = LiftForce.Vector(ramp.Solver.LastPressureWatts, Wind(), half).Length();

            Assert.Equal(full * 0.5f, halved, 2);

            half.LiftCoefficient = 0f;
            Assert.Equal(Vector3.Zero, LiftForce.Vector(ramp.Solver.LastPressureWatts, Wind(), half));
        }


        private static GridBuilder RampBuilder()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 8; x++)
                    for (int z = 0; z < 6 - y; z++)
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));

            return builder;
        }
    }
}

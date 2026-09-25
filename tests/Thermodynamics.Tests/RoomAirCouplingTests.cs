using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The properties the game's own pressurisation path relied on and did not have.
    ///
    /// Room air was sound everywhere it was tested and dead everywhere it ran. Every test and the
    /// mod API set pressure through <see cref="ThermalSimulation.SetRoomPressure"/>, which rebuilds
    /// the room's links and seeds new air from the walls holding it. The game's own sweep assigned
    /// the field directly and refreshed the mass by hand, so in a live world a pressurised room got
    /// its air mass, no links at all, and whatever temperature the last rebuild left behind — 2.7 K
    /// for a ship in vacuum. Measured: a 30-cell cabin with two vents on it, the game reporting it
    /// sealed and 99% full, running at zero pressure.
    ///
    /// These pin the two properties that made the difference, so a caller that bypasses the solver
    /// again fails here rather than in somebody's world.
    /// </summary>
    public class RoomAirCouplingTests
    {
        private static readonly Vector3I Interior = Vector3I.Zero;


        private static ThermalSimulation Shell(float blockTemperature)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            return builder.BuildSimulation(new ThermalSettings(), blockTemperature);
        }


        private static RoomAirNode AirOf(ThermalSimulation simulation)
        {
            IList<RoomAirNode> air = simulation.Solver.RoomAir;
            return air.Count == 0 ? null : air[0];
        }

        [Fact]

        public void AirGivenToARoomIsCoupledToTheWalls()
        {

            ThermalSimulation simulation = Shell(300f);


            RoomAirNode air = AirOf(simulation);
            Assert.NotNull(air);

            Assert.False(air.HasAir);
            Assert.Empty(air.Links);

            simulation.SetRoomPressure(Interior, 1f);

            Assert.True(air.HasAir);
            Assert.True(air.AirMass > 0f);

            Assert.NotEmpty(air.Links);
        }

        [Fact]

        public void AirAppearingForTheFirstTimeTakesTheTemperatureOfTheWalls()
        {

            ThermalSimulation simulation = Shell(300f);
            simulation.Update(1f, Worlds.Shadow());

            simulation.SetRoomPressure(Interior, 1f);


            RoomAirNode air = AirOf(simulation);
            Assert.NotNull(air);

            Assert.Equal(300f, air.Temperature, 0);
            Assert.True(air.Temperature > 100f,
                "air seeded from ambient rather than from the walls: " + air.Temperature + " K");
        }

        [Fact]

        public void APressurisedRoomActuallyMovesHeat()
        {

            ThermalSimulation simulation = Shell(300f);
            simulation.SetRoomPressure(Interior, 1f);


            RoomAirNode air = AirOf(simulation);
            Assert.NotNull(air);

            air.Temperature = 200f;
            float before = air.Temperature;

            for (int i = 0; i < 200; i++) simulation.Update(0.25f, Worlds.Shadow());

            Assert.True(air.Temperature > before + 1f,
                "cold air beside warm walls did not warm: " + air.Temperature + " K");
        }

        [Fact]

        public void ConductanceToARoomsAirDoesNotDependOnHowFullTheRoomIs()
        {

            ThermalSimulation simulation = Shell(300f);

            simulation.SetRoomPressure(Interior, 1f);

            RoomAirNode air = AirOf(simulation);

            float full = 0f;
            foreach (RoomLink link in air.Links) full += link.Conductance;
            float fullMass = air.ThermalMass;

            simulation.SetRoomPressure(Interior, 0.02f);

            float sliver = 0f;
            foreach (RoomLink link in air.Links) sliver += link.Conductance;

            Assert.True(full > 0f, "a full room has no coupling at all, so this judges nothing");
            Assert.Equal(full, sliver, 4);

            Assert.True(air.ThermalMass < fullMass * 0.1f,
                "a room at 2 % pressure holds " + air.ThermalMass + " J/K against a full room's "
                + fullMass + " J/K, so pressure is not reaching the capacity either");

            simulation.SetRoomPressure(Interior, 0f);
            Assert.Empty(AirOf(simulation).Links);
        }

        [Fact]

        public void RefusingTheAirsDemandApproximatesRatherThanDiverging()
        {

            float refused = AirSpreadAtCeiling(1, true);

            float unclamped = AirSpreadAtCeiling(1, false);

            Assert.True(refused <= 300f,
                "the hull started 300 K apart with nothing making heat and reached " + refused
                + " K apart, so a substep left the range the temperatures pulling on it span");

            Assert.True(unclamped > 300f,
                "the unclamped run stayed inside " + unclamped + " K, so this rig no longer"
                + " over-subscribes the air and the clamped figure above is proving nothing");
        }


        private static float AirSpreadAtCeiling(int ceiling, bool clamp)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Frequency = 1;
            settings.EnableEnvironment = false;
            settings.EnableDamage = false;
            settings.MaxSubsteps = ceiling;
            settings.MaxSubstepsPerBlock = 0;
            settings.MaxElementVisitsPerStep = 0;
            settings.ClampConductionOvershoot = clamp;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 600f);
            simulation.SetRoomPressure(Interior, 0.02f);


            RoomAirNode air = AirOf(simulation);
            air.Temperature = 300f;

            float widest = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            for (int step = 0; step < 300; step++)
            {
                simulation.StepExact(1, Worlds.Shadow());

                float hottest = air.Temperature;
                float coldest = air.Temperature;

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Temperature > hottest) hottest = nodes[i].Temperature;
                    if (nodes[i].Temperature < coldest) coldest = nodes[i].Temperature;
                }

                if (hottest - coldest > widest) widest = hottest - coldest;
            }

            return widest;
        }

        [Fact]

        public void DroppingPressureTakesTheLinksAwayAgain()
        {

            ThermalSimulation simulation = Shell(300f);
            simulation.SetRoomPressure(Interior, 1f);

            Assert.NotEmpty(AirOf(simulation).Links);

            simulation.SetRoomPressure(Interior, 0f);


            RoomAirNode air = AirOf(simulation);
            Assert.False(air.HasAir);
            Assert.Empty(air.Links);
        }
    }
}

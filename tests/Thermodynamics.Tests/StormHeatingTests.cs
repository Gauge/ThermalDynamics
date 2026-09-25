using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a storm does to a ship that is standing still.
    ///
    /// <para>
    /// The defect the wind field was written to prevent: the engine's raw figure put every parked
    /// ship in a permanent 80 m/s wind, over the friction threshold and at double the still-air
    /// convection. The composed model reopened it — a storm reached 187 m/s, 143 of that within a
    /// hundred metres of the ground — for two reasons that were faults rather than balance, and one
    /// that is not. backlog.md `B17`, `B18`.
    /// </para>
    ///
    /// <para>
    /// The faults are fixed: the weather's own wind modifier moved the intensity instead of
    /// multiplying the share, so the same storm is no longer counted twice, and the composed speed
    /// is bounded by the planet's own figure again. What is left is that a storm's wind heats a
    /// parked hull by friction, and **that is the model working rather than failing** — `v_rel`
    /// is relative wind by design, so a ship parked in a hurricane heats like a ship flying at
    /// hurricane speed, and since 2026-09-02 the term is live at every speed rather than gated at
    /// a floor (the v³ law is the guard now). This measures how much, because "acceptable" is an
    /// assertion until it is a number.
    /// </para>
    /// </summary>
    public class StormHeatingTests
    {

        private static float Settled(float windSpeed, float seconds)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = false;
            settings.EnableSolarHeat = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 3, 8));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            EnvironmentSample world = Worlds.Storm(1f, windSpeed);
            int steps = (int)Math.Round(seconds / settings.StepSeconds);
            for (int i = 0; i < steps; i++) simulation.StepExact(1, world);

            float hottest = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float temperature = simulation.Solver.Nodes[i].Temperature;
                if (temperature > hottest) hottest = temperature;
            }

            return hottest;
        }

        [Fact]

        public void AParkedHullInAHurricaneWarmsByDegreesRatherThanHundreds()
        {

            float still = Settled(0f, 600f);

            float storm = Settled(70f, 600f);

            Assert.True(storm > still,
                "a hurricane-speed wind has to heat something, or the term is dead");

            float rise = storm - still;
            Assert.True(rise < 25f,
                "a parked hull in a hurricane rose " + rise.ToString("n1")
                + " K, which is not a few degrees");
        }

        [Fact]

        public void AWorldThatSetsTheFloorGetsNoFrictionBelowIt()
        {
            Assert.Equal(0f, FrictionWatts(40f, 50f), 4);
            Assert.Equal(0f, FrictionWatts(50f, 50f), 4);
            Assert.True(FrictionWatts(70f, 50f) > 0f,
                "past the floor the term has to do something, or it is dead");
        }

        [Fact]

        public void AtTheShippedDefaultFrictionIsLiveAtEverySpeedAndVanishesByTheCube()
        {

            float slow = FrictionWatts(10f, 0f);

            float fast = FrictionWatts(100f, 0f);

            Assert.True(slow > 0f, "friction at 10 m/s is zero, so the floor is still gating");
            Assert.True(fast > slow, "friction has to grow with speed");

            float ratio = fast / slow;
            Assert.True(ratio > 900f && ratio < 1100f,
                "10 to 100 m/s moved friction by " + ratio.ToString("n0")
                + "x where the v^3 law says 1,000x");
        }

        [Fact]

        public void AirspeedCoolsAHotHullSlowAndHeatsItFast()
        {

            ThermalSettings world = new ThermalSettings();
            world.EnableDamage = false;
            world.EnableSolarHeat = false;
            world.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 3, 8));
            builder.Remove(new Vector3I(1, 1, 1));
            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1)).Producing(2e6f);


            float still = Mean(builder, world, Worlds.PlanetSurface(1f, 0.5f), 600f);

            float breezy = Mean(builder, world, Worlds.Storm(1f, 45f), 600f);

            float screaming = Mean(builder, world, Worlds.Storm(1f, 300f), 600f);

            Assert.True(breezy < still,
                "45 m/s left the hull at " + breezy.ToString("n1") + " K against "
                + still.ToString("n1") + " K still — the wind should net-cool a hot hull");
            Assert.True(screaming > still,
                "300 m/s left the hull at " + screaming.ToString("n1") + " K against "
                + still.ToString("n1") + " K still — the cube should have won by here");
        }

        [Fact]

        public void AWindyDayCoolsAHullThatIsMakingHeat()
        {

            ThermalSettings world = new ThermalSettings();
            world.EnableDamage = false;
            world.EnableSolarHeat = false;
            world.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 3, 8));
            builder.Remove(new Vector3I(1, 1, 1));
            builder.Place(Catalog.Reactor(), new Vector3I(1, 1, 1)).Producing(2e6f);


            float still = Mean(builder, world, Worlds.PlanetSurface(1f, 0.5f), 600f);

            for (float speed = 5f; speed < 50f; speed += 5f)
            {

                float windy = Mean(builder, world, Worlds.Storm(1f, speed), 600f);

                Assert.True(windy < still,
                    "a " + speed.ToString("n0") + " m/s wind left the hull at "
                    + windy.ToString("n1") + " K against " + still.ToString("n1")
                    + " K in still air");
            }
        }


        private static float FrictionWatts(float windSpeed, float floor)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = false;
            settings.EnableSolarHeat = false;
            settings.FrictionAtSpeedsAbove = floor;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 3, 8));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.RebuildAll();
            simulation.StepExact(1, Worlds.Storm(1f, windSpeed));

            float total = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                total += simulation.Solver.Nodes[i].LastFrictionWatts;
            }

            return total;
        }


        private static float Mean(GridBuilder builder, ThermalSettings settings,
            EnvironmentSample world, float seconds)
        {
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            int steps = (int)Math.Round(seconds / settings.StepSeconds);
            for (int i = 0; i < steps; i++) simulation.StepExact(1, world);

            double total = 0d;
            int count = simulation.Solver.Nodes.Count;
            for (int i = 0; i < count; i++) total += simulation.Solver.Nodes[i].Temperature;

            return count == 0 ? 0f : (float)(total / count);
        }
    }
}

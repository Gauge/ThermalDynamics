using Thermodynamics.Fixtures;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **A block behind another is sheltered from the wind, for heat and for drag.**
    ///
    /// <para>
    /// The same self-shadowing pass the sun uses, aimed at the relative wind — `SunShadowMap` takes
    /// its direction as an argument, so it was always a direction pass. What differs is the
    /// cadence, and that is measured rather than inherited: see `WindShieldingCostTests` for why the
    /// threshold is 20° rather than the sun's 2°, and why a running pass is never restarted.
    /// </para>
    ///
    /// <para>
    /// **Off by default, and the default is the conservative answer.** Unshielded, every face is
    /// treated as being in the open, so a hull is heated and dragged at least as much as it should
    /// be and never less. A world that switches this on is asking for the more forgiving model, not
    /// the harsher one.
    /// </para>
    /// </summary>
    public class WindShieldingTests
    {
        private const float ThickAir = 1f;
        private const float Speed = 120f;


        private static HistoricalThermalSettings Settings(bool shielding)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableWindwardShielding = shielding;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Sheltered(bool shielding)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), new Vector3I(0, 0, 4), new Vector3I(4, 4, 6));
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 2));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(shielding), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float DragWatts(ThermalSimulation simulation)
        {
            simulation.StepExact(8, Worlds.Flight(ThickAir, Speed));
            return simulation.Solver.LastFrictionWatts;
        }

        [Fact]

        public void AHullBehindAnotherTakesLessWindWhenShielded()
        {

            float open = DragWatts(Sheltered(false));

            float shielded = DragWatts(Sheltered(true));

            Assert.True(open > 0f, "the hull took no wind at all, so this compares nothing");
            Assert.True(shielded < open,
                "shielding changed nothing: " + shielded + " against " + open);
        }

        [Fact]

        public void TheDefaultIsTheHarsherAnswer()
        {
            Assert.False(new HistoricalThermalSettings().EnableWindwardShielding);
            Assert.True(DragWatts(Sheltered(false)) >= DragWatts(Sheltered(true)));
        }

        [Fact]

        public void AHullInClearAirIsUnchangedByShielding()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 1));

            ThermalSimulation open = builder.BuildSimulation(Settings(false), 293.15f);
            open.Planet = PlanetThermalProperties.Default();

            GridBuilder second = GridBuilder.Large();
            second.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 1));
            ThermalSimulation shielded = second.BuildSimulation(Settings(true), 293.15f);
            shielded.Planet = PlanetThermalProperties.Default();

            Assert.Equal(DragWatts(open), DragWatts(shielded), 2);
        }

        [Fact]

        public void TheShieldingArrayIsNotAllocatedWhenItIsOff()
        {

            ThermalSimulation off = Sheltered(false);
            off.StepExact(1, Worlds.Flight(ThickAir, Speed));

            Assert.Equal(0, off.Solver.WindLitLength);


            ThermalSimulation on = Sheltered(true);
            on.StepExact(1, Worlds.Flight(ThickAir, Speed));

            Assert.True(on.Solver.WindLitLength > 0,
                "the shielding is on and its array is empty, so it is reading nothing");
        }
        [Fact]

        public void ShieldingChangesTemperaturesAndNotOnlyDrag()
        {

            ThermalSimulation open = Convecting(false);

            ThermalSimulation shielded = Convecting(true);

            open.StepExact(120, Worlds.Flight(ThickAir, Speed));
            shielded.StepExact(120, Worlds.Flight(ThickAir, Speed));


            float openPeak = Peak(open);

            float shieldedPeak = Peak(shielded);

            Assert.True(openPeak > 0f && shieldedPeak > 0f);

            Assert.True(shieldedPeak > openPeak,
                "the shielded hull is not hotter, so the shielding is not reducing convection the "
                + "way it reduces friction — open " + openPeak + " K, shielded " + shieldedPeak + " K");
            Assert.True(Math.Abs(openPeak - shieldedPeak) > 0.001f,
                "shielding moved no temperature at all, so it is not reaching the convection term "
                + "that the same six-face sum feeds — open " + openPeak + " K, shielded "
                + shieldedPeak + " K");
        }


        private static ThermalSimulation Convecting(bool shielding)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableWindwardShielding = shielding;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), new Vector3I(0, 0, 4), new Vector3I(4, 4, 6));
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 2));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 500f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float Peak(ThermalSimulation simulation)
        {
            float peak = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float t = simulation.Solver.Nodes[i].Temperature;
                if (t > peak) peak = t;
            }

            return peak;
        }

    }
}

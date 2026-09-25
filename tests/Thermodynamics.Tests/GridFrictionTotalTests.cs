using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The grid's drag power, which is the one figure a drag force needs for its magnitude.**
    ///
    /// <para>
    /// What the solver computes per node is `FrictionScale x rho x v_rel^3 x area x windward
    /// exposure`, and real drag power is `1/2 C_d rho A v^3` — the same expression. The mod turns
    /// that power into heat in the hull and takes nothing from the ship's motion, so energy enters
    /// the world with nothing paying for it: measured over the 8,144 published blueprints at
    /// `reentry`, the median hull absorbs 5.05 MW and the largest 4.91 GW, on 100 % of them. At
    /// 300 m/s a median 5.05 MW is 16.8 kN of force never applied. See thermal-model.md's change log.
    /// </para>
    ///
    /// <para>
    /// the drag milestone is the part that costs nothing and makes the rest measurable: sum it per grid and
    /// publish it. Until now the friction term existed per node, in the harness's per-scenario
    /// outcome, and per block type in the telemetry — everywhere except the one place a force would
    /// read it from.
    /// </para>
    /// </summary>
    public class GridFrictionTotalTests
    {
        private const float ThickAir = 1f;


        private static ThermalSimulation Rig(bool diagnostics)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = diagnostics;
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }

        [Fact]

        public void TheGridTotalIsTheSumOfItsNodes()
        {

            ThermalSimulation simulation = Rig(true);
            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));

            float summed = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                summed += simulation.Solver.Nodes[i].LastFrictionWatts;
            }

            Assert.True(summed > 0f, "nothing was heated by friction, so this proves nothing");
            Assert.Equal(summed, simulation.Solver.LastFrictionWatts, 3);
        }

        [Fact]

        public void TheTotalDoesNotDependOnDiagnosticsBeingOn()
        {

            ThermalSimulation watched = Rig(true);

            ThermalSimulation unwatched = Rig(false);

            watched.StepExact(1, Worlds.Flight(ThickAir, 120f));
            unwatched.StepExact(1, Worlds.Flight(ThickAir, 120f));

            Assert.True(watched.Solver.LastFrictionWatts > 0f);
            Assert.Equal(watched.Solver.LastFrictionWatts, unwatched.Solver.LastFrictionWatts, 3);

            Assert.Equal(0f, unwatched.Solver.Nodes[0].LastFrictionWatts);
        }

        [Fact]

        public void TheTotalIsARateRatherThanAnAccumulation()
        {

            ThermalSimulation simulation = Rig(false);

            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));
            float first = simulation.Solver.LastFrictionWatts;

            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));
            float second = simulation.Solver.LastFrictionWatts;

            Assert.True(first > 0f);
            Assert.Equal(first, second, 3);
        }

        [Fact]

        public void TheTotalSurvivesEverySubstepOfAStep()
        {

            ThermalSimulation simulation = Rig(false);
            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));
            float once = simulation.Solver.LastFrictionWatts;


            ThermalSimulation many = Rig(false);
            many.StepExact(8, Worlds.Flight(ThickAir, 120f));

            Assert.True(once > 0f);
            Assert.Equal(once, many.Solver.LastFrictionWatts, 3);
        }

        [Fact]

        public void AGridThatIsNotMovingThroughAirHasNoDragPower()
        {

            ThermalSimulation simulation = Rig(false);
            simulation.StepExact(1, Worlds.Flight(ThickAir, 0f));

            Assert.Equal(0f, simulation.Solver.LastFrictionWatts);
        }

        [Fact]

        public void DragPowerRisesSteeplyWithSpeed()
        {

            ThermalSimulation slow = Rig(false);

            ThermalSimulation fast = Rig(false);

            slow.StepExact(1, Worlds.Flight(ThickAir, 60f));
            fast.StepExact(1, Worlds.Flight(ThickAir, 120f));

            float ratio = fast.Solver.LastFrictionWatts / Math.Max(1e-6f, slow.Solver.LastFrictionWatts);
            Assert.True(ratio > 4f,
                "doubling the speed multiplied the drag power by " + ratio + ", which is not a cube law");
        }

        [Fact]

        public void TheFrictionTotalIsInsideTheHeatGain()
        {

            ThermalSimulation simulation = Rig(false);
            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));

            Assert.True(simulation.Solver.LastFrictionWatts > 0f);
            Assert.True(simulation.HeatGainWatts >= simulation.Solver.LastFrictionWatts,
                "the heat gain is smaller than the friction inside it, so they are not the same sum");
            Assert.Equal(simulation.Solver.LastFrictionWatts, simulation.FrictionWatts, 3);
        }
    }
}

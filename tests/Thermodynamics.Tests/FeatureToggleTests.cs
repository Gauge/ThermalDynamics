using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every mechanism is meant to be independently switchable, and switching one off is meant to
    /// remove exactly its own contribution and nothing else. These pin that down one mechanism at
    /// a time: build the same grid twice, change one switch, and check that the difference is the
    /// one mechanism.
    ///
    /// <para>
    /// **One test per switch, and the switch is the subject.** Most of these mechanisms are tested
    /// hard somewhere else — friction against the cube of airspeed, self-shadowing against a block
    /// standing behind another, coolant against the energy it carries — but those rigs reach the
    /// mechanism through its physics rather than through its switch, and several of them turn other
    /// switches off only to keep the fixture quiet. A mechanism that ignores its own setting passes
    /// every one of them. What is checked here is narrower and duller: the setting is honoured, and
    /// what disappears when it is off is that mechanism's contribution and not a neighbour's.
    /// </para>
    ///
    /// <para>
    /// Where a mechanism reports watts, the assertion is on the watts rather than on a temperature
    /// that moved, because a temperature only says something happened. Diagnostics are opt-in, so
    /// those rigs set <c>CollectDiagnostics</c>.
    /// </para>
    /// </summary>
    public class FeatureToggleTests
    {

        private static ThermalSimulation TwoBlocks(HistoricalThermalSettings settings, float hot, float cold)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(settings, cold);
            simulation.Solver.Nodes[0].Temperature = hot;
            return simulation;
        }

        [Fact]

        public void ConductionOffLeavesNeighboursAlone()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableConduction = false;
            settings.Derive();


            ThermalSimulation simulation = TwoBlocks(settings, 500f, 300f);
            simulation.StepExact(20, Worlds.Shadow());

            Assert.Equal(500f, simulation.Solver.Nodes[0].Temperature, 3);
            Assert.Equal(300f, simulation.Solver.Nodes[1].Temperature, 3);
        }

        [Fact]

        public void ConductionOnEqualisesThem()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();


            ThermalSimulation simulation = TwoBlocks(settings, 500f, 300f);
            simulation.StepExact(1000, Worlds.Shadow());

            float a = simulation.Solver.Nodes[0].Temperature;
            float b = simulation.Solver.Nodes[1].Temperature;
            Assert.True(Math.Abs(a - b) < 1f, "expected the pair to equalise, got " + a + " and " + b);
            Assert.Equal(400f, a, 0);
        }

        [Fact]

        public void RadiationOffStopsAHotBlockCoolingIntoSpace()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableRadiation = false;
            settings.Derive();


            ThermalSimulation simulation = TwoBlocks(settings, 900f, 900f);
            simulation.StepExact(50, Worlds.Shadow());

            Assert.Equal(900f, simulation.Solver.Nodes[0].Temperature, 2);
        }

        [Fact]

        public void RadiationOnCoolsIt()
        {

            ThermalSimulation simulation = TwoBlocks(new HistoricalThermalSettings(), 900f, 900f);
            simulation.StepExact(50, Worlds.Shadow());

            Assert.True(simulation.Solver.Nodes[0].Temperature < 899f);
        }

        [Fact]

        public void RadiationShedsWhatTheLawSays()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableConduction = false;
            settings.Derive();


            ThermalSimulation simulation = TwoBlocks(settings, 900f, 900f);
            simulation.Solver.CollectDiagnostics = true;

            ThermalNode node = simulation.Solver.Nodes[0];

            Assert.Equal(node.Thermal.Emissivity * ThermalConstants.StefanBoltzmann * node.ExposedArea,
                node.RadiationCoefficient, 6);

            float temperature = node.Temperature;
            float ambient = settings.VacuumTemperature;
            simulation.StepExact(1, Worlds.Shadow());

            double expected = node.RadiationCoefficient
                * (Math.Pow(ambient, 4d) - Math.Pow(temperature, 4d));

            Assert.True(node.LastRadiationWatts < 0f);
            Assert.True(Math.Abs(node.LastRadiationWatts - expected) < Math.Abs(expected) * 0.01d,
                "expected about " + expected.ToString("n0") + " W, got " + node.LastRadiationWatts);
        }

        [Fact]

        public void ConvectionOffLeavesOnlyRadiationInAtmosphere()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableConvection = false;
            settings.Derive();


            ThermalSimulation withoutConvection = TwoBlocks(settings, 600f, 600f);
            withoutConvection.StepExact(20, Worlds.PlanetSurface(1f, 0.5f));


            ThermalSimulation withConvection = TwoBlocks(new HistoricalThermalSettings(), 600f, 600f);
            withConvection.StepExact(20, Worlds.PlanetSurface(1f, 0.5f));

            Assert.True(withoutConvection.Solver.Nodes[0].Temperature
                > withConvection.Solver.Nodes[0].Temperature + 10f);
        }

        [Fact]

        public void ConvectionOffLeavesTheRadiativeShareUntouched()
        {

            HistoricalThermalSettings off = new HistoricalThermalSettings();
            off.EnableConvection = false;
            off.Derive();


            ThermalSimulation without = TwoBlocks(off, 600f, 600f);
            without.Solver.CollectDiagnostics = true;
            without.StepExact(1, ThinAir());


            ThermalSimulation with = TwoBlocks(new HistoricalThermalSettings(), 600f, 600f);
            with.Solver.CollectDiagnostics = true;
            with.StepExact(1, ThinAir());

            Assert.Equal(0f, without.Solver.Nodes[0].LastConvectionWatts, 5);
            Assert.True(with.Solver.Nodes[0].LastConvectionWatts < 0f,
                "a 600 K block in air should be shedding convective watts");

            Assert.True(with.Solver.Nodes[0].LastRadiationWatts < 0f);
            Assert.Equal(with.Solver.Nodes[0].LastRadiationWatts,
                without.Solver.Nodes[0].LastRadiationWatts, 3);
        }


        private static EnvironmentSample ThinAir()
        {
            return Worlds.PlanetSurface(0.3f, 0.5f);
        }

        [Fact]

        public void TheEnvironmentSwitchRemovesRadiationAndConvectionAndLeavesSourcesAlone()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(15e6f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 600f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.StepExact(1, ThinAir());

            ThermalNode node = simulation.Solver.Nodes[0];
            Assert.Equal(0f, node.LastRadiationWatts, 5);
            Assert.Equal(0f, node.LastConvectionWatts, 5);

            Assert.True(node.HeatGenerationWatts > 0f);
            Assert.True(node.Temperature > 600f);

            ThermalSimulation exchanging = builder.BuildSimulation(new HistoricalThermalSettings(), 600f);
            exchanging.Solver.CollectDiagnostics = true;
            exchanging.StepExact(1, ThinAir());

            Assert.True(exchanging.Solver.Nodes[0].LastRadiationWatts < 0f);
            Assert.True(exchanging.Solver.Nodes[0].LastConvectionWatts < 0f);
        }

        [Fact]

        public void WasteHeatOffStopsAReactorHeatingItself()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableWasteHeat = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(15e6f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.StepExact(50, Worlds.Shadow());

            Assert.Equal(293.15f, simulation.Solver.Nodes[0].Temperature, 3);
        }

        [Fact]

        public void SwitchingASettingTakesEffectWithoutARebuild()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(15e6f);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.StepExact(10, Worlds.Shadow());
            float heated = simulation.Solver.Nodes[0].Temperature;
            Assert.True(heated > 293.15f);

            settings.EnableWasteHeat = false;
            settings.Derive();

            simulation.StepExact(10, Worlds.Shadow());
            Assert.Equal(heated, simulation.Solver.Nodes[0].Temperature, 3);
        }

        [Fact]

        public void ChangingHeatTimeScaleMidSessionRescalesCapacity()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.HeatTimeScale = 1f;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            float slow = simulation.Solver.Nodes[0].ThermalMass;

            settings.HeatTimeScale = 100f;
            settings.Derive();
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(slow / 100f, simulation.Solver.Nodes[0].ThermalMass, 4);
        }

        [Fact]

        public void CoolantLoopsSwitchOffMidSession()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 4, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            Assert.NotEmpty(simulation.Solver.Loops);

            settings.EnableCoolantLoops = false;
            settings.Derive();
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Empty(simulation.Solver.Loops);
        }


        [Fact]

        public void FrictionOffLeavesAHullAtSpeedCold()
        {

            ThermalNode cold = HullAtSpeed(false);

            ThermalNode hot = HullAtSpeed(true);

            Assert.Equal(0f, cold.LastFrictionWatts, 5);
            Assert.Equal(293.15f, cold.Temperature, 3);

            Assert.True(hot.LastFrictionWatts > 0f);
            Assert.True(hot.Temperature > 293.15f);
        }


        private static ThermalNode HullAtSpeed(bool friction)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableFriction = friction;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();

            simulation.StepExact(4, Worlds.Flight(0.8f, 300f));
            return simulation.Solver.Nodes[0];
        }

        [Fact]

        public void SolarOffLeavesNoSolarWattsOnASunwardNode()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);


            HistoricalThermalSettings off = new HistoricalThermalSettings();
            off.EnableSolarHeat = false;
            off.Derive();

            ThermalSimulation dark = builder.BuildSimulation(off, 293.15f);
            dark.Solver.CollectDiagnostics = true;
            dark.StepExact(1, Worlds.Space(new Vector3(1f, 0f, 0f)));


            HistoricalThermalSettings on = new HistoricalThermalSettings();
            on.EnableSolarHeat = true;
            on.Derive();

            ThermalSimulation lit = builder.BuildSimulation(on, 293.15f);
            lit.Solver.CollectDiagnostics = true;
            lit.StepExact(1, Worlds.Space(new Vector3(1f, 0f, 0f)));

            Assert.Equal(0f, dark.Solver.Nodes[0].LastSolarWatts, 5);
            Assert.True(lit.Solver.Nodes[0].LastSolarWatts > 0f,
                "a block facing the sun should be taking solar watts");
        }

        [Fact]

        public void CoolantLoopsOffStopTheRingCarryingHeat()
        {

            float withLoops = HotBlockBesideARing(true);

            float withoutLoops = HotBlockBesideARing(false);

            Assert.True(withLoops < withoutLoops - 1f,
                "the ring should have carried heat away from the block: with loops it reached "
                + withLoops + " K, without them " + withoutLoops + " K");
        }


        private static float HotBlockBesideARing(bool loops)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.EnableCoolantLoops = loops;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[1] = Vector3I.Down;

            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(Vector3I.Zero, 3, 3), -1, sinks);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, -1, 0));
            BlockInstance hot = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            Assert.Equal(loops, simulation.Solver.Loops.Count > 0);

            ThermalNode node = simulation.Solver.GetNode(hot);
            node.Temperature = 900f;
            simulation.StepExact(20, Worlds.Shadow());

            return node.Temperature;
        }

        [Fact]

        public void RoomAirOffStopsTheAirCarryingHeatAcrossARoom()
        {

            float withAir = FarWallOfASealedBox(true);

            float withoutAir = FarWallOfASealedBox(false);

            Assert.Equal(293.15f, withoutAir, 3);
            Assert.True(withAir > withoutAir + 1f,
                "the air should have warmed the far wall, got " + withAir + " K against "
                + withoutAir + " K");
        }


        private static float FarWallOfASealedBox(bool air)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableConduction = false;
            settings.EnableRoomAir = air;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(5, 5, 5));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.SetRoomPressure(new Vector3I(2, 2, 2), 1f);

            simulation.Solver.GetNodeAt(new Vector3I(2, 0, 2)).Temperature = 600f;
            ThermalNode far = simulation.Solver.GetNodeAt(new Vector3I(2, 4, 2));

            simulation.StepExact(400, Worlds.Shadow());
            return far.Temperature;
        }
    }
}

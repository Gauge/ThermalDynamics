using Thermodynamics.Fixtures;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The world a grid sits in, resolved from one sample into the terms a step consumes.
    ///
    /// <para>
    /// Everything here is a function of where the grid is and what time it is, and none of it depends
    /// on a temperature. These are the boundary conditions; the suites below them are what the model
    /// does with those conditions.
    /// </para>
    /// </summary>
    public class EnvironmentSolverTests
    {
        [Fact]

        public void NoPlanetMeansVacuum()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            EnvironmentState state = EnvironmentSolver.Solve(settings, null, Worlds.Shadow());

            Assert.Equal(settings.VacuumTemperature, state.AmbientTemperature, 3);
            Assert.Equal(0f, state.AirDensity, 5);
            Assert.Equal(0f, state.AtmosphereFactor, 5);
            Assert.Equal(0f, state.ConvectionCoefficient, 5);
        }

        [Fact]

        public void AmbientIsNeverColderThanVacuum()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            EnvironmentSample sample = Worlds.PlanetSurface(0.0001f, 0f);
            EnvironmentState state = EnvironmentSolver.Solve(settings, planet, sample);

            Assert.True(state.AmbientTemperature >= settings.VacuumTemperature);
        }

        [Fact]

        public void NoonIsWarmerThanMidnight()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            float noon = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(1f, 0.5f)).AmbientTemperature;
            float midnight = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(1f, 0f)).AmbientTemperature;

            Assert.True(noon > midnight);
            Assert.Equal(planet.DayTemperature, noon, 1);
            Assert.Equal(planet.NightTemperature, midnight, 1);
        }

        [Fact]

        public void UndergroundUsesItsOwnTemperatureAndBlocksTheSun()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            EnvironmentState state = EnvironmentSolver.Solve(settings, planet, Worlds.Underground());

            Assert.Equal(planet.UndergroundTemperature, state.AmbientTemperature, 1);
            Assert.True(state.IsSolarOccluded);
            Assert.Equal(0f, state.SolarEnergy, 5);
        }

        [Fact]

        public void AtmosphereFactorRisesFasterThanRawDensity()
        {
            Assert.Equal(0f, EnvironmentSolver.AtmosphereFactor(0f), 5);
            Assert.Equal(1f, EnvironmentSolver.AtmosphereFactor(1f), 5);

            float quarter = EnvironmentSolver.AtmosphereFactor(0.25f);
            Assert.True(quarter > 0.25f);
            Assert.True(quarter < 1f);

            float previous = -1f;
            for (float d = 0f; d <= 1f; d += 0.05f)
            {
                float value = EnvironmentSolver.AtmosphereFactor(d);
                Assert.True(value >= previous);
                previous = value;
            }
        }

        [Fact]

        public void SolarEnergyDecaysThroughAtmosphere()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            float vacuum = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(0f, 0.5f)).SolarEnergy;
            float thick = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(1f, 0.5f)).SolarEnergy;

            Assert.Equal(settings.SolarEnergy, vacuum, 1);
            Assert.Equal(settings.SolarEnergy * (1f - planet.SolarDecay), thick, 1);
        }

        [Fact]

        public void WindRaisesTheConvectionCoefficient()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            float still = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(1f, 0.5f)).ConvectionCoefficient;
            float windy = EnvironmentSolver.Solve(settings, planet, Worlds.PlanetSurface(1f, 0.5f, 100f)).ConvectionCoefficient;

            Assert.Equal(planet.ConvectionCoefficient, still, 2);
            Assert.True(windy > still);
        }

        [Fact]

        public void FrictionNeedsBothAirAndSpeed()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            Assert.False(EnvironmentSolver.Solve(settings, planet, Worlds.Flight(0.8f, 0f)).FrictionActive);
            Assert.False(EnvironmentSolver.Solve(settings, planet, Worlds.Flight(0.001f, 300f)).FrictionActive);
            Assert.True(EnvironmentSolver.Solve(settings, planet, Worlds.Flight(0.8f, 10f)).FrictionActive);
            Assert.True(EnvironmentSolver.Solve(settings, planet, Worlds.Flight(0.8f, 300f)).FrictionActive);
        }

        [Fact]

        public void DisablingPlanetsFallsBackToVacuumEvenOnASurface()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnablePlanets = false;

            EnvironmentState state = EnvironmentSolver.Solve(
                settings, PlanetThermalProperties.Default(), Worlds.PlanetSurface(1f, 0.5f));

            Assert.Equal(settings.VacuumTemperature, state.AmbientTemperature, 3);
        }

        [Fact]

        public void AmbientPow4TracksAmbient()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            EnvironmentState state = EnvironmentSolver.Solve(settings, PlanetThermalProperties.Default(), Worlds.PlanetSurface(1f, 0.5f));

            float t = state.AmbientTemperature;
            float squared = t * t;
            Assert.Equal(1f, state.AmbientTemperaturePow4 / (squared * squared), 5);
        }
    }

    /// <summary>
    /// Stefan-Boltzmann, against a figure computed by hand rather than by the model.
    ///
    /// <para>
    /// Four properties follow from the equation and each has a case: radiation is toward ambient
    /// rather than away, it needs an exposed face, emissivity is the rate dial, and a block colder
    /// than ambient warms by the same equation that cools a hot one.
    /// </para>
    /// </summary>
    public class RadiationTests
    {

        private static ThermalSimulation SingleBlock(HistoricalThermalSettings settings, float temperature)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, temperature);
            simulation.Solver.CollectDiagnostics = true;
            return simulation;
        }

        [Fact]

        public void ALoneBlockRadiatesTowardAmbient()
        {

            ThermalSimulation simulation = SingleBlock(Fixture.EnvironmentOnly(), 800f);
            ThermalNode node = simulation.Solver.Nodes[0];

            simulation.StepExact(400, Worlds.Shadow());

            Assert.True(node.Temperature < 300f, "block should have shed most of its heat, at " + node.Temperature);
            Assert.True(node.Temperature > 0f);
        }

        [Fact]

        public void RadiatedPowerMatchesStefanBoltzmann()
        {
            HistoricalThermalSettings settings = Fixture.EnvironmentOnly(1000);

            ThermalSimulation simulation = SingleBlock(settings, 500f);
            ThermalNode node = simulation.Solver.Nodes[0];

            simulation.StepExact(1, Worlds.Shadow());

            float area = 6f * 6.25f;
            float emissivity = node.Thermal.Emissivity;
            float expected = -emissivity * ThermalConstants.StefanBoltzmann * area
                * ((500f * 500f * 500f * 500f) - (2.7f * 2.7f * 2.7f * 2.7f));

            Assert.Equal(1f, node.LastRadiationWatts / expected, 3);
        }

        [Fact]

        public void ASealedBlockWithNoExposedFacesDoesNotRadiate()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.EnvironmentOnly(), 293.15f);
            ThermalNode interior = simulation.Solver.GetNode(builder.Last);

            interior.Temperature = 900f;
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(0, interior.TotalExposedFaces);
            Assert.Equal(0f, interior.LastRadiationWatts, 5);
        }

        [Fact]

        public void HigherEmissivityCoolsFaster()
        {
            BlockThermalProperties dull = Catalog.DefaultThermal();
            dull.Emissivity = 0.05f;
            BlockThermalProperties shiny = Catalog.DefaultThermal();
            shiny.Emissivity = 0.9f;


            float dullResult = CoolFor(dull);

            float shinyResult = CoolFor(shiny);

            Assert.True(shinyResult < dullResult);
        }


        private static float CoolFor(BlockThermalProperties thermal)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Test", Vector3I.One, 500f, thermal), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(Fixture.EnvironmentOnly(), 600f);
            simulation.StepExact(20, Worlds.Shadow());
            return simulation.Solver.Nodes[0].Temperature;
        }

        [Fact]

        public void ABlockColderThanAmbientWarmsUp()
        {
            HistoricalThermalSettings settings = Fixture.EnvironmentOnly();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 100f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();

            simulation.StepExact(100, Worlds.PlanetSurface(1f, 0.5f));

            Assert.True(simulation.Solver.Nodes[0].Temperature > 100f);
        }
    }

    /// <summary>
    /// The three environment mechanisms that are not radiation, each checked for what makes it distinct.
    ///
    /// <para>
    /// Convection carries the air blend and falls with density; solar follows the projected area of a
    /// face and disappears under occlusion; friction has a speed threshold and cubes above it. The one
    /// non-obvious result is that a settled temperature is not monotonic in air density — thinner air
    /// convects less, and also blocks less sun.
    /// </para>
    /// </summary>
    public class ConvectionSolarFrictionTests
    {
        [Fact]

        public void AirCoolsFasterThanVacuum()
        {

            float inVacuum = CoolIn(Worlds.Shadow());

            float inAir = CoolIn(Worlds.PlanetSurface(1f, 0f));

            Assert.True(inAir < inVacuum,
                "convection should beat radiation at sea level: air " + inAir + " vs vacuum " + inVacuum);
        }


        private static float CoolIn(EnvironmentSample sample)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(Fixture.EnvironmentOnly(), 600f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(8, sample);
            return simulation.Solver.Nodes[0].Temperature;
        }

        [Fact]

        public void SolarGainMatchesTheProjectedArea()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 200f);
            simulation.Solver.CollectDiagnostics = true;

            simulation.StepExact(1, Worlds.Space(new Vector3(1f, 0f, 0f)));

            ThermalNode node = simulation.Solver.Nodes[0];
            float expected = 1000f * node.Thermal.EffectiveSolarAbsorptivity * 6.25f;
            Assert.Equal(expected, node.LastSolarWatts, 1);
        }

        [Fact]

        public void OcclusionRemovesSolarGainEntirely()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 200f);
            simulation.Solver.CollectDiagnostics = true;

            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(0f, simulation.Solver.Nodes[0].LastSolarWatts, 5);
            Assert.Equal(200f, simulation.Solver.Nodes[0].Temperature, 3);
        }

        [Fact]

        public void SolarGainFollowsTheSunAroundTheBlock()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));
            ThermalSimulation simulation = builder.BuildSimulation(settings, 200f);
            simulation.Solver.CollectDiagnostics = true;

            ThermalNode centre = simulation.Solver.GetNodeAt(new Vector3I(1, 0, 1));

            simulation.StepExact(1, Worlds.Space(new Vector3(0f, 1f, 0f)));
            float fromAbove = centre.LastSolarWatts;

            simulation.StepExact(1, Worlds.Space(new Vector3(1f, 0f, 0f)));
            float fromTheSide = centre.LastSolarWatts;

            Assert.True(fromAbove > fromTheSide,
                "overhead sun should beat a grazing one: " + fromAbove + " vs " + fromTheSide);
        }

        [Fact]

        public void FrictionActuallyHeatsTheBlock()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();

            float before = simulation.Solver.Nodes[0].Temperature;
            simulation.StepExact(4, Worlds.Flight(0.8f, 300f));

            Assert.True(simulation.Solver.Nodes[0].LastFrictionWatts > 0f);
            Assert.True(simulation.Solver.Nodes[0].Temperature > before,
                "friction must raise the temperature");
        }

        [Fact]

        public void BelowAConfiguredFloorFrictionDoesNothing()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.FrictionAtSpeedsAbove = 50f;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();

            simulation.StepExact(4, Worlds.Flight(0.8f, 10f));

            Assert.Equal(0f, simulation.Solver.Nodes[0].LastFrictionWatts, 5);
            Assert.Equal(293.15f, simulation.Solver.Nodes[0].Temperature, 3);
        }

        [Fact]

        public void FrictionScalesWithTheCubeOfSpeed()
        {

            float slow = FrictionWattsAt(100f);

            float fast = FrictionWattsAt(200f);

            Assert.Equal(8f, fast / slow, 2);
        }


        private static float FrictionWattsAt(float speed)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableRecoveryHeating = false; // Cubic potential, before the recovery bound.
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();

            simulation.StepExact(1, Worlds.Flight(0.8f, speed));
            return simulation.Solver.Nodes[0].LastFrictionWatts;
        }


        [Fact]

        public void TheEffectiveCoefficientCarriesTheAtmosphereBlend()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            foreach (float density in new float[] { 1f, 0.5f, 0.25f, 0.05f, 0.001f })
            {
                EnvironmentState state = EnvironmentSolver.Solve(
                    settings, planet, Worlds.PlanetSurface(density, 0.5f));

                Assert.Equal(
                    state.ConvectionCoefficient * state.AtmosphereFactor,
                    state.EffectiveConvectionCoefficient,
                    4);

                Assert.Equal(planet.ConvectionCoefficient, state.ConvectionCoefficient, 2);
            }
        }

        [Fact]

        public void TheEffectiveCoefficientFallsWithTheAir()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            float sealevel = EnvironmentSolver.Solve(
                settings, planet, Worlds.PlanetSurface(1f, 0.5f)).EffectiveConvectionCoefficient;
            float thin = EnvironmentSolver.Solve(
                settings, planet, Worlds.PlanetSurface(0.1f, 0.5f)).EffectiveConvectionCoefficient;
            float nearVacuum = EnvironmentSolver.Solve(
                settings, planet, Worlds.PlanetSurface(0.0001f, 0.5f)).EffectiveConvectionCoefficient;

            Assert.Equal(planet.ConvectionCoefficient, sealevel, 2);
            Assert.True(sealevel > thin);
            Assert.True(thin > nearVacuum);
            Assert.True(nearVacuum < 0.05f);
        }

        [Fact]

        public void ConvectiveWattsFallWithTheAir()
        {

            float thick = ConvectionWatts(1f);

            float thin = ConvectionWatts(0.05f);

            float nearVacuum = ConvectionWatts(0.0001f);

            Assert.True(thick < 0f);
            Assert.True(thick < thin);
            Assert.True(thin < nearVacuum);
            Assert.True(nearVacuum > -1000f);
        }

        [Fact]

        public void ASettledTemperatureIsNotMonotonicInAirDensity()
        {

            float sealevel = SettledTemperature(1f);

            float thin = SettledTemperature(0.05f);

            float vacuum = SettledTemperature(0f);

            Assert.True(thin < sealevel);
            Assert.True(vacuum > sealevel);
        }

        [Fact]

        public void WindStillRaisesTheCoefficientAtEveryDensity()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            foreach (float density in new float[] { 1f, 0.25f })
            {
                float still = EnvironmentSolver.Solve(settings, planet,
                    Worlds.PlanetSurface(density, 0.5f)).EffectiveConvectionCoefficient;
                float windy = EnvironmentSolver.Solve(settings, planet,
                    Worlds.PlanetSurface(density, 0.5f, 30f)).EffectiveConvectionCoefficient;

                Assert.True(windy > still);
            }
        }


        private static ThermalSimulation Rig(float density, out EnvironmentSample sample)
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                MaxSubsteps = 64,
                MaxElementVisitsPerStep = 0,
                EnableSolarHeat = false,
            };
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Wasting(50000f);

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            simulation.Planet = PlanetThermalProperties.Default();

            sample = Worlds.PlanetSurface(density, 0.5f);
            return simulation;
        }


        private static float SettledTemperature(float density)
        {
            EnvironmentSample sample;

            ThermalSimulation simulation = Rig(density, out sample);

            for (int step = 0; step < LabClock.Steps(600); step++) simulation.StepExact(1, sample);
            return simulation.Solver.Nodes[0].Temperature;
        }


        private static float ConvectionWatts(float density)
        {
            EnvironmentSample sample;

            ThermalSimulation simulation = Rig(density, out sample);
            simulation.Solver.CollectDiagnostics = true;

            for (int step = 0; step < LabClock.Steps(600); step++) simulation.StepExact(1, sample);
            return simulation.Solver.Nodes[0].LastConvectionWatts;
        }
    }
}

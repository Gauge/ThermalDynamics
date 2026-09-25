using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Wind and grid velocity, separately and together, against the friction system.
    ///
    /// The hull feels one thing: the relative wind, composed as ambient wind minus grid velocity.
    /// Friction and forced convection both read that one scalar, which makes the composed cases
    /// impossible to attribute in play — a session with a storm and a moving ship shows a heat
    /// that could belong to either. These tests hold each contributor still while the other
    /// moves, and then pin the compositions where the sum surprises: a headwind that doubles the
    /// airflow neither part supplies alone, and a downwind run that heats at walking-pace airflow
    /// at full throttle. Since 2026-09-02 friction is live at every speed — the old 50 m/s
    /// threshold ships at 0 — so the compositions move watts smoothly rather than tripping a gate.
    /// </summary>
    public class FrictionIsolationTests
    {
        private const float ThickAir = 1f;


        private static ThermalSimulation Rig()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float FrictionWatts(EnvironmentSample sample)
        {

            ThermalSimulation simulation = Rig();
            simulation.StepExact(1, sample);
            return simulation.Solver.Nodes[0].LastFrictionWatts;
        }


        [Fact]

        public void MotionAloneHeatsAtEverySpeed()
        {

            float fast = FrictionWatts(Worlds.Flight(ThickAir, 80f));

            float slow = FrictionWatts(Worlds.Flight(ThickAir, 40f));

            Assert.True(slow > 0f, "slow flight has to heat now that the floor ships at zero");
            Assert.True(fast > slow, "faster airflow has to heat harder");
        }

        [Fact]

        public void WindAloneHeatsAtEverySpeed()
        {

            float fast = FrictionWatts(Worlds.Storm(ThickAir, 80f));

            float slow = FrictionWatts(Worlds.Storm(ThickAir, 40f));

            Assert.True(slow > 0f, "a slow wind has to heat now that the floor ships at zero");
            Assert.True(fast > slow, "a faster wind has to heat harder");
        }

        [Fact]

        public void AStormAndAFlightAtTheSameAirspeedAreTheSameHeat()
        {

            float storm = FrictionWatts(Worlds.Storm(ThickAir, 80f));

            float flight = FrictionWatts(Worlds.Flight(ThickAir, 80f));

            Assert.True(storm > 0f);
            Assert.Equal(storm, flight, 2);
        }


        [Fact]

        public void FlyingWithTheWindAtItsOwnSpeedIsCalmAir()
        {
            EnvironmentSample sample = Worlds.WindAndMotion(
                ThickAir, 80f, Vector3.Forward, Vector3.Forward * 80f);

            Assert.Equal(0f, sample.RelativeWindSpeed, 3);
            Assert.Equal(0f, FrictionWatts(sample));
        }

        [Fact]

        public void AHeadwindHeatsAsTheSumOfItsParts()
        {
            EnvironmentSample headwind = Worlds.WindAndMotion(
                ThickAir, 40f, Vector3.Forward, Vector3.Backward * 40f);

            Assert.Equal(80f, headwind.RelativeWindSpeed, 3);


            float watts = FrictionWatts(headwind);
            Assert.True(watts > 0f);
            Assert.Equal(FrictionWatts(Worlds.Flight(ThickAir, 80f)), watts, 2);
        }

        [Fact]

        public void ATailwindHeatsAtTheAirflowNotTheGroundSpeed()
        {
            EnvironmentSample downwind = Worlds.WindAndMotion(
                ThickAir, 60f, Vector3.Forward, Vector3.Forward * 80f);

            Assert.Equal(20f, downwind.RelativeWindSpeed, 3);


            float watts = FrictionWatts(downwind);
            Assert.True(watts > 0f, "20 m/s of airflow has to heat now that the floor ships at zero");
            Assert.Equal(FrictionWatts(Worlds.Flight(ThickAir, 20f)), watts, 2);
            Assert.True(watts < FrictionWatts(Worlds.Flight(ThickAir, 80f)) / 32f,
                "the tailwind case has to sit far below the ground-speed figure, or composition is broken");
        }

        [Fact]

        public void ACrosswindComposesByVectorNotByAddition()
        {
            EnvironmentSample crosswind = Worlds.WindAndMotion(
                ThickAir, 60f, Vector3.Forward, Vector3.Right * 60f);

            Assert.Equal(60f * (float)System.Math.Sqrt(2), crosswind.RelativeWindSpeed, 2);

            Vector3 airflow = crosswind.RelativeWindDirectionLocal;
            Assert.Equal(airflow.Z, airflow.X, 3);
            Assert.True(airflow.X < 0f && airflow.Z < 0f);
        }


        [Fact]

        public void ForcedConvectionCannotTellWindFromMotion()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Derive();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            EnvironmentState storm = EnvironmentSolver.Solve(settings, planet, Worlds.Storm(ThickAir, 30f));
            EnvironmentState flight = EnvironmentSolver.Solve(settings, planet, Worlds.Flight(ThickAir, 30f));
            EnvironmentState calm = EnvironmentSolver.Solve(
                settings, planet, Worlds.WindAndMotion(ThickAir, 30f, Vector3.Forward, Vector3.Forward * 30f));

            Assert.Equal(storm.ConvectionCoefficient, flight.ConvectionCoefficient, 3);
            Assert.True(calm.ConvectionCoefficient < storm.ConvectionCoefficient);
        }

        [Fact]

        public void FrictionGrowsWithTheCubeOfTheAirspeed()
        {

            float at60 = FrictionWatts(Worlds.Flight(ThickAir, 60f));

            float at120 = FrictionWatts(Worlds.Flight(ThickAir, 120f));

            Assert.True(at60 > 0f);
            Assert.Equal(8f, at120 / at60, 2);
        }

        [Fact]

        public void TheFrictionLawStillHoldsAtARaisedSpeedLimit()
        {

            float at100 = FrictionWatts(Worlds.Flight(ThickAir, 100f));

            float at300 = FrictionWatts(Worlds.Flight(ThickAir, 300f));

            Assert.True(at100 > 0f, "100 m/s is above the friction threshold");
            Assert.Equal(27f, at300 / at100, 1);

            Assert.Equal(at300, FrictionWatts(Worlds.Storm(ThickAir, 300f)), 0);

            Assert.Equal(0f, FrictionWatts(
                Worlds.WindAndMotion(ThickAir, 300f, Vector3.Forward, Vector3.Forward * 300f)));
        }

        [Fact]

        public void ForcedConvectionSaturatesWhileFrictionCubes()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Derive();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            float calm = EnvironmentSolver.Solve(
                settings, planet, Worlds.Flight(ThickAir, 0f)).ConvectionCoefficient;
            float at100 = EnvironmentSolver.Solve(
                settings, planet, Worlds.Flight(ThickAir, 100f)).ConvectionCoefficient;
            float at300 = EnvironmentSolver.Solve(
                settings, planet, Worlds.Flight(ThickAir, 300f)).ConvectionCoefficient;

            Assert.Equal(2.000f, at100 / calm, 2);
            Assert.Equal(2.732f, at300 / calm, 2);

            Assert.True(at300 / at100 < 1.4f,
                "the airflow term saturates: three times the speed is under 1.4x the cooling");
        }

        [Fact]

        public void ActivationFollowsTheRelativeWindThroughTheSolve()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.Derive();
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            Assert.True(EnvironmentSolver.Solve(
                settings, planet, Worlds.WindAndMotion(ThickAir, 40f, Vector3.Forward, Vector3.Backward * 40f))
                .FrictionActive);

            Assert.False(EnvironmentSolver.Solve(
                settings, planet, Worlds.WindAndMotion(ThickAir, 60f, Vector3.Forward, Vector3.Forward * 60f))
                .FrictionActive);
        }
    }
}

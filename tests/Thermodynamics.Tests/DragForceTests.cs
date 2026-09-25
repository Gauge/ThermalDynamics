using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The force the mod has been computing and throwing away.**
    ///
    /// <para>
    /// The friction term is drag power under another name, so the force is one division: power is
    /// force times speed. Deriving it from the watts rather than recomputing it from the geometry
    /// is what keeps the two consistent — a second expression would be a second place for the
    /// exposure, the wind direction and the density to be got subtly differently, and the two would
    /// disagree only on the ships where it mattered.
    /// </para>
    ///
    /// <para>
    /// These tests are about the arithmetic and the refusals. Whether the force is *applied* is the
    /// game layer, which no harness runs — see known-issues.md.
    /// </para>
    /// </summary>
    public class DragForceTests
    {

        private static ThermalSettings Settings(float frictionScale = 0.001f, float dragCoefficient = 1f)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.FrictionScale = frictionScale;
            settings.DragCoefficient = dragCoefficient;
            settings.Derive();
            return settings;
        }

        [Fact]

        public void TheForceIsThePowerOverTheSpeed()
        {

            ThermalSettings settings = Settings();

            Assert.Equal(5.0e6f, DragForce.Newtons(1.0e6f, 100f, settings), 0);
        }

        [Fact]

        public void TheMedianHullsMissingForceIsWhatTheBacklogSays()
        {
            float heatingShare = 5.05e6f / 300f;
            Assert.True(System.Math.Abs(heatingShare - 16.8e3f) < 100f,
                "the backlog's 16.8 kN is not 5.05 MW over 300 m/s, it is " + heatingShare);


            ThermalSettings settings = Settings();
            float whole = DragForce.Newtons(5.05e6f, 300f, settings);

            float expected = heatingShare / 0.002f;
            Assert.True(System.Math.Abs(whole - expected) <= expected * 1e-5f,
                "expected " + expected + " N and got " + whole + " N");
        }

        [Fact]

        public void TheForceScalesWithTheDragCoefficient()
        {
            float one = DragForce.Newtons(1.0e6f, 100f, Settings(dragCoefficient: 1f));
            float two = DragForce.Newtons(1.0e6f, 100f, Settings(dragCoefficient: 2f));

            Assert.Equal(2f * one, two, 0);
        }

        [Fact]

        public void TuningTheHeatDialLeavesTheForceAlone()
        {
            float baseline = DragForce.Newtons(1.0e6f, 100f, Settings(frictionScale: 0.001f));
            float tuned = DragForce.Newtons(2.0e6f, 100f, Settings(frictionScale: 0.002f));

            Assert.Equal(baseline, tuned, 0);
        }

        [Theory]
        [InlineData(0f, 100f)]
        [InlineData(1.0e6f, 0f)]
        [InlineData(-1f, 100f)]

        public void NoForceWhereThereIsNothingToDivide(float watts, float speed)
        {
            Assert.Equal(0f, DragForce.Newtons(watts, speed, Settings()));
        }

        [Fact]

        public void AWorldWithNoFrictionTermHasNoDrag()
        {
            Assert.Equal(0f, DragForce.Newtons(1.0e6f, 100f, Settings(frictionScale: 0f)));
            Assert.Equal(0f, DragForce.Newtons(1.0e6f, 100f, Settings(dragCoefficient: 0f)));
        }

        [Fact]

        public void TheForceIsAlongTheRelativeWind()
        {

            ThermalSettings settings = Settings();

            Vector3 wind = new Vector3(0f, 0f, 100f);

            Vector3 force = DragForce.Vector(1.0e6f, wind, settings);

            Assert.Equal(5.0e6f, force.Length(), 0);
            Assert.True(Vector3.Dot(Vector3.Normalize(force), Vector3.Normalize(wind)) > 0.999f,
                "the force is not along the wind");
        }

        [Fact]

        public void AHeadwindAndATailwindPushOppositeWays()
        {

            ThermalSettings settings = Settings();

            Vector3 ahead = DragForce.Vector(1.0e6f, new Vector3(0f, 0f, 100f), settings);
            Vector3 behind = DragForce.Vector(1.0e6f, new Vector3(0f, 0f, -100f), settings);

            Assert.Equal(ahead.Length(), behind.Length(), 0);
            Assert.True(Vector3.Dot(ahead, behind) < 0f);
        }

        [Fact]

        public void StillAirIsNoForce()
        {
            Assert.Equal(Vector3.Zero, DragForce.Vector(1.0e6f, Vector3.Zero, Settings()));
        }

        [Fact]

        public void TheForceComesOutOfTheSolversOwnWatts()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(1, Worlds.Flight(1f, 120f));

            float watts = simulation.Solver.LastFrictionWatts;
            Assert.True(watts > 0f, "the hull took no drag, so this proves nothing");

            float newtons = DragForce.Newtons(watts, 120f, settings);

            float eta = settings.FrictionScale / (0.5f * settings.DragCoefficient);

            float expected = watts / eta / 120f;
            Assert.True(System.Math.Abs(newtons - expected) <= expected * 1e-5f,
                "expected " + expected + " N and got " + newtons + " N");
        }
    }
}

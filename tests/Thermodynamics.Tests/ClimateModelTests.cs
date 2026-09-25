using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The air a grid sits in: where on the planet, what the ground is, and how long the air takes
    /// to answer the sun.
    ///
    /// Measured against a test world, the model these replace gave a desert a 9 K day-night swing,
    /// left a snowfield at +4 to +14 C, and produced the same climate at 7 degrees of latitude as
    /// at 41 — every difference between those sites came from air density, which is a fact about
    /// altitude and not about climate.
    /// </summary>
    public class ClimateModelTests
    {

        private static PlanetThermalProperties Earthlike()
        {
            PlanetThermalProperties planet = PlanetThermalProperties.Default();
            planet.NightTemperature = 283.15f;
            planet.DayTemperature = 294.15f;
            planet.PoleTemperatureDrop = 40f;
            return planet;
        }


        private static float Sine(double degrees)
        {
            return (float)Math.Sin(degrees * Math.PI / 180d);
        }


        [Fact]

        public void TheEquatorGetsThePlanetsOwnFigures()
        {

            PlanetThermalProperties planet = Earthlike();

            Assert.Equal(planet.DayTemperature, ClimateModel.Target(planet, 0f, 1f, 0f), 2);
            Assert.Equal(planet.NightTemperature, ClimateModel.Target(planet, 0f, -1f, 0f), 2);
        }

        [Fact]

        public void ThePolesAreColderThanTheEquatorDayAndNight()
        {

            PlanetThermalProperties planet = Earthlike();

            float equatorNoon = ClimateModel.Target(planet, 0f, 1f, 0f);
            float poleNoon = ClimateModel.Target(planet, 1f, 1f, 0f);

            Assert.Equal(planet.PoleTemperatureDrop, equatorNoon - poleNoon, 2);
            Assert.True(ClimateModel.Target(planet, Sine(60), -1f, 0f)
                < ClimateModel.Target(planet, 0f, -1f, 0f));
        }

        [Fact]

        public void TheDropGrowsWithLatitudeRatherThanSteppingAtABand()
        {

            PlanetThermalProperties planet = Earthlike();

            float previous = ClimateModel.Target(planet, 0f, 0.5f, 0f);

            for (int latitude = 5; latitude <= 90; latitude += 5)
            {
                float current = ClimateModel.Target(planet, Sine(latitude), 0.5f, 0f);

                Assert.True(current <= previous, "should not warm toward the pole at " + latitude);
                Assert.True(previous - current < 8f, "should not step at " + latitude);

                previous = current;
            }
        }

        [Fact]

        public void APlanetWithNoPoleDropIsOneClimateAsItUsedToBe()
        {

            PlanetThermalProperties planet = Earthlike();
            planet.PoleTemperatureDrop = 0f;

            Assert.Equal(
                ClimateModel.Target(planet, 0f, 0.3f, 0f),
                ClimateModel.Target(planet, Sine(75), 0.3f, 0f), 3);
        }


        [Fact]

        public void BelowTheHorizonIsNightWhateverTheDepth()
        {

            PlanetThermalProperties planet = Earthlike();

            float dusk = ClimateModel.Target(planet, 0f, -0.01f, 0f);
            float midnight = ClimateModel.Target(planet, 0f, -1f, 0f);

            Assert.Equal(dusk, midnight, 3);
        }

        [Fact]

        public void WarmthFollowsTheSunsHeight()
        {

            PlanetThermalProperties planet = Earthlike();

            Assert.True(ClimateModel.Target(planet, 0f, Sine(60), 0f)
                > ClimateModel.Target(planet, 0f, Sine(20), 0f));
        }


        [Fact]

        public void TheGroundShiftsTheAirAboveItBothWays()
        {

            PlanetThermalProperties planet = Earthlike();

            float bare = ClimateModel.Target(planet, 0f, 0.5f, 0f);

            Assert.Equal(bare - 14f, ClimateModel.Target(planet, 0f, 0.5f, -14f), 3);
            Assert.Equal(bare + 9f, ClimateModel.Target(planet, 0f, 0.5f, 9f), 3);
        }

        [Fact]

        public void SnowIsColdAndSandIsWarmAndAnythingUnknownIsNeither()
        {
            Assert.True(GroundTemperature.OffsetFor("Snow") < -10f);
            Assert.True(GroundTemperature.OffsetFor("MyPlanet_Snow_01") < -10f);
            Assert.True(GroundTemperature.OffsetFor("Sand_02") > 5f);
            Assert.True(GroundTemperature.OffsetFor("Desert_Sand") > 5f);

            Assert.Equal(0f, GroundTemperature.OffsetFor("Unobtainium"), 3);
            Assert.Equal(0f, GroundTemperature.OffsetFor(""), 3);
            Assert.Equal(0f, GroundTemperature.OffsetFor(null), 3);
        }


        [Fact]

        public void AWiderSwingCoolsTheNightAsMuchAsItWarmsTheNoon()
        {

            PlanetThermalProperties planet = Earthlike();

            float flatDay = ClimateModel.Target(planet, 0f, 1f, 0f, 1f);
            float flatNight = ClimateModel.Target(planet, 0f, -1f, 0f, 1f);
            float mean = (flatDay + flatNight) * 0.5f;

            float wideDay = ClimateModel.Target(planet, 0f, 1f, 0f, 2f);
            float wideNight = ClimateModel.Target(planet, 0f, -1f, 0f, 2f);

            Assert.Equal(mean, (wideDay + wideNight) * 0.5f, 2);
            Assert.Equal((flatDay - flatNight) * 2f, wideDay - wideNight, 2);
        }

        [Fact]

        public void NoSwingIsTheSameTemperatureAllDay()
        {

            PlanetThermalProperties planet = Earthlike();

            Assert.Equal(
                ClimateModel.Target(planet, 0f, 1f, 0f, 0f),
                ClimateModel.Target(planet, 0f, -1f, 0f, 0f), 3);
        }

        [Fact]

        public void SandSwingsMoreThanSnowAndBothStillPointTheRightWay()
        {
            GroundTemperature.Ground sand = GroundTemperature.For("Sand_02");
            GroundTemperature.Ground snow = GroundTemperature.For("Snow");
            GroundTemperature.Ground unknown = GroundTemperature.For("Unobtainium");

            Assert.True(sand.Swing > 1.4f, "dry sand should swing hard");
            Assert.True(snow.Swing < 0.9f, "snow should hold its temperature");
            Assert.True(sand.Offset > 0f && snow.Offset < 0f);

            Assert.Equal(0f, unknown.Offset, 3);
            Assert.Equal(1f, unknown.Swing, 3);
        }


        [Fact]

        public void AirChasesTheSunRatherThanTrackingIt()
        {
            float ambient = ClimateModel.Follow(283.15f, 303.15f, 10f, 45f);

            Assert.True(ambient > 283.15f, "should have warmed");
            Assert.True(ambient < 303.15f, "should not have arrived");
        }

        [Fact]

        public void OneLagClosesAboutTwoThirdsOfTheGap()
        {
            float ambient = ClimateModel.Follow(0f + 100f, 200f, 45f, 45f);

            Assert.Equal(100f + (100f * 0.632f), ambient, 1);
        }

        [Fact]

        public void EnoughTimeArrives()
        {
            Assert.Equal(300f, ClimateModel.Follow(200f, 300f, 1000f, 45f), 1);
        }

        [Fact]

        public void NoLagMeansTheTargetAtOnce()
        {
            Assert.Equal(300f, ClimateModel.Follow(200f, 300f, 10f, 0f), 3);
            Assert.Equal(300f, ClimateModel.Follow(0f, 300f, 10f, 45f), 3);
        }

        [Fact]

        public void TheHottestPartOfTheDayLandsAfterNoon()
        {

            PlanetThermalProperties planet = Earthlike();

            float ambient = planet.NightTemperature;
            float hottest = float.MinValue;
            int hottestStep = 0;
            int noonStep = 0;
            float highestSun = float.MinValue;

            const int steps = 360;
            for (int i = 0; i < steps; i++)
            {

                float elevation = Sine(i - 90);

                if (elevation > highestSun)
                {
                    highestSun = elevation;
                    noonStep = i;
                }

                float target = ClimateModel.Target(planet, 0f, elevation, 0f);
                ambient = ClimateModel.Follow(ambient, target, 1f, 20f);

                if (ambient > hottest)
                {
                    hottest = ambient;
                    hottestStep = i;
                }
            }

            Assert.True(hottestStep > noonStep,
                "peak at " + hottestStep + " should be after noon at " + noonStep);
        }
    }
}

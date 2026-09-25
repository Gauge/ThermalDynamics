using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The wind map that stands in for the one the game does not have.
    ///
    /// What the game gives is a planet-wide ceiling — its definition's maximum wind scaled by air
    /// density — which on an earthlike world reads 80 m/s at sea level everywhere. Treated as a
    /// wind it puts every parked ship in a permanent hurricane, over the friction threshold and at
    /// double the still-air convection. These pin the field that replaces it: bands like Earth's,
    /// a speed that is a fraction of the ceiling, and nothing that varies from one moment to the
    /// next.
    /// </summary>
    public class WindFieldTests
    {

        private static readonly Vector3 Axis = new Vector3(0f, 1f, 0f);


        private static Vector3 Up(double latitudeDegrees, double longitudeDegrees = 0d)
        {
            double lat = latitudeDegrees * Math.PI / 180d;
            double lon = longitudeDegrees * Math.PI / 180d;

            return Vector3.Normalize(new Vector3(
                (float)(Math.Cos(lat) * Math.Cos(lon)),
                (float)Math.Sin(lat),
                (float)(Math.Cos(lat) * Math.Sin(lon))));
        }


        private static float Eastward(double latitude)
        {

            Vector3 up = Up(latitude);
            Vector3 east = Vector3.Normalize(Vector3.Cross(Axis, up));

            return Vector3.Dot(WindField.Direction(up, Axis), east);
        }


        private static float Poleward(double latitude)
        {

            Vector3 up = Up(latitude);
            Vector3 east = Vector3.Normalize(Vector3.Cross(Axis, up));
            Vector3 north = Vector3.Normalize(Vector3.Cross(up, east));

            return Vector3.Dot(WindField.Direction(up, Axis), latitude < 0d ? -north : north);
        }


        private static Vector3 Velocity(double latitude)
        {

            Vector3 up = Up(latitude);
            return WindField.Direction(up, Axis) * WindField.BandStrength(up, Axis);
        }

        [Fact]

        public void WindBlowsAlongTheGroundAndNotThroughIt()
        {
            for (int latitude = -85; latitude <= 85; latitude += 5)
            {

                Vector3 up = Up(latitude);
                Vector3 wind = WindField.Direction(up, Axis);

                Assert.True(Math.Abs(Vector3.Dot(wind, up)) < 1e-4f,
                    "wind at " + latitude + " should be tangent to the surface");

                float strength = WindField.BandStrength(up, Axis);
                if (strength < 1e-3f)
                {
                    Assert.Equal(0f, wind.Length(), 3);
                    continue;
                }

                Assert.Equal(1f, wind.Length(), 3);
            }
        }

        [Fact]

        public void TheBandsRunEasterlyWesterlyEasterlyOutFromTheEquator()
        {
            Assert.True(Eastward(10) < -0.5f, "trades near the equator should blow west");
            Assert.True(Eastward(45) > 0.5f, "mid latitudes should blow east");
            Assert.True(Eastward(75) < -0.5f, "polar latitudes should blow west");
        }

        [Fact]

        public void TheSouthernHemisphereMirrorsTheNorthern()
        {
            for (int latitude = 5; latitude <= 85; latitude += 10)
            {
                Assert.Equal(Eastward(latitude), Eastward(-latitude), 3);
            }
        }

        [Fact]

        public void TheWindSwingsThroughTheCalmsRatherThanReversingAcrossALine()
        {

            Vector3 previous = Velocity(-89);

            for (int latitude = -88; latitude <= 89; latitude++)
            {

                Vector3 current = Velocity(latitude);
                Assert.True((current - previous).Length() < 0.25f,
                    "wind turned too sharply between " + (latitude - 1) + " and " + latitude
                    + ": " + previous + " to " + current);
                previous = current;
            }
        }

        [Fact]

        public void TheSidewaysComponentFollowsTheBand()
        {
            foreach (int latitude in new[] { 15, -15, 75, -75 })
            {
                Assert.True(Poleward(latitude) < -0.1f,
                    "the trades and the polar easterlies blow toward the equator, not away from it;"

                    + " at " + latitude + " the poleward component is " + Poleward(latitude));
            }

            foreach (int latitude in new[] { 45, -45 })
            {
                Assert.True(Poleward(latitude) > 0.1f,
                    "the westerlies blow toward the pole; at " + latitude

                    + " the poleward component is " + Poleward(latitude));
            }
        }

        [Fact]

        public void TheEquatorIsACalmRatherThanAReversal()
        {
            Assert.True(Velocity(0).Length() < 0.02f,
                "the doldrums should be calm, not the windiest place on the planet");

            Assert.True((Velocity(0.5) - Velocity(-0.5)).Length() < 0.2f,
                "half a degree either side of the equator should be nearly the same wind");
        }

        [Fact]

        public void ABandIsStrongestInItsMiddleAndCalmAtItsEdges()
        {
            foreach (int latitude in new[] { 0, 30, 60, 90 })
            {
                Assert.True(WindField.BandStrength(Up(latitude), Axis) < 0.01f,
                    latitude + " is a band edge and should be calm");
            }

            foreach (int latitude in new[] { 15, 45, 75 })
            {
                Assert.True(WindField.BandStrength(Up(latitude), Axis) > 0.99f,
                    latitude + " is the middle of a band and should be its windiest");
            }

            for (int latitude = 5; latitude <= 85; latitude += 10)
            {
                Assert.Equal(WindField.BandStrength(Up(latitude), Axis),
                    WindField.BandStrength(Up(-latitude), Axis), 3);
            }
        }

        [Fact]

        public void APoleHasNoOneDirectionAndSaysSo()
        {
            Assert.Equal(Vector3.Zero, WindField.Direction(Axis, Axis));
        }

        [Fact]

        public void OrdinaryWeatherIsABreezeRatherThanTheCeiling()
        {
            float calm = WindField.Speed(80f, 0f, 0.5f);

            Assert.InRange(calm, 4f, 20f);
        }

        [Fact]

        public void WeatherRaisesTheWindWithoutReachingTheCeiling()
        {
            float calm = WindField.Speed(80f, 0f, 0.5f);
            float storm = WindField.Speed(80f, 1f, 0.5f);

            Assert.True(storm > calm * 2f, "a storm should be much windier than a calm day");
            Assert.True(storm < 80f, "and still under the planet's own maximum");
        }

        [Fact]

        public void NoAtmosphereIsNoWind()
        {
            Assert.Equal(0f, WindField.Speed(0f, 1f, 1f), 5);
        }

        [Fact]

        public void TwoPlacesInTheSameWeatherDoNotHaveTheSameWind()
        {
            float here = WindField.Speed(80f, 0.2f, WindField.Variation(new Vector3D(0, 0, 0)));
            float there = WindField.Speed(80f, 0.2f, WindField.Variation(new Vector3D(4000, 0, 0)));

            Assert.NotEqual(here, there, 2);
        }

        [Fact]

        public void TheSamePlaceAlwaysHasTheSameWind()
        {

            Vector3D place = new Vector3D(1234.5, -678.9, 4321.0);

            Assert.Equal(WindField.Variation(place), WindField.Variation(place), 6);
            Assert.Equal(
                WindField.Velocity(Up(30), Axis, 80f, 0.3f, WindField.Variation(place)),
                WindField.Velocity(Up(30), Axis, 80f, 0.3f, WindField.Variation(place)));
        }

        [Fact]

        public void VariationStaysInsideItsRange()
        {

            Random random = new Random(11);

            for (int i = 0; i < 500; i++)
            {

                Vector3D place = new Vector3D(
                    random.NextDouble() * 120000d - 60000d,
                    random.NextDouble() * 120000d - 60000d,
                    random.NextDouble() * 120000d - 60000d);

                float variation = WindField.Variation(place);
                Assert.InRange(variation, 0f, 1f);
            }
        }

        [Fact]

        public void AParkedShipIsNotFlying()
        {
            for (int latitude = -85; latitude <= 85; latitude += 5)
            {
                float speed = WindField.Speed(80f, 0f, WindField.Variation(new Vector3D(latitude * 700, 0, 0)));

                Assert.True(speed < WindScenarios.FrictionThreshold,
                    "wind at " + latitude + " was " + speed + " m/s, into material friction heating");
            }
        }
    }
}

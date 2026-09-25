using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The offline planet, and the three questions a field dump could not answer.
    ///
    /// The first run of wind telemetry from a real world produced 2.9 minutes of samples, at
    /// latitudes 5° to 30°, on ground so gentle that sheltering never took more than 7% and
    /// channelling never turned the wind more than a degree for 57% of samples. None of the model's
    /// interesting behaviour was exercised. These hold the simulator to exercising all of it, so
    /// that a change to the model is answerable at a desk and only the *world* has to be checked in
    /// game.
    /// </summary>
    public class WindLabTests
    {

        private static List<WindLab.Row> AtHeight(List<WindLab.Row> rows, double height)
        {

            List<WindLab.Row> found = new List<WindLab.Row>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (Math.Abs(rows[i].HeightAboveGround - height) < 1e-6d) found.Add(rows[i]);
            }
            return found;
        }


        private static double Mean(List<WindLab.Row> rows, Func<WindLab.Row, double> of)
        {
            if (rows.Count == 0) return 0d;
            double total = 0d;
            for (int i = 0; i < rows.Count; i++) total += of(rows[i]);
            return total / rows.Count;
        }


        private static void DayNight(
            List<WindLab.Row> rows, double height, out double day, out double night)
        {
            Dictionary<double, double> dayTotal = new Dictionary<double, double>();
            Dictionary<double, double> nightTotal = new Dictionary<double, double>();
            Dictionary<double, int> dayCount = new Dictionary<double, int>();
            Dictionary<double, int> nightCount = new Dictionary<double, int>();

            for (int i = 0; i < rows.Count; i++)
            {
                WindLab.Row r = rows[i];
                if (Math.Abs(r.HeightAboveGround - height) > 1e-6d) continue;

                if (r.Heating > 0.6f) Add(dayTotal, dayCount, r.Latitude, r.Speed);
                else if (r.Heating < 0.05f) Add(nightTotal, nightCount, r.Latitude, r.Speed);
            }

            double dayMeans = 0d, nightMeans = 0d;
            int latitudes = 0;

            foreach (KeyValuePair<double, int> entry in dayCount)
            {
                int nights;
                if (!nightCount.TryGetValue(entry.Key, out nights) || nights == 0) continue;

                double dayMean = dayTotal[entry.Key] / entry.Value;
                double nightMean = nightTotal[entry.Key] / nights;

                if (dayMean <= 1e-6d && nightMean <= 1e-6d) continue;

                dayMeans += dayMean;
                nightMeans += nightMean;
                latitudes++;
            }

            Assert.True(latitudes > 0,
                "no latitude at " + height + " m had both a hot afternoon and a cold night with"
                + " wind in it, so this comparison judges nothing");

            day = dayMeans / latitudes;
            night = nightMeans / latitudes;
        }


        private static void Add(Dictionary<double, double> totals, Dictionary<double, int> counts,
            double latitude, double speed)
        {
            double total;
            totals.TryGetValue(latitude, out total);
            totals[latitude] = total + speed;

            int count;
            counts.TryGetValue(latitude, out count);
            counts[latitude] = count + 1;
        }


        [Fact]

        public void TheEnginesOwnWindFigureIsReproducedExactly()
        {
            WindLab.Planet planet = new WindLab.Planet();

            Assert.Equal(80f, planet.WindCeiling(planet.AverageRadius), 4);
            Assert.Equal(40f, planet.WindCeiling(planet.AverageRadius + (planet.AtmosphereAltitude / 2d)), 4);
            Assert.Equal(0f, planet.WindCeiling(planet.AverageRadius + planet.AtmosphereAltitude), 4);
            Assert.Equal(0f, planet.WindCeiling(planet.AverageRadius + (planet.AtmosphereAltitude * 5d)), 4);

            Assert.Equal(80f, planet.WindCeiling(planet.AverageRadius - 2000d), 4);
        }

        [Fact]

        public void TheEnginesFalloffIsLinearRatherThanExponential()
        {
            WindLab.Planet planet = new WindLab.Planet();

            double quarter = planet.AverageRadius + (planet.AtmosphereAltitude * 0.25d);
            double half = planet.AverageRadius + (planet.AtmosphereAltitude * 0.5d);
            double threeQuarters = planet.AverageRadius + (planet.AtmosphereAltitude * 0.75d);

            Assert.Equal(
                planet.WindCeiling(quarter) - planet.WindCeiling(half),
                planet.WindCeiling(half) - planet.WindCeiling(threeQuarters), 3);
        }

        [Fact]

        public void TheEnginesWindIgnoresTheGroundEntirelyAndIsInvertedByIt()
        {
            WindLab.Planet planet = new WindLab.Planet();

            float valley = planet.WindCeiling(planet.AverageRadius - 500d);
            float ridge = planet.WindCeiling(planet.AverageRadius + 500d);

            Assert.True(valley > ridge,
                "the engine gives a valley more wind than a ridge; the sim must not quietly fix that");
        }


        [Fact]

        public void TheSurfaceIsWindiestByDayAndTheAirAloftIsWindiestAtNight()
        {
            WindLab.Options options = new WindLab.Options();
            options.SlopeStrength = 0f;

            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), options);

            double day, night;

            DayNight(rows, 2d, out day, out night);
            Assert.True(day > night * 1.3d,
                "at 2 m the afternoon should be much windier than the night: " + day + " vs " + night);

            DayNight(rows, 400d, out day, out night);
            Assert.True(night > day * 1.3d,
                "at 400 m the nocturnal jet should beat the afternoon: " + night + " vs " + day);
        }

        [Fact]

        public void SlopeWindCanOverturnTheSurfaceCycleOnSlopedGround()
        {
            List<WindLab.Row> withSlope = WindLab.Run(new WindLab.Planet(), new WindLab.Options());

            WindLab.Options off = new WindLab.Options();
            off.SlopeStrength = 0f;
            List<WindLab.Row> without = WindLab.Run(new WindLab.Planet(), off);

            double dayOn, nightOn, dayOff, nightOff;
            DayNight(withSlope, 2d, out dayOn, out nightOn);
            DayNight(without, 2d, out dayOff, out nightOff);

            Assert.True(nightOn > nightOff * 1.3d,
                "slope wind should lift the night-time surface wind materially: "
                + nightOff + " to " + nightOn);

            Assert.True(dayOff > nightOff * 1.3d, "and without it the plain cycle still shows");
        }

        [Fact]

        public void ThereIsAHeightBetweenThemWhereTheDayBarelyMatters()
        {
            WindLab.Options options = new WindLab.Options();
            options.SlopeStrength = 0f;
            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), options);

            double day, night;
            DayNight(rows, 100d, out day, out night);

            double ratio = day / night;
            Assert.InRange(ratio, 0.7d, 1.3d);
        }

        [Fact]

        public void AboveTheBoundaryLayerTheDayStopsMatteringAtAll()
        {
            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), new WindLab.Options());

            double day, night;
            DayNight(rows, 1200d, out day, out night);

            Assert.InRange(day / night, 0.93d, 1.07d);
        }

        [Fact]

        public void TheHeatingCurveCoversTheWholeDay()
        {
            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), new WindLab.Options());

            float low = 1f, high = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Heating < low) low = rows[i].Heating;
                if (rows[i].Heating > high) high = rows[i].Heating;
            }

            Assert.True(low < 0.05f, "the modelled day must contain a proper night, got " + low);
            Assert.True(high > 0.8f, "and a proper afternoon, got " + high);
        }


        [Fact]

        public void TheSimulatedGroundIsRoughEnoughToExerciseTheTerrainModel()
        {

            List<WindLab.Row> rows = AtHeight(
                WindLab.Run(new WindLab.Planet(), new WindLab.Options()), 10d);

            float leastShelter = 1f, mostSpeedUp = 0f, mostChannel = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Shelter < leastShelter) leastShelter = rows[i].Shelter;
                if (rows[i].SpeedUp > mostSpeedUp) mostSpeedUp = rows[i].SpeedUp;
                if (rows[i].ChannelDegrees > mostChannel) mostChannel = rows[i].ChannelDegrees;
            }

            Assert.True(leastShelter < 0.8f, "somewhere should be properly sheltered, got " + leastShelter);
            Assert.True(mostSpeedUp > 1.15f, "somewhere should be properly exposed, got " + mostSpeedUp);
            Assert.True(mostChannel > 15f, "somewhere should be properly channelled, got " + mostChannel);
        }

        [Fact]

        public void LevellingTheGroundSwitchesEveryTerrainEffectOff()
        {
            WindLab.Planet flat = new WindLab.Planet();
            flat.Ground = new WindLab.FlatTerrain();


            List<WindLab.Row> rows = AtHeight(WindLab.Run(flat, new WindLab.Options()), 10d);
            Assert.NotEmpty(rows);

            for (int i = 0; i < rows.Count; i++)
            {
                Assert.Equal(1f, rows[i].SpeedUp, 4);
                Assert.Equal(1f, rows[i].Shelter, 4);
                Assert.Equal(0f, rows[i].ChannelDegrees, 3);
            }
        }

        [Fact]

        public void TurningTerrainOffMatchesFlatteningTheWorld()
        {
            WindLab.Options off = new WindLab.Options();
            off.TerrainInfluence = 0f;

            WindLab.Planet flat = new WindLab.Planet();
            flat.Ground = new WindLab.FlatTerrain();


            List<WindLab.Row> a = AtHeight(WindLab.Run(new WindLab.Planet(), off), 10d);

            List<WindLab.Row> b = AtHeight(WindLab.Run(flat, new WindLab.Options()), 10d);

            Assert.Equal(a.Count, b.Count);
            Assert.Equal(Mean(a, r => r.SpeedUp), Mean(b, r => r.SpeedUp), 4);
            Assert.Equal(Mean(a, r => r.Shelter), Mean(b, r => r.Shelter), 4);
        }


        [Fact]

        public void EveryLatitudeIsSampledIncludingTheOnesAFieldRunNeverReaches()
        {
            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), new WindLab.Options());

            bool equator = false, mid = false, polar = false;
            for (int i = 0; i < rows.Count; i++)
            {
                double latitude = Math.Abs(rows[i].Latitude);
                if (latitude < 1d) equator = true;
                if (latitude > 35d && latitude < 65d) mid = true;
                if (latitude > 70d) polar = true;
            }

            Assert.True(equator && mid && polar);
        }

        [Fact]

        public void TheWindIsNeverNegativeNorAbsurdAnywhereOnThePlanetAtAnyHour()
        {
            WindLab.Options options = new WindLab.Options();
            options.WeatherIntensity = 1f;

            List<WindLab.Row> rows = WindLab.Run(new WindLab.Planet(), options);
            Assert.True(rows.Count > 10000);

            for (int i = 0; i < rows.Count; i++)
            {
                WindLab.Row r = rows[i];
                Assert.True(r.Speed >= 0f, "negative wind at latitude " + r.Latitude);
                Assert.True(r.Speed < 200f, "absurd wind " + r.Speed + " at latitude " + r.Latitude);
                Assert.InRange(r.SpeedUp, 0.1f, 2f);
                Assert.InRange(r.Shelter, 0.1f, 1.0001f);
                Assert.InRange(r.ChannelDegrees, 0f, 180f);
            }
        }

        [Fact]

        public void TheCsvCarriesTheSameColumnNamesTheGameWrites()
        {
            string csv = WindLab.Csv(WindLab.Run(new WindLab.Planet(), new WindLab.Options()));
            string header = csv.Substring(0, csv.IndexOf('\n'));

            foreach (string column in new string[]
            {
                "wind_agl_m", "wind_ceiling", "wind_band_share", "wind_profile", "wind_heating",
                "wind_speedup", "wind_shelter", "wind_channel_deg", "wind_speed", "wind_bearing_deg",
            })
            {
                Assert.Contains(column, header);
            }
        }

        [Fact]

        public void ARunIsRepeatable()
        {
            List<WindLab.Row> a = WindLab.Run(new WindLab.Planet(), new WindLab.Options());
            List<WindLab.Row> b = WindLab.Run(new WindLab.Planet(), new WindLab.Options());

            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i += 97)
            {
                Assert.Equal(a[i].Speed, b[i].Speed, 6);
                Assert.Equal(a[i].BearingDegrees, b[i].BearingDegrees, 6);
            }
        }
    }
}

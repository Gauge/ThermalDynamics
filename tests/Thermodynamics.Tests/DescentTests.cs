using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A ship digging from the surface to the core.
    ///
    /// The descent is where four parts of the environment hand over to each other, and each was
    /// written on its own: the sun goes at the surface, the wind over the hull's own height, the
    /// day's swing over tens of metres of rock, and the rock's own warmth only below the deadzone.
    /// A field dump showed the second of those was never happening — a grid ten metres under the
    /// surface was still reading four metres a second of wind, because the wind model clamped a
    /// negative height to zero and then blew at ground level.
    /// </summary>
    public class DescentTests
    {

        private static List<Descent.Reading> Readings()
        {
            return Descent.Run();
        }

        [Fact]

        public void TheWindBlowsWhileTheHullIsAboveTheSurface()
        {

            List<Descent.Reading> readings = Readings();

            Assert.True(Descent.At(readings, 40).WindSpeed > 0f);
            Assert.True(Descent.At(readings, 0).WindSpeed > 0f);
            Assert.Equal(1f, Descent.At(readings, 0).WindBurial);
        }

        [Fact]

        public void TheWindIsGoneOnceTheHullIsWhollyBuried()
        {

            List<Descent.Reading> readings = Readings();

            foreach (double height in new double[] { -6, -10, -20, -100, -1000, -20000 })
            {
                Descent.Reading reading = Descent.At(readings, height);

                Assert.Equal(0f, reading.WindSpeed);
                Assert.Equal(0f, reading.WindBurial);
            }
        }

        [Fact]

        public void TheWindFadesOverTheHullRatherThanStopping()
        {

            List<Descent.Reading> readings = Readings();

            float surface = Descent.At(readings, 0).WindSpeed;
            float half = Descent.At(readings, -Descent.HullReach * 0.5).WindSpeed;

            Assert.True(half > 0f);
            Assert.True(half < surface);
            Assert.InRange(Descent.At(readings, -Descent.HullReach * 0.5).WindBurial, 0.4f, 0.6f);
        }

        [Fact]

        public void TheWindFallsMonotonicallyThroughTheDig()
        {

            List<Descent.Reading> readings = Readings();

            float previous = float.MaxValue;
            for (int i = 0; i < readings.Count; i++)
            {
                if (readings[i].Height > 0) continue;

                Assert.True(readings[i].WindSpeed <= previous + 1e-4f,
                    "wind rose at " + readings[i].Height + " m");
                previous = readings[i].WindSpeed;
            }
        }

        [Fact]

        public void TheSunIsGoneAsSoonAsTheHullIsUnderTheSurface()
        {

            List<Descent.Reading> readings = Readings();

            Assert.True(Descent.At(readings, 10).SolarWatts > 0f);

            for (int i = 0; i < readings.Count; i++)
            {
                if (readings[i].Height >= 0) continue;

                Assert.Equal(1f, readings[i].SolarOcclusion);
                Assert.Equal(0f, readings[i].SolarWatts);
            }
        }

        [Fact]

        public void AmbientDampsToTheRockAndThenWarmsTowardsTheCore()
        {

            List<Descent.Reading> readings = Readings();

            PlanetThermalProperties planet = new PlanetThermalProperties();

            float shallow = Descent.At(readings, -100).AmbientKelvin;
            Assert.InRange(shallow, planet.UndergroundTemperature - 1f, planet.UndergroundTemperature + 1f);

            Assert.InRange(
                Descent.At(readings, -1000).AmbientKelvin,
                planet.UndergroundTemperature - 1f, planet.UndergroundTemperature + 1f);

            Assert.True(Descent.At(readings, -5000).AmbientKelvin > shallow);
            Assert.True(Descent.At(readings, -20000).AmbientKelvin > Descent.At(readings, -5000).AmbientKelvin);
            Assert.True(Descent.At(readings, -59000).AmbientKelvin < planet.CoreTemperature);
        }

        [Fact]

        public void AmbientRisesMonotonicallyBelowTheDeadzone()
        {

            List<Descent.Reading> readings = Readings();

            PlanetThermalProperties planet = new PlanetThermalProperties();

            float previous = 0f;
            for (int i = 0; i < readings.Count; i++)
            {
                if (readings[i].Depth <= planet.SealevelDeadzone) continue;

                Assert.True(readings[i].AmbientKelvin >= previous - 1e-3f,
                    "ambient fell at " + readings[i].Height + " m");
                previous = readings[i].AmbientKelvin;
            }
        }

        [Fact]

        public void TheGamesFlagTurnsBeforeTheHullIsActuallyBuried()
        {

            List<Descent.Reading> readings = Readings();
            Descent.Reading dug = Descent.At(readings, -1);

            Assert.True(dug.GameUnderground);
            Assert.True(dug.WindBurial > 0f);
            Assert.True(dug.WindSpeed > 0f);
        }

        [Fact]

        public void TheCsvCarriesEveryReading()
        {

            List<Descent.Reading> readings = Readings();
            string csv = Descent.Csv(readings);

            Assert.StartsWith("height_m,depth_m,ambient_k", csv);
            Assert.Equal(readings.Count + 1, csv.TrimEnd('\n').Split('\n').Length);
        }
    }
}

using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The lead cue's forecast, and the defect it exists to avoid.
    ///
    /// <para>
    /// The interesting tests here are the last two. Everything above them checks the arithmetic
    /// against itself, which is worth doing and proves nothing about whether the forecast is right
    /// about a real block; the last two run an actual grid and ask the solver.
    /// </para>
    /// </summary>
    public class HeatWarningTests
    {
        [Fact]

        public void AlreadyCrossedReadsZeroSeconds()
        {
            HeatForecast forecast = HeatWarning.Forecast(950f, 1f, 1f, 1f, 900f);

            Assert.True(forecast.WillCross);
            Assert.Equal(0f, forecast.Seconds);
        }

        [Fact]

        public void ACoolingBlockIsNeverWarnedAbout()
        {
            Assert.False(HeatWarning.Forecast(800f, -1f, 5f, 1f, 900f).WillCross);
            Assert.False(HeatWarning.Forecast(800f, 0f, 5f, 1f, 900f).WillCross);
        }

        [Fact]

        public void OneSampleGivesTheStraightLine()
        {
            HeatForecast forecast = HeatWarning.Forecast(800f, 10f, 0f, 1f, 900f);

            Assert.True(forecast.WillCross);
            Assert.Equal(10f, forecast.Seconds, 3);
        }

        [Fact]

        public void ABlockLevellingOffShortOfItsRatingIsNotWarnedAbout()
        {
            const float tau = 10f;
            const float settles = 850f;
            const float kelvin = 800f;

            float rate = (settles - kelvin) / tau;
            float previousRate = rate * (float)Math.Exp(1d / tau);

            float straightLine = (900f - kelvin) / rate;
            Assert.InRange(straightLine, 19f, 21f);

            HeatForecast forecast = HeatWarning.Forecast(kelvin, rate, previousRate, 1f, 900f);

            Assert.False(forecast.WillCross);
            Assert.Equal(settles, forecast.Settles, 0);
        }

        [Fact]

        public void ABlockHeadingPastItsRatingIsTimedOnTheCurveNotTheLine()
        {
            const float tau = 20f;
            const float settles = 1000f;
            const float kelvin = 800f;

            float rate = (settles - kelvin) / tau;
            float previousRate = rate * (float)Math.Exp(1d / tau);

            HeatForecast forecast = HeatWarning.Forecast(kelvin, rate, previousRate, 1f, 900f);

            Assert.True(forecast.WillCross);
            Assert.Equal(settles, forecast.Settles, 0);

            Assert.Equal(tau * (float)Math.Log(2d), forecast.Seconds, 1);

            Assert.True(forecast.Seconds > (900f - kelvin) / rate);
        }

        [Fact]

        public void AnAcceleratingBlockFallsBackToTheStraightLineAndWarnsEarly()
        {
            HeatForecast forecast = HeatWarning.Forecast(800f, 20f, 10f, 1f, 900f);

            Assert.True(forecast.WillCross);
            Assert.True(forecast.Accelerating);
            Assert.Equal(5f, forecast.Seconds, 3);
        }

        [Fact]

        public void NonsenseNumbersDoNotEscapeAsAWarning()
        {
            Assert.False(HeatWarning.Forecast(float.NaN, 1f, 1f, 1f, 900f).WillCross);
            Assert.False(HeatWarning.Forecast(800f, float.NaN, 1f, 1f, 900f).WillCross);
            Assert.False(HeatWarning.Forecast(800f, 1f, 1f, 1f, 0f).WillCross);
            HeatForecast backwards = HeatWarning.Forecast(800f, 1f, 1f, -1f, 900f);
            Assert.True(backwards.Accelerating);
            Assert.Equal(100f, backwards.Seconds, 3);
            Assert.True(float.IsPositiveInfinity(backwards.Settles));
        }

        [Fact]

        public void TheForecastNeverCriesWolfOnABlockThatSettlesShortOfCritical()
        {

            Sampled run = March(700f);

            Assert.True(run.Peak < run.Critical,
                "the rig was supposed to settle short of critical and reached "
                + run.Peak.ToString("n0") + " K against " + run.Critical.ToString("n0"));

            Assert.True(run.Peak >= run.Critical * HeatCueState.WatchFraction,
                "the block never entered the watched band, so nothing here was tested: "
                + run.Peak.ToString("n0") + " K against a watch point of "
                + (run.Critical * HeatCueState.WatchFraction).ToString("n0"));

            Assert.False(run.Warned,
                "the forecast warned about a block that settled at " + run.Peak.ToString("n0")
                + " K, short of " + run.Critical.ToString("n0"));

            Assert.True(run.HighestSettles < run.Critical,
                "the forecast read an equilibrium of " + run.HighestSettles.ToString("n0")
                + " K on a block that settles at " + run.Peak.ToString("n0"));
        }

        [Fact]

        public void TheCueArrivesBeforeTheCrossing()
        {

            Sampled run = March(1200f);

            Assert.True(run.Peak >= run.Critical,
                "the rig was supposed to cross and reached only " + run.Peak.ToString("n0") + " K");
            Assert.True(run.Warned, "no cue was raised before a crossing");

            float lead = run.CrossedAt - run.WarnedAt;
            Assert.True(lead > 0f,
                "the cue arrived at " + run.WarnedAt.ToString("n1") + " s and the crossing at "
                + run.CrossedAt.ToString("n1") + " s, so it was not a warning");

            Assert.InRange(lead, 0.5f, 4f * HeatWarning.LeadSeconds);
        }

        private struct Sampled
        {
            public float Critical;
            public float Peak;
            public bool Warned;
            public float WarnedAt;
            public float CrossedAt;

            public float HighestSettles;
        }

        private const float Critical = 900f;

        private const float Resting = 600f;


        private static Sampled March(float settlesAt)
        {
            const float seconds = 120f;
            const float mass = 1000f;


            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = false;
            settings.Derive();

            float interval = settings.StepSeconds;

            BlockThermalProperties source = Catalog.DefaultThermal();
            source.ProducerWasteEnergy = 0f;
            source.CriticalTemperature = Critical;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("source", Vector3I.One, mass, source), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, seconds);
            ThermalNode block = simulation.Solver.GetNode(builder.Last);

            double gap = Math.Pow(Resting, 4d) - Math.Pow(settings.VacuumTemperature, 4d);
            source.HeatSourceWatts = (float)(block.RadiationCoefficient * gap);
            block.RefreshHeatGeneration();

            for (float t = 0f; t < 600f; t += 1f) simulation.Update(1f, Worlds.Shadow());

            Assert.InRange(block.Temperature, Resting - 5f, Resting + 5f);

            gap = Math.Pow(settlesAt, 4d) - Math.Pow(settings.VacuumTemperature, 4d);
            source.HeatSourceWatts = (float)(block.RadiationCoefficient * gap);
            block.RefreshHeatGeneration();


            Sampled run = new Sampled();
            run.Critical = Critical;
            run.CrossedAt = float.PositiveInfinity;

            float previousKelvin = block.Temperature;
            float previousRate = 0f;
            bool hasRate = false;

            for (float t = interval; t <= seconds; t += interval)
            {
                simulation.Update(interval, Worlds.Shadow());

                float kelvin = block.Temperature;
                if (kelvin > run.Peak) run.Peak = kelvin;

                float rate = (kelvin - previousKelvin) / interval;

                if (kelvin >= run.Critical && float.IsPositiveInfinity(run.CrossedAt))
                {
                    run.CrossedAt = t;
                }

                HeatForecast forecast = HeatWarning.Forecast(kelvin, rate,
                    hasRate ? previousRate : 0f, interval, run.Critical);

                bool due = forecast.WillCross && forecast.Seconds <= HeatWarning.LeadSeconds;
                if (due && kelvin < run.Critical && !run.Warned)
                {
                    run.Warned = true;
                    run.WarnedAt = t;
                }

                if (!float.IsInfinity(forecast.Settles) && forecast.Settles > run.HighestSettles)
                {
                    run.HighestSettles = forecast.Settles;
                }

                previousKelvin = kelvin;
                previousRate = rate;
                hasRate = true;
            }

            return run;
        }

    }
}

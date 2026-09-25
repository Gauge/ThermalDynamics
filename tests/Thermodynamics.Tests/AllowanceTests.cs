using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Holds <see cref="AllowanceLab"/> to the two claims a decision about
    /// <c>MaxElementVisitsPerStep</c> rests on: that the setting is a per-frame budget, and that
    /// the rate it trades away has a price nobody may read off a straight line.
    ///
    /// <para>
    /// The timed columns are not asserted here — a millisecond is a claim about the machine
    /// (`M4`), and the rows that carry one are the lab's report rather than its contract. What is
    /// asserted is the arithmetic, the ordering between worlds, and the interpolation, all of
    /// which are properties of the code.
    /// </para>
    /// </summary>
    [Collection("load")]
    [Trait("speed", "slow")]
    public class AllowanceTests
    {
        private readonly ITestOutputHelper output;


        public AllowanceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void AnAllowanceIsAPerFrameBudgetScaledByTheStepRate()
        {
            List<AllowanceLab.Row> rows = AllowanceLab.Run("ship", new[] { 16000 },
                new[] { 1000000 }, new[] { "flight" }, AllowanceLab.DefaultFrames, null);

            AllowanceLab.Row row = rows[0];

            ThermalSettings settings = new ThermalSettings().Derive();
            double ceiling = row.Allowance * settings.StepsPerSecond / 60d;

            output.WriteLine(row.Blocks.ToString("n0") + " blocks in flight, allowance "
                + row.Allowance.ToString("n0") + ": demand " + row.Demand.ToString("n2")
                + ", granted " + row.Granted + ", rate "
                + (100d * row.Rate).ToString("n1") + "%, visits per frame "
                + row.VisitsPerFrame.ToString("n0") + " against a ceiling of "
                + ceiling.ToString("n0") + ".");

            Assert.True(row.Rate < 1d, "the allowance has to bind for there to be a ceiling");

            double slack = row.SubstepCost * settings.StepsPerSecond / 60d;
            Assert.InRange(row.VisitsPerFrame, ceiling - slack, ceiling + 1d);
        }

        [Fact]

        public void TheAllowanceBindsInAirLongBeforeItBindsInVacuum()
        {
            List<AllowanceLab.Row> rows = AllowanceLab.Run("ship", new[] { 16000 },
                new[] { 2000000 }, new[] { "vacuum", "flight" }, AllowanceLab.DefaultFrames, null);

            AllowanceLab.Row vacuum = rows[0];
            AllowanceLab.Row flight = rows[1];

            output.WriteLine("at " + vacuum.Blocks.ToString("n0")
                + " blocks on the shipped allowance: vacuum demands "
                + vacuum.Demand.ToString("n2") + " of " + vacuum.Granted + " granted and keeps "
                + (100d * vacuum.Rate).ToString("n1") + "% of real time; flight demands "
                + flight.Demand.ToString("n2") + " and keeps "
                + (100d * flight.Rate).ToString("n1") + "%.");

            Assert.Equal(vacuum.Granted, flight.Granted);
            Assert.True(flight.Demand > vacuum.Demand * 2f,
                "air is what makes a hull stiff, and the whole finding rests on it");

            Assert.Equal(1d, vacuum.Rate, 6);
            Assert.True(flight.Rate < 1d,
                "the shipped allowance binds on a hull this size once there is air to fly through");
        }

        [Fact]

        public void ThePriceLadderLandsOnTheSweepsOwnSlowClockRow()
        {
            ClientInputLab.Result published = ClientInputLab.Measure(
                new ClientInputLab.Degradation { SimSpeedError = -0.1f },
                ClientDriftLab.Correction.None);

            List<AllowanceLab.PriceRow> ladder =
                AllowanceLab.PriceRates(new[] { 0.10d }, null);

            output.WriteLine("sweep " + published.StandingKelvin.ToString("n2")
                + " K standing, ladder " + ladder[0].StandingKelvin.ToString("n2") + " K.");

            Assert.Equal(published.StandingKelvin, ladder[0].StandingKelvin, 3);
            Assert.Equal(published.PeakKelvin, ladder[0].PeakKelvin, 3);
        }

        [Fact]

        public void TheKelvinPriceOfALostRateIsNotLinearInIt()
        {
            List<AllowanceLab.PriceRow> ladder = AllowanceLab.PriceRates(
                new[] { 0.05d, 0.10d, 0.45d }, null);

            for (int i = 0; i < ladder.Count; i++)
            {
                output.WriteLine((100d * ladder[i].Deficit).ToString("n0") + "% slow: "
                    + ladder[i].StandingKelvin.ToString("n2") + " K standing, "
                    + ladder[i].KelvinPerUnit.ToString("n1") + " K per unit.");
            }

            Assert.True(ladder[1].StandingKelvin > ladder[0].StandingKelvin);
            Assert.True(ladder[2].StandingKelvin > ladder[1].StandingKelvin);

            Assert.True(ladder[2].KelvinPerUnit > ladder[0].KelvinPerUnit * 1.5d,
                "if this curve were straight the ladder would be a constant and `F23`'s one point "
                + "would have been enough");
        }

        [Fact]

        public void APriceIsInterpolatedAndNeverExtrapolated()
        {
            List<AllowanceLab.PriceRow> ladder = new List<AllowanceLab.PriceRow>
            {
                new AllowanceLab.PriceRow { Deficit = 0.10d, StandingKelvin = 10f },
                new AllowanceLab.PriceRow { Deficit = 0.20d, StandingKelvin = 30f },
            };

            Assert.Equal(0d, AllowanceLab.RateCost(At(1.00d), ladder), 6);
            Assert.Equal(5d, AllowanceLab.RateCost(At(0.95d), ladder), 6);
            Assert.Equal(10d, AllowanceLab.RateCost(At(0.90d), ladder), 6);
            Assert.Equal(20d, AllowanceLab.RateCost(At(0.85d), ladder), 6);
            Assert.Equal(30d, AllowanceLab.RateCost(At(0.80d), ladder), 6);

            Assert.Equal(30d, AllowanceLab.RateCost(At(0.50d), ladder), 6);
            Assert.False(AllowanceLab.PastTheLadder(At(0.85d), ladder));
            Assert.True(AllowanceLab.PastTheLadder(At(0.50d), ladder));
        }


        private static AllowanceLab.Row At(double rate)
        {
            return new AllowanceLab.Row { Rate = rate };
        }
    }
}

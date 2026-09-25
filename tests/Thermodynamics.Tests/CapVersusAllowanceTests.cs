using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **A shortened step against a floored one, on the same hull at the same allowance** —
    /// configuration.md, under FloorBlocksWhenOverBudget.
    ///
    /// <para>
    /// `C3` measured a per-block cap on all 8,144 published blueprints and refused it as a global
    /// default: the error is charged per block and the throughput is collected per grid, so four
    /// fifths of the population would pay and collect nothing. Where the budget *does* bind the
    /// mechanism is right, and this is the measurement that says whether swapping it in is an
    /// improvement rather than a different loss.
    /// </para>
    ///
    /// <para>
    /// **What today does is not an approximation.** `ThermalSimulation.AffordableStepSeconds`
    /// shortens the step when a grid cannot afford its demand, so the grid advances less simulated
    /// time per real second and its whole thermal clock runs slow — a loss with no bound on it. A
    /// cap lowers the demand instead, so the grid keeps its clock and pays a bounded error on its
    /// stiffest blocks.
    /// </para>
    ///
    /// <para>
    /// Both sides are priced in kelvin from measurements this repository already has: the lost rate
    /// through `AllowanceLab.PriceRates`, which runs the mechanism rather than scaling a single
    /// point, and the cap through the population walk of `C3`.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class CapVersusAllowanceTests
    {
        private readonly ITestOutputHelper output;


        public CapVersusAllowanceTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const int Cap = 6;

        private const double CapKelvinP99 = 0.024d;

        [Fact]

        public void OffItDoesNotReachTheStepPathAtAll()
        {
            int[] sizes = { 64000 };
            int[] allowances = { 4000000 };
            string[] worlds = { "flight" };

            List<AllowanceLab.Row> before = AllowanceLab.Run("ship", sizes, allowances, worlds,
                new[] { 0 }, AllowanceLab.DefaultFrames, null);

            List<AllowanceLab.Row> after = AllowanceLab.RunFloored("ship", sizes, allowances,
                worlds, false, AllowanceLab.DefaultFrames, null);

            Assert.Single(before);
            Assert.Single(after);

            output.WriteLine("demand {0:n4} against {1:n4}; granted {2} against {3}",
                before[0].Demand, after[0].Demand, before[0].Granted, after[0].Granted);

            Assert.Equal(before[0].Demand, after[0].Demand);
            Assert.Equal(before[0].Granted, after[0].Granted);
        }

        [Fact]

        public void OnAnOverBudgetGridKeepsItsClockByFlooringToWhatItCanAfford()
        {
            int[] sizes = { 64000 };
            int[] allowances = { 4000000 };
            string[] worlds = { "flight" };

            List<AllowanceLab.Row> shortened = AllowanceLab.RunFloored("ship", sizes, allowances,
                worlds, false, AllowanceLab.DefaultFrames, null);

            List<AllowanceLab.Row> floored = AllowanceLab.RunFloored("ship", sizes, allowances,
                worlds, true, AllowanceLab.DefaultFrames, null);

            output.WriteLine("step shortened: demand {0:n2}, granted {1}, clock {2:n1}%",
                shortened[0].Demand, shortened[0].Granted, 100d * shortened[0].Rate);
            output.WriteLine("blocks floored: demand {0:n2}, granted {1}, clock {2:n1}%",
                floored[0].Demand, floored[0].Granted, 100d * floored[0].Rate);

            Assert.True(shortened[0].Rate < 0.9d,
                "the allowance is not binding on this hull — it keeps "
                + (100d * shortened[0].Rate).ToString("n1") + "% of real time with the step "
                + "shortened — so this test is not measuring the branch it is about");

            Assert.InRange(floored[0].Demand, 1f, floored[0].Granted + 0.01f);

            Assert.True(floored[0].Rate > 0.99d,
                "the grid still lost its clock: " + (100d * floored[0].Rate).ToString("n1")
                + "% of real time, so flooring did not remove the deficit it was meant to");
        }

        [Fact]

        public void WhereTheBudgetBindsTheCapKeepsTheClockThatTheAllowanceGivesAway()
        {
            int[] sizes = { 32000, 64000 };

            int[] allowances = { 4000000, 0 };

            string[] worlds = { "flight" };

            List<AllowanceLab.Row> rows = AllowanceLab.Run("ship", sizes, allowances, worlds,
                new[] { 0, Cap }, AllowanceLab.DefaultFrames, null);

            Assert.Equal(sizes.Length * allowances.Length * 2, rows.Count);

            List<AllowanceLab.PriceRow> ladder =
                AllowanceLab.PriceRates(AllowanceLab.DefaultDeficits, null);

            output.WriteLine("{0,8} {1,10} {2,5} {3,9} {4,9} {5,10} {6,12}",
                "blocks", "allowance", "cap", "demand", "granted", "rate", "lost K");

            AllowanceLab.Row bound = null;
            AllowanceLab.Row floored = null;

            foreach (AllowanceLab.Row row in rows)
            {
                double lost = AllowanceLab.RateCost(row, ladder);

                output.WriteLine("{0,8:n0} {1,10} {2,5} {3,9:n2} {4,9} {5,9:n1}% {6,11:n3}{7}",
                    row.Blocks,
                    row.Allowance == 0 ? "off" : row.Allowance.ToString("n0"),
                    row.Cap == 0 ? "off" : row.Cap.ToString(),
                    row.Demand,
                    row.Granted < 0 ? "—" : row.Granted.ToString("n0"),
                    100d * row.Rate,
                    lost,
                    AllowanceLab.PastTheLadder(row, ladder) ? " (past the ladder)" : "");

                if (row.Allowance == 0 || row.Blocks < 40000) continue;

                if (row.Cap == 0) bound = row;
                else floored = row;
            }

            Assert.NotNull(bound);
            Assert.NotNull(floored);

            output.WriteLine("");
            output.WriteLine("on the larger hull at the shipped allowance:");
            output.WriteLine("  step shortened : clock {0:n1}% of real, which stands at {1:n3} K",
                100d * bound.Rate, AllowanceLab.RateCost(bound, ladder));
            output.WriteLine("  blocks floored : clock {0:n1}% of real, and the cap's own p99 over "
                + "the population is {1:n3} K", 100d * floored.Rate, CapKelvinP99);

            Assert.True(floored.Rate > bound.Rate,
                "the cap did not give the clock back: " + (100d * floored.Rate).ToString("n1")
                + "% against " + (100d * bound.Rate).ToString("n1") + "% with the step shortened, "
                + "so there is nothing to trade and the swap is answered no");

            Assert.True(floored.Demand < bound.Demand,
                "the cap did not lower the demand on this hull — " + floored.Demand.ToString("n2")
                + " against " + bound.Demand.ToString("n2") + " — so this rig is not measuring the "
                + "mechanism the switch is about");
        }
    }
}

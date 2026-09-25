using System.Collections.Generic;
using System.Linq;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What decided the last of the three unsourced fractions with a real figure beside them,
    /// pinned so it cannot invert quietly.
    ///
    /// <para>
    /// The oxygen generator wasted **0.6** of what it draws under a comment admitting the figure
    /// was invented, against water electrolysis sourcing 0.20–0.40. The rule for moving it was
    /// registered in balance.md, *Oxygen generator waste heat*, before
    /// <see cref="OxygenGeneratorLab"/> produced a number; the rule's first clause fired, and it
    /// fired on a finding nobody had gone looking for. **At 0.6, two of the six vanilla generators
    /// are past their own critical temperature alone in open space at their own rated draw** — a
    /// state no build can improve on, because there is nothing cooler than every face on a 2.7 K
    /// sky. 0.40 is the top of the sourced band and the highest value where all six survive both
    /// rigs.
    /// </para>
    ///
    /// <para>
    /// Figures come from <see cref="OxygenGeneratorLab"/>, which reads the shipped XML at run time,
    /// so a tuning change moves these tests rather than sliding past them.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class OxygenGeneratorWasteHeatTests
    {
        [Fact]

        public void TheShippedFractionIsInsideTheBandElectrolysisSources()
        {
            float shipped = ShippedBlocks.FunctionOf("OxygenGenerator").ConsumerWasteEnergy;

            Assert.InRange(shipped, OxygenGeneratorLab.SourcedLow, OxygenGeneratorLab.SourcedHigh);
        }

        [Fact]

        public void NoOxygenGeneratorDestroysItselfWithEveryFaceOnOpenSpace()
        {
            foreach (OxygenGeneratorLab.Row row in Shipped())
            {
                Assert.True(row.BareMarginKelvin > 0f,
                    row.Name + " at its " + row.DrawWatts.ToString("n0") + " W draw settles at "
                    + row.BareKelvin.ToString("n1") + " K bare, past its critical "
                    + row.CriticalKelvin.ToString("n1")
                    + " K. Nothing a player builds is cooler than bare.");
            }
        }

        [Fact]

        public void TheFractionThisReplacedStillCooksTwoGeneratorsBare()
        {
            List<OxygenGeneratorLab.Row> cooked = OxygenGeneratorLab.Run(0.6f)
                .Where(r => r.Load == OxygenGeneratorLab.Draw.Operational && r.BareMarginKelvin < 0f)
                .ToList();

            Assert.True(cooked.Count == 2,
                "at the 0.6 that was shipped, " + cooked.Count + " generators are past critical"
                + " bare at their rated draw, where two were: "
                + string.Join(", ", cooked.Select(r => r.Name).ToArray()));

            foreach (OxygenGeneratorLab.Row row in cooked) Assert.False(row.Large);
        }

        [Fact]

        public void SkinningASmallHeatSourceCoolsItWhereSkinningAReactorDoesNot()
        {
            foreach (OxygenGeneratorLab.Row row in Shipped()
                         .Where(r => r.Load == OxygenGeneratorLab.Draw.Operational))
            {
                Assert.True(row.SkinnedKelvin < row.BareKelvin,
                    row.Name + " at rated draw settles at " + row.SkinnedKelvin.ToString("n1")
                    + " K under one cell of armour against " + row.BareKelvin.ToString("n1")
                    + " K bare, so the shell has stopped being the larger radiator and the two"
                    + " rigs' roles have inverted back");
            }
        }

        [Fact]

        public void AGeneratorAtStandbyOrTheObservedDutyNeedsNoCooling()
        {
            foreach (OxygenGeneratorLab.Row row in Shipped()
                         .Where(r => r.Load != OxygenGeneratorLab.Draw.Operational))
            {
                Assert.True(row.BareMarginKelvin > 100f && row.SkinnedMarginKelvin > 100f,
                    row.Name + " at " + row.DrawWatts.ToString("n0") + " W is within 100 K of its"
                    + " critical " + row.CriticalKelvin.ToString("n1") + " K — bare "
                    + row.BareKelvin.ToString("n1") + " K, skinned "
                    + row.SkinnedKelvin.ToString("n1") + " K");
            }
        }


        private static List<OxygenGeneratorLab.Row> Shipped()
        {
            List<OxygenGeneratorLab.Row> rows = OxygenGeneratorLab.Shipped();

            Assert.True(rows.Count > 0, "the oxygen generator sweep produced no rows");
            return rows;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// That reactors heat at all, and that the fraction chosen for them still means what it was
    /// chosen to mean.
    ///
    /// The defect these were written against: the <c>Reactor</c> entry in Cubes.xml carried
    /// <c>ProducerWasteEnergy</c> 0 and <c>ConsumerWasteEnergy</c> 0.25. A reactor delivers power
    /// through the source component, so only the producer fraction can ever apply to it, and every
    /// reactor in the game made exactly no heat — the largest heat source a ship has, inert, while
    /// a suite of 1,179 tests stayed green. Nothing in the solver was wrong; the number it was
    /// handed was.
    ///
    /// Figures come from <see cref="ReactorLab"/>, which reads the shipped XML at run time, so a
    /// tuning change moves these tests rather than sliding past them.
    /// </summary>
    [Trait("speed", "slow")]
    public class ReactorWasteHeatTests
    {
        private static readonly string[] ProducerTypes = { "Reactor", "HydrogenEngine", "BatteryBlock" };

        [Fact]

        public void EveryPowerProducerConvertsSomeOfItsOutputToHeat()
        {
            foreach (string typeId in ProducerTypes)
            {
                ShippedBlocks.Function function = ShippedBlocks.FunctionOf(typeId);

                Assert.True(function.ProducerWasteEnergy > 0f,
                    typeId + " carries ProducerWasteEnergy " + function.ProducerWasteEnergy
                    + ", so its output makes no heat. A producer's consumer fraction never applies.");
            }
        }

        [Fact]

        public void NoReactorDestroysItselfWithEveryFaceOnOpenSpace()
        {
            foreach (ReactorLab.Row row in Shipped())
            {
                Assert.True(row.BareMarginKelvin > 0f,
                    row.Subtype + " at " + (row.LoadFraction * 100f) + "% of rating settles at "
                    + row.BareKelvin.ToString("n1") + " K bare, past its critical "
                    + row.CriticalKelvin.ToString("n1") + " K. Nothing a player builds is cooler than bare.");
            }
        }

        [Fact]

        public void BuryingALargeReactorCostsItRatherThanCookingIt()
        {

            ReactorLab.Row row = Shipped()
                .First(r => r.Subtype == "LargeBlockLargeGenerator" && r.LoadFraction == 1f);

            Assert.True(row.SkinnedKelvin > row.BareKelvin,
                "a 300 MW reactor under one cell of armour settles at " + row.SkinnedKelvin.ToString("n1")
                + " K against " + row.BareKelvin.ToString("n1")
                + " K bare, so burying it has stopped costing anything at all");

            Assert.InRange(row.SkinnedKelvin - row.BareKelvin, 20f, 150f);

            Assert.True(row.SkinnedMarginKelvin > 0f,
                "a 300 MW reactor under one cell of armour is past critical again at "
                + row.SkinnedKelvin.ToString("n1") + " K; if that is deliberate then C28 is closed"
                + " and this test is the one to rewrite");
        }

        [Fact]

        public void HowAReactorIsInstalledStillDecidesWhetherItSurvivesSomewhere()
        {

            ReactorLab.Row shipped = Shipped()
                .First(r => r.Subtype == "LargeBlockLargeGenerator" && r.LoadFraction == 1f);

            Assert.True(shipped.BareMarginKelvin > 0f && shipped.SkinnedMarginKelvin > 0f,
                "bare " + shipped.BareKelvin.ToString("n1") + " K, skinned "
                + shipped.SkinnedKelvin.ToString("n1") + " K against critical "
                + shipped.CriticalKelvin.ToString("n1") + " K: one of them is past it again, so"
                + " C28 has moved and the test above needs reading with this one");

            ReactorLab.Row doubled = ReactorLab.Run(0.02f)
                .First(r => r.Subtype == "LargeBlockLargeGenerator" && r.LoadFraction == 1f);

            Assert.True(doubled.BareMarginKelvin > 0f && doubled.SkinnedMarginKelvin < 0f,
                "at twice the shipped fraction: bare " + doubled.BareKelvin.ToString("n1")
                + " K, skinned " + doubled.SkinnedKelvin.ToString("n1")
                + " K, critical " + doubled.CriticalKelvin.ToString("n1")
                + " K — both sides of critical were expected");
        }

        [Fact]

        public void AnIdlingReactorNeedsNoCoolingHoweverItIsBuiltIn()
        {
            foreach (ReactorLab.Row row in Shipped().Where(r => r.LoadFraction == 0.1f))
            {
                Assert.True(row.SkinnedMarginKelvin > 0f,
                    row.Subtype + " idling at 10% of rating settles at " + row.SkinnedKelvin.ToString("n1")
                    + " K under armour, past its critical " + row.CriticalKelvin.ToString("n1") + " K");
            }
        }


        private static List<ReactorLab.Row> Shipped()
        {
            List<ReactorLab.Row> rows = ReactorLab.Shipped();

            Assert.True(rows.Count > 0, "the reactor sweep produced no rows");
            return rows;
        }
    }
}

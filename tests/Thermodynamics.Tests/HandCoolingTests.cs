using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Thermodynamics.Core;
using VRageMath;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What a hand tool would have to be worth**, which is the number
    /// document-of-intent.md needed before it could say whether acting on
    /// heat by hand is in scope.
    ///
    /// <para>
    /// backlog.md `B32` was a void rather than a defect: the mod ships an
    /// extinguisher that scans and does not extinguish, and nothing said whether that was the
    /// design. A position taken without this measurement would be indistinguishable from never
    /// having asked, which is why these run before that sentence was written (`E1`).
    /// </para>
    ///
    /// <para>
    /// **The bottle inventory comparison uses physical capacity.** The steady cooling/waste
    /// power ratio is independent of HeatTimeScale; finite-energy temperature changes and
    /// game-time survival windows are not. These controls keep the two bases distinct.
    /// </para>
    /// </summary>
    public class HandCoolingTests
    {
        private readonly ITestOutputHelper output;


        public HandCoolingTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static List<HandCoolingLab.Price> Priced()
        {
            return GameBlocks.IsInstalled
                ? HandCoolingLab.All() : new List<HandCoolingLab.Price>();
        }

        [Fact]

        public void ReturningABlockToAmbientCostsTensOfBottlesAtTheMedian()
        {

            List<HandCoolingLab.Price> prices = Priced();
            if (prices.Count == 0) return;


            List<float> bottles = new List<float>();
            foreach (HandCoolingLab.Price price in prices) bottles.Add(price.BottlesToReturn);
            bottles.Sort();

            float cheapest = bottles[0];
            float median = HandCoolingLab.Quantile(bottles, 0.5d);

            output.WriteLine("{0} block types cross critical on their own waste", prices.Count);
            output.WriteLine("bottles to return: cheapest {0:n1}, median {1:n1}, worst {2:n0}",
                cheapest, median, bottles[bottles.Count - 1]);

            Assert.True(cheapest > 1f,
                "the cheapest block to cool by hand takes " + cheapest.ToString("n2")
                + " bottles, so a single bottle would do it and the intent's position is wrong");

            Assert.True(median > 10f,
                "the median block takes " + median.ToString("n1") + " bottles, not tens");
        }

        [Fact]

        public void DoingItInsideTheWindowTakesTensOfBottlesAtOnce()
        {

            List<HandCoolingLab.Price> prices = Priced();
            if (prices.Count == 0) return;


            List<float> atOnce = new List<float>();

            List<float> windows = new List<float>();

            foreach (HandCoolingLab.Price price in prices)
            {
                atOnce.Add(price.BottlesAtOnce);
                if (!float.IsInfinity(price.WindowSeconds)) windows.Add(price.WindowSeconds);
            }

            atOnce.Sort();
            windows.Sort();

            float median = HandCoolingLab.Quantile(atOnce, 0.5d);

            output.WriteLine("window s: median {0:n0}, p90 {1:n0}",
                HandCoolingLab.Quantile(windows, 0.5d), HandCoolingLab.Quantile(windows, 0.9d));
            output.WriteLine("bottles at once: cheapest {0:n1}, median {1:n1}, p90 {2:n1}",
                atOnce[0], median, HandCoolingLab.Quantile(atOnce, 0.9d));

            Assert.True(atOnce[0] > 1f,
                "the easiest block in the game can be saved inside its own window by "
                + atOnce[0].ToString("n2") + " bottles, so one would nearly do");

            Assert.True(median > 10f,
                "the median block needs " + median.ToString("n1")
                + " bottles at once, which is not the order of magnitude this claim rests on");
        }

        [Fact]

        public void AlmostEveryBlockThatCooksShedsAllOfItsOwnHeatInTheBestCase()
        {

            List<HandCoolingLab.Price> prices = Priced();
            if (prices.Count == 0) return;

            int selfSufficient = 0;
            foreach (HandCoolingLab.Price price in prices)
            {
                if (price.HoldWatts <= 0f) selfSufficient++;
            }

            output.WriteLine("{0} of {1} have no surplus at all in the best case",
                selfSufficient, prices.Count);

            Assert.True(selfSufficient * 100 >= prices.Count * 90,
                selfSufficient + " of " + prices.Count + " shed everything they make, which is "
                + "under the nine in ten the finding is stated at");

            Assert.True(selfSufficient < prices.Count,
                "every block sheds everything it makes, so this test cannot tell a best case from "
                + "a vacuous one");
        }

        [Fact]

        public void PullingABlockBackFromItsRatingIsCheapEnoughForAHandTool()
        {

            List<HandCoolingLab.Price> prices = Priced();
            if (prices.Count == 0) return;

            foreach (double overshoot in new[] { 10d, 25d, 50d, 100d })
            {

                List<float> bottles = new List<float>();

                foreach (HandCoolingLab.Price price in prices)
                {
                    float rise = price.ReturnJoules <= 0f ? 0f : price.ReturnJoules;
                    if (rise <= 0f) continue;

                    float perKelvin = rise / (price.CriticalKelvin - BlockHeatIndex.AmbientKelvin);
                    bottles.Add((float)(perKelvin * overshoot) / HandCoolingLab.BottleJoules);
                }

                bottles.Sort();

                output.WriteLine("{0:n0} K back from the rating: median {1:n1} bottles, "
                    + "p90 {2:n1}, worst {3:n0}",
                    overshoot,
                    HandCoolingLab.Quantile(bottles, 0.5d),
                    HandCoolingLab.Quantile(bottles, 0.9d),
                    bottles[bottles.Count - 1]);

                if (overshoot > 50d) continue;

                Assert.True(HandCoolingLab.Quantile(bottles, 0.5d) < 10f,
                    "pulling the median block " + overshoot + " K back takes "
                    + HandCoolingLab.Quantile(bottles, 0.5d).ToString("n1")
                    + " bottles, which is not a hand tool");
            }
        }

        [Theory]
        [InlineData(1f, 0.1f)]
        [InlineData(90f, 9f)]
        [InlineData(225f, 22.5f)]
        public void FixedJoulesHaveAClockDependentTemperatureEffect(float scale, float expectedKelvin)
        {
            BlockModel model = new BlockModel { Mass = 100f };
            model.Thermal.SpecificHeat = 450f;
            ThermalNode node = new ThermalNode(new BlockInstance(model, Vector3I.Zero,
                BlockOrientation.Identity), 2.5f, 500f, scale);

            Assert.Equal(expectedKelvin, 4500f / node.ThermalMass, 4);
            Assert.Equal(45000f, node.ThermalMass * scale, 1);
        }

        [Fact]
        public void PhysicalInventoryAndWindowPowerHaveDifferentClockDependence()
        {
            HandCoolingLab.Price slow = new HandCoolingLab.Price
                { ReturnJoules = 3300000f, HoldWatts = 165000f, WindowSeconds = 20f };
            HandCoolingLab.Price fast = new HandCoolingLab.Price
                { ReturnJoules = 3300000f, HoldWatts = 165000f, WindowSeconds = 10f };

            Assert.Equal(1f, slow.BottlesToReturn);
            Assert.Equal(1f, fast.BottlesToReturn);
            Assert.Equal(2f, slow.BottlesAtOnce);
            Assert.Equal(3f, fast.BottlesAtOnce);
            Assert.True(slow.OneBottleHolds);
            Assert.True(fast.OneBottleHolds);
        }

        [Fact]

        public void TheReferenceBottleIsFiveKilogramsOfCarbonDioxide()
        {
            Assert.Equal(3300000f, HandCoolingLab.BottleJoules);
            Assert.Equal(165000f, HandCoolingLab.BottleWatts);
        }
    }
}

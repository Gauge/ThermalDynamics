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

        private static ThermalSimulation CoolingRig(float clock, float temperature = 500f)
        {
            BlockModel model = new BlockModel { Mass = 100f };
            model.Thermal.SpecificHeat = 450f;
            GridBuilder builder = GridBuilder.Large();
            builder.Place(model, Vector3I.Zero);
            return builder.BuildSimulation(new ThermalSettings
                { HeatTimeScale = clock, EnableEnvironment = false }, temperature);
        }

        [Theory]
        [InlineData(1f)]
        [InlineData(90f)]
        [InlineData(225f)]
        public void PhysicalCoolingBudgetPreservesClockIndependentInventory(float clock)
        {
            ThermalSimulation rig = CoolingRig(clock);
            ThermalNode node = rig.Solver.Nodes[0];
            double removed;
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, 4500d, 300f, out removed));
            Assert.InRange(node.Temperature, 499.9f, MathF.BitIncrement(499.9f));
            Assert.Equal((500d - node.Temperature) * 45000d, removed, 6);
            Assert.InRange(removed, 4498d, 4500d);
        }

        [Fact]
        public void CoolingClipsAtItsFloorIncludesHeldCoolantAndReportsActualRemoval()
        {
            ThermalSimulation rig = CoolingRig(90f);
            ThermalNode node = rig.Solver.Nodes[0];
            node.HeldCoolantCapacity = 45000f;
            double removed;
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, 18000000d, 400f, out removed));
            Assert.Equal(400f, node.Temperature);
            Assert.Equal(9000000d, removed);
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, 1d, 600f, out removed));
            Assert.Equal(0d, removed);
            Assert.Equal(400f, node.Temperature);
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, double.MaxValue, -100f, out removed));
            Assert.Equal(ThermalConstants.MinimumTemperature, node.Temperature);
            Assert.Equal((400d - ThermalConstants.MinimumTemperature) * 90000d, removed);
        }

        [Fact]
        public void InvalidCoolingAndForeignOrRemovedBlocksLeaveStateUntouched()
        {
            ThermalSimulation rig = CoolingRig(90f);
            ThermalNode node = rig.Solver.Nodes[0];
            double removed;
            foreach (double request in new[] { -1d, double.NaN, double.PositiveInfinity })
            {
                Assert.False(rig.Solver.TryRemovePhysicalHeat(node.Block, request, 300f, out removed));
                Assert.Equal(0d, removed);
                Assert.Equal(500f, node.Temperature);
            }
            foreach (float floor in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.False(rig.Solver.TryRemovePhysicalHeat(node.Block, 1d, floor, out removed));
            Assert.False(rig.Solver.TryRemovePhysicalHeat(null, 1d, 300f, out removed));
            ThermalSimulation foreign = CoolingRig(90f);
            Assert.False(rig.Solver.TryRemovePhysicalHeat(foreign.Solver.Nodes[0].Block, 1d, 300f, out removed));
            node.Temperature = float.NaN;
            Assert.False(rig.Solver.TryRemovePhysicalHeat(node.Block, 1d, 300f, out removed));
            node.Temperature = 500f;
            rig.RemoveBlock(node.Block);
            Assert.False(rig.Solver.TryRemovePhysicalHeat(node.Block, 1d, 300f, out removed));
            Assert.Equal(500f, node.Temperature);
        }

        [Fact]
        public void CoolingIsRejectedThroughoutASlicedSolveThenPersistsIntoTheNextSolve()
        {
            ThermalSimulation rig = CoolingRig(90f);
            ThermalNode node = rig.Solver.Nodes[0];
            EnvironmentState state = EnvironmentSolver.Solve(rig.Settings, rig.Planet, Worlds.Shadow());
            Assert.True(rig.Solver.BeginStep(0.1f, state));
            int attempts = 0;
            double removed;
            do
            {
                Assert.False(rig.Solver.TryRemovePhysicalHeat(node.Block, 45000d, 300f, out removed));
                Assert.Equal(0d, removed);
                Assert.Equal(500f, node.Temperature);
                attempts++;
            } while (!rig.Solver.AdvanceStep(1));
            Assert.True(attempts > 1);
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, 45000d, 300f, out removed));
            Assert.Equal(499f, node.Temperature);
            rig.Solver.Step(0.1f, state);
            Assert.Equal(499f, node.Temperature);
        }

        [Fact]
        public void RoundingNeverRemovesMoreThanTheAvailableEnergy()
        {
            ThermalSimulation rig = CoolingRig(1f);
            ThermalNode node = rig.Solver.Nodes[0];
            Random random = new Random(32171);
            for (int i = 0; i < 2000; i++)
            {
                float before = (float)(3d + random.NextDouble() * 100000d);
                node.Temperature = before;
                double requested = random.NextDouble() * before * 45000d;
                double removed;
                Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, requested, 3f, out removed));
                Assert.InRange(removed, 0d, requested);
                Assert.Equal(((double)before - node.Temperature) * 45000d, removed);
                float nearest = (float)Math.Max(3d, before - requested / 45000d);
                float conservative = ((double)before - nearest) * 45000d > requested
                    ? MathF.BitIncrement(nearest) : nearest;
                Assert.Equal(conservative, node.Temperature);
            }
            node.Temperature = 512f;
            double tiny;
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, double.Epsilon, 3f, out tiny));
            Assert.Equal(0d, tiny);
            Assert.Equal(512f, node.Temperature);
        }

        [Theory]
        [InlineData(4f)]
        [InlineData(512f)]
        [InlineData(65536f)]
        public void RoundingAtPowerOfTwoBoundariesUsesTheNextRepresentableTemperature(float target)
        {
            ThermalSimulation rig = CoolingRig(1f, target * 2f);
            ThermalNode node = rig.Solver.Nodes[0];
            float next = MathF.BitIncrement(target);
            double exactTarget = target + ((double)next - target) / 4d;
            double request = (node.Temperature - exactTarget) * 45000d;
            double removed;
            Assert.True(rig.Solver.TryRemovePhysicalHeat(node.Block, request, 3f, out removed));
            Assert.Equal(next, node.Temperature);
            Assert.InRange(removed, 0d, request);
        }

        [Fact]
        public void CoolingInvalidatesTheCachedHottestNodeAndRejectsInvalidCapacity()
        {
            ThermalSimulation rig = CoolingRig(90f);
            ThermalNode first = rig.Solver.Nodes[0];
            ThermalNode second = rig.AddBlock(new BlockInstance(new BlockModel(),
                new Vector3I(10, 0, 0), BlockOrientation.Identity), 490f);
            rig.RebuildAll();
            EnvironmentState state = EnvironmentSolver.Solve(rig.Settings, rig.Planet, Worlds.Shadow());
            rig.Solver.Step(0.1f, state);
            Assert.Same(first, rig.Solver.HottestNode());
            double removed;
            Assert.True(rig.Solver.TryRemovePhysicalHeat(first.Block, 900000d, 300f, out removed));
            Assert.Equal(480f, first.Temperature);
            Assert.Same(second, rig.Solver.HottestNode());
            first.Block.Model.Thermal.SpecificHeat = float.NaN;
            first.RefreshThermalMass();
            Assert.False(rig.Solver.TryRemovePhysicalHeat(first.Block, 1d, 300f, out removed));
            Assert.Equal(480f, first.Temperature);
            Assert.Equal(0d, removed);
        }

        [Fact]
        public void RepeatedCoolingPulsesConserveEnergyWithoutAllocations()
        {
            ThermalSimulation rig = CoolingRig(90f);
            ThermalNode node = rig.Solver.Nodes[0];
            double removed;
            rig.Solver.TryRemovePhysicalHeat(node.Block, 0d, 300f, out removed);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            double total = 0d;
            for (int i = 0; i < 100; i++)
            {
                rig.Solver.TryRemovePhysicalHeat(node.Block, 45000d, 300f, out removed);
                total += removed;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.Equal(0, allocated);
            Assert.Equal(400f, node.Temperature);
            Assert.Equal(4500000d, total);
        }

        [Fact]

        public void TheReferenceBottleIsFiveKilogramsOfCarbonDioxide()
        {
            Assert.Equal(3300000f, HandCoolingLab.BottleJoules);
            Assert.Equal(165000f, HandCoolingLab.BottleWatts);
        }
    }
}

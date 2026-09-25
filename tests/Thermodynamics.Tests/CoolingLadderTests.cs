using System.Collections.Generic;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What bolting a cooling block to a reactor actually buys, pinned so the answer cannot
    /// quietly invert.
    ///
    /// <para>
    /// `blocks.md` says the radiator is a block you **plumb**, not one you bolt, and `balance.md`
    /// prices a coolant sink face at six times a bolt joint. Both were argued from conductances.
    /// The ladder measures the end result instead — the largest reactor the game ships, at its
    /// plate rating, in shadow, with every block that has a plausible claim to being the best
    /// cooling in the game stacked against it — and the end result is starker than the argument:
    /// the mod's own radiator takes **two kelvin off eight hundred and ninety**, and a stack of
    /// thirty-two takes no more off than a stack of eight.
    /// </para>
    ///
    /// <para>
    /// These assertions are on directions and orders of magnitude rather than on figures, because
    /// the figures move with any definition change and the conclusions are what a reader is
    /// entitled to rely on. The lab prints the figures; this says they still mean what the pages
    /// claim they mean.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class CoolingLadderTests
    {

        private static readonly object Gate = new object();
        private static List<CoolingLadder.Row> rows;
        private static float bareKelvin;


        private static List<CoolingLadder.Row> Rows(out float bare)
        {
            lock (Gate)
            {
                if (rows == null) rows = CoolingLadder.Run(out bareKelvin);
                bare = bareKelvin;
                return rows;
            }
        }


        private static List<CoolingLadder.Row> Of(string block, out float bare)
        {

            List<CoolingLadder.Row> mine = new List<CoolingLadder.Row>();
            foreach (CoolingLadder.Row row in Rows(out bare))
            {
                if (row.Block == block) mine.Add(row);
            }
            return mine;
        }

        [Fact]

        public void TheBareReactorRunsHotEnoughForTheComparisonToMeanAnything()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> all = Rows(out bare);

            Assert.NotEmpty(all);
            Assert.True(bare > 700f,
                "the bare reactor settled at " + bare.ToString("n0")
                + " K, which is not a cooling problem, so nothing below is being measured");
        }

        [Fact]

        public void BoltingTheRadiatorToAReactorSavesAFewPercentOfIt()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> radiator = Of("Gauge_LG_Radiator", out bare);
            Assert.NotEmpty(radiator);

            float best = 0f;
            foreach (CoolingLadder.Row row in radiator)
            {
                if (row.Saved > best) best = row.Saved;
            }

            Assert.True(best > 0f, "bolting radiators to the reactor did not cool it at all");
            Assert.True(best < bare * 0.04f,
                "thirty-two bolted radiators took " + best.ToString("n1") + " K off "
                + bare.ToString("n0") + " K, which is more than a bolt joint should buy; if this is"
                + " real then blocks.md's 'plumb it, do not bolt it' needs rewriting");
        }

        [Fact]

        public void NoBoltedBlockInTheGameSolvesTheReactor()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> all = Rows(out bare);

            foreach (CoolingLadder.Row row in all)
            {
                Assert.True(row.Saved < bare * 0.10f,
                    row.Count + " x " + row.Block + " took " + row.Saved.ToString("n1")
                    + " K off " + bare.ToString("n0") + " K by being bolted on, which would make it"
                    + " a cooling solution the mod's own blocks are not");
            }
        }

        [Fact]

        public void TheBlockWithNoSurfaceAdvantageIsWorthAFractionOfOneBuiltForIt()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> armour = Of("LargeBlockArmorBlock", out bare);

            List<CoolingLadder.Row> radiator = Of("Gauge_LG_Radiator", out bare);

            Assert.NotEmpty(armour);
            Assert.NotEmpty(radiator);

            Assert.True(armour[0].Saved < radiator[0].Saved * 0.25f,
                "a plain armour block bolted to the reactor saved " + armour[0].Saved.ToString("n2")
                + " K against a radiator's " + radiator[0].Saved.ToString("n2")
                + " K, so the block built to shed heat is no longer worth building");

            Assert.True(armour[0].Saved < bare * 0.005f,
                "a plain armour block took " + armour[0].Saved.ToString("n2") + " K off "
                + bare.ToString("n0") + " K, which is a cooling solution made of hull");
        }

        [Fact]

        public void AStackOfCoolersSaturates()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> radiator = Of("Gauge_LG_Radiator", out bare);
            Assert.True(radiator.Count >= 3);

            CoolingLadder.Row first = radiator[0];
            CoolingLadder.Row last = radiator[radiator.Count - 1];

            Assert.True(first.Marginal > 0f, "the first radiator saved nothing");
            Assert.True(last.Marginal < first.Marginal * 0.05f,
                "the last rung still saved " + last.Marginal.ToString("n3")
                + " K against the first's " + first.Marginal.ToString("n3")
                + ", so the ladder has not reached the flat it exists to find");
        }

        [Fact]

        public void TheFarEndSaysWhetherHeatEverReachedIt()
        {
            if (!GameBlocks.IsInstalled) return;

            float bare;

            List<CoolingLadder.Row> radiator = Of("Gauge_LG_Radiator", out bare);
            Assert.NotEmpty(radiator);

            Assert.True(radiator[0].TopKelvin > 293.15f,
                "the block bolted straight to the reactor came back at "
                + radiator[0].TopKelvin.ToString("n1") + " K, so the joint carried nothing");

            CoolingLadder.Row last = radiator[radiator.Count - 1];
            Assert.True(last.TopKelvin < radiator[0].TopKelvin,
                "the far end of a long stack is no cooler than the near end, so the column is not"
                + " measuring a gradient");
        }
    }
}

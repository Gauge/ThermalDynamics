using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What the mod's blocks cost to build, placed inside the distribution the game itself
    /// prices** — backlog.md `B33`.
    /// </summary>
    public class BuildCostTests
    {
        private readonly ITestOutputHelper output;


        public BuildCostTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void EveryShippedBlockIsPricedInsideTheGamesOwnRange()
        {
            if (!GameBlocks.IsInstalled) return;

            int dropped;
            List<BuildCostLab.Row> vanilla = BuildCostLab.Vanilla(out dropped);
            List<BuildCostLab.Row> shipped = BuildCostLab.Shipped();

            Assert.True(vanilla.Count > 100,
                "only " + vanilla.Count + " installed blocks carry all four of mass, volume, PCU "
                + "and build time, so there is no population to place anything inside");

            Assert.True(shipped.Count > 0, "this mod ships no block with a component list");

            output.WriteLine("{0} vanilla blocks priced, {1} dropped for missing one of the four",
                vanilla.Count, dropped);
            output.WriteLine("{0} shipped blocks", shipped.Count);

            List<float> mass = BuildCostLab.Column(vanilla, r => r.KilogramsPerCubicMetre);
            List<float> pcu = BuildCostLab.Column(vanilla, r => r.PcuPerCubicMetre);
            List<float> weld = BuildCostLab.Column(vanilla, r => r.SecondsPerKilogram);
            List<float> perBlock = BuildCostLab.Column(vanilla, r => r.PcuPerBlock);

            foreach (KeyValuePair<string, List<float>> entry in new[]
                     {
                         new KeyValuePair<string, List<float>>("kg/m3", mass),
                         new KeyValuePair<string, List<float>>("PCU/m3", pcu),
                         new KeyValuePair<string, List<float>>("s/kg", weld),
                         new KeyValuePair<string, List<float>>("PCU", perBlock),
                     })
            {
                output.WriteLine("vanilla {0}: p01 {1:n3}, p50 {2:n3}, p99 {3:n3}",
                    entry.Key,
                    BuildCostLab.Quantile(entry.Value, 0.01d),
                    BuildCostLab.Quantile(entry.Value, 0.50d),
                    BuildCostLab.Quantile(entry.Value, 0.99d));
            }


            List<BuildCostLab.Row> byVolume = new List<BuildCostLab.Row>(vanilla);
            byVolume.Sort(delegate (BuildCostLab.Row a, BuildCostLab.Row b)
            {
                return a.PcuPerCubicMetre.CompareTo(b.PcuPerCubicMetre);
            });

            for (int i = 0; i < byVolume.Count && i < 6; i++)
            {
                output.WriteLine("vanilla cheapest PCU/m3: {0} at {1:n4} ({2} PCU over {3:n0} m3)",
                    byVolume[i].Subtype, byVolume[i].PcuPerCubicMetre, byVolume[i].Pcu,
                    byVolume[i].CubicMetres);
            }


            List<string> outside = new List<string>();

            foreach (BuildCostLab.Row row in shipped)
            {
                double byMass = BuildCostLab.Percentile(mass, row.KilogramsPerCubicMetre);
                double byPcu = BuildCostLab.Percentile(pcu, row.PcuPerCubicMetre);
                double byWeld = BuildCostLab.Percentile(weld, row.SecondsPerKilogram);

                output.WriteLine(
                    "{0}: {1:n0} kg, {2} PCU, {3:n0} s, {4:n1} m3 — kg/m3 p{5:n0}, "
                    + "PCU/m3 p{6:n0}, s/kg p{7:n0}, PCU p{8:n0}",
                    row.Subtype, row.Kilograms, row.Pcu, row.BuildSeconds, row.CubicMetres,
                    byMass * 100d, byPcu * 100d, byWeld * 100d,
                    BuildCostLab.Percentile(perBlock, row.PcuPerBlock) * 100d);

                Check(outside, row.Subtype, "kg/m3", byMass);
                Check(outside, row.Subtype, "s/kg", byWeld);
                Check(outside, row.Subtype, "PCU",
                    BuildCostLab.Percentile(perBlock, row.PcuPerBlock));

            }

            outside.Sort(StringComparer.Ordinal);
            Assert.True(outside.Count == 0,
                outside.Count + " shipped prices sit outside the range the game charges over its "
                + "own blocks, which is a price this mod invented rather than derived:\n  "
                + string.Join("\n  ", outside.ToArray()));
        }

        [Fact]
        [Trait("speed", "slow")]

        public void ChangingARecipeMovesTheTransientAndNotTheSteadyState()
        {
            if (!GameBlocks.IsInstalled) return;

            List<BuildCostLab.MassRung> rungs =
                BuildCostLab.MassLadder(new[] { 1f, 2f, 4f });

            if (rungs.Count == 0) return;

            foreach (BuildCostLab.MassRung rung in rungs)
            {
                output.WriteLine("x{0:n0}: {1:n0} kg of radiator, source settles {2:n2} K, "
                    + "far radiator {4:n2} K, reaches it in {3}",
                    rung.Factor, rung.Kilograms, rung.SettledKelvin,
                    float.IsInfinity(rung.SecondsToSettle)
                        ? "never" : rung.SecondsToSettle.ToString("n0") + " s",
                    rung.TopSettledKelvin);
            }

            Assert.Equal(3, rungs.Count);

            for (int i = 1; i < rungs.Count; i++)
            {
                float moved = Math.Abs(rungs[i].SettledKelvin - rungs[0].SettledKelvin);
                Assert.True(moved < 0.5f,
                    "x" + rungs[i].Factor + " settles " + moved.ToString("n2")
                    + " K from x1, so mass reaches the steady state and the two purposes of a "
                    + "component list are in conflict after all");
            }

            for (int i = 1; i < rungs.Count; i++)
            {
                Assert.True(rungs[i].SecondsToSettle > rungs[i - 1].SecondsToSettle,
                    "x" + rungs[i].Factor + " settles in " + rungs[i].SecondsToSettle
                    + " s against x" + rungs[i - 1].Factor + "'s " + rungs[i - 1].SecondsToSettle
                    + ", so mass does not reach the transient either and this test measures "
                    + "nothing");
            }
        }


        private static void Check(List<string> outside, string subtype, string ratio,
            double percentile)
        {
            if (percentile >= 0.01d && percentile <= 0.99d) return;

            outside.Add(subtype + " " + ratio + " at p" + (percentile * 100d).ToString("n0"));
        }
    }
}

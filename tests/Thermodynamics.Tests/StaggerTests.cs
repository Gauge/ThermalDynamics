using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What spreading a step across frames costs, and what staggering whole steps would cost
    /// instead** — the trade backlog.md `D14` asks to re-decide.
    ///
    /// <para>
    /// Both schedules do identical arithmetic over the same nodes in the same order; only the
    /// interleaving differs, so what separates them is locality. These hold the two halves of the
    /// answer: that the penalty is set by how many grids are competing for cache rather than by how
    /// big any of them is, and that a whole grid's step — the lump staggering would put on one
    /// frame — is small enough to say where the schedule could change.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    [Collection("alone")]
    public class StaggerTests
    {
        private readonly ITestOutputHelper output;


        public StaggerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const int NodesEach = 600;

        private const double UnreadableFloor = 0.30d;

        [Fact]

        public void SpreadingCostsMoreOnlyOnceTheFleetStopsFittingInCache()
        {
            List<StaggerLab.Row> rows = StaggerLab.Run(new[] { 4, 64 }, NodesEach, 8);
            Assert.Equal(2, rows.Count);

            foreach (StaggerLab.Row row in rows)
            {
                output.WriteLine(
                    "{0,4} grids x {1} rounds: staggered {2:n3} ms, spread {3:n3} ms, {4:n1} % more"
                    + " (noise {5:n2}/{6:n2}x)",
                    row.Grids, row.Rounds, row.StaggeredMs, row.SpreadMs,
                    (row.Penalty - 1d) * 100d, row.StaggeredSpread, row.SpreadSpread);

                Assert.True(row.StaggeredMs > 0d && row.SpreadMs > 0d);

                Assert.Equal(StaggerLab.GridStepsPerRepeat, row.Grids * row.Rounds);
            }

            double floor = System.Math.Max(
                System.Math.Max(rows[0].StaggeredSpread, rows[0].SpreadSpread),
                System.Math.Max(rows[1].StaggeredSpread, rows[1].SpreadSpread)) - 1d;

            output.WriteLine("noise floor {0:P1}; penalties {1:P1} and {2:P1}",
                floor, rows[0].Penalty - 1d, rows[1].Penalty - 1d);

            if (floor >= UnreadableFloor)
            {
                output.WriteLine("REFUSED: the repeats spread {0:P0}, which is wider than any"
                    + " locality effect this compares. Nothing is asserted about the ordering.",
                    floor);
                return;
            }

            if (rows[1].Penalty - 1d <= floor)
            {
                Assert.True(System.Math.Abs(rows[0].Penalty - 1d) <= floor + 0.05d,
                    "a small fleet paid " + rows[0].Penalty + "x for spreading, which is outside a"
                    + " noise floor of " + floor + " and should have been free");
                return;
            }

            Assert.True(rows[1].Penalty >= rows[0].Penalty,
                "a 64-grid fleet paid " + rows[1].Penalty + "x for spreading against a 4-grid"
                + " fleet's " + rows[0].Penalty + "x, so the penalty is not the fleet size");
        }

        [Fact]

        public void TheLumpIsOneGridsStepAndDoesNotGrowWithTheFleet()
        {
            List<StaggerLab.Row> rows = StaggerLab.Run(new[] { 4, 64 }, NodesEach, 8);

            output.WriteLine("lump {0:n0} work units at {1} grids, {2:n0} at {3}"
                + "  ({4:n3} ms and {5:n3} ms, which the machine decides)",
                rows[0].LumpWork, rows[0].Grids, rows[1].LumpWork, rows[1].Grids,
                rows[0].LumpMs, rows[1].LumpMs);

            Assert.True(rows[0].LumpWork > 0d, "a grid's step charged no work");

            Assert.Equal(StaggerLab.GridStepsPerRepeat, rows[0].Grids * rows[0].Rounds);
            Assert.Equal(StaggerLab.GridStepsPerRepeat, rows[1].Grids * rows[1].Rounds);

            Assert.Equal(rows[0].LumpWork, rows[1].LumpWork, 6);
        }
    }
}

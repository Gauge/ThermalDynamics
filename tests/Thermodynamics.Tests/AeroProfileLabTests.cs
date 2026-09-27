using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The cost lab's population, sample counts, work units and binary-readable rows. No timing
    /// threshold is asserted: costs must be read on a held machine, outside the suite (M4, M7).
    /// </summary>
    [Collection("alone")]
    public class AeroProfileLabTests
    {
        [Theory]
        [InlineData(AeroProfile.CoarseDirections)]
        [InlineData(AeroProfile.FineDirections)]
        public void EveryPhaseCountsTheWorkAndTheSamplesItReports(int directions)
        {
            var rows = AeroProfileLab.Measure(2, directions, 2, 2);
            Assert.Equal(4, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                Assert.Equal(8, row.Blocks);
                Assert.Equal(2, row.Repeats);
                Assert.Equal(2, row.Samples.Count);
                Assert.Equal(i == 2 ? AeroProfileLab.EvaluationsPerRepeat : 1, row.Work);
                Assert.Equal(i == 0 ? "profiles" : "evaluations", row.WorkUnit);
                Assert.Equal(StageLab.Row.Capped, row.Stop);
            }
            string csv = StageLab.Csv(rows);
            Assert.Contains("build-" + directions, csv);
            Assert.Contains("shifted-" + directions, csv);
        }

        [Fact]
        public void ACostRunCannotReportAnEmptyPopulationOrOneSample()
        {
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(0, 26));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(1, 26));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 27));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 26, 1, 1));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 26, 3, 2));
        }
    }
}

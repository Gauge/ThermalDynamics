using Aerodynamics.Core;
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
    [Trait("scope", "integration")]
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

        [Theory]
        [InlineData(26, 1)]
        [InlineData(26, 256)]
        [InlineData(98, 1)]
        [InlineData(98, 256)]
        public void SlicePeaksCountCompleteBuildsWithoutPoolingAwayTheSlowestCall(int directions, int budget)
        {
            var row = AeroProfileLab.MeasureSlices(2, directions, budget, 2, 2);
            Assert.Equal(8, row.Blocks);
            Assert.Equal(2, row.Repeats);
            Assert.Equal(2, row.Samples.Count);
            Assert.Equal(1, row.Work);
            Assert.Equal("maximum-slice", row.WorkUnit);
            Assert.Equal(StageLab.Row.Capped, row.Stop);
            Assert.Contains("budget=" + budget + ";", row.Note);
            var grid = AeroCalibrationLab.Box(2, 2, 2);
            var job = new AeroProfileBuilder(grid, directions, 0);
            int slices = 0;
            while (!job.Complete) { job.Advance(budget, 0); slices++; }
            Assert.Contains("work=" + job.Work + ";", row.Note);
            Assert.Contains("slices=" + slices + ";", row.Note);
        }

        [Theory]
        [InlineData(26)]
        [InlineData(98)]
        public void CentreSlicesReportEveryExposedFaceAndItsLargestCall(int directions)
        {
            var row = AeroProfileLab.MeasureSlices(3, directions, 7, 2, 2, true);
            Assert.Equal("centre-slice-peak-" + directions, row.Stage);
            Assert.Equal(2, row.Repeats);
            Assert.Equal(2, row.Samples.Count);
            Assert.Equal(0, row.AllocatedBytes);
            Assert.Contains("work=54; slices=8;", row.Note);
            Assert.Equal("maximum-slice", row.WorkUnit);
        }

        [Fact]
        public void ACostRunCannotReportAnEmptyPopulationOrOneSample()
        {
            Assert.Throws<ArgumentException>(() => AeroProfileLab.MeasureSlices(2, 26, 0));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.MeasureSlices(2, 26, 1, 1, 1));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.MeasureSlices(2, 27));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.MeasureSlices(1, 26));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(0, 26));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(1, 26));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 27));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 26, 1, 1));
            Assert.Throws<ArgumentException>(() => AeroProfileLab.Measure(2, 26, 3, 2));
        }
    }
}

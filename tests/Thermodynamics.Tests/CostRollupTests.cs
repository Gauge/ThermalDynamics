using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The cost table's total, and the nesting rule behind it.
    ///
    /// The report measures the same milliseconds at several depths: the session frame wraps the
    /// call that drives every grid, grid simulation sits inside it, and the solver and the mapping
    /// passes sit inside that. Adding an inner row to the outer one it lives in charges the same
    /// work twice — which is what the table did for grid simulation, reporting a field dump that
    /// cost 36.8 % of real time as costing 73.1 %.
    ///
    /// These tests exist because the arithmetic has no other guard: a report is written from a live
    /// session and read by eye, and a total that is twice the truth still looks like a plausible
    /// total.
    /// </summary>
    public class CostRollupTests
    {
        [Fact]

        public void TheTotalIsTheRootsOnly()
        {
            Assert.Equal(1000.0, Thermodynamics.CostRollup.MeasuredMilliseconds(880.0, 40.0, 60.0, 20.0));
        }

        [Fact]

        public void WorkNestedInsideTheFrameIsNotChargedTwice()
        {
            const double sessionFrame = 221719.10;
            const double gridSimulation = 218419.39;

            double total = Thermodynamics.CostRollup.MeasuredMilliseconds(sessionFrame, 19.81, 47.33, 0.0);

            Assert.True(total < sessionFrame + gridSimulation);
            Assert.Equal(221786.24, total, 2);
        }

        [Fact]

        public void AModCannotCostMoreTimeThanThereWas()
        {
            double total = Thermodynamics.CostRollup.MeasuredMilliseconds(600.0, 0.0, 0.0, 0.0);

            Assert.Equal(60.0, Thermodynamics.CostRollup.ShareOfRealTime(total, 1.0), 6);
        }

        [Fact]

        public void WhatTheChildrenDoNotClaimIsReportedRatherThanLost()
        {
            const double gridSimulation = 39540.00;
            double timed = 521.40 + 260.05 + 189.43 + 33627.24;

            double unattributed = Thermodynamics.CostRollup.Unattributed(gridSimulation, timed);

            Assert.Equal(4941.88, unattributed, 2);
            Assert.True(unattributed / gridSimulation > 0.12);
        }

        [Fact]

        public void DoubleTimedWorkShowsAsNegativeRatherThanZero()
        {
            Assert.Equal(-10.0, Thermodynamics.CostRollup.Unattributed(20.0, 30.0), 6);
        }

        [Fact]

        public void TheOneOffBuildIsARootAndReachesTheTotal()
        {
            double without = Thermodynamics.CostRollup.MeasuredMilliseconds(900.0, 0.0, 0.0, 0.0);
            double with = Thermodynamics.CostRollup.MeasuredMilliseconds(900.0, 0.0, 0.0, 179.1);

            Assert.Equal(179.1, with - without, 6);
        }

        [Fact]

        public void TheShareIsUndefinedBeforeTheClockHasRun()
        {
            Assert.True(Thermodynamics.CostRollup.ShareOfRealTime(100.0, 0.0) < 0.0);
        }
    }
}

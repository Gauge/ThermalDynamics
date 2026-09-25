using Thermodynamics.Core;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The rolling sweep's slice arithmetic.
    ///
    /// A rota is where a mod looks for changes the game raises no event for — block mass here —
    /// and its failure mode is quiet. A slice that rounds to zero stops the sweep entirely and
    /// nothing reports it: masses simply stop being noticed, blocks keep the thermal inertia they
    /// were built with, and the only symptom is temperatures that are subtly wrong on exactly the
    /// largest grids. That is worth arithmetic tests of its own.
    /// </summary>
    public class SweepSliceTests
    {
        private const int Interval = 8;
        private const int Cap = 4096;

        [Fact]

        public void NothingToSweepIsNoWork()
        {
            Assert.Equal(0, SimulationScheduler.SweepSlice(0, 1, Interval, Cap));
            Assert.Equal(0, SimulationScheduler.SweepSlice(100, 0, Interval, Cap));
        }

        [Fact]

        public void AGridUnderTheCapIsSweptWholeOverTheInterval()
        {
            const int count = 800;

            int swept = 0;
            for (int step = 0; step < Interval; step++)
            {
                swept += SimulationScheduler.SweepSlice(count, 1, Interval, Cap);
            }

            Assert.Equal(count, swept);
        }

        [Fact]

        public void AMultiStepTickOwesEveryStepsShare()
        {
            Assert.Equal(300, SimulationScheduler.SweepSlice(800, 3, Interval, Cap));
        }

        [Fact]

        public void TheCapBoundsTheSliceHoweverLargeTheGrid()
        {
            Assert.Equal(Cap, SimulationScheduler.SweepSlice(1000000, 1, Interval, Cap));
            Assert.Equal(Cap, SimulationScheduler.SweepSlice(1000000, 4, Interval, Cap));
        }

        [Fact]

        public void ALargeGridTimesSeveralStepsDoesNotOverflow()
        {
            int slice = SimulationScheduler.SweepSlice(int.MaxValue / 2, 60, Interval, Cap);
            Assert.Equal(Cap, slice);
            Assert.True(slice > 0);
        }

        [Fact]

        public void TheSliceNeverExceedsTheRota()
        {
            Assert.Equal(3, SimulationScheduler.SweepSlice(3, 60, Interval, Cap));
            Assert.Equal(1, SimulationScheduler.SweepSlice(1, 1, Interval, Cap));
        }

        [Fact]

        public void ASliceNeverRoundsDownToNothing()
        {
            Assert.Equal(1, SimulationScheduler.SweepSlice(4, 1, 64, Cap));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(800)]
        [InlineData(50000)]

        public void WalkingTheRotaCoversEveryItem(int count)
        {
            bool[] seen = new bool[count];
            int cursor = 0;

            int ticks = Interval * 2 + (2 * count / Cap) + 2;

            for (int tick = 0; tick < ticks; tick++)
            {
                int slice = SimulationScheduler.SweepSlice(count, 1, Interval, Cap);
                for (int i = 0; i < slice; i++)
                {
                    if (cursor >= count) cursor = 0;
                    seen[cursor++] = true;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Assert.True(seen[i], "item " + i + " of " + count + " was never swept");
            }
        }
    }
}

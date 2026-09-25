using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The step-walks lab prices pass fusion and the per-step mirror on redesign.md's third
    /// sweep. Its load-bearing half is its own agreement check — the two schedules must land on
    /// the same bits for every temperature, every watts entry and the accumulator before either
    /// is timed, and it throws if they do not — so what is pinned here is the precondition that
    /// makes that check mean something: the fixture holds both buried and exposed nodes, both
    /// schedules get a clock, and the mirror is measured against a step taken on the same grid
    /// in the same window (`E8`).
    /// </summary>
    public class StepWalksLabTests
    {
        [Fact]

        public void BothSchedulesAgreeGetTimedAndTheMirrorIsMeasuredAgainstAStep()
        {
            StepWalksLab.Result result = StepWalksLab.Run("ship", 4000, 3);

            Assert.Equal(2, result.Fusion.Count);
            foreach (StepWalksLab.Row row in result.Fusion)
            {
                Assert.True(row.Nodes > 1000, row.Walk + " judged only " + row.Nodes + " nodes (`E8`)");
                Assert.True(row.BestMs > 0, row.Walk + " was never timed");
            }

            Assert.True(result.MirrorMs > 0, "the mirror was never timed");
            Assert.True(result.SettledStepMs > result.MirrorMs,

                "a settled step (" + result.SettledStepMs + " ms) reads cheaper than the mirror it contains ("
                + result.MirrorMs + " ms), so one of the two clocks is wrong");

        }
    }
}

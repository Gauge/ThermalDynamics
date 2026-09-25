using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Stopping a run when it has nowhere left to go, rather than when it last moved slowly.**
    ///
    /// <para>
    /// The settle rule asks how far the hottest block moved over the last sixty seconds and stops
    /// when that is under 0.25 K. On three of the air walk's four scenarios a hull meets that in
    /// two minutes. On `vacuum-shadow` it never does: a hull radiating into the dark decays towards
    /// its floor, so its per-chunk movement shrinks geometrically and creeps under a fixed
    /// threshold only after most of an hour. Measured on the 2026-08-28 air walk, **every ship**
    /// runs its full 1,800 s there and the scenario is **33.7 %** of the walk's cost.
    /// </para>
    ///
    /// <para>
    /// <see cref="Battery.Converged"/> asks the other question. If a chunk moved the hull `d` and
    /// the one before moved it `p`, then `r = d / p` is the decay per chunk and what is left is the
    /// rest of that geometric series, `d * r / (1 - r)`. Inside the same tolerance, the run is
    /// already where it is going. **This is a cheaper route to the same reading, not a looser
    /// reading**, which is why every test here is about the bound rather than about the saving.
    /// </para>
    ///
    /// <para>
    /// Every case below that returns false is a refusal to extrapolate something that is not
    /// decaying, because an extrapolation off a curve that is not a decay is a guess with a
    /// number attached (`E8`).
    /// </para>
    /// </summary>
    public class SettleExtrapolationTests
    {
        [Fact]

        public void AHalvingApproachInsideTheToleranceIsDone()
        {
            Assert.True(Battery.Converged(-0.2f, -0.4f));
        }

        [Fact]

        public void AHalvingApproachWithFurtherToGoIsNotDone()
        {
            Assert.False(Battery.Converged(-2f, -4f));
        }

        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.25f)]
        [InlineData(0.5f)]
        [InlineData(0.75f)]
        [InlineData(0.89f)]

        public void WhatIsSkippedIsInsideTheSameTolerance(float ratio)
        {
            for (float step = 0.01f; step < 50f; step *= 1.3f)
            {
                float previous = step / ratio;
                if (!Battery.Converged(step, previous)) continue;

                float remaining = step * ratio / (1f - ratio);
                Assert.True(remaining <= Battery.SettleWithin,
                    "accepted a stop with " + remaining + " K still to go");
            }
        }

        [Fact]

        public void ACreepIsNotAConvergence()
        {
            Assert.False(Battery.Converged(0.99f, 1.0f));
            Assert.False(Battery.Converged(0.0999f, 0.1f));
        }

        [Fact]

        public void AnAcceleratingRunIsNotExtrapolated()
        {
            Assert.False(Battery.Converged(-0.4f, -0.2f));
            Assert.False(Battery.Converged(4f, 2f));
        }

        [Fact]

        public void AnOscillationIsNotExtrapolated()
        {
            Assert.False(Battery.Converged(0.1f, -0.2f));
            Assert.False(Battery.Converged(-0.1f, 0.2f));
        }

        [Fact]

        public void NothingIsExtrapolatedFromOneSample()
        {
            Assert.False(Battery.Converged(-0.1f, float.NaN));
            Assert.False(Battery.Converged(-0.1f, 0f));
            Assert.False(Battery.Converged(0f, -0.1f));
        }

        [Fact]

        public void ItIsOffUnlessTheWalkAsksForIt()
        {
            Assert.Equal(
                (System.Environment.GetEnvironmentVariable("THERMAL_SETTLE_EXTRAPOLATE") ?? "") == "1",
                Battery.Extrapolates);
        }
    }
}

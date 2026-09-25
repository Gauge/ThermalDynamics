using System;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Whether the realism sweep's one unexplained divergence is a divergence at all**, which is
    /// what backlog.md `C8` leaves open.
    ///
    /// <para>
    /// Scenario run lengths are cut for the shipped world and thermal time runs at `HeatTimeScale`,
    /// so the `physical` column — at a clock 225× slower — is a column of runs stopped while they
    /// were still climbing. `physical / x-overloaded` reaches 11,662 K with every substep granted,
    /// and the sweep cannot tell that from a run that ended too early: it is `M1` in practice, two
    /// runs stopped on different physical states.
    /// </para>
    ///
    /// <para>
    /// **Scaling the whole sweep costs the same 225×, so this asks the question of one cell.**
    /// `ScenarioRunner.DurationScale` is the mechanism and `ProfileSweep.MeasureAtItsOwnClock` sets
    /// it from the profile's own clock, which is what makes the comparison one of equals.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    [Collection("alone")]
    public class ProfileClockTests
    {
        private readonly ITestOutputHelper output;


        public ProfileClockTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static BalanceProfile Profile(string name)
        {
            foreach (BalanceProfile profile in BalanceProfile.All())
            {
                if (profile.Name == name) return profile;
            }

            throw new InvalidOperationException("no profile named " + name);
        }

        [Fact]

        public void TheDurationScaleReachesTheRunAndIsTheRatioOfTheClocks()
        {
            Assert.Equal(1f, ScenarioRunner.EffectiveDurationScale, 3);

            ScenarioRunner.DurationScale = 4f;
            try
            {
                Assert.Equal(4f, ScenarioRunner.EffectiveDurationScale, 3);

                ScenarioRunner runner = Scenarios.Run("vacuum-soak").Runner;
                float scaled = runner.ElapsedSeconds;

                ScenarioRunner.DurationScale = 1f;
                float plain = Scenarios.Run("vacuum-soak").Runner.ElapsedSeconds;

                output.WriteLine("{0:n0} s against {1:n0} s", scaled, plain);
                Assert.Equal(4f, scaled / plain, 2);
            }
            finally
            {
                ScenarioRunner.DurationScale = 1f;
            }
        }

        [Fact]

        public void ThePhysicalProfilesOverloadedRigSettlesOnceItIsGivenItsOwnClock()
        {

            BalanceProfile physical = Profile("physical");
            Assert.True(physical.HeatTimeScale < 2f,
                "the physical profile is supposed to run the real clock, and runs at "
                + physical.HeatTimeScale);

            ProfileSweep.Cell cell =
                ProfileSweep.MeasureAtItsOwnClock(physical, "x-overloaded", true);

            output.WriteLine("peak {0:n0} K, converged {1}, starved {2:P0}, {3:n0} steps",
                cell.PeakKelvin, cell.Converged, cell.StarvedShare, cell.Steps);

            Assert.False(cell.Failed, "the cell failed to run: " + cell.Error);

            Assert.True(cell.Steps > 1000000L,
                "only " + cell.Steps + " steps, so the clock scaling did not reach the rig");

            Assert.False(cell.Diverged,
                "x-overloaded reached " + cell.PeakKelvin + " K at its own clock, so the divergence"
                + " survives the run length and is a real one after all");

            Assert.True(cell.Converged,
                "the run has to reach a steady state for the reading to mean anything, and it"
                + " reports " + cell.PeakKelvin + " K still moving");

            Assert.InRange(cell.PeakKelvin, 500f, 3000f);

            Assert.True(cell.StarvedShare < 0.01f,
                "the rig was starved " + cell.StarvedShare.ToString("P0") + ", which is a different"
                + " explanation from the one this measures");
        }
    }
}

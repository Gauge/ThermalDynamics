using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What it costs when `MaxSubsteps` refuses the demand**, which is the question
    /// backlog.md `C19` answered by assertion.
    ///
    /// <para>
    /// The shipped configuration's p99 substep demand in thick air at 200 m/s is 73.4 against the
    /// 64 the ceiling grants, and that was read as *those hulls are simulated wrong rather than
    /// slowly*. Nothing had measured it. The mechanism named in support of it — the solver flooring
    /// a block's heat capacity — belongs to `MaxSubstepsPerBlock`, which ships at zero and does not
    /// run; what actually happens is that the step is integrated at the ceiling and the two
    /// overshoot clamps bound every exchange it makes.
    /// </para>
    ///
    /// <para>
    /// **The quantity is the over-subscription, not the substep count**, and that is what makes a
    /// rig able to answer for a population it is not a member of: 72 demanded of 62 and 36 demanded
    /// of 31 are the same approximation, and these tests assert they cost the same. The ladder and
    /// the figures are in stiffness.md.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class SubstepCeilingTests
    {
        private const int Blocks = 1200;

        private const int Steps = 120;


        private static List<LoadBenchmarks.CeilingRow> Ladder(int frequency, int steps = Steps)
        {
            return LoadBenchmarks.SubstepCeiling("ship", Blocks, steps, null,
                null, false, frequency);
        }


        private static LoadBenchmarks.CeilingRow Nearest(
            IList<LoadBenchmarks.CeilingRow> rows, float oversubscription)
        {
            LoadBenchmarks.CeilingRow best = null;
            float bestGap = float.MaxValue;

            for (int i = 1; i < rows.Count; i++)
            {
                float gap = System.Math.Abs(rows[i].Oversubscription - oversubscription);
                if (gap >= bestGap) continue;
                bestGap = gap;
                best = rows[i];
            }

            Assert.NotNull(best);
            return best;
        }

        [Fact]

        public void TheSweepRefusesADemandItFirstMeasured()
        {

            List<LoadBenchmarks.CeilingRow> rows = Ladder(4);
            Assert.True(rows.Count >= 4, "the ladder produced " + rows.Count + " rows");

            LoadBenchmarks.CeilingRow granted = rows[0];
            Assert.False(granted.Bound, "the reference run was itself capped");
            Assert.True(granted.RequiredSubsteps > 8f,
                "the hull demands " + granted.RequiredSubsteps + " substeps in air, which is too"
                + " few for a ceiling sweep to mean anything");

            for (int i = 1; i < rows.Count; i++)
            {
                Assert.True(rows[i].Bound, "ceiling " + rows[i].Ceiling + " did not bind");
                Assert.Equal(rows[i].Ceiling, (int)System.Math.Round(rows[i].MeanGranted));
            }
        }

        [Fact]

        public void RefusingTheShippedBreachMovesNothingThatIsRead()
        {

            LoadBenchmarks.CeilingRow row = Nearest(Ladder(4), 1.15f);

            Assert.InRange(row.Oversubscription, 1.05f, 1.30f);
            Assert.True(row.MaxError < 0.5f,
                "the worst block moved " + row.MaxError + " K at " + row.Oversubscription + "x");
            Assert.True(System.Math.Abs(row.PeakError) < 0.1f,
                "the hottest block moved " + row.PeakError + " K at " + row.Oversubscription + "x");
        }

        [Fact]

        public void RefusingNineTimesTheDemandBreaksIt()
        {

            LoadBenchmarks.CeilingRow row = Nearest(Ladder(4), 9f);

            Assert.InRange(row.Oversubscription, 7f, 11f);
            Assert.True(row.MaxError > 10f,
                "nine times over-subscribed moved the worst block only " + row.MaxError + " K,"
                + " so this suite is no longer measuring the thing it says it is");
        }

        [Fact]

        public void TheRingsLadderIsAnApproximationRatherThanACliff()
        {
            List<LoadBenchmarks.CeilingRow> rows = LoadBenchmarks.SubstepCeiling(
                "ship", 20, 300, null, null, false, 1, 0f, 1f,
                LoadBenchmarks.CeilingFixtures.Rings, 64f);

            LoadBenchmarks.CeilingRow granted = rows[0];

            Assert.False(granted.Bound, "the reference run was itself capped");
            Assert.True(granted.CoolantLoops > 0,

                "the fixture built no ring, so this is a ladder about blocks (`E8`)");
            Assert.True(granted.RequiredSubsteps > 8f,
                "the fixture demands " + granted.RequiredSubsteps + " substeps, too few for a"
                + " ceiling sweep to reach the band this is about");

            for (int i = 1; i < rows.Count; i++)
            {
                LoadBenchmarks.CeilingRow row = rows[i];

                Assert.True(row.PeakTemperature < granted.PeakTemperature * 10f,
                    "at " + row.Oversubscription + "x the hottest block reached "
                    + row.PeakTemperature + " K against " + granted.PeakTemperature
                    + " K granted in full, which is a divergence rather than an approximation");

                Assert.True(row.PeakCoupledTemperature < granted.PeakTemperature * 10f,
                    "at " + row.Oversubscription + "x the hottest parcel reached "
                    + row.PeakCoupledTemperature + " K against a hull granted its demand at "
                    + granted.PeakTemperature + " K");
            }
        }

        [Fact]

        public void TheErrorFollowsTheOversubscriptionRatherThanTheSubstepCount()
        {

            List<LoadBenchmarks.CeilingRow> quarter = Ladder(4, Steps);

            List<LoadBenchmarks.CeilingRow> half = Ladder(2, Steps / 2);

            Assert.True(half[0].RequiredSubsteps > quarter[0].RequiredSubsteps * 1.8f,
                "halving the step rate should double the demand; it went from "
                + quarter[0].RequiredSubsteps + " to " + half[0].RequiredSubsteps);

            int judged = 0;
            foreach (float ratio in LoadBenchmarks.Oversubscriptions)
            {

                LoadBenchmarks.CeilingRow a = Nearest(quarter, ratio);

                LoadBenchmarks.CeilingRow b = Nearest(half, ratio);

                if (a.MaxError < 0.5f && b.MaxError < 0.5f) continue;

                judged++;
                float larger = System.Math.Max(a.MaxError, b.MaxError);
                float smaller = System.Math.Min(a.MaxError, b.MaxError);

                Assert.True(larger <= smaller * 1.35f,
                    "at " + ratio + "x the two step lengths disagree: " + a.MaxError + " K at "
                    + a.Ceiling + " substeps against " + b.MaxError + " K at " + b.Ceiling);
            }

            Assert.True(judged > 0,

                "no rung of the ladder produced an error worth comparing, so this judged nothing (`E8`)");
        }
    }
}

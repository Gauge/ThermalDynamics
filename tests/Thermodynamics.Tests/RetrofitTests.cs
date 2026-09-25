using System.Collections.Generic;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Whether cooling fitted to a ship somebody built does anything — criterion G3, answered on
    /// real hulls rather than on a rig.
    ///
    /// <para>
    /// `CoolingLadder` measured one reactor with blocks stacked against it. That is a measurement
    /// of blocks. `RetrofitLab` parses a workshop blueprint, runs it under load to find where its
    /// heat actually is, and puts the mod's own blocks in the cells that hull left free — so the
    /// constraint a player is under is part of the answer.
    /// </para>
    ///
    /// <para>
    /// The conclusions are pinned here rather than the figures. The corpus run takes seven minutes
    /// and needs a corpus; these cases are the claims that run without one, plus a re-measurement
    /// on a small stride when `THERMAL_CORPUS_TESTS` is set.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class RetrofitTests
    {
        [Fact]

        public void BoltingFitsEverywhereAndPlumbingHardlyFitsAtAll()
        {
            Assert.True(RetrofitLab.Corpus.BoltedFitted > RetrofitLab.Corpus.Warm * 0.7f,
                "radiators could be bolted to only " + RetrofitLab.Corpus.BoltedFitted
                + " of " + RetrofitLab.Corpus.Warm + " warm ships, so the bolted column is no"
                + " longer the case where installation is easy");

            Assert.True(RetrofitLab.Corpus.PlumbedFitted < RetrofitLab.Corpus.Warm * 0.3f,
                "a ring with a sink face now fits on " + RetrofitLab.Corpus.PlumbedFitted
                + " of " + RetrofitLab.Corpus.Warm + " warm ships; if that has risen, the thing"
                + " this criterion is blocked on has changed and G3 should be re-read");
        }

        [Fact]

        public void BoltingRadiatorsToARealShipHurtsMoreOftenThanItHelps()
        {
            Assert.True(RetrofitLab.Corpus.BoltedHurt > RetrofitLab.Corpus.BoltedHelped,
                "bolted radiators now help " + RetrofitLab.Corpus.BoltedHelped
                + " ships and hurt " + RetrofitLab.Corpus.BoltedHurt
                + "; if bolting has become a net gain then blocks.md's 'plumb it, do not bolt it'"
                + " needs rewriting and so does this test");

            Assert.True(RetrofitLab.Corpus.BoltedMedianPercent < 1f,
                "the median bolted fit now takes " + RetrofitLab.Corpus.BoltedMedianPercent
                + " % off a ship's peak, which would make it a lever a player has");
        }

        [Fact]

        public void PlumbingWorksOnTheShipsItCanBeFittedTo()
        {
            Assert.True(RetrofitLab.Corpus.PlumbedHelped > RetrofitLab.Corpus.PlumbedHurt * 3,
                "a plumbed fit now helps " + RetrofitLab.Corpus.PlumbedHelped + " and hurts "
                + RetrofitLab.Corpus.PlumbedHurt + " of the ships it fits on");

            Assert.True(RetrofitLab.Corpus.PlumbedMedianPercent
                        > RetrofitLab.Corpus.BoltedMedianPercent + 1f,
                "plumbing and bolting now buy the same thing, which the conductances say they"
                + " cannot: a sink face carries 1,000 W/K against a bolt joint's 167");

            Assert.True(RetrofitLab.Corpus.PlumbedP90Percent > 5f,
                "the ninetieth percentile of a plumbed fit is " + RetrofitLab.Corpus.PlumbedP90Percent
                + " %, which is no longer a mechanic worth building");
        }

    }
}

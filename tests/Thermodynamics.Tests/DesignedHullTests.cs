using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The mod's design thesis, which had never been measured.**
    ///
    /// <para>
    /// `C40` bounded every path that moves heat *inside* a grid at about 9 % of the peak, and closed
    /// the transport line for retrofits with it. What it could not settle is the claim
    /// document-of-intent.md actually makes — *cooling designed
    /// in* — because every rig this repository owned had an **exposed** source. A panel bolted to an
    /// exposed source radiates wherever it is put, so no rig could see the difference between
    /// reaching the sky and not reaching it.
    /// </para>
    ///
    /// <para>
    /// <see cref="DesignedHullLab"/> buries the source in solid armour, which is how one is
    /// installed, and the difference appears: a bolted panel is buried too.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class DesignedHullTests
    {
        private readonly ITestOutputHelper output;


        public DesignedHullTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        private static DesignedHullLab.Row Find(List<DesignedHullLab.Row> rows, string fit)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Fit == fit) return rows[i];
            }

            Assert.Fail( "no row for " + fit);
            return null;
        }

        [Fact]

        public void PlumbingToTheSkinBeatsBoltingIntoTheHull()
        {
            List<DesignedHullLab.Row> rows = DesignedHullLab.Run(200000f, 4);


            DesignedHullLab.Row bare = Find(rows, "buried, bare");

            DesignedHullLab.Row bolted = Find(rows, "bolted, buried");

            DesignedHullLab.Row plumbed = Find(rows, "plumbed, to the skin");

            foreach (DesignedHullLab.Row row in rows)
            {
                output.WriteLine("{0,-22} {1,8:n1} K  saved {2,7:n1} K  panels {3}  on the skin {4}",
                    row.Fit, row.SourceKelvin, row.SavedKelvin, row.Radiators, row.RadiatorsOnTheSkin);
            }

            Assert.True(bare.SourceKelvin > 340f,
                "the buried source settled at " + bare.SourceKelvin.ToString("n1")
                + " K, which is not a cooling problem, so nothing below is being measured");

            Assert.Equal(bolted.Radiators, plumbed.Radiators);
            Assert.True(bolted.Radiators > 0,
                "no panels were fitted in either arm, so this test would pass on a lab that measured"
                + " nothing — which is exactly how the first run of it behaved");

            Assert.Equal(0, bolted.RadiatorsOnTheSkin);
            Assert.True(plumbed.RadiatorsOnTheSkin > 0,
                "the plumbed arm put no panel on the skin, so it is not testing the claim");

            Assert.True(plumbed.SavedKelvin > bolted.SavedKelvin,
                "plumbing to the skin saved " + plumbed.SavedKelvin.ToString("n1")
                + " K against bolting's " + bolted.SavedKelvin.ToString("n1")
                + " K on the same hull with the same panels; if this is real then the mod's design"
                + " thesis is wrong and document-of-intent.md needs rewriting");
        }

        [Fact]

        public void BoltingAPanelIntoASolidHullIsWorseThanFittingNothing()
        {
            List<DesignedHullLab.Row> rows = DesignedHullLab.Run(200000f, 4);

            DesignedHullLab.Row bolted = Find(rows, "bolted, buried");

            Assert.True(bolted.Radiators > 0, "no panels were fitted, so nothing is being measured");
            Assert.Equal(0, bolted.RadiatorsOnTheSkin);

            Assert.True(bolted.SavedKelvin < 0f,
                "a buried panel took " + bolted.SavedKelvin.ToString("n1")
                + " K off a buried source; a block that cannot radiate and displaces armour that"
                + " conducts was expected to cost rather than pay");
        }

        [Fact]

        public void TheShippedPickupIsNotStarvedOnABlockThisLarge()
        {

            List<DesignedHullLab.Row> rows = new List<DesignedHullLab.Row>();
            float bare = 0f;

            for (int panels = 1; panels <= 4; panels++)
            {
                List<DesignedHullLab.Row> run = DesignedHullLab.Run(6400000f, panels);

                DesignedHullLab.Row plumbed = Find(run, "plumbed, to the skin");

                if (bare == 0f) bare = Find(run, "buried, bare").SourceKelvin;
                rows.Add(plumbed);

                output.WriteLine("{0} panel(s): {1,8:n1} K   sink {2,8:n0} W/K   needs {3,7:n0} K",
                    panels, plumbed.SourceKelvin, plumbed.SinkWattsPerKelvin,
                    plumbed.SinkWattsPerKelvin > 0f ? 6400000f / plumbed.SinkWattsPerKelvin : 0f);
            }

            Assert.True(rows[0].SinkWattsPerKelvin > 0f,
                "the ring had no sink onto the source, so nothing here is being measured");

            float forced = 6400000f / rows[0].SinkWattsPerKelvin;

            Assert.True(forced < 2000f,
                "the shipped pickup forces " + forced.ToString("n0")
                + " K on 6.4 MW through one sink face; above the block's own rating this is a"
                + " block no radiator count can cool, which is what C42 moved the coefficient to"
                + " prevent");

            float first = bare - rows[0].SourceKelvin;
            float rest = rows[0].SourceKelvin - rows[rows.Count - 1].SourceKelvin;

            Assert.True(rest > 0f,
                "panels beyond the first took " + rest.ToString("n1") + " K between them, so the"
                + " ladder is still saturating and something upstream of the panels binds");

            output.WriteLine("first panel {0:n1} K, the rest {1:n1} K, forced gradient {2:n0} K",
                first, rest, forced);
        }
    }
}

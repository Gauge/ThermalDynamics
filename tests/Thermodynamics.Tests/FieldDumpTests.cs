using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The auditor against a real field dump rather than invented rows.
    ///
    /// <para>Separate from <see cref="DumpAuditTests"/> on purpose. Those tests build the smallest
    /// CSV that could break one check and assert that it breaks; nothing about them changes when a
    /// new dump arrives. These read a dump a world produced, and every number in them is a fact
    /// about that dump — so refreshing the fixture changes this file and only this file.</para>
    ///
    /// <para>Synthetic rows prove a check can fire. They cannot prove it holds against numbers a
    /// planet produced, which is the only place the model and the world meet — the same reason
    /// `Census` keeps a real ship's block population rather than a plausible one.</para>
    ///
    /// <para>The fixture is the 2026-08-21 ToastyBugs dump — five planets, a grid buried 99 m
    /// into Mars, a Hailstorm, and the first session with per-stage after-step timing: 597 climate
    /// rows thinned one in twenty and then completed with the minority rows a uniform sample would
    /// drop (every row under weather or over the engine's ceiling; one in ten of the buried,
    /// airless and slope-wind rows), beside all 304 of its grids and 720 block types. Refresh it
    /// from `-- dump`.</para>
    /// </summary>
    public class FieldDumpTests
    {

        private static string Fixture()
        {
            string folder = Path.Combine(
                ShippedBlocks.RepoRoot(), "tests", "benchmarks", "field-dump");

            string path = DumpAudit.Newest(folder);
            Assert.True(path != null, "field dump fixture missing under " + folder);
            return path;
        }


        private static DumpAudit.CheckResult Check(DumpAudit.Result result, string name)
        {
            foreach (DumpAudit.CheckResult check in result.Checks)
            {
                if (check.Name.StartsWith(name, System.StringComparison.Ordinal)) return check;
            }

            throw new Xunit.Sdk.XunitException("no check named " + name);
        }

        [Fact]

        public void TheComfortColumnIsReadableUnderEitherName()
        {

            string path = Fixture();
            string[] lines = File.ReadAllLines(path);
            Assert.True(lines.Length > 1, "the fixture has no rows");


            List<string> header = new List<string>(lines[0].Split(','));
            int column = header.IndexOf("game_comfort");
            if (column < 0) column = header.IndexOf("game_temperature");

            Assert.True(column >= 0,
                "neither the current comfort column nor the name it retired is in the fixture");

            int rows = 0;
            int zero = 0;
            float highest = 0f;

            for (int i = 1; i < lines.Length; i++)
            {
                string[] cells = lines[i].Split(',');
                if (cells.Length <= column) continue;

                float value;
                if (!float.TryParse(cells[column], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out value)) continue;

                rows++;
                if (value == 0f) zero++;
                if (value > highest) highest = value;
            }

            Assert.True(rows > 500, "only " + rows + " rows were read");
            Assert.True(highest > 0f && highest <= 1f,
                "a comfort fraction should sit inside 0..1 and not be flat: " + highest);
            Assert.True(zero > rows / 2,
                "most of the fixture should be zero, because most of it is airless: "
                + zero + " of " + rows);
        }

        [Fact]

        public void EveryCheckReachesTheFixture()
        {
            DumpAudit.Result result = DumpAudit.Run(Fixture());

            foreach (DumpAudit.CheckResult check in result.Checks)
            {
                Assert.False(check.Skipped, check.Name + " skipped: the fixture lost a column");
            }

            Assert.Equal(597, result.Rows);
            Assert.Equal(304, result.GridRows);
            Assert.Equal(720, result.BlockTypeRows);
        }

        [Fact]

        public void TheFixtureStillCarriesTheCensusTearItCaught()
        {
            DumpAudit.Result result = DumpAudit.Run(Fixture());


            DumpAudit.CheckResult census = Check(result, "live blocks");
            Assert.Equal(1, census.Hits);
            Assert.Contains("SmallBlockArmorBlock", census.Worst);

            foreach (DumpAudit.CheckResult check in result.Checks)
            {
                if (check == census) continue;
                Assert.False(check.Failed, check.Name + " failed: " + check.Worst);
            }
        }

        [Fact]

        public void TheFixtureCarriesTheOpenObservations()
        {
            DumpAudit.Result result = DumpAudit.Run(Fixture());


            DumpAudit.CheckResult ceiling = Check(result, "wind stays under");
            Assert.Equal(23, ceiling.Hits);
            Assert.Contains("103.94", ceiling.Worst);


            DumpAudit.CheckResult slope = Check(result, "slope wind");
            Assert.Equal(112, slope.Hits);


            DumpAudit.CheckResult decomposes = Check(result, "wind decomposes");
            Assert.True(decomposes.Observation);
            Assert.Equal(20, decomposes.Hits);
        }
    }
}

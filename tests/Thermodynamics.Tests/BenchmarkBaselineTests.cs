using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The committed baseline is the tree a change is measured against, and it rots without
    /// announcing that it has.
    ///
    /// <para>
    /// A comparison joins on <c>section / case / metric</c>. Rename a profile, add a section, or
    /// change what a metric means, and the join stops matching: the affected rows leave the
    /// regression list and appear under "new" and "gone", where they read as bookkeeping rather
    /// than as a baseline that can no longer answer the question. The baseline in this repository
    /// had drifted exactly that far — two profiles renamed and every substep figure rescaled by an
    /// earlier units change — and produced seventy false moves against an unmodified tree.
    /// </para>
    ///
    /// <para>
    /// This does not check the figures. Timings belong to the machine that took them and comparing
    /// them across machines is what the calibration row is for. It checks the <em>keys</em>, which
    /// belong to the code, so a change that makes the baseline unusable fails here instead of six
    /// weeks later in the middle of a measurement.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class BenchmarkBaselineTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static HashSet<string> Keys(IEnumerable<ReportRow> rows)
        {

            HashSet<string> keys = new HashSet<string>();
            foreach (ReportRow row in rows) keys.Add(row.Key);
            return keys;
        }

        [Fact]

        public void TheCommittedBaselineCarriesTheKeysTheReportStillProduces()
        {
            string path = Path.Combine(RepoRoot(), "tests", "benchmarks", "performance.csv");
            Assert.True(File.Exists(path), "no committed baseline at " + path);

            PerformanceReport.Repeats = 1;
            List<ReportRow> fresh = PerformanceReport.Run("ship", 600, 2, new int[] { 600 });


            HashSet<string> committed = Keys(PerformanceReport.ParseCsv(File.ReadAllText(path)));

            HashSet<string> current = Keys(fresh);


            List<string> missing = new List<string>();
            foreach (string key in current)
            {
                if (!key.StartsWith("ladder/") && !committed.Contains(key)) missing.Add(key);
            }


            List<string> orphaned = new List<string>();
            foreach (string key in committed)
            {
                if (!key.StartsWith("ladder/") && !current.Contains(key)) orphaned.Add(key);
            }

            missing.Sort();
            orphaned.Sort();

            Assert.True(missing.Count == 0 && orphaned.Count == 0,
                "tests/benchmarks/performance.csv no longer matches the report. Re-record it with\n"
                + "  dotnet run --project Thermodynamics.Sim -- bench report --size 32000 --max 125000 --csv benchmarks\n"
                + "figures the report now produces and the baseline lacks:\n  "
                + string.Join("\n  ", missing.ToArray())
                + "\nfigures the baseline carries and the report no longer produces:\n  "
                + string.Join("\n  ", orphaned.ToArray()));
        }
    }
}

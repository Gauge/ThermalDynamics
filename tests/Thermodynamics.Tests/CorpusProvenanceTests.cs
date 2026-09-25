using System;
using System.IO;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **A dataset records the build it was collected on**, because the alternative was found out
    /// the expensive way: the 2026-08-24 air walk finished one minute after a commit that moved
    /// twenty-seven waste fractions, and establishing that took comparing its rows against a later
    /// walk and then reading the git log for the window between them.
    /// </summary>
    public class CorpusProvenanceTests
    {
        [Fact]

        public void AWalkWritesWhatBuildItRanOn()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "thermal-provenance-" + Guid.NewGuid().ToString("n"));

            Directory.CreateDirectory(directory);
            string previous = Environment.GetEnvironmentVariable("THERMAL_CORPUS_DATA");

            try
            {
                Environment.SetEnvironmentVariable("THERMAL_CORPUS_DATA", directory);
                CorpusRecord.Provenance("a-walk-that-does-not-exist");

                string path = Path.Combine(directory, "provenance.txt");
                Assert.True(File.Exists(path), "no provenance written to " + path);

                string text = File.ReadAllText(path);
                Assert.Contains("walk a-walk-that-does-not-exist", text);
                Assert.Contains("commit ", text);
                Assert.Contains("Cubes.xml ", text);

                foreach (string line in text.Split('\n'))
                {
                    if (!line.StartsWith("commit ", StringComparison.Ordinal)) continue;

                    string hash = line.Substring("commit ".Length).Trim();
                    Assert.True(hash.Length == 40,
                        "the commit reads '" + hash + "', which is not a hash");
                }

                Assert.DoesNotContain("Cubes.xml missing", text);
                Assert.DoesNotContain("Cubes.xml unreadable", text);

                CorpusRecord.Provenance("a-walk-that-does-not-exist");
                Assert.Equal(text, File.ReadAllText(path));

                CorpusRecord.Started.Clear();
                CorpusRecord.Provenance("a-walk-that-does-not-exist");

                string appended = File.ReadAllText(path);
                Assert.StartsWith(text, appended);
                Assert.True(appended.Length > text.Length,
                    "a resumed walk overwrote its provenance instead of appending, so a dataset"
                    + " assembled across two builds would claim only the second");
                Assert.Equal(2, Occurrences(appended, "walk a-walk-that-does-not-exist"));
                Assert.Equal(2, Occurrences(appended, "Cubes.xml "));
            }
            finally
            {
                Environment.SetEnvironmentVariable("THERMAL_CORPUS_DATA", previous);
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                }
            }
        }


        private static int Occurrences(string text, string needle)
        {
            int count = 0;
            for (int i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }
    }
}

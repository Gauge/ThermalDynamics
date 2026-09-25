using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The mod API's delegate table has the shape the API page says it has.
    ///
    /// <para>
    /// A consumer reaches this mod by casting: `api["GetBlockTemperature"] as
    /// Func&lt;IMySlimBlock, float&gt;`. A cast that does not match returns **null**, not an error —
    /// so a signature that changes on one side and not the other produces a second mod whose
    /// feature silently does nothing, in a session, with no message anywhere. `R9` calls the API
    /// page part of the contract, and until now the only thing checked was that every entry is
    /// *named* there.
    /// </para>
    ///
    /// <para>
    /// Textual, because the shape is text on both sides and neither can be reached without a
    /// session. It cannot check that a signature is a good one; it checks that there is one answer
    /// rather than two (`D3`).
    /// </para>
    /// </summary>
    public class ModApiShapeTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static Dictionary<string, string> Declared()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "ThermalApi.cs"));

            Dictionary<string, string> shapes = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in Regex.Matches(source, @"methods\[""(\w+)""\]"))
            {
                int from = source.IndexOf("new Func<", match.Index, StringComparison.Ordinal);
                if (from < 0) continue;

                int open = source.IndexOf('<', from);
                int depth = 0;
                int i = open;

                while (i < source.Length)
                {
                    if (source[i] == '<') depth++;

                    else if (source[i] == '>')
                    {
                        depth--;
                        if (depth == 0) break;
                    }
                    i++;
                }

                if (i >= source.Length) continue;

                shapes[match.Groups[1].Value] = Normalise("Func" + source.Substring(open, i - open + 1));
            }

            return shapes;
        }

        [Fact]

        public void EveryEntryInTheTableIsWrappedSoItCannotThrowIntoItsCaller()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "ThermalApi.cs"));


            List<string> bare = new List<string>();

            foreach (Match match in Regex.Matches(source,
                @"methods\[""(\w+)""\]\s*=\s*(?<value>[^;]+);", RegexOptions.Singleline))
            {
                if (!match.Groups["value"].Value.TrimStart().StartsWith("Guard(", StringComparison.Ordinal))
                {
                    bare.Add(match.Groups[1].Value);
                }
            }

            Assert.True(bare.Count == 0,
                "these API entries are not wrapped in Guard, so an exception inside one reaches the"

                + " consumer that called it (`W4`): " + string.Join(", ", bare.ToArray()));

            Assert.True(Declared().Count >= 15,

                "only " + Declared().Count + " entries were found in the table, so this judged"
                + " almost nothing");
        }


        private static Dictionary<string, string> Documented()
        {
            string page = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "api.md"));

            Dictionary<string, string> shapes = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Match match in Regex.Matches(page,
                @"\|\s*`(\w+)`\s*\|\s*`(Func<[^`]*)`\s*\|"))
            {

                shapes[match.Groups[1].Value] = Normalise(match.Groups[2].Value);
            }

            return shapes;
        }


        private static string Normalise(string signature)
        {
            return Regex.Replace(signature, @"\s+", "");
        }

        [Fact]

        public void ApiVersionMovesWhenTheSurfaceBreaks()
        {
            string path = Path.Combine(RepoRoot(), "tests", "Thermodynamics.Tests",
                "ApiSurface.txt");

            Assert.True(File.Exists(path), "no recorded API surface at " + path);

            Dictionary<string, string> recorded =
                new Dictionary<string, string>(StringComparer.Ordinal);
            int recordedVersion = 0;

            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

                int split = trimmed.IndexOf('=');
                Assert.True(split > 0, "unreadable line in ApiSurface.txt: " + trimmed);

                string name = trimmed.Substring(0, split);
                string value = trimmed.Substring(split + 1);

                if (name == "version")
                {
                    recordedVersion = int.Parse(value, CultureInfo.InvariantCulture);
                    continue;
                }

                recorded[name] = value;
            }

            Assert.True(recorded.Count > 5,
                "the recorded surface holds " + recorded.Count + " entries, so this check would "
                + "pass whatever the table said");


            Dictionary<string, string> declared = Declared();

            List<string> breaking = new List<string>();

            List<string> added = new List<string>();

            foreach (KeyValuePair<string, string> entry in recorded)
            {
                string signature;
                if (!declared.TryGetValue(entry.Key, out signature))
                {
                    breaking.Add(entry.Key + " is gone");
                    continue;
                }

                if (signature != entry.Value)
                {
                    breaking.Add(entry.Key + " was " + entry.Value + " and is " + signature);
                }
            }

            foreach (KeyValuePair<string, string> entry in declared)
            {
                if (!recorded.ContainsKey(entry.Key)) added.Add(entry.Key);
            }


            int current = DeclaredVersion();

            if (breaking.Count == 0)
            {
                Assert.True(current == recordedVersion,

                    "the API surface has not broken — " + added.Count + " key(s) added, nothing "
                    + "removed or reshaped — but ThermalApi.Version is " + current
                    + " against the recorded " + recordedVersion + ". A major that moves without a "
                    + "break is a caller refused for nothing.");
                return;
            }

            breaking.Sort(StringComparer.Ordinal);
            Assert.True(current > recordedVersion,
                "the API surface broke and ThermalApi.Version is still " + current
                + ". Either put it back or move the version and regenerate ApiSurface.txt in the "
                + "same commit:\n  " + string.Join("\n  ", breaking.ToArray()));
        }


        private static int DeclaredVersion()
        {
            string source = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "ThermalApi.cs"));

            Match match = Regex.Match(source, @"public\s+const\s+int\s+Version\s*=\s*(\d+)");
            Assert.True(match.Success, "ThermalApi.cs declares no `public const int Version`");

            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        [Fact]

        public void EveryEntryHasTheSignatureTheApiPageGivesIt()
        {

            Dictionary<string, string> declared = Declared();

            Dictionary<string, string> documented = Documented();

            Assert.True(declared.Count > 10,
                "only " + declared.Count + " entries were read out of the delegate table, so it has"
                + " changed shape and this test is reading nothing");
            Assert.True(documented.Count > 10,
                "only " + documented.Count + " signatures were read out of the API page");


            List<string> wrong = new List<string>();

            foreach (KeyValuePair<string, string> entry in declared)
            {
                string page;
                if (!documented.TryGetValue(entry.Key, out page))
                {
                    wrong.Add(entry.Key + ": the page gives it no signature");
                    continue;
                }

                if (page == entry.Value) continue;

                wrong.Add(entry.Key + ":\n      code " + entry.Value + "\n      page " + page);
            }

            foreach (KeyValuePair<string, string> entry in documented)
            {
                if (!declared.ContainsKey(entry.Key))
                {
                    wrong.Add(entry.Key + ": the page documents it and the table does not hold it");
                }
            }

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,
                "the delegate table and the API page disagree about " + wrong.Count
                + " entries:\n  " + string.Join("\n  ", wrong.ToArray()));
        }

        [Fact]

        public void TheWorkedExamplesCastWithTheSignaturesTheTableGives()
        {
            string page = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "api.md"));

            Dictionary<string, string> documented = Documented();


            List<string> wrong = new List<string>();
            int checked_ = 0;

            foreach (Match match in Regex.Matches(page,
                @"api\[""(\w+)""\]\s*as\s+(Func<[^;>]*(?:<[^>]*>)?[^;]*?)>\s*;"))
            {
                string name = match.Groups[1].Value;

                string cast = Normalise(match.Groups[2].Value + ">");

                string table;
                if (!documented.TryGetValue(name, out table)) continue;

                checked_++;
                if (cast == table) continue;

                wrong.Add(name + ":\n      example " + cast + "\n      table   " + table);
            }

            Assert.True(checked_ > 3,
                "only " + checked_ + " worked casts were read, so this test is not reading the"
                + " examples");

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,
                "worked examples that cast differently from the table:\n  "
                + string.Join("\n  ", wrong.ToArray()));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The documentation is checked the way the code is: every cross-reference between the pages is a
    /// claim that a file, a heading, a rule or a test exists, and **a dead link is worse than a missing
    /// one, because it reads as though somebody checked**. Textual and cheap.
    /// Holds `R7`, `R10`, `R12`, `R13` and half of `E5`.
    /// </summary>
    [Trait("speed", "slow")]
    public class DocumentationTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static List<string> MarkdownFiles()
        {

            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(RepoRoot(), "*.md", SearchOption.AllDirectories))
            {

                string relative = Relative(file);
                if (relative.StartsWith("out/", StringComparison.Ordinal)) continue;
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                files.Add(file);
            }

            files.Sort(StringComparer.Ordinal);
            return files;
        }


        // A directory with no file in it is a leftover of a move rather than a target, and git does
        // not carry one, so a link resolving to it resolves on this machine and nowhere else.
        private static bool HoldsAFile(string directory)
        {
            return Directory.Exists(directory)
                && Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
        }


        private static string Relative(string file)
        {
            return file.Substring(RepoRoot().Length).TrimStart('/', '\\').Replace('\\', '/');
        }


        private static string Slug(string heading)
        {
            string text = Regex.Replace(heading, @"\[([^\]]*)\]\([^)]*\)", "$1");
            text = text.Replace("`", "").Replace("*", "").Replace("_", "");
            text = text.ToLowerInvariant().Trim();


            StringBuilder slug = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) slug.Append(c);
                else if (c == ' ' || c == '-') slug.Append('-');
            }

            return slug.ToString();
        }


        private static HashSet<string> Anchors(string file)
        {

            HashSet<string> anchors = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.Ordinal);

            bool inFence = false;
            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("```", StringComparison.Ordinal)) { inFence = !inFence; continue; }
                if (inFence) continue;

                Match heading = Regex.Match(line, @"^#{1,6}\s+(.*?)\s*$");
                if (!heading.Success) continue;


                string slug = Slug(heading.Groups[1].Value);
                if (slug.Length == 0) continue;

                int count;
                if (seen.TryGetValue(slug, out count))
                {
                    seen[slug] = count + 1;
                    anchors.Add(slug + "-" + count);
                }
                else
                {
                    seen[slug] = 1;
                    anchors.Add(slug);
                }
            }

            return anchors;
        }

        [Fact]

        public void EveryRelativeLinkResolves()
        {

            List<string> files = MarkdownFiles();
            Assert.True(files.Count > 25,
                "only " + files.Count + " markdown files were found, so this test is looking in the"
                + " wrong place and would pass whatever the documentation said");


            List<string> broken = new List<string>();
            int checked_ = 0;

            foreach (string file in files)
            {
                string folder = Path.GetDirectoryName(file);
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\[[^\]]*\]\(([^)\s]+)\)"))
                {
                    string target = match.Groups[1].Value;
                    if (target.StartsWith("http", StringComparison.Ordinal)) continue;
                    if (target.StartsWith("#", StringComparison.Ordinal)) continue;

                    string path = target.Split('#')[0];
                    if (path.Length == 0) continue;

                    checked_++;
                    string full = Path.GetFullPath(Path.Combine(folder, path));
                    if (!File.Exists(full) && !HoldsAFile(full))
                    {
                        broken.Add(Relative(file) + " -> " + target);
                    }
                }
            }

            Assert.True(checked_ > 200,
                "only " + checked_ + " relative links were checked, so the link pattern has changed");

            broken.Sort(StringComparer.Ordinal);
            Assert.True(broken.Count == 0,
                "documentation links pointing at files that do not exist:\n  "
                + string.Join("\n  ", broken.ToArray()));
        }

        [Fact]

        public void EveryAnchorNamesAHeading()
        {
            Dictionary<string, HashSet<string>> anchors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            List<string> broken = new List<string>();
            int checked_ = 0;

            foreach (string file in MarkdownFiles())
            {
                string folder = Path.GetDirectoryName(file);
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\[[^\]]*\]\(([^)\s]+)\)"))
                {
                    string target = match.Groups[1].Value;
                    if (target.StartsWith("http", StringComparison.Ordinal)) continue;

                    int hash = target.IndexOf('#');
                    if (hash < 0) continue;

                    string anchor = target.Substring(hash + 1);
                    if (anchor.Length == 0) continue;

                    string path = target.Substring(0, hash);
                    string full = path.Length == 0 ? file : Path.GetFullPath(Path.Combine(folder, path));
                    if (!full.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(full)) continue;

                    HashSet<string> headings;
                    if (!anchors.TryGetValue(full, out headings))
                    {

                        headings = Anchors(full);
                        anchors[full] = headings;
                    }

                    checked_++;
                    if (!headings.Contains(anchor))
                    {
                        broken.Add(Relative(file) + " -> " + target);
                    }
                }
            }

            Assert.True(checked_ > 20,
                "only " + checked_ + " anchored links were checked, so the link pattern has changed");

            broken.Sort(StringComparer.Ordinal);
            Assert.True(broken.Count == 0,
                "documentation links pointing at headings that do not exist:\n  "
                + string.Join("\n  ", broken.ToArray()));
        }


        private static int TestCaseCount()
        {
            int count = 0;

            foreach (Type type in typeof(DocumentationTests).Assembly.GetTypes())
            {
                if (!type.IsPublic || type.IsAbstract) continue;

                foreach (System.Reflection.MethodInfo method in type.GetMethods(
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    object[] facts = method.GetCustomAttributes(typeof(FactAttribute), true);
                    if (facts.Length == 0) continue;

                    object[] data = method.GetCustomAttributes(typeof(Xunit.Sdk.DataAttribute), true);
                    if (data.Length == 0) { count++; continue; }

                    foreach (Xunit.Sdk.DataAttribute source in data)
                    {
                        foreach (object[] row in source.GetData(method)) { count++; }
                    }
                }
            }

            return count;
        }

        [Fact]

        public void EveryQuotedSuiteSizeIsCurrent()
        {

            int actual = TestCaseCount();
            Assert.True(actual > 1000,
                "only " + actual + " test cases were counted by reflection, so this test is not"
                + " seeing the suite and would pass whatever a page claimed");


            List<string> wrong = new List<string>();

            foreach (string file in MarkdownFiles())
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file),
                    @"([\d][\d,]*)\s+tests\b"))
                {
                    int quoted = int.Parse(match.Groups[1].Value.Replace(",", ""));
                    if (quoted < 500) continue;

                    if (quoted > actual || quoted < actual - actual / 10)
                    {
                        wrong.Add(Relative(file) + ": quoted " + match.Groups[1].Value
                            + " tests against " + actual + " actual");
                    }
                }
            }

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,

                "pages quoting a suite size that is no longer true (the current count is "
                + actual + "):\n  " + string.Join("\n  ", wrong.ToArray()));
        }

        private class QuotedCount
        {
            public string Noun;
            public Func<int> Actual;

            public float Tolerance;
        }


        private static List<QuotedCount> QuotedCounts()
        {
            return new List<QuotedCount>
            {
                new QuotedCount
                {
                    Noun = "panel ships",
                    Actual = PanelShipCount,
                    Tolerance = 0f,
                },
                new QuotedCount
                {
                    Noun = "authored values",
                    Actual = AuthoredValueCount,
                    Tolerance = 0f,
                },
                new QuotedCount
                {
                    Noun = "waste fractions",
                    Actual = WasteFractionCount,
                    Tolerance = 0f,
                },
                new QuotedCount
                {
                    Noun = "test classes",
                    Actual = TestClassCount,
                    Tolerance = 0.1f,
                },
            };
        }


        private static int PanelShipCount()
        {
            string path = Path.Combine(RepoRoot(), "tools", "corpus", "panel.csv");
            if (!File.Exists(path)) return -1;

            int rows = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Trim().Length > 0) rows++;
            }

            return rows - 1;
        }


        private static int AuthoredValueCount()
        {
            string path = Path.Combine(ShippedBlocks.DataRoot(), "Cubes.xml");
            if (!File.Exists(path)) return -1;

            return Regex.Matches(File.ReadAllText(path), @"<(?:Decimal|Bool)\s+Name=").Count;
        }


        private static int WasteFractionCount()
        {
            string path = Path.Combine(ShippedBlocks.DataRoot(), "Cubes.xml");
            if (!File.Exists(path)) return -1;

            return Regex.Matches(File.ReadAllText(path),
                "<Decimal\\s+Name=\"(?:Producer|Consumer)WasteEnergy\"").Count;
        }


        private static int TestClassCount()
        {
            int count = 0;
            foreach (string file in Directory.GetFiles(
                Path.Combine(RepoRoot(), "tests", "Thermodynamics.Tests"), "*.cs"))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), TestClassPattern))
                {
                    if (HoldsCases(match.Groups[1].Value)) count++;
                }
            }

            return count;
        }

        [Fact]

        public void EveryQuotedDatasetCountIsCurrent()
        {

            List<string> wrong = new List<string>();
            int judged = 0;

            foreach (QuotedCount quoted in QuotedCounts())
            {
                int actual = quoted.Actual();
                Assert.True(actual > 0,
                    "could not count the " + quoted.Noun + ", so this test would pass whatever a"
                    + " page claimed about them");

                int slack = (int)(actual * quoted.Tolerance);

                Regex pattern = new Regex(@"([\d][\d,]*)\s+" + Regex.Escape(quoted.Noun));

                foreach (string file in Documented())
                {
                    foreach (string sentence in Sentences(PresentTense(File.ReadAllText(file))))
                    {
                        foreach (Match match in pattern.Matches(sentence))
                        {
                            int stated = int.Parse(match.Groups[1].Value.Replace(",", ""));
                            judged++;

                            if (Math.Abs(stated - actual) <= slack) continue;

                            if (Regex.IsMatch(sentence, @"\b" + actual + @"\b")) continue;

                            wrong.Add(Relative(file) + ": " + stated + " " + quoted.Noun
                                + " against " + actual + " actual");
                        }
                    }
                }
            }

            Assert.True(judged > 0,
                "no page quoted any of the counts this test knows how to check, so it judged"

                + " nothing (`E8`)");

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,
                "counts that no longer match what they describe:\n  "
                + string.Join("\n  ", wrong.ToArray()));
        }


        private static IEnumerable<string> Documented()
        {
            foreach (string file in MarkdownFiles()) yield return file;
            foreach (string file in SourceFiles())
            {

                string relative = Relative(file);
                if (relative.Contains("RichHudFramework")) continue;
                if (relative.Contains("NetworkAPI")) continue;

                yield return file;
            }
        }


        private static string PresentTense(string page)
        {
            Match log = Regex.Match(page, @"(?m)^##+\s+Change log\s*$");
            return log.Success ? page.Substring(0, log.Index) : page;
        }


        private static IEnumerable<string> Sentences(string text)
        {
            return Regex.Split(text, @"(?<=[.!?])\s+|\n\s*\n");
        }

        private const string TestClassPattern =
            @"(?m)^[ \t]*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)";


        private static bool HoldsCases(string name)
        {
            return name.EndsWith("Tests", StringComparison.Ordinal)
                || name.EndsWith("Walk", StringComparison.Ordinal)
                || name.EndsWith("Survey", StringComparison.Ordinal)
                || name.EndsWith("Sweep", StringComparison.Ordinal)
                || name.EndsWith("Census", StringComparison.Ordinal);
        }

        [Fact]

        public void EveryTestClassIsInTheIndex()
        {
            string index = File.ReadAllText(Path.Combine(RepoRoot(), "tests", "README.md"));
            string folder = Path.Combine(RepoRoot(), "tests", "Thermodynamics.Tests");


            List<string> missing = new List<string>();

            List<string> names = new List<string>();

            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), TestClassPattern))
                {
                    string name = match.Groups[1].Value;
                    if (!HoldsCases(name)) continue;

                    names.Add(name);
                    if (index.IndexOf("`" + name + "`", StringComparison.Ordinal) < 0) missing.Add(name);
                }
            }

            Assert.True(names.Count > 100,
                "only " + names.Count + " test classes were recognised, so the declaration pattern"
                + " has changed and this test is no longer reading anything");

            missing.Sort(StringComparer.Ordinal);
            Assert.True(missing.Count == 0,
                "test classes filed under no subject in tests/README.md:\n  "
                + string.Join("\n  ", missing.ToArray()));
        }


        private static string CamelPrefix(string name, int words)
        {
            MatchCollection parts = Regex.Matches(name, @"[A-Z][a-z0-9]*");
            if (parts.Count < words) return null;

            System.Text.StringBuilder prefix = new System.Text.StringBuilder();
            for (int i = 0; i < words; i++) prefix.Append(parts[i].Value);
            return prefix.ToString();
        }

        [Fact]

        public void NoPageNamesATestThatHasBeenRenamed()
        {

            HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);

            List<string> testNames = new List<string>();

            foreach (string file in Directory.GetFiles(
                Path.Combine(RepoRoot(), "tests", "Thermodynamics.Tests"), "*.cs"))
            {
                string source = File.ReadAllText(file);

                foreach (Match match in Regex.Matches(source,
                    @"public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\("))
                {
                    declared.Add(match.Groups[1].Value);
                    testNames.Add(match.Groups[1].Value);
                }

                foreach (Match match in Regex.Matches(source, TestClassPattern))
                {
                    declared.Add(match.Groups[1].Value);
                }
            }

            Assert.True(testNames.Count > 1000,
                "only " + testNames.Count + " test names were found, so this test is looking in the"
                + " wrong place and would pass whatever a page claimed");

            Dictionary<string, string> byPrefix = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in testNames)
            {

                string prefix = CamelPrefix(name, 3);
                if (prefix != null && !byPrefix.ContainsKey(prefix)) byPrefix[prefix] = name;
            }


            List<string> renamed = new List<string>();

            foreach (string file in MarkdownFiles())
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file),
                    @"`([A-Z][a-z0-9]*(?:[A-Z][a-z0-9]*){3,})`"))
                {
                    string cited = match.Groups[1].Value;
                    if (declared.Contains(cited)) continue;


                    string prefix = CamelPrefix(cited, 3);
                    string actual;
                    if (prefix == null || !byPrefix.TryGetValue(prefix, out actual)) continue;

                    renamed.Add(Relative(file) + ": " + cited + " — did the page mean " + actual + "?");
                }
            }

            renamed.Sort(StringComparer.Ordinal);
            Assert.True(renamed.Count == 0,
                "documentation naming tests that have been renamed:\n  "
                + string.Join("\n  ", renamed.ToArray()));
        }

        [Fact]

        public void EveryTestClassSaysWhatItIsFor()
        {
            string folder = Path.Combine(RepoRoot(), "tests", "Thermodynamics.Tests");
            string[] files = Directory.GetFiles(folder, "*.cs");

            Assert.True(files.Length > 100,
                "only " + files.Length + " test sources were found, so this test is looking in the"
                + " wrong place and would pass whatever the suite did");


            List<string> undocumented = new List<string>();
            int classes = 0;

            foreach (string file in files)
            {
                string source = File.ReadAllText(file);

                foreach (Match match in Regex.Matches(source, TestClassPattern))
                {
                    string name = match.Groups[1].Value;
                    if (!HoldsCases(name)) continue;

                    classes++;

                    string before = source.Substring(0, match.Index).TrimEnd();
                    while (before.EndsWith("]", StringComparison.Ordinal))
                    {
                        int open = before.LastIndexOf('[');
                        if (open < 0) break;
                        before = before.Substring(0, open).TrimEnd();
                    }

                    if (!before.EndsWith("</summary>", StringComparison.Ordinal))
                    {
                        undocumented.Add(Path.GetFileName(file) + ": " + name);
                    }
                }
            }

            Assert.True(classes > 100,
                "only " + classes + " test classes were recognised, so the declaration pattern has"
                + " changed and this test is no longer reading anything");

            undocumented.Sort(StringComparer.Ordinal);
            Assert.True(undocumented.Count == 0,
                "test classes with no summary saying what they are for:\n  "
                + string.Join("\n  ", undocumented.ToArray()));
        }

        [Theory]
        [InlineData("ThermalCellDefinition.cs", "ThermalBlockProperties", 8)]
        [InlineData("PlanetDefinition.cs", "ThermalPlanetProperties", 8)]
        [InlineData("ThermalLoopDefinition.cs", "ThermalLoopProperties", 8)]

        public void EveryPropertyTheGameReadsIsInTheReference(string file, string group, int least)
        {
            string reader = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "Definitions", file));


            HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(reader, @"GetOrCompute\(""(\w+)""\)"))
            {
                read.Add(match.Groups[1].Value);
            }

            read.Remove(group);

            Assert.True(read.Count >= least,
                "only " + read.Count + " property names were found in " + file + ", so this test is"
                + " no longer looking at it");

            string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "definitions.md"));


            HashSet<string> documented = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(doc, @"(?m)^\|\s*`(\w+)`"))
            {
                documented.Add(match.Groups[1].Value);
            }


            List<string> missing = new List<string>();
            foreach (string name in read)
            {
                if (!documented.Contains(name)) missing.Add(name);
            }

            missing.Sort(StringComparer.Ordinal);
            Assert.True(missing.Count == 0,
                "properties " + file + " reads and docs/definitions.md does not list:\n  "
                + string.Join("\n  ", missing.ToArray()));
        }

        [Fact]

        public void EveryModApiEntryIsDocumented()
        {
            string api = File.ReadAllText(Path.Combine(RepoRoot(),
                "Thermodynamics", "ThermalApi.cs"));


            List<string> keys = new List<string>();
            foreach (Match match in Regex.Matches(api, @"methods\[""(\w+)""\]"))
            {
                keys.Add(match.Groups[1].Value);
            }

            Assert.True(keys.Count > 10,
                "only " + keys.Count + " API entries were found, so the table has changed shape and"
                + " this test is no longer reading it");

            string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "api.md"));


            List<string> missing = new List<string>();
            foreach (string key in keys)
            {
                if (doc.IndexOf("`" + key + "`", StringComparison.Ordinal) < 0) missing.Add(key);
            }

            missing.Sort(StringComparer.Ordinal);
            Assert.True(missing.Count == 0,
                "entries in the mod API's delegate table that docs/api.md does not name:\n  "
                + string.Join("\n  ", missing.ToArray()));
        }

        [Fact]

        public void EveryPageHasAChangeLog()
        {

            List<string> missing = new List<string>();

            List<string> undated = new List<string>();
            int checked_ = 0;

            foreach (string file in MarkdownFiles())
            {

                string relative = Relative(file);

                if (relative.Contains("RichHudFramework")) continue;
                if (relative.Contains("NetworkAPI")) continue;

                checked_++;
                string text = File.ReadAllText(file);

                if (!Regex.IsMatch(text, @"(?m)^##\s+Change log\s*$"))
                {
                    missing.Add(relative);
                    continue;
                }

                string log = text.Substring(text.LastIndexOf("## Change log", StringComparison.Ordinal));
                if (!Regex.IsMatch(log, @"(?m)^\|\s*\d{4}-\d{2}-\d{2}\s*\|"))
                {
                    undated.Add(relative);
                }
            }

            Assert.True(checked_ > 20,
                "only " + checked_ + " pages were checked, so this test is looking in the wrong"
                + " place and would pass whatever the documentation said");

            missing.Sort(StringComparer.Ordinal);
            undated.Sort(StringComparer.Ordinal);

            Assert.True(missing.Count == 0,

                "pages with no \"## Change log\" section (see docs/development.md, Documentation"
                + " conventions):\n  " + string.Join("\n  ", missing.ToArray()));

            Assert.True(undated.Count == 0,
                "pages whose change log holds no dated `| YYYY-MM-DD |` row:\n  "
                + string.Join("\n  ", undated.ToArray()));
        }

        [Fact]

        public void NoDocCommentDescribesSomethingThatIsNotThere()
        {
            string[] roots =
            {
                Path.Combine(RepoRoot(), "Thermodynamics"),
                Path.Combine(RepoRoot(), "tests"),
            };


            List<string> orphans = new List<string>();
            int files = 0;

            foreach (string root in roots)
            {
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {

                    string relative = Relative(file);

                    if (relative.Contains("RichHudFramework")) continue;
                    if (relative.Contains("NetworkAPI")) continue;
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;

                    files++;
                    string[] lines = File.ReadAllLines(file);

                    for (int i = 0; i + 1 < lines.Length; i++)
                    {
                        string current = lines[i].Trim();
                        if (!current.StartsWith("///", StringComparison.Ordinal)) continue;
                        if (!current.EndsWith("</summary>", StringComparison.Ordinal)) continue;
                        if (!lines[i + 1].Trim().StartsWith("/// <summary>", StringComparison.Ordinal)) continue;

                        orphans.Add(relative + ":" + (i + 1) + " — closed and reopened");
                    }

                    int openedAt = -1;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string current = lines[i].Trim();
                        if (!current.StartsWith("///", StringComparison.Ordinal))
                        {
                            openedAt = -1;
                            continue;
                        }

                        int at = 0;
                        while (at < current.Length)
                        {
                            int open = current.IndexOf("<summary>", at, StringComparison.Ordinal);
                            int close = current.IndexOf("</summary>", at, StringComparison.Ordinal);

                            if (close >= 0 && (open < 0 || close < open))
                            {
                                openedAt = -1;
                                at = close + "</summary>".Length;
                                continue;
                            }

                            if (open < 0) break;

                            if (openedAt >= 0) orphans.Add(relative + ":" + (openedAt + 1) + " — opened twice");
                            openedAt = i;
                            at = open + "<summary>".Length;
                        }
                    }
                }
            }

            Assert.True(files > 200,
                "only " + files + " source files were read, so this test is looking in the wrong"
                + " place and would pass whatever the tree said");

            orphans.Sort(StringComparer.Ordinal);
            Assert.True(orphans.Count == 0,
                "doc comments closed and immediately reopened, which means the first one belongs to a"
                + " member that is no longer there:\n  " + string.Join("\n  ", orphans.ToArray()));
        }

        [Fact]

        public void EveryDocumentIsInTheIndex()
        {
            string index = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "README.md"));


            List<string> missing = new List<string>();
            foreach (string file in Directory.GetFiles(Path.Combine(RepoRoot(), "docs"), "*.md"))
            {
                string name = Path.GetFileName(file);
                if (name == "README.md") continue;

                if (index.IndexOf("(" + name + ")", StringComparison.Ordinal) < 0) missing.Add(name);
            }

            missing.Sort(StringComparer.Ordinal);
            Assert.True(missing.Count == 0,
                "pages under docs/ that the documentation index does not list:\n  "
                + string.Join("\n  ", missing.ToArray()));
        }


        private static string RulesPage()
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "docs", "rules.md"));
        }


        private static HashSet<string> StatedRules()
        {

            HashSet<string> rules = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(RulesPage(), @"(?m)^#{3,4}\s+([EMDCROJW]\d{1,2})\s+—"))
            {
                rules.Add(m.Groups[1].Value);
            }

            return rules;
        }

        [Fact]

        public void EveryRuleCitedByAPageExists()
        {

            HashSet<string> stated = StatedRules();
            Assert.True(stated.Count > 40,
                "only " + stated.Count + " rules were read out of docs/rules.md, so this test is"
                + " parsing the page wrongly and would pass whatever any page cited");


            List<string> dangling = new List<string>();
            int banners = 0;

            foreach (string file in MarkdownFiles())
            {

                string relative = Relative(file);
                if (relative == "docs/rules.md") continue;

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("stated canonically in", StringComparison.Ordinal) < 0) continue;


                    StringBuilder banner = new StringBuilder(lines[i]);
                    for (int j = i + 1; j < lines.Length && lines[j].StartsWith(">", StringComparison.Ordinal); j++)
                    {
                        banner.Append(' ').Append(lines[j]);
                    }

                    banners++;
                    int cited = 0;
                    foreach (Match m in Regex.Matches(banner.ToString(), @"`([EMDCROJW]\d{1,2})`"))
                    {
                        cited++;
                        if (!stated.Contains(m.Groups[1].Value))
                        {
                            dangling.Add(relative + " cites `" + m.Groups[1].Value + "`");
                        }
                    }

                    Assert.True(cited > 0,
                        relative + " says its rules are stated canonically in rules.md and then"
                        + " names none of them by identifier");
                    break;
                }
            }

            Assert.True(banners > 10,
                "only " + banners + " pages carry a rules banner, so this test found almost"
                + " nothing to check and would pass on a page citing a rule that never existed");

            dangling.Sort(StringComparer.Ordinal);
            Assert.True(dangling.Count == 0,
                "rules cited by a page that docs/rules.md does not state:\n  "
                + string.Join("\n  ", dangling.ToArray()));
        }

        [Fact]

        public void EveryCitedIdentifierResolves()
        {

            HashSet<string> known = new HashSet<string>(StatedRules(), StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(BacklogPage(), @"(?m)^\|\s*([A-Z]\d{1,2})\s*\|"))
            {
                known.Add(m.Groups[1].Value);
            }

            Assert.True(known.Count > 100,
                "only " + known.Count + " identifiers were read out of the two pages, so this test"
                + " is parsing them wrongly and would pass on any citation at all");


            List<string> dangling = new List<string>();
            int cited = 0;
            int modFiles = 0;
            int testFiles = 0;

            foreach (string file in SourceFiles())
            {

                string relative = Relative(file);
                if (relative.StartsWith("Thermodynamics/", StringComparison.Ordinal)) modFiles++;
                if (relative.StartsWith("tests/", StringComparison.Ordinal)) testFiles++;

                if (relative.Contains("RichHudFramework")) continue;
                if (relative.Contains("NetworkAPI")) continue;

                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"`([EMDCROJWK]\d{1,2})`"))
                {
                    cited++;
                    if (!known.Contains(m.Groups[1].Value))
                    {
                        dangling.Add(relative + " cites `" + m.Groups[1].Value + "`");
                    }
                }
            }

            Assert.True(modFiles > 100 && testFiles > 100 && cited > 0,
                "read " + modFiles + " mod files, " + testFiles + " test files and " + cited
                + " citations, so this test is looking in the wrong place and would pass whatever a"
                + " comment said");

            dangling.Sort(StringComparer.Ordinal);
            Assert.True(dangling.Count == 0,
                "identifiers cited in source that are neither a rule in docs/rules.md nor a row in"
                + " docs/backlog.md:\n  " + string.Join("\n  ", dangling.ToArray()));
        }

        [Fact]

        public void TheTwoPagesShareNoIdentifierTheyDidNotAlreadyShare()
        {

            HashSet<string> allowed = new HashSet<string>(new[]
            {
                "C3", "C7", "C8", "D1", "D2", "D3", "D4", "D5", "D6", "E2", "E4",
            }, StringComparer.Ordinal);


            HashSet<string> rules = new HashSet<string>(StatedRules(), StringComparer.Ordinal);


            HashSet<string> rows = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(BacklogPage(), @"(?m)^\|\s*([A-Z]\d{1,2})\s*\|"))
            {
                rows.Add(m.Groups[1].Value);
            }

            Assert.True(rules.Count > 50,
                "only " + rules.Count + " rules were read out of docs/rules.md");
            Assert.True(rows.Count > 40,
                "only " + rows.Count + " rows were read out of docs/backlog.md");


            List<string> shared = new List<string>();
            foreach (string identifier in rules)
            {
                if (rows.Contains(identifier)) shared.Add(identifier);
            }


            List<string> added = new List<string>();
            foreach (string identifier in shared)
            {
                if (!allowed.Contains(identifier)) added.Add(identifier);
            }

            added.Sort(StringComparer.Ordinal);
            Assert.True(added.Count == 0,

                added.Count + " identifier(s) now mean one thing in docs/rules.md and another in"
                + " docs/backlog.md that did not before. Pick a letter the other page does not use:"
                + "\n  " + string.Join("\n  ", added.ToArray()));
        }

        [Fact]

        public void NoPointerInCodeIsWrittenAsALink()
        {

            List<string> links = new List<string>();
            int files = 0;

            foreach (string file in SourceFiles())
            {

                string relative = Relative(file);
                if (!relative.EndsWith(".cs", StringComparison.Ordinal)) continue;
                if (relative.Contains("RichHudFramework")) continue;
                if (relative.Contains("NetworkAPI")) continue;

                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (Regex.IsMatch(lines[i], @"\[[^\]]+\]\([^)]*\.md[^)]*\)"))
                    {
                        links.Add(relative + ":" + (i + 1));
                    }
                }
            }

            Assert.True(files > 200,
                "only " + files + " source files were read, so this test is looking in the wrong"
                + " place and would pass whatever the tree held");

            links.Sort(StringComparer.Ordinal);
            Assert.True(links.Count == 0,
                "markdown links inside .cs files, which render nowhere and are checked by nothing"
                + " (R16) — write the page's name as plain text instead:\n  "
                + string.Join("\n  ", links.ToArray()));
        }


        private static string BacklogPage()
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "docs", "backlog.md"));
        }


        private static IEnumerable<string> SourceFiles()
        {
            string[] patterns = { "*.cs", "*.py" };
            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(RepoRoot(), pattern, SearchOption.AllDirectories))
                {

                    string relative = Relative(file);
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    if (relative.StartsWith("out/", StringComparison.Ordinal)) continue;

                    yield return file;
                }
            }
        }

        [Fact]

        public void TheRulesPageIndexesEveryRuleItStates()
        {

            string page = RulesPage();


            HashSet<string> stated = StatedRules();

            HashSet<string> indexed = new HashSet<string>(StringComparer.Ordinal);

            HashSet<string> lowValue = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match m in Regex.Matches(page, @"(?m)^\|\s*\*\*([EMDCROJW]\d{1,2})\*\*\s*\|([^|]*)\|([^|]*)\|"))
            {
                indexed.Add(m.Groups[1].Value);
                if (m.Groups[3].Value.IndexOf("low value", StringComparison.Ordinal) >= 0)
                {
                    lowValue.Add(m.Groups[1].Value);
                }
            }

            Assert.True(indexed.Count > 40,
                "only " + indexed.Count + " rules were read out of the index, so this test is"
                + " parsing the page wrongly");


            List<string> unstated = new List<string>(indexed);
            unstated.RemoveAll(stated.Contains);
            unstated.Sort(StringComparer.Ordinal);
            Assert.True(unstated.Count == 0,
                "rules the index lists that the page never states:\n  "
                + string.Join("\n  ", unstated.ToArray()));


            List<string> unindexed = new List<string>(stated);
            unindexed.RemoveAll(indexed.Contains);
            unindexed.Sort(StringComparer.Ordinal);
            Assert.True(unindexed.Count == 0,
                "rules the page states that the index does not list:\n  "
                + string.Join("\n  ", unindexed.ToArray()));


            HashSet<string> underAPrinciple = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match row in Regex.Matches(page, @"(?m)^\|\s*\*\*(P\d{1,2})\*\*\s*\|(.*)$"))
            {
                foreach (Match cited in Regex.Matches(row.Groups[2].Value, @"`([EMDCROJW]\d{1,2})`"))
                {
                    underAPrinciple.Add(cited.Groups[1].Value);
                }
            }


            List<string> orphaned = new List<string>();
            foreach (string rule in stated)
            {
                if (lowValue.Contains(rule)) continue;
                if (!underAPrinciple.Contains(rule)) orphaned.Add(rule);
            }

            orphaned.Sort(StringComparer.Ordinal);
            Assert.True(orphaned.Count == 0,
                "rules that follow from no principle in the table:\n  "
                + string.Join("\n  ", orphaned.ToArray()));
        }


        private static string CodeText()
        {
            if (_codeText != null) return _codeText;


            StringBuilder all = new StringBuilder();
            string[] patterns = { "*.cs", "*.csproj", "*.props", "*.json", "*.py", "*.sh" };
            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(RepoRoot(), pattern, SearchOption.AllDirectories))
                {

                    string relative = Relative(file);
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    if (relative.StartsWith("out/", StringComparison.Ordinal)) continue;
                    all.Append(File.ReadAllText(file)).Append('\n');
                }
            }

            _codeText = all.ToString();
            return _codeText;
        }

        private static string _codeText;


        private static HashSet<string> LiveCases()
        {

            HashSet<string> cases = new HashSet<string>(StringComparer.Ordinal);
            string tests = Path.Combine(RepoRoot(), "tests");

            foreach (string file in Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories))
            {

                string relative = Relative(file);
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;

                string text = File.ReadAllText(file);
                string current = null;

                foreach (Match m in Regex.Matches(text,
                    @"(?m)^\s*(?:public|internal)\s+(?:sealed\s+|static\s+|partial\s+|abstract\s+)*class\s+([A-Za-z0-9_]+)"
                    + @"|\[(?:Fact|Theory)[^\]]*\][\s\S]{0,400}?\bpublic\s+(?:async\s+)?[A-Za-z0-9_<>,\[\]\. ]+?\s([A-Za-z0-9_]+)\s*\("))
                {
                    if (m.Groups[1].Success)
                    {
                        current = m.Groups[1].Value;
                    }

                    else if (m.Groups[2].Success)
                    {
                        cases.Add(m.Groups[2].Value);
                        if (current != null) cases.Add(current);
                    }
                }
            }

            return cases;
        }

        [Fact]

        public void EveryClaimAPageSaysIsPinnedNamesSomethingThatExists()
        {

            HashSet<string> live = LiveCases();

            string code = CodeText();

            Assert.True(live.Count > 500,
                "only " + live.Count + " live test names were found, so this test is not reading"
                + " the suite and would pass on a citation to nothing");


            Regex citation = new Regex(
                "\\b(?:[Pp]inned|[Cc]hecked|[Aa]sserted|[Mm]easured) by(.{0,400}?)"
                + "(?:\\r?\\n\\r?\\n|\\z)",
                RegexOptions.Singleline);


            Regex identifier = new Regex(@"^[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*$");


            List<string> unresolved = new List<string>();
            int cited = 0;

            foreach (string file in MarkdownFiles())
            {
                if (Relative(file) == "docs/rules.md") continue;

                foreach (Match match in citation.Matches(File.ReadAllText(file)))
                {
                    foreach (Match name in Regex.Matches(match.Groups[1].Value, @"`([^`\r\n]+)`"))
                    {
                        string cite = name.Groups[1].Value;
                        if (!identifier.IsMatch(cite)) continue;

                        cited++;
                        if (live.Contains(cite)) continue;

                        if (Regex.Matches(code, @"\b" + Regex.Escape(cite) + @"\b").Count > 1) continue;

                        unresolved.Add(Relative(file) + ": " + cite);
                    }
                }
            }

            Assert.True(cited >= 15,
                "only " + cited + " pinned-by citations were read across the tree, so this test is"
                + " parsing them wrongly and would pass on a page citing nothing");

            unresolved.Sort(StringComparer.Ordinal);
            Assert.True(unresolved.Count == 0,
                "claims a page says are pinned, naming something that does not exist:\n  "
                + string.Join("\n  ", unresolved.ToArray())
                + "\nA citation dies when the test it names is rewritten, and the prose it was"
                + " holding up stays where it was — check the paragraph as well as the name.");
        }

        [Fact]

        public void EveryCheckCitedByTheRulesPageResolves()
        {
            string[] lines = File.ReadAllLines(Path.Combine(RepoRoot(), "docs", "rules.md"));

            HashSet<string> live = LiveCases();

            string code = CodeText();

            Assert.True(live.Count > 500,
                "only " + live.Count + " live test names were found, so this test is not reading"
                + " the suite and would pass on a citation to nothing");


            List<string> unresolved = new List<string>();
            int cited = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("*Checked by:*", StringComparison.Ordinal)) continue;


                StringBuilder field = new StringBuilder(lines[i]);
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (lines[j].Length == 0 || lines[j].StartsWith("*", StringComparison.Ordinal)) break;
                    field.Append(' ').Append(lines[j]);
                }

                foreach (Match m in Regex.Matches(field.ToString(), @"`([^`]+)`"))
                {
                    string name = m.Groups[1].Value;

                    if (name.Contains("/") || Regex.IsMatch(name, @"\.[a-z]{1,6}$"))
                    {
                        cited++;
                        string path = Path.Combine(RepoRoot(), name.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(path)) continue;
                        if (Directory.GetFiles(RepoRoot(), Path.GetFileName(name), SearchOption.AllDirectories).Length > 0) continue;
                        unresolved.Add(name + " — no such file");
                        continue;
                    }

                    if (!Regex.IsMatch(name, @"^[A-Z][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")) continue;

                    cited++;
                    string leaf = name.Substring(name.LastIndexOf('.') + 1);
                    if (live.Contains(leaf)) continue;

                    if (Regex.Matches(code, @"\b" + Regex.Escape(leaf) + @"\b").Count > 1) continue;

                    unresolved.Add(name + " — declared nowhere, or declared and never used");
                }

                i++;
            }

            Assert.True(cited > 30,
                "only " + cited + " checks were read out of the rules page's *Checked by* fields,"
                + " so this test is parsing them wrongly");

            unresolved.Sort(StringComparer.Ordinal);
            Assert.True(unresolved.Count == 0,

                "checks docs/rules.md cites that do not resolve to anything that runs (R11):\n  "
                + string.Join("\n  ", unresolved.ToArray()));
        }

        [Fact]

        public void EveryCorpusWalkDeclaresThatItRunsAlone()
        {
            string tests = Path.Combine(RepoRoot(), "tests");

            List<string> offenders = new List<string>();
            int walks = 0;

            foreach (string file in Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories))
            {

                string relative = Relative(file);
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;

                string text = File.ReadAllText(file);

                if (Path.GetFileName(file) == "CorpusFixture.cs") continue;
                if (!Regex.IsMatch(text, @"\bCorpusFixture\.(Files|Sweep|Walk)\b")) continue;

                walks++;
                if (!text.Contains("[Collection(\"alone\")]")) offenders.Add(relative);
            }

            Assert.True(walks >= 4,
                "only " + walks + " corpus walks were found, so this test is not reading the suite"
                + " and would pass on a walk that runs beside another");

            offenders.Sort(StringComparer.Ordinal);
            Assert.True(offenders.Count == 0,

                "corpus walks that do not declare the collection that runs alone (O4):\n  "
                + string.Join("\n  ", offenders.ToArray()));
        }
        [Fact]

        public void NoCommittedFileCarriesAConflictMarker()
        {
            string[] markers = { "<" + "<<<<<< ", "=" + "======", ">" + ">>>>>> " };

            List<string> found = new List<string>();

            foreach (string file in TextFiles())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    for (int m = 0; m < markers.Length; m++)
                    {
                        if (!line.StartsWith(markers[m], StringComparison.Ordinal)) continue;
                        if (markers[m][0] == '=' && line.TrimEnd() != "=======") continue;

                        found.Add(Relative(file) + ":" + (i + 1) + "  " + line.Trim());
                    }
                }
            }

            Assert.True(found.Count == 0,
                "these files carry unresolved merge conflicts:\n  " + string.Join("\n  ", found));
        }


        private static List<string> TextFiles()
        {
            string[] extensions = { "*.md", "*.xml", "*.py", "*.sh", "*.cs", "*.csproj", "*.sln" };

            List<string> files = new List<string>();

            foreach (string pattern in extensions)
            {
                foreach (string file in Directory.GetFiles(RepoRoot(), pattern,
                    SearchOption.AllDirectories))
                {

                    string relative = Relative(file);
                    if (relative.StartsWith("out/", StringComparison.Ordinal)) continue;
                    if (relative.StartsWith(".git/", StringComparison.Ordinal)) continue;
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    if (relative.Contains("/RichHudFramework/")) continue;
                    files.Add(file);
                }
            }

            files.Sort(StringComparer.Ordinal);
            return files;
        }

    }
}

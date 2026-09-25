using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every setting a world can carry is documented, and every documented setting exists.
    ///
    /// <para>
    /// `configuration.md` is the reference an administrator reads before changing anything, and it
    /// drifts in one direction: settings get added to `Settings.cs` and the menu, and the reference
    /// is written later or not at all. Twenty-one had accumulated that way — the whole `Loop*` and
    /// `Planet*` families, added when the loop and planet definitions became world settings, plus
    /// `MaxSubsteps` and `ClampEnvironmentOvershoot`, which the prose discussed at length and the
    /// tables never listed.
    /// </para>
    ///
    /// <para>
    /// The check is textual because `Settings.cs` reads `Sandbox.*` and cannot be linked into this
    /// project. That is enough: a setting is declared on one line with a `ProtoMember` attribute,
    /// and documented as the first cell of a table row. Both forms are stable, and the failure this
    /// guards against is a setting that appears in one and not the other.
    /// </para>
    /// </summary>
    public class ConfigurationDocTests
    {
        [Fact]

        public void TheCruiseToolScoresTheCoefficientTheModShips()
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), "tools", "corpus", "cruise.py");
            Assert.True(File.Exists(path), path + " does not exist");

            Match declared = Regex.Match(File.ReadAllText(path),
                @"^SHIPPED_DRAG_COEFFICIENT\s*=\s*([0-9.]+)", RegexOptions.Multiline);

            Assert.True(declared.Success,
                "cruise.py declares no SHIPPED_DRAG_COEFFICIENT, so nothing pins it to the setting");

            float pinned = float.Parse(declared.Groups[1].Value, CultureInfo.InvariantCulture);
            float shipped = new Thermodynamics.Core.ThermalSettings().DragCoefficient;

            Assert.True(Math.Abs(pinned - shipped) < 1e-6f,
                "cruise.py scores C_d " + pinned + " and the mod ships " + shipped
                    + ". Every cruise figure taken with the tool's default describes a"
                    + " configuration nobody runs — move the constant with the setting.");
        }

        private static readonly HashSet<string> NotTunable =
            new HashSet<string> { "Version", "LegacyLoopConductivity" };


        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }

        [Fact]

        public void EverySettingIsOnAMenuPageThatNamesIt()
        {
            string menu = File.ReadAllText(Path.Combine(
                RepoRoot(), "Thermodynamics", "ThermalSettingsMenu.cs"));

            int folders = menu.IndexOf("private static readonly Folder[] Folders", StringComparison.Ordinal);
            int debug = menu.IndexOf("private static readonly Leaf DebugPage", StringComparison.Ordinal);
            Assert.True(folders >= 0 && debug >= 0, "the menu's layout tables have been renamed");


            HashSet<string> placed = new HashSet<string>();
            foreach (Match match in Regex.Matches(
                menu.Substring(folders, menu.IndexOf("private static readonly Dictionary<string, string> PageNotes",
                    StringComparison.Ordinal) - folders)
                + menu.Substring(debug, 900), "\"([A-Za-z0-9_]+)\""))
            {
                placed.Add(match.Groups[1].Value);
            }


            List<string> orphans = new List<string>();
            foreach (string name in Declared())
            {
                if (NotTunable.Contains(name)) continue;
                if (!placed.Contains(name)) orphans.Add(name);
            }

            orphans.Sort();

            Assert.True(orphans.Count == 0,
                orphans.Count + " settings reach no page and would land on *Other*, with their own"
                + " field name for a label and a slider that fits nothing:\n  "
                + string.Join("\n  ", orphans.ToArray()));
        }


        private static HashSet<string> Declared()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot(), "Thermodynamics", "Settings.cs"));


            HashSet<string> names = new HashSet<string>();
            foreach (Match match in Regex.Matches(source,
                @"\[ProtoMember\(\d+\)\]\s*public\s+[A-Za-z0-9_<>\[\]]+\s+([A-Za-z0-9_]+)"))
            {
                string name = match.Groups[1].Value;
                if (!NotTunable.Contains(name)) names.Add(name);
            }

            return names;
        }


        private static HashSet<string> Documented()
        {
            string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "configuration.md"));


            HashSet<string> names = new HashSet<string>();
            foreach (Match match in Regex.Matches(doc, @"(?m)^\|\s*`([A-Za-z0-9_]+)`"))
            {
                names.Add(match.Groups[1].Value);
            }

            return names;
        }

        [Fact]

        public void EveryMechanismSwitchIsClassifiedInTheLadderInventory()
        {
            string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "configuration.md"));

            const string Heading = "## Every mechanism, and the rungs it has";
            int start = doc.IndexOf(Heading, StringComparison.Ordinal);
            Assert.True(start >= 0, "docs/configuration.md has no ladder inventory section");

            int end = doc.IndexOf("\n## ", start + Heading.Length, StringComparison.Ordinal);
            string inventory = end < 0 ? doc.Substring(start) : doc.Substring(start, end - start);

            HashSet<string> diagnostics = new HashSet<string> { "EnableTelemetry" };


            List<string> mechanisms = new List<string>();
            foreach (string name in Declared())
            {
                if (!name.StartsWith("Enable", StringComparison.Ordinal)) continue;
                if (diagnostics.Contains(name)) continue;
                mechanisms.Add(name);
            }

            Assert.True(mechanisms.Count > 10, "only " + mechanisms.Count + " mechanism switches"
                + " were found, so this test is not reading Settings.cs");


            List<string> unclassified = new List<string>();
            foreach (string name in mechanisms)
            {
                if (!inventory.Contains("`" + name + "`")) unclassified.Add(name);
            }

            unclassified.Sort(StringComparer.Ordinal);
            Assert.True(unclassified.Count == 0,
                "mechanisms with no row in configuration.md's ladder inventory, so nothing says"
                + " what rungs they have:\n  " + string.Join("\n  ", unclassified.ToArray()));
        }

        [Fact]

        public void EveryMechanismInTheLadderInventoryHasASwitchOrSaysItHasNoLadder()
        {
            string doc = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "configuration.md"));

            const string Heading = "## Every mechanism, and the rungs it has";
            int start = doc.IndexOf(Heading, StringComparison.Ordinal);
            Assert.True(start >= 0, "docs/configuration.md has no ladder inventory section");

            int end = doc.IndexOf("\n## ", start + Heading.Length, StringComparison.Ordinal);
            string inventory = end < 0 ? doc.Substring(start) : doc.Substring(start, end - start);


            HashSet<string> switches = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in Declared())
            {
                if (name.StartsWith("Enable", StringComparison.Ordinal)) switches.Add(name);
            }


            HashSet<string> noLadder = new HashSet<string>(StringComparer.Ordinal)
                { "1", "scalars", "—", "-" };


            List<string> switchless = new List<string>();
            int rows = 0;

            foreach (string line in inventory.Split('\n'))
            {
                string row = line.Trim();
                if (!row.StartsWith("|", StringComparison.Ordinal)) continue;

                string[] cells = row.Trim('|').Split('|');
                if (cells.Length < 3) continue;

                string mechanism = cells[0].Trim();
                if (mechanism == "Mechanism" || mechanism.StartsWith("---", StringComparison.Ordinal))
                {
                    continue;
                }

                rows++;
                if (noLadder.Contains(cells[2].Trim())) continue;

                bool named = false;
                foreach (string name in switches)
                {
                    if (cells[1].Contains("`" + name + "`")) { named = true; break; }
                }

                if (!named) switchless.Add(mechanism + " — configured by " + cells[1].Trim());
            }

            Assert.True(rows > 10,
                "only " + rows + " inventory rows were read, so this test is not reading the table");

            Assert.True(switchless.Count == 0,
                "mechanisms in configuration.md's ladder inventory with a ladder and no switch to"
                + " turn them off, which is `C7`:\n  " + string.Join("\n  ", switchless.ToArray()));
        }

        [Fact]

        public void EveryCoreSettingIsReachableFromAWorldsConfiguration()
        {
            string core = File.ReadAllText(Path.Combine(ShippedBlocks.ModRoot(), "Core", "Settings", "ThermalSettings.cs"));
            string world = File.ReadAllText(Path.Combine(ShippedBlocks.ModRoot(), "Settings.cs"));

            HashSet<string> derived = new HashSet<string>
            {
                "Version", "Revision", "StepSeconds", "StepsPerSecond",
            };


            List<string> unreachable = new List<string>();
            int fields = 0;

            foreach (Match match in Regex.Matches(core,
                @"(?m)^\s*public\s+(?:bool|float|int)\s+([A-Za-z0-9_]+)\s*(?:=|\{)"))
            {
                string name = match.Groups[1].Value;
                if (derived.Contains(name)) continue;

                fields++;
                if (!Regex.IsMatch(world, @"core\." + Regex.Escape(name) + @"\s*=")) unreachable.Add(name);
            }

            Assert.True(fields > 30, "only " + fields + " core settings were found, so this test is"
                + " not reading ThermalSettings.cs and would pass on a rung wired to nothing");

            unreachable.Sort(StringComparer.Ordinal);
            Assert.True(unreachable.Count == 0,
                "core settings the solver reads that no world configuration can set, so they are"
                + " rungs reachable only by a test:\n  " + string.Join("\n  ", unreachable.ToArray()));
        }

        [Fact]

        public void EverySettingIsInTheReference()
        {

            HashSet<string> declared = Declared();
            Assert.True(declared.Count > 40, "only " + declared.Count
                + " settings were found in Settings.cs, so the declaration pattern has changed and"
                + " this test is no longer reading anything");


            HashSet<string> documented = Documented();


            List<string> missing = new List<string>();
            foreach (string name in declared)
            {
                if (!documented.Contains(name)) missing.Add(name);
            }

            missing.Sort();

            Assert.True(missing.Count == 0,
                "settings that exist and are not in docs/configuration.md's tables:\n  "
                + string.Join("\n  ", missing.ToArray()));
        }

        [Fact]

        public void TheReferenceDocumentsNothingThatHasBeenRemoved()
        {

            HashSet<string> declared = Declared();

            HashSet<string> documented = Documented();

            HashSet<string> profiles = new HashSet<string>
            {
                "simulation", "optimized", "simlite", "responsive", "arcade",
            };


            List<string> stale = new List<string>();
            foreach (string name in documented)
            {
                if (profiles.Contains(name)) continue;
                if (!declared.Contains(name) && !NotTunable.Contains(name)) stale.Add(name);
            }

            stale.Sort();

            Assert.True(stale.Count == 0,
                "docs/configuration.md documents settings that no longer exist:\n  "
                + string.Join("\n  ", stale.ToArray()));
        }

        private static readonly HashSet<string> NotReaders = new HashSet<string>
        {
            "Settings.cs", "ThermalSettingsMenu.cs", "SettingsSync.cs", "SettingsRequests.cs",
        };

        [Fact]

        public void EverySettingIsReadBySomething()
        {

            HashSet<string> declared = Declared();

            string scripts = Path.Combine(RepoRoot(), "Thermodynamics");
            System.Text.StringBuilder body = new System.Text.StringBuilder();

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("RichHudFramework") || file.Contains("NetworkAPI")) continue;
                if (NotReaders.Contains(Path.GetFileName(file))) continue;

                body.Append(File.ReadAllText(file)).Append('\n');
            }

            foreach (Match assignment in Regex.Matches(
                File.ReadAllText(Path.Combine(scripts, "Settings.cs")),
                @"\bcore\.[A-Za-z0-9_]+\s*=\s*([^;]+);"))
            {
                body.Append(assignment.Groups[1].Value).Append('\n');
            }

            string source = body.ToString();
            Assert.True(source.Length > 100000,
                "only " + source.Length + " characters of source were read, so this test is looking"
                + " in the wrong place and would pass whatever the code did");


            List<string> unread = new List<string>();
            foreach (string name in declared)
            {
                if (source.IndexOf(name, StringComparison.Ordinal) < 0) unread.Add(name);
            }

            unread.Sort();

            Assert.True(unread.Count == 0,
                "settings that exist, appear in the menu and are read by nothing:\n  "
                + string.Join("\n  ", unread.ToArray()));
        }
    }
}

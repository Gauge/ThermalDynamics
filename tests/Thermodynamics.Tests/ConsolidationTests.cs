using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Consolidations decay, and this is the sweep that noticed, promoted to a check.**
    ///
    /// <para>
    /// The cleanup effort's fourth pass found three of its finds were earlier passes' own
    /// consolidations with a surviving or re-created copy, and its fifth found a tool that had
    /// been unimportable for six days because a consolidation deleted a name a sibling still
    /// used. The sweep that finds these was run by hand at the top of each pass; a sweep run by
    /// hand is a sweep that is eventually not run (`P5`, `D5`). Each case here is one
    /// consolidated definition's banned form: the pattern must appear in the file that owns the
    /// definition — a scan that sees nothing is not looking (`E8`) — and nowhere else, with the
    /// standing exclusions from cleanup-effort.md carried as exemptions, each with its reason.
    /// </para>
    ///
    /// <para>
    /// The python tools' half of this is `tools/corpus/test_tool_imports.py`; the vendored trees
    /// are excluded because `R6` forbids editing them.
    /// </para>
    /// </summary>
    public class ConsolidationTests
    {

        private static List<KeyValuePair<string, string>> SourceFiles()
        {
            List<KeyValuePair<string, string>> files = new List<KeyValuePair<string, string>>();
            string root = ShippedBlocks.RepoRoot();

            foreach (string folder in new[] { "Thermodynamics", "tests" })
            {
                foreach (string path in Directory.GetFiles(Path.Combine(root, folder), "*.cs",
                    SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                    if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
                    if (relative.Contains("NetworkAPI/") || relative.Contains("RichHudFramework/")) continue;
                    if (relative.EndsWith("DefinitionExtensionsAPI.cs")) continue;
                    if (relative.EndsWith("ConsolidationTests.cs")) continue;
                    files.Add(new KeyValuePair<string, string>(relative, File.ReadAllText(path)));
                }
            }

            return files;
        }

        private sealed class Consolidation
        {
            public string Pattern;
            public string Owner;
            public string[] Exempt = new string[0];
            public string Why;
        }

        private static readonly Consolidation[] Consolidations =
        {
            new Consolidation
            {

                Pattern = "float Clamp01(",
                Owner = "Thermodynamics/Core/Util/ThermalMath.cs",
                Why = "ten private copies were consolidated in iteration 36",
            },
            new Consolidation
            {
                Pattern = "Replace(\"\\\"\", \"\\\"\\\"\")",
                Owner = "tests/Thermodynamics.Harness/CsvLine.cs",
                Exempt = new[]
                {
                    "tests/Thermodynamics.Sim/CorpusFetch.cs",

                    "Thermodynamics/Telemetry/TelemetryFormat.cs",
                },
                Why = "six sites stated the CSV escape independently; iteration 48",
            },
            new Consolidation
            {
                Pattern = "fastest = double.MaxValue",
                Owner = "tests/Thermodynamics.Harness/LabTiming.cs",
                Why = "the duration labs' stopwatch; iteration 13",
            },
            new Consolidation
            {
                Pattern = "values[i] = nodes[i].Temperature",
                Owner = "tests/Thermodynamics.Harness/GridState.cs",
                Why = "the grid temperature snapshot; iteration 28",
            },
            new Consolidation
            {
                Pattern = "EntityId < identity",
                Owner = "Thermodynamics/Game/GridGroups.cs",
                Why = "the group-claim identity convention; iteration 20",
            },
            new Consolidation
            {
                Pattern = "WorldToGridInteger(hit.Position",
                Owner = "Thermodynamics/Crosshair.cs",
                Why = "the crosshair's nudged-cell resolution; iteration 16",
            },
            new Consolidation
            {
                Pattern = "(axis + 1) % 3",
                Owner = "Thermodynamics/Core/Util/BoxGeometry.cs",
                Exempt = new[]
                {
                    "tests/Thermodynamics.Tests/SurfaceMapPackingTests.cs",
                },
                Why = "the per-face frame is BoxGeometry.Span; cleanup 4 and iteration 19",
            },
            new Consolidation
            {
                Pattern = "GetEnvironmentVariable(\"SE_BIN\")",
                Owner = "tests/Thermodynamics.Harness/GameBlocks.cs",
                Why = "the game-install candidate list; iteration 12",
            },
        };

        [Fact]

        public void NoConsolidatedDefinitionHasGrownASecondCopy()
        {

            List<KeyValuePair<string, string>> files = SourceFiles();
            Assert.True(files.Count > 100,

                "the scan found almost no source files, so it is not looking at the tree (E8)");


            List<string> problems = new List<string>();

            foreach (Consolidation entry in Consolidations)
            {
                bool ownerSeen = false;

                foreach (KeyValuePair<string, string> file in files)
                {
                    bool contains = file.Value.Contains(entry.Pattern);
                    if (file.Key == entry.Owner)
                    {
                        ownerSeen = contains;
                        continue;
                    }

                    if (!contains) continue;

                    bool exempt = false;
                    foreach (string name in entry.Exempt)
                    {
                        if (file.Key == name) { exempt = true; break; }
                    }

                    if (!exempt)
                    {
                        problems.Add(file.Key + " carries '" + entry.Pattern + "' — " + entry.Why);
                    }
                }

                if (!ownerSeen)
                {
                    problems.Add("the owner " + entry.Owner + " no longer carries '" + entry.Pattern

                        + "', so this case is checking against nothing (E8) — update or retire it");
                }
            }

            Assert.True(problems.Count == 0,
                problems.Count + " consolidation stragglers:\n  "
                + string.Join("\n  ", problems.ToArray()));
        }
    }
}

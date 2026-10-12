using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Settings schema, catalogue coverage and real accessor behavior without loading the game.
    /// The runtime check compiles SettingsAccess against the serialized field declarations and
    /// records the game-facing setter notifications through small stubs. Source checks cover
    /// wire identifiers, clamps, documentation and propagation into the solver.
    /// </summary>
    public class SettingsWiringTests
    {

        [Fact]
        public void CatalogueAccessorsPreserveValuesAndSideEffects()
        {
            string folder = Path.Combine(RepoRoot(), "Thermodynamics");
            string schema = File.ReadAllText(Path.Combine(folder, "Settings.cs"));
            var fields = Regex.Matches(schema, @"\[ProtoMember\(\d+\)\]\s*public\s+(bool|int|float)\s+(\w+)")
                .Cast<Match>().ToDictionary(m => m.Groups[2].Value, m => m.Groups[1].Value);
            string declarations = string.Join("\n", fields.Select(f => "public " + f.Value + " " + f.Key + ";"));
            string support = "namespace Thermodynamics { public partial class Settings { " + declarations + @" }
                public static class ThermalDebugView { public enum Mode { Zero } public static int Last; public static void Set(Mode mode) { Last = (int)mode; } }
                public static class WindOverlay { public enum Mode { Zero } public static int Last; public static void Set(Mode mode) { Last = (int)mode; } }
                public static class Telemetry { public static bool Enabled; public static void SetEnabled(bool value) { Enabled = value; } }
            }";
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("SettingsCatalogueContract", new[]
            {
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(folder, "SettingsAccess.cs"))),
                CSharpSyntaxTree.ParseText(support),
            }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                Assert.True(result.Success, string.Join("\n", result.Diagnostics));
                var assembly = Assembly.Load(stream.ToArray());
                var type = assembly.GetType("Thermodynamics.Settings");
                object settings = Activator.CreateInstance(type);
                var read = type.GetMethod("GetValue");
                var write = type.GetMethod("SetValue");
                var flag = type.GetMethod("IsFlag");
                var names = (List<string>)type.GetMethod("Names").Invoke(null, null);
                Assert.Equal(names.Count, names.Distinct().Count());
                Assert.True(names.Count > 60);
                foreach (string name in names)
                {
                    var field = type.GetField(name);
                    Assert.NotNull(field);
                    Assert.Equal(field.FieldType == typeof(bool), (bool)flag.Invoke(null, new object[] { name }));
                    foreach (float value in new[] { 0f, -2.75f, 7.25f })
                    {
                        Assert.True((bool)write.Invoke(settings, new object[] { name, value }));
                        object expected = field.FieldType == typeof(bool) ? (object)(value != 0f)
                            : field.FieldType == typeof(int) ? (object)(int)value : value;
                        Assert.Equal(expected, field.GetValue(settings));
                        Assert.Equal(Convert.ToSingle(expected), (float)read.Invoke(settings, new object[] { name }));
                    }
                }
                Assert.Equal(7, assembly.GetType("Thermodynamics.ThermalDebugView").GetField("Last").GetValue(null));
                Assert.Equal(7, assembly.GetType("Thermodynamics.WindOverlay").GetField("Last").GetValue(null));
                Assert.Equal(true, assembly.GetType("Thermodynamics.Telemetry").GetField("Enabled").GetValue(null));
                foreach (string missing in new[] { null, "", "enableEnvironment", "Unknown" })
                {
                    Assert.True(float.IsNaN((float)read.Invoke(settings, new object[] { missing })));
                    Assert.False((bool)write.Invoke(settings, new object[] { missing, 1f }));
                }
            }
        }

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static string Source()
        {
            return string.Join("\n", new[] { "Settings.cs", "SettingsAccess.cs", "SettingsStorage.cs" }
                .Select(name => File.ReadAllText(Path.Combine(RepoRoot(), "Thermodynamics", name))));
        }


        private static readonly Regex Declaration = new Regex(
            @"\[ProtoMember\((\d+)\)\]\s*public\s+[A-Za-z0-9_<>\[\]]+\s+([A-Za-z0-9_]+)");

        [Fact]

        public void NoTwoSettingsShareAProtoMemberNumber()
        {
            Dictionary<int, List<string>> byNumber = new Dictionary<int, List<string>>();

            foreach (Match match in Declaration.Matches(Source()))
            {
                int number = int.Parse(match.Groups[1].Value);
                string field = match.Groups[2].Value;

                if (!byNumber.ContainsKey(number)) byNumber[number] = new List<string>();
                byNumber[number].Add(field);
            }

            Assert.True(byNumber.Count > 60,
                "only " + byNumber.Count + " settings were found, so the pattern has changed and this"
                + " test is no longer reading anything");


            List<string> clashes = new List<string>();
            foreach (KeyValuePair<int, List<string>> pair in byNumber)
            {
                if (pair.Value.Count > 1)
                    clashes.Add(pair.Key + ": " + string.Join(", ", pair.Value.ToArray()));
            }

            clashes.Sort();

            Assert.True(clashes.Count == 0,
                "settings sharing a ProtoMember number:\n  " + string.Join("\n  ", clashes.ToArray()));
        }

        [Fact]

        public void NoSettingReusesANumberThatWasDeliberatelyRetired()
        {

            string source = Source();

            int[] retired =
            {
                // Extracted aerodynamic settings belong to a different mod and wire contract.
                18, 42, 43, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173,
                53, 54, 55, 56, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 72, 76,
                // the aerodynamic force and the top-speed port, stripped by K25 on 2026-09-24
                135, 136, 138, 139, 140,
                145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163,
            };

            HashSet<int> banned = new HashSet<int>(retired);


            List<string> reused = new List<string>();
            foreach (Match match in Declaration.Matches(source))
            {
                int number = int.Parse(match.Groups[1].Value);
                if (banned.Contains(number)) reused.Add(number + " on " + match.Groups[2].Value);
            }

            Assert.True(reused.Count == 0,
                "retired ProtoMember numbers reused:\n  " + string.Join("\n  ", reused.ToArray()));
        }



        private static List<string> Names(string source)
        {
            var names = new List<string>();
            foreach (Match match in Regex.Matches(source, @"new SettingBinding\(""(\w+)"""))
                names.Add(match.Groups[1].Value);
            Assert.True(names.Count > 60, "The settings catalogue is missing or unreadable.");

            return names;
        }


        private static HashSet<string> Cases(string source, string signature, string endSignature)
        {
            var cases = new HashSet<string>();
            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            foreach (var entry in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Where(node => node.Type.ToString() == "SettingBinding"))
            {
                var arguments = entry.ArgumentList.Arguments;
                Assert.Equal(3, arguments.Count);
                int accessor = signature.Contains("GetValue") ? 1 : 2;
                Assert.IsAssignableFrom<LambdaExpressionSyntax>(arguments[accessor].Expression);
                cases.Add(((LiteralExpressionSyntax)arguments[0].Expression).Token.ValueText);
            }
            return cases;
        }


        [Fact]

        public void EveryNamedSettingCanBeReadAndWritten()
        {

            string source = Source();

            List<string> names = Names(source);

            Assert.True(names.Count > 60, "only " + names.Count + " names found");


            HashSet<string> readable = Cases(source, "public float GetValue", "public bool SetValue");

            HashSet<string> writable = Cases(source, "public bool SetValue", null);


            List<string> unreadable = new List<string>();

            List<string> unwritable = new List<string>();

            for (int i = 0; i < names.Count; i++)
            {
                if (!readable.Contains(names[i])) unreadable.Add(names[i]);
                if (!writable.Contains(names[i])) unwritable.Add(names[i]);
            }

            Assert.True(unreadable.Count == 0,
                "named settings GetValue cannot read:\n  " + string.Join("\n  ", unreadable.ToArray()));
            Assert.True(unwritable.Count == 0,
                "named settings SetValue cannot write:\n  " + string.Join("\n  ", unwritable.ToArray()));
        }

        [Fact]

        public void NothingIsReadableOrWritableWithoutBeingNamed()
        {

            string source = Source();

            HashSet<string> names = new HashSet<string>(Names(source));


            HashSet<string> readable = Cases(source, "public float GetValue", "public bool SetValue");

            HashSet<string> writable = Cases(source, "public bool SetValue", null);


            List<string> orphans = new List<string>();
            foreach (string name in readable) if (!names.Contains(name)) orphans.Add("get " + name);
            foreach (string name in writable) if (!names.Contains(name)) orphans.Add("set " + name);

            orphans.Sort();

            Assert.True(orphans.Count == 0,

                "cases that Names() does not list:\n  " + string.Join("\n  ", orphans.ToArray()));
        }

        [Fact]

        public void EverySettingThatIsAModeRatherThanASwitchIsExcludedFromIsFlag()
        {

            string source = Source();

            int start = source.IndexOf("public static bool IsFlag", StringComparison.Ordinal);
            Assert.True(start >= 0);

            string body = source.Substring(start, Math.Min(1200, source.Length - start));

            foreach (string mode in new[] { "DebugBlockOverlay", "DebugWindOverlay" })
            {
                Assert.True(source.Contains(mode),
                    mode + " no longer exists, so this test should be updated");

                Assert.Contains("!= \"" + mode + "\"", body);
            }
        }

        [Fact]

        public void EveryBoolSettingIsAFlagAndNothingElseIs()
        {

            string source = Source();

            int start = source.IndexOf("public static bool IsFlag", StringComparison.Ordinal);
            Assert.True(start >= 0, "IsFlag is gone, so this test should be updated");

            string body = source.Substring(start, Math.Min(1600, source.Length - start));


            List<string> named = Names(source);

            List<string> wrong = new List<string>();

            foreach (Match match in Regex.Matches(source, @"public (bool|int|float) (\w+)\s*[;=]"))
            {
                string type = match.Groups[1].Value;
                string name = match.Groups[2].Value;
                if (!named.Contains(name)) continue;

                bool recognised = name.StartsWith("Enable", StringComparison.Ordinal)
                    || name.StartsWith("Debug", StringComparison.Ordinal)
                    || body.Contains("name == \"" + name + "\"");

                if (body.Contains("name != \"" + name + "\"")) recognised = false;

                if (type == "bool" && !recognised)
                {
                    wrong.Add(name + " is a bool and IsFlag does not know it: the menu draws a"
                        + " slider, /thermal prints a number and the report records one");
                }

                if (type != "bool" && recognised)
                {
                    wrong.Add(name + " is a " + type + " and IsFlag calls it a switch");
                }
            }

            Assert.True(wrong.Count == 0,
                wrong.Count + " settings are the wrong shape to IsFlag:\n  "
                + string.Join("\n  ", wrong.ToArray()));
        }

        [Fact]

        public void EverySettingIsClampedOrDeliberatelyNot()
        {

            string source = Source();

            string[] mustBeGuarded =
            {
                "WindRoughnessLength", "WindGradientHeight", "WindDiurnalAmplitude",
                "WindDiurnalCrossover", "WindTerrainInfluence", "WindTerrainRadius",
                "WindSlopeStrength", "DebugWindOverlay", "TelemetryPlanetProbes",
            };

            int start = source.IndexOf("private void Clamp()", StringComparison.Ordinal);
            Assert.True(start >= 0, "could not find the clamping pass");

            int open = source.IndexOf('{', start);
            Assert.True(open > 0);

            int depth = 0, end = -1;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;

                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            Assert.True(end > open, "Clamp() has no closing brace this test can find");
            string body = source.Substring(open, end - open);

            for (int i = 0; i < mustBeGuarded.Length; i++)
            {
                Assert.Contains(mustBeGuarded[i], body);
            }
        }

        [Fact]

        public void TheTwoClampListsAgreeOnEveryFieldTheyShare()
        {
            Dictionary<string, string> world =
                ClampLines(MethodBody(Source(), "private void Clamp()"));

            Dictionary<string, string> solver = ClampLines(MethodBody(
                File.ReadAllText(Path.Combine(RepoRoot(),
                    "Thermodynamics", "Core", "Settings", "ThermalSettings.cs")),

                "public ThermalSettings Derive()"));


            List<string> differ = new List<string>();
            int shared = 0;
            foreach (KeyValuePair<string, string> pair in world)
            {
                string other;
                if (!solver.TryGetValue(pair.Key, out other)) continue;
                shared++;
                if (pair.Value != other)
                {
                    differ.Add(pair.Key + ": world [" + pair.Value + "] solver [" + other + "]");
                }
            }

            Assert.True(shared >= 10,
                "the scan matched only " + shared + " shared clamped fields, so it is not seeing"

                + " the lists it claims to compare (E8)");
            Assert.True(differ.Count == 0,
                differ.Count + " shared fields are clamped differently by the two copies:\n  "
                + string.Join("\n  ", differ.ToArray()));
        }


        private static string MethodBody(string source, string anchor)
        {
            int start = source.IndexOf(anchor, StringComparison.Ordinal);
            Assert.True(start >= 0, "could not find '" + anchor + "'");

            int open = source.IndexOf('{', start);
            Assert.True(open > 0);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;

                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open);
                }
            }

            Assert.Fail("'" + anchor + "' has no closing brace this test can find");
            return null;
        }


        private static readonly Regex ClampLine = new Regex(
            @"if \((\w+) (<=?|>=?) ([^)]+)\)\s*(\w+) = ([^;]+);");


        private static Dictionary<string, string> ClampLines(string body)
        {
            Dictionary<string, string> clamps = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in ClampLine.Matches(body))
            {
                if (match.Groups[1].Value != match.Groups[4].Value) continue;

                string value = match.Groups[5].Value.Trim();
                int dot = value.LastIndexOf('.');
                if (dot >= 0 && Regex.IsMatch(value, @"^[\w.]+$")) value = value.Substring(dot + 1);

                string rule = match.Groups[2].Value + " " + match.Groups[3].Value.Trim()
                    + " -> " + value;
                clamps[match.Groups[1].Value] =
                    clamps.ContainsKey(match.Groups[1].Value)
                        ? clamps[match.Groups[1].Value] + "; " + rule
                        : rule;
            }
            return clamps;
        }

        [Fact]

        public void EverySolverSettingIsCopiedFromTheWorldsCopy()
        {

            string source = Source();


            HashSet<string> exempt = new HashSet<string>(StringComparer.Ordinal)
            {
                "Version",
            };


            List<string> missing = new List<string>();
            int checked_ = 0;

            foreach (System.Reflection.FieldInfo field in typeof(Thermodynamics.Core.ThermalSettings)
                .GetFields(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance))
            {
                // The offline assembly adds historical aerodynamic fixture knobs; they are not shipped thermal settings.
                string production = File.ReadAllText(Path.Combine(RepoRoot(), "Thermodynamics/Core/Settings/ThermalSettings.cs"));
                if (!Regex.IsMatch(production, @"public\s+(?:bool|float|int)\s+" + field.Name + @"\s*[=;{]")) continue;
                Type type = field.FieldType;
                if (type != typeof(bool) && type != typeof(int) && type != typeof(float)) continue;
                if (exempt.Contains(field.Name)) continue;

                checked_++;
                if (source.Contains("core." + field.Name + " = ")) continue;

                missing.Add(field.Name);
            }

            Assert.True(checked_ >= 30,
                "only " + checked_ + " solver settings were found, so this test would pass on a"
                + " class that had lost most of them");

            missing.Sort(StringComparer.Ordinal);
            Assert.True(missing.Count == 0,
                "settings the solver has that Settings.cs never copies into it, so a world can"
                + " never move them:\n  " + string.Join("\n  ", missing.ToArray()));
        }

        [Fact]

        public void NothingIsCopiedIntoTheSolverThatTheSolverDoesNotHave()
        {

            HashSet<string> core = new HashSet<string>(StringComparer.Ordinal);

            foreach (System.Reflection.FieldInfo field in typeof(Thermodynamics.Core.ThermalSettings)
                .GetFields(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance))
            {
                core.Add(field.Name);
            }


            foreach (System.Reflection.PropertyInfo property in typeof(Thermodynamics.Core.ThermalSettings).GetProperties())
                if (property.CanWrite) core.Add(property.Name); // Includes the externally registered heating callback.
            List<string> stray = new List<string>();
            foreach (Match match in Regex.Matches(Source(), @"\bcore\.([A-Za-z0-9_]+)\s*="))
            {
                string name = match.Groups[1].Value;
                if (core.Contains(name)) continue;
                stray.Add(name);
            }

            stray.Sort(StringComparer.Ordinal);
            Assert.True(stray.Count == 0,
                "Settings.cs assigns to solver settings that do not exist:\n  "
                + string.Join("\n  ", stray.ToArray()));
        }
    }
}

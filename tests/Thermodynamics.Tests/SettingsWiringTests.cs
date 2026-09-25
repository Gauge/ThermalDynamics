using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The plumbing every setting has to pass through, checked as text.
    ///
    /// <para><c>Settings.cs</c> reads <c>Sandbox.*</c> and cannot be linked into this project, so
    /// none of this can be done by calling it. That is fine: every fault this guards against is
    /// visible in the source, and each of them is silent at runtime.</para>
    ///
    /// <para><b>Why these exist.</b> A setting is wired by hand in five places — a field with a
    /// <c>ProtoMember</c> number, an entry in <c>Names()</c>, a case in <c>GetValue</c>, a case in
    /// <c>SetValue</c>, and a row in the reference documentation. Miss one and nothing fails; the
    /// setting simply does not work, or works in one direction, or quietly overwrites another
    /// setting on the wire. All four of those have happened in this codebase.</para>
    /// </summary>
    public class SettingsWiringTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static string Source()
        {
            return File.ReadAllText(Path.Combine(
                RepoRoot(), "Thermodynamics", "Settings.cs"));
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
            Match block = Regex.Match(source,
                @"public static List<string> Names\(\)\s*\{\s*return new List<string>\s*\{(.*?)\};",
                RegexOptions.Singleline);

            Assert.True(block.Success, "Names() no longer has the shape this test reads");


            List<string> names = new List<string>();
            foreach (Match match in Regex.Matches(block.Groups[1].Value, "\"([A-Za-z0-9_]+)\""))
            {
                names.Add(match.Groups[1].Value);
            }
            return names;
        }


        private static HashSet<string> Cases(string source, string signature, string endSignature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, "could not find " + signature);

            int end = endSignature == null
                ? source.Length
                : source.IndexOf(endSignature, start, StringComparison.Ordinal);

            if (end < 0) end = source.Length;


            HashSet<string> cases = new HashSet<string>();
            foreach (Match match in Regex.Matches(source.Substring(start, end - start),
                "case \"([A-Za-z0-9_]+)\""))
            {
                cases.Add(match.Groups[1].Value);
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Which way each integration dial points is stated in the code, and the page cannot drift
    /// from it.**
    ///
    /// <para>
    /// `C15` wants a mechanism configured from `off` to `realistic`. The four integration dials are
    /// the exception, and configuration.md said so and then said *nothing tells a reader which way
    /// each points but this page* — which is the sentence this file exists to make false. They run
    /// in three directions: two are faithful at zero, two at their ceiling, and a player in the
    /// menu had four differently worded tips and no rule (`B31`).
    /// </para>
    /// </summary>
    public class FidelityEndTests
    {

        private static string Configuration()
        {
            return File.ReadAllText(Path.Combine(ShippedBlocks.RepoRoot(), "docs", "configuration.md"));
        }


        private static Dictionary<string, float> Defaults()
        {
            string source = File.ReadAllText(Path.Combine(ShippedBlocks.RepoRoot(),
                "Thermodynamics", "Settings.cs"));

            Dictionary<string, float> defaults = new Dictionary<string, float>(StringComparer.Ordinal);

            foreach (Match match in Regex.Matches(source,
                @"\[ProtoMember\(\d+\)\]\s*public\s+[A-Za-z0-9_<>\[\]]+\s+([A-Za-z0-9_]+)\s*=\s*(-?[0-9.]+)f?\s*;"))
            {
                float value;
                if (float.TryParse(match.Groups[2].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    defaults[match.Groups[1].Value] = value;
                }
            }

            Assert.True(defaults.Count > 20,
                "only " + defaults.Count + " defaults were read from Settings.cs, so this test is"
                + " not reading it");

            return defaults;
        }

        [Fact]

        public void EveryDialNamesASettingThatExists()
        {

            Dictionary<string, float> defaults = Defaults();

            Assert.NotEmpty(FidelityEnds.All);

            foreach (FidelityEnds.End end in FidelityEnds.All)
            {
                Assert.True(defaults.ContainsKey(end.Setting),
                    end.Setting + " is in the fidelity table and is not a setting in Settings.cs");
            }
        }

        [Fact]

        public void ThePageAndTheTableAgreeAboutWhichEndIsFaithful()
        {

            string page = Configuration();

            Assert.Equal(0f, Faithful("MaxSubstepsPerBlock"));
            Assert.Contains("`MaxSubstepsPerBlock` 0 is the *faithful* end", page);

            Assert.Equal(0f, Faithful("MaxElementVisitsPerStep"));
            Assert.Contains("`MaxElementVisitsPerStep` 0 means uncapped", page);

            Assert.True(Faithful("MaxSubsteps") > 0f);
            Assert.Contains("`MaxSubsteps` is the opposite", page);

            Assert.True(Faithful("Frequency") > Defaults()["Frequency"]);
            Assert.Contains("`Frequency` up is dearer and more faithful", page);
        }

        [Fact]

        public void TheTwoDialsThatDoNotShipFaithfulAreTheTwoTheSentenceIsFor()
        {

            Dictionary<string, float> shipped = Defaults();

            Assert.Equal(Faithful("MaxSubstepsPerBlock"), shipped["MaxSubstepsPerBlock"]);
            Assert.Equal(Faithful("MaxSubsteps"), shipped["MaxSubsteps"]);

            Assert.NotEqual(Faithful("Frequency"), shipped["Frequency"]);
            Assert.NotEqual(Faithful("MaxElementVisitsPerStep"), shipped["MaxElementVisitsPerStep"]);
        }

        [Fact]

        public void TheSentenceIsUniformAndOnlyForDialsThatHaveOne()
        {
            foreach (FidelityEnds.End end in FidelityEnds.All)
            {
                string sentence = FidelityEnds.Sentence(end.Setting);
                Assert.StartsWith(" Most faithful at ", sentence);
                Assert.EndsWith(".", sentence);
                Assert.Contains(end.Means, sentence);
            }

            Assert.Equal(string.Empty, FidelityEnds.Sentence("EnableConduction"));
            Assert.Equal(string.Empty, FidelityEnds.Sentence("NoSuchSetting"));
            Assert.Null(FidelityEnds.For("EnableConduction"));
        }

        [Fact]

        public void TheMenuRendersTheSentenceIntoEveryDialsTip()
        {
            string menu = File.ReadAllText(Path.Combine(ShippedBlocks.RepoRoot(),
                "Thermodynamics", "ThermalSettingsMenu.cs"));
            string window = File.ReadAllText(Path.Combine(ShippedBlocks.RepoRoot(),
                "Thermodynamics", "ThermalSettingsWindow.cs"));

            Assert.Equal(1, Occurrences(menu, "internal static ToolTip TipFor"));
            Assert.Equal(1, Occurrences(menu, "FidelityEnds.Sentence(name)"));

            int builder = menu.IndexOf("internal static ToolTip TipFor", StringComparison.Ordinal);
            Assert.Contains("DebugBlockOverlay", menu.Substring(builder, 600));

            Assert.Equal(1, Occurrences(window, "ThermalSettingsMenu.TipFor("));
            Assert.Equal(0, Occurrences(window, "new ToolTip"));


            int given = Occurrences(window, "ToolTip = tip");
            Assert.True(given >= 4, "only " + given + " controls were given a tip, so either a"
                + " control kind has lost its tooltip or this is reading the wrong file");
        }


        private static float Faithful(string setting)
        {
            FidelityEnds.End? end = FidelityEnds.For(setting);
            Assert.True(end != null, setting + " has no faithful end in the table");
            return end.Value.Faithful;
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

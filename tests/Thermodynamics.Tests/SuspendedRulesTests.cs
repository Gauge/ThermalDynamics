using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The simulation does not know that the game's own rules have been suspended**, and that is
    /// the decision rather than an omission — document-of-intent.md, When the
    /// game's own rules are suspended.
    ///
    /// <para>
    /// backlog.md `B36` asked what heat should do in creative mode, for a
    /// ship spawned whole, a block placed instantly, or a player in god mode. The answer is that
    /// creative suspends *scarcity* and heat is *physics*: the game keeps damaging, accelerating
    /// and colliding blocks in creative and only stops charging for them. A player who wants no
    /// heat has the mod's own switches, which are a choice rather than an inference from the
    /// world's mode.
    /// </para>
    ///
    /// <para>
    /// **A claim about what code does not do rots the moment somebody adds the line**, which is why
    /// it is pinned here rather than only written down.
    /// </para>
    /// </summary>
    public class SuspendedRulesTests
    {
        private static readonly string[] Routes =
        {
            "CreativeMode",
            "IsCreative",
            "CreativeTools",
            "MyGameModeEnum",
            "GameMode",
            "EnableInstantBuildingCreative",
            "EnableGodMode",
            "InfiniteAmmo",
        };

        [Fact]

        public void NothingInTheModAsksWhetherTheWorldsRulesAreSuspended()
        {
            string scripts = Path.Combine(
                ShippedBlocks.RepoRoot(), "Thermodynamics");

            Assert.True(Directory.Exists(scripts), "no mod sources at " + scripts);


            List<string> offenders = new List<string>();
            int scanned = 0;

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/RichHudFramework/")) continue;

                scanned++;
                string text = File.ReadAllText(file);

                foreach (string route in Routes)
                {
                    if (!text.Contains(route)) continue;

                    offenders.Add(route + " in " + Path.GetFileName(file));
                }
            }

            Assert.True(scanned > 50,
                "only " + scanned + " mod sources were scanned, so this check is looking in the "
                + "wrong place and would pass whatever the code said");

            offenders.Sort(StringComparer.Ordinal);
            Assert.True(offenders.Count == 0,
                "the simulation now behaves differently when the game's rules are suspended, which "
                + "document-of-intent.md says it does not:\n  " + string.Join("\n  ", offenders));
        }

        [Fact]

        public void TurningOffTheConsequenceIsASettingRatherThanAMode()
        {
            ThermalSettingsFields fields = ThermalSettingsFields.Read();

            Assert.True(fields.Has("EnableDamage"),
                "there is no world setting that stops heat from destroying blocks, so a creative "
                + "builder's only lever would be the world's mode");

            Assert.True(fields.Has("EnableSuitDamage"),
                "there is no world setting that stops heat from hurting a player");
        }
    }

    /// <summary>The mod's settings fields, read as text so no game type is loaded.</summary>

    /// <summary>The mod's settings fields, read as text so no game type is loaded.</summary>
    internal class ThermalSettingsFields
    {
        private readonly string text;


        private ThermalSettingsFields(string text)
        {
            this.text = text;
        }


        public static ThermalSettingsFields Read()
        {
            string path = Path.Combine(
                ShippedBlocks.RepoRoot(), "Thermodynamics", "Settings.cs");

            Assert.True(File.Exists(path), "no Settings.cs at " + path);
            return new ThermalSettingsFields(File.ReadAllText(path));
        }


        public bool Has(string field)
        {
            return text.Contains(" " + field + " =") || text.Contains(" " + field + ";");
        }
    }
}

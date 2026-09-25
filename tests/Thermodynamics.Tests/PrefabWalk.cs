using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// `G7`, the compatibility floor: a ship the game spawns survives arriving.
    ///
    /// <para>
    /// Every other criterion is measured on ships players chose to upload. These are the 705 the
    /// game puts in front of a player whether they want them or not — cargo ships, drones,
    /// encounters, unknown signals, respawn pods — and a mod that destroys them as they arrive is
    /// broken however good its physics is. The criterion is stated in
    /// balance-lab.md and was written before any of this ran (`E11`).
    /// </para>
    ///
    /// <para>
    /// **The second case is what makes the first mean anything.** A floor that can only ever pass
    /// has not been tested, so the same prefabs are run again under full load, where they are
    /// expected to fail — 616 of 705 do. Without it, `G7 HOLDS` would be indistinguishable from a
    /// walk that measured nothing (`E8`).
    /// </para>
    ///
    /// <para>
    /// Needs the game installed, and stands down without it like everything else that reads the
    /// install.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class PrefabWalk
    {
        private const int Sample = 140;


        private static List<string> Spread(int count)
        {
            List<string> all = Blueprints.PrefabFiles();
            if (all.Count <= count) return all;


            List<string> spread = new List<string>();
            int stride = all.Count / count;
            if (stride < 1) stride = 1;

            for (int i = 0; i < all.Count && spread.Count < count; i += stride) spread.Add(all[i]);
            return spread;
        }


        private static List<PrefabLab.Outcome> Walk(ShipLoad.State load)
        {
            return LabRun.Map(Spread(Sample), path => PrefabLab.Measure(path, null, load),
                LabMode.Parallel);
        }

        [Fact]

        public void NoShipTheGameSpawnsLosesABlockOnArrival()
        {
            if (Blueprints.PrefabPath() == null) return;


            List<PrefabLab.Outcome> outcomes = Walk(ShipLoad.State.Idle);

            int measured = 0;
            long blocks = 0;

            List<string> lost = new List<string>();

            foreach (PrefabLab.Outcome outcome in outcomes)
            {
                if (outcome.Skipped != null) continue;

                measured++;
                blocks += outcome.Blocks;
                if (outcome.Lost)
                {
                    lost.Add(outcome.Prefab + " (" + outcome.Category + ") lost a block after "
                        + outcome.SecondsToFirstLoss.ToString("n0") + " s");
                }
            }

            Assert.True(measured > 100,

                "only " + measured + " prefabs were measured, so this walk judged nothing (`E8`)");
            Assert.True(blocks > 20000,
                "only " + blocks + " blocks were simulated, so the prefabs are being read as empty");

            lost.Sort(System.StringComparer.Ordinal);
            Assert.True(lost.Count == 0,
                "G7 fails — ships the game spawns lose blocks on arrival:\n  "
                + string.Join("\n  ", lost.ToArray()));
        }

        [Fact]

        public void TheSameShipsFlownHardDoLoseBlocks()
        {
            if (Blueprints.PrefabPath() == null) return;


            List<PrefabLab.Outcome> outcomes = Walk(ShipLoad.State.Everything);

            int measured = 0;
            int lost = 0;

            foreach (PrefabLab.Outcome outcome in outcomes)
            {
                if (outcome.Skipped != null) continue;

                measured++;
                if (outcome.Lost) lost++;
            }

            Assert.True(measured > 100, "the control measured nothing");
            Assert.True(lost > measured / 2,
                "under full load most prefabs should lose a block; only " + lost + " of "
                + measured + " did, which makes the idle case above unfalsifiable");
        }

        [Fact]

        public void EveryPrefabTheGameShipsCanBeRead()
        {
            if (Blueprints.PrefabPath() == null) return;

            List<string> files = Blueprints.PrefabFiles();
            Assert.True(files.Count > 500,
                "only " + files.Count + " prefab files were found, so this is reading the wrong place");


            List<string> unreadable = new List<string>();
            foreach (string path in files)
            {
                if (Blueprints.Read(path).Count == 0) unreadable.Add(path);
            }

            Assert.True(unreadable.Count == 0,
                "prefab files the parser could not read:\n  "
                + string.Join("\n  ", unreadable.ToArray()));
        }
    }
}

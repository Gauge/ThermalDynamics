using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// That the committed retest set is a set something can actually run.
    ///
    /// <para>
    /// <c>tools/corpus/typical.csv</c> was written by <c>typical.py</c> and then read by nothing for
    /// a while, which is the defect class this repository keeps finding: built, documented, and
    /// reached by nothing (`D2`). These checks are the cheap half of wiring it — they need neither
    /// the corpus opt-in nor a game install, so a set that has gone stale fails in the ordinary
    /// suite rather than eight hours into a walk.
    /// </para>
    /// </summary>
    public class RetestSetTests
    {
        [Fact]

        public void TheRetestSetIsReadableAndCarriesAPathPerShip()
        {
            List<ShipSet.Entry> rows = ShipSet.Read("THERMAL_RETEST", "tools/corpus/typical.csv");
            Assert.True(rows.Count > 0,
                "tools/corpus/typical.csv is missing or empty; rebuild it with typical.py");

            Assert.Equal(40, rows.Count);

            foreach (ShipSet.Entry entry in rows)
            {
                Assert.False(string.IsNullOrEmpty(entry.Name), "a retest row has no ship name");
                Assert.False(string.IsNullOrEmpty(entry.Path),
                    "retest ship '" + entry.Name + "' carries no blueprint path");
            }
        }

        [Fact]

        public void TheRetestSetSpansBothGridSizesAndEveryBand()
        {

            HashSet<string> large = new HashSet<string>(StringComparer.Ordinal);

            HashSet<string> bands = new HashSet<string>(StringComparer.Ordinal);

            foreach (ShipSet.Entry entry in
                ShipSet.Read("THERMAL_RETEST", "tools/corpus/typical.csv"))
            {
                if (entry.Fields.Length < 6) continue;
                large.Add(entry.Fields[3]);
                bands.Add(entry.Fields[3] + "/" + entry.Fields[4]);
            }

            Assert.Equal(2, large.Count);
            Assert.Equal(8, bands.Count);
        }

        [Fact]

        public void EveryScenarioTheRetestAsksForExists()
        {

            HashSet<string> known = new HashSet<string>(StringComparer.Ordinal);
            foreach (Battery.Scenario scenario in Battery.All()) known.Add(scenario.Name);

            string[] wanted =
            {
                "idle", "full-electrical", "burn-forward", "vacuum-sunlit", "recovery",
                "flight-100", "flight-300",
            };

            foreach (string name in wanted)
            {
                Assert.True(known.Contains(name),
                    "the retest asks for scenario '" + name + "' and the battery has no such case");
            }
        }

        [Fact]

        public void ThePreConversionTableHasTheShapeTheOldFileHeld()
        {
            Assert.Equal(ConductanceRetest.PreDefault,
                ReferenceMaterials.MildSteel.Conductivity * ThermalConstants.ConductionScale, 3);

            Assert.Equal(200f / 120f, ConductanceRetest.PreBest / ConductanceRetest.PreDefault, 3);

            Assert.Equal(120f, ConductanceRetest.PreDefault * (2.4f / ThermalConstants.ConductionScale), 3);
            Assert.Equal(200f, ConductanceRetest.PreBest * (2.4f / ThermalConstants.ConductionScale), 3);

            Assert.True(ConductanceRetest.WasBest("Thrust", "LargeBlockLargeThrust"));
            Assert.True(ConductanceRetest.WasBest("Reactor", "LargeBlockLargeGenerator"));
            Assert.True(ConductanceRetest.WasBest("CubeBlock", "Gauge_LG_Radiator"));
            Assert.True(ConductanceRetest.WasBest("UpgradeModule", "Gauge_LG_CoolantPump"));

            Assert.False(ConductanceRetest.WasBest("CubeBlock", "LargeBlockArmorBlock"));
            Assert.False(ConductanceRetest.WasBest("SolarPanel", "LargeBlockSolarPanel"));
        }

        [Fact]

        public void EachArmRewritesOnlyWhatItNames()
        {
            Dictionary<string, ConductanceRetest.World> worlds =
                new Dictionary<string, ConductanceRetest.World>(StringComparer.Ordinal);
            foreach (ConductanceRetest.World world in ConductanceRetest.All())
            {
                worlds[world.Name] = world;
            }

            Assert.Null(worlds["shipped"].Material());


            float armour = Conductivity(worlds["vanilla-flat"], "CubeBlock", "LargeBlockArmorBlock");
            Assert.Equal(ConductanceRetest.Authored(ConductanceRetest.PreDefault), armour, 3);

            Assert.True(float.IsNaN(
                Conductivity(worlds["vanilla-flat"], "Thrust", "LargeBlockLargeThrust")));

            Assert.True(float.IsNaN(
                Conductivity(worlds["thrust-and-reactors"], "CubeBlock", "LargeBlockArmorBlock")));
            Assert.Equal(ConductanceRetest.Authored(ConductanceRetest.PreBest),
                Conductivity(worlds["thrust-and-reactors"], "Thrust", "LargeBlockLargeThrust"), 3);

            Assert.True(float.IsNaN(
                Conductivity(worlds["mod-blocks"], "Thrust", "LargeBlockLargeThrust")));
            Assert.Equal(ConductanceRetest.Authored(ConductanceRetest.PreBest),
                Conductivity(worlds["mod-blocks"], "CubeBlock", "Gauge_LG_Radiator"), 3);

            Assert.Equal(ConductanceRetest.Authored(ConductanceRetest.PreDefault),
                Conductivity(worlds["pre-units"], "CubeBlock", "LargeBlockArmorBlock"), 3);
            Assert.Equal(ConductanceRetest.Authored(ConductanceRetest.PreBest),
                Conductivity(worlds["pre-units"], "Thrust", "LargeBlockLargeThrust"), 3);
        }


        private static float Conductivity(ConductanceRetest.World world, string typeId, string subtype)
        {
            BlockThermalProperties source = new BlockThermalProperties { Conductivity = 12.5f };
            BlockThermalProperties result = world.Material()(typeId, subtype, source);
            return ReferenceEquals(result, source) ? float.NaN : result.Conductivity;
        }
    }
}

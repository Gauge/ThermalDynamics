using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The conclusions the balance pass reached, pinned so they cannot quietly invert.
    ///
    /// These are not assertions about the solver — the physics suite covers that. They are
    /// assertions about the *definitions*: that the radiator still beats the armour it displaces,
    /// that the coolant loop is still the stiffest way to feed a panel, that a block's mass is
    /// still the sum of its components. Every one of them is a statement a design decision was
    /// made on, and every one of them would otherwise be re-derived from memory a year from now.
    ///
    /// Figures come from <see cref="BalanceLab"/>, which reads the shipped XML at run time, so
    /// editing a definition moves these tests. That is the point: a tuning change that inverts a
    /// conclusion should have to say so out loud.
    /// </summary>
    [Trait("speed", "slow")]
    public class BalanceTests
    {

        [Fact]

        public void TheVanillaReferenceStillMatchesTheInstalledGame()
        {
            string content = GameBlocks.ContentPath();
            if (content == null) return;

            Dictionary<string, float> componentMass = new Dictionary<string, float>();
            foreach (XElement component in XDocument.Load(Path.Combine(content, "Components.sbc"))
                         .Descendants("Component"))
            {
                XElement id = component.Element("Id");
                if (id == null) continue;
                string subtype = (string)id.Element("SubtypeId");
                float mass;
                if (subtype != null && float.TryParse((string)component.Element("Mass"),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
                {
                    componentMass[subtype] = mass;
                }
            }

            foreach (KeyValuePair<string, float> entry in Vanilla.ComponentMasses)
            {
                float actual;
                Assert.True(componentMass.TryGetValue(entry.Key, out actual),
                    entry.Key + " is no longer a component in the installed game");
                Assert.True(Math.Abs(actual - entry.Value) < 0.01f,
                    entry.Key + " mass is " + actual + " in game, " + entry.Value + " in Vanilla.cs");
            }

            Dictionary<string, XElement> blocks = new Dictionary<string, XElement>();
            foreach (string file in Directory.GetFiles(Path.Combine(content, "CubeBlocks"), "*.sbc"))
            {
                XDocument document;
                try { document = XDocument.Load(file); }
                catch (Exception) { continue; }

                foreach (XElement definition in document.Descendants("Definition"))
                {
                    XElement id = definition.Element("Id");
                    if (id == null) continue;

                    string subtype = (string)id.Element("SubtypeId");
                    string type = (string)id.Element("TypeId");
                    if (subtype == null || type == null) continue;


                    string key = Key(type, subtype);
                    if (!blocks.ContainsKey(key)) blocks[key] = definition;
                }
            }

            foreach (Vanilla.Block block in Vanilla.Reference)
            {
                XElement definition;
                Assert.True(blocks.TryGetValue(Key(block.TypeId, block.Subtype), out definition),
                    Name(block) + " is no longer a block in the installed game");

                float mass = 0f;
                foreach (XElement component in definition.Descendants("Component"))
                {
                    int count;
                    float each;
                    if (int.TryParse((string)component.Attribute("Count"), out count)
                        && componentMass.TryGetValue((string)component.Attribute("Subtype") ?? "", out each))
                    {
                        mass += each * count;
                    }
                }

                Assert.True(Math.Abs(mass - block.Mass) < 1f,
                    Name(block) + " weighs " + mass + " kg in game, " + block.Mass + " kg in Vanilla.cs");
            }
        }


        private static string Key(string typeId, string subtypeId)
        {
            string type = typeId ?? "";
            if (type.StartsWith("MyObjectBuilder_")) type = type.Substring("MyObjectBuilder_".Length);
            return type + "/" + (subtypeId ?? "");
        }


        private static string Name(Vanilla.Block block)
        {
            return block.Subtype.Length > 0 ? block.Subtype : block.TypeId + " (no subtype)";
        }


        [Fact]

        public void TheVanillaComponentListsStillMatchTheInstalledGame()
        {
            if (!GameBlocks.IsInstalled) return;


            List<string> wrong = new List<string>();

            foreach (Vanilla.Block reference in Vanilla.Reference)
            {
                GameBlocks.Definition installed = null;
                foreach (GameBlocks.Definition block in GameBlocks.All())
                {
                    if (Key(block.TypeId, block.SubtypeId) == Key(reference.TypeId, reference.Subtype))
                    {
                        installed = block;
                        break;
                    }
                }

                if (installed == null)
                {
                    wrong.Add(Name(reference) + " is no longer a block in the installed game");
                    continue;
                }

                float transcribed = 0f;
                foreach (BlockComponent line in reference.Components) transcribed += line.Mass;

                if (Math.Abs(transcribed - installed.Mass) > 0.5f)
                {
                    wrong.Add(Name(reference) + " components weigh " + transcribed
                        + " in Vanilla.cs and " + installed.Mass + " in game");
                }
            }

            Assert.Empty(wrong);
        }

        [Fact]

        public void EveryShippedBlockIsPricedAndWeighsSomething()
        {
            foreach (string subtype in ShippedBlocks.Subtypes())
            {
                ShippedBlocks.Definition definition = ShippedBlocks.Get(subtype);

                Assert.True(definition.Components.Count > 0, subtype + " lists no components");
                Assert.True(definition.Mass > 0f, subtype + " weighs nothing");

                foreach (KeyValuePair<string, int> component in definition.Components)
                {
                    Assert.True(Vanilla.ComponentMasses.ContainsKey(component.Key),
                        subtype + " is built from " + component.Key
                        + ", which Vanilla.ComponentMasses does not price");
                }
            }
        }

        [Fact]

        public void EveryShippedBlockGetsItsOwnThermalProperties()
        {
            foreach (string subtype in ShippedBlocks.Subtypes())
            {
                Assert.True(ShippedBlocks.Get(subtype).HasOwnThermalEntry,
                    subtype + " has no entry of its own in Cubes.xml and fell through to a default");
            }
        }

        [Fact]

        public void EveryCoolantBlockHasPlumbingAndNothingElseDoes()
        {
            foreach (string subtype in ShippedBlocks.Subtypes())
            {
                ShippedBlocks.Definition definition = ShippedBlocks.Get(subtype);
                bool shouldPlumb = subtype.Contains("CoolantPipe") || subtype.Contains("CoolantPump");
                bool doesPlumb = ThermalCoolantShapes.Get(subtype, definition.Size) != null;

                Assert.True(shouldPlumb == doesPlumb,
                    subtype + (shouldPlumb ? " is a coolant block with no plumbing"
                                           : " is not a coolant block but declares plumbing"));
            }
        }

        [Theory]
        [InlineData("SurfaceAreaScaler", "ExposedSurfaceMultiplier")]
        [InlineData("CriticalTemperatureScaler", "OverheatDamagePerKelvin")]

        public void TheRetiredPropertyNamesAreStillRead(string legacy, string current)
        {
            string cubes = File.ReadAllText(Path.Combine(ShippedBlocks.DataRoot(), "Cubes.xml"));

            Assert.Contains(current, cubes);
            Assert.DoesNotContain(legacy + "\"", cubes);

            BlockThermalProperties viaCurrent = ShippedBlocks.Get("Gauge_LG_Radiator").Thermal;
            Assert.True(viaCurrent.ExposedSurfaceMultiplier > 1f,
                "the radiator lost its surface multiplier, so the rename dropped a value");
        }


        [Fact]

        public void TheRadiatorBeatsTheArmourItDisplaces()
        {
            List<BalanceLab.DeliveredRow> rows = BalanceLab.Delivered();

            BalanceLab.DeliveredRow radiator = rows.First(
                r => r.Label.Contains("radiators") && r.Label.StartsWith("200") && r.Count == 1);
            BalanceLab.DeliveredRow armour = rows.First(
                r => r.Label.Contains("armour") && r.Label.StartsWith("200") && r.Count == 1);

            Assert.True(radiator.KelvinSaved > armour.KelvinSaved,
                "the radiator saved " + radiator.KelvinSaved + " K against the armour slab's "
                + armour.KelvinSaved + " K");

            Assert.True(radiator.KelvinPerTonne > armour.KelvinPerTonne * 10f,
                "the radiator is only " + (radiator.KelvinPerTonne / armour.KelvinPerTonne)
                + "x better per tonne than plain armour");
        }

        [Fact]

        public void PlumbingAPanelBeatsEveryDialButAMultipliedArea()
        {
            List<BalanceLab.SensitivityRow> rows = BalanceLab.Sensitivity();

            BalanceLab.SensitivityRow coolant = rows.First(r => r.Dial == "coolant sink");

            Assert.True(coolant.KelvinVersusShipped > 0f,
                "plumbing the panel rather than bolting it bought " + coolant.KelvinVersusShipped
                + " K, so the loop has stopped being the better way to feed a panel and"
                + " blocks.md's 'plumb it, do not bolt it' needs rewriting");

            float emissivity = rows.First(r => r.Dial == "Emissivity").KelvinVersusShipped;
            Assert.True(coolant.KelvinVersusShipped > emissivity,
                "a coolant sink bought " + coolant.KelvinVersusShipped + " K against emissivity's "
                + emissivity + " K");

            List<BalanceLab.SensitivityRow> area = rows
                .Where(r => r.Dial == "ExposedSurfaceMultiplier")
                .OrderBy(r => r.KelvinVersusShipped)
                .ToList();

            Assert.True(area.Count >= 3, "the area sweep lost its rows");
            Assert.True(coolant.KelvinVersusShipped > area[0].KelvinVersusShipped,
                "doubling the panel's area bought " + area[0].KelvinVersusShipped
                + " K against plumbing it at " + coolant.KelvinVersusShipped
                + " K, so the cheapest surface dial has passed the loop");

            Assert.True(coolant.JointWattsPerKelvin > 500f,
                "a sink face now couples at only " + coolant.JointWattsPerKelvin + " W/K");
        }

        [Fact]

        public void ASinkFaceConductsSeveralTimesHarderThanABoltJoint()
        {
            List<BalanceLab.SensitivityRow> rows = BalanceLab.Sensitivity();

            BalanceLab.SensitivityRow bolted = rows.First(r => r.Dial == "(shipped)");
            BalanceLab.SensitivityRow coolant = rows.First(r => r.Dial == "coolant sink");

            Assert.InRange(coolant.JointWattsPerKelvin / bolted.JointWattsPerKelvin, 3f, 8f);

            Assert.InRange(
                bolted.JointWattsPerKelvin * (2.4f / ThermalConstants.ConductionScale),
                200f, 400f);
        }

        [Fact]

        public void SurfaceDialsGiveDiminishingReturns()
        {
            List<BalanceLab.SensitivityRow> rows = BalanceLab.Sensitivity()
                .Where(r => r.Dial == "ExposedSurfaceMultiplier")
                .OrderBy(r => r.KelvinVersusShipped)
                .ToList();

            Assert.True(rows.Count >= 3, "the area sweep lost its rows");

            for (int i = 1; i < rows.Count; i++)
            {
                float previous = rows[i - 1].KelvinVersusShipped;
                float current = rows[i].KelvinVersusShipped;
                Assert.True(current > previous, "the sweep is not monotonic");
                Assert.True(current < previous * 2f,
                    "doubling the area scaler bought " + (current - previous)
                    + " K on top of " + previous + " K, which is more than proportional");
            }
        }

        [Fact]

        public void LongerRingsDeliverColderBlocks()
        {
            List<BalanceLab.LoopRow> rows = BalanceLab.Loops();
            Assert.True(rows.Count >= 2, "the ring sweep lost its rows");

            for (int i = 1; i < rows.Count; i++)
            {
                Assert.True(rows[i].Pipes > rows[i - 1].Pipes, "the sweep is not ordered by size");
                Assert.True(rows[i].CouplingWattsPerKelvin > rows[i - 1].CouplingWattsPerKelvin,
                    "a longer ring stopped coupling harder");
                Assert.True(rows[i].SettledKelvin < rows[i - 1].SettledKelvin,
                    "a " + rows[i].Pipes + "-pipe ring settled at " + rows[i].SettledKelvin
                    + " K against a " + rows[i - 1].Pipes + "-pipe ring's " + rows[i - 1].SettledKelvin + " K");
            }
        }

        [Fact]

        public void TheHeatPumpPassesThroughAllThreeOfItsLimits()
        {
            List<BalanceLab.PumpRow> rows = BalanceLab.Pump();

            ThermalSettings settings = new ThermalSettings();


            HashSet<string> seen = new HashSet<string>();
            foreach (BalanceLab.PumpRow row in rows)
            {
                seen.Add(row.Binding);
                Assert.True(row.Coefficient <= settings.HeatPumpMaxCoefficient + 0.001f,
                    "coefficient " + row.Coefficient + " exceeded the cap");
                Assert.True(row.DrawnWatts <= 20000f + 0.5f,
                    "the pump drew " + row.DrawnWatts + " W, above its rating");
            }

            Assert.Contains("coefficient cap", seen);
            Assert.Contains("rating", seen);
            Assert.Contains("carnot", seen);
        }

        [Fact]

        public void OnlyTheRadiatorClaimsExtraSurface()
        {
            foreach (BalanceLab.BlockRow row in BalanceLab.Blocks())
            {
                bool radiator = row.Kind == "radiator";
                Assert.True(radiator == (row.ExposedSurfaceMultiplier > 1f),
                    row.Subtype + " has area scaler " + row.ExposedSurfaceMultiplier
                    + (radiator ? " and should claim extra surface" : " and should not"));
            }
        }
    }
}

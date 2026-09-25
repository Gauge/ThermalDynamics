using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Cross-checks the shipped XML against itself, and against the code that binds to it. These
    /// are the failures a green solver suite cannot see: the simulation is correct, and the numbers
    /// it is handed are not the ones the definitions author — or the block the definitions author
    /// has no logic attached to it at all.
    ///
    /// The defect that prompted this: every one of the mod's own blocks declared its thermal
    /// properties under <c>&lt;TypeId&gt;CubeBlocks&lt;/TypeId&gt;</c>, which is not an object
    /// builder type. Definition Extensions therefore matched none of them, every block fell
    /// through to <c>DefaultThermodynamics</c>, and a live dump showed the radiator running at
    /// emissivity 0.125 with no area bonus instead of the authored 0.35 and 1.25 — the two
    /// properties the block exists for.
    /// </summary>
    public class ShippedDefinitionTests
    {
        const string CUBES_DEFINITION_PATH = "Cubes.xml";

        private static XDocument LoadDefinition(string path) => XDocument.Load(Path.Combine(ShippedBlocks.DataRoot(), path));


        private static Dictionary<string, string> ShippedBlockTypes()
        {
            Dictionary<string, string> types = new Dictionary<string, string>();
            string folder = Path.Combine(ShippedBlocks.DataRoot(), "CubeBlocks");

            foreach (string file in Directory.GetFiles(folder, "*.sbc"))
            {
                XDocument document = XDocument.Load(file);
                foreach (XElement definition in document.Descendants("Definition"))
                {
                    XElement id = definition.Element("Id");
                    if (id == null) continue;

                    XElement type = id.Element("TypeId");
                    XElement subtype = id.Element("SubtypeId");
                    if (type == null || subtype == null) continue;

                    types[subtype.Value.Trim()] = type.Value.Trim();
                }
            }

            return types;
        }


        private static List<KeyValuePair<string, string>> CubesEntries()
        {
            List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();

            foreach (XElement definition in LoadDefinition(CUBES_DEFINITION_PATH).Descendants("Definition"))
            {
                XElement id = definition.Element("Id");
                if (id == null) continue;

                XElement type = id.Element("TypeId");
                XElement subtype = id.Element("SubtypeId");
                if (type == null || subtype == null) continue;

                entries.Add(new KeyValuePair<string, string>(type.Value.Trim(), subtype.Value.Trim()));
            }

            return entries;
        }

        [Fact]

        public void EveryShippedBlockDeclaresItsThermalPropertiesUnderItsOwnTypeId()
        {

            Dictionary<string, string> shipped = ShippedBlockTypes();

            List<string> wrong = new List<string>();

            foreach (KeyValuePair<string, string> entry in CubesEntries())
            {
                string expected;
                if (!shipped.TryGetValue(entry.Value, out expected)) continue;

                if (entry.Key != expected)
                {
                    wrong.Add(entry.Value + " authored as <TypeId>" + entry.Key +
                        "</TypeId> but the block is a " + expected);
                }
            }

            Assert.Empty(wrong);
        }

        [Fact]

        public void EveryShippedBlockHasThermalPropertiesOfItsOwn()
        {

            Dictionary<string, string> shipped = ShippedBlockTypes();

            HashSet<string> authored = new HashSet<string>();

            foreach (KeyValuePair<string, string> entry in CubesEntries())
            {
                authored.Add(entry.Value);
            }


            List<string> missing = new List<string>();
            foreach (KeyValuePair<string, string> block in shipped)
            {
                if (!authored.Contains(block.Key)) missing.Add(block.Key);
            }

            Assert.Empty(missing);
        }

        [Fact]

        public void TheRadiatorShedsFasterThanTheDefaultBlock()
        {

            Dictionary<string, double> fallback = PropertiesOf("DefaultThermodynamics");

            foreach (string subtype in new string[] { "Gauge_LG_Radiator", "Gauge_SG_Radiator" })
            {

                Dictionary<string, double> radiator = PropertiesOf(subtype);

                Assert.True(radiator["Emissivity"] > fallback["Emissivity"],
                    subtype + " emissivity " + radiator["Emissivity"] + " does not beat the default");
                Assert.True(radiator["ExposedSurfaceMultiplier"] > fallback["ExposedSurfaceMultiplier"],
                    subtype + " area scaler " + radiator["ExposedSurfaceMultiplier"] + " does not beat the default");
            }
        }

        [Fact]

        public void TheHeatPumpChargesItsWorkOnlyOnce()
        {
            foreach (string subtype in new string[] { "Gauge_LG_HeatPump", "Gauge_SG_HeatPump" })
            {

                Dictionary<string, double> pump = PropertiesOf(subtype);

                Assert.Equal(0d, pump["ProducerWasteEnergy"]);
                Assert.Equal(0d, pump["ConsumerWasteEnergy"]);
            }
        }

        private static readonly string[] FunctionProperties =
        {
            "ProducerWasteEnergy",
            "ConsumerWasteEnergy",
            "ExposedSurfaceMultiplier",
            "OverheatDamagePerKelvin",
        };

        private static readonly string[] RequiredProperties =
        {
            "Conductivity",
            "SpecificHeat",
            "Emissivity",
            "ExposedSurfaceMultiplier",
            "ProducerWasteEnergy",
            "ConsumerWasteEnergy",
            "CriticalTemperature",
            "OverheatDamagePerKelvin",

        };


        private static Dictionary<string, string> BoundSubtypes()
        {
            Dictionary<string, string> bound = new Dictionary<string, string>();
            string scripts = Path.Combine(ShippedBlocks.ModRoot());

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);

                foreach (Match descriptor in Regex.Matches(
                    source, @"MyEntityComponentDescriptor\s*\((.*?)\)\s*\]", RegexOptions.Singleline))
                {
                    foreach (Match subtype in Regex.Matches(descriptor.Groups[1].Value, "\"([^\"]+)\""))
                    {
                        bound[subtype.Groups[1].Value] = Path.GetFileName(file);
                    }
                }
            }

            return bound;
        }

        [Fact]

        public void EveryBlockLogicComponentIsBoundToASubtypeThatShips()
        {

            Dictionary<string, string> shipped = ShippedBlockTypes();

            List<string> dangling = new List<string>();

            foreach (KeyValuePair<string, string> entry in BoundSubtypes())
            {
                if (!shipped.ContainsKey(entry.Key))
                {
                    dangling.Add(entry.Value + " binds logic to " + entry.Key
                        + ", which no .sbc in Data/CubeBlocks defines");
                }
            }

            dangling.Sort(StringComparer.Ordinal);
            Assert.True(dangling.Count == 0, string.Join("\n  ", dangling.ToArray()));
        }

        [Fact]

        public void EveryUpgradeModuleTheModShipsHasLogicBoundToIt()
        {

            Dictionary<string, string> bound = BoundSubtypes();

            List<string> inert = new List<string>();
            int modules = 0;

            foreach (KeyValuePair<string, string> block in ShippedBlockTypes())
            {
                if (block.Value != "UpgradeModule") continue;

                modules++;
                if (!bound.ContainsKey(block.Key))
                {
                    inert.Add(block.Key + " is an UpgradeModule with no MyEntityComponentDescriptor"
                        + " naming it, so it builds and does nothing");
                }
            }

            Assert.True(modules >= 6,
                "only " + modules + " upgrade modules were found, so this test is reading the wrong"
                + " thing and would pass over a tree with none");

            inert.Sort(StringComparer.Ordinal);
            Assert.True(inert.Count == 0, string.Join("\n  ", inert.ToArray()));
        }

        [Fact]

        public void EveryEntryInCubesDeclaresEveryPropertyTheGameReads()
        {

            List<string> incomplete = new List<string>();

            foreach (XElement definition in LoadDefinition(CUBES_DEFINITION_PATH).Descendants("Definition"))
            {
                XElement id = definition.Element("Id");
                if (id == null) continue;

                string subtype = (string)id.Element("SubtypeId");
                string type = (string)id.Element("TypeId");
                string name = type + "/" + subtype;

                bool typeEntry = subtype == "DefaultThermodynamics" && type != "EnvironmentDefinition";
                string[] required = typeEntry ? FunctionProperties : RequiredProperties;

                foreach (XElement group in definition.Descendants("Group"))
                {
                    XAttribute groupName = group.Attribute("Name");
                    if (groupName == null || groupName.Value != "ThermalBlockProperties") continue;


                    HashSet<string> declared = new HashSet<string>();
                    foreach (XElement value in group.Elements("Decimal"))
                    {
                        XAttribute key = value.Attribute("Name");
                        if (key != null) declared.Add(key.Value);
                    }

                    foreach (string property in required)
                    {
                        if (!declared.Contains(property))
                        {
                            incomplete.Add(name + " omits " + property + ", which the game reads as 0");
                        }
                    }

                }
            }

            Assert.Empty(incomplete);
        }

        [Fact]

        public void TheTypeEntriesStillCarryTheNumbersTheCodeTableHeld()
        {
            Dictionary<string, float[]> expected = new Dictionary<string, float[]>
            {
                { "Reactor", new[] { 0.01f, 0.01f, 1f, 0.25f } },
                { "HydrogenEngine", new[] { 0.60f, 0.05f, 1f, 0.5f } },
                { "BatteryBlock", new[] { 0.03f, 0.03f, 1f, 1f } },
                { "JumpDrive", new[] { 0f, 0.2f, 1f, 2f } },
                { "Thrust", new[] { 0f, 0.25f, 1.5f, 1f } },
                { "InteriorLight", new[] { 0f, 1f, 1f, 1f } },
                { "ReflectorLight", new[] { 0f, 0.9f, 1f, 1f } },
                { "Gyro", new[] { 0f, 0.15f, 1f, 1f } },
                { "Warhead", new[] { 0f, 0.05f, 1f, 4f } },
                { "HeatVentBlock", new[] { 0f, 0.3f, 3f, 1f } },
            };

            foreach (KeyValuePair<string, float[]> entry in expected)
            {
                ShippedBlocks.Function f = ShippedBlocks.FunctionOf(entry.Key);
                Assert.Equal(entry.Value[0], f.ProducerWasteEnergy, 4);
                Assert.Equal(entry.Value[1], f.ConsumerWasteEnergy, 4);
                Assert.Equal(entry.Value[2], f.ExposedSurfaceMultiplier, 4);
                Assert.Equal(entry.Value[3], f.OverheatDamagePerKelvin, 4);
            }

            Assert.Equal(95, ShippedBlocks.FunctionTypes().Count);
        }

        [Fact]

        public void EveryEntryInCubesDeclaresTheBoolThatMakesItVisible()
        {

            List<string> invisible = new List<string>();

            foreach (XElement definition in LoadDefinition(CUBES_DEFINITION_PATH).Descendants("Definition"))
            {
                XElement id = definition.Element("Id");
                if (id == null) continue;

                foreach (XElement group in definition.Descendants("Group"))
                {
                    XAttribute groupName = group.Attribute("Name");
                    if (groupName == null || groupName.Value != "ThermalBlockProperties") continue;

                    bool found = false;
                    foreach (XElement value in group.Elements("Bool"))
                    {
                        XAttribute key = value.Attribute("Name");
                        if (key == null) continue;
                        if (key.Value == "ExcludeFromSimulation" || key.Value == "IgnoreThermals") found = true;
                    }

                    if (!found)
                    {
                        invisible.Add((string)id.Element("TypeId") + "/" + (string)id.Element("SubtypeId")
                            + " omits ExcludeFromSimulation, so the whole entry is skipped");
                    }
                }
            }

            Assert.Empty(invisible);
        }


        private static Dictionary<string, double> PropertiesOf(string subtype)
        {
            foreach (XElement definition in LoadDefinition(CUBES_DEFINITION_PATH).Descendants("Definition"))
            {
                XElement id = definition.Element("Id");
                if (id == null) continue;

                XElement name = id.Element("SubtypeId");
                if (name == null || name.Value.Trim() != subtype) continue;

                Dictionary<string, double> values = new Dictionary<string, double>();
                foreach (XElement group in definition.Descendants("Group"))
                {
                    XAttribute groupName = group.Attribute("Name");
                    if (groupName == null || groupName.Value != "ThermalBlockProperties") continue;

                    foreach (XElement value in group.Elements("Decimal"))
                    {
                        XAttribute key = value.Attribute("Name");
                        XAttribute number = value.Attribute("Value");
                        if (key == null || number == null) continue;

                        values[key.Value] = double.Parse(number.Value, CultureInfo.InvariantCulture);
                    }
                }

                if (values.Count > 0) return values;
            }

            throw new InvalidOperationException("No ThermalBlockProperties for " + subtype);
        }
    }
}

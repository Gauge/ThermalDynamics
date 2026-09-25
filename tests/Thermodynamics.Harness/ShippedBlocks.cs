using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class ShippedBlocks
    {
        public class Definition
        {
            public string Subtype;
            public string TypeId;
            public bool Large;
            public Vector3I Size;

            public float Mass;

            public readonly List<KeyValuePair<string, int>> Components = new List<KeyValuePair<string, int>>();

            public int Pcu;

            public float BuildSeconds;

            public readonly bool[] MountFaces = new bool[Face.Count];

            public BlockThermalProperties Thermal;

            public bool HasOwnThermalEntry;

            public int CellCount
            {
                get { return Size.X * Size.Y * Size.Z; }
            }

            public float GridSize
            {
                get { return Large ? Catalog.LargeGridSize : Catalog.SmallGridSize; }
            }


            public float CapacityJoulesPerKelvin(float heatTimeScale)
            {
                float scale = heatTimeScale > 0f ? heatTimeScale : 1f;
                return Mass * Thermal.SpecificHeat / scale;
            }


            public override string ToString()
            {
                return Subtype;
            }
        }


        private static readonly object Gate = new object();
        private static Dictionary<string, Definition> cache;
        private static Dictionary<string, BlockThermalProperties> byTypeCache;
        private static BlockThermalProperties fallbackCache;
        private static string repoRoot;


        public static string RepoRoot()
        {
            lock (Gate)
            {
                if (repoRoot != null) return repoRoot;


                repoRoot = Above(AppContext.BaseDirectory) ?? Above(SourceDirectory());
                if (repoRoot != null) return repoRoot;

                throw new InvalidOperationException(
                    "Could not find the repository root from " + AppContext.BaseDirectory

                    + " or from " + SourceDirectory());
            }
        }


        private static string Above(string start)
        {
            if (string.IsNullOrEmpty(start)) return null;


            DirectoryInfo directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Thermodynamics.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            return null;
        }


        private static string SourceDirectory([CallerFilePath] string file = "")
        {
            return string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);
        }


        public static string DataRoot()
        {
            return Path.Combine(ContentRoot(), "Data");
        }
        

        public static string ContentRoot()
        {
            return Path.Combine(ModRoot(), "Content");
        }
        

        public static string ModRoot()
        {
            return Path.Combine(RepoRoot(), "Thermodynamics");
        }


        public static Dictionary<string, Definition> All()
        {
            lock (Gate)
            {
                if (cache != null) return cache;

                cache = Load();
                return cache;
            }
        }


        public static Definition Get(string subtype)
        {
            Definition definition;
            if (!All().TryGetValue(subtype, out definition))
            {
                throw new KeyNotFoundException("No shipped block definition for " + subtype);
            }
            return definition;
        }


        public static List<string> Subtypes()
        {

            List<string> names = new List<string>(All().Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }



        public static BlockModel Model(string subtype)
        {

            Definition definition = Get(subtype);
            BlockModel model = BlockModel.Solid(subtype, definition.Size, definition.Mass, definition.Thermal);

            bool everyFace = true;
            for (int face = 0; face < Face.Count; face++)
            {
                if (!definition.MountFaces[face]) everyFace = false;
            }

            if (!everyFace)
            {
                foreach (Vector3I cell in model.LocalCells())
                {
                    int state = CellSurface.SelfAirtightMask;
                    for (int face = 0; face < Face.Count; face++)
                    {
                        state = CellSurface.WithSelfMount(state, face, definition.MountFaces[face]);
                    }
                    model.SetLocalSurface(cell, state);
                }
            }

            CoolantShape coolant = ThermalCoolantShapes.Get(subtype, definition.Size);
            if (coolant != null) model.WithCoolant(coolant);

            HeatPumpShape pump = ThermalHeatPumpShapes.Get(subtype, definition.Size);
            if (pump != null) model.WithHeatPump(pump);

            return model;
        }



        private static Dictionary<string, Definition> Load()
        {
            Dictionary<string, Definition> blocks = new Dictionary<string, Definition>(StringComparer.Ordinal);
            string folder = Path.Combine(DataRoot(), "CubeBlocks");

            foreach (string file in Directory.GetFiles(folder, "*.sbc"))
            {
                XDocument document = XDocument.Load(file);
                foreach (XElement element in document.Descendants("Definition"))
                {

                    Definition definition = ParseBlock(element);
                    if (definition != null) blocks[definition.Subtype] = definition;
                }
            }

            ApplyThermal(blocks);
            return blocks;
        }


        private static Definition ParseBlock(XElement element)
        {
            XElement id = element.Element("Id");
            if (id == null) return null;

            XElement subtype = id.Element("SubtypeId");
            XElement type = id.Element("TypeId");
            if (subtype == null || type == null) return null;

            Definition definition = new Definition
            {
                Subtype = subtype.Value.Trim(),
                TypeId = type.Value.Trim(),
                Large = (string)element.Element("CubeSize") == "Large",

                Size = ParseSize(element.Element("Size")),

                Pcu = ParseInt(element.Element("PCU")),

                BuildSeconds = ParseFloat(element.Element("BuildTimeSeconds")),
            };

            XElement components = element.Element("Components");
            if (components != null)
            {
                foreach (XElement component in components.Elements("Component"))
                {
                    string name = (string)component.Attribute("Subtype");

                    int count = ParseInt(component.Attribute("Count"));
                    if (name != null && count > 0)
                    {
                        definition.Components.Add(new KeyValuePair<string, int>(name, count));
                    }
                }
            }
            definition.Mass = Vanilla.MassOf(definition.Components);

            XElement mounts = element.Element("MountPoints");
            if (mounts != null)
            {
                foreach (XElement mount in mounts.Elements("MountPoint"))
                {

                    int face = FaceOf((string)mount.Attribute("Side"));
                    if (face >= 0) definition.MountFaces[face] = true;
                }
            }

            return definition;
        }

        public struct Function
        {
            public float ProducerWasteEnergy;
            public float ConsumerWasteEnergy;
            public float ExposedSurfaceMultiplier;
            public float OverheatDamagePerKelvin;
        }

        public static readonly Function Ordinary = new Function
        {
            ProducerWasteEnergy = 0.05f,
            ConsumerWasteEnergy = 0.05f,
            ExposedSurfaceMultiplier = 1f,
            OverheatDamagePerKelvin = 1f,
        };

        private static Dictionary<string, Function> functionCache;


        public static Function FunctionOf(string typeId)
        {
            All();
            Function function;
            return typeId != null && functionCache.TryGetValue(typeId, out function)
                ? function : Ordinary;
        }


        public static ICollection<string> FunctionTypes()
        {
            All();
            return functionCache.Keys;
        }


        public static BlockThermalProperties DeriveWithFunction(IList<BlockComponent> components,
            string typeId)
        {

            return DeriveWithFunction(components, typeId, 0f);
        }


        public static BlockThermalProperties DeriveWithFunction(IList<BlockComponent> components,
            string typeId, float statedEfficiency)
        {
            BlockThermalProperties properties = BlockThermalDerivation.Derive(components);

            Function function = FunctionOf(typeId);
            properties.ProducerWasteEnergy = function.ProducerWasteEnergy;
            properties.ConsumerWasteEnergy = function.ConsumerWasteEnergy;
            properties.ExposedSurfaceMultiplier = function.ExposedSurfaceMultiplier;
            properties.OverheatDamagePerKelvin = function.OverheatDamagePerKelvin;

            float stated = BlockThermalDerivation.WasteFromEfficiency(statedEfficiency);
            if (stated >= 0f) properties.ConsumerWasteEnergy = stated;

            return properties.Clamp();
        }


        private static void ApplyThermal(Dictionary<string, Definition> blocks)
        {
            Dictionary<string, BlockThermalProperties> bySubtype =
                new Dictionary<string, BlockThermalProperties>(StringComparer.Ordinal);
            Dictionary<string, BlockThermalProperties> byType =
                new Dictionary<string, BlockThermalProperties>(StringComparer.Ordinal);
            Dictionary<string, Function> functions =
                new Dictionary<string, Function>(StringComparer.Ordinal);
            BlockThermalProperties fallback = null;

            XDocument cubes = XDocument.Load(Path.Combine(DataRoot(), "Cubes.xml"));
            foreach (XElement element in cubes.Descendants("Definition"))
            {
                XElement id = element.Element("Id");
                if (id == null) continue;

                string type = ((string)id.Element("TypeId") ?? "").Trim();
                string subtype = ((string)id.Element("SubtypeId") ?? "").Trim();

                BlockThermalProperties properties = ParseThermal(element);
                if (properties == null) continue;

                if (subtype == "DefaultThermodynamics")
                {
                    if (type == "EnvironmentDefinition") fallback = properties;
                    else
                    {
                        byType[type] = properties;
                        functions[type] = new Function
                        {
                            ProducerWasteEnergy = properties.ProducerWasteEnergy,
                            ConsumerWasteEnergy = properties.ConsumerWasteEnergy,
                            ExposedSurfaceMultiplier = properties.ExposedSurfaceMultiplier,
                            OverheatDamagePerKelvin = properties.OverheatDamagePerKelvin,
                        };
                    }
                }
                else
                {
                    bySubtype[subtype] = properties;
                }
            }

            if (fallback == null) fallback = BlockThermalProperties.Default();
            byTypeCache = byType;
            functionCache = functions;
            fallbackCache = fallback;

            foreach (Definition definition in blocks.Values)
            {
                BlockThermalProperties properties;
                if (bySubtype.TryGetValue(definition.Subtype, out properties))
                {
                    definition.Thermal = properties;
                    definition.HasOwnThermalEntry = true;
                }
                else if (byType.TryGetValue(definition.TypeId, out properties))
                {
                    definition.Thermal = properties;
                }
                else
                {
                    definition.Thermal = fallback;
                }
            }
        }


        public static BlockThermalProperties ParseThermalForTest(XElement definition)
        {

            return ParseThermal(definition);
        }


        private static BlockThermalProperties ParseThermal(XElement definition)
        {
            XElement group = null;
            foreach (XElement candidate in definition.Descendants("Group"))
            {
                if ((string)candidate.Attribute("Name") == "ThermalBlockProperties") group = candidate;
            }
            if (group == null) return null;

            BlockThermalProperties properties = BlockThermalProperties.Default();
            foreach (XElement value in group.Elements())
            {
                string name = (string)value.Attribute("Name");
                string raw = (string)value.Attribute("Value");
                if (name == null || raw == null) continue;

                if (name == "ExcludeFromSimulation" || name == "IgnoreThermals")
                {
                    bool excluded;
                    if (bool.TryParse(raw, out excluded)) properties.ExcludeFromSimulation = excluded;
                    continue;
                }

                float number;
                if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) continue;

                switch (name)
                {
                    case "Conductivity": properties.Conductivity = number; break;
                    case "SpecificHeat": properties.SpecificHeat = number; break;
                    case "Emissivity": properties.Emissivity = number; break;
                    case "SolarAbsorptivity": properties.SolarAbsorptivity = number; break;
                    case "ExposedSurfaceMultiplier":
                    case "SurfaceAreaScaler": properties.ExposedSurfaceMultiplier = number; break;
                    case "ProducerWasteEnergy": properties.ProducerWasteEnergy = number; break;
                    case "ConsumerWasteEnergy": properties.ConsumerWasteEnergy = number; break;
                    case "HeatSourceWatts": properties.HeatSourceWatts = number; break;
                    case "CriticalTemperature": properties.CriticalTemperature = number; break;
                    case "OverheatDamagePerKelvin":
                    case "CriticalTemperatureScaler": properties.OverheatDamagePerKelvin = number; break;
                }
            }
            return properties;
        }



        private static Vector3I ParseSize(XElement size)
        {
            if (size == null) return Vector3I.One;
            return new Vector3I(
                Math.Max(1, ParseInt(size.Attribute("x"))),
                Math.Max(1, ParseInt(size.Attribute("y"))),
                Math.Max(1, ParseInt(size.Attribute("z"))));
        }


        private static int ParseInt(XAttribute attribute)
        {
            int value;
            return attribute != null
                && int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value : 0;
        }


        private static int ParseInt(XElement element)
        {
            int value;
            return element != null
                && int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value : 0;
        }


        private static float ParseFloat(XElement element)
        {
            float value;
            return element != null
                && float.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value : 0f;
        }


        private static int FaceOf(string side)
        {
            switch (side)
            {
                case "Front": return Face.Forward;
                case "Back": return Face.Backward;
                case "Left": return Face.Left;
                case "Right": return Face.Right;
                case "Top": return Face.Up;
                case "Bottom": return Face.Down;
                default: return -1;
            }
        }
    }
}

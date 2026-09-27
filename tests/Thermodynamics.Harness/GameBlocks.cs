using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class GameBlocks
    {
        public class Definition
        {
            public string TypeId;
            public string SubtypeId;
            public bool Large;

            public Vector3I Size = Vector3I.One;

            public readonly bool[] MountFaces = new bool[Face.Count];

            public float JumpEnergyJoules;

            public float PowerEfficiency;

            public bool HasDeclaredMounts;

            public bool? Airtight;

            public float PowerOutputWatts;

            public float PowerDrawWatts;

            public float ThrustNewtons;
            public float MinPlanetaryInfluence;
            public float MaxPlanetaryInfluence = 1f;
            public float EffectivenessAtMinInfluence = 1f;
            public float EffectivenessAtMaxInfluence = 1f;
            public bool NeedsAtmosphereForInfluence;

            /// <summary>Rated force at the engine's relative air density, with unlimited fuel/power and multiplier one.</summary>
            public float EffectiveThrust(float planetaryInfluence, bool inAtmosphere)
            {
                if (float.IsNaN(planetaryInfluence) || float.IsInfinity(planetaryInfluence))
                    throw new ArgumentException("Planetary influence must be finite.", "planetaryInfluence");
                float scale = 1f;
                if (NeedsAtmosphereForInfluence && !inAtmosphere) scale = EffectivenessAtMinInfluence;
                else if (MaxPlanetaryInfluence != MinPlanetaryInfluence)
                {
                    float t = Math.Max(0f, Math.Min(1f, (planetaryInfluence - MinPlanetaryInfluence)
                        / (MaxPlanetaryInfluence - MinPlanetaryInfluence)));
                    scale = EffectivenessAtMinInfluence + t * (EffectivenessAtMaxInfluence - EffectivenessAtMinInfluence);
                }
                return ThrustNewtons * scale;
            }


            public List<BlockComponent> Components = new List<BlockComponent>();

            public float Integrity;

            public int Pcu;

            public float BuildSeconds;

            public int CellCount
            {
                get { return Math.Abs(Size.X * Size.Y * Size.Z); }
            }

            public float GridSize
            {
                get { return Large ? 2.5f : 0.5f; }
            }

            public float VolumeCubicMetres
            {
                get { return CellCount * GridSize * GridSize * GridSize; }
            }

            public float Mass
            {
                get
                {
                    float total = 0f;
                    for (int i = 0; i < Components.Count; i++) total += Components[i].Mass;
                    return total;
                }
            }


            public override string ToString()
            {
                return TypeId + "/" + SubtypeId;
            }
        }


        public static string ContentPath()
        {

            List<string> candidates = new List<string>();

            string bin = Environment.GetEnvironmentVariable("SE_BIN");
            if (!string.IsNullOrEmpty(bin))
            {
                DirectoryInfo parent = Directory.GetParent(bin.TrimEnd('/', '\\'));
                if (parent != null) candidates.Add(Path.Combine(parent.FullName, "Content", "Data"));
            }

            string home = Environment.GetEnvironmentVariable("HOME") ?? "";
            candidates.Add(Path.Combine(home,
                "Steam/SteamLibrary/steamapps/common/SpaceEngineers/Content/Data"));
            candidates.Add("C:/Program Files (x86)/Steam/steamapps/common/SpaceEngineers/Content/Data");

            foreach (string candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "Components.sbc"))) return candidate;
            }
            return null;
        }

        public static bool IsInstalled
        {

            get { return ContentPath() != null; }
        }

        private static List<Definition> _all;
        private static Dictionary<string, float> _componentMasses;
        private static Dictionary<string, float> _componentIntegrities;


        private static readonly object CacheLock = new object();


        public static void Warm()
        {
            All();
            BySubtype();
        }


        public static Dictionary<string, float> ComponentMasses()
        {
            lock (CacheLock)
            {
            if (_componentMasses != null) return _componentMasses;

            Dictionary<string, float> masses = new Dictionary<string, float>();

            string content = ContentPath();

            if (content != null)
            {
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
                        masses[subtype] = mass;
                    }
                }
            }

            _componentMasses = masses;
            return masses;
            }
        }


        public static Dictionary<string, float> ComponentIntegrities()
        {
            lock (CacheLock)
            {
            if (_componentIntegrities != null) return _componentIntegrities;

            Dictionary<string, float> integrities = new Dictionary<string, float>();

            string content = ContentPath();

            if (content != null)
            {
                foreach (XElement component in XDocument.Load(Path.Combine(content, "Components.sbc"))
                             .Descendants("Component"))
                {
                    XElement id = component.Element("Id");
                    if (id == null) continue;

                    string subtype = (string)id.Element("SubtypeId");
                    float integrity;
                    if (subtype != null && float.TryParse((string)component.Element("MaxIntegrity"),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out integrity))
                    {
                        integrities[subtype] = integrity;
                    }
                }
            }

            _componentIntegrities = integrities;
            return integrities;
            }
        }


        public static float IntegrityOf(string subtype)
        {
            if (string.IsNullOrEmpty(subtype)) return 0f;

            Definition definition;
            return BySubtype().TryGetValue(subtype, out definition) ? definition.Integrity : 0f;
        }


        public static List<Definition> All()
        {
            lock (CacheLock)
            {
            if (_all != null) return _all;


            List<Definition> blocks = new List<Definition>();

            string content = ContentPath();
            if (content == null)
            {
                _all = blocks;
                return blocks;
            }


            Dictionary<string, float> masses = ComponentMasses();

            Dictionary<string, float> integrities = ComponentIntegrities();
            string directory = Path.Combine(content, "CubeBlocks");
            if (!Directory.Exists(directory))
            {
                _all = blocks;
                return blocks;
            }

            foreach (string file in Directory.GetFiles(directory, "*.sbc"))
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }

                foreach (XElement definition in document.Descendants("Definition"))
                {

                    Definition block = Read(definition, masses, integrities);
                    if (block != null) blocks.Add(block);
                }
            }

            _all = blocks;
            return blocks;
            }
        }


        private static Definition Read(XElement definition, Dictionary<string, float> masses,
            Dictionary<string, float> integrities)
        {
            XElement id = definition.Element("Id");
            if (id == null) return null;

            string type = (string)id.Element("TypeId");
            string subtype = (string)id.Element("SubtypeId");
            if (string.IsNullOrEmpty(type)) return null;

            if (type.StartsWith("MyObjectBuilder_")) type = type.Substring("MyObjectBuilder_".Length);

            Definition block = new Definition
            {
                TypeId = type,
                SubtypeId = subtype ?? "",
                Large = ((string)definition.Element("CubeSize") ?? "Large") == "Large",

                Size = ParseSize(definition.Element("Size")),
            };


            block.PowerOutputWatts = Megawatts(definition, "MaxPowerOutput");
            block.PowerDrawWatts = Math.Max(
                Megawatts(definition, "RequiredPowerInput"),
                Math.Max(Megawatts(definition, "MaxRequiredPowerInput"),
                    Math.Max(Megawatts(definition, "MaxPowerConsumption"),
                        Megawatts(definition, "OperationalPowerConsumption"))));
            if (block.TypeId == "Thrust")
            {
                block.ThrustNewtons = Number(definition, "ForceMagnitude");
                block.MinPlanetaryInfluence = FlightNumber(definition, "MinPlanetaryInfluence");
                block.MaxPlanetaryInfluence = FlightNumber(definition, "MaxPlanetaryInfluence", 1f);
                block.EffectivenessAtMinInfluence = FlightNumber(definition, "EffectivenessAtMinInfluence", 1f);
                block.EffectivenessAtMaxInfluence = FlightNumber(definition, "EffectivenessAtMaxInfluence", 1f);
                block.NeedsAtmosphereForInfluence = (bool?)definition.Element("NeedsAtmosphereForInfluence") ?? false;
            }


            block.JumpEnergyJoules = Number(definition, "PowerNeededForJump") * 3600f
                * ThermalConstants.MegawattsToWatts;


            float efficiency = Number(definition, "PowerEfficiency");
            if (efficiency > 0f) block.PowerEfficiency = efficiency;

            bool airtight;
            string airtightText = (string)definition.Element("IsAirTight");
            if (airtightText != null && bool.TryParse(airtightText, out airtight)) block.Airtight = airtight;

            XElement mounts = definition.Element("MountPoints");
            if (mounts != null)
            {
                foreach (XElement mount in mounts.Elements("MountPoint"))
                {

                    int face = FaceOf((string)mount.Attribute("Side"));
                    if (face < 0) continue;

                    block.MountFaces[face] = true;
                    block.HasDeclaredMounts = true;
                }
            }

            int pcu;
            if (int.TryParse((string)definition.Element("PCU"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out pcu))
            {
                block.Pcu = pcu;
            }


            block.BuildSeconds = Number(definition, "BuildTimeSeconds");

            XElement components = definition.Element("Components");
            if (components != null)
            {
                foreach (XElement component in components.Elements("Component"))
                {
                    string name = (string)component.Attribute("Subtype");
                    if (name == null) continue;

                    int count;
                    if (!int.TryParse((string)component.Attribute("Count"),
                            NumberStyles.Integer, CultureInfo.InvariantCulture, out count)) continue;

                    float integrityEach;
                    if (integrities.TryGetValue(name, out integrityEach))
                    {
                        block.Integrity += count * integrityEach;
                    }

                    float mass;
                    if (!masses.TryGetValue(name, out mass)) continue;

                    block.Components.Add(new BlockComponent(name, count, mass));
                }
            }

            return block;
        }


        private static float Megawatts(XElement definition, string name)
        {
            return Number(definition, name) * ThermalConstants.MegawattsToWatts;
        }


        private static float FlightNumber(XElement definition, string name, float fallback = 0f)
        {
            XElement element = definition.Element(name);
            if (element == null) return fallback;
            float value;
            if (!float.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("Nonfinite or malformed thruster field: " + name);
            return value;
        }

        private static float Number(XElement definition, string name)
        {
            float value;
            string text = (string)definition.Element(name);
            return text != null && float.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) ? value : 0f;
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
            return attribute != null && int.TryParse((string)attribute,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 1;
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


        public static Dictionary<string, Definition> BySubtype()
        {
            lock (CacheLock)
            {
            if (_bySubtype != null) return _bySubtype;

            Dictionary<string, Definition> map = new Dictionary<string, Definition>(StringComparer.Ordinal);
            foreach (Definition block in All())
            {
                if (!map.ContainsKey(block.SubtypeId)) map[block.SubtypeId] = block;
            }

            _bySubtype = map;
            return map;
            }
        }

        private static Dictionary<string, Definition> _bySubtype;


        public static Dictionary<string, Definition> BaseVariants()
        {
            lock (CacheLock)
            {
                if (_baseVariants != null) return _baseVariants;

                Dictionary<string, Definition> map =
                    new Dictionary<string, Definition>(StringComparer.Ordinal);

                foreach (Definition block in All())
                {
                    if (block.SubtypeId.Length > 0) continue;


                    string key = BaseVariantKey(block.TypeId, block.Large);
                    if (!map.ContainsKey(key)) map[key] = block;
                }

                _baseVariants = map;
                return map;
            }
        }


        public static string BaseVariantKey(string typeId, bool large)
        {
            return (typeId ?? "") + (large ? "/large" : "/small");
        }

        private static Dictionary<string, Definition> _baseVariants;


        public static string ModelName(Definition definition)
        {
            if (definition == null) return null;
            return definition.SubtypeId.Length > 0 ? definition.SubtypeId : definition.TypeId;
        }


        public static Dictionary<string, Definition> ByModelName()
        {
            lock (CacheLock)
            {
                if (_byModelName != null) return _byModelName;

                Dictionary<string, Definition> map =
                    new Dictionary<string, Definition>(BySubtype(), StringComparer.Ordinal);
                map.Remove("");

                foreach (Definition block in BaseVariants().Values)
                {
                    if (!map.ContainsKey(block.TypeId)) map[block.TypeId] = block;
                }

                _byModelName = map;
                return map;
            }
        }

        private static Dictionary<string, Definition> _byModelName;


        public static Dictionary<string, Definition> ByTypeAndSubtype()
        {
            lock (CacheLock)
            {
                if (_byTypeAndSubtype != null) return _byTypeAndSubtype;

                Dictionary<string, Definition> map =
                    new Dictionary<string, Definition>(StringComparer.Ordinal);

                foreach (Definition block in All())
                {

                    string key = TypeAndSubtypeKey(block.TypeId, block.SubtypeId);
                    if (!map.ContainsKey(key)) map[key] = block;
                }

                _byTypeAndSubtype = map;
                return map;
            }
        }


        /// <summary>Mirror a native subtype upgrade only when one installed target exists and the saved type is recognized.</summary>
        public static Definition UniqueSubtypeUpgrade(string typeId, string subtypeId)
        {
            if (string.IsNullOrEmpty(typeId) || string.IsNullOrEmpty(subtypeId)) return null;
            if (typeId.StartsWith("MyObjectBuilder_", StringComparison.Ordinal)) typeId = typeId.Substring("MyObjectBuilder_".Length);
            lock (CacheLock)
            {
                if (_upgradeBySubtype == null)
                {
                    _upgradeBySubtype = new Dictionary<string, Definition>(StringComparer.Ordinal);
                    _declaredBlockTypes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Definition block in ByTypeAndSubtype().Values)
                    {
                        _declaredBlockTypes.Add(block.TypeId);
                        if (string.IsNullOrEmpty(block.SubtypeId)) continue;
                        if (_upgradeBySubtype.ContainsKey(block.SubtypeId)) _upgradeBySubtype[block.SubtypeId] = null;
                        else _upgradeBySubtype.Add(block.SubtypeId, block);
                    }
                }
                Definition result;
                return _declaredBlockTypes.Contains(typeId) && _upgradeBySubtype.TryGetValue(subtypeId, out result) ? result : null;
            }
        }

        private static Dictionary<string, Definition> _upgradeBySubtype;
        private static HashSet<string> _declaredBlockTypes;


        public static string TypeAndSubtypeKey(string typeId, string subtypeId)
        {
            string type = typeId ?? "";
            if (type.StartsWith("MyObjectBuilder_")) type = type.Substring("MyObjectBuilder_".Length);
            return type + "/" + (subtypeId ?? "");
        }

        private static Dictionary<string, Definition> _byTypeAndSubtype;


        public static Dictionary<string, List<Definition>> ByType()
        {
            Dictionary<string, List<Definition>> types = new Dictionary<string, List<Definition>>();

            foreach (Definition block in All())
            {
                List<Definition> list;
                if (!types.TryGetValue(block.TypeId, out list))
                {

                    list = new List<Definition>();
                    types[block.TypeId] = list;
                }
                list.Add(block);
            }

            return types;
        }


        public static List<BlockComponent> TypeComponents(IList<Definition> blocks)
        {
            Dictionary<string, BlockComponent> total = new Dictionary<string, BlockComponent>();

            for (int i = 0; i < blocks.Count; i++)
            {
                List<BlockComponent> components = blocks[i].Components;
                for (int j = 0; j < components.Count; j++)
                {
                    BlockComponent line = components[j];

                    BlockComponent running;
                    if (total.TryGetValue(line.Component, out running))
                    {
                        running.Count += line.Count;
                        total[line.Component] = running;
                    }
                    else
                    {
                        total[line.Component] = line;
                    }
                }
            }

            return new List<BlockComponent>(total.Values);
        }
    }
}

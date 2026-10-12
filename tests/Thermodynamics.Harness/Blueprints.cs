using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class Blueprints
    {
        public class Grid
        {
            public string Name;
            public bool Large;
            public int Blocks;
            public GridBuilder Builder;
            public BlueprintFrame Frame;


            public readonly Dictionary<long, Vector3I> BlocksById = new Dictionary<long, Vector3I>();

            public readonly List<KeyValuePair<Vector3I, long>> Mechanical =
                new List<KeyValuePair<Vector3I, long>>();
        }

        public class Ship
        {
            public string Name;

            public string Path;

            public long WorkshopId;


            public readonly List<Grid> Grids = new List<Grid>();

            public bool Large
            {
                get { return Grids.Count > 0 && Grids[0].Large; }
            }

            public int Blocks
            {
                get
                {
                    int total = 0;
                    for (int i = 0; i < Grids.Count; i++) total += Grids[i].Blocks;
                    return total;
                }
            }

            public int Subgrids
            {
                get { return Grids.Count == 0 ? 0 : Grids.Count - 1; }
            }

            public int UnknownBlocks;

            public int AmbiguousBlocks;

            public int UpgradedBlocks;


            public List<string> UnknownSubtypes = new List<string>();

            public bool IsVanilla
            {
                get { return UnknownBlocks == 0 && Blocks > 0; }
            }


            public ShipAssembly Build(ThermalSettings settings = null, float kelvin = 293.15f)
            {

                ThermalSettings effective = settings ?? new HistoricalThermalSettings();

                ShipAssembly assembly = new ShipAssembly();

                for (int i = 0; i < Grids.Count; i++)
                {
                    assembly.Simulations.Add(Grids[i].Builder.BuildSimulation(effective, kelvin));
                }

                LinkJoints(assembly);
                return assembly;
            }


            private void LinkJoints(ShipAssembly assembly)
            {
                for (int i = 0; i < Grids.Count; i++)
                {
                    Grid grid = Grids[i];

                    for (int m = 0; m < grid.Mechanical.Count; m++)
                    {
                        Vector3I baseCell = grid.Mechanical[m].Key;
                        long topId = grid.Mechanical[m].Value;

                        for (int j = 0; j < Grids.Count; j++)
                        {
                            if (j == i) continue;

                            Vector3I topCell;
                            if (!Grids[j].BlocksById.TryGetValue(topId, out topCell)) continue;

                            assembly.Bridge2(

                                assembly.Simulations[i], NodeAt(assembly.Simulations[i], baseCell),

                                assembly.Simulations[j], NodeAt(assembly.Simulations[j], topCell));
                            break;
                        }
                    }
                }
            }


            private static ThermalNode NodeAt(ThermalSimulation simulation, Vector3I cell)
            {
                IList<ThermalNode> nodes = simulation.Solver.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Block.Position == cell) return nodes[i];
                }
                return null;
            }


            public Ship Reload()
            {
                if (Path == null) return this;


                List<Ship> ships = Read(Path);
                return ships.Count > 0 ? ships[0] : this;
            }


            public override string ToString()
            {
                return Name + " (" + Blocks + " blocks, " + Grids.Count + " grids)";
            }
        }


        public static string CorpusPath()
        {
            string configured = Environment.GetEnvironmentVariable("THERMAL_CORPUS");
            if (!string.IsNullOrEmpty(configured)) return configured;

            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(data))
            {
                string home = Environment.GetEnvironmentVariable("HOME")
                    ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? ".";
                data = Path.Combine(home, ".local", "share");
            }

            return Path.Combine(data, "thermal-dynamics", "corpus");
        }



        public static bool IsLegacyBlueprintEntry(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.EndsWith(".sbc", StringComparison.OrdinalIgnoreCase);
        }


        public static string DefaultPath()
        {

            string corpus = CorpusPath();
            if (Directory.Exists(corpus) && Files(corpus).Count > 0) return corpus;


            return WorkshopPath();
        }


        public static string WorkshopPath()
        {
            string content = GameBlocks.ContentPath();
            if (content == null) return null;


            DirectoryInfo directory = new DirectoryInfo(content);
            for (int i = 0; i < 4 && directory != null; i++) directory = directory.Parent;
            if (directory == null) return null;

            string workshop = Path.Combine(directory.FullName, "workshop", "content", "244850");
            return Directory.Exists(workshop) ? workshop : null;
        }


        public static List<string> Files(string root)
        {

            List<string> files = new List<string>();
            if (root == null || !Directory.Exists(root)) return files;

            files.AddRange(Directory.GetFiles(root, "bp.sbc", SearchOption.AllDirectories));
            return files;
        }


        public static string PrefabPath()
        {
            string content = GameBlocks.ContentPath();
            if (content == null) return null;

            string prefabs = Path.Combine(content, "Prefabs");
            return Directory.Exists(prefabs) ? prefabs : null;
        }


        public static List<string> PrefabFiles(string root = null)
        {

            List<string> files = new List<string>();


            string path = root ?? PrefabPath();
            if (path == null || !Directory.Exists(path)) return files;

            files.AddRange(Directory.GetFiles(path, "*.sbc", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            return files;
        }


        public static string PrefabCategory(string path)
        {

            string root = PrefabPath();
            if (path == null || root == null) return "";

            string relative = path.StartsWith(root, StringComparison.Ordinal)
                ? path.Substring(root.Length).TrimStart('/', '\\')
                : path;

            int slash = relative.IndexOfAny(new[] { '/', '\\' });
            return slash < 0 ? "" : relative.Substring(0, slash);
        }


        public static List<Ship> Read(string path)
        {
            try
            {

                return ReadFile(path);
            }
            catch (Exception error)
            {
                lock (UnreadableGate)
                {
                    unreadable[path] = error.GetType().Name + ": " + error.Message;
                }

                return new List<Ship>();
            }
        }


        private static readonly object UnreadableGate = new object();

        private static readonly Dictionary<string, string> unreadable =
            new Dictionary<string, string>(StringComparer.Ordinal);


        public static Dictionary<string, string> Unreadable()
        {
            lock (UnreadableGate)
            {
                return new Dictionary<string, string>(unreadable, StringComparer.Ordinal);
            }
        }


        private static List<Ship> ReadFile(string path)
        {

            List<Ship> ships = new List<Ship>();


            XDocument document = Load(path);
            if (document == null) return ships;

            Dictionary<string, GameBlocks.Definition> definitions = GameBlocks.BySubtype();


            string name = NameOf(document, "ShipBlueprint") ?? NameOf(document, "Prefab");

            Ship ship = new Ship
            {
                Path = path,

                WorkshopId = WorkshopIdOf(path),
                Name = name,
            };

            foreach (XElement grid in document.Descendants("CubeGrid"))
            {

                Grid part = ReadGrid(grid, definitions, ship);
                if (part != null && part.Blocks > 0) ship.Grids.Add(part);
            }

            if (ship.Grids.Count == 0) return ships;

            ship.Grids.Sort(delegate (Grid a, Grid b) { return b.Blocks.CompareTo(a.Blocks); });

            if (string.IsNullOrEmpty(ship.Name)) ship.Name = ship.Grids[0].Name;

            ships.Add(ship);
            return ships;
        }


        public static Ship Probe(string path)
        {

            Ship ship = new Ship { Path = path, WorkshopId = WorkshopIdOf(path) };


            XDocument document = Load(path);
            if (document == null) return ship;

            Dictionary<string, GameBlocks.Definition> definitions = GameBlocks.BySubtype();

            foreach (XElement grid in document.Descendants("CubeGrid"))
            {

                Grid part = ReadGrid(grid, definitions, ship);
                if (part != null && part.Blocks > 0) ship.Grids.Add(part);
            }

            return ship;
        }


        private static long WorkshopIdOf(string path)
        {
            long id;
            string folder = Path.GetDirectoryName(path);

            while (!string.IsNullOrEmpty(folder))
            {
                string name = Path.GetFileName(folder);
                string parent = Path.GetDirectoryName(folder);

                if (!string.IsNullOrEmpty(parent) && Path.GetFileName(parent) == AppId
                    && long.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out id))
                {
                    return id;
                }

                folder = parent;
            }

            folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
            return long.TryParse(folder, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
                ? id : 0L;
        }

        private const string AppId = "244850";


        private static Grid ReadGrid(XElement grid,
            Dictionary<string, GameBlocks.Definition> definitions, Ship ship)
        {
            XElement blocks = grid.Element("CubeBlocks");
            if (blocks == null) return null;

            bool large = ((string)grid.Element("GridSizeEnum") ?? "Large") == "Large";

            Grid part = new Grid
            {
                Name = (string)grid.Element("DisplayName") ?? "(unnamed)",
                Large = large,
                Builder = large ? GridBuilder.Large() : GridBuilder.Small(),
                Frame = BlueprintFrame.Read(grid.Element("PositionAndOrientation")),
            };

            foreach (XElement block in blocks.Elements())
            {
                string subtype = (string)block.Element("SubtypeName");
                GameBlocks.Definition definition = null;
                bool upgraded = false;
                string named = subtype;

                if (string.IsNullOrEmpty(subtype))
                {

                    named = TypeOf(block);

                    definition = BaseVariantOf(named, large);

                    if (definition == null && named == "CubeBlock")
                    {
                        named = large ? "LargeBlockArmorBlock" : "SmallBlockArmorBlock";
                        definitions.TryGetValue(named, out definition);
                    }
                }
                else
                {

                    string typeId = TypeOf(block);
                    if (typeId == null
                        || !GameBlocks.ByTypeAndSubtype().TryGetValue(
                            GameBlocks.TypeAndSubtypeKey(typeId, subtype), out definition))
                    {
                        definition = GameBlocks.UniqueSubtypeUpgrade(typeId, subtype);
                        upgraded = definition != null;
                        if (definition == null && definitions.ContainsKey(subtype)) ship.AmbiguousBlocks++;
                    }
                }

                if (definition == null || definition.Large != large)
                {
                    ship.UnknownBlocks++;
                    if (ship.UnknownSubtypes.Count < 4 && named != null
                        && !ship.UnknownSubtypes.Contains(named))
                    {
                        ship.UnknownSubtypes.Add(named);
                    }
                    continue;
                }


                Vector3I cell = ParseCell(block.Element("Min"));
                part.Builder.Place(Model(definition), cell, Orientation(block));
                part.Blocks++;
                if (upgraded) ship.UpgradedBlocks++;


                long entityId = ParseLong(block.Element("EntityId"));
                if (entityId != 0L && !part.BlocksById.ContainsKey(entityId))
                {
                    part.BlocksById[entityId] = cell;
                }


                long topId = ParseLong(block.Element("TopBlockId"));
                if (topId != 0L) part.Mechanical.Add(new KeyValuePair<Vector3I, long>(cell, topId));
            }

            return part;
        }


        private static XDocument Load(string path)
        {
            try
            {
                bool compressed;
                using (FileStream probe = File.OpenRead(path))
                {
                    compressed = probe.ReadByte() == 0x1f && probe.ReadByte() == 0x8b;
                }

                if (!compressed) return XDocument.Load(path);

                using (FileStream file = File.OpenRead(path))
                using (GZipStream stream = new GZipStream(file, CompressionMode.Decompress))
                {
                    return XDocument.Load(stream);
                }
            }
            catch
            {
                return null;
            }
        }


        private static string NameOf(XDocument document, string element)
        {
            foreach (XElement definition in document.Descendants(element))
            {
                XElement id = definition.Element("Id");
                if (id == null) return null;

                return (string)id.Attribute("Subtype") ?? (string)id.Element("SubtypeId");
            }

            return null;
        }


        private static long ParseLong(XElement element)
        {
            long value;
            return element != null && long.TryParse((string)element,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0L;
        }


        private static GameBlocks.Definition BaseVariantOf(string typeId, bool large)
        {
            GameBlocks.Definition definition;
            return GameBlocks.BaseVariants().TryGetValue(
                GameBlocks.BaseVariantKey(typeId, large), out definition) ? definition : null;
        }


        private static string TypeOf(XElement block)
        {
            XAttribute type = block.Attribute(
                XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"));
            string value = type == null ? block.Name.LocalName : type.Value;
            if (type == null && (block.Name.NamespaceName.Length != 0
                || !value.StartsWith("MyObjectBuilder_", StringComparison.Ordinal))) return null;
            return value.StartsWith("MyObjectBuilder_")
                ? value.Substring("MyObjectBuilder_".Length) : value;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BlockModel> Models =
            new System.Collections.Concurrent.ConcurrentDictionary<string, BlockModel>(StringComparer.Ordinal);

        private static Func<string, string, BlockThermalProperties, BlockThermalProperties> materialOverride;

        public static Func<string, string, BlockThermalProperties, BlockThermalProperties> MaterialOverride
        {
            get { return materialOverride; }
            set
            {
                materialOverride = value;
                Models.Clear();
            }
        }


        public static BlockModel Model(GameBlocks.Definition definition)
        {
            string key = GameBlocks.TypeAndSubtypeKey(definition.TypeId, definition.SubtypeId);
            string name = GameBlocks.ModelName(definition);

            BlockModel model;
            if (Models.TryGetValue(key, out model)) return model;

            BlockThermalProperties thermal = ShippedBlocks.DeriveWithFunction(
                definition.Components, definition.TypeId, definition.PowerEfficiency);

            Func<string, string, BlockThermalProperties, BlockThermalProperties> material = materialOverride;
            if (material != null) thermal = material(definition.TypeId, definition.SubtypeId, thermal);

            model = BlockModel.Solid(name, definition.Size, definition.Mass, thermal);

            bool[] mounts = new bool[Face.Count];
            bool everyFace = true;

            for (int face = 0; face < Face.Count; face++)
            {
                mounts[face] = definition.HasDeclaredMounts ? definition.MountFaces[face] : true;
                if (!mounts[face]) everyFace = false;
            }

            bool seals = definition.Airtight ?? true;

            if (!everyFace || !seals)
            {
                foreach (Vector3I cell in model.LocalCells())
                {
                    int state = seals ? CellSurface.SelfAirtightMask : 0;
                    for (int face = 0; face < Face.Count; face++)
                    {
                        state = CellSurface.WithSelfMount(state, face, mounts[face]);
                    }
                    model.SetLocalSurface(cell, state);
                }
            }

            return Models.GetOrAdd(key, model);
        }


        private static Vector3I ParseCell(XElement element)
        {
            if (element == null) return Vector3I.Zero;

            return new Vector3I(
                ParseInt(element.Attribute("x")),
                ParseInt(element.Attribute("y")),
                ParseInt(element.Attribute("z")));
        }


        private static int ParseInt(XAttribute attribute)
        {
            int value;
            return attribute != null && int.TryParse((string)attribute,
                NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }


        private static BlockOrientation Orientation(XElement block)
        {
            XElement element = block.Element("BlockOrientation");
            if (element == null) return BlockOrientation.Identity;

            return new BlockOrientation(
                DirectionOf((string)element.Attribute("Forward"), Base6Directions.Direction.Forward),
                DirectionOf((string)element.Attribute("Up"), Base6Directions.Direction.Up));
        }


        private static Base6Directions.Direction DirectionOf(string name, Base6Directions.Direction fallback)
        {
            switch (name)
            {
                case "Forward": return Base6Directions.Direction.Forward;
                case "Backward": return Base6Directions.Direction.Backward;
                case "Left": return Base6Directions.Direction.Left;
                case "Right": return Base6Directions.Direction.Right;
                case "Up": return Base6Directions.Direction.Up;
                case "Down": return Base6Directions.Direction.Down;
                default: return fallback;
            }
        }
    }
}

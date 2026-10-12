using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class DesignedHullLab
    {
        public const int Side = 7;

        private const float Seconds = 14400f;
        private const float Step = 1200f;

        public class Row
        {
            public string Fit;

            public float SourceKelvin;

            public float SavedKelvin;

            public int Radiators;

            public int RadiatorsOnTheSkin;

            public float HottestKelvin;

            public string Note = "";

            public float SinkWattsPerKelvin;
        }


        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static readonly Vector3I Centre = new Vector3I(Side / 2, Side / 2, Side / 2);


        private static Vector3I PanelSize(bool large)
        {
            return Panel(large).Size;
        }


        private static BlockModel Panel(bool large)
        {
            return ShippedBlocks.Model(large ? "Gauge_LG_Radiator" : "Gauge_SG_Radiator");
        }


        private static List<Vector3I> Footprint(bool large, Vector3I origin)
        {

            Vector3I size = PanelSize(large);

            List<Vector3I> cells = new List<Vector3I>();

            for (int x = 0; x < size.X; x++)
            {
                for (int y = 0; y < size.Y; y++)
                {
                    for (int z = 0; z < size.Z; z++)
                    {
                        cells.Add(origin + new Vector3I(x, y, z));
                    }
                }
            }

            return cells;
        }


        private static List<Vector3I> Channel(int panels)
        {
            int beyond = 4 + panels;
            return PipeFitter.RectangleXZ(

                new Vector3I(Centre.X - 1, Centre.Y, Centre.Z - 1), Side / 2 + beyond, 3);
        }


        private static bool InHull(Vector3I cell)
        {
            return cell.X >= 0 && cell.X < Side
                && cell.Y >= 0 && cell.Y < Side
                && cell.Z >= 0 && cell.Z < Side;
        }


        private static bool SeesSky(GridModel grid, Vector3I cell)
        {
            for (int f = 0; f < Face.Count; f++)
            {
                if (!grid.IsOccupied(cell + Face.Offsets[f])) return true;
            }
            return false;
        }


        private static GridBuilder Hull(bool large, ICollection<Vector3I> reserved)
        {
            GridBuilder builder = large ? GridBuilder.Large() : GridBuilder.Small();
            BlockModel armour = Catalog.LightArmor();

            for (int x = 0; x < Side; x++)
            {
                for (int y = 0; y < Side; y++)
                {
                    for (int z = 0; z < Side; z++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);
                        if (cell == Centre) continue;
                        if (reserved != null && reserved.Contains(cell)) continue;

                        builder.Place(armour, cell);
                    }
                }
            }

            return builder;
        }


        private static Row Measure(string fit, GridBuilder builder, BlockInstance source,
            List<BlockInstance> radiators, float bare, LoopThermalProperties properties = null)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 293.15f);

            if (properties != null)
            {
                simulation.LoopProperties = properties;
                simulation.RebuildAll();
            }


            ScenarioRunner runner = new ScenarioRunner(simulation);
            runner.Environment = t => Worlds.Shadow();
            runner.Track("source", source);
            runner.Run(Seconds, Step);

            float sourceKelvin;
            runner.Final.Tracked.TryGetValue("source", out sourceKelvin);

            float hottest = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Temperature > hottest) hottest = nodes[i].Temperature;
            }

            float sink = 0f;
            IList<CoolantLoop> loops = simulation.Solver.Loops;
            if (loops != null)
            {
                for (int l = 0; l < loops.Count; l++)
                {
                    for (int i = 0; i < loops[l].Links.Count; i++)
                    {
                        int index = loops[l].Links[i].NodeIndex;
                        if (index < 0 || index >= simulation.Solver.Nodes.Count) continue;
                        if (simulation.Solver.Nodes[index].Block != source) continue;

                        sink += loops[l].LinkConductance(i);
                    }
                }
            }

            int onSkin = 0;
            for (int i = 0; i < radiators.Count; i++)
            {
                if (SeesSky(builder.Grid, radiators[i].Position)) onSkin++;
            }

            return new Row
            {
                Fit = fit,
                SourceKelvin = sourceKelvin,
                SavedKelvin = bare > 0f ? bare - sourceKelvin : 0f,
                Radiators = radiators.Count,
                RadiatorsOnTheSkin = onSkin,
                HottestKelvin = hottest,
                SinkWattsPerKelvin = sink,
            };
        }


        private static Row Bare(bool large, float watts, out float settled)
        {

            GridBuilder builder = Hull(large, null);
            builder.Place(BalanceLab.Heater(), Centre);
            BlockInstance source = builder.Last;
            builder.Last.PowerConsumedWatts = watts;


            Row row = Measure("buried, bare", builder, source, new List<BlockInstance>(), 0f);
            settled = row.SourceKelvin;
            return row;
        }


        private static Row Bolted(bool large, float watts, int panels, float bare)
        {

            Vector3I size = PanelSize(large);


            List<Vector3I> origins = new List<Vector3I>();

            List<Vector3I> reserved = new List<Vector3I>();

            for (int i = 0; i < panels; i++)
            {

                Vector3I origin = new Vector3I(
                    Centre.X + 1,
                    Centre.Y - (size.Y / 2),
                    Centre.Z - (size.Z / 2) + (i * size.Z));


                List<Vector3I> cells = Footprint(large, origin);

                bool fits = true;
                for (int c = 0; c < cells.Count && fits; c++) fits = InHull(cells[c]);
                if (!fits) continue;

                origins.Add(origin);
                reserved.AddRange(cells);
            }


            GridBuilder builder = Hull(large, reserved);
            builder.Place(BalanceLab.Heater(), Centre);
            BlockInstance source = builder.Last;
            builder.Last.PowerConsumedWatts = watts;


            BlockModel radiator = Panel(large);

            List<BlockInstance> radiators = new List<BlockInstance>();
            string note = "";

            for (int i = 0; i < origins.Count; i++)
            {
                try
                {
                    builder.Place(radiator, origins[i]);
                    radiators.Add(builder.Last);
                }
                catch (Exception error)
                {
                    if (note.Length == 0) note = error.Message;
                }
            }

            if (radiators.Count < panels && note.Length == 0)
            {
                note = "only " + radiators.Count + " of " + panels + " pockets fit inside the hull";
            }


            Row row = Measure("bolted, buried", builder, source, radiators, bare);
            row.Note = note;
            return row;
        }


        private static Row Plumbed(bool large, float watts, int panels, float bare,
            LoopThermalProperties properties = null, bool everyFace = false)
        {

            List<Vector3I> ring = Channel(panels);


            GridBuilder builder = Hull(large, ring);
            builder.Place(BalanceLab.Heater(), Centre);
            BlockInstance source = builder.Last;
            builder.Last.PowerConsumedWatts = watts;

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3I toSource = Centre - ring[i];
                if (Face.IndexOf(toSource) < 0) continue;

                sinks[i] = toSource;
                if (!everyFace) break;
            }

            string note = sinks.Count == 0 ? "no ring cell touches the source" : "";


            BlockModel radiator = Panel(large);

            Vector3I size = PanelSize(large);

            List<BlockInstance> radiators = new List<BlockInstance>();

            for (int i = 0; i < ring.Count && radiators.Count < panels; i++)
            {
                if (sinks.ContainsKey(i)) continue;
                if (InHull(ring[i])) continue;
                if (!IsStraightRun(ring, i)) continue;

                for (int f = 0; f < Face.Count; f++)
                {
                    Vector3I direction = Face.Offsets[f];
                    Vector3I origin = ring[i] + direction;


                    List<Vector3I> footprint = Footprint(large, origin);

                    bool clear = true;
                    for (int c = 0; c < footprint.Count && clear; c++)
                    {
                        clear = !InHull(footprint[c])
                            && !ring.Contains(footprint[c])
                            && !builder.Grid.IsOccupied(footprint[c]);
                    }

                    if (!clear) continue;

                    try
                    {
                        builder.Place(radiator, origin);
                        radiators.Add(builder.Last);
                        sinks[i] = direction;
                    }
                    catch (Exception error)
                    {
                        if (note.Length == 0) note = error.Message;
                    }

                    break;
                }
            }

            if (radiators.Count < panels && note.Length == 0)
            {
                note = "only " + radiators.Count + " of " + panels + " panels found room on the ring";
            }

            try
            {
                PipeFitter.BuildRing(builder, ring, -1, sinks);
            }
            catch (Exception error)
            {
                return new Row
                {
                    Fit = "plumbed, to the skin",
                    SourceKelvin = bare,
                    Note = "ring refused: " + error.Message,
                };
            }


            Row row = Measure("plumbed, to the skin", builder, source, radiators, bare, properties);
            row.Note = note;
            return row;
        }


        private static bool IsStraightRun(List<Vector3I> ring, int index)
        {
            Vector3I cell = ring[index];
            Vector3I previous = ring[(index - 1 + ring.Count) % ring.Count];
            Vector3I next = ring[(index + 1) % ring.Count];

            return (previous - cell) == -(next - cell);
        }


        public static List<Row> Run(float watts, int panels)
        {

            return Run(watts, panels, true);
        }


        public static List<Row> Run(float watts, int panels, bool large)
        {
            float bare;

            Row bareRow = Bare(large, watts, out bare);


            Row bolted = Bolted(large, watts, panels, bare);

            Row plumbed = Plumbed(large, watts, panels, bare);

            int fair = Math.Min(bolted.Radiators, plumbed.Radiators);

            if (fair > 0 && bolted.Radiators != plumbed.Radiators)
            {
                if (bolted.Radiators > fair) bolted = Bolted(large, watts, fair, bare);
                if (plumbed.Radiators > fair) plumbed = Plumbed(large, watts, fair, bare);

                string levelled = "levelled to " + fair + " panels, the most both fits had room for";
                bolted.Note = string.IsNullOrEmpty(bolted.Note) ? levelled : bolted.Note;
                plumbed.Note = string.IsNullOrEmpty(plumbed.Note) ? levelled : plumbed.Note;
            }


            List<Row> rows = new List<Row>();
            rows.Add(bareRow);
            rows.Add(bolted);
            rows.Add(plumbed);
            return rows;
        }


        public static string Sweep(float watts, float criticalKelvin, int maxPanels)
        {

            return Sweep(watts, criticalKelvin, maxPanels, true);
        }


        public static string Sweep(float watts, float criticalKelvin, int maxPanels, bool large)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("SKIN LADDER  (how much radiator a buried load needs, plumbed to the hull)");
            sb.AppendLine();
            sb.AppendLine("  " + (watts / 1000000f).ToString("n2") + " MW buried in a " + Side
                + "-cube of " + (large ? "large" : "small") + " grid, shadow, against a "
                + criticalKelvin.ToString("n0") + " K rating");
            sb.AppendLine();
            sb.AppendLine("  loop                 panels   on the skin   sink W/K   needs K   source K   under");

            float bare;
            Bare(large, watts, out bare);
            sb.AppendLine(string.Format("  {0,-19}  {1,6}   {2,11}   {3,8}   {4,7}   {5,8}   {6,5}",
                "none", 0, 0, "-", "-", bare.ToString("n1"), bare < criticalKelvin ? "yes" : "no"));

            for (int arm = 0; arm < 4; arm++)
            {
                bool before = (arm & 1) != 0;
                bool everyFace = (arm & 2) != 0;

                LoopThermalProperties properties = before
                    ? LoopBefore.For(large ? Catalog.LargeGridSize : Catalog.SmallGridSize)
                    : null;
                string label = (before ? "before C42/C43" : "shipped")
                    + (everyFace ? ", 3 faces" : ", 1 face");

                for (int panels = 1; panels <= maxPanels; panels++)
                {

                    Row row = Plumbed(large, watts, panels, bare, properties, everyFace);

                    string needs = row.SinkWattsPerKelvin > 0f
                        ? (watts / row.SinkWattsPerKelvin).ToString("n0")
                        : "-";

                    sb.AppendLine(string.Format("  {0,-19}  {1,6}   {2,11}   {3,8}   {4,7}   {5,8}   {6,5}",
                        label, panels, row.RadiatorsOnTheSkin,
                        row.SinkWattsPerKelvin.ToString("n0"), needs,
                        row.SourceKelvin.ToString("n1"),
                        row.SourceKelvin < criticalKelvin ? "yes" : "no"));
                }
            }

            sb.AppendLine();
            sb.AppendLine("  `sink W/K` is the pickup: every sink face the ring has onto the source.");
            sb.AppendLine("  `needs K` is the watts over it - the gradient the source is forced to");
            sb.AppendLine("  sit at whatever is hung off the other end. When that exceeds the rating,");
            sb.AppendLine("  adding radiator cannot help, and the panel column shows it saturating.");
            sb.AppendLine("  `before C42/C43` is the loop as it was: a coefficient of 160 applied");
            sb.AppendLine("  whether or not anything circulated, and a flat 50 kg of coolant a pipe.");
            sb.AppendLine("  It does nothing for a retrofit and is measured here because a buried");
            sb.AppendLine("  source has no other path.");

            return sb.ToString();
        }


        public static string Report(float watts = 200000f, int panels = 4, bool large = true)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("DESIGNED HULL  (a source buried in a " + Side + "-cube of "
                + (large ? "large" : "small") + "-grid light armour)");
            sb.AppendLine();
            sb.AppendLine("  " + (watts / 1000f).ToString("n0") + " kW into the centre cell, shadow, "
                + (Seconds / 3600f).ToString("n0") + " h to steady state");
            sb.AppendLine("  the arms differ in WHERE the heat is put, not in how fast it moves");
            sb.AppendLine();


            List<Row> rows = Run(watts, panels, large);
            LastRows = rows;

            sb.AppendLine("  fit                    source K   saved K   hottest K   panels   on the skin");
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                sb.AppendLine(string.Format("  {0,-20} {1,10} {2,9} {3,11} {4,8} {5,13}",
                    row.Fit, row.SourceKelvin.ToString("n1"), row.SavedKelvin.ToString("n1"),
                    row.HottestKelvin.ToString("n1"), row.Radiators, row.RadiatorsOnTheSkin));

                if (!string.IsNullOrEmpty(row.Note))
                {
                    sb.AppendLine("      note: " + row.Note);
                }
            }

            sb.AppendLine();
            sb.AppendLine("  `on the skin` is the whole comparison: a panel with no unoccupied face");
            sb.AppendLine("  radiates to nothing, however good a radiator it is. If the two arms carry");
            sb.AppendLine("  the same panels and only one arm's panels see sky, the difference is the");
            sb.AppendLine("  loop reaching somewhere a bolt joint cannot - which is the mod's design");
            sb.AppendLine("  thesis, and was untested until this lab (backlog.md C41).");

            return sb.ToString();
        }

        public static List<Row> LastRows { get; private set; }
    }
}

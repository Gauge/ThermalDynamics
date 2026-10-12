using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class RetrofitLab
    {
        public const float Seconds = 1800f;

        public static class Corpus
        {
            public const int Warm = 332;


            public const int BoltedFitted = 281;

            public const float BoltedMedianPercent = -0.11f;
            public const int BoltedHelped = 60;
            public const int BoltedHurt = 85;


            public const int PlumbedFitted = 49;

            public const float PlumbedMedianPercent = 1.35f;
            public const int PlumbedHelped = 26;
            public const int PlumbedHurt = 5;

            public const float PlumbedP90Percent = 8.94f;
        }

        public const int MaxBlocks = 8;

        public class Row
        {
            public string Ship;
            public int Blocks;
            public bool Large;

            public float BareK;

            public string HottestBlock;

            public int BoltedPlaced;
            public float BoltedK;

            public int PlumbedPipes;
            public float PlumbedK;

            public float TransportK;

            public bool LoopFlowing;
            public float LoopSinkWattsPerKelvin;
            public float CarriedSinkWattsPerKelvin;

            public float HotBlockWattsPerKelvin;

            public float AboveHullKelvin;

            public float BoltedSaved { get { return BareK - BoltedK; } }
            public float PlumbedSaved { get { return BareK - PlumbedK; } }
            public float TransportSaved { get { return BareK - TransportK; } }
        }


        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new HistoricalThermalSettings();
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static float Load(ShipAssembly assembly, out ThermalNode hottest)
        {
            ShipLoad.Apply(assembly, ShipLoad.State.Full);


            AssemblyRunner runner = new AssemblyRunner(assembly);
            runner.Environment = t => Worlds.Shadow();
            runner.Run(Seconds);

            hottest = assembly.Hottest();
            return hottest == null ? 0f : hottest.Temperature;
        }


        private static List<Vector3I> FreeCellsAround(GridModel grid, Vector3I centre, int reach)
        {

            List<Vector3I> free = new List<Vector3I>();

            for (int radius = 1; radius <= reach; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            if (Math.Abs(x) != radius && Math.Abs(y) != radius && Math.Abs(z) != radius) continue;


                            Vector3I cell = centre + new Vector3I(x, y, z);
                            if (grid.IsOccupied(cell)) continue;

                            bool touches = false;
                            for (int face = 0; face < Face.Count && !touches; face++)
                            {
                                touches = grid.IsOccupied(cell + Face.Offsets[face]);
                            }

                            if (touches) free.Add(cell);
                        }
                    }
                }
            }

            return free;
        }


        private static GridBuilder BuilderOf(Blueprints.Ship ship, ShipAssembly assembly, ThermalNode node)
        {
            for (int i = 0; i < assembly.Simulations.Count && i < ship.Grids.Count; i++)
            {
                IList<ThermalNode> nodes = assembly.Simulations[i].Solver.Nodes;
                for (int n = 0; n < nodes.Count; n++)
                {
                    if (ReferenceEquals(nodes[n], node)) return ship.Grids[i].Builder;
                }
            }

            return null;
        }


        private static int Bolt(GridBuilder builder, Vector3I hot, BlockModel radiator)
        {

            List<Vector3I> free = FreeCellsAround(builder.Grid, hot, 3);
            int placed = 0;

            for (int i = 0; i < free.Count && placed < MaxBlocks; i++)
            {
                try
                {
                    builder.Place(radiator, free[i]);
                    placed++;
                }
                catch (Exception)
                {
                }
            }

            return placed;
        }


        private static int Plumb(GridBuilder builder, Vector3I hot, BlockModel radiator)
        {
            GridModel grid = builder.Grid;

            for (int plane = 0; plane < 3; plane++)
            {
                for (int offset = -3; offset <= 1; offset++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {

                        Vector3I origin = Origin(hot, plane, offset, side);

                        List<Vector3I> ring = Ring(origin, plane);
                        if (ring == null) continue;

                        bool clear = true;
                        for (int i = 0; i < ring.Count && clear; i++)
                        {
                            clear = !grid.IsOccupied(ring[i]);
                        }

                        if (!clear) continue;

                        Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
                        for (int i = 0; i < ring.Count; i++)
                        {
                            Vector3I toHot = hot - ring[i];
                            if (Face.IndexOf(toHot) >= 0) sinks[i] = toHot;
                        }

                        if (sinks.Count == 0) continue;


                        int fed = FeedPanels(builder, ring, sinks, radiator);

                        try
                        {
                            PipeFitter.BuildRing(builder, ring, -1, sinks);
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        Bolt(builder, hot, radiator);

                        return ring.Count;
                    }
                }
            }

            return 0;
        }


        private static Vector3I Origin(Vector3I hot, int plane, int offset, int side)
        {
            switch (plane)
            {

                case 0: return hot + new Vector3I(offset, side, -1);

                case 1: return hot + new Vector3I(-1, offset, side);

                default: return hot + new Vector3I(side, -1, offset);
            }
        }


        private static List<Vector3I> Ring(Vector3I origin, int plane)
        {
            switch (plane)
            {
                case 0: return PipeFitter.RectangleXZ(origin, 3, 3);
                case 1: return PipeFitter.RectangleXY(origin, 3, 3);
                default: return PipeFitter.RectangleYZ(origin, 3, 3);
            }
        }



        private static int FeedPanels(GridBuilder builder, List<Vector3I> ring,
            Dictionary<int, Vector3I> sinks, BlockModel radiator)
        {
            int fed = 0;

            int straightLeft = CountStraightWithoutSink(ring, sinks);

            for (int i = 0; i < ring.Count && fed < MaxBlocks; i++)
            {
                if (sinks.ContainsKey(i)) continue;


                bool straight = IsStraight(ring, i);
                if (straight && straightLeft <= 1) continue;

                for (int f = 0; f < Face.Count; f++)
                {
                    Vector3I direction = Face.Offsets[f];
                    Vector3I at = ring[i] + direction;

                    if (builder.Grid.IsOccupied(at)) continue;
                    if (ring.Contains(at)) continue;

                    try
                    {
                        builder.Place(radiator, at);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    sinks[i] = direction;
                    fed++;
                    if (straight) straightLeft--;
                    break;
                }
            }

            return fed;
        }


        private static float ConductanceOutOf(ShipAssembly assembly, Vector3I cell)
        {
            for (int s = 0; s < assembly.Simulations.Count; s++)
            {
                ThermalSimulation simulation = assembly.Simulations[s];

                BlockInstance block = simulation.Grid.GetAtCell(cell);
                if (block == null) continue;

                ThermalNode node = simulation.Solver.GetNode(block);
                if (node == null) continue;

                IList<ThermalNode> nodes = simulation.Solver.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] == node) return simulation.Solver.NodeConductanceTotal(i);
                }
            }

            return 0f;
        }


        private static float AboveMedian(ShipAssembly assembly, Vector3I cell)
        {
            for (int s = 0; s < assembly.Simulations.Count; s++)
            {
                ThermalSimulation simulation = assembly.Simulations[s];

                BlockInstance block = simulation.Grid.GetAtCell(cell);
                if (block == null) continue;

                ThermalNode node = simulation.Solver.GetNode(block);
                if (node == null) continue;

                IList<ThermalNode> nodes = simulation.Solver.Nodes;

                List<float> temperatures = new List<float>(nodes.Count);
                for (int i = 0; i < nodes.Count; i++) temperatures.Add(nodes[i].Temperature);

                temperatures.Sort();
                return node.Temperature - At(temperatures, 0.5);
            }

            return 0f;
        }


        private static int CountStraightWithoutSink(List<Vector3I> ring, Dictionary<int, Vector3I> sinks)
        {
            int count = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                if (!sinks.ContainsKey(i) && IsStraight(ring, i)) count++;
            }
            return count;
        }


        private static bool IsStraight(List<Vector3I> ring, int index)
        {
            Vector3I cell = ring[index];
            Vector3I previous = ring[(index - 1 + ring.Count) % ring.Count];
            Vector3I next = ring[(index + 1) % ring.Count];

            return (previous - cell) == -(next - cell);
        }


        public static Row Measure(Blueprints.Ship ship)
        {

            ThermalSettings settings = Settings();

            ShipAssembly bare = ship.Build(settings);
            ThermalNode hottest;

            float bareK = Load(bare, out hottest);

            Row row = new Row
            {
                Ship = ship.Name,
                Blocks = ship.Blocks,
                Large = ship.Large,
                BareK = bareK,
                HottestBlock = hottest == null ? "" : hottest.Block.Name,
                BoltedK = bareK,
                PlumbedK = bareK,
                TransportK = bareK,
            };

            if (hottest == null) return row;


            GridBuilder builder = BuilderOf(ship, bare, hottest);
            if (builder == null) return row;

            Vector3I hot = hottest.Block.Position;
            BlockModel radiator = ShippedBlocks.Model(ship.Large ? "Gauge_LG_Radiator" : "Gauge_SG_Radiator");


            row.BoltedPlaced = Bolt(builder, hot, radiator);
            if (row.BoltedPlaced > 0)
            {
                ShipAssembly bolted = ship.Build(settings);
                ThermalNode after;

                row.BoltedK = Load(bolted, out after);
            }


            row.PlumbedPipes = Plumb(builder, hot, radiator);
            if (row.PlumbedPipes > 0)
            {
                ShipAssembly plumbed = ship.Build(settings);
                ThermalNode after;

                row.PlumbedK = Load(plumbed, out after);

                bool flowing;

                row.LoopSinkWattsPerKelvin = WorstSink(plumbed, out flowing);
                row.LoopFlowing = flowing;

                row.HotBlockWattsPerKelvin = ConductanceOutOf(plumbed, hot);

                row.AboveHullKelvin = AboveMedian(plumbed, hot);

                ShipAssembly carried = ship.Build(settings);
                LoopBefore.Apply(carried);

                row.TransportK = Load(carried, out after);

                bool carriedFlowing;

                row.CarriedSinkWattsPerKelvin = WorstSink(carried, out carriedFlowing);
            }

            return row;
        }


        private static float WorstSink(ShipAssembly assembly, out bool flowing)
        {
            float worst = 0f;
            flowing = false;

            for (int s = 0; s < assembly.Simulations.Count; s++)
            {
                IList<CoolantLoop> loops = assembly.Simulations[s].Solver.Loops;
                if (loops == null) continue;

                for (int l = 0; l < loops.Count; l++)
                {
                    CoolantLoop loop = loops[l];
                    if (loop.FlowSegmentsPerSecond != 0f) flowing = true;

                    for (int i = 0; i < loop.Links.Count; i++)
                    {
                        float carried = loop.LinkConductance(i);
                        if (carried > worst) worst = carried;
                    }
                }
            }

            return worst;
        }


        public static List<Row> Run(IList<Blueprints.Ship> ships, LabMode mode = LabMode.Parallel)
        {
            GameBlocks.Warm();
            return LabRun.Map(ships, Measure, mode);
        }

        public static List<Row> LastRows { get; private set; }


        private static float At(List<float> sorted, double q)
        {
            if (sorted.Count == 0) return 0f;
            int index = (int)Math.Round(q * (sorted.Count - 1));
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return sorted[index];
        }


        public static string Report(string path, int ships, LabMode mode = LabMode.Parallel)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("RETROFIT  (cooling fitted to ships somebody built)");
            sb.AppendLine();
            sb.Append("  full electrical load in shadow, ").Append(Seconds.ToString("n0"))
                .AppendLine(" simulated seconds, peak block temperature");
            sb.AppendLine("  cooling goes in the free cells nearest the hottest block, or nowhere");
            sb.AppendLine();

            string root = path ?? Blueprints.DefaultPath();
            if (root == null)
            {
                sb.AppendLine("  No blueprints anywhere. The corpus lives at:");
                sb.Append("    ").AppendLine(Blueprints.CorpusPath());
                return sb.ToString();
            }


            List<Blueprints.Ship> usable = Sample(root, ships);
            if (usable.Count == 0)
            {
                sb.Append("  No usable ships under ").AppendLine(root);
                return sb.ToString();
            }

            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

            List<Row> rows = Run(usable, mode);
            clock.Stop();
            LastRows = rows;


            List<float> bolted = new List<float>();

            List<float> plumbed = new List<float>();

            List<float> carried = new List<float>();
            int noRoomBolted = 0, noRoomPlumbed = 0, measured = 0, cold = 0, flowingLoops = 0;
            float sinkPlumbed = 0f, sinkCarried = 0f;

            List<float> hotConductances = new List<float>();

            List<float> aboveHulls = new List<float>();

            List<float> ceilings = new List<float>();

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row == null) continue;
                measured++;

                if (row.BareK < 320f) { cold++; continue; }

                if (row.BoltedPlaced == 0) noRoomBolted++;
                else bolted.Add(100f * row.BoltedSaved / row.BareK);

                if (row.PlumbedPipes == 0) noRoomPlumbed++;
                else
                {
                    plumbed.Add(100f * row.PlumbedSaved / row.BareK);
                    carried.Add(100f * row.TransportSaved / row.BareK);

                    if (row.LoopFlowing) flowingLoops++;
                    if (row.LoopSinkWattsPerKelvin > sinkPlumbed) sinkPlumbed = row.LoopSinkWattsPerKelvin;
                    if (row.CarriedSinkWattsPerKelvin > sinkCarried) sinkCarried = row.CarriedSinkWattsPerKelvin;
                    hotConductances.Add(row.HotBlockWattsPerKelvin);
                    aboveHulls.Add(row.AboveHullKelvin);

                    if (row.BareK > 0f) ceilings.Add(100f * row.AboveHullKelvin / row.BareK);
                }
            }

            bolted.Sort();
            plumbed.Sort();
            carried.Sort();
            hotConductances.Sort();
            aboveHulls.Sort();

            float hotConductance = At(hotConductances, 0.5);

            float aboveHull = At(aboveHulls, 0.5);
            ceilings.Sort();

            float ceiling = At(ceilings, 0.5);

            sb.Append("  ").Append(measured.ToString("n0")).Append(" ships measured in ")
                .Append(clock.Elapsed.TotalSeconds.ToString("n0")).Append(" s, ")
                .AppendLine(LabRun.Describe(mode));
            sb.Append("  ").Append(cold.ToString("n0"))
                .AppendLine(" never passed 320 K under load, so cooling them proves nothing");
            sb.AppendLine();

            sb.AppendLine("  fit            ships   no room     p10     p50     p90     max   (% of peak taken off)");
            Band(sb, "bolted", bolted, noRoomBolted);
            Band(sb, "plumbed", plumbed, noRoomPlumbed);
            Band(sb, "carried", carried, noRoomPlumbed);

            sb.AppendLine();
            sb.AppendLine("  A fit that takes single-figure percentages off a ship that is burning is");
            sb.AppendLine("  not a lever a player has. `no room` is the other half of the answer: cooling");
            sb.AppendLine("  that cannot be installed without demolishing the hull is not cooling either.");
            sb.AppendLine();
            sb.Append("  of the ").Append(plumbed.Count).Append(" plumbed hulls, ")
                .Append(flowingLoops).AppendLine(" had a ring that was circulating");
            sb.Append("  worst sink coupling: plumbed ").Append(sinkPlumbed.ToString("n0"))
                .Append(" W/K, carried ").Append(sinkCarried.ToString("n0")).AppendLine(" W/K");
            sb.Append("  the hot block already sheds ").Append(hotConductance.ToString("n0"))
                .AppendLine(" W/K into the hull it is welded to (median)");
            sb.Append("  and stands ").Append(aboveHull.ToString("n1"))
                .AppendLine(" K above the median block on its own grid (median)");
            sb.AppendLine();
            sb.Append("  so a path that moves heat INSIDE the grid can win at most ")
                .Append(ceiling.ToString("n2")).AppendLine(" % of the peak,");
            sb.Append("  and the plumbed ring wins ").Append(At(plumbed, 0.5).ToString("n2"))
                .AppendLine(" % of the peak, about a quarter of that bound.");
            sb.AppendLine("  The bound is the finding rather than the shortfall: a perfect internal");
            sb.AppendLine("  path is still single figures, because the hull is nearly as hot as the");
            sb.AppendLine("  block and there is nowhere inside the grid left to put the heat.");

            sb.AppendLine();
            sb.AppendLine("  `carried` is the plumbed hull again on the loop this mod shipped before");
            sb.AppendLine("  C42 and C43: the same ring, the same panels, a fluid that could not move");
            sb.AppendLine("  what they can shed. The gap between it and `plumbed` is what closing the");
            sb.AppendLine("  transport line bought, and is the part of G3 that is a number rather");
            sb.AppendLine("  than a design.");

            return sb.ToString();
        }


        private static void Band(StringBuilder sb, string label, List<float> sorted, int noRoom)
        {
            sb.Append("  ").Append(label.PadRight(12));
            sb.Append(sorted.Count.ToString("n0").PadLeft(7));
            sb.Append(noRoom.ToString("n0").PadLeft(10));

            if (sorted.Count == 0)
            {
                sb.AppendLine("        —       —       —       —");
                return;
            }

            double[] quantiles = { 0.10d, 0.50d, 0.90d, 1d };
            for (int i = 0; i < quantiles.Length; i++)
            {
                sb.Append(At(sorted, quantiles[i]).ToString("n2").PadLeft(8));
            }
            sb.AppendLine();
        }


        private static List<Blueprints.Ship> Sample(string root, int ships)
        {
            List<string> files = Blueprints.Files(root);
            files.Sort(StringComparer.Ordinal);

            long limit = (long)StiffnessLab.MaxMegabytes() * 1024L * 1024L;

            List<Blueprints.Ship> usable = new List<Blueprints.Ship>();
            int stride = ships > 0 && files.Count > ships ? files.Count / ships : 1;

            for (int i = 0; i < files.Count; i += stride)
            {
                if (ships > 0 && usable.Count >= ships) break;

                try
                {
                    if (new System.IO.FileInfo(files[i]).Length > limit) continue;
                }
                catch (Exception) { continue; }

                List<Blueprints.Ship> read = Blueprints.Read(files[i]);
                for (int s = 0; s < read.Count; s++)
                {
                    if (!read[s].IsVanilla) continue;
                    if (read[s].Blocks < CorpusLab.MinimumBlocks) continue;
                    usable.Add(read[s]);
                    break;
                }
            }

            return usable;
        }


        private static string Cell(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }


        public static string Csv(IList<Row> rows)
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ship,large,blocks,bare_k,hottest_block,bolted_n,bolted_k,plumbed_pipes,plumbed_k,transport_k");

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row == null) continue;

                sb.Append(CsvLine.Text(row.Ship)).Append(',');
                sb.Append(row.Large ? 1 : 0).Append(',').Append(row.Blocks).Append(',');
                sb.Append(Cell(row.BareK)).Append(',');
                sb.Append(CsvLine.Text(row.HottestBlock)).Append(',');
                sb.Append(row.BoltedPlaced).Append(',');
                sb.Append(Cell(row.BoltedK)).Append(',');
                sb.Append(row.PlumbedPipes).Append(',');
                sb.Append(Cell(row.PlumbedK)).Append(',');
                sb.AppendLine(Cell(row.TransportK));
            }

            return sb.ToString();
        }
    }
}

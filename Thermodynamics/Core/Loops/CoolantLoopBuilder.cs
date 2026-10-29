using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public static class CoolantLoopBuilder
    {

        public static List<CoolantLoop> FindLoops(GridModel grid, LoopThermalProperties properties, float initialTemperature)
        {

            return FindLoops(grid, properties, initialTemperature, null);
        }


        public static List<CoolantLoop> FindLoops(GridModel grid, LoopThermalProperties properties,
            float initialTemperature, SimulationWork work)
        {

            return FindLoops(grid, properties, initialTemperature, work, null);
        }


        public static List<CoolantLoop> FindLoops(GridModel grid, LoopThermalProperties properties,
            float initialTemperature, SimulationWork work, CoolantLoopDiagnostics diagnostics)
        {

            List<CoolantLoop> loops = new List<CoolantLoop>();
            if (grid == null) return loops;

            if (work != null) work.LoopSearches++;

            if (grid.CoolantBlockCount == 0) return loops;

            if (work != null) work.LoopSearchCells += grid.Blocks.Count;


            HashSet<long> claimed = new HashSet<long>();
            CoolantLoopSearch search = null;
            bool searchAttempted = false;

            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockInstance start = blocks[i];
                if (start.Model.Coolant == null) continue;
                if (start.Model.Coolant.LinkPorts.Length < 2) continue;
                if (claimed.Contains(start.Key)) continue;


                CoolantFault fault;
                List<BlockInstance> ring = search == null
                    ? TraceRing(grid, start) : search.Trace(start, out fault);
                if (ring == null)
                {
                    // Intact rings are already linear. Build the directional failure cache only
                    // when an unclaimed run fails, and attempt it once for custom-shape fallback.
                    if (!searchAttempted)
                    {
                        searchAttempted = true;
                        search = CoolantLoopSearch.TryCreate(grid);
                    }
                    continue;
                }


                CoolantLoop loop = new CoolantLoop(properties, initialTemperature);
                for (int r = 0; r < ring.Count; r++)
                {
                    loop.Pipes.Add(ring[r]);
                    claimed.Add(ring[r].Key);

                    if (ring[r].Model.Coolant == null || !ring[r].Model.Coolant.IsPump) continue;


                    CoolantPump pump = new CoolantPump();
                    pump.Block = ring[r];

                    pump.Direction = PumpDirection(grid, ring, r);
                    pump.MaxPowerWatts = ring[r].Model.Coolant.MaxPowerWatts;
                    loop.Pumps.Add(pump);
                }

                loop.HasPump = loop.Pumps.Count > 0;
                loop.ParcelLengthMetres = grid.GridSize;
                loop.RefreshSignature();
                loop.RefreshThermalMass();
                loop.RefreshFlow();
                loops.Add(loop);
            }

            if (diagnostics != null) Diagnose(grid, claimed, loops, diagnostics, search);

            return loops;
        }


        private static void Diagnose(GridModel grid, HashSet<long> claimed,
            List<CoolantLoop> loops, CoolantLoopDiagnostics diagnostics, CoolantLoopSearch search)
        {
            diagnostics.Loops = loops.Count;
            diagnostics.PipesInLoops = claimed.Count;

            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockInstance block = blocks[i];
                if (block.Model.Coolant == null) continue;
                if (claimed.Contains(block.Key)) continue;

                CoolantFault fault;
                if (search == null) TraceRing(grid, block, out fault);
                else search.Trace(block, out fault);
                diagnostics.Record(block, fault);
            }
        }


        private static int PumpDirection(GridModel grid, List<BlockInstance> ring, int index)
        {
            BlockInstance pump = ring[index];

            CoolantShape shape = pump.Model.Coolant;
            CoolantPort[] ports = shape == null ? null : shape.LinkPorts;
            if (ports == null || ports.Length < 2) return 1;

            BlockInstance next = ring[(index + 1) % ring.Count];
            if (next == pump) return 1;

            GridPort outlet = TransformPort(pump, ports[1]);
            return grid.GetAtCell(outlet.Target) == next ? 1 : -1;
        }


        public static List<BlockInstance> TraceRing(GridModel grid, BlockInstance start)
        {
            CoolantFault fault;

            return TraceRing(grid, start, out fault);
        }


        public static List<BlockInstance> TraceRing(GridModel grid, BlockInstance start, out CoolantFault fault)
        {
            fault = CoolantFault.None;
            if (grid == null || start == null || start.Model.Coolant == null) return null;

            CoolantPort[] startPorts = start.Model.Coolant.LinkPorts;
            if (startPorts == null || startPorts.Length < 2)
            {
                fault = CoolantFault.OpenEnd;
                return null;
            }


            List<BlockInstance> ring = new List<BlockInstance>();

            HashSet<long> visited = new HashSet<long>();

            ring.Add(start);
            visited.Add(start.Key);

            BlockInstance current = start;
            GridPort firstPort = TransformPort(start, startPorts[0]);
            GridPort exit = firstPort;

            int guard = grid.BlockCount + 1;

            while (guard-- > 0)
            {
                BlockInstance next = grid.GetAtCell(exit.Target);
                if (next == null)
                {
                    fault = CoolantFault.OpenEnd;
                    return null;
                }
                if (next.Model.Coolant == null)
                {
                    fault = CoolantFault.BlockedByNonCoolant;
                    return null;
                }

                GridPort entry;
                if (!TryFindPortFacing(next, exit.Target, -exit.Direction, out entry))
                {
                    fault = CoolantFault.PortsDoNotMeet;
                    return null;
                }

                if (next == start)
                {
                    if (!SamePort(entry, firstPort)) return ring;

                    fault = CoolantFault.DoubledBack;
                    return null;
                }

                if (visited.Contains(next.Key))
                {
                    fault = CoolantFault.BranchOrCrossing;
                    return null;
                }

                GridPort onward;
                if (!TryFindOtherPort(next, entry, out onward))
                {
                    fault = CoolantFault.OpenEnd;
                    return null;
                }

                ring.Add(next);
                visited.Add(next.Key);

                current = next;
                exit = onward;
            }

            fault = CoolantFault.BranchOrCrossing;
            return null;
        }


        private static bool TryFindPortFacing(BlockInstance block, Vector3I cell, Vector3I direction, out GridPort port)
        {
            CoolantShape shape = block.Model.Coolant;
            CoolantPort[] ports = shape == null ? null : shape.LinkPorts;
            int count = ports == null ? 0 : ports.Length;
            for (int i = 0; i < count; i++)
            {
                GridPort candidate = TransformPort(block, ports[i]);
                if (candidate.Cell == cell && candidate.Direction == direction)
                {
                    port = candidate;
                    return true;
                }
            }

            port = default(GridPort);
            return false;
        }


        private static bool TryFindOtherPort(BlockInstance block, GridPort entry, out GridPort other)
        {
            CoolantShape shape = block.Model.Coolant;
            CoolantPort[] ports = shape == null ? null : shape.LinkPorts;
            int count = ports == null ? 0 : ports.Length;
            for (int i = 0; i < count; i++)
            {
                GridPort candidate = TransformPort(block, ports[i]);
                if (SamePort(candidate, entry)) continue;
                other = candidate;
                return true;
            }

            other = default(GridPort);
            return false;
        }


        private static bool SamePort(GridPort a, GridPort b)
        {
            return a.Cell == b.Cell && a.Direction == b.Direction;
        }


        // Discovery needs one value at a time, not the independently owned public port list.
        // Read the current model and orientation each call; definitions remain mutable.
        internal static GridPort TransformPort(BlockInstance block, CoolantPort port)
        {
            return new GridPort(block.LocalToGrid(port.LocalCell),
                block.LocalDirectionToGrid(port.LocalDirection));
        }


        public static float PipeConductance(GridModel grid, BlockInstance pipe, LoopThermalProperties properties)
        {
            float area = grid.CellFaceArea * properties.PipeContactMultiplier;
            return properties.HeatTransferCoefficient * area;
        }


        public static float PlateConductance(GridModel grid, LoopThermalProperties properties)
        {
            float area = grid.CellFaceArea * properties.SinkContactMultiplier;
            return properties.HeatTransferCoefficient * area;
        }
    }
}

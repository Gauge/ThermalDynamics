using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    // Rebuild-local discovery for ordinary two-port plumbing. Each direction of an open
    // chain reaches one terminal fault, so cache directions rather than whole components:
    // the opposite end can have a different fault. Reciprocal two-port connections cannot
    // branch into a ring. Custom/degenerate shapes retain the general TraceRing behavior.
    internal sealed class CoolantLoopSearch
    {
        private sealed class Exit
        {
            public BlockInstance Block;
            public GridPort Port;
            public Exit Other;
            public Exit Next;
            public CoolantFault Fault;
            public long Visit;
        }

        private readonly Dictionary<BlockInstance, Exit> first = new Dictionary<BlockInstance, Exit>();
        private readonly List<Exit> path = new List<Exit>();
        private readonly List<BlockInstance> ring = new List<BlockInstance>();
        private long trace;

        public static CoolantLoopSearch TryCreate(GridModel grid)
        {
            CoolantLoopSearch search = new CoolantLoopSearch();
            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockInstance block = blocks[i];
                if (block.Model.Coolant == null) continue;
                if (block.Model.Coolant.LinkPorts.Length != 2) return null;

                CoolantPort[] ports = block.Model.Coolant.LinkPorts;
                GridPort firstPort = CoolantLoopBuilder.TransformPort(block, ports[0]);
                GridPort secondPort = CoolantLoopBuilder.TransformPort(block, ports[1]);
                if (!ValidPort(grid, block, firstPort) || !ValidPort(grid, block, secondPort)
                    || SamePort(firstPort, secondPort)) return null;

                Exit a = new Exit { Block = block, Port = firstPort };
                Exit b = new Exit { Block = block, Port = secondPort, Other = a };
                a.Other = b;
                search.first.Add(block, a);
            }

            foreach (Exit exit in search.first.Values)
            {
                search.Connect(grid, exit);
                search.Connect(grid, exit.Other);
            }
            return search;
        }

        private static bool ValidPort(GridModel grid, BlockInstance block, GridPort port)
        {
            Vector3I d = port.Direction;
            return Math.Abs((long)d.X) + Math.Abs((long)d.Y) + Math.Abs((long)d.Z) == 1
                && grid.GetAtCell(port.Cell) == block && grid.GetAtCell(port.Target) != block;
        }

        private static bool SamePort(GridPort a, GridPort b)
        {
            return a.Cell == b.Cell && a.Direction == b.Direction;
        }

        private void Connect(GridModel grid, Exit exit)
        {
            BlockInstance next = grid.GetAtCell(exit.Port.Target);
            if (next == null) { exit.Fault = CoolantFault.OpenEnd; return; }
            Exit entry;
            if (!first.TryGetValue(next, out entry))
            {
                exit.Fault = CoolantFault.BlockedByNonCoolant;
                return;
            }
            GridPort facing = new GridPort(exit.Port.Target, -exit.Port.Direction);
            if (!SamePort(entry.Port, facing)) entry = entry.Other;
            if (!SamePort(entry.Port, facing))
            {
                exit.Fault = CoolantFault.PortsDoNotMeet;
                return;
            }
            exit.Next = entry.Other;
        }

        // The returned ring is scratch storage, consumed before the next trace.
        public List<BlockInstance> Trace(BlockInstance start, out CoolantFault fault)
        {
            path.Clear();
            ring.Clear();
            // A cleared HashSet retains its largest bucket array; clearing it per short trace
            // after a long trace can reintroduce quadratic work. Stamp the cached exits instead.
            trace++;
            Exit initial = first[start];
            Exit exit = initial;
            while (true)
            {
                if (exit.Fault != CoolantFault.None)
                {
                    fault = exit.Fault;
                    for (int i = 0; i < path.Count; i++) path[i].Fault = fault;
                    return null;
                }
                if (exit.Visit == trace)
                {
                    fault = exit.Block == start
                        ? (exit == initial ? CoolantFault.None : CoolantFault.DoubledBack)
                        : CoolantFault.BranchOrCrossing;
                    return fault == CoolantFault.None ? ring : null;
                }
                exit.Visit = trace;
                exit.Other.Visit = trace;
                path.Add(exit);
                ring.Add(exit.Block);
                exit = exit.Next;
            }
        }
    }
}

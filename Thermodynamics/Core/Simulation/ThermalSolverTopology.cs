using System;
using System.Collections.Generic;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private int[] nodeFirstLink = new int[0];

        private int[] linkNextA = new int[0];
        private int[] linkNextB = new int[0];


        private readonly List<int> doomedLinks = new List<int>();


        private void EnsureNodeChainCapacity(int nodeCount)
        {
            if (nodeFirstLink.Length >= nodeCount) return;

            int size = Math.Max(16, nodeCount * 2);
            int previous = nodeFirstLink.Length;
            Array.Resize(ref nodeFirstLink, size);
            for (int i = previous; i < size; i++) nodeFirstLink[i] = -1;
        }


        private void EnsureLinkChainCapacity(int linkCount)
        {
            if (linkNextA.Length >= linkCount) return;

            int size = Math.Max(16, linkCount * 2);
            Array.Resize(ref linkNextA, size);
            Array.Resize(ref linkNextB, size);
        }


        private void ResetLinkChains()
        {
            EnsureNodeChainCapacity(nodes.Count);
            for (int i = 0; i < nodeFirstLink.Length; i++) nodeFirstLink[i] = -1;
        }


        private int NextLink(int link, int node)
        {
            return links[link].NodeA == node ? linkNextA[link] : linkNextB[link];
        }


        private void SetNextLink(int link, int node, int next)
        {
            if (links[link].NodeA == node) linkNextA[link] = next;
            else linkNextB[link] = next;
        }


        private void ChainLink(int link)
        {
            ThermalLink entry = links[link];

            EnsureNodeChainCapacity(nodes.Count);
            EnsureLinkChainCapacity(links.Count);

            linkNextA[link] = nodeFirstLink[entry.NodeA];
            nodeFirstLink[entry.NodeA] = link;

            linkNextB[link] = nodeFirstLink[entry.NodeB];
            nodeFirstLink[entry.NodeB] = link;
        }


        private void UnchainFrom(int node, int link)
        {
            if (node < 0 || node >= nodeFirstLink.Length) return;

            int current = nodeFirstLink[node];
            if (current == link)
            {

                nodeFirstLink[node] = NextLink(link, node);
                return;
            }

            while (current != -1)
            {

                int next = NextLink(current, node);
                if (next == link)
                {
                    SetNextLink(current, node, NextLink(link, node));
                    return;
                }
                current = next;
            }
        }


        private void RetargetLink(int node, int from, int to)
        {
            if (node < 0 || node >= nodeFirstLink.Length) return;

            if (nodeFirstLink[node] == from)
            {
                nodeFirstLink[node] = to;
                return;
            }

            int current = nodeFirstLink[node];
            while (current != -1)
            {

                int next = NextLink(current, node);
                if (next == from)
                {
                    SetNextLink(current, node, to);
                    return;
                }
                current = next;
            }
        }


        private void RemoveLinkAt(int link)
        {
            ThermalLink entry = links[link];

            if (conductanceIncludesBlocks)
            {
                nodeConductanceTotal[entry.NodeA] -= entry.Conductance;
                nodeConductanceTotal[entry.NodeB] -= entry.Conductance;
            }

            nodes[entry.NodeA].LinkCount--;
            nodes[entry.NodeB].LinkCount--;

            UnchainFrom(entry.NodeA, link);
            UnchainFrom(entry.NodeB, link);

            int last = links.Count - 1;
            if (link != last)
            {
                ThermalLink moved = links[last];

                links[link] = moved;
                linkNextA[link] = linkNextA[last];
                linkNextB[link] = linkNextB[last];

                linkA[link] = linkA[last];
                linkB[link] = linkB[last];
                linkConductance[link] = linkConductance[last];
                if (last < linkMassFactor.Length) linkMassFactor[link] = linkMassFactor[last];

                RetargetLink(moved.NodeA, last, link);
                RetargetLink(moved.NodeB, last, link);
            }

            links.RemoveAt(last);
            if (syncedLinks > links.Count) syncedLinks = links.Count;
        }


        private void DropLinksOf(ThermalNode node)
        {
            int index = node.Index;

            doomedLinks.Clear();
            for (int link = nodeFirstLink[index]; link != -1; link = NextLink(link, index))
            {
                doomedLinks.Add(link);
            }

            Work.LinksRemoved += doomedLinks.Count;

            doomedLinks.Sort();
            for (int i = doomedLinks.Count - 1; i >= 0; i--)
            {
                RemoveLinkAt(doomedLinks[i]);
            }

            nodeFirstLink[index] = -1;
        }


        private float SpillEnergyOf(ThermalNode node)
        {
            int index = node.Index;

            float capacity = 0f;
            spillTargets.Clear();

            for (int link = nodeFirstLink[index]; link != -1; link = NextLink(link, index))
            {
                ThermalLink edge = links[link];
                int other = edge.NodeA == index ? edge.NodeB : edge.NodeA;
                if (other == index) continue;

                ThermalNode neighbour = nodes[other];
                if (neighbour.ThermalMass <= 0f) continue;

                spillTargets.Add(neighbour);
                capacity += neighbour.ThermalMass;
            }

            if (capacity <= 0f || spillTargets.Count == 0) return 0f;

            float energy = node.Energy;
            if (energy <= 0f) return 0f;

            float rise = energy / capacity;
            for (int i = 0; i < spillTargets.Count; i++)
            {
                ThermalNode neighbour = spillTargets[i];
                neighbour.Temperature += rise;
                neighbour.StateDirty = true;
            }

            node.Temperature = 0f;
            node.StateDirty = true;

            return energy;
        }


        private readonly List<ThermalNode> spillTargets = new List<ThermalNode>();


        private void RemoveNodeIncremental(ThermalNode node)
        {
            int index = node.Index;

            Work.NodesRemoved++;

            EnsureBuffers();
            EnsureNodeChainCapacity(nodes.Count);

            DropLinksOf(node);

            int last = nodes.Count - 1;
            if (index != last)
            {
                ThermalNode moved = nodes[last];

                nodes[index] = moved;
                moved.Index = index;
                moved.Block.NodeIndex = index;

                nodeFirstLink[index] = nodeFirstLink[last];
                nodeConductanceTotal[index] = nodeConductanceTotal[last];

                moved.StateDirty = true;

                RepointNode(last, index);
            }

            nodeFirstLink[last] = -1;
            nodes.RemoveAt(last);
        }


        private void RepointNode(int from, int to)
        {
            for (int link = nodeFirstLink[to]; link != -1; )
            {
                ThermalLink entry = links[link];
                int next;

                if (entry.NodeA == from)
                {
                    next = linkNextA[link];
                    entry.NodeA = to;
                    linkA[link] = to;
                }
                else
                {
                    next = linkNextB[link];
                    entry.NodeB = to;
                    linkB[link] = to;
                }

                links[link] = entry;
                link = next;
            }

            for (int l = 0; l < loops.Count; l++)
            {
                List<LoopLink> loopLinks = loops[l].Links;
                for (int i = 0; i < loopLinks.Count; i++)
                {
                    if (loopLinks[i].NodeIndex != from) continue;

                    loopLinks[i] = new LoopLink(to, loopLinks[i].Conductance, loopLinks[i].SegmentIndex);
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                List<RoomLink> roomLinks = roomAir[r].Links;
                for (int i = 0; i < roomLinks.Count; i++)
                {
                    if (roomLinks[i].NodeIndex != from) continue;

                    roomLinks[i] = new RoomLink(to, roomLinks[i].Conductance);
                }
            }

            for (int p = 0; p < heatPumps.Count; p++)
            {
                HeatPumpDevice device = heatPumps[p];
                if (device.ColdNodeIndex == from) device.ColdNodeIndex = to;
                if (device.HotNodeIndex == from) device.HotNodeIndex = to;
            }
        }
        public bool CanonicalLinkOrder = true;


        private void CanonicaliseLinks()
        {
            int count = links.Count;
            int from = 0;

            while (from < count)
            {
                int node = links[from].NodeA;

                int to = from + 1;
                while (to < count && links[to].NodeA == node) to++;

                for (int i = from + 1; i < to; i++)
                {
                    ThermalLink moving = links[i];
                    int j = i - 1;

                    while (j >= from && links[j].NodeB > moving.NodeB)
                    {
                        links[j + 1] = links[j];
                        j--;
                    }

                    links[j + 1] = moving;
                }

                for (int i = from; i < to; i++) ChainLink(i);

                from = to;
            }
        }


        public void BuildLinksIfNeeded()
        {
            RebuildLinksIfNeeded();
        }


        private void RebuildLinksIfNeeded()
        {
            if (linksDirty)
            {
                RebuildLinks();
                return;
            }

            if (pendingLinkNodes.Count > 0) LinkPendingNodes();
        }


        public void RebuildLinks()
        {
            AbandonStep();

            Work.TopologyRebuilds++;
            Work.TopologyNodeVisits += nodes.Count;

            for (int i = 0; i < pendingLinkNodes.Count; i++) pendingLinkNodes[i].PendingLinks = false;
            pendingLinkNodes.Clear();

            links.Clear();
            syncedLinks = 0;

            int expected = nodes.Count * 2;
            if (links.Capacity < expected) links.Capacity = expected;

            EnsureBuffers();
            ResetLinkChains();
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].LinkCount = 0;
            }

            IBlockAdjacency adjacency = Adjacency;

            GridModel walked = adjacency as GridModel;

            CellBitset occupied = walked != null ? walked.OccupancyForQueries() : null;

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode a = nodes[i];

                neighbourScratch.Clear();
                neighbourFaces.Clear();
                if (walked != null) walked.GetNeighbours(a.Block, neighbourScratch, neighbourFaces, occupied);
                else adjacency.GetNeighbours(a.Block, neighbourScratch);

                for (int n = 0; n < neighbourScratch.Count; n++)
                {

                    ThermalNode b = GetNode(neighbourScratch[n]);
                    if (b == null) continue;

                    if (b.Index <= a.Index) continue;

                    int face = neighbourFaces.Count == neighbourScratch.Count
                        ? neighbourFaces[n]
                        : ConductionBuilder.ContactFace(a.Block, b.Block);
                    if (face < 0) continue;

                    int contacts = ConductionBuilder.CountContactFaces(a.Block, b.Block, face);
                    if (contacts <= 0) continue;

                    float conductance = ConductionBuilder.Conductance(
                        grid.GridSize, a.Block, b.Block, contacts, Face.Axis(face));
                    if (conductance <= 0f) continue;

                    links.Add(new ThermalLink(a.Index, b.Index, conductance, contacts));
                    a.LinkCount++;
                    b.LinkCount++;
                }
            }

            EnsureNodeChainCapacity(nodes.Count);
            EnsureLinkChainCapacity(links.Count);

            if (CanonicalLinkOrder) CanonicaliseLinks();
            else for (int link = 0; link < links.Count; link++) ChainLink(link);

            Work.LinksBuilt += links.Count;

            linksDirty = false;
            linkMassFactorFrom = 0;
            SyncLinkArrays();
            EnsureBuffers();
            RecomputeConductanceTotals();
        }


        private void SyncLinkArrays()
        {
            if (linkA.Length < links.Count)
            {
                int size = Math.Max(16, links.Count * 2);
                Array.Resize(ref linkA, size);
                Array.Resize(ref linkB, size);
                Array.Resize(ref linkConductance, size);
            }

            for (int i = syncedLinks; i < links.Count; i++)
            {
                ThermalLink link = links[i];
                linkA[i] = link.NodeA;
                linkB[i] = link.NodeB;
                linkConductance[i] = link.Conductance;
            }

            syncedLinks = links.Count;
        }


        private void LinkPendingNodes()
        {
            Work.TopologyRebuilds++;
            Work.TopologyNodeVisits += pendingLinkNodes.Count;


            bool buffersGrew = EnsureBuffers();

            IBlockAdjacency adjacency = Adjacency;

            GridModel walked = adjacency as GridModel;
            int firstNewLink = links.Count;

            for (int p = 0; p < pendingLinkNodes.Count; p++)
            {
                ThermalNode a = pendingLinkNodes[p];

                if (a.Index < 0 || a.Index >= nodes.Count || nodes[a.Index] != a) continue;

                neighbourScratch.Clear();
                neighbourFaces.Clear();
                if (walked != null) walked.GetNeighbours(a.Block, neighbourScratch, neighbourFaces);
                else adjacency.GetNeighbours(a.Block, neighbourScratch);

                for (int n = 0; n < neighbourScratch.Count; n++)
                {

                    ThermalNode b = GetNode(neighbourScratch[n]);
                    if (b == null) continue;

                    if (b.PendingLinks && b.Index <= a.Index) continue;

                    int face = neighbourFaces.Count == neighbourScratch.Count
                        ? neighbourFaces[n]
                        : ConductionBuilder.ContactFace(a.Block, b.Block);
                    if (face < 0) continue;

                    int contacts = ConductionBuilder.CountContactFaces(a.Block, b.Block, face);
                    if (contacts <= 0) continue;

                    float conductance = ConductionBuilder.Conductance(
                        grid.GridSize, a.Block, b.Block, contacts, Face.Axis(face));
                    if (conductance <= 0f) continue;

                    links.Add(new ThermalLink(a.Index, b.Index, conductance, contacts));
                    ChainLink(links.Count - 1);
                    a.LinkCount++;
                    b.LinkCount++;
                }
            }

            for (int p = 0; p < pendingLinkNodes.Count; p++)
            {
                pendingLinkNodes[p].PendingLinks = false;
            }
            pendingLinkNodes.Clear();

            Work.LinksBuilt += links.Count - firstNewLink;

            SyncLinkArrays();

            if (!buffersGrew) AddConductanceOfNewLinks(firstNewLink);

            if (firstNewLink < linkMassFactorFrom) linkMassFactorFrom = firstNewLink;
        }


        private void AddConductanceOfNewLinks(int firstNewLink)
        {
            if (!conductanceIncludesBlocks) return;
            for (int i = firstNewLink; i < links.Count; i++)
            {
                ThermalLink link = links[i];
                nodeConductanceTotal[link.NodeA] += link.Conductance;
                nodeConductanceTotal[link.NodeB] += link.Conductance;
            }
        }


    }
}

using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public partial class ThermalSolver
    {
        private readonly Dictionary<long, float> ventedRings = new Dictionary<long, float>();


        private void SpillDissolvedLoops(List<CoolantLoop> newLoops,
            Dictionary<long, float> previousFill)
        {
            if (loops.Count == 0) return;

            for (int i = 0; i < loops.Count; i++)
            {
                CoolantLoop dying = loops[i];

                bool survives = false;
                if (newLoops != null)
                {
                    for (int n = 0; n < newLoops.Count; n++)
                    {
                        if (newLoops[n].Signature != dying.Signature) continue;
                        survives = true;
                        break;
                    }
                }
                if (survives) continue;

                int pipes = dying.Pipes.Count;
                if (pipes <= 0) continue;

                bool lostAPipe = false;
                for (int p = 0; p < pipes; p++)
                {
                    BlockInstance pipe = dying.Pipes[p];
                    if (pipe.Cells.Length > 0 && grid.GetAtCell(pipe.Cells[0]) == pipe) continue;

                    lostAPipe = true;
                    break;
                }

                if (lostAPipe && dying.FillFraction > 0f)
                {
                    ventedRings[dying.Signature] = 0f;
                    dying.FillFraction = 0f;
                    continue;
                }

                float segmentMass = dying.ThermalMass / pipes;
                if (segmentMass <= 0f) continue;

                float segmentCapacity = segmentMass * dying.HeatTimeScale;

                for (int p = 0; p < pipes; p++)
                {

                    ThermalNode node = GetNode(dying.Pipes[p]);
                    if (node == null) continue;

                    float nodeMass = node.ThermalMass;
                    float combined = nodeMass + segmentMass;
                    if (combined <= 0f) continue;

                    float mixed = ((node.Temperature * nodeMass)
                                 + (dying.SegmentTemperature(p) * segmentMass)) / combined;

                    node.Temperature = mixed < ThermalConstants.MinimumTemperature
                        ? ThermalConstants.MinimumTemperature
                        : mixed;

                    node.HeldCoolantCapacity = node.HeldCoolantCapacity + segmentCapacity;
                }
            }
        }


        private void ReclaimHeldCoolant(CoolantLoop loop)
        {
            int pipes = loop.Pipes.Count;
            int parcels = loop.ParcelCount;
            if (pipes <= 0 || parcels <= 0) return;

            float parcelMass = loop.SegmentThermalMass;
            if (parcelMass <= 0f) return;

            float[] energy = null;
            float[] capacity = null;
            int[] representative = null;

            for (int p = 0; p < pipes; p++)
            {

                ThermalNode node = GetNode(loop.Pipes[p]);
                if (node == null || node.HeldCoolantCapacity <= 0f) continue;

                if (energy == null)
                {
                    energy = new float[parcels];
                    capacity = new float[parcels];
                    representative = new int[parcels];
                    for (int i = 0; i < parcels; i++) representative[i] = -1;
                }

                float share = node.HeldCoolantCapacity / loop.HeatTimeScale;

                int slot = loop.ParcelOf(p);
                energy[slot] += node.Temperature * share;
                capacity[slot] += share;
                if (representative[slot] < 0) representative[slot] = p;

                node.HeldCoolantCapacity = 0f;
            }

            if (energy == null) return;

            float standing = loop.Temperature;

            for (int slot = 0; slot < parcels; slot++)
            {
                if (representative[slot] < 0) continue;

                float covered = capacity[slot];
                if (covered > parcelMass) covered = parcelMass;

                float uncovered = parcelMass - covered;
                float landed = (energy[slot] * (covered / capacity[slot])) + (standing * uncovered);

                loop.SetSegmentTemperature(representative[slot], landed / parcelMass);
            }
        }


        public void SetLoops(List<CoolantLoop> newLoops)
        {
            Dictionary<long, float> previous = new Dictionary<long, float>();

            Dictionary<long, float> previousFill = new Dictionary<long, float>();

            for (int i = 0; i < loops.Count; i++)
            {
                previous[loops[i].Signature] = loops[i].Temperature;
                previousFill[loops[i].Signature] = loops[i].FillFraction;
            }

            Dictionary<long, CoolantPump> previousPumps = new Dictionary<long, CoolantPump>();
            for (int i = 0; i < loops.Count; i++)
            {
                IList<CoolantPump> pumps = loops[i].Pumps;
                for (int p = 0; p < pumps.Count; p++)
                {
                    if (pumps[p].Block == null) continue;
                    previousPumps[pumps[p].Block.Key] = pumps[p];
                }
            }

            SpillDissolvedLoops(newLoops, previousFill);

            loops.Clear();
            if (newLoops != null)
            {
                for (int i = 0; i < newLoops.Count; i++)
                {
                    CoolantLoop loop = newLoops[i];
                    float carried;
                    if (previous.TryGetValue(loop.Signature, out carried))
                    {
                        loop.Temperature = carried;
                    }

                    float carriedFill;
                    if (previousFill.TryGetValue(loop.Signature, out carriedFill))
                    {
                        loop.FillFraction = carriedFill;
                    }
                    else if (ventedRings.TryGetValue(loop.Signature, out carriedFill))
                    {
                        loop.FillFraction = carriedFill;
                        ventedRings.Remove(loop.Signature);
                    }

                    loop.HeatTimeScale = settings.HeatTimeScale;
                    loop.WellMixed = settings.WellMixedCoolant;

                    for (int p = 0; p < loop.Pumps.Count; p++)
                    {
                        CoolantPump fresh = loop.Pumps[p];
                        if (fresh.Block == null) continue;

                        CoolantPump kept;
                        if (!previousPumps.TryGetValue(fresh.Block.Key, out kept)) continue;

                        fresh.Speed = kept.Speed;
                        fresh.Enabled = kept.Enabled;
                        fresh.PowerAvailable = kept.PowerAvailable;
                        fresh.MaxPowerWatts = kept.MaxPowerWatts;
                    }

                    ReclaimHeldCoolant(loop);

                    loop.RefreshFlow();
                    BuildLoopLinks(loop);
                    loops.Add(loop);
                }
            }

            EnsureBuffers();
            conductanceTotalsDirty = true;
        }


        private void BuildLoopLinks(CoolantLoop loop)
        {
            loop.Links.Clear();

            for (int i = 0; i < loop.Pipes.Count; i++)
            {
                BlockInstance pipe = loop.Pipes[i];


                ThermalNode pipeNode = GetNode(pipe);
                if (pipeNode != null)
                {
                    loop.Links.Add(new LoopLink(
                        pipeNode.Index,
                        CoolantLoopBuilder.PipeConductance(grid, pipe, loop.Properties),
                        i));
                }

                List<GridPort> sinks = pipe.CoolantSinkPorts();
                for (int s = 0; s < sinks.Count; s++)
                {
                    BlockInstance target = grid.GetAtCell(sinks[s].Target);
                    if (target == null || target == pipe) continue;


                    ThermalNode targetNode = GetNode(target);
                    if (targetNode == null) continue;

                    loop.Links.Add(new LoopLink(
                        targetNode.Index,
                        CoolantLoopBuilder.PlateConductance(grid, loop.Properties),
                        i));
                }
            }
        }



        public void RefreshExposure(RoomMap rooms)
        {
            BeginExposureRefresh(rooms);
            while (StepExposureRefresh(int.MaxValue)) { }
        }

        private RoomMap exposureMap;
        private int exposureCursor;

        public bool ExposureRefreshPending
        {
            get { return exposureMap != null; }
        }


        public void BeginExposureRefresh(RoomMap rooms)
        {
            Work.ExposureRefreshes++;
            exposureMap = rooms;
            exposureCursor = 0;
        }


        public bool StepExposureRefresh(int nodeBudget)
        {
            if (exposureMap == null) return false;
            if (nodeBudget <= 0) return true;

            int end = exposureCursor + nodeBudget;
            if (end > nodes.Count) end = nodes.Count;

            Work.ExposureNodeVisits += end - exposureCursor;

            for (int i = exposureCursor; i < end; i++)
            {
                ThermalNode node = nodes[i];
                surfaces.GetExposedFaces(node.Block, exposureMap, exposureScratch);
                if (node.SetExposedFaces(exposureScratch)) Work.ExposureNodeWrites++;
            }

            exposureCursor = end;

            if (exposureCursor < nodes.Count) return true;

            exposureMap = null;
            exposureCursor = 0;
            return false;
        }


        public void RefreshExposureAround(RoomMap rooms, IList<int> roomIndices)
        {
            if (rooms == null || roomIndices == null) return;

            if (affected == null) affected = new HashSet<BlockInstance>();
            affected.Clear();

            CellBitset occupied = grid.Occupancy();

            for (int r = 0; r < roomIndices.Count; r++)
            {
                int index = roomIndices[r];
                if (index < 0 || index >= rooms.RoomCount) continue;

                foreach (Vector3I cell in rooms.CellsOf(index))
                {
                    long key = GridMath.Key(cell);
                    long slot = occupied.IndexOf(cell);

                    for (int face = 0; face < Face.Count; face++)
                    {
                        if (!occupied.ContainsIndex(slot + occupied.IndexStep(face))) continue;

                        BlockInstance block = grid.GetAtKey(key + GridMath.KeyByFace[face]);
                        if (block != null) affected.Add(block);
                    }

                    if (!occupied.ContainsIndex(slot)) continue;

                    BlockInstance occupant = grid.GetAtKey(key);
                    if (occupant != null) affected.Add(occupant);
                }
            }

            Work.ExposureRefreshes++;
            Work.ExposureNodeVisits += affected.Count;

            foreach (BlockInstance block in affected)
            {

                ThermalNode node = GetNode(block);
                if (node == null) continue;

                surfaces.GetExposedFaces(block, rooms, exposureScratch);
                if (node.SetExposedFaces(exposureScratch)) Work.ExposureNodeWrites++;
            }
        }

        private HashSet<BlockInstance> affected;


        private struct RememberedAir
        {
            public float Temperature;
            public float Pressure;
            public bool Initialised;
        }


        private readonly List<RoomAirNode> roomAirPool = new List<RoomAirNode>();

        private readonly Dictionary<Vector3I, RememberedAir> rememberedAir =
            new Dictionary<Vector3I, RememberedAir>(Vector3I.Comparer);

        private int[] roomContactFaces = new int[0];


        private readonly List<int> roomContactOrder = new List<int>();


        public void RebuildRoomAir(RoomMap rooms)
        {
            Work.RoomAirRebuilds++;
            if (rooms != null) Work.RoomAirRoomVisits += rooms.RoomCount;

            rememberedAir.Clear();
            for (int i = 0; i < roomAir.Count; i++)
            {
                RoomAirNode was = roomAir[i];

                RememberedAir remembered;
                remembered.Temperature = was.Temperature;
                remembered.Pressure = was.Pressure;
                remembered.Initialised = was.Initialised;
                rememberedAir[was.Anchor] = remembered;

                roomAirPool.Add(was);
            }

            roomAir.Clear();

            if (settings.EnableRoomAir && rooms != null)
            {
                float cellVolume = grid.GridSize * grid.GridSize * grid.GridSize;

                for (int r = 0; r < rooms.RoomCount; r++)
                {
                    if (rooms.IsVented(r)) continue;

                    RoomMap.RoomCells cells = rooms.CellsOf(r);
                    if (cells.Count == 0) continue;


                    RoomAirNode air = TakeRoomAirNode();
                    air.RoomIndex = r;

                    air.Anchor = LowestCell(cells);
                    air.CellCount = cells.Count;
                    air.Volume = cells.Count * cellVolume;

                    RememberedAir previous;
                    if (rememberedAir.TryGetValue(air.Anchor, out previous))
                    {
                        air.Temperature = previous.Temperature;
                        air.Pressure = previous.Pressure;
                        air.Initialised = previous.Initialised;
                    }
                    else
                    {
                        air.Temperature = Environment.AmbientTemperature;
                        air.Pressure = 0f;

                        air.Initialised = false;
                    }

                    air.AirDensity = settings.RoomAirDensity;
                    air.HeatTimeScale = settings.HeatTimeScale;

                    roomAir.Add(air);
                    BuildRoomLinks(air, rooms);
                }
            }

            rememberedAir.Clear();
            EnsureBuffers();
            conductanceTotalsDirty = true;
        }


        private RoomAirNode TakeRoomAirNode()
        {
            int last = roomAirPool.Count - 1;
            if (last < 0) return new RoomAirNode();

            RoomAirNode air = roomAirPool[last];
            roomAirPool.RemoveAt(last);
            return air;
        }


        private void BuildRoomLinks(RoomAirNode air, RoomMap rooms)
        {
            air.Links.Clear();
            if (!air.HasAir) return;
            if (air.RoomIndex < 0 || air.RoomIndex >= rooms.RoomCount) return;

            if (roomContactFaces.Length < nodes.Count) roomContactFaces = new int[nodes.Count];
            roomContactOrder.Clear();

            CellBitset occupied = grid.Occupancy();

            foreach (Vector3I cell in rooms.CellsOf(air.RoomIndex))
            {
                long key = GridMath.Key(cell);
                long slot = occupied.IndexOf(cell);

                Work.RoomAirFaceProbes += Face.Count;

                for (int face = 0; face < Face.Count; face++)
                {
                    if (!occupied.ContainsIndex(slot + occupied.IndexStep(face))) continue;

                    BlockInstance block = grid.GetAtKey(key + GridMath.KeyByFace[face]);
                    if (block == null) continue;

                    Work.RoomAirFaceHits++;


                    ThermalNode node = GetNode(block);
                    if (node == null) continue;

                    int index = node.Index;
                    if (roomContactFaces[index] == 0) roomContactOrder.Add(index);
                    roomContactFaces[index]++;
                }
            }

            roomContactOrder.Sort();

            float surfaceSum = 0f;
            int surfaceCount = 0;

            for (int i = 0; i < roomContactOrder.Count; i++)
            {
                int node = roomContactOrder[i];
                surfaceSum += nodes[node].Temperature;
                surfaceCount++;

                float area = roomContactFaces[node] * nodes[node].CellFaceArea;
                float conductance = settings.RoomConvectionCoefficient * area;

                if (conductance <= 0f) continue;

                air.Links.Add(new RoomLink(node, conductance));
            }

            for (int i = 0; i < roomContactOrder.Count; i++) roomContactFaces[roomContactOrder[i]] = 0;

            if (!air.Initialised && surfaceCount > 0)
            {
                air.Temperature = surfaceSum / surfaceCount;
                air.Initialised = true;
            }
        }


        public int RestoreRoomAir(IList<StoredRoom> stored)
        {
            if (stored == null || stored.Count == 0 || roomAir.Count == 0) return 0;

            Dictionary<Vector3I, float> byAnchor = new Dictionary<Vector3I, float>(stored.Count, Vector3I.Comparer);
            for (int i = 0; i < stored.Count; i++)
            {
                byAnchor[stored[i].Anchor] = stored[i].Temperature;
            }

            int restored = 0;
            for (int i = 0; i < roomAir.Count; i++)
            {
                float temperature;
                if (!byAnchor.TryGetValue(roomAir[i].Anchor, out temperature)) continue;

                roomAir[i].Temperature = Math.Max(ThermalConstants.MinimumTemperature, temperature);
                roomAir[i].Initialised = true;
                restored++;
            }

            return restored;
        }


        private static Vector3I LowestCell(RoomMap.RoomCells cells)
        {
            bool first = true;
            Vector3I lowest = Vector3I.Zero;

            foreach (Vector3I cell in cells)
            {
                if (first)
                {
                    lowest = cell;
                    first = false;
                    continue;
                }

                if (cell.Z < lowest.Z
                    || (cell.Z == lowest.Z && cell.Y < lowest.Y)
                    || (cell.Z == lowest.Z && cell.Y == lowest.Y && cell.X < lowest.X))
                {
                    lowest = cell;
                }
            }

            return lowest;
        }



        public void RebuildHeatPumps()
        {
            Work.HeatPumpRebuilds++;

            if (grid.HeatPumpBlockCount == 0)
            {
                heatPumps.Clear();
                return;
            }

            Work.HeatPumpNodeVisits += grid.Blocks.Count;

            Dictionary<long, HeatPumpDevice> previous = new Dictionary<long, HeatPumpDevice>();
            for (int i = 0; i < heatPumps.Count; i++)
            {
                if (heatPumps[i].Block == null) continue;
                previous[heatPumps[i].Block.Key] = heatPumps[i];
            }

            heatPumps.Clear();

            IList<BlockInstance> blocks = grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockInstance block = blocks[i];
                HeatPumpShape shape = block.Model.HeatPump;
                if (shape == null) continue;

                Vector3I coldCell, hotCell;
                if (!block.TryHeatPumpCells(out coldCell, out hotCell)) continue;


                HeatPumpDevice device = new HeatPumpDevice();
                device.Block = block;
                device.RatedWatts = shape.RatedWatts;
                device.MaxPowerWatts = shape.MaxPowerWatts;

                HeatPumpDevice carried;
                if (previous.TryGetValue(block.Key, out carried))
                {
                    device.Enabled = carried.Enabled;
                    device.PowerAvailable = carried.PowerAvailable;
                }


                device.ColdNodeIndex = NodeIndexAt(coldCell);

                device.HotNodeIndex = NodeIndexAt(hotCell);

                heatPumps.Add(device);
            }
        }


        private int NodeIndexAt(Vector3I cell)
        {
            BlockInstance block = grid.GetAtCell(cell);
            if (block == null) return -1;


            ThermalNode node = GetNode(block);
            return node == null ? -1 : node.Index;
        }


        public HeatPumpDevice GetHeatPump(BlockInstance block)
        {
            if (block == null) return null;

            for (int i = 0; i < heatPumps.Count; i++)
            {
                if (heatPumps[i].Block == block) return heatPumps[i];
            }
            return null;
        }


        private void AccumulateHeatPumps(float h)
        {
            if (!settings.EnableHeatPumps) return;

            float fraction = settings.HeatPumpCarnotFraction;
            float ceiling = settings.HeatPumpMaxCoefficient;

            for (int p = 0; p < heatPumps.Count; p++)
            {
                HeatPumpDevice pump = heatPumps[p];
                if (!pump.Enabled || !pump.IsConnected) continue;

                int cold = pump.ColdNodeIndex;
                int hot = pump.HotNodeIndex;
                if (cold >= nodes.Count || hot >= nodes.Count) continue;

                float coldTemperature = nodeTemperatures[cold];
                float hotTemperature = nodeTemperatures[hot];

                float coefficient = HeatPumpDevice.Coefficient(
                    coldTemperature, hotTemperature, fraction, ceiling);
                if (coefficient <= 0f) continue;

                float headroom = (coldTemperature - ThermalConstants.MinimumTemperature)
                    * nodeThermalMass[cold] / h;
                if (headroom <= 0f) continue;

                float settable = pump.SettablePowerWatts;

                pump.LastOptimalMarginKelvin = pump.RatedWatts > 0f
                    ? ((fraction * coldTemperature * settable) / pump.RatedWatts)
                        - (hotTemperature - coldTemperature)
                    : 0f;

                if (settable <= 0f) continue;


                float wanted = Limit(coefficient * settable, pump.RatedWatts, headroom);
                pump.DemandEnergy += (wanted / coefficient) * h;

                float available = settable * ThermalMath.Clamp01(pump.PowerAvailable);
                if (available <= 0f) continue;


                float lift = Limit(coefficient * available, pump.RatedWatts, headroom);
                float work = lift / coefficient;

                nodeWatts[cold] -= lift;
                nodeWatts[hot] += lift + work;

                pump.LiftedEnergy += lift * h;
                pump.PowerEnergy += work * h;
                pump.RejectedEnergy += (lift + work) * h;
            }
        }


        private static float Limit(float watts, float rating, float headroom)
        {
            if (watts > rating) watts = rating;
            if (watts > headroom) watts = headroom;
            return watts;
        }


        public RoomAirNode GetRoomAir(RoomMap rooms, Vector3I cell)
        {
            if (rooms == null) return null;

            int room = rooms.RoomIndexOf(cell);
            if (room < 0) return null;

            for (int i = 0; i < roomAir.Count; i++)
            {
                if (roomAir[i].RoomIndex == room) return roomAir[i];
            }
            return null;
        }


        public bool SetRoomPressure(RoomMap rooms, Vector3I cell, float pressure)
        {

            RoomAirNode air = GetRoomAir(rooms, cell);
            if (air == null) return false;

            if (pressure < 0f) pressure = 0f;
            if (pressure > 1f) pressure = 1f;
            if (air.Pressure == pressure) return true;

            bool hadAir = air.HasAir;
            air.Pressure = pressure;
            air.RefreshThermalMass();

            if (hadAir != air.HasAir)
            {
                BuildRoomLinks(air, rooms);
                conductanceTotalsDirty = true;
            }

            return true;
        }
    }
}

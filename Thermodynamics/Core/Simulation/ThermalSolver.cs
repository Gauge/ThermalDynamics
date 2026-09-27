using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public struct OverheatEvent
    {
        public BlockInstance Block;
        public float Temperature;
        public float Damage;


        public OverheatEvent(BlockInstance block, float temperature, float damage)
        {
            Block = block;
            Temperature = temperature;
            Damage = damage;
        }
    }

    public partial class ThermalSolver
    {
        public const float StabilitySafetyFactor = 0.5f;

        public int MaxSubsteps
        {
            get { return settings.MaxSubsteps; }
        }

        private readonly ThermalSettings settings;
        private readonly GridModel grid;
        private readonly SurfaceMap surfaces;


        private readonly List<ThermalNode> nodes = new List<ThermalNode>();

        private readonly List<ThermalLink> links = new List<ThermalLink>();

        private readonly List<CoolantLoop> loops = new List<CoolantLoop>();

        private readonly List<RoomAirNode> roomAir = new List<RoomAirNode>();

        private readonly List<HeatPumpDevice> heatPumps = new List<HeatPumpDevice>();

        private float[] nodeWatts = new float[0];
        private float[] nodeTemperatures = new float[0];

        private float[] nodeStepStart = new float[0];

        private float[] nodeConductanceTotal = new float[0];
        private float[] roomWatts = new float[0];

        private float[] loopEffectiveMass = new float[0];
        private float[] roomEffectiveMass = new float[0];

        private float[] nodeThermalMass = new float[0];
        private float[] nodeRadiation = new float[0];
        private float[] nodeGeneration = new float[0];
        private float[] nodeExposedArea = new float[0];
        private float[] nodeAbsorptivity = new float[0];
        private int[] nodeExposedFaces = new int[0];

        private float[] nodeCritical = new float[0];

        private float[] nodeFaceWeights = new float[0];


        private float[] nodeRelaxation = new float[0];

        private float[] parcelConductanceTotal = new float[0];

        private float[] nodeSolarRow = new float[0];
        private float[] nodeFrictionRow = new float[0];
        private float[] nodeConvectionRow = new float[0];

        private float[] nodeSourceRow = new float[0];

        private bool environmentRowsValid;


        private void InvalidateEnvironmentRows()
        {
            environmentRowsValid = false;
            heatGainRowTotalValid = false;
        }

        public bool PrecomputeEnvironment = true;

        public bool HoistHeatGainTotal = true;

        private float[] nodeSunLit = new float[0];

        private float[] nodeWindLit = new float[0];


        private readonly SunShadowMap sunShadow = new SunShadowMap();


        private readonly SunShadowMap windShadow = new SunShadowMap();


        public readonly List<SunShadowMap.Occluder> SunOccluders = new List<SunShadowMap.Occluder>();


        public void MarkSunOccludersChanged()
        {
            sunLitDirty = true;
            windLitDirty = true;
        }

        private const float SunRebuildCosine = 0.99939f;

        private const float WindRebuildCosine = 0.93969f;

        public int SunShadowBudget = 2048;

        public int SunLitBudget = 4096;

        private int sunLitCursor;
        private bool sunLitPending;

        private float sunLitFill = -1f;

        private float windLitFill = -1f;
        private int windLitCursor;
        private bool windLitPending;

        private bool windLitDirty = true;

        private bool sunLitDirty = true;

        private float[] linkMassFactor = new float[0];

        private int linkMassFactorFrom;

        private int[] linkA = new int[0];
        private int[] linkB = new int[0];
        private float[] linkConductance = new float[0];

        private readonly float[] sunWeights = new float[Face.Count];
        private readonly float[] windWeights = new float[Face.Count];

        private readonly float[] sourceWeights = new float[Face.Count];

        public bool CollectDiagnostics;

        public float LastEnvironmentWatts { get; private set; }

        public float LastVentedWatts
        {
            get { return LastEnvironmentWatts < 0f ? -LastEnvironmentWatts : 0f; }
        }

        public float LastHeatGainWatts { get; private set; }

        public float LastFrictionWatts { get; private set; }


        private float environmentWattsAccumulator;
        private float heatGainAccumulator;
        private float frictionAccumulator;


        private float heatGainRowTotal;

        private float frictionRowTotal;

        private bool heatGainRowTotalValid;


        private void ResetEnvironmentTotals()
        {
            environmentWattsAccumulator = 0f;
            heatGainAccumulator = 0f;
            frictionAccumulator = 0f;
        }


        private void SettleHeatGainRowTotal(bool summed)
        {
            if (summed)
            {
                heatGainRowTotal = heatGainAccumulator;
                frictionRowTotal = frictionAccumulator;
                heatGainRowTotalValid = true;
                return;
            }

            heatGainAccumulator += heatGainRowTotal;

            frictionAccumulator += frictionRowTotal;
        }


        private void PublishEnvironmentTotals()
        {
            LastEnvironmentWatts = environmentWattsAccumulator;
            LastHeatGainWatts = heatGainAccumulator;
            LastFrictionWatts = frictionAccumulator;
        }


        private readonly List<OverheatEvent> overheats = new List<OverheatEvent>();

        private float[] nodeOverheatDamage = new float[0];
        private float[] nodeOverheatPeak = new float[0];


        private readonly List<int> overheated = new List<int>();


        private readonly ThermalThresholds thresholds = new ThermalThresholds();

        private readonly List<ThresholdCrossing> crossings = new List<ThresholdCrossing>();
        private readonly int[] exposureScratch = new int[Face.Count];

        private readonly List<BlockInstance> neighbourScratch = new List<BlockInstance>();


        private readonly List<int> neighbourFaces = new List<int>();

        private IBlockAdjacency adjacency;


        public SimulationWork Work = new SimulationWork();

        private bool linksDirty = true;


        private readonly List<ThermalNode> pendingLinkNodes = new List<ThermalNode>();

        private int syncedLinks;

        private bool resyncAll = true;


        public ThermalSolver(ThermalSettings settings, GridModel grid, SurfaceMap surfaces)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (grid == null) throw new ArgumentNullException("grid");
            if (surfaces == null) throw new ArgumentNullException("surfaces");

            this.settings = settings;
            this.grid = grid;
            this.surfaces = surfaces;

            Environment = EnvironmentState.Vacuum(settings.VacuumTemperature);
        }

        public ThermalSettings Settings
        {
            get { return settings; }
        }

        public GridModel Grid
        {
            get { return grid; }
        }

        public IBlockAdjacency Adjacency
        {
            get { return adjacency ?? grid; }
            set
            {
                adjacency = value;
                linksDirty = true;
            }
        }

        public IList<ThermalNode> Nodes
        {
            get { return nodes; }
        }

        public IList<ThermalLink> Links
        {

            get { RebuildLinksIfNeeded(); return links; }
        }

        public int LinkCount
        {
            get { return links.Count; }
        }

        public IList<CoolantLoop> Loops
        {
            get { return loops; }
        }

        public IList<RoomAirNode> RoomAir
        {
            get { return roomAir; }
        }

        public IList<HeatPumpDevice> HeatPumps
        {
            get { return heatPumps; }
        }

        public IList<OverheatEvent> Overheats
        {
            get { return overheats; }
        }

        public ThermalThresholds Thresholds
        {
            get { return thresholds; }
        }

        public IList<ThresholdCrossing> Crossings
        {
            get { return crossings; }
        }

        public int LastSubsteps { get; private set; }

        public float LastRequiredSubsteps { get; private set; }


        public float NodeConductanceTotal(int index)
        {
            if (index < 0 || index >= nodes.Count) return 0f;
            if (index >= nodeConductanceTotal.Length) return 0f;

            return nodeConductanceTotal[index];
        }


        public float NodeSubstepDemand(int index)
        {
            EnvironmentState environment = Environment;

            return NodeSubstepDemand(index, ref environment);
        }


        public float NodeSubstepDemand(int index, ref EnvironmentState environment)
        {
            if (index < 0 || index >= nodes.Count) return 0f;

            if (index >= nodeConductanceTotal.Length) return 0f;

            ThermalNode node = nodes[index];

            float capacity = node.ThermalMass;
            if (capacity <= 0f) return 0f;


            StabilityTerms terms = StabilityEnvironment(ref environment);

            float rate = nodeConductanceTotal[index];

            if (terms.Exposed && node.TotalExposedFaces > 0)
            {
                if (terms.Radiating)
                {
                    float t = node.Temperature;
                    rate += 4f * node.RadiationCoefficient * t * t * t;
                }

                rate += terms.Convection * node.ExposedArea;
            }

            return (rate / capacity) * (settings.StepSeconds / StabilitySafetyFactor);
        }

        public bool LastStepWasClamped { get; private set; }

        public long StepCount { get; private set; }

        public EnvironmentState Environment { get; private set; }



        public void EnsureNodeCapacity(int count)
        {
            if (count <= 0) return;

            if (nodes.Capacity < count) nodes.Capacity = count;
            if (pendingLinkNodes.Capacity < count) pendingLinkNodes.Capacity = count;
        }


        public ThermalNode AddBlock(BlockInstance block, float initialTemperature)
        {
            if (block == null) throw new ArgumentNullException("block");
            if (block.Thermal.ExcludeFromSimulation) return null;


            ThermalNode existing = GetNode(block);
            if (existing != null) return existing;


            ThermalNode node = new ThermalNode(block, grid.GridSize, initialTemperature, settings.HeatTimeScale);
            node.Index = nodes.Count;
            nodes.Add(node);
            block.NodeIndex = node.Index;

            node.PendingLinks = true;
            pendingLinkNodes.Add(node);
            EnsureNodeChainCapacity(nodes.Count);
            nodeFirstLink[node.Index] = -1;

            sunLitDirty = true;
            windLitDirty = true;
            return node;
        }

        public float SpilledEnergy { get; private set; }


        public bool RemoveBlock(BlockInstance block)
        {
            if (block == null) return false;


            ThermalNode node = GetNode(block);
            if (node == null) return false;

            AbandonStep();

            SpilledEnergy = 0f;
            float critical = node.Thermal.CriticalTemperature;
            if (critical > 0f && node.Temperature >= critical)
            {
                BuildLinksIfNeeded();

                EnsureBuffers();
                EnsureNodeChainCapacity(nodes.Count);

                SpilledEnergy = SpillEnergyOf(node);
            }

            block.NodeIndex = -1;

            if (node.PendingLinks)
            {
                node.PendingLinks = false;
                pendingLinkNodes.Remove(node);
            }

            if (linksDirty)
            {
                nodes.RemoveAt(node.Index);
                for (int i = node.Index; i < nodes.Count; i++)
                {
                    nodes[i].Index = i;
                    nodes[i].Block.NodeIndex = i;
                }
                resyncAll = true;
            }
            else
            {
                RemoveNodeIncremental(node);
            }

            sunLitDirty = true;
            windLitDirty = true;

            hottestNode = -1;

            return true;
        }


        public bool RefreshBlockLinks(BlockInstance block)
        {
            if (block == null) return false;


            ThermalNode node = GetNode(block);
            if (node == null) return false;

            if (linksDirty || node.PendingLinks) return true;

            AbandonStep();

            EnsureBuffers();
            EnsureNodeChainCapacity(nodes.Count);
            DropLinksOf(node);

            node.PendingLinks = true;
            pendingLinkNodes.Add(node);
            return true;
        }


        public void RefreshExposureOf(BlockInstance block, RoomMap rooms)
        {
            if (block == null) return;


            ThermalNode node = GetNode(block);
            if (node == null) return;

            Work.ExposureRefreshes++;
            Work.ExposureNodeVisits++;

            surfaces.GetExposedFaces(block, rooms, exposureScratch);
            if (node.SetExposedFaces(exposureScratch)) Work.ExposureNodeWrites++;
        }


        public ThermalNode GetNode(BlockInstance block)
        {
            if (block == null) return null;

            int index = block.NodeIndex;
            if (index < 0 || index >= nodes.Count) return null;

            ThermalNode node = nodes[index];
            return node != null && ReferenceEquals(node.Block, block) ? node : null;
        }


        // Removes a bounded physical-equivalent energy between steps; the caller owns exclusive access.
        public bool TryRemovePhysicalHeat(BlockInstance block, double joules, float minimumKelvin,
            out double removedJoules)
        {
            removedJoules = 0d;
            if (StepInFlight || double.IsNaN(joules) || double.IsInfinity(joules) || joules < 0d
                || float.IsNaN(minimumKelvin) || float.IsInfinity(minimumKelvin)) return false;

            ThermalNode node = GetNode(block);
            if (node == null) return false;
            float before = node.Temperature;
            double capacity = (double)node.ThermalMass * node.HeatTimeScale;
            if (float.IsNaN(before) || float.IsInfinity(before) || before < ThermalConstants.MinimumTemperature
                || double.IsNaN(capacity) || double.IsInfinity(capacity) || capacity <= 0d) return false;

            float floor = Math.Max(ThermalConstants.MinimumTemperature, minimumKelvin);
            if (joules == 0d || before <= floor) return true;
            float after = (float)Math.Max(floor, before - joules / capacity);
            double removed = ((double)before - after) * capacity;
            if (removed > joules)
            {
                // Move a positive normal float up exactly one ULP without allocating or reinterpreting bits.
                float warmer = (float)(after + (double)after / 16777216d);
                if (warmer == after) warmer = (float)(after + (double)after / 8388608d);
                after = Math.Min(before, warmer);
                removed = ((double)before - after) * capacity;
            }
            if (removed < 0d || removed > joules) return false;

            node.Temperature = after;
            if (after != before) hottestNode = -1;
            removedJoules = removed;
            return true;
        }


        public ThermalNode GetNodeAt(Vector3I cell)
        {
            return GetNode(grid.GetAtCell(cell));
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

            CellBitset occupied = walked != null ? walked.Occupancy() : null;

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
            ResetLinkChains();

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
            for (int i = firstNewLink; i < links.Count; i++)
            {
                ThermalLink link = links[i];
                nodeConductanceTotal[link.NodeA] += link.Conductance;
                nodeConductanceTotal[link.NodeB] += link.Conductance;
            }
        }

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


        public void RefreshHeatGeneration()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].RefreshHeatGeneration();
            }
        }



        internal void SyncNodeState()
        {
            Work.NodeStateSyncs++;

            bool all = resyncAll;
            resyncAll = false;
            if (all) Work.FullNodeResyncs++;

            if (all) lowestCritical = float.PositiveInfinity;

            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];

                float temperature = node.Temperature;
                nodeTemperatures[i] = temperature;
                nodeStepStart[i] = temperature;

                if (!all && !node.StateDirty) continue;
                node.StateDirty = false;

                if (nodeThermalMass[i] != node.ThermalMass) linkMassFactorFrom = 0;

                nodeThermalMass[i] = node.ThermalMass;
                nodeRadiation[i] = node.RadiationCoefficient;
                nodeGeneration[i] = node.HeatGenerationWatts;
                nodeExposedArea[i] = node.ExposedArea;
                nodeAbsorptivity[i] = node.Thermal.EffectiveSolarAbsorptivity;
                nodeCritical[i] = node.Thermal.CriticalTemperature;
                if (nodeCritical[i] > 0f && nodeCritical[i] < lowestCritical)
                {
                    lowestCritical = nodeCritical[i];
                }

                int total = node.TotalExposedFaces;
                nodeExposedFaces[i] = total;

                int b = i * Face.Count;
                if (total <= 0)
                {
                    for (int f = 0; f < Face.Count; f++) nodeFaceWeights[b + f] = 0f;
                    continue;
                }

                float inverse = 1f / total;
                for (int f = 0; f < Face.Count; f++)
                {
                    nodeFaceWeights[b + f] = node.GetExposedFaces(f) * inverse;
                }
            }
        }

        private const float ClampBindingMargin = 0.9999f;


        private bool ClampCanBind(float h)
        {
            if (h <= 0f) return false;

            int nodeCount = nodes.Count;
            if (nodeCount > nodeConductanceTotal.Length) nodeCount = nodeConductanceTotal.Length;
            if (nodeCount > nodeThermalMass.Length) nodeCount = nodeThermalMass.Length;

            for (int i = 0; i < nodeCount; i++)
            {
                if (h * nodeConductanceTotal[i] >= ClampBindingMargin * nodeThermalMass[i]) return true;
            }

            int linkCount = links.Count;
            if (linkCount > linkConductance.Length) linkCount = linkConductance.Length;
            if (linkCount > linkMassFactor.Length) linkCount = linkMassFactor.Length;

            for (int i = 0; i < linkCount; i++)
            {
                if (h * linkConductance[i] >= ClampBindingMargin * linkMassFactor[i]) return true;
            }

            for (int l = 0; l < loops.Count; l++)
            {
                if (h * SegmentConductance(l) >= ClampBindingMargin * EffectiveLoopMass(l)) return true;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                if (!roomAir[r].HasAir) continue;
                if (h * RoomConductance(r) >= ClampBindingMargin * EffectiveRoomMass(r)) return true;
            }

            return false;
        }


        private void RefreshLinkMassFactors()
        {
            int from = linkMassFactorFrom;
            if (from >= links.Count) return;
            linkMassFactorFrom = int.MaxValue;

            if (linkMassFactor.Length < links.Count)
            {
                Array.Resize(ref linkMassFactor, Math.Max(16, links.Count * 2));
            }

            for (int i = from; i < links.Count; i++)
            {
                float massA = nodeThermalMass[links[i].NodeA];
                float massB = nodeThermalMass[links[i].NodeB];
                float combined = massA + massB;
                linkMassFactor[i] = combined <= 0f ? 0f : (massA * massB) / combined;
            }
        }


        private static void ResolveDirection(ref Vector3 direction, float[] weights)
        {
            for (int f = 0; f < Face.Count; f++)
            {
                float dot = Vector3.Dot(Face.Normals[f], direction);
                weights[f] = dot > 0f ? dot : 0f;
            }
        }

        private struct EnvironmentPlan
        {
            public bool EnvironmentEnabled;
            public bool Radiating;
            public bool Convecting;
            public bool Windy;
            public bool SolarEnabled;
            public bool FrictionEnabled;
            public bool SourcesEnabled;
            public bool Generating;
            public bool Diagnostics;

            public bool GenerationOnly;

            public float RadiationShare;
            public float FrictionScale;
        }


        private float RelaxationFactor(int node, float h)
        {
            if (!ConductionClampLive || h <= 0f) return 1f;

            float conductance = nodeConductanceTotal[node];
            if (conductance <= 0f) return 1f;

            float mass = nodeThermalMass[node];
            if (mass <= 0f) return 1f;

            float stable = mass / (h * conductance);
            return stable >= 1f ? 1f : stable;
        }


        private EnvironmentPlan PlanEnvironment(ref EnvironmentState env)
        {

            EnvironmentPlan plan = new EnvironmentPlan();

            plan.Radiating = settings.EnableEnvironment && settings.EnableRadiation;
            plan.EnvironmentEnabled = plan.Radiating;
            plan.SolarEnabled = settings.EnableSolarHeat && !env.IsSolarOccluded && env.SolarEnergy > 0f;
            plan.SourcesEnabled = settings.EnableHeatSources && env.HeatSourceCount > 0;
            plan.FrictionEnabled = env.FrictionActive;
            plan.Generating = settings.EnableWasteHeat;
            plan.Diagnostics = diagnosticsSubstep;

            plan.Convecting = settings.EnableEnvironment && settings.EnableConvection
                && env.AtmosphereFactor > 0f && env.ConvectionCoefficient > 0f;

            if (plan.Convecting) plan.EnvironmentEnabled = true;

            plan.GenerationOnly = !plan.EnvironmentEnabled && !plan.SolarEnabled
                && !plan.FrictionEnabled && !plan.SourcesEnabled;

            if (plan.GenerationOnly) return plan;

            plan.Windy = plan.Convecting && env.WindSpeed > 0f;
            plan.RadiationShare = 1f - env.AtmosphereFactor;

            float airCubed = env.WindSpeed * env.WindSpeed * env.WindSpeed;
            plan.FrictionScale = settings.FrictionScale * env.AirDensity * airCubed;

            if (plan.Windy || plan.FrictionEnabled)
            {
                Vector3 wind = env.WindDirectionLocal;
                ResolveDirection(ref wind, windWeights);

                if (RefreshWindShadow(ref wind)) InvalidateEnvironmentRows();
            }

            if (plan.SolarEnabled)
            {
                Vector3 sun = env.SunDirectionLocal;
                ResolveDirection(ref sun, sunWeights);

                if (RefreshSunShadow(ref sun)) InvalidateEnvironmentRows();
            }

            return plan;
        }

        private void AccumulateEnvironmentRange(ref EnvironmentState env, ref EnvironmentPlan plan,
            float h, int from, int to)
        {
            bool diagnostics = plan.Diagnostics;

            if (plan.GenerationOnly)
            {
                if (ConductionClampLive && (!environmentRowsValid || !PrecomputeEnvironment))
                {
                    for (int i = from; i < to; i++)
                    {

                        nodeRelaxation[i] = RelaxationFactor(i, h);
                    }
                }

                bool summing = !HoistHeatGainTotal || !heatGainRowTotalValid;

                if (plan.Generating)
                {
                    float generated = 0f;

                    for (int i = from; i < to; i++)
                    {
                        float generation = nodeGeneration[i];
                        nodeWatts[i] = generation;
                        if (summing) generated += generation;
                    }

                    if (summing) heatGainAccumulator += generated;
                }
                else
                {
                    Array.Clear(nodeWatts, from, to - from);
                }
                if (diagnostics)
                {
                    for (int i = from; i < to; i++) ClearEnvironmentDiagnostics(i);
                }

                if (to >= nodes.Count && plan.Generating) SettleHeatGainRowTotal(summing);
                if (PrecomputeEnvironment && to >= nodes.Count) environmentRowsValid = true;
                return;
            }

            bool generating = plan.Generating;
            bool clampRelaxation = settings.ClampEnvironmentOvershoot && h > 0f;
            float inverseH = h > 0f ? 1f / h : 0f;
            bool environmentEnabled = plan.EnvironmentEnabled;
            bool radiating = plan.Radiating;
            bool convecting = plan.Convecting;
            bool windy = plan.Windy;
            bool solarEnabled = plan.SolarEnabled;
            bool frictionEnabled = plan.FrictionEnabled;
            float radiationShare = plan.RadiationShare;
            float frictionScale = plan.FrictionScale;

            bool shielded = settings.EnableWindwardShielding
                && nodeWindLit.Length >= nodes.Count * Face.Count;

            bool fill = !environmentRowsValid || !PrecomputeEnvironment;

            bool fillRelaxation = fill && ConductionClampLive;

            bool summingRows = !HoistHeatGainTotal || !heatGainRowTotalValid;

            for (int i = from; i < to; i++)
            {
                if (fillRelaxation) nodeRelaxation[i] = RelaxationFactor(i, h);

                if (nodeExposedFaces[i] <= 0)
                {
                    if (fill)
                    {
                        nodeSolarRow[i] = 0f;
                        nodeFrictionRow[i] = 0f;
                        nodeConvectionRow[i] = 0f;
                        nodeSourceRow[i] = generating ? nodeGeneration[i] : 0f;
                    }

                    float buried = nodeSourceRow[i];
                    nodeWatts[i] = buried;
                    if (summingRows) heatGainAccumulator += buried;
                    if (diagnostics) ClearEnvironmentDiagnostics(i);
                    continue;
                }

                float temperature = nodeTemperatures[i];

                float radiationWatts = 0f;
                float convectionWatts = 0f;

                if (fill)
                {
                    float area = nodeExposedArea[i];

                    int b = i * Face.Count;
                    float f0 = nodeFaceWeights[b];
                    float f1 = nodeFaceWeights[b + 1];
                    float f2 = nodeFaceWeights[b + 2];
                    float f3 = nodeFaceWeights[b + 3];
                    float f4 = nodeFaceWeights[b + 4];
                    float f5 = nodeFaceWeights[b + 5];

                    float wind = 0f;
                    if (windy || frictionEnabled)
                    {
                        DragProfile profile = nodes[i].Drag;
                        wind = shielded
                            ? (f0 * windWeights[0] * nodeWindLit[b] * profile[0])
                                + (f1 * windWeights[1] * nodeWindLit[b + 1] * profile[1])
                                + (f2 * windWeights[2] * nodeWindLit[b + 2] * profile[2])
                                + (f3 * windWeights[3] * nodeWindLit[b + 3] * profile[3])
                                + (f4 * windWeights[4] * nodeWindLit[b + 4] * profile[4])
                                + (f5 * windWeights[5] * nodeWindLit[b + 5] * profile[5])
                            : (f0 * windWeights[0] * profile[0]) + (f1 * windWeights[1] * profile[1])
                                + (f2 * windWeights[2] * profile[2]) + (f3 * windWeights[3] * profile[3])
                                + (f4 * windWeights[4] * profile[4]) + (f5 * windWeights[5] * profile[5]);
                    }

                    float windFactor = windy ? 1f + wind : 1f;

                    nodeConvectionRow[i] = convecting
                        ? -env.ConvectionCoefficient * area * windFactor
                        : 0f;

                    float solar = 0f;
                    if (solarEnabled)
                    {
                        float lit = (f0 * nodeSunLit[b] * sunWeights[0])
                            + (f1 * nodeSunLit[b + 1] * sunWeights[1])
                            + (f2 * nodeSunLit[b + 2] * sunWeights[2])
                            + (f3 * nodeSunLit[b + 3] * sunWeights[3])
                            + (f4 * nodeSunLit[b + 4] * sunWeights[4])
                            + (f5 * nodeSunLit[b + 5] * sunWeights[5]);

                        solar = env.SolarEnergy * nodeAbsorptivity[i] * lit * area;
                    }
                    nodeSolarRow[i] = solar;

                    float friction = frictionEnabled ? frictionScale * area * wind : 0f;

                    nodeFrictionRow[i] = friction;

                    nodeSourceRow[i] = (generating ? nodeGeneration[i] : 0f) + solar + friction;
                }

                float source = nodeSourceRow[i];
                float watts = source;

                if (environmentEnabled)
                {
                    float radiation = 0f;
                    if (radiating)
                    {
                        float squared = temperature * temperature;
                        radiation = -nodeRadiation[i] * ((squared * squared) - env.AmbientTemperaturePow4);
                    }

                    float convection = 0f;
                    if (convecting)
                    {
                        convection = nodeConvectionRow[i] * (temperature - env.AmbientTemperature);
                    }

                    radiationWatts = radiationShare * radiation;
                    convectionWatts = env.AtmosphereFactor * convection;

                    float relaxation = radiationWatts + convectionWatts;

                    if (clampRelaxation)
                    {

                        float capped = ClampRelaxation(relaxation,
                            (env.AmbientTemperature - temperature) * nodeThermalMass[i] * inverseH);

                        if (capped != relaxation)
                        {
                            float scale = relaxation == 0f ? 0f : capped / relaxation;
                            radiationWatts *= scale;
                            convectionWatts *= scale;
                            relaxation = capped;
                        }
                    }

                    watts += relaxation;
                }

                nodeWatts[i] = watts;

                environmentWattsAccumulator += radiationWatts + convectionWatts;
                if (summingRows)
                {
                    heatGainAccumulator += source;

                    frictionAccumulator += nodeFrictionRow[i];
                }

                if (!diagnostics) continue;

                ThermalNode node = nodes[i];
                node.LastRadiationWatts = radiationWatts;
                node.LastConvectionWatts = convectionWatts;
                node.LastSolarWatts = nodeSolarRow[i];
                node.LastFrictionWatts = nodeFrictionRow[i];
                node.LastHeatSourceWatts = 0f;
            }

            if (to >= nodes.Count) SettleHeatGainRowTotal(summingRows);
            if (fill && to >= nodes.Count) Work.EnvironmentRowFills++;
            if (PrecomputeEnvironment && to >= nodes.Count) environmentRowsValid = true;
        }


        private void AccumulateHeatSources(ref EnvironmentState env, bool diagnostics)
        {
            for (int s = 0; s < env.HeatSourceCount; s++)
            {
                HeatSourceState source = env.HeatSources[s];
                if (source.Irradiance <= 0f) continue;

                Vector3 direction = source.DirectionLocal;
                ResolveDirection(ref direction, sourceWeights);

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodeExposedFaces[i] <= 0) continue;

                    float watts = source.Irradiance * nodeAbsorptivity[i]

                        * Weighted(i, sourceWeights) * nodeExposedArea[i];
                    if (watts == 0f) continue;

                    nodeWatts[i] += watts;
                    heatGainAccumulator += watts;
                    if (diagnostics) nodes[i].LastHeatSourceWatts += watts;
                }
            }
        }


        private bool RefreshSunShadow(ref Vector3 sunLocal)
        {
            if (!settings.SolarSelfShadowing)
            {
                if (sunShadow.IsBuilt || sunShadow.IsRunning || sunLitDirty)
                {
                    sunShadow.Clear();
                    BeginSunLit(1f);
                    sunLitDirty = false;
                }


                return StepSunLit(SunLitBudget);
            }

            if (sunLitDirty || sunShadow.NeedsRestart(ref sunLocal, SunRebuildCosine))
            {
                sunShadow.Restart(grid, sunLocal, SunOccluders);
                sunLitDirty = false;
            }

            if (sunShadow.Step(SunShadowBudget)) BeginSunLit(-1f);


            return StepSunLit(SunLitBudget);
        }


        private bool RefreshWindShadow(ref Vector3 windLocal)
        {
            if (!settings.EnableWindwardShielding)
            {
                if (windShadow.IsBuilt || windShadow.IsRunning)
                {
                    windShadow.Clear();
                    BeginWindLit(1f);
                }


                return StepWindLit(SunLitBudget);
            }

            if (windLitDirty)
            {
                windShadow.Restart(grid, windLocal, SunOccluders);
                windLitDirty = false;
            }
            else if (!windShadow.IsRunning && windShadow.NeedsRestart(ref windLocal, WindRebuildCosine))
            {
                windShadow.Restart(grid, windLocal, SunOccluders);
            }

            if (windShadow.Step(SunShadowBudget)) BeginWindLit(-1f);


            return StepWindLit(SunLitBudget);
        }


        private void BeginWindLit(float fill)
        {
            windLitFill = fill;
            windLitCursor = 0;
            windLitPending = true;
        }


        private bool StepWindLit(int nodeBudget)
        {
            if (!windLitPending) return false;

            if (windLitCursor >= nodes.Count)
            {
                windLitPending = false;
                return false;
            }

            if (nodeWindLit.Length < nodes.Count * Face.Count) return false;

            int end = Math.Min(nodes.Count, windLitCursor + nodeBudget);

            if (windLitFill >= 0f)
            {
                for (int i = windLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++) nodeWindLit[b + f] = windLitFill;
                }
            }
            else
            {
                for (int i = windLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++)
                    {
                        nodeWindLit[b + f] = windShadow.FaceLitFraction(nodes[i].Block, f);
                    }
                }
            }

            windLitCursor = end;
            if (windLitCursor >= nodes.Count) windLitPending = false;
            return true;
        }


        private void BeginSunLit(float fill)
        {
            sunLitFill = fill;
            sunLitCursor = 0;
            sunLitPending = true;
        }


        private bool StepSunLit(int nodeBudget)
        {
            if (!sunLitPending) return false;

            if (sunLitCursor >= nodes.Count)
            {
                sunLitPending = false;
                return false;
            }

            int end = sunLitCursor + nodeBudget;
            if (end > nodes.Count) end = nodes.Count;

            if (sunLitFill >= 0f)
            {
                for (int i = sunLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++) nodeSunLit[b + f] = sunLitFill;
                }
            }
            else
            {
                for (int i = sunLitCursor; i < end; i++)
                {
                    int b = i * Face.Count;
                    for (int f = 0; f < Face.Count; f++)
                    {
                        nodeSunLit[b + f] = sunShadow.FaceLitFraction(nodes[i].Block, f);
                    }
                }
            }

            sunLitCursor = end;
            if (sunLitCursor >= nodes.Count) sunLitPending = false;
            return true;
        }


        public void FinishSunLit()
        {
            while (sunLitPending) StepSunLit(int.MaxValue);
        }

        public bool SunLitRefreshPending
        {
            get { return sunLitPending; }
        }

        public SunShadowMap SunShadow { get { return sunShadow; } }

        public SunShadowMap WindShadow { get { return windShadow; } }

        public int WindLitLength { get { return nodeWindLit.Length; } }


        public float SunLitFraction(int node, int face)
        {
            int index = (node * Face.Count) + face;
            return index >= 0 && index < nodeSunLit.Length ? nodeSunLit[index] : 1f;
        }


        private float Weighted(int node, float[] weights)
        {
            int b = node * Face.Count;
            return (nodeFaceWeights[b] * weights[0])
                + (nodeFaceWeights[b + 1] * weights[1])
                + (nodeFaceWeights[b + 2] * weights[2])
                + (nodeFaceWeights[b + 3] * weights[3])
                + (nodeFaceWeights[b + 4] * weights[4])
                + (nodeFaceWeights[b + 5] * weights[5]);
        }


        private void PublishOverheats()
        {
            for (int i = 0; i < overheated.Count; i++)
            {
                int index = overheated[i];
                overheats.Add(new OverheatEvent(
                    nodes[index].Block, nodeOverheatPeak[index], nodeOverheatDamage[index]));
            }
        }


        private void ClearOverheatAccumulator()
        {
            for (int i = 0; i < overheated.Count; i++)
            {
                nodeOverheatDamage[overheated[i]] = 0f;
                nodeOverheatPeak[overheated[i]] = 0f;
            }

            overheated.Clear();
        }


        private void ClearEnvironmentDiagnostics()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                ClearEnvironmentDiagnostics(i);
            }
        }


        private void ClearEnvironmentDiagnostics(int i)
        {
            ThermalNode node = nodes[i];
            node.LastRadiationWatts = 0f;
            node.LastConvectionWatts = 0f;
            node.LastSolarWatts = 0f;
            node.LastFrictionWatts = 0f;
            node.LastHeatSourceWatts = 0f;
        }


        private void ClearConductionDiagnostics()
        {
            if (!diagnosticsSubstep) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].LastConductionWatts = 0f;
            }
        }


        private void AccumulateConductionRange(float h, int from, int to)
        {
            bool clamp = ConductionClampLive;
            bool diagnostics = diagnosticsSubstep;

            if (!settings.EnableConduction) return;

            float inverseH = h > 0f ? 1f / h : 0f;

            if (to > links.Count) to = links.Count;
            int[] fromIndex = linkA;
            int[] toIndex = linkB;
            float[] conductance = linkConductance;
            float[] massFactor = linkMassFactor;
            float[] temperatures = nodeTemperatures;
            float[] watts = nodeWatts;
            float[] relaxation = nodeRelaxation;

            for (int i = from; i < to; i++)
            {
                int a = fromIndex[i];
                int b = toIndex[i];

                float difference = temperatures[b] - temperatures[a];
                if (difference == 0f) continue;

                float exchange = conductance[i] * difference;

                if (clamp)
                {
                    float scale = relaxation[a] < relaxation[b] ? relaxation[a] : relaxation[b];
                    if (scale < 1f) exchange *= scale;

                    float maxWatts = difference * massFactor[i] * inverseH;
                    if (exchange > 0f)
                    {
                        if (exchange > maxWatts) exchange = maxWatts;
                    }

                    else if (exchange < maxWatts)
                    {
                        exchange = maxWatts;
                    }
                }

                watts[a] += exchange;
                watts[b] -= exchange;

                if (!diagnostics) continue;

                nodes[a].LastConductionWatts += exchange;
                nodes[b].LastConductionWatts -= exchange;
            }
        }


        private void AccumulateLoops(float h)
        {
            if (!settings.EnableCoolantLoops) return;

            bool clamp = ConductionClampLive;

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                float[] watts = loop.SegmentWatts;

                float relaxation = 1f;
                if (clamp && h > 0f)
                {
                    if (parcelConductanceTotal.Length < loop.PipeCount)
                    {
                        parcelConductanceTotal = new float[loop.PipeCount];
                    }
                    for (int i = 0; i < loop.PipeCount; i++) parcelConductanceTotal[i] = 0f;

                    for (int i = 0; i < loop.Links.Count; i++)
                    {
                        LoopLink probe = loop.Links[i];
                        if (probe.SegmentIndex < 0 || probe.SegmentIndex >= loop.PipeCount) continue;
                        parcelConductanceTotal[loop.ParcelOf(probe.SegmentIndex)] +=
                            loop.LinkConductance(i);
                    }

                    float mass = loop.SegmentThermalMass;
                    for (int i = 0; i < loop.PipeCount; i++)
                    {
                        float total = parcelConductanceTotal[i];
                        if (total <= 0f || mass <= 0f) continue;

                        float stable = mass / (h * total);
                        if (stable < relaxation) relaxation = stable;
                    }
                }

                for (int i = 0; i < loop.Links.Count; i++)
                {
                    LoopLink link = loop.Links[i];
                    if (link.NodeIndex < 0 || link.NodeIndex >= nodes.Count) continue;
                    if (link.SegmentIndex < 0 || link.SegmentIndex >= loop.PipeCount) continue;

                    int parcel = loop.ParcelOf(link.SegmentIndex);
                    if (parcel >= watts.Length) continue;

                    float difference = loop.SegmentTemperature(link.SegmentIndex)
                                     - nodeTemperatures[link.NodeIndex];
                    float exchange = loop.LinkConductance(i) * difference;

                    if (clamp)
                    {

                        exchange = ClampExchange(
                            exchange, h, difference,
                            loop.SegmentThermalMass,
                            nodeThermalMass[link.NodeIndex]);

                        float scale = relaxation;
                        if (nodeRelaxation[link.NodeIndex] < scale) scale = nodeRelaxation[link.NodeIndex];
                        if (scale < 1f) exchange *= scale;
                    }

                    nodeWatts[link.NodeIndex] += exchange;
                    watts[parcel] -= exchange;

                    if (exchange < 0f) loop.AbsorbedEnergy -= exchange * h;
                    else loop.RejectedEnergy += exchange * h;
                }
            }
        }


        private void AccumulateRoomAir(float h)
        {
            if (!settings.EnableRoomAir) return;

            bool clamp = ConductionClampLive;
            bool diagnostics = CollectDiagnostics;

            if (diagnostics)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    nodes[i].LastRoomWatts = 0f;
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float airTemperature = air.Temperature;

                float roomRelaxation = 1f;
                if (clamp && h > 0f)
                {

                    float mass = EffectiveRoomMass(r);

                    float total = RoomConductance(r);
                    if (mass > 0f && total > 0f)
                    {
                        float stable = mass / (h * total);
                        if (stable < 1f) roomRelaxation = stable;
                    }
                }

                for (int i = 0; i < air.Links.Count; i++)
                {
                    RoomLink link = air.Links[i];
                    if (link.NodeIndex < 0 || link.NodeIndex >= nodes.Count) continue;

                    float difference = airTemperature - nodeTemperatures[link.NodeIndex];
                    float watts = link.Conductance * difference;

                    if (clamp)
                    {

                        watts = ClampExchange(
                            watts, h, difference,
                            air.ThermalMass,
                            nodeThermalMass[link.NodeIndex]);

                        float scale = roomRelaxation;
                        if (nodeRelaxation[link.NodeIndex] < scale) scale = nodeRelaxation[link.NodeIndex];
                        if (scale < 1f) watts *= scale;
                    }

                    nodeWatts[link.NodeIndex] += watts;
                    roomWatts[r] -= watts;

                    if (diagnostics) nodes[link.NodeIndex].LastRoomWatts += watts;
                }
            }
        }


        public static float ClampRelaxation(float watts, float wattsToEquilibrium)
        {
            if (watts > 0f)
            {
                if (wattsToEquilibrium <= 0f) return 0f;
                return watts > wattsToEquilibrium ? wattsToEquilibrium : watts;
            }

            if (watts < 0f)
            {
                if (wattsToEquilibrium >= 0f) return 0f;
                return watts < wattsToEquilibrium ? wattsToEquilibrium : watts;
            }

            return 0f;
        }


        public static float ClampExchange(float watts, float h, float difference, float massA, float massB)
        {
            if (h <= 0f) return watts;

            float combined = massA + massB;
            if (combined <= 0f) return watts;

            float maxEnergy = difference * (massA * massB / combined);
            float energy = watts * h;

            if (Math.Abs(energy) <= Math.Abs(maxEnergy)) return watts;
            return maxEnergy / h;
        }


        private void ApplyNodeWattsRange(float h, int from, int to)
        {
            bool damageEnabled = settings.EnableDamage;

            bool perSecond = settings.DamageIsPerSecond;

            for (int i = from; i < to; i++)
            {
                float previous = nodeTemperatures[i];
                float updated = previous + (nodeWatts[i] * h / nodeThermalMass[i]);
                if (updated < ThermalConstants.MinimumTemperature)
                {
                    updated = ThermalConstants.MinimumTemperature;
                }

                nodeTemperatures[i] = updated;

                if (!damageEnabled) continue;

                if (updated <= lowestCritical) continue;

                float critical = nodeCritical[i];
                if (critical <= 0f || updated <= critical) continue;

                float damage = (updated - critical) * nodes[i].Thermal.OverheatDamagePerKelvin;
                if (perSecond) damage *= h;
                if (damage <= 0f) continue;

                if (nodeOverheatDamage[i] == 0f) overheated.Add(i);
                nodeOverheatDamage[i] += damage;
                if (updated > nodeOverheatPeak[i]) nodeOverheatPeak[i] = updated;
            }
        }


        private void ApplyCoupledWatts(float h)
        {
            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];

                float mass = EffectiveLoopMass(l);
                float[] watts = loop.SegmentWatts;

                for (int i = 0; i < watts.Length; i++)
                {
                    loop.ApplyParcelWatts(i, watts[i], h, mass);
                }

                loop.Advect(h);
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float updated = air.Temperature + (roomWatts[r] * h / EffectiveRoomMass(r));
                air.Temperature = updated < ThermalConstants.MinimumTemperature
                    ? ThermalConstants.MinimumTemperature
                    : updated;
            }
        }



        public float RequiredSubsteps(float deltaSeconds)
        {
            PrepareStepState();

            return RequiredSubstepsFromState(deltaSeconds);
        }


        public float RequiredSubsteps(float deltaSeconds, EnvironmentState environment)
        {
            Environment = environment;

            return RequiredSubsteps(deltaSeconds);
        }


        private void PrepareStepState()
        {
            RebuildLinksIfNeeded();
            EnsureBuffers();
            SyncNodeState();
            RecomputeConductanceTotalsIfNeeded();
            ApplyThermalMassFloor();
        }

        private struct StabilityTerms
        {
            public bool Exposed;
            public bool Radiating;
            public float Convection;
        }


        private StabilityTerms StabilityEnvironment()
        {
            EnvironmentState environment = Environment;

            return StabilityEnvironment(ref environment);
        }


        private StabilityTerms StabilityEnvironment(ref EnvironmentState environment)
        {

            StabilityTerms terms = new StabilityTerms();

            terms.Radiating = settings.EnableEnvironment && settings.EnableRadiation;
            bool convecting = settings.EnableEnvironment && settings.EnableConvection;
            terms.Convection = convecting
                ? environment.ConvectionCoefficient * environment.AtmosphereFactor
                : 0f;
            terms.Exposed = terms.Radiating || convecting;
            return terms;
        }


        private float NodeStabilityRate(int i, ref StabilityTerms terms)
        {
            float rate = nodeConductanceTotal[i];

            if (!terms.Exposed || nodeExposedFaces[i] <= 0) return rate;

            if (terms.Radiating)
            {
                float t = nodeTemperatures[i];
                rate += 4f * nodeRadiation[i] * t * t * t;
            }

            return rate + (terms.Convection * nodeExposedArea[i]);
        }


        private float RequiredSubstepsFromState(float deltaSeconds)
        {
            Work.StabilityEstimates++;

            float worst = 0f;


            StabilityTerms terms = StabilityEnvironment();

            for (int i = 0; i < nodes.Count; i++)
            {

                float perNode = NodeStabilityRate(i, ref terms) / nodeThermalMass[i];
                if (perNode > worst) worst = perNode;
            }

            for (int l = 0; l < loops.Count; l++)
            {

                float perLoop = SegmentConductance(l) / EffectiveLoopMass(l);
                if (perLoop > worst) worst = perLoop;

            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                if (!roomAir[r].HasAir) continue;


                float perRoom = RoomConductance(r) / EffectiveRoomMass(r);
                if (perRoom > worst) worst = perRoom;
            }

            if (worst <= 0f) return 1f;
            return (deltaSeconds * worst) / StabilitySafetyFactor;
        }

        public class SubstepProfile
        {
            public static readonly float[] DemandEdges =
                { 0.5f, 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f };

            public static readonly int[] ProjectedCaps = { 32, 16, 8, 6, 4, 3, 2, 1 };

            public float StepSeconds;
            public int Nodes;
            public int Links;

            public float RequiredSubsteps;

            public float RequiredSubstepsInForce;

            public float WorstNodeDemand;
            public int WorstNodeIndex = -1;

            public float WorstNodeConductionShare;

            public float WorstRoomAirDemand;
            public int WorstRoomAirIndex = -1;
            public float WorstLoopDemand;
            public int WorstLoopIndex = -1;

            public readonly long[] Buckets = new long[DemandEdges.Length + 1];

            public readonly long[] CapNodesFloored = new long[ProjectedCaps.Length];
            public readonly float[] CapRequiredSubsteps = new float[ProjectedCaps.Length];

            public long EnvironmentDominatedNodes;
        }


        public void PrepareForSteps()
        {
            if (StepInFlight) return;
            PrepareStepState();

            RefreshLinkMassFactors();
        }


        public SubstepProfile ProfileSubsteps()
        {
            if (!StepInFlight)
            {
                RebuildLinksIfNeeded();
                EnsureBuffers();
                SyncNodeState();
                RecomputeConductanceTotalsIfNeeded();
                ApplyThermalMassFloor();
            }


            SubstepProfile profile = new SubstepProfile();
            profile.StepSeconds = settings.StepSeconds;
            profile.Links = links.Count;

            float scale = settings.StepSeconds / StabilitySafetyFactor;

            StabilityTerms terms = StabilityEnvironment();

            float[] edges = SubstepProfile.DemandEdges;
            int[] caps = SubstepProfile.ProjectedCaps;

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                float demand = loop.ThermalMass <= 0f
                    ? 0f
                    : (LoopConductance(l) / loop.ThermalMass) * scale;

                if (demand > profile.WorstLoopDemand)
                {
                    profile.WorstLoopDemand = demand;
                    profile.WorstLoopIndex = l;
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                float demand = air.ThermalMass <= 0f
                    ? 0f
                    : (RoomConductance(r) / air.ThermalMass) * scale;

                if (demand > profile.WorstRoomAirDemand)
                {
                    profile.WorstRoomAirDemand = demand;
                    profile.WorstRoomAirIndex = r;
                }
            }

            float coupled = profile.WorstLoopDemand > profile.WorstRoomAirDemand
                ? profile.WorstLoopDemand
                : profile.WorstRoomAirDemand;

            for (int c = 0; c < caps.Length; c++)
            {
                profile.CapNodesFloored[c] = 0;
                profile.CapRequiredSubsteps[c] = coupled > caps[c] ? caps[c] : coupled;
            }

            int count = nodes.Count;
            if (count > nodeConductanceTotal.Length) count = nodeConductanceTotal.Length;
            profile.Nodes = count;

            for (int i = 0; i < count; i++)
            {

                float rate = NodeStabilityRate(i, ref terms);
                float conduction = nodeConductanceTotal[i];

                float capacity = nodes[i].ThermalMass;
                float demand = capacity <= 0f ? 0f : (rate / capacity) * scale;

                if (demand > profile.WorstNodeDemand)
                {
                    profile.WorstNodeDemand = demand;
                    profile.WorstNodeIndex = i;
                    profile.WorstNodeConductionShare = rate <= 0f ? 0f : conduction / rate;
                }

                if (rate > 0f && conduction < rate * 0.5f && demand > 1f)
                {
                    profile.EnvironmentDominatedNodes++;
                }

                int bucket = edges.Length;
                for (int e = 0; e < edges.Length; e++)
                {
                    if (demand < edges[e]) { bucket = e; break; }
                }
                profile.Buckets[bucket]++;

                for (int c = 0; c < caps.Length; c++)
                {
                    float capped = demand > caps[c] ? caps[c] : demand;
                    if (demand > caps[c]) profile.CapNodesFloored[c]++;
                    if (capped > profile.CapRequiredSubsteps[c]) profile.CapRequiredSubsteps[c] = capped;
                }
            }

            float worst = profile.WorstNodeDemand > coupled ? profile.WorstNodeDemand : coupled;
            profile.RequiredSubsteps = worst <= 0f ? 1f : worst;


            profile.RequiredSubstepsInForce = RequiredSubstepsFromState(settings.StepSeconds);

            return profile;
        }


        private int ClampSubsteps(float required)
        {
            if (required <= 1f) return 1;

            int substeps = (int)Math.Ceiling(required);
            if (substeps > MaxSubsteps) substeps = MaxSubsteps;
            return substeps;
        }

        private bool conductanceTotalsDirty = true;


        private void RecomputeConductanceTotalsIfNeeded()
        {
            if (!conductanceTotalsDirty) return;
            RecomputeConductanceTotals();
        }

        public int AdaptiveSubstepFloor;

        public int EffectiveSubstepFloor
        {
            get
            {
                int world = settings.MaxSubstepsPerBlock;
                int grid = AdaptiveSubstepFloor;

                if (world <= 0) return grid <= 0 ? 0 : grid;
                if (grid <= 0) return world;

                return world < grid ? world : grid;
            }
        }


        private void ApplyThermalMassFloor()
        {
            FlooredNodes = 0;

            int cap = EffectiveSubstepFloor;
            if (cap <= 0) return;

            float step = settings.StepSeconds;
            if (step <= 0f) return;

            float perRate = step / (StabilitySafetyFactor * cap);
            bool moved = false;


            StabilityTerms terms = StabilityEnvironment();

            int count = nodes.Count;
            for (int i = 0; i < count; i++)
            {
                float real = nodes[i].ThermalMass;

                float floor = NodeStabilityRate(i, ref terms) * perRate;
                float wanted = real < floor ? floor : real;

                if (nodeThermalMass[i] != wanted)
                {
                    nodeThermalMass[i] = wanted;
                    moved = true;
                }

                if (wanted > real) FlooredNodes++;
            }

            if (moved) linkMassFactorFrom = 0;

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];

                float floor = SegmentConductance(l) * perRate;
                loopEffectiveMass[l] = loop.SegmentThermalMass < floor ? floor : loop.SegmentThermalMass;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];

                float floor = RoomConductance(r) * perRate;
                roomEffectiveMass[r] = air.ThermalMass < floor ? floor : air.ThermalMass;
            }
        }

        private float[] loopConductanceTotal = new float[0];
        private float[] roomConductanceTotal = new float[0];


        private float LoopConductance(int index)
        {
            return index >= 0 && index < loopConductanceTotal.Length ? loopConductanceTotal[index] : 0f;
        }


        private float SegmentConductance(int index)
        {
            if (index < 0 || index >= loops.Count) return 0f;

            CoolantLoop loop = loops[index];
            int count = loop.ParcelCount;
            if (count <= 0) return 0f;

            if (segmentConductanceScratch.Length < count)
            {
                segmentConductanceScratch = new float[Math.Max(16, count * 2)];
            }
            Array.Clear(segmentConductanceScratch, 0, count);

            for (int i = 0; i < loop.Links.Count; i++)
            {
                LoopLink link = loop.Links[i];
                if (link.SegmentIndex < 0 || link.SegmentIndex >= loop.PipeCount) continue;

                int parcel = loop.ParcelOf(link.SegmentIndex);
                if (parcel < 0 || parcel >= count) continue;

                segmentConductanceScratch[parcel] += loop.LinkConductance(i);
            }

            float worst = 0f;
            for (int i = 0; i < count; i++)
            {
                if (segmentConductanceScratch[i] > worst) worst = segmentConductanceScratch[i];
            }
            return worst;
        }

        private float[] segmentConductanceScratch = new float[0];


        private float RoomConductance(int index)
        {
            return index >= 0 && index < roomConductanceTotal.Length ? roomConductanceTotal[index] : 0f;
        }


        private void RecomputeCoupledConductance()
        {
            if (loopConductanceTotal.Length < loops.Count)
            {
                loopConductanceTotal = new float[Math.Max(4, loops.Count * 2)];
            }

            if (roomConductanceTotal.Length < roomAir.Count)
            {
                roomConductanceTotal = new float[Math.Max(4, roomAir.Count * 2)];
            }

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                float total = 0f;
                for (int i = 0; i < loop.Links.Count; i++) total += loop.LinkConductance(i);
                loopConductanceTotal[l] = total;
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir)
                {
                    roomConductanceTotal[r] = 0f;
                    continue;
                }

                float total = 0f;
                for (int i = 0; i < air.Links.Count; i++) total += air.Links[i].Conductance;
                roomConductanceTotal[r] = total;
            }
        }


        private float EffectiveLoopMass(int index)
        {
            if (EffectiveSubstepFloor <= 0 || index >= loopEffectiveMass.Length
                || loopEffectiveMass[index] <= 0f)
            {
                return loops[index].SegmentThermalMass;
            }

            return loopEffectiveMass[index];
        }


        private float EffectiveRoomMass(int index)
        {
            if (EffectiveSubstepFloor <= 0 || index >= roomEffectiveMass.Length
                || roomEffectiveMass[index] <= 0f)
            {
                return roomAir[index].ThermalMass;
            }

            return roomEffectiveMass[index];
        }

        public int FlooredNodes { get; private set; }


        private void RecomputeConductanceTotals()
        {
            Work.ConductanceRecomputes++;
            conductanceTotalsDirty = false;
            EnsureBuffers();
            RecomputeCoupledConductance();
            for (int i = 0; i < nodes.Count; i++)
            {
                nodeConductanceTotal[i] = 0f;
            }

            for (int i = 0; i < links.Count; i++)
            {
                ThermalLink link = links[i];
                nodeConductanceTotal[link.NodeA] += link.Conductance;
                nodeConductanceTotal[link.NodeB] += link.Conductance;
            }

            for (int l = 0; l < loops.Count; l++)
            {
                CoolantLoop loop = loops[l];
                for (int i = 0; i < loop.Links.Count; i++)
                {
                    LoopLink link = loop.Links[i];
                    if (link.NodeIndex >= 0 && link.NodeIndex < nodeConductanceTotal.Length)
                    {
                        nodeConductanceTotal[link.NodeIndex] += link.Conductance;
                    }
                }
            }

            for (int r = 0; r < roomAir.Count; r++)
            {
                RoomAirNode air = roomAir[r];
                if (!air.HasAir) continue;

                for (int i = 0; i < air.Links.Count; i++)
                {
                    RoomLink link = air.Links[i];
                    if (link.NodeIndex >= 0 && link.NodeIndex < nodeConductanceTotal.Length)
                    {
                        nodeConductanceTotal[link.NodeIndex] += link.Conductance;
                    }
                }
            }
        }


        private bool EnsureBuffers()
        {
            bool grew = false;

            if (nodeWatts.Length < nodes.Count)
            {
                grew = true;

                AbandonStep();

                conductanceTotalsDirty = true;

                int size = Math.Max(16, nodes.Count + (nodes.Count / 4) + 16);
                nodeWatts = new float[size];
                nodeTemperatures = new float[size];
                nodeStepStart = new float[size];
                nodeConductanceTotal = new float[size];
                resyncAll = true;
                sunLitDirty = true;
            windLitDirty = true;
                nodeThermalMass = new float[size];
                nodeRadiation = new float[size];
                nodeGeneration = new float[size];
                nodeExposedArea = new float[size];
                nodeAbsorptivity = new float[size];
                nodeCritical = new float[size];
                nodeExposedFaces = new int[size];
                nodeRelaxation = new float[size];
                nodeSolarRow = new float[size];
                nodeFrictionRow = new float[size];
                nodeConvectionRow = new float[size];
                nodeSourceRow = new float[size];
                nodeOverheatDamage = new float[size];
                nodeOverheatPeak = new float[size];
                InvalidateEnvironmentRows();
                nodeFaceWeights = new float[size * Face.Count];
                nodeSunLit = new float[size * Face.Count];

                nodeWindLit = settings.EnableWindwardShielding
                    ? new float[size * Face.Count]
                    : new float[0];

            }
            if (loopEffectiveMass.Length < loops.Count)
            {
                loopEffectiveMass = new float[Math.Max(4, loops.Count * 2)];
            }
            if (roomWatts.Length < roomAir.Count)
            {
                roomWatts = new float[Math.Max(4, roomAir.Count * 2)];
                roomEffectiveMass = new float[roomWatts.Length];
            }

            return grew;
        }


        public float TotalEnergy
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < nodes.Count; i++)
                {
                    total += nodes[i].Energy;
                }
                for (int i = 0; i < loops.Count; i++)
                {
                    total += loops[i].Energy;
                }
                for (int i = 0; i < roomAir.Count; i++)
                {
                    if (roomAir[i].HasAir) total += roomAir[i].Energy;
                }
                return total;
            }
        }

        private int hottestNode = -1;


        public ThermalNode HottestNode()
        {
            if (hottestNode < 0 || hottestNode >= nodes.Count)
            {
                ThermalNode found = null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (found == null || nodes[i].Temperature > found.Temperature) found = nodes[i];
                }
                return found;
            }

            return nodes[hottestNode];
        }


        public void SetAllTemperatures(float kelvin)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Temperature = kelvin;
            }
            for (int i = 0; i < loops.Count; i++)
            {
                loops[i].Temperature = kelvin;
            }
        }
    }
}

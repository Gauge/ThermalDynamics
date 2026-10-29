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
            // Dormant sun/wind mechanisms may not restart for a long time. Their
            // pending publication can still read the last completed map without blockers.
            sunShadow.ReleaseOccluders();
            windShadow.ReleaseOccluders();
            sunLitDirty = true;
            windLitDirty = true;
        }

        private const float SunRebuildCosine = 0.99939f;

        private const float WindRebuildCosine = 0.93969f;

        public int SunShadowBudget = 2048;

        public int SunLitBudget = 4096;

        private int sunLitCursor;
        private int sunLitCount;
        private bool sunLitPending;

        private float sunLitFill = -1f;

        private float windLitFill = -1f;
        private int windLitCursor;
        private int windLitCount;
        private bool windLitPending;

        private bool windLitDirty = true;

        private bool sunLitDirty = true;
        private bool shadowNodesDeferred;

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
            if (StepInFlight) shadowNodesDeferred = true;
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


    }
}

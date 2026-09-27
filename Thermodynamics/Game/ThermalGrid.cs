using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), true)]
    public partial class ThermalGrid : MyGameLogicComponent
    {
        public const float TickSeconds = 10f / 60f;

        public static readonly MyStringHash ThermalDamage = MyStringHash.GetOrCompute("thermal");

        public MyCubeGrid Grid;

        public GridModel Model;

        public ThermalSimulation Simulation;

        public GridTelemetry Stats;

        private readonly Dictionary<Vector3I, ThermalBlock> blocks =
            new Dictionary<Vector3I, ThermalBlock>(Vector3I.Comparer);

        private readonly List<ThermalBlock> sweepOrder = new List<ThermalBlock>();

        private int massSweepCursor;

        public readonly Dictionary<Vector3I, float> RecentlyRemoved =
            new Dictionary<Vector3I, float>(Vector3I.Comparer);

        public const int MaxRecentlyRemoved = 4096;

        private readonly List<ThermalBlock> vents = new List<ThermalBlock>();

        public IList<ThermalBlock> Vents
        {
            get { return vents; }
        }

        internal void RegisterVent(ThermalBlock bound)
        {
            if (bound != null && !vents.Contains(bound)) vents.Add(bound);
        }

        internal void UnregisterVent(ThermalBlock bound)
        {
            vents.Remove(bound);
        }

        private readonly List<ThermalBlock> heatPumps = new List<ThermalBlock>();

        internal void RegisterHeatPump(ThermalBlock bound)
        {
            if (bound != null && !heatPumps.Contains(bound)) heatPumps.Add(bound);
        }

        internal void UnregisterHeatPump(ThermalBlock bound)
        {
            heatPumps.Remove(bound);
        }

        public ThermalNode HottestNode;

        public int CriticalBlocks;

        private bool started;

        public bool Started
        {
            get { return started && !disabled && Simulation != null; }
        }
        private bool disabled;

        private static readonly List<ThermalGrid> Live = new List<ThermalGrid>();

        public static IList<ThermalGrid> LiveGrids
        {
            get { return Live; }
        }

        public IEnumerable<ThermalBlock> Blocks
        {
            get { return blocks.Values; }
        }

        public long TopologyRevision { get; private set; }

        public int BlockCount
        {
            get { return blocks.Count; }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Settings.EnsureLoaded();

            Grid = Entity as MyCubeGrid;
            if (Grid == null)
            {
                disabled = true;
                return;
            }

            Model = new GridModel(Grid.GridSize);
            Simulation = new ThermalSimulation(Settings.Instance.ToCore(), Model);

            Stats = Telemetry.RegisterGrid(this);
            if (Stats != null) Simulation.Profiler = Stats.Profiler;

            ThermalApi.ApplyThresholds(Simulation);

            Live.Add(this);

            if (Entity.Storage == null)
            {
                Entity.Storage = new MyModStorageComponent();
            }

            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;
            Grid.OnGridSplit += GridSplit;
            Grid.OnGridMerge += GridMerge;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (disabled) return;

            if (Grid.Physics == null)
            {
                disabled = true;
                return;
            }

            Simulation.LoopProperties = ThermalBlockCatalog.ToLoopProperties(
                ThermalLoopDefinition.GetDefinition(ThermalLoopDefinition.DefaultLoopDefinitionId));

            AddExistingBlocks();

            if (Stats != null) Stats.BuildTime.Begin();
            Simulation.RebuildAll();
            if (Stats != null) Stats.BuildTime.End();

            Load();
            started = true;

        }

        private void AddExistingBlocks()
        {
            var existing = Grid.GetBlocks();

            Simulation.EnsureCapacity(existing.Count);

            foreach (IMySlimBlock block in existing)
            {
                AddBlock(block);
            }
        }

        public override bool IsSerialized()
        {
            Save();
            return base.IsSerialized();
        }

        public override void Close()
        {
            ResetAerodynamics();
            if (Stats != null)
            {
                Stats.Close();
                Stats = null;
            }

            if (Grid != null)
            {
                Grid.OnBlockAdded -= BlockAdded;
                Grid.OnBlockRemoved -= BlockRemoved;
                Grid.OnGridSplit -= GridSplit;
                Grid.OnGridMerge -= GridMerge;
            }

            foreach (ThermalBlock block in blocks.Values)
            {
                block.Detach();
            }
            blocks.Clear();
            sweepOrder.Clear();
            massSweepCursor = 0;

            Live.Remove(this);

            base.Close();
        }


        private void BlockAdded(IMySlimBlock block)
        {
            TopologyRevision++;
            if (Stats == null)
            {
                AddBlock(block);
                return;
            }

            Stats.BlockEventTime.Begin();
            AddBlock(block);
            Stats.BlockEventTime.End();
        }

        private void AddBlock(IMySlimBlock block)
        {
            if (disabled || block == null) return;

            if (Grid.EntityId != block.CubeGrid.EntityId)
            {
                if (Stats != null) Stats.ForeignBlockEvents++;
                return;
            }

            if (blocks.ContainsKey(block.Min)) return;

            try
            {
                BlockModel model = ThermalBlockCatalog.Get(block);
                if (model == null || model.Thermal.ExcludeFromSimulation)
                {
                    if (Stats != null) Stats.BlocksIgnored++;
                    return;
                }

                ThermalBlock bound = new ThermalBlock(this, block, model);

                float temperature = StartingTemperature(block.Min);
                bound.Node = Simulation.AddBlock(bound.Instance, temperature);

                blocks.Add(block.Min, bound);
                bound.SweepSlot = sweepOrder.Count;
                sweepOrder.Add(bound);
                bound.Attach();

                if (Stats != null) Stats.BlocksAdded++;
                if (bound.Stats != null) bound.Stats.OnPlaced(bound);
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.AddBlock", e);
            }
        }

        private float StartingTemperature(Vector3I position)
        {
            float carried;
            if (RecentlyRemoved.TryGetValue(position, out carried))
            {
                RecentlyRemoved.Remove(position);
                return carried;
            }
            return Simulation.DefaultTemperature;
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            TopologyRevision++;
            if (disabled || block == null) return;

            if (Stats != null) Stats.BlockEventTime.Begin();
            try
            {
                RemoveBlock(block);
            }
            finally
            {
                if (Stats != null) Stats.BlockEventTime.End();
            }
        }

        private void RemoveBlock(IMySlimBlock block)
        {

            if (Grid.EntityId != block.CubeGrid.EntityId)
            {
                if (Stats != null) Stats.ForeignBlockEvents++;
                return;
            }

            ThermalBlock bound;
            if (!blocks.TryGetValue(block.Min, out bound)) return;

            try
            {
                if (bound.Node != null)
                {
                    if (RecentlyRemoved.Count >= MaxRecentlyRemoved) RecentlyRemoved.Clear();
                    RecentlyRemoved[block.Min] = bound.Node.Temperature;
                }

                bound.Detach();
                Simulation.RemoveBlock(bound.Instance);
                blocks.Remove(block.Min);
                RemoveFromSweepOrder(bound);

                if (Stats != null) Stats.BlocksRemoved++;
                if (bound.Stats != null) bound.Stats.OnRemoved();
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.BlockRemoved", e);
            }
        }

        public void RefreshDefinitions()
        {
            if (Simulation == null || !started) return;

            try
            {
                Simulation.LoopProperties = ThermalBlockCatalog.ToLoopProperties(
                    ThermalLoopDefinition.GetDefinition(ThermalLoopDefinition.DefaultLoopDefinitionId));

                PlanetProperties.Clear();
                currentPlanetId = 0;

                foreach (ThermalBlock bound in blocks.Values)
                {
                    if (bound != null) bound.RefreshProperties();
                }
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.RefreshDefinitions", e);
            }
        }

        public void RefreshBlock(ThermalBlock bound)
        {
            if (bound == null || bound.Instance == null) return;

            Simulation.RefreshBlock(bound.Instance);
            if (Stats != null) Stats.SurfaceRecalcs++;
        }

        public void RefreshBlockSealing(ThermalBlock bound)
        {
            if (bound == null || bound.Instance == null) return;

            Simulation.RefreshBlockSealing(bound.Instance);
            if (Stats != null) Stats.SurfaceRecalcs++;
        }

        private void GridSplit(MyCubeGrid parent, MyCubeGrid child)
        {
            ThermalGrid a = parent.GameLogic.GetAs<ThermalGrid>();
            ThermalGrid b = child.GameLogic.GetAs<ThermalGrid>();
            if (a == null || b == null) return;

            if (a.Stats != null) a.Stats.Splits++;
            if (b.Stats != null) b.Stats.Splits++;

            a.HandOver(b);
        }

        private void GridMerge(MyCubeGrid survivor, MyCubeGrid absorbed)
        {
            ThermalGrid a = survivor.GameLogic.GetAs<ThermalGrid>();
            ThermalGrid b = absorbed.GameLogic.GetAs<ThermalGrid>();
            if (a == null || b == null) return;

            if (a.Stats != null) a.Stats.Merges++;
            if (b.Stats != null) b.Stats.Merges++;

            foreach (KeyValuePair<Vector3I, ThermalBlock> entry in b.blocks)
            {
                if (entry.Value.Node == null) continue;
                a.Receive(MapCell(absorbed, survivor, entry.Key), entry.Value.Node.Temperature);
            }

            b.HandOver(a);
        }

        private void HandOver(ThermalGrid other)
        {
            if (other == null || RecentlyRemoved.Count == 0) return;

            bool sameFrame = Grid == other.Grid;

            List<Vector3I> handled = new List<Vector3I>();
            foreach (KeyValuePair<Vector3I, float> entry in RecentlyRemoved)
            {
                Vector3I target = sameFrame ? entry.Key : MapCell(Grid, other.Grid, entry.Key);
                if (other.Receive(target, entry.Value)) handled.Add(target);
            }

            for (int i = 0; i < handled.Count; i++)
            {
                RecentlyRemoved.Remove(handled[i]);
            }
        }

        private bool Receive(Vector3I min, float temperature)
        {
            ThermalBlock bound = Get(min);
            if (bound != null && bound.Node != null)
            {
                bound.Node.Temperature = temperature;
                return true;
            }

            if (RecentlyRemoved.Count < MaxRecentlyRemoved)
            {
                RecentlyRemoved[min] = temperature;
            }
            return false;
        }

        private static Vector3I MapCell(MyCubeGrid from, MyCubeGrid to, Vector3I cell)
        {
            if (from == to) return cell;
            return to.WorldToGridInteger(from.GridIntegerToWorld(cell));
        }


        public void RefreshTelemetry()
        {
            if (!Telemetry.Enabled || Simulation == null)
            {
                if (Simulation != null) Simulation.Profiler = null;
                Stats = null;
                return;
            }

            if (Stats == null) Stats = Telemetry.RegisterGrid(this);
            if (Stats != null) Simulation.Profiler = Stats.Profiler;

            foreach (ThermalBlock bound in blocks.Values)
            {
                bound.RefreshStats();
            }
        }

        private void RemoveFromSweepOrder(ThermalBlock bound)
        {
            int slot = bound.SweepSlot;
            if (slot < 0 || slot >= sweepOrder.Count || sweepOrder[slot] != bound)
            {
                sweepOrder.Remove(bound);
                bound.SweepSlot = -1;
                return;
            }

            int last = sweepOrder.Count - 1;
            if (slot != last)
            {
                ThermalBlock moved = sweepOrder[last];
                sweepOrder[slot] = moved;
                moved.SweepSlot = slot;
            }

            sweepOrder.RemoveAt(last);
            bound.SweepSlot = -1;
        }

        public ThermalBlock Get(Vector3I min)
        {
            ThermalBlock bound;
            return blocks.TryGetValue(min, out bound) ? bound : null;
        }

        public ThermalBlock GetAtCell(Vector3I cell)
        {
            ThermalBlock direct = Get(cell);
            if (direct != null) return direct;

            BlockInstance instance = Model.GetAtCell(cell);
            if (instance == null) return null;

            return Get(instance.Position);
        }
    }
}

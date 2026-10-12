using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        public long StepsRun;

        private const int HottestInterval = 4;

        private const int MassSweepInterval = 8;

        private const int MassSweepCap = 4096;

        private int stepsSinceHottest;
        private int stepsSinceMassSweep;

        public override void UpdateBeforeSimulation10()
        {
        }

        public void Tick(float frameSeconds)
        {
            if (!PrepareTick(frameSeconds)) return;

            SolveTick();
            PublishTick();
        }

        public bool PrepareTick(float frameSeconds)
        {
            solveFailure = null;
            steppedThisFrame = 0;

            if (disabled || !started || Simulation == null) return false;

            this.frameSeconds = frameSeconds;

            try
            {
                RefreshDiagnosticsFlag();

                startingStep = Simulation.NeedsEnvironmentSample;
                pendingSample = startingStep ? TimedSample() : default(EnvironmentSample);

                if (startingStep) PushHeatPumpState();
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.PrepareTick", e);
                return false;
            }

            stepsBefore = Simulation.Scheduler.StepsRun;

            timeSimulation = Telemetry.Enabled && Stats != null;
            if (timeSimulation)
            {
                SimulationWork work = Simulation.Work;
                topologyVisitsBefore = work.TopologyNodeVisits;
                exposureVisitsBefore = work.ExposureNodeVisits;
                roomCellsBefore = work.RoomCellsVisited;

                Stats.Profiler.InTick = true;
            }

            return true;
        }

        public void SolveTick()
        {
            if (Simulation == null) return;

            try
            {
                if (timeSimulation) Stats.SimulationTime.Begin();
                Simulation.Update(frameSeconds, pendingSample);
                if (timeSimulation) Stats.SimulationTime.End();

                steppedThisFrame = Simulation.Scheduler.StepsRun - stepsBefore;
            }
            catch (Exception e)
            {
                solveFailure = e;
            }
        }

        public void PublishTick()
        {
            if (Simulation == null) return;

            if (solveFailure != null)
            {
                Telemetry.Exception("ThermalGrid.SolveTick", solveFailure);
                solveFailure = null;
                return;
            }

            try
            {
                PublishInternal();
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.PublishTick", e);
            }
        }

        private void PublishInternal()
        {
            long stepped = steppedThisFrame;

            if (timeSimulation)
            {
                SimulationWork work = Simulation.Work;

                Stats.Profiler.InTick = false;
                Stats.NoteTick(Telemetry.FramesObserved);

                Telemetry.FrameCost.AddGrid(
                    Grid == null ? "(grid)" : Grid.DisplayName,
                    Stats.SimulationTime.LastMilliseconds,
                    blocks.Count);

                Telemetry.FrameCost.AddWork(
                    work.TopologyNodeVisits - topologyVisitsBefore,
                    work.ExposureNodeVisits - exposureVisitsBefore,
                    work.RoomCellsVisited - roomCellsBefore);
            }

            if (stepped <= 0) return;

            StepsRun += stepped;
            TimedAfterSteps((int)stepped);
        }

        private float frameSeconds = ThermalGridScheduler.FrameSeconds;

        private EnvironmentSample pendingSample;

        private bool startingStep;

        private long stepsBefore;
        private long steppedThisFrame;

        private Exception solveFailure;

        private long topologyVisitsBefore;
        private long exposureVisitsBefore;
        private long roomCellsBefore;

        private bool timeSimulation;

        private void RefreshDiagnosticsFlag()
        {
            bool client = MyAPIGateway.Utilities == null || !MyAPIGateway.Utilities.IsDedicated;

            bool wanted = Telemetry.Enabled
                || (client && Settings.Instance.DebugTextOnScreen)
                || (client && ThermalDebugView.NeedsWatts);

            Simulation.Solver.CollectDiagnostics = wanted;
        }

        private EnvironmentSample TimedSample()
        {
            GridProfiler profiler = Profiler;
            if (profiler == null) return Sample();

            profiler.EnvironmentSample.Begin();
            EnvironmentSample sample = Sample();
            profiler.EnvironmentSample.End();

            Telemetry.FrameCost.AddSample(profiler.EnvironmentSample.LastMilliseconds);
            return sample;
        }

        private void TimedAfterSteps(int steps)
        {
            GridProfiler profiler = Profiler;
            if (profiler == null)
            {
                AfterSteps(steps);
                return;
            }

            profiler.AfterStep.Begin();
            AfterSteps(steps);
            profiler.AfterStep.End();

            Telemetry.FrameCost.AddAfterStep(profiler.AfterStep.LastMilliseconds);
        }

        private GridProfiler Profiler
        {
            get { return Telemetry.Enabled && Stats != null ? Stats.Profiler : null; }
        }

        private void AfterSteps(int steps)
        {
            GridProfiler profiler = Profiler;

            Begin(profiler == null ? null : profiler.Damage);
            ApplyOverheatDamage();
            RaiseThresholdCrossings();
            PublishHeatPumpDemand();
            End(profiler == null ? null : profiler.Damage);

            Begin(profiler == null ? null : profiler.MassSweep);
            SweepMass(steps);
            End(profiler == null ? null : profiler.MassSweep);

            stepsSinceMassSweep += steps;
            if (stepsSinceMassSweep >= MassSweepInterval)
            {
                stepsSinceMassSweep = 0;
                SweepRoomPressure();
            }

            Begin(profiler == null ? null : profiler.LostRooms);
            RefreshLostRooms();
            End(profiler == null ? null : profiler.LostRooms);

            stepsSinceHottest += steps;
            if (stepsSinceHottest >= HottestInterval)
            {
                stepsSinceHottest = 0;

                Begin(profiler == null ? null : profiler.Health);

                HottestNode = Simulation.Solver.HottestNode();
                CheckHealth();

                End(profiler == null ? null : profiler.Health);
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                UpdateCues(steps, steps * Simulation.Settings.StepSeconds);
            }

            if (Telemetry.Enabled)
            {
                Begin(profiler == null ? null : profiler.Sampling);
                Telemetry.OnGridStepped(this, steps);
                ProfileEnvironment();
                End(profiler == null ? null : profiler.Sampling);
            }
        }

        private static void Begin(TimingStat stat)
        {
            if (stat != null) stat.Begin();
        }

        private static void End(TimingStat stat)
        {
            if (stat != null) stat.End();
        }

        private TelemetryAnomalyKind lastHealth;

        private void CheckHealth()
        {
            TelemetryAnomalyKind health = TelemetryAnomalies.ClassifyGrid(
                Simulation.EnvironmentWatts,
                Simulation.HeatGainWatts,
                HottestNode == null ? 0f : HottestNode.Temperature,
                Telemetry.ImplausibleTemperature);

            if (health == lastHealth) return;
            lastHealth = health;

            if (health == TelemetryAnomalyKind.None) return;

            Telemetry.GridFault(this, TelemetryAnomalies.GridName(health, Telemetry.ImplausibleTemperature));
        }

        private void ApplyOverheatDamage()
        {
            IList<OverheatEvent> overheats = Simulation.Overheats;
            CriticalBlocks = overheats.Count;
            if (overheats.Count == 0) return;

            if (!MyAPIGateway.Session.IsServer) return;

            for (int i = 0; i < overheats.Count; i++)
            {
                OverheatEvent overheat = overheats[i];

                ThermalBlock bound = Get(overheat.Block.Position);
                if (bound == null) continue;

                if (Telemetry.Enabled) Telemetry.OnCriticalDamage(bound, overheat.Damage);

                bound.Block.DoDamage(overheat.Damage, ThermalDamage, false);
            }
        }

        private void SweepMass(int steps)
        {
            int count = sweepOrder.Count;
            int share = SimulationScheduler.SweepSlice(count, steps, MassSweepInterval, MassSweepCap);
            if (share <= 0) return;

            if (massSweepCursor >= count) massSweepCursor = 0;

            for (int i = 0; i < share; i++)
            {
                if (massSweepCursor >= count) massSweepCursor = 0;
                if (sweepOrder[massSweepCursor++].RefreshMass()) InvalidateGeometry();
            }
        }

        private void PushHeatPumpState()
        {
            PushCoolantPumpState();

            if (heatPumps.Count == 0) return;

            for (int i = 0; i < heatPumps.Count; i++)
            {
                ThermalBlock bound = heatPumps[i];
                HeatPumpDevice device = Simulation.GetHeatPump(bound.Instance);
                if (device == null) continue;

                ThermalHeatPumpBlock electrical = bound.HeatPump;
                if (electrical == null)
                {
                    device.Enabled = false;
                    device.PowerAvailable = 0f;
                    continue;
                }

                device.Enabled = electrical.IsRunning;
                device.PowerAvailable = electrical.PowerAvailable;
                device.PowerSetting = electrical.PowerSetting;
            }
        }

        private void PushCoolantPumpState()
        {
            IList<CoolantLoop> loops = Simulation.Solver.Loops;
            if (loops == null || loops.Count == 0) return;

            for (int i = 0; i < loops.Count; i++)
            {
                CoolantLoop loop = loops[i];
                IList<Core.CoolantPump> pumps = loop.Pumps;
                bool changed = false;

                for (int p = 0; p < pumps.Count; p++)
                {
                    Core.CoolantPump pump = pumps[p];
                    if (pump.Block == null) continue;

                    ThermalBlock bound = Get(pump.Block.Min);
                    ThermalCoolantPumpBlock control = bound == null ? null : bound.CoolantPump;

                    if (control == null)
                    {
                        continue;
                    }

                    bool running = control.IsRunning;
                    float speed = control.Speed;
                    float available = control.PowerAvailable;
                    float rating = control.MaxPowerWatts;

                    if (pump.Enabled == running && pump.Speed == speed
                        && pump.PowerAvailable == available && pump.MaxPowerWatts == rating) continue;

                    pump.Enabled = running;
                    pump.Speed = speed;

                    pump.PowerAvailable = available;
                    pump.MaxPowerWatts = rating;
                    changed = true;
                }

                if (changed) loop.RefreshFlow();

                PublishRefillDemand(loop);
            }
        }

        private void PublishRefillDemand(CoolantLoop loop)
        {
            IList<Core.CoolantPump> pumps = loop.Pumps;
            if (pumps.Count == 0) return;

            float demand = loop.RefillDemandWatts;

            for (int p = 0; p < pumps.Count; p++)
            {
                Core.CoolantPump pump = pumps[p];
                if (pump.Block == null) continue;

                ThermalBlock bound = Get(pump.Block.Min);
                if (bound == null || bound.CoolantPump == null) continue;

                bound.CoolantPump.SetRefillDemandWatts(p == 0 ? demand : 0f);
            }
        }

        private void PublishHeatPumpDemand()
        {
            if (heatPumps.Count == 0) return;

            for (int i = 0; i < heatPumps.Count; i++)
            {
                ThermalBlock bound = heatPumps[i];
                if (bound.HeatPump == null) continue;

                HeatPumpDevice device = Simulation.GetHeatPump(bound.Instance);
                bound.HeatPump.SetDemandWatts(device == null ? 0f : device.LastDemandWatts);
            }
        }

        private void SweepRoomPressure()
        {
            if (!Settings.Instance.EnableRoomAir) return;

            IList<RoomAirNode> air = Simulation.RoomAir;
            if (air.Count == 0) return;

            GridProfiler profiler = Stats == null ? null : Stats.Profiler;
            if (profiler != null) profiler.RoomPressure.Begin();

            try
            {
                if (Stats != null)
                {
                    Stats.RoomPressureSweeps++;
                    Stats.RoomPressureRoomVisits += air.Count;
                }

                bool worldPressurised = WorldPressurised();

                if (!worldPressurised)
                {
                    for (int i = 0; i < air.Count; i++)
                    {
                        Simulation.SetRoomPressure(air[i].Anchor, 0f);
                    }
                    return;
                }

                GasLevels.Clear();
                SealedByGame.Clear();
                bool anyUnanswered = false;

                for (int i = 0; i < air.Count; i++)
                {
                    float level = GameOxygenAt(air[i].Anchor);
                    bool sealedByGame = Grid.IsRoomAtPositionAirtight(air[i].Anchor);

                    GasLevels.Add(level);
                    SealedByGame.Add(sealedByGame);

                    if (RoomPressure.NeedsVentFallback(true, sealedByGame, level))
                    {
                        anyUnanswered = true;
                    }
                }

                if (Stats != null) Stats.RoomPressureGameQueries += air.Count * 2;

                if (anyUnanswered)
                {
                    for (int i = 0; i < air.Count; i++)
                    {
                        VentLevels[air[i].RoomIndex] = RoomPressure.NotReported;
                    }

                    if (Stats != null) Stats.RoomPressureVentScans++;
                    ReadVents();
                }

                for (int i = 0; i < air.Count; i++)
                {
                    RoomAirNode room = air[i];

                    float reported = GasLevels[i];

                    if (reported < 0f && !VentLevels.TryGetValue(room.RoomIndex, out reported))
                    {
                        reported = RoomPressure.NotReported;
                    }

                    float level = RoomPressure.Level(true, SealedByGame[i], reported);

                    Simulation.SetRoomPressure(room.Anchor, level);
                }
            }
            finally
            {
                if (profiler != null) profiler.RoomPressure.End();
            }
        }

        private static bool WorldPressurised()
        {
            if (MyAPIGateway.Session == null) return false;

            MyObjectBuilder_SessionSettings settings = MyAPIGateway.Session.SessionSettings;
            if (settings == null) return false;

            return settings.EnableOxygen && settings.EnableOxygenPressurization;
        }

        private void ReadVents()
        {
            for (int i = vents.Count - 1; i >= 0; i--)
            {
                ThermalBlock bound = vents[i];
                IMyAirVent vent = bound.Vent;

                if (vent == null || bound.Block.FatBlock == null || bound.Block.FatBlock.Closed)
                {
                    vents.RemoveAt(i);
                    continue;
                }

                if (Stats != null) Stats.RoomPressureVentsWalked++;

                float level = vent.Depressurize ? 0f : vent.GetOxygenLevel();

                Vector3I[] cells = bound.Instance.Cells;
                for (int c = 0; c < cells.Length; c++)
                {
                    for (int face = 0; face < Face.Count; face++)
                    {
                        int room = Simulation.Rooms.Map.RoomIndexOf(cells[c] + Face.Offsets[face]);
                        if (room < 0) continue;

                        float existing;
                        if (!VentLevels.TryGetValue(room, out existing) || level < existing
                            || existing <= RoomPressure.NotReported)
                        {
                            VentLevels[room] = level;
                        }
                    }
                }
            }
        }

        private readonly Dictionary<int, float> VentLevels = new Dictionary<int, float>();

        private readonly List<float> GasLevels = new List<float>();

        private readonly List<bool> SealedByGame = new List<bool>();

        private void RaiseThresholdCrossings()
        {
            IList<ThresholdCrossing> crossings = Simulation.Crossings;
            if (crossings.Count == 0) return;

            for (int i = 0; i < crossings.Count; i++)
            {
                ThresholdCrossing crossing = crossings[i];
                ThermalBlock bound = Get(crossing.Block.Position);
                if (bound == null) continue;

                ThermalApi.RaiseThreshold(crossing, bound.Block);
            }
        }
    }
}

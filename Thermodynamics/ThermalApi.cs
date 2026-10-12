using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Thermodynamics
{
    public static partial class ThermalApi
    {
        public const long ChannelId = 2985582372;

        public const int Version = 1;

        private static Dictionary<string, Delegate> methods;
        private static bool registered;

        private static readonly Dictionary<int, Action<IMySlimBlock, int, float, float, bool>> ThresholdHandlers =
            new Dictionary<int, Action<IMySlimBlock, int, float, float, bool>>();

        private static readonly List<ThermalThreshold> GlobalThresholds = new List<ThermalThreshold>();

        private static int nextThresholdId = 1;

        public static void Register()
        {
            if (registered) return;
            registered = true;

            Build();
            MyAPIGateway.Utilities.RegisterMessageHandler(ChannelId, OnRequest);
            Publish();
        }

        public static void Unregister()
        {
            if (!registered) return;
            registered = false;

            MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, OnRequest);
            ThresholdHandlers.Clear();
            GlobalThresholds.Clear();
            ClearAerodynamics();
            MyAPIGateway.Utilities.SendModMessage(ChannelId, false);
            methods = null;
        }

        private static void OnRequest(object payload)
        {
            if (payload is Dictionary<string, Delegate> || payload is bool) return;
            Publish();
        }

        private static void Publish()
        {
            MyAPIGateway.Utilities.SendModMessage(ChannelId, methods);
        }

        private static Func<TResult> Guard<TResult>(Func<TResult> call, string name)
        {
            return () =>
            {
                try { return call(); }
                catch (Exception e) { Telemetry.Exception("ThermalApi." + name, e); return default(TResult); }
            };
        }

        private static Func<T1, TResult> Guard<T1, TResult>(Func<T1, TResult> call, string name)
        {
            return a =>
            {
                try { return call(a); }
                catch (Exception e) { Telemetry.Exception("ThermalApi." + name, e); return default(TResult); }
            };
        }

        private static Func<T1, T2, TResult> Guard<T1, T2, TResult>(Func<T1, T2, TResult> call, string name)
        {
            return (a, b) =>
            {
                try { return call(a, b); }
                catch (Exception e) { Telemetry.Exception("ThermalApi." + name, e); return default(TResult); }
            };
        }

        private static Func<T1, T2, T3, TResult> Guard<T1, T2, T3, TResult>(
            Func<T1, T2, T3, TResult> call, string name)
        {
            return (a, b, c) =>
            {
                try { return call(a, b, c); }
                catch (Exception e) { Telemetry.Exception("ThermalApi." + name, e); return default(TResult); }
            };
        }

        private static void Build()
        {
            methods = new Dictionary<string, Delegate>();

            methods["ApiVersion"] = Guard(new Func<int>(() => Version), "ApiVersion");

            methods["GetBlockTemperature"] = Guard(new Func<IMySlimBlock, float>(GetBlockTemperature), "GetBlockTemperature");
            methods["GetBlockThermals"] = Guard(new Func<IMySlimBlock, MyTuple<float, float, float, int>>(GetBlockThermals), "GetBlockThermals");
            methods["GetGridGeometryVersion"] = Guard(new Func<IMyCubeGrid, long>(g => { var t = GridOf(g); return t == null ? -1 : t.GeometryVersion; }), "GetGridGeometryVersion");
            methods["GetGridGeometry"] = Guard(new Func<IMyCubeGrid, Dictionary<string, object>>(g => { var t = GridOf(g); return t == null ? null : t.CopyGeometry(); }), "GetGridGeometry");
            methods["GetGridThermalUpstream"] = Guard(new Func<IMyCubeGrid, Vector3>(g => { var t = GridOf(g); return t == null ? Vector3.Zero : -t.LastState.WindDirectionLocal; }), "GetGridThermalUpstream");
            methods["GetGridWindCeiling"] = Guard(new Func<IMyCubeGrid,float>(g => { if (g == null) return 0f; var p = PlanetManager.GetClosestPlanet(g.GetPosition()); return p == null || p.Entity == null ? 0f : p.Entity.GetWindSpeed(g.GetPosition()); }), "GetGridWindCeiling");
            methods["GetGridEnvironment"] = Guard(new Func<IMyCubeGrid, MyTuple<bool, Vector3, float, float, float, Vector3>>(GetGridEnvironment), "GetGridEnvironment");
            methods["GetEnvironmentAt"] = Guard(new Func<Vector3D, MyTuple<bool, Vector3, float, float, float, Vector3>>(GetEnvironmentAt), "GetEnvironmentAt");
            methods["RegisterAerodynamics"] = Guard(new Func<long, Dictionary<string, Delegate>, bool>(RegisterAerodynamics), "RegisterAerodynamics");
            methods["GetGridSummary"] = Guard(new Func<IMyCubeGrid, MyTuple<float, float, int, int>>(GetGridSummary), "GetGridSummary");
            methods["GetRoom"] = Guard(new Func<IMyCubeGrid, Vector3I, MyTuple<bool, float, float, float>>(GetRoom), "GetRoom");
            methods["GetGridHeatBalance"] = Guard(new Func<IMyCubeGrid, MyTuple<float, float>>(GetGridHeatBalance), "GetGridHeatBalance");
            methods["GetGridFrictionWatts"] = Guard(new Func<IMyCubeGrid, float>(GetGridFrictionWatts), "GetGridFrictionWatts");
            methods["GetGridAeroForces"] = Guard(new Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>(GetGridAeroForces), "GetGridAeroForces");
            methods["GetGridAeroMoments"] = Guard(new Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>(GetGridAeroMoments), "GetGridAeroMoments");
            methods["GetGridAeroProfile"] = Guard(new Func<IMyCubeGrid, Dictionary<string, object>>(GetGridAeroProfile), "GetGridAeroProfile");
            methods["SetGridAeroProfileConsumer"] = Guard(new Func<IMyCubeGrid, long, bool, bool>(SetGridAeroProfileConsumer), "SetGridAeroProfileConsumer");
            methods["GetGridAeroStatus"] = Guard(new Func<IMyCubeGrid, MyTuple<bool, bool, int, int>>(GetGridAeroStatus), "GetGridAeroStatus");
            methods["GetCoolantLoop"] = Guard(new Func<IMySlimBlock, MyTuple<bool, float, float, float, int>>(GetCoolantLoop), "GetCoolantLoop");
            methods["SetBlockDragProfile"] = Guard(new Func<IMySlimBlock, float[], bool>(SetBlockDragProfile), "SetBlockDragProfile");
            methods["SetBlockLiftProfile"] = Guard(new Func<IMySlimBlock, float[], bool>(SetBlockLiftProfile), "SetBlockLiftProfile");
            methods["ClearBlockLiftProfile"] = Guard(new Func<IMySlimBlock, bool>(ClearBlockLiftProfile), "ClearBlockLiftProfile");
            methods["ClearBlockDragProfile"] = Guard(new Func<IMySlimBlock, bool>(ClearBlockDragProfile), "ClearBlockDragProfile");

            methods["SetBlockTemperature"] = Guard(new Func<IMySlimBlock, float, bool>(SetBlockTemperature), "SetBlockTemperature");
            methods["AddBlockHeat"] = Guard(new Func<IMySlimBlock, float, bool>(AddBlockHeat), "AddBlockHeat");
            methods["SetRoomPressure"] = Guard(new Func<IMyCubeGrid, Vector3I, float, bool>(SetRoomPressure), "SetRoomPressure");

            methods["AddHeatSource"] = Guard(new Func<IMyEntity, float, float, int>(ThermalHeatSources.Add), "AddHeatSource");
            methods["AddHeatSourceAt"] = Guard(new Func<Vector3D, float, float, int>(ThermalHeatSources.Add), "AddHeatSourceAt");
            methods["UpdateHeatSource"] = Guard(new Func<int, float, bool>(ThermalHeatSources.Update), "UpdateHeatSource");
            methods["RemoveHeatSource"] = Guard(new Func<int, bool>(ThermalHeatSources.Remove), "RemoveHeatSource");

            methods["AddThreshold"] = Guard(new Func<float, int, Action<IMySlimBlock, int, float, float, bool>, int>(AddThreshold), "AddThreshold");
            methods["RemoveThreshold"] = Guard(new Func<int, bool>(RemoveThreshold), "RemoveThreshold");

            methods["GetSetting"] = Guard(new Func<string, float>(GetSetting), "GetSetting");
            methods["SetSetting"] = Guard(new Func<string, float, bool>(SetSetting), "SetSetting");
            methods["ListSettings"] = Guard(new Func<List<string>>(Settings.Names), "ListSettings");
        }

        private static ThermalGrid GridOf(IMySlimBlock block)
        {
            if (block == null || block.CubeGrid == null) return null;
            return block.CubeGrid.GameLogic == null ? null : block.CubeGrid.GameLogic.GetAs<ThermalGrid>();
        }

        private static ThermalGrid GridOf(IMyCubeGrid grid)
        {
            if (grid == null || grid.GameLogic == null) return null;
            return grid.GameLogic.GetAs<ThermalGrid>();
        }

        private static ThermalBlock Bound(IMySlimBlock block)
        {
            ThermalGrid grid = GridOf(block);
            return grid == null ? null : grid.Get(block.Min);
        }

        private static float GetBlockTemperature(IMySlimBlock block)
        {
            ThermalBlock bound = Bound(block);
            return bound == null || bound.Node == null ? 0f : bound.Node.Temperature;
        }

        private static MyTuple<float, float, float, int> GetBlockThermals(IMySlimBlock block)
        {
            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null)
            {
                return new MyTuple<float, float, float, int>(0f, 0f, 0f, 0);
            }

            ThermalNode node = bound.Node;
            return new MyTuple<float, float, float, int>(
                node.Temperature,
                node.ThermalMass,
                node.Thermal.CriticalTemperature,
                node.TotalExposedFaces);
        }

        private static MyTuple<float, float, int, int> GetGridSummary(IMyCubeGrid grid)
        {
            ThermalGrid thermals = GridOf(grid);
            if (thermals == null || thermals.Simulation == null)
            {
                return new MyTuple<float, float, int, int>(0f, 0f, 0, 0);
            }

            ThermalNode hottest = thermals.HottestNode;
            return new MyTuple<float, float, int, int>(
                hottest == null ? 0f : hottest.Temperature,
                thermals.LastState.AmbientTemperature,
                thermals.CriticalBlocks,
                thermals.Simulation.Solver.Loops.Count);
        }

        private static MyTuple<float, float> GetGridHeatBalance(IMyCubeGrid grid)
        {
            ThermalGrid thermals = GridOf(grid);
            if (thermals == null || thermals.Simulation == null)
            {
                return new MyTuple<float, float>(0f, 0f);
            }

            return new MyTuple<float, float>(
                thermals.Simulation.VentedWatts, thermals.Simulation.HeatGainWatts);
        }

        private static float GetGridFrictionWatts(IMyCubeGrid grid)
        {
            ThermalGrid thermals = GridOf(grid);
            if (thermals == null || thermals.Simulation == null) return 0f;

            return thermals.Simulation.FrictionWatts;
        }

        private static bool SetBlockDragProfile(IMySlimBlock block, float[] faces)
        {
            if (faces == null || faces.Length != Face.Count) return false;

            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null) return false;

            bound.Node.Drag = DragProfile.Of(faces[0], faces[1], faces[2], faces[3], faces[4], faces[5]);
            bound.Grid.InvalidateGeometry();
            return true;
        }

        private static bool SetBlockLiftProfile(IMySlimBlock block, float[] geometry)
        {
            var call = AeroMethod<Func<IMySlimBlock, float[], bool>>("SetBlockLiftProfile");
            return call == null ? false : call(block, geometry);
        }

        private static bool ClearBlockLiftProfile(IMySlimBlock block)
        {
            var call = AeroMethod<Func<IMySlimBlock, bool>>("ClearBlockLiftProfile");
            return call == null ? false : call(block);
        }

        private static bool ClearBlockDragProfile(IMySlimBlock block)
        {
            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null) return false;

            bound.Node.Drag = default(DragProfile);
            bound.Grid.InvalidateGeometry();
            return true;
        }

        /// <summary>
        /// The aerodynamic drag and lift applied in the last physics tick, in world newtons.
        /// Lift includes the transverse component of the submitted aerodynamic force.
        /// </summary>
        private static MyTuple<Vector3, Vector3> GetGridAeroForces(IMyCubeGrid grid)
        {
            var call = AeroMethod<Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>>("GetGridAeroForces");
            return call == null ? new MyTuple<Vector3, Vector3>() : call(grid);
        }

        /// <summary>Latest world-space restoring and damping moments, N·m.</summary>
        private static MyTuple<Vector3, Vector3> GetGridAeroMoments(IMyCubeGrid grid)
        {
            var call = AeroMethod<Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>>("GetGridAeroMoments");
            return call == null ? new MyTuple<Vector3, Vector3>() : call(grid);
        }

        /// <summary>Latest profile readiness, guard binding, construction work and centre-update faces.</summary>
        private static MyTuple<bool, bool, int, int> GetGridAeroStatus(IMyCubeGrid grid)
        {
            var call = AeroMethod<Func<IMyCubeGrid, MyTuple<bool, bool, int, int>>>("GetGridAeroStatus");
            return call == null ? new MyTuple<bool, bool, int, int>() : call(grid);
        }

        private static Dictionary<string, object> GetGridAeroProfile(IMyCubeGrid grid)
        {
            var call = AeroMethod<Func<IMyCubeGrid, Dictionary<string, object>>>("GetGridAeroProfile");
            return call == null ? null : call(grid);
        }

        private static bool SetGridAeroProfileConsumer(IMyCubeGrid grid, long consumer, bool enabled)
        {
            var call = AeroMethod<Func<IMyCubeGrid, long, bool, bool>>("SetGridAeroProfileConsumer");
            return call == null ? false : call(grid, consumer, enabled);
        }

        private static MyTuple<bool, float, float, float, int> GetCoolantLoop(IMySlimBlock block)
        {
            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null)
            {
                return new MyTuple<bool, float, float, float, int>(false, 0f, 0f, 0f, 0);
            }

            ThermalGrid thermals = GridOf(block);
            if (thermals == null || thermals.Simulation == null)
            {
                return new MyTuple<bool, float, float, float, int>(false, 0f, 0f, 0f, 0);
            }

            CoolantLoop loop = thermals.Simulation.FindLoopContaining(bound.Node.Block);
            if (loop == null)
            {
                return new MyTuple<bool, float, float, float, int>(false, 0f, 0f, 0f, 0);
            }

            return new MyTuple<bool, float, float, float, int>(
                true, loop.Temperature, loop.HottestSegment, loop.ColdestSegment, loop.PipeCount);
        }

        private static MyTuple<bool, float, float, float> GetRoom(IMyCubeGrid grid, Vector3I cell)
        {
            ThermalGrid thermals = GridOf(grid);
            if (thermals == null || thermals.Simulation == null)
            {
                return new MyTuple<bool, float, float, float>(false, 0f, 0f, 0f);
            }

            RoomAirNode air = thermals.Simulation.GetRoomAir(cell);
            if (air == null) return new MyTuple<bool, float, float, float>(false, 0f, 0f, 0f);

            return new MyTuple<bool, float, float, float>(true, air.Temperature, air.Pressure, air.Volume);
        }

        private static bool SetBlockTemperature(IMySlimBlock block, float kelvin)
        {
            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null) return false;
            if (kelvin < ThermalConstants.MinimumTemperature) kelvin = ThermalConstants.MinimumTemperature;

            bound.Node.Temperature = kelvin;
            return true;
        }

        private static bool AddBlockHeat(IMySlimBlock block, float joules)
        {
            ThermalBlock bound = Bound(block);
            if (bound == null || bound.Node == null) return false;

            ThermalNode node = bound.Node;
            float updated = node.Temperature + (joules / node.ThermalMass);
            node.Temperature = updated < ThermalConstants.MinimumTemperature
                ? ThermalConstants.MinimumTemperature
                : updated;
            return true;
        }

        private static bool SetRoomPressure(IMyCubeGrid grid, Vector3I cell, float pressure)
        {
            ThermalGrid thermals = GridOf(grid);
            if (thermals == null || thermals.Simulation == null) return false;
            return thermals.Simulation.SetRoomPressure(cell, pressure);
        }

        private static float GetSetting(string name)
        {
            Settings settings = Settings.EnsureLoaded();
            return settings == null ? 0f : settings.GetValue(name);
        }

        private static bool SetSetting(string name, float value)
        {
            if (Settings.Instance == null) return false;
            if (!Settings.Instance.SetValue(name, value)) return false;

            Settings.Instance.Apply();
            return true;
        }

        private static int AddThreshold(
            float temperature, int direction, Action<IMySlimBlock, int, float, float, bool> callback)
        {
            if (callback == null) return 0;

            ThresholdDirection resolved =
                direction == 1 ? ThresholdDirection.Falling :
                direction == 2 ? ThresholdDirection.Both :
                ThresholdDirection.Rising;

            int id = nextThresholdId++;
            ThermalThreshold threshold = new ThermalThreshold(id, temperature, resolved);

            GlobalThresholds.Add(threshold);
            ThresholdHandlers[id] = callback;

            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                if (grids[i].Simulation != null) grids[i].Simulation.Thresholds.Add(threshold);
            }

            return id;
        }

        private static bool RemoveThreshold(int id)
        {
            if (!ThresholdHandlers.Remove(id)) return false;

            for (int i = 0; i < GlobalThresholds.Count; i++)
            {
                if (GlobalThresholds[i].Id != id) continue;
                GlobalThresholds.RemoveAt(i);
                break;
            }

            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                if (grids[i].Simulation != null) grids[i].Simulation.Thresholds.Remove(id);
            }

            return true;
        }

        public static void ApplyThresholds(ThermalSimulation simulation)
        {
            if (simulation == null) return;

            for (int i = 0; i < GlobalThresholds.Count; i++)
            {
                simulation.Thresholds.Add(GlobalThresholds[i]);
            }
        }

        public static void RaiseThreshold(ThresholdCrossing crossing, IMySlimBlock block)
        {
            Action<IMySlimBlock, int, float, float, bool> handler;
            if (!ThresholdHandlers.TryGetValue(crossing.ThresholdId, out handler)) return;

            try
            {
                handler(block, crossing.ThresholdId, crossing.Threshold, crossing.Temperature, crossing.Rising);
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalApi.RaiseThreshold", e);
                ThresholdHandlers.Remove(crossing.ThresholdId);
            }
        }
    }
}

using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Generated;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Utils;

namespace Thermodynamics
{
    public static class Telemetry
    {
        public static bool Enabled;

        public static int SampleStride
        {
            get { return Gate.Stride; }
            set { Gate.Stride = value; }
        }

        public const int MaxGridRecords = 2048;

        public const int MaxSurfaceRows = 300000;

        public static int SurfaceRowsCaptured;
        public const int MaxAnomalyKinds = 64;
        public const int MaxBlockTypes = 4096;

        public const int ExceptionFrames = 6;

        public const float ImplausibleTemperature = 20000f;

        public static DateTime StartedUtc;
        public static string WorldName = "(unknown)";
        public static string OnlineMode = "(unknown)";
        public static bool IsServer;
        public static bool IsDedicated;
        public static bool IsMultiplayer;
        public static int MaxPlayers;
        public static string SessionPath = "";
        public static DateTime GameStartDate;
        public static string GameVersion = "(unknown)";

        public static readonly List<KeyValuePair<string, string>> WorldSettingsRows = new List<KeyValuePair<string, string>>();

        public static readonly List<string> Mods = new List<string>();

        public static readonly Dictionary<string, string> PlanetProperties = new Dictionary<string, string>();
        public static DateTime GameEndDate;
        public static Settings SettingsSnapshot;

        public static long FramesObserved;
        public static long SimulationStepsObserved;
        public static long CellUpdatesObserved;


        private static readonly Stopwatch SessionClock = new Stopwatch();

        private static readonly SampleGate Gate = new SampleGate();
        private static bool _started;
        private static bool _finished;
        private static bool _identityCaptured;


        public static readonly List<GridTelemetry> Grids = new List<GridTelemetry>();
        public static readonly Dictionary<long, GridTelemetry> GridsById = new Dictionary<long, GridTelemetry>();
        public static long GridsSeen;
        public static long GridRecordsDropped;

        public static readonly Dictionary<MyDefinitionId, BlockTypeTelemetry> BlockTypes = new Dictionary<MyDefinitionId, BlockTypeTelemetry>(MyDefinitionId.Comparer);
        public static long BlockTypeRecordsDropped;


        public static readonly AnomalyRegistry Faults = new AnomalyRegistry(MaxAnomalyKinds);

        public static Dictionary<string, AnomalyRecord> Anomalies
        {
            get { return Faults.Records; }
        }

        public static long AnomalyKindsDropped
        {
            get { return Faults.KindsDropped; }
        }


        private static readonly object RegistryLock = new object();


        public static OverlayTelemetry Overlay = new OverlayTelemetry();

        public static ThermalVisionTelemetry Vision = new ThermalVisionTelemetry();


        public static readonly TimingStat SessionFrameTime = new TimingStat("session frame");


        public static readonly FrameCostTracker FrameCost = new FrameCostTracker();

        public static double SessionSeconds
        {
            get { return SessionClock.Elapsed.TotalSeconds; }
        }



        public static void Start()
        {
            if (_started) return;
            _started = true;
            _finished = false;

            Enabled = Settings.Instance == null || Settings.Instance.EnableTelemetry;
            if (Settings.Instance != null && Settings.Instance.TelemetrySampleStride > 0)
            {
                SampleStride = Settings.Instance.TelemetrySampleStride;
            }

            StartedUtc = DateTime.UtcNow;
            SessionClock.Reset();
            SessionClock.Start();

            MyLog.Default.Info("[" + Settings.Name + "] [Telemetry] collection " + (Enabled ? "started" : "disabled"));
        }

        /// <summary>Enables or disables telemetry collection.</summary>
        [ChatCommand("telemetry")]
        internal static void SetTelemetryCommand(bool enabled)
        {
            SetEnabled(enabled);
            if (enabled)
            {
                ThermalChatCommands.Reply("telemetry collection ON (stride " + Telemetry.SampleStride + ")");
                return;
            }

            ThermalChatCommands.Reply("telemetry collection OFF");
        }

        [ChatCommand("dump")]
        internal static void Dump()
        {
            if (!Enabled)
            {
                ThermalChatCommands.Reply("telemetry is off; /thermal telemetry on first");
                return;
            }

            Finish("manual dump", true);
            ThermalChatCommands.Reply("telemetry report written to world storage");
        }

        public static void SetEnabled(bool enabled)
        {
            if (!_started) Start();
            if (Enabled == enabled) return;

            Enabled = enabled;
            _finished = false;

            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;
            for (int i = 0; i < grids.Count; i++)
            {
                grids[i].RefreshTelemetry();
            }

            MyLog.Default.Info("[" + Settings.Name + "] [Telemetry] collection "
                + (enabled ? "enabled" : "disabled") + " at runtime");
        }


        private static void CaptureIdentity()
        {
            if (_identityCaptured) return;

            try
            {
                if (MyAPIGateway.Session == null) return;

                WorldName = MyAPIGateway.Session.Name;
                OnlineMode = MyAPIGateway.Session.OnlineMode.ToString();
                MaxPlayers = MyAPIGateway.Session.MaxPlayers;
                IsServer = MyAPIGateway.Session.IsServer;
                IsDedicated = MyAPIGateway.Utilities != null && MyAPIGateway.Utilities.IsDedicated;
                IsMultiplayer = MyAPIGateway.Multiplayer != null && MyAPIGateway.Multiplayer.MultiplayerActive;
                SessionPath = MyAPIGateway.Session.CurrentPath;
                GameStartDate = MyAPIGateway.Session.GameDateTime;
                GameVersion = MyAPIGateway.Session.Version.ToString();
                SettingsSnapshot = Settings.Instance;

                CaptureWorldSettings();
                CaptureMods();

                _identityCaptured = true;
            }
            catch (Exception e)
            {
                Exception("Telemetry.CaptureIdentity", e);
                _identityCaptured = true;
            }
        }


        private static void CaptureWorldSettings()
        {
            WorldSettingsRows.Clear();

            MyObjectBuilder_SessionSettings settings = MyAPIGateway.Session.SessionSettings;
            if (settings == null || MyAPIGateway.Utilities == null) return;

            WorldSettingsRows.AddRange(WorldSettings.Parse(MyAPIGateway.Utilities.SerializeToXML(settings)));
        }


        private static void CaptureMods()
        {
            Mods.Clear();

            List<MyObjectBuilder_Checkpoint.ModItem> mods = MyAPIGateway.Session.Mods;
            if (mods == null) return;

            for (int i = 0; i < mods.Count; i++)
            {
                MyObjectBuilder_Checkpoint.ModItem mod = mods[i];
                string name = string.IsNullOrEmpty(mod.FriendlyName) ? mod.Name : mod.FriendlyName;

                Mods.Add(mod.PublishedFileId != 0
                    ? name + " (" + mod.PublishedFileId + ")"
                    : name + " (local)");
            }
        }


        public static void NotePlanetProperties(string planet, PlanetThermalProperties properties, string supplied)
        {
            if (!Enabled || properties == null) return;

            string name = string.IsNullOrEmpty(planet) ? "(unnamed)" : planet;

            lock (RegistryLock)
            {
                PlanetProperties[name] =
                    "day " + properties.DayTemperature.ToString("n1") + " K"
                    + ", night " + properties.NightTemperature.ToString("n1") + " K"
                    + ", pole drop " + properties.PoleTemperatureDrop.ToString("n1") + " K"
                    + ", lapse " + properties.AmbientLapseRate.ToString("n2") + " K/km"
                    + ", lag " + properties.AmbientLagSeconds.ToString("n0") + " s"
                    + ", underground " + properties.UndergroundTemperature.ToString("n1") + " K"
                    + ", damping " + properties.UndergroundDampingDepth.ToString("n0") + " m"
                    + ", core " + properties.CoreTemperature.ToString("n0") + " K"
                    + ", deadzone " + properties.SealevelDeadzone.ToString("n0") + " m"
                    + ", solar decay " + properties.SolarDecay.ToString("n2")
                    + ", convection " + properties.ConvectionCoefficient.ToString("n1") + " W/m2K"
                    + " [from definition: " + supplied + "]";
            }
        }


        public static void FrameTick()
        {
            if (!Enabled) return;

            FrameCost.EndFrame(FramesObserved, SessionSeconds);

            FramesObserved++;
            CaptureIdentity();

            try
            {
                if (MyAPIGateway.Session != null) GameEndDate = MyAPIGateway.Session.GameDateTime;
            }
            catch { }
        }


        public static void Finish(string reason, bool force = false)
        {
            if (!_started || !Enabled) return;
            if (_finished && !force) return;
            if (!force) _finished = true;

            try
            {
                SessionClock.Stop();

                foreach (BlockTypeTelemetry type in BlockTypes.Values)
                {
                    type.FinalTemperatures.Clear();
                }

                for (int i = 0; i < Grids.Count; i++)
                {
                    GridTelemetry g = Grids[i];
                    if (!g.IsClosed) g.SnapshotFinalState();
                }

                TelemetryReport.Write(reason);
            }
            catch (Exception e)
            {
                MyLog.Default.Error("[" + Settings.Name + "] [Telemetry] report failed: " + e);
            }
            finally
            {
                if (!force) Enabled = false;
                if (force) SessionClock.Start();
            }
        }


        public static void Reset()
        {
            Enabled = false;
            _started = false;
            _finished = false;
            _identityCaptured = false;
            SampleStride = 4;
            Gate.Reset();

            FramesObserved = 0;
            SimulationStepsObserved = 0;
            CellUpdatesObserved = 0;
            GridsSeen = 0;
            GridRecordsDropped = 0;
            SurfaceRowsCaptured = 0;
            BlockTypeRecordsDropped = 0;
            WorldSettingsRows.Clear();
            Mods.Clear();
            PlanetProperties.Clear();
            GameVersion = "(unknown)";

            Overlay = new OverlayTelemetry();

            Vision = new ThermalVisionTelemetry();

            lock (RegistryLock)
            {
                Grids.Clear();
                GridsById.Clear();
                BlockTypes.Clear();
                Faults.Clear();
            }

            SessionClock.Reset();

            IList<ThermalGrid> live = ThermalGrid.LiveGrids;
            for (int i = 0; i < live.Count; i++)
            {
                ThermalGrid grid = live[i];
                if (grid != null) grid.RefreshTelemetry();
            }
        }



        public static GridTelemetry RegisterGrid(ThermalGrid grid)
        {
            if (!_started) Start();
            if (!Enabled || grid == null) return null;

            try
            {

                GridTelemetry record = new GridTelemetry(grid);

                lock (RegistryLock)
                {
                    GridsSeen++;

                    if (Grids.Count >= MaxGridRecords)
                    {
                        GridRecordsDropped++;
                        return null;
                    }

                    Grids.Add(record);
                    GridsById[record.EntityId] = record;
                }

                return record;
            }
            catch (Exception e)
            {
                Exception("Telemetry.RegisterGrid", e);
                return null;
            }
        }


        public static BlockTypeTelemetry GetBlockType(MyDefinitionId id)
        {
            if (!Enabled) return null;

            try
            {
                lock (RegistryLock)
                {
                    BlockTypeTelemetry type;
                    if (BlockTypes.TryGetValue(id, out type)) return type;

                    if (BlockTypes.Count >= MaxBlockTypes)
                    {
                        BlockTypeRecordsDropped++;
                        return null;
                    }


                    type = new BlockTypeTelemetry(id);
                    BlockTypes.Add(id, type);
                    return type;
                }
            }
            catch (Exception e)
            {
                Exception("Telemetry.GetBlockType", e);
                return null;
            }
        }



        public static void OnGridStepped(ThermalGrid grid, int steps)
        {
            if (!Enabled || grid == null || grid.Stats == null) return;

            try
            {
                SimulationStepsObserved += steps;
                grid.Stats.OnSteps(steps);
            }
            catch (Exception e)
            {
                Exception("Telemetry.OnGridStepped", e);
            }
        }


        public static void CheckNode(GridTelemetry grid, ThermalNode node)
        {
            CellUpdatesObserved++;

            float previous = node.Temperature - node.LastDeltaTemperature;
            TelemetryAnomalyKind kind = TelemetryAnomalies.Classify(
                node.Temperature, previous, ImplausibleTemperature);

            if (kind == TelemetryAnomalyKind.None) return;

            Anomaly(TelemetryAnomalies.Name(kind, ImplausibleTemperature),
                Describe(grid, node) + " T=" + node.Temperature.ToString("n2")
                + " from " + previous.ToString("n2"));
        }


        public static void NoteRoomLeak(GridTelemetry grid, RoomAudit audit)
        {
            string example = (grid == null ? "grid" : grid.Name) +
                ": " + audit.LeakedCells + " of " + audit.BlockCells + " fully sealed block cells read as open space" +
                ", rooms=" + audit.RoomCount;

            if (audit.Examples != null && audit.Examples.Count > 0)
            {
                example += " | " + audit.Examples[0];
            }

            Anomaly("room map treats structure as open space", example);
        }


        public static void OnCriticalDamage(ThermalBlock block, float damage)
        {
            if (!Enabled || block == null) return;

            if (block.Stats != null) block.Stats.OnCriticalDamage(damage);

            GridTelemetry grid = block.Grid != null ? block.Grid.Stats : null;
            if (grid != null)
            {
                grid.DamageEvents++;
                grid.TotalDamage += damage;
            }
        }



        public static void Anomaly(string kind, string example)
        {
            Record(kind, example, false);
        }


        public static void Exception(string where, Exception e)
        {
            Record("exception in " + where, Describe(e), true);
        }


        public static void GridFault(ThermalGrid grid, string kind)
        {
            string example;

            try
            {
                example = (grid == null || grid.Entity == null ? "(unknown grid)" : grid.Entity.DisplayName)
                    + " vented=" + (grid == null ? 0f : grid.Simulation.VentedWatts)
                    + "W made=" + (grid == null ? 0f : grid.Simulation.HeatGainWatts)
                    + "W hottest=" + (grid == null || grid.HottestNode == null
                        ? "(none)"
                        : grid.HottestNode.Block.Name + " " + grid.HottestNode.Temperature + "K");
            }
            catch
            {
                example = "(undescribable grid)";
            }

            Record(kind, example, true);
        }


        private static void Record(string kind, string example, bool fault)
        {
            if (!Enabled && !fault) return;

            try
            {
                bool log;
                lock (RegistryLock)
                {
                    log = Faults.Record(kind, example, fault, Enabled, SessionSeconds);
                }

                if (log) LogLine(kind + "\n        " + example);
            }
            catch { }
        }


        public static void LogFaultSummary()
        {
            try
            {
                string summary;
                lock (RegistryLock)
                {
                    summary = Faults.FaultSummary(Enabled);
                }

                if (summary != null) LogLine(summary);
            }
            catch { }
        }


        private static void LogLine(string text)
        {
            try
            {
                MyLog.Default.Error("[" + Settings.Name + "] " + text);
            }
            catch { }
        }


        private static string Describe(Exception e)
        {
            if (e == null) return "(null)";

            string message = e.GetType().Name + ": " + e.Message;

            try
            {
                string trace = e.StackTrace;
                if (string.IsNullOrEmpty(trace)) return message;

                string[] frames = trace.Split('\n');
                int take = frames.Length < ExceptionFrames ? frames.Length : ExceptionFrames;

                for (int i = 0; i < take; i++)
                {
                    message += "\n        " + frames[i].Trim();
                }
            }
            catch { }

            return message;
        }


        private static string Describe(GridTelemetry grid, ThermalNode node)
        {
            try
            {
                if (node == null) return "(null node)";

                string name = grid != null && grid.Name != null ? grid.Name : "(no grid)";
                return name + " / " + node.Block.Name + " " + node.Block.Position;
            }
            catch
            {
                return "(undescribable)";
            }
        }
    }
}

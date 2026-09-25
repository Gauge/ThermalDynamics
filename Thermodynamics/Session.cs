using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Draygo.BlockExtensionsAPI;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SENetworkAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
	[MySessionComponentDescriptor(MyUpdateOrder.Simulation)]
	public class Session : MySessionComponentBase
	{
        public const ushort ModID = 30323;

        public static Session Instance;
        public static DefinitionExtensionsAPI Definitions;

        private const string DumpCommand = "/thermaldump";

        private const string Command = "/thermal";

        private bool _commandRegistered;
        private long _frame;

        public Session()
        {
            MyLog.Default.Info($"[{Settings.Name}] Setup Definition Extention API");
            Definitions = new DefinitionExtensionsAPI(Done);
        }

        private void Done()
        {
            MyLog.Default.Info($"[{Settings.Name}] Definition Extention API - Done");
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            Instance = this;

            Core.ThermalValidation.Writer = WriteValidationProblem;

            NetworkAPI.Init(ModID, Settings.Name);
            NetworkAPI.LogNetworkTraffic = true;

            Settings.EnsureLoaded();

            SettingsSync.Register(this);

            Telemetry.Start();

            ThermalTerminal.Register();

            SettingsRequests.Register();

            ThermalGridSync.Register();

            ThermalApi.Register();

            RelativeTopSpeedLink.Load();

            ThermalDebugView.Current = (ThermalDebugView.Mode)Settings.Instance.DebugBlockOverlay;
            WindOverlay.Current = (WindOverlay.Mode)Settings.Instance.DebugWindOverlay;

            ThermalSettingsMenu.Initialize();
        }

        protected override void UnloadData()
        {
            SessionCleanup.Run(new Action[]
            {
                Telemetry.LogFaultSummary,
                () => Telemetry.Finish("world closing"),
                Telemetry.Reset,
                ThermalBlockCatalog.Clear,
                ThermalCoolantShapes.Clear,
                ThermalHeatPumpShapes.Clear,
                ThermalBridges.Clear,
                ThermalGrid.ResetEnvironmentCaches,
                ThermalHeatSources.Clear,
                ThermalGlow.Clear,
                ThermalVisionProbe.Reset,
                ThermalApi.Unregister,
                RelativeTopSpeedLink.Unload,
                ThermalTerminal.Unregister,
                SettingsRequests.Unregister,
                ThermalGridSync.Unregister,
                () => Instance = null,
                UnregisterCommands,
                () => { if (Definitions != null) Definitions.UnloadData(); },
                () => base.UnloadData(),
            }, ReportUnloadFailure);
        }

        private static void ReportUnloadFailure(int stage, Exception error)
        {
            MyLog.Default.Error("[Thermodynamics] unload stage " + stage + " failed: " + error);
        }

        private void UnregisterCommands()
        {
            if (_commandRegistered && MyAPIGateway.Utilities != null)
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
            _commandRegistered = false;
        }

        private const int SaveFlushFrames = 60;

        private const int SuitFrames = 60;

        private int framesSinceSaveCheck;

        public override void Simulate()
        {
            if (++framesSinceSaveCheck >= SaveFlushFrames)
            {
                framesSinceSaveCheck = 0;
                Settings.FlushPending();
            }

            _frame++;

            if (!Telemetry.Enabled)
            {
                Tick();
                return;
            }

            Telemetry.SessionFrameTime.Begin();

            Telemetry.FrameTick();
            Tick();

            Telemetry.SessionFrameTime.End();
        }

        private void Tick()
        {
            RegisterCommand();
            PollKeys();

            ThermalGridScheduler.Tick();

            PlanetProbes.Step(ThermalGrid.TickSeconds);
            ThermalHeatSourceDebug.Update(ThermalGrid.TickSeconds);

            if (_frame % 10 == 0)
            {
                ThermalBridges.Update(ThermalGrid.TickSeconds);
                ThermalTerminal.Update();
            }

            ThermalGridSync.Tick();

            if (_frame % SuitFrames == 0)
            {
                ThermalCharacters.Step(SuitFrames / 60f);
            }

            ThermalSettingsMenu.Tick();

            Debug.ShowDebugInfo();
        }

        public override void Draw()
        {
            ThermalHud.Draw();
            ThermalDebugView.Draw();
            ThermalGlow.Draw();
            ThermalVisionProbe.Draw();
            WindOverlay.Draw();
            AeroOverlay.Draw();
            ThermalDebugPanel.Update();
        }

        private void PollKeys()
        {
            ThermalVisionProbe.PollVisionKey();
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (MyAPIGateway.Input == null || MyAPIGateway.Gui == null) return;
            if (MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible) return;
            if (!MyAPIGateway.Input.IsAnyCtrlKeyPressed()) return;
            if (!MyAPIGateway.Input.IsAnyShiftKeyPressed()) return;

            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.OemPlus))
            {
                ThermalDebugView.Cycle();
                return;
            }

            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.S))
            {
                ThermalSettingsMenu.Toggle();
                return;
            }

            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.W))
            {
                WindOverlay.Cycle();
                return;
            }

            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.M))
            {
                bool shown = ThermalHud.TogglePerformancePanel();
                MyAPIGateway.Utilities.ShowNotification(
                    "Thermal performance panel " + (shown ? "on" : "off"), 2000);
            }
        }

        private void RegisterCommand()
        {
            if (_commandRegistered || MyAPIGateway.Utilities == null) return;

            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
            _commandRegistered = true;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText == null) return;

            string text = messageText.Trim();

            if (text.StartsWith(DumpCommand))
            {
                sendToOthers = false;
                Dump();
                return;
            }

            if (!text.StartsWith(Command)) return;
            sendToOthers = false;

            string argument = text.Length > Command.Length ? text.Substring(Command.Length).Trim() : "";
            RunCommand(argument);
        }

        private void RunCommand(string argument)
        {
            string lowered = argument.ToLower();

            if (lowered == "vision" || lowered.StartsWith("vision "))
            {
                Reply(ThermalVisionProbe.Run(argument.Length > 6 ? argument.Substring(6).Trim() : ""));
                return;
            }

            if (lowered == "telemetry on")
            {
                Telemetry.SetEnabled(true);
                Reply("telemetry collection ON (stride " + Telemetry.SampleStride + ")");
                return;
            }

            if (lowered == "telemetry off")
            {
                Telemetry.SetEnabled(false);
                Reply("telemetry collection OFF");
                return;
            }

            if (lowered == "dump")
            {
                Dump();
                return;
            }

            if (lowered == "menu")
            {
                ThermalSettingsMenu.Open();
                return;
            }

            if (lowered == "heat" || lowered.StartsWith("heat "))
            {
                Reply(ThermalHeatSourceDebug.Run(argument.Length > 4
                    ? argument.Substring(4).Trim() : ""));
                return;
            }

            if (lowered == "overlay")
            {
                ThermalDebugView.Cycle();
                Reply("block overlay: " + ThermalDebugView.Describe(ThermalDebugView.Current));
                return;
            }

            if (lowered == "wind")
            {
                WindOverlay.Cycle();
                Reply("wind map: " + WindOverlay.Describe(WindOverlay.Current));
                return;
            }

            if (lowered == "aero")
            {
                Settings.Instance.DebugAeroOverlay = !Settings.Instance.DebugAeroOverlay;
                Reply("aero debug view: " + (Settings.Instance.DebugAeroOverlay ? "on" : "off"));
                return;
            }

            if (lowered == "settings" || lowered == "list")
            {
                ListSettings();
                return;
            }

            if (lowered == "sync")
            {
                ReportSync();
                return;
            }

            if (lowered == "sync fetch")
            {
                Reply(SettingsSync.Fetch()
                    ? "asked the server for the settings again"
                    : "nothing to fetch: this is the server");
                return;
            }

            if (lowered.StartsWith("set "))
            {
                RunSet(argument.Substring(4).Trim());
                return;
            }

            if (lowered == "save")
            {
                Settings.Save(Settings.Instance);
                Reply("settings written to world storage");
                return;
            }

            if (lowered.StartsWith("stride "))
            {
                int stride;
                if (int.TryParse(lowered.Substring(7).Trim(), out stride) && stride > 0)
                {
                    Telemetry.SampleStride = stride;
                    Reply("telemetry sample stride " + stride);
                }
                else
                {
                    Reply("stride must be a positive whole number");
                }
                return;
            }

            if (lowered == "status" || lowered.Length == 0)
            {
                Reply("telemetry " + (Telemetry.Enabled ? "ON" : "OFF")
                    + ", stride " + Telemetry.SampleStride
                    + ", grids " + ThermalGrid.LiveGrids.Count
                    + ", block models " + ThermalBlockCatalog.ModelCount
                    + ", bridges " + ThermalBridges.Count
                    + ", validation problems " + Core.ThermalValidation.Count);
                Reply("heat glow " + ThermalGlow.LastState
                    + ", hot blocks " + ThermalGlow.LastHotBlocks
                    + ", exposed/in-range blocks " + ThermalGlow.LastSurfaceBlocks
                    + ", quads " + ThermalGlow.LastQuads
                    + ", lights " + ThermalGlow.ActiveLights);
                return;
            }

            if (lowered == "problems")
            {
                List<string> found = Core.ThermalValidation.Problems;
                if (found.Count == 0)
                {
                    Reply("no definition or settings problems reported this session");
                    return;
                }

                for (int i = 0; i < found.Count; i++) Reply(found[i]);
                return;
            }

            if (lowered.StartsWith("heat"))
            {
                RunHeat(lowered.Length > 4 ? lowered.Substring(4).Trim() : "");
                return;
            }

            Reply("commands: status | problems | settings | set <name> <value> | save"
                + " | sync [fetch] | overlay | aero | menu | telemetry on | telemetry off"
                + " | stride <n> | heat <k> | vision [[scene|survey|depth] colour|grey|scan|detail|quick|off|range auto|range <low C> <high C>|note <text>] | dump");
        }


        private static void RunHeat(string argument)
        {
            if (MyAPIGateway.Session == null || !MyAPIGateway.Session.IsServer)
            {
                Reply("heat is server side");
                return;
            }

            ThermalGrid thermals;
            ThermalBlock block;
            if (!Aimed(out thermals, out block))
            {
                Reply("aim at a block on a simulated grid");
                return;
            }

            float kelvin;
            float critical = block.Node.Thermal.CriticalTemperature;

            if (argument.Length == 0)
            {
                kelvin = critical > 0f ? critical : 1200f;
            }
            else if (!float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture,
                out kelvin) || kelvin <= 0f)
            {
                Reply("usage: /thermal heat [kelvin]");
                return;
            }

            block.Node.Temperature = kelvin;

            Reply(block.Block.BlockDefinition.Id.SubtypeName + " set to "
                + kelvin.ToString("n0") + " K, rated " + critical.ToString("n0") + " K");
        }

        private static bool Aimed(out ThermalGrid thermals, out ThermalBlock block)
        {
            thermals = null;
            block = null;

            IMyPlayer player = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.Player;
            if (player == null || player.Character == null) return false;

            MatrixD head = player.Character.GetHeadMatrix(true);
            LineD ray = new LineD(head.Translation, head.Translation + (head.Forward * 150));

            List<MyLineSegmentOverlapResult<MyEntity>> hits =
                new List<MyLineSegmentOverlapResult<MyEntity>>();
            MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref ray, hits);

            for (int i = 0; i < hits.Count; i++)
            {
                IMyCubeGrid grid = hits[i].Element as IMyCubeGrid;
                if (grid == null) continue;

                Vector3I? cell = grid.RayCastBlocks(ray.From, ray.To);
                if (!cell.HasValue) continue;

                ThermalGrid found = grid.GameLogic == null
                    ? null
                    : grid.GameLogic.GetAs<ThermalGrid>();
                if (found == null) continue;

                ThermalBlock bound = found.GetAtCell(cell.Value);
                if (bound == null || bound.Node == null) continue;

                thermals = found;
                block = bound;
                return true;
            }

            return false;
        }

        private static void WriteValidationProblem(string line)
        {
            MyLog.Default.Warning("[" + Settings.Name + "] " + line);
        }

        private void RunSet(string argument)
        {
            int space = argument.IndexOf(' ');
            if (space <= 0)
            {
                Reply("usage: /thermal set <name> <value>   (switches take 0 or 1)");
                return;
            }

            string name = Resolve(argument.Substring(0, space).Trim());
            string text = argument.Substring(space + 1).Trim().ToLower();

            if (name == null)
            {
                Reply("no such setting; /thermal settings lists them");
                return;
            }

            float value;
            if (text == "on" || text == "true") value = 1f;
            else if (text == "off" || text == "false") value = 0f;
            else if (!float.TryParse(text, out value))
            {
                Reply("could not read '" + text + "' as a number");
                return;
            }

            if (SettingsRequests.MustAsk && !Settings.ClientOwned.Contains(name))
            {
                SettingsRequests.Send(name, value);
                Reply("asked the server to set " + name);
                return;
            }

            Settings.Instance.SetValue(name, value);
            Settings.Instance.Apply();

            Reply(name + " = " + Format(name, Settings.Instance.GetValue(name)) + " (unsaved)");
        }

        private static string Resolve(string name)
        {
            List<string> names = Settings.Names();
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return names[i];
            }
            return null;
        }

        private void ListSettings()
        {
            List<string> names = Settings.Names();
            StringBuilder text = new StringBuilder();

            for (int i = 0; i < names.Count; i++)
            {
                text.Append(names[i]).Append(" = ")
                    .Append(Format(names[i], Settings.Instance.GetValue(names[i])))
                    .Append('\n');
            }

            MyAPIGateway.Utilities.ShowMissionScreen(
                Settings.Name, "Settings", "", text.ToString(), null, "Close");
        }

        private static string Format(string name, float value)
        {
            if (Settings.IsFlag(name)) return value != 0f ? "on" : "off";
            return value.ToString("0.####");
        }

        private void Dump()
        {
            if (!Telemetry.Enabled)
            {
                Reply("telemetry is off; /thermal telemetry on first");
                return;
            }

            Telemetry.Finish("manual dump", true);
            Reply("telemetry report written to world storage");
        }

        private static void ReportSync()
        {
            bool server = MyAPIGateway.Session != null && MyAPIGateway.Session.IsServer;

            Reply((server ? "server" : "client")
                + " | settings digest " + SettingsSync.Fingerprint()
                + " over " + SettingsSync.ReplicatedCount() + " values"
                + (SettingsSync.Ready ? "" : " | NOT SYNCED: nothing to send or receive"));

            Reply("  Frequency " + Settings.Instance.Frequency
                + " | HeatTimeScale " + Settings.Instance.HeatTimeScale.ToString("n0")
                + " | MaxSubsteps " + Settings.Instance.MaxSubsteps
                + " | MaxSubstepsPerBlock " + Settings.Instance.MaxSubstepsPerBlock
                + " | MaxElementVisits " + Settings.Instance.MaxElementVisitsPerStep.ToString("n0"));

            Reply("  " + ThermalGridSync.Report());

            if (!server) Reply("  digests differ? run /thermal sync fetch, then this again");
        }

        private static void Reply(string message)
        {
            MyAPIGateway.Utilities.ShowMessage(Settings.Name, message);
        }
    }
}

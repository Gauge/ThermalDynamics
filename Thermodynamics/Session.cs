using Thermodynamics.Presentation;
using Thermodynamics.Audio;
using System;
using Draygo.BlockExtensionsAPI;
using Sandbox.ModAPI;
using SENetworkAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Input;
using VRage.Utils;

namespace Thermodynamics
{
    [MySessionComponentDescriptor(MyUpdateOrder.Simulation)]
    public class Session : MySessionComponentBase
    {
        public const ushort ModID = 30323;

        public static Session Instance;
        public static DefinitionExtensionsAPI Definitions;

        private long _frame;

        public Session()
        {
            MyLog.Default.Info($"[{Settings.Name}] Setup Definition Extension API");
            Definitions = new DefinitionExtensionsAPI(Done);
        }

        private void Done()
        {
            MyLog.Default.Info($"[{Settings.Name}] Definition Extension API - Done");
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
                ThermalCockpitAudio.Reset,
                ThermalGlow.Clear,
                ThermalVisionProbe.Reset,
                ThermalApi.Unregister,
                ThermalTerminal.Unregister,
                SettingsRequests.Unregister,
                ThermalGridSync.Unregister,
                () => Instance = null,
                () =>
                {
                    if (Definitions != null) Definitions.UnloadData();
                },
                () => base.UnloadData(),
            }, ReportUnloadFailure);
        }

        private static void ReportUnloadFailure(int stage, Exception error)
        {
            MyLog.Default.Error("[Thermodynamics] unload stage " + stage + " failed: " + error);
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
            PollKeys();

            ThermalGridScheduler.Tick();

            ThermalGridDrag.Tick();
            ThermalGridTopSpeed.Tick();

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

        private static void WriteValidationProblem(string line)
        {
            MyLog.Default.Warning("[" + Settings.Name + "] " + line);
        }
    }
}

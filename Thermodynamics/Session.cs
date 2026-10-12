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
	public partial class Session : MySessionComponentBase
	{
        public const ushort ModID = 30323;

        public static Session Instance;
        public static DefinitionExtensionsAPI Definitions;

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
            _frame++;

            if (!Telemetry.Enabled)
            {
                FlushSettings();
                Tick();
                return;
            }

            // The deferred settings save writes a file, so it belongs on the clock with the rest.
            Telemetry.SessionFrameTime.Begin();

            FlushSettings();
            Telemetry.FrameTick();
            Tick();

            Telemetry.SessionFrameTime.End();
            Telemetry.Pacing.AddSimulation(Telemetry.SessionFrameTime.LastMilliseconds);
        }

        private void FlushSettings()
        {
            if (++framesSinceSaveCheck < SaveFlushFrames) return;
            framesSinceSaveCheck = 0;
            Settings.FlushPending();
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

        private static readonly System.Diagnostics.Stopwatch DrawClock = System.Diagnostics.Stopwatch.StartNew();

        private double lastDrawStarted = -1d;

        private double lastDrawMilliseconds;

        public override void Draw()
        {
            if (!Telemetry.Enabled)
            {
                lastDrawStarted = -1d;
                ThermalHud.Draw();
                ThermalDebugView.Draw();
                ThermalGlow.Draw();
                ThermalVisionProbe.Draw();
                WindOverlay.Draw();
                ThermalDebugPanel.Update();
                return;
            }

            // The gap since the last draw began is the game's frame; ours inside it is that draw
            // plus the simulation ticks since. Frame pacing in the dump compares the two.
            double started = DrawClock.Elapsed.TotalMilliseconds;
            if (lastDrawStarted >= 0d)
            {
                Telemetry.Pacing.Record(Telemetry.SessionSeconds, started - lastDrawStarted, lastDrawMilliseconds);
            }
            lastDrawStarted = started;

            TimingStat[] parts = Telemetry.DrawParts;
            parts[0].Begin(); ThermalHud.Draw(); parts[0].End();
            parts[1].Begin(); ThermalDebugView.Draw(); parts[1].End();
            parts[2].Begin(); ThermalGlow.Draw(); parts[2].End();
            parts[3].Begin(); ThermalVisionProbe.Draw(); parts[3].End();
            parts[4].Begin(); WindOverlay.Draw(); parts[4].End();
            parts[5].Begin(); ThermalDebugPanel.Update(); parts[5].End();

            lastDrawMilliseconds = DrawClock.Elapsed.TotalMilliseconds - started;
            Telemetry.DrawTime.Record(lastDrawMilliseconds);
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


    }
}

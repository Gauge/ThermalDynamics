using System;
using System.Collections.Generic;
using System.Text;
using Generated;
using Sandbox.ModAPI;

namespace Thermodynamics
{
    internal static class ThermalChatCommands
    {
        /// <summary>Toggles the aerodynamic debug view.</summary>
        [ChatCommand("aero")]
        internal static void Aero()
        {
            Settings.Instance.DebugAeroOverlay = !Settings.Instance.DebugAeroOverlay;
            Reply("aero debug view: " + (Settings.Instance.DebugAeroOverlay ? "on" : "off"));
        }

        /// <summary>Lists all thermal settings and their current values.</summary>
        [ChatCommand("settings")]
        [ChatCommand("list")]
        internal static void ListSettings()
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

        /// <summary>Reports synchronization state or fetches settings from the server.</summary>
        [ChatCommand("sync")]
        internal static void Sync(string action = null)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                ReportSync();
                return;
            }

            if (string.Equals(action, "fetch", StringComparison.OrdinalIgnoreCase))
            {
                Reply(SettingsSync.Fetch()
                    ? "asked the server for the settings again"
                    : "nothing to fetch: this is the server");
                return;
            }

            Reply("usage: /thermal sync [fetch]");
        }

        /// <summary>Changes a thermal setting without saving it.</summary>
        [ChatCommand("set")]
        internal static void Set(string name, float value)
        {
            name = Resolve(name);

            if (name == null)
            {
                Reply("no such setting; /thermal settings lists them");
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

        /// <summary>Saves the current thermal settings to world storage.</summary>
        [ChatCommand("save")]
        internal static void Save()
        {
            Settings.Save(Settings.Instance);
            Reply("settings written to world storage");
        }

        /// <summary>Changes the telemetry sampling stride.</summary>
        [ChatCommand("stride")]
        internal static void Stride(int stride)
        {
            if (stride <= 0)
            {
                Reply("stride must be a positive whole number");
                return;
            }

            Telemetry.SampleStride = stride;
            Reply("telemetry sample stride " + stride);
        }

        /// <summary>Reports the current thermal simulation status.</summary>
        [ChatCommand("status")]
        internal static void Status()
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
        }

        /// <summary>Lists definition and settings validation problems.</summary>
        [ChatCommand("problems")]
        internal static void Problems()
        {
            List<string> found = Core.ThermalValidation.Problems;
            if (found.Count == 0)
            {
                Reply("no definition or settings problems reported this session");
                return;
            }

            for (int i = 0; i < found.Count; i++) Reply(found[i]);
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

        private static string Format(string name, float value)
        {
            if (Settings.IsFlag(name)) return value != 0f ? "on" : "off";
            return value.ToString("0.####");
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

        public static void Reply(string message)
        {
            MyAPIGateway.Utilities.ShowMessage(Settings.Name, message);
        }
    }
}

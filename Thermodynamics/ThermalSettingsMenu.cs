using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Utils;

namespace Thermodynamics
{
    public static partial class ThermalSettingsMenu
    {
        private static HashSet<string> ClientSide
        {
            get { return Settings.ClientOwned; }
        }

        private static bool initialised;

        private static ThermalSettingsWindow window;

        private static Settings shipped;


        public static void Initialize()
        {
            if (initialised) return;
            if (MyAPIGateway.Utilities != null && MyAPIGateway.Utilities.IsDedicated) return;

            initialised = true;

            MyLog.Default.Info("[" + Settings.Name + "] requesting Rich HUD registration");
            RichHudClient.Init(Settings.Name, OnRegistered, OnReset);
        }


        public static void Tick()
        {
            if (window == null) return;

            if (++framesSinceStatistics < StatisticsFrames) return;
            framesSinceStatistics = 0;

            if (!window.IsOpen) return;

            Refresh();
        }

        private const int StatisticsFrames = 30;

        private static int framesSinceStatistics;


        public static void Open()
        {
            if (window == null)
            {
                MyAPIGateway.Utilities.ShowNotification(
                    "Thermodynamics: the settings menu needs the Rich HUD Master mod", 4000, "Red");
                return;
            }

            window.Show();
        }


        public static void Toggle()
        {
            if (window != null && window.IsOpen)
            {
                window.Hide();
                return;
            }

            Open();
        }


        private static void OnRegistered()
        {
            MyLog.Default.Info("[" + Settings.Name + "] Rich HUD registered; building menu and readout");

            Build();
            ThermalDebugPanel.Build();
            ThermalHud.Build();
            ThermalVisionProbe.Build();
        }


        private static void OnReset()
        {
            window = null;
            ThermalDebugPanel.Reset();
            ThermalHud.Reset();
            ThermalVisionProbe.ResetHud();
        }

        private static void Build()
        {
            bool editable = MyAPIGateway.Session == null
                || MyAPIGateway.Session.IsServer
                || SettingsRequests.MayAsk;

            bool local = MyAPIGateway.Session == null || MyAPIGateway.Session.IsServer;

            if (shipped == null) shipped = Settings.GetDefaults();


            window = new ThermalSettingsWindow(HudMain.HighDpiRoot);

            window.AddStatisticsPage();


            HashSet<string> placed = new HashSet<string>();

            AddPage(DebugPage, editable, placed, false);
            window.AddDefaultsPage(local, RestoreDefaults);


            List<string> leftovers = Unplaced();
            if (leftovers.Count > 0)
            {
                AddPage(new Leaf("Other", leftovers.ToArray()), editable, placed, false);
            }

            for (int f = 0; f < Folders.Length; f++)
            {
                Folder folder = Folders[f];
                window.AddFolder(folder.Name);

                for (int p = 0; p < folder.Pages.Length; p++)
                {
                    AddPage(folder.Pages[p], editable, placed, true);
                }
            }

            window.OpenToFirst();
            BuildTerminalEntry();
            Refresh();
        }


        private static void BuildTerminalEntry()
        {
            RichHudTerminal.Root.Enabled = true;

            TerminalButton button = new TerminalButton
            {
                Name = "Open the settings",
                ToolTip = new ToolTip
                {

                    text = new RichText("Closes this menu and opens the Thermodynamics settings"
                        + " window, which is where every setting this mod has lives."
                        + "\n\nCtrl+Shift+S opens the same window at any time, and closes it"
                        + " again; so does /thermal menu."),
                },
            };

            button.ControlChangedHandler = (sender, args) =>
            {
                RichHudTerminal.CloseMenu();
                Open();
            };


            ControlTile tile = new ControlTile();
            tile.Add(button);

            tile.Add(new TerminalLabel { Name = "or press Ctrl+Shift+S" });

            ControlCategory group = new ControlCategory
            {
                HeaderText = "Thermodynamics",
                SubheaderText = "Its settings are in a window of this mod's own",
            };
            group.Add(tile);

            ControlPage page = new ControlPage { Name = "Settings" };
            page.Add(group);

            RichHudTerminal.Root.Add(page);
        }


        private static void AddPage(Leaf leaf, bool editable, HashSet<string> placed, bool indented)
        {

            List<string> members = Take(leaf.Settings, placed);

            List<string> advanced = Take(leaf.Advanced, placed);

            string note;
            PageNotes.TryGetValue(leaf.Name, out note);


            List<string> both = new List<string>(members);
            both.AddRange(advanced);

            window.AddPage(leaf.Name, Subheader(leaf.Name, both, editable), members, advanced,
                editable, note, indented);
        }


        private static List<string> Take(string[] names, HashSet<string> placed)
        {

            List<string> taken = new List<string>();

            for (int i = 0; i < names.Length; i++)
            {
                if (placed.Contains(names[i])) continue;

                placed.Add(names[i]);
                taken.Add(names[i]);
            }

            return taken;
        }


        private static List<string> Unplaced()
        {

            HashSet<string> named = new HashSet<string>();

            for (int f = 0; f < Folders.Length; f++)
            {
                for (int p = 0; p < Folders[f].Pages.Length; p++)
                {
                    Leaf page = Folders[f].Pages[p];

                    for (int i = 0; i < page.Settings.Length; i++) named.Add(page.Settings[i]);
                    for (int i = 0; i < page.Advanced.Length; i++) named.Add(page.Advanced[i]);
                }
            }

            for (int i = 0; i < DebugPage.Settings.Length; i++) named.Add(DebugPage.Settings[i]);
            for (int i = 0; i < DebugPage.Advanced.Length; i++) named.Add(DebugPage.Advanced[i]);


            List<string> missing = new List<string>();
            List<string> names = Settings.Names();

            for (int i = 0; i < names.Count; i++)
            {
                if (!named.Contains(names[i])) missing.Add(names[i]);
            }

            return missing;
        }


        private static void RestoreDefaults()
        {
            Settings.Instance.RestoreDefaults();
            Refresh();

            MyAPIGateway.Utilities.ShowNotification(
                "Thermodynamics: every world setting back to its shipped value", 3000, "White");
        }


        public static void Refresh()
        {
            Refresh(true);
        }


        public static void Refresh(bool statistics)
        {
            if (window == null || shipped == null) return;

            try
            {
                int changed = 0;
                List<string> names = Settings.Names();

                for (int i = 0; i < names.Count; i++)
                {
                    if (Changed(names[i])) changed++;
                }

                window.Refresh();
                if (statistics) window.SetStatistics(StatisticsText(changed, names));
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Settings.Name + "] failed to refresh the settings menu\n" + e);
            }
        }


        private static string Watts(float watts)
        {
            return Units.Watts(watts);
        }


        internal static bool Changed(string name)
        {
            float mine = Settings.Instance.GetValue(name);
            float theirs = shipped.GetValue(name);

            float difference = mine - theirs;
            if (difference < 0f) difference = -difference;

            float scale = theirs < 0f ? -theirs : theirs;
            return difference > 0.0001f * (scale < 1f ? 1f : scale);
        }


        internal static string Label(string name, bool moved)
        {

            string label = EntryFor(name).Label;
            return moved ? "• " + label : label;
        }


        private static string StatisticsText(int changed, List<string> names)
        {

            StringBuilder text = new StringBuilder();

            WriteWorld(text, changed, names);
            WriteLive(text);
            WriteChanged(text, names);

            return text.ToString();
        }


        private static void WriteWorld(StringBuilder text, int changed, List<string> names)
        {
            bool server = MyAPIGateway.Session == null || MyAPIGateway.Session.IsServer;

            text.Append("THIS WORLD\n");
            Row(text, "Mod version", Settings.Name + ", config v" + Settings.CurrentVersion);
            Row(text, "Settings changed", changed + " of " + names.Count
                + (changed == 0 ? "  (all shipped defaults)" : ""));
            Row(text, "Settings digest", SettingsSync.Fingerprint());
            Row(text, "This machine", server
                ? "server; changes are saved here"
                : SettingsRequests.MayAsk
                    ? "client; changes are sent to the server"
                    : "client; world settings are read only");


            string warning = Warning();
            if (warning.Length > 0) text.Append("\nWorth knowing: ").Append(warning).Append('\n');

            text.Append("\nCompare the digest with the server's /thermal sync to tell a settings"
                + " disagreement from a simulation one.\n\n");
        }


        private static void WriteLive(StringBuilder text)
        {
            int grids = 0, floored = 0, granted = 0, critical = 0, links = 0, clamped = 0;
            long blocks = 0, nodes = 0, steps = 0, visits = 0;
            float demanded = 0f, hottest = float.MinValue;
            float vented = 0f, made = 0f, ambient = 0f, friction = 0f;
            double rate = 1d;
            int budget = int.MaxValue;

            IList<ThermalGrid> live = ThermalGrid.LiveGrids;
            for (int i = 0; live != null && i < live.Count; i++)
            {
                ThermalGrid thermals = live[i];
                if (thermals == null || thermals.Simulation == null) continue;

                ThermalSimulation simulation = thermals.Simulation;
                Core.ThermalSolver solver = simulation.Solver;

                grids++;
                blocks += thermals.BlockCount;
                nodes += solver.Nodes.Count;
                links += solver.LinkCount;
                floored += solver.FlooredNodes;
                critical += thermals.CriticalBlocks;
                steps += simulation.StepsCompleted;

                vented += simulation.VentedWatts;
                made += simulation.HeatGainWatts;
                ambient += simulation.EnvironmentWatts;
                friction += simulation.FrictionWatts;

                if (solver.LastSubsteps > granted) granted = solver.LastSubsteps;
                if (solver.LastRequiredSubsteps > demanded) demanded = solver.LastRequiredSubsteps;
                if (solver.LastStepWasClamped) clamped++;
                if (simulation.SubstepCost > visits) visits = simulation.SubstepCost;
                if (simulation.SubstepBudget < budget) budget = simulation.SubstepBudget;
                if (simulation.SimulationRate < rate) rate = simulation.SimulationRate;

                Core.ThermalNode node = thermals.HottestNode;
                if (node != null && node.Temperature > hottest) hottest = node.Temperature;
            }

            if (grids == 0)
            {
                text.Append("WHAT IS RUNNING\n");
                text.Append("    No grids are being simulated yet, so there is nothing to report"
                    + " here. This fills in as soon as a grid loads.\n\n");
                WriteFrameTime(text);
                return;
            }

            text.Append("WHAT IS RUNNING\n");
            Row(text, "Grids simulated", grids.ToString("n0"));
            Row(text, "Blocks", blocks.ToString("n0"));
            Row(text, "Solver nodes", nodes.ToString("n0"));
            Row(text, "Links between them", links.ToString("n0"));
            Row(text, "Hottest block", TemperatureScale.ToCelsiusString(hottest));
            Row(text, "Blocks over critical", critical.ToString("n0")
                + (critical == 0 ? "  (nothing is failing)" : "  (taking damage)"));

            text.Append("\nENERGY\n");
            Row(text, "Heat being made", Watts(made));
            Row(text, "Vented to the world", Watts(vented));
            Row(text, "Ambient exchange", Watts(ambient));
            Row(text, "Aerodynamic friction", Watts(friction));

            text.Append("\nSOLVER, WORST GRID\n");
            Row(text, "Substeps granted", granted + " of " + demanded.ToString("n1") + " asked for");
            Row(text, "Steps shortened", clamped == 0
                ? "none"
                : clamped + " of " + grids + " grids, to fit the budget");
            Row(text, "Blocks floored by the cap", floored.ToString("n0"));
            Row(text, "Element visits a step", visits.ToString("n0")
                + " against a budget of "
                + (budget == int.MaxValue ? "unbounded" : budget.ToString("n0") + " substeps"));
            Row(text, "Simulation rate", (100d * rate).ToString("n0") + "%"
                + (rate > 0.999d
                    ? "  (heat is keeping up with real time)"
                    : "  (heat is running slow; the step is being shortened)"));
            Row(text, "Steps completed", steps.ToString("n0"));

            text.Append('\n');
            WriteFrameTime(text);
        }


        private static void WriteFrameTime(StringBuilder text)
        {
            text.Append("FRAME COST\n");

            if (!Telemetry.Enabled)
            {
                text.Append("    Not measured. The frame timer runs only while telemetry is"
                    + " recording — turn Collect telemetry on, on the Debug page, and this"
                    + " fills in.\n\n");
                return;
            }

            TimingStat frame = Telemetry.SessionFrameTime;
            if (frame.Calls == 0)
            {
                text.Append("    Telemetry is on but no frame has been timed yet.\n\n");
                return;
            }

            Row(text, "This mod, per frame", frame.LastMilliseconds.ToString("n3") + " ms");
            Row(text, "Mean over the session", frame.MeanMilliseconds.ToString("n3") + " ms");
            Row(text, "Worst frame", frame.MaxMilliseconds.ToString("n3") + " ms");
            Row(text, "Frames timed", frame.Calls.ToString("n0"));
            Row(text, "Telemetry sampling", "1 frame in " + Telemetry.SampleStride);
            text.Append("\n    A frame is 16.7 ms at 60 updates a second, so that is the figure"
                + " these are a share of.\n\n");
        }


        private static void WriteChanged(StringBuilder text, List<string> names)
        {
            text.Append("CHANGED FROM THE SHIPPED DEFAULTS\n");

            int written = 0;
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (!Changed(name)) continue;

                written++;
                Row(text, EntryFor(name).Label,
                    Settings.Instance.GetValue(name).ToString("n2")
                        + "   was " + shipped.GetValue(name).ToString("n2"));
            }

            if (written == 0)
            {
                text.Append("    Nothing has been moved. This world runs exactly what a fresh"
                    + " install ships with.\n");
            }
        }


        private static void Row(StringBuilder text, string label, string value)
        {
            text.Append("    ").Append(label);

            for (int i = label.Length; i < RowLabelWidth; i++) text.Append(' ');

            text.Append("  ").Append(value).Append('\n');
        }

        private const int RowLabelWidth = 26;


        private static string Warning()
        {
            Settings s = Settings.Instance;

            if (s.MaxSubstepsPerBlock > 0 && s.MaxSubsteps < s.MaxSubstepsPerBlock)
            {
                return "MaxSubsteps (" + s.MaxSubsteps + ") refuses what MaxSubstepsPerBlock ("
                    + s.MaxSubstepsPerBlock + ") asks for. Raise MaxSubsteps to at least that.";
            }

            if (s.MaxElementVisitsPerStep <= 0)
            {
                return "The step budget is off. A very large grid can spend a whole frame in one step.";
            }

            if (!s.EnableEnvironment)
            {
                return "The environment is off: nothing radiates, convects or takes sunlight.";
            }

            return "";
        }


        private static string Subheader(string section, List<string> members, bool editable)
        {
            bool clientOwned = members.Count > 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (ClientSide.Contains(members[i])) continue;

                clientOwned = false;
                break;
            }

            if (clientOwned) return "Yours to change; it reaches nobody else's screen";

            if (MyAPIGateway.Session != null && !MyAPIGateway.Session.IsServer)
            {
                return editable
                    ? "World settings; your changes are sent to the server"
                    : "World settings; read only on a client";
            }

            string note;
            return SectionNotes.TryGetValue(section, out note) ? note : "";
        }

        internal static string[] OverlayNames()
        {
            string[] names = new string[ThermalDebugView.ModeCount];

            for (int mode = 0; mode < names.Length; mode++)
            {
                names[mode] = ThermalDebugView.Describe((ThermalDebugView.Mode)mode);
            }

            return names;
        }


        internal static ToolTip TipFor(string name, Entry entry)
        {
            string text = name == "DebugBlockOverlay"
                ? entry.Tip
                : entry.Tip + FidelityEnds.Sentence(name);

            if (NeedsTyping(entry))
            {

                text += "\n\nTyped; usual values run from " + Number(entry.Min, entry) + " to "

                    + Number(entry.Max, entry) + ".";
            }

            return new ToolTip { text = new RichText(text) };
        }


        internal static bool MayOffer(string name, bool editable)
        {
            return editable || ClientSide.Contains(name);
        }


        internal static void Write(string name, float value)
        {
            if (!CanEdit(name)) return;

            if (SettingsRequests.MustAsk && !Settings.ClientOwned.Contains(name))
            {
                SettingsRequests.Send(name, value);
                return;
            }

            Settings.Instance.SetValue(name, value);
            Settings.Instance.Apply();
            Refresh(false);
        }


        private static bool CanEdit(string name)
        {
            if (MyAPIGateway.Session == null || MyAPIGateway.Session.IsServer) return true;
            if (ClientSide.Contains(name)) return true;

            return SettingsRequests.MayAsk;
        }


        internal static bool NeedsTyping(Entry entry)
        {
            return (entry.Max - entry.Min) > 200f || entry.Max <= 0.1f;
        }


        internal static string Number(float value, Entry entry)
        {
            return entry.Integer
                ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.####", CultureInfo.InvariantCulture);
        }


        internal static string ValueText(float value, Entry entry)
        {
            if (entry.Integer) return ((int)Math.Round(value)).ToString();
            return value.ToString(entry.Max <= 0.1f ? "n4" : "n2");
        }


        internal static bool TryParse(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }


        internal static Entry EntryFor(string name)
        {
            Entry entry;
            if (Layout.TryGetValue(name, out entry)) return entry;

            return new Entry(Other, name, "Not yet described in the menu's layout table.", 0f, 1000f);
        }
    }
}

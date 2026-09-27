using System;
using System.Collections.Generic;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using VRageMath;

namespace Thermodynamics
{
    public sealed class ThermalSettingsWindow : WindowBase
    {
        private const float NavWidth = 232f;

        private const float RowHeight = 32f, NavRowHeight = 26f;

        private const float ControlWidth = 250f, ValueWidth = 86f;

        private const float Gap = 10f;


        private static readonly Color Body = new Color(24, 30, 36, 240);

        private static readonly Color Edge = new Color(72, 86, 98);

        private static readonly Color Selected = new Color(51, 66, 76);

        private static readonly Color Clear = new Color(0, 0, 0, 0);

        private static readonly GlyphFormat TitleFormat =

            new GlyphFormat(new Color(238, 244, 248), TextAlignment.Left, 1.2f);
        private static readonly GlyphFormat NoteFormat =

            new GlyphFormat(new Color(140, 156, 168), TextAlignment.Left, 0.95f);
        private static readonly GlyphFormat NameFormat =

            new GlyphFormat(new Color(210, 224, 232), TextAlignment.Left, 1.02f);
        private static readonly GlyphFormat DimFormat =

            new GlyphFormat(new Color(126, 138, 148), TextAlignment.Left, 1.02f);
        private static readonly GlyphFormat ValueFormat =

            new GlyphFormat(new Color(178, 196, 208), TextAlignment.Right, 1.02f);
        private static readonly GlyphFormat FolderFormat =

            new GlyphFormat(new Color(132, 148, 160), TextAlignment.Left, 0.95f);
        private static readonly GlyphFormat NavFormat =

            new GlyphFormat(new Color(206, 220, 230), TextAlignment.Left, 1.02f);
        private static readonly GlyphFormat StatFormat =

            new GlyphFormat(new Color(198, 214, 224), TextAlignment.Left, 0.98f);

        private sealed class Page
        {
            public string Name;
            public string Note;
            public ScrollBox Box;
            public LabelBoxButton Button;
        }

        private readonly ScrollBox nav;
        private readonly Label title, note;
        private readonly TexturedBox divider;
        private readonly BorderedButton close;


        private readonly List<Page> pages = new List<Page>();
        private Page current;


        private readonly List<Action> refreshers = new List<Action>();

        private bool refreshing;


        private readonly List<Action> polls = new List<Action>();

        private bool CtrlHeld;


        private readonly List<Label> statisticsLines = new List<Label>();
        private ScrollBox statisticsBox;


        public ThermalSettingsWindow(HudParentBase parent) : base(parent)
        {
            HeaderText = "Thermodynamics";

            HeaderBuilder.Format = new GlyphFormat(new Color(232, 240, 246), TextAlignment.Center, 1.1f);

            BodyColor = Body;
            BorderColor = Edge;


            Size = new Vector2(1080f, 680f);

            MinimumSize = new Vector2(720f, 400f);


            close = new BorderedButton(header)
            {
                Text = "close",

                Size = new Vector2(72f, 22f),
                ParentAlignment = ParentAlignments.InnerRight | ParentAlignments.InnerV,

                Offset = new Vector2(-8f, 0f),

                Format = new GlyphFormat(new Color(214, 226, 234), TextAlignment.Center, 0.9f),
                Color = Clear,
                BorderColor = Edge,
                ZOffset = 2,
            };
            close.MouseInput.LeftClicked += (sender, args) => Hide();


            nav = new ScrollBox(true, body)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                SizingMode = HudChainSizingModes.FitMembersOffAxis
                    | HudChainSizingModes.AlignMembersStart,
                Color = Clear,
                Width = NavWidth,
                Spacing = 2f,
            };


            divider = new TexturedBox(body)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,

                Color = new Color(56, 68, 78),
                Width = 1f,
            };


            title = new Label(body)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                AutoResize = false,
                VertCenterText = true,
                Format = TitleFormat,
                Height = 30f,
            };


            note = new Label(body)
            {
                ParentAlignment = ParentAlignments.InnerTopLeft,
                AutoResize = false,
                VertCenterText = true,
                Format = NoteFormat,
                Height = 22f,
            };

            Visible = false;
        }


        protected override void Layout()
        {
            base.Layout();

            float height = body.Height, width = body.Width;

            nav.Width = NavWidth;
            nav.Height = Math.Max(height - 2f * Gap, 1f);

            nav.Offset = new Vector2(Gap, -Gap);

            divider.Height = Math.Max(height - 2f * Gap, 1f);

            divider.Offset = new Vector2(NavWidth + 1.5f * Gap, -Gap);

            float left = NavWidth + 2f * Gap;
            float contentWidth = Math.Max(width - left - Gap, 1f);

            title.Width = contentWidth;

            title.Offset = new Vector2(left, -Gap);

            note.Width = contentWidth;

            note.Offset = new Vector2(left, -(Gap + title.Height));

            float top = Gap + title.Height + note.Height + Gap;

            for (int i = 0; i < pages.Count; i++)
            {
                ScrollBox box = pages[i].Box;
                if (!box.Visible) continue;

                box.Width = contentWidth;
                box.Height = Math.Max(height - top - Gap, 1f);

                box.Offset = new Vector2(left, -top);
            }
        }


        protected override void HandleInput(Vector2 cursorPos)
        {
            base.HandleInput(cursorPos);

            CtrlHeld = MyAPIGateway.Input != null && MyAPIGateway.Input.IsAnyCtrlKeyPressed();


            bool typing = TypingSomewhere();

            for (int i = 0; i < polls.Count; i++) polls[i]();

            if (SharedBinds.Escape.IsNewPressed && !typing) Hide();
        }


        private bool TypingSomewhere()
        {
            for (int i = 0; i < typingCells.Count; i++)
            {
                if (typingCells[i]()) return true;
            }

            return false;
        }

        private readonly List<Func<bool>> typingCells = new List<Func<bool>>();

        private readonly List<Action> closeTextInputs = new List<Action>();


        public void Show()
        {
            Visible = true;
            HudMain.EnableCursor = true;
            GetWindowFocus();
        }


        public void Hide()
        {
            foreach (Action close in closeTextInputs) close();
            Visible = false;
            HudMain.EnableCursor = false;
        }

        public bool IsOpen => Visible;



        public void AddFolder(string name)
        {
            nav.Add(new Label
            {
                AutoResize = false,
                VertCenterText = true,
                Format = FolderFormat,
                Height = NavRowHeight,

                Padding = new Vector2(10f, 0f),
                Text = name.ToUpper(),
            });
        }


        public void AddPage(string name, string subheader, IList<string> settings,
            IList<string> advanced, bool editable, string trailing, bool indented)
        {

            Page page = NewPage(name, subheader, indented);

            for (int i = 0; i < settings.Count; i++)
            {
                page.Box.Add(BuildRow(settings[i], editable));
            }

            if (advanced != null && advanced.Count > 0)
            {
                page.Box.Add(Spacer());
                page.Box.Add(Divider("Advanced"));

                for (int i = 0; i < advanced.Count; i++)
                {
                    page.Box.Add(BuildRow(advanced[i], editable));
                }
            }

            if (!string.IsNullOrEmpty(trailing))
            {
                page.Box.Add(Spacer());
                page.Box.Add(TextLine("The rest of this system: " + trailing, NoteFormat));
            }
        }


        public void AddDefaultsPage(bool local, Action restore)
        {

            Page page = NewPage("Defaults", local
                ? "Returns every world setting to the value a fresh install ships"
                : "Applied by the server; ask an administrator", false);

            page.Box.Add(TextLine(
                "A change applies as it is made and reaches the config file a second later, so"
                + " there is no Save button here. Starting over is the one case that needs an"
                + " action of its own.", NoteFormat));
            page.Box.Add(Spacer());

            BorderedButton button = new BorderedButton
            {
                Text = "Restore every world setting",

                Size = new Vector2(300f, 30f),

                Format = new GlyphFormat(new Color(226, 236, 242), TextAlignment.Center, 1.02f),
                Color = Clear,
                BorderColor = Edge,
            };

            if (local) button.MouseInput.LeftClicked += (sender, args) => restore();
            else button.UseCursor = false;


            HudChain row = new HudChain(false)
            {
                Height = 34f,
                Spacing = 10f,
                SizingMode = HudChainSizingModes.AlignMembersStart,
            };
            row.Add(button, 0f);

            page.Box.Add(row);
        }


        public void AddStatisticsPage()
        {

            Page page = NewPage("Statistics", "What this world is set to, and what it is doing", false);
            statisticsBox = page.Box;
        }


        public void SetStatistics(string text)
        {
            if (statisticsBox == null) return;

            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (i == statisticsLines.Count)
                {
                    Label line = new Label
                    {
                        AutoResize = false,
                        VertCenterText = true,
                        Format = StatFormat,
                        Height = 19f,
                    };

                    statisticsLines.Add(line);
                    statisticsBox.Add(line);
                }

                statisticsLines[i].Text = lines[i].Length == 0 ? " " : lines[i];
                statisticsLines[i].Visible = true;
            }

            for (int i = lines.Length; i < statisticsLines.Count; i++)
            {
                statisticsLines[i].Visible = false;
            }
        }


        public void OpenToFirst()
        {
            if (current == null && pages.Count > 0) Select(pages[0]);
        }


        private Page NewPage(string name, string subheader, bool indented)
        {

            ScrollBox box = new ScrollBox(true, body)
            {
                SizingMode = HudChainSizingModes.FitMembersOffAxis
                    | HudChainSizingModes.AlignMembersStart,
                ParentAlignment = ParentAlignments.InnerTopLeft,
                Color = Clear,
                Spacing = 6f,
                Visible = false,
            };

            LabelBoxButton button = new LabelBoxButton
            {
                AutoResize = false,
                VertCenterText = true,
                Format = NavFormat,
                Height = NavRowHeight,

                TextPadding = new Vector2(indented ? 26f : 12f, 0f),
                Text = name,
                Color = Clear,
                HighlightEnabled = true,

                HighlightColor = new Color(44, 56, 66),
            };

            Page page = new Page { Name = name, Note = subheader, Box = box, Button = button };
            button.MouseInput.LeftClicked += (sender, args) => Select(page);

            nav.Add(button);
            pages.Add(page);

            if (current == null) Select(page);

            return page;
        }


        private void Select(Page page)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                bool chosen = pages[i] == page;

                pages[i].Box.Visible = chosen;
                pages[i].Button.Color = chosen ? Selected : Clear;
            }

            current = page;
            title.Text = page.Name;
            note.Text = page.Note;
        }


        private static HudElementBase Divider(string text)
        {
            Label label = new Label
            {
                AutoResize = false,
                VertCenterText = true,
                Format = NoteFormat,
                Text = text,
                Width = 90f,
            };


            TexturedBox rule = new TexturedBox { Color = new Color(56, 68, 78), Height = 1f };


            HudChain row = new HudChain(false)
            {
                Height = 24f,
                Spacing = 8f,
                SizingMode = HudChainSizingModes.None,
            };

            row.Add(label, 0f);
            row.Add(rule, 1f);

            return row;
        }


        private static HudElementBase Spacer()
        {
            return new EmptyHudElement { Height = 10f, Width = 10f };
        }


        private static HudElementBase TextLine(string text, GlyphFormat format)
        {
            return new Label
            {
                AutoResize = false,
                BuilderMode = TextBuilderModes.Wrapped,
                Format = format,
                Height = 46f,
                Text = text,
            };
        }


        private HudElementBase BuildRow(string name, bool editable)
        {
            ThermalSettingsMenu.Entry entry = ThermalSettingsMenu.EntryFor(name);
            bool enabled = ThermalSettingsMenu.MayOffer(name, editable);

            Label label = new Label
            {
                AutoResize = false,
                VertCenterText = true,
                Format = enabled ? NameFormat : DimFormat,
                Text = entry.Label,
            };

            Label value = new Label
            {
                AutoResize = false,
                VertCenterText = true,
                Format = ValueFormat,
                Width = ValueWidth,
                Text = "",
            };


            HudElementBase control = BuildControl(name, entry, enabled, label, value);


            HudChain row = new HudChain(false)
            {
                Height = RowHeight,
                Spacing = 10f,
                SizingMode = HudChainSizingModes.FitMembersOffAxis,
            };

            row.Add(label, 1f);
            row.Add(control, 0f);
            row.Add(value, 0f);

            return row;
        }


        private HudElementBase BuildControl(string name, ThermalSettingsMenu.Entry entry,
            bool enabled, Label label, Label value)
        {
            ToolTip tip = ThermalSettingsMenu.TipFor(name, entry);

            if (Settings.IsFlag(name)) return Switch(name, enabled, label, tip);

            if (name == "DebugBlockOverlay")
            {
                return Choice(name, enabled, label, tip, ThermalSettingsMenu.OverlayNames(),
                    () => (int)ThermalDebugView.Current);
            }

            if (name == "AeroProfileDirections")
                return Choice(name, enabled, label, tip, new[] { "26: coarse", "98: fine" }, null, new[] { 26, 98 });

            if (name == "ShadowDetail")
            {

                return Choice(name, enabled, label, tip, ThermalSettingsMenu.ShadowDetailNames, null);
            }

            if (ThermalSettingsMenu.NeedsTyping(entry))
            {

                return Field(name, entry, enabled, label, tip);
            }


            return Slider(name, entry, enabled, label, value, tip);
        }


        private HudElementBase Switch(string name, bool enabled, Label label, ToolTip tip)
        {
            BorderedCheckBox box = new BorderedCheckBox
            {

                Size = new Vector2(26f, 26f),
                BorderColor = Edge,
                Value = Settings.Instance.GetValue(name) > 0.5f,
            };

            box.MouseInput.ToolTip = tip;
            box.UseCursor = enabled;

            box.ValueChanged += (sender, args) =>
            {
                if (refreshing) return;
                ThermalSettingsMenu.Write(name, box.Value ? 1f : 0f);
            };

            refreshers.Add(() =>
            {
                box.Value = Settings.Instance.GetValue(name) > 0.5f;
                Mark(label, name);
            });


            HudChain holder = new HudChain(false)
            {
                Width = ControlWidth,
                SizingMode = HudChainSizingModes.AlignMembersEnd,
            };
            holder.Add(box, 0f);

            return holder;
        }


        private HudElementBase Choice(string name, bool enabled, Label label, ToolTip tip,
            string[] labels, Func<int> live, int[] values = null)
        {
            Func<int> selected = live ?? (() => values == null ? (int)Settings.Instance.GetValue(name)
                : Array.IndexOf(values, (int)Settings.Instance.GetValue(name)));

            Dropdown<int> dropdown = new Dropdown<int>
            {
                Width = ControlWidth,
                Height = 26f,

                Color = new Color(38, 48, 56),
            };

            for (int i = 0; i < labels.Length; i++) dropdown.Add(new RichText(labels[i]), values == null ? i : values[i]);

            dropdown.SetSelection(selected());
            dropdown.MouseInput.ToolTip = tip;
            dropdown.UseCursor = enabled;

            dropdown.ValueChanged += (sender, args) =>
            {
                if (refreshing) return;

                ListBoxEntry<int> selection = dropdown.Value;
                if (selection == null) return;

                ThermalSettingsMenu.Write(name, selection.AssocMember);
            };

            refreshers.Add(() =>
            {
                dropdown.SetSelection(selected());
                Mark(label, name);
            });

            return dropdown;
        }


        private HudElementBase Field(string name, ThermalSettingsMenu.Entry entry, bool enabled,
            Label label, ToolTip tip)
        {
            TextField field = new TextField
            {
                Width = ControlWidth,
                Height = 26f,

                Color = new Color(38, 48, 56),
                BorderColor = Edge,

                Format = new GlyphFormat(new Color(214, 228, 236), TextAlignment.Left, 1.02f),
                Text = ThermalSettingsMenu.Number(Settings.Instance.GetValue(name), entry),
                EnableEditing = enabled,
            };

            field.MouseInput.ToolTip = tip;
            closeTextInputs.Add(() => { field.FocusHandler.ReleaseFocus(); field.CloseInput(); });
            field.UseCursor = enabled;

            field.CharFilterFunc = c =>
                (c >= '0' && c <= '9') || c == '.' || c == '-' || c == 'e' || c == 'E' || c == '+';

            field.ValueChanged += (sender, args) =>
            {
                if (refreshing) return;

                float typed;
                if (!ThermalSettingsMenu.TryParse(field.TextBoard.ToString(), out typed)) return;

                ThermalSettingsMenu.Write(name, entry.Integer ? (float)Math.Round(typed) : typed);
            };

            refreshers.Add(() =>
            {
                if (!field.InputOpen)
                    field.Text = ThermalSettingsMenu.Number(Settings.Instance.GetValue(name), entry);

                Mark(label, name);
            });

            return field;
        }


        private HudElementBase Slider(string name, ThermalSettingsMenu.Entry entry, bool enabled,
            Label label, Label value, ToolTip tip)
        {
            HudElementBase cell = new EmptyHudElement { Width = ControlWidth };


            SliderBox slider = new SliderBox(cell)
            {
                DimAlignment = DimAlignments.Size,
                Min = entry.Min,
                Max = entry.Max,
                Value = Settings.Instance.GetValue(name),
                BorderColor = Edge,

                BackgroundColor = new Color(38, 48, 56),
            };


            TextField typed = new TextField(cell)
            {
                DimAlignment = DimAlignments.Size,

                Color = new Color(38, 48, 56),

                BorderColor = new Color(120, 168, 196),

                Format = new GlyphFormat(new Color(226, 238, 246), TextAlignment.Left, 1.02f),
                Visible = false,
            };

            typed.CharFilterFunc = c =>
                (c >= '0' && c <= '9') || c == '.' || c == '-' || c == 'e' || c == 'E' || c == '+';


            MouseInputElement grab = new MouseInputElement(cell)
            {
                DimAlignment = DimAlignments.Size,
                ZOffset = sbyte.MaxValue,
                CanIgnoreMasking = true,
                Visible = false,
            };

            slider.MouseInput.ToolTip = tip;
            grab.ToolTip = tip;
            slider.UseCursor = enabled;

            value.Text = ThermalSettingsMenu.ValueText(slider.Value, entry);

            bool held = false;
            float pending = 0f;

            slider.ValueChanged += (sender, args) =>
            {
                if (refreshing || typed.Visible) return;

                float moved = entry.Integer ? (float)Math.Round(slider.Value) : slider.Value;
                value.Text = ThermalSettingsMenu.ValueText(moved, entry);

                if (slider.MouseInput.IsLeftClicked)
                {
                    held = true;
                    pending = moved;
                    return;
                }

                ThermalSettingsMenu.Write(name, moved);
            };

            grab.LeftClicked += (sender, args) =>
            {
                if (!enabled) return;

                typed.Text = ThermalSettingsMenu.Number(Settings.Instance.GetValue(name), entry);
                typed.Visible = true;
                slider.Visible = false;
                grab.Visible = false;
                typed.OpenInput();
            };

            Action close = () =>
            {
                if (!typed.Visible) return;

                typed.CloseInput();
                typed.Visible = false;
                slider.Visible = true;
            };

            closeTextInputs.Add(() => { typed.FocusHandler.ReleaseFocus(); typed.CloseInput(); close(); });

            Action commit = () =>
            {
                float parsed;
                if (ThermalSettingsMenu.TryParse(typed.TextBoard.ToString(), out parsed))
                {
                    ThermalSettingsMenu.Write(name, entry.Integer ? (float)Math.Round(parsed) : parsed);
                }

                close();
            };

            typingCells.Add(() => typed.Visible);

            polls.Add(() =>
            {
                if (held && !slider.MouseInput.IsLeftClicked)
                {
                    held = false;
                    ThermalSettingsMenu.Write(name, pending);
                }

                if (typed.Visible)
                {
                    if (SharedBinds.Escape.IsNewPressed) { close(); return; }
                    if (SharedBinds.Enter.IsNewPressed || !typed.InputOpen) commit();

                    return;
                }

                grab.Visible = enabled && CtrlHeld;
            });

            refreshers.Add(() =>
            {
                float set = Settings.Instance.GetValue(name);

                if (!slider.MouseInput.IsLeftClicked && !typed.Visible) slider.Value = set;
                if (!typed.Visible) value.Text = ThermalSettingsMenu.ValueText(set, entry);

                Mark(label, name);
            });

            return cell;
        }


        private static void Mark(Label label, string name)
        {
            label.Text = ThermalSettingsMenu.Label(name, ThermalSettingsMenu.Changed(name));
        }


        public void Refresh()
        {
            refreshing = true;

            try
            {
                for (int i = 0; i < refreshers.Count; i++) refreshers[i]();
            }
            finally
            {
                refreshing = false;
            }
        }
    }
}

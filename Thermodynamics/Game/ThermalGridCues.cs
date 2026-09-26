using System;
using System.Collections.Generic;
using Generated;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using ThermalDynamics;
using Thermodynamics.Audio;
using Thermodynamics.Core;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private const int CueInterval = 4;

        private readonly HeatCueState cueState = new HeatCueState();
        private readonly List<HeatCue> cues = new List<HeatCue>();

        private readonly Dictionary<Vector3I, float> glowing = new Dictionary<Vector3I, float>();

        private readonly List<LitBlock> litBlocks = new List<LitBlock>();

        public IList<LitBlock> LitBlocks => litBlocks;

        private readonly List<Vector3I> faded = new List<Vector3I>();

        private int stepsSinceCues;
        private float secondsSinceCues;

#if DEBUG
        private static IMyHudNotification _hudNotification;
#endif

        private void UpdateCues(int steps, float seconds)
        {
            secondsSinceCues += seconds;
            stepsSinceCues += steps;
            if (stepsSinceCues < CueInterval) return;

            float interval = secondsSinceCues;
            stepsSinceCues = 0;
            secondsSinceCues = 0f;

            Settings settings = Settings.Instance;
            bool glow = settings != null && settings.HeatGlow;
            bool audible = settings != null && settings.HeatWarningSound;

            if (!glow && !audible)
            {
                ClearGlow();
                cueState.Clear();
                return;
            }

            litBlocks.Clear();

            ThermalNode hottest = HottestNode;
            if (hottest == null || hottest.Temperature < Simulation.Solver.CueFloorTemperature())
            {
                ClearGlow();
                cueState.Clear();
                return;
            }

            cues.Clear();
            Simulation.Solver.CollectHeatCues(cueState, interval, cues);

            for (int i = 0; i < cues.Count; i++)
            {
                HeatCue cue = cues[i];
                if (cue.Block == null) continue;

                if (glow) ApplyGlow(cue);
                if (audible && cue.Announce) Announce(cue);
            }

            if (glow) FadeBlocksNoLongerCued();
        }

        private void ApplyGlow(HeatCue cue)
        {
            float glow = cue.Glow;
            float written;

            if (glow <= 0f)
            {
                if (glowing.TryGetValue(cue.Block.Position, out written)) Fade(cue.Block.Position);
                return;
            }

            LitBlock lit = new LitBlock();
            lit.Position = cue.Block.Position;
            lit.Kelvin = cue.Kelvin;
            lit.Glow = glow;
            litBlocks.Add(lit);

            MyCubeBlock cube = FatBlockAt(cue.Block.Position);
            if (cube == null) return;

            Vector3 colour = Incandescence.Colour(cue.Kelvin);
            Color emissive = new Color(colour * glow);

            if (!Emit(cube, glow, emissive)) return;
            glowing[cue.Block.Position] = glow;
        }

        [ChatCommand("playcue")]
        public static void PlayAudioCue(string stage = "overheat-warning")
        {
            IMyPlayer player = MyAPIGateway.Session == null
                ? null
                : MyAPIGateway.Session.LocalHumanPlayer;
            IMyCockpit cockpit = player == null || player.Controller == null
                ? null
                : player.Controller.ControlledEntity as IMyCockpit;

            if (cockpit == null)
            {
                ThermalChatCommands.Reply("playcue requires the local player to occupy a cockpit");
                return;
            }

            PlayAudioCue(cockpit.CubeGrid, stage);
        }

        private static void PlayAudioCue(IMyCubeGrid grid, string stage)
        {
            PwmAudioGeneratorDefinition pwmAudioGeneratorDefinition;
            if(PwmAudioGeneratorDefinition.Default.TryGetValue(stage, out pwmAudioGeneratorDefinition))
            {
                byte[] pcm = WarningToneGenerator.GeneratePcm16(pwmAudioGeneratorDefinition);
                int cockpitCount = ThermalCockpitAudio.PlayOnGrid(grid, pcm);
                if (cockpitCount == 0)
                {
                    LogHelper.Log(MyLogSeverity.Warning,
                        "ThermalGrid.PlaySound: No cockpit audio components found on grid " + grid.EntityId);
                }

#if DEBUG
                if(_hudNotification == null)
                    _hudNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, "Red");

                _hudNotification.Hide();
                _hudNotification.Text = $"Thermal cue: {stage}";
                _hudNotification.Show();
#endif

                return;
            }

            LogHelper.Log(MyLogSeverity.Warning, "ThermalGrid.PlaySound: No recipe found for stage " + stage);
        }

        private void Announce(HeatCue cue)
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (ThermalCockpitAudio.IsPlayingOnGrid(Grid)) return;

            PlayAudioCue(Grid,
                cue.Stage == HeatCueStage.Critical ? "overheat-critical" : "overheat-warning");
        }

        private void FadeBlocksNoLongerCued()
        {
            if (glowing.Count == 0) return;

            faded.Clear();
            foreach (KeyValuePair<Vector3I, float> entry in glowing)
            {
                bool held = false;
                for (int i = 0; i < cues.Count; i++)
                {
                    if (cues[i].Block == null || cues[i].Block.Position != entry.Key) continue;
                    held = cues[i].Glow > 0f;
                    break;
                }

                if (!held) faded.Add(entry.Key);
            }

            for (int i = 0; i < faded.Count; i++) Fade(faded[i]);
            faded.Clear();
        }

        private void ClearGlow()
        {
            litBlocks.Clear();

            if (glowing.Count == 0) return;

            faded.Clear();
            foreach (KeyValuePair<Vector3I, float> entry in glowing) faded.Add(entry.Key);
            for (int i = 0; i < faded.Count; i++) Fade(faded[i]);
            faded.Clear();
        }

        private void Fade(Vector3I position)
        {
            glowing.Remove(position);

            MyCubeBlock cube = FatBlockAt(position);
            if (cube == null) return;

            try
            {
                if (!cube.SetEmissiveStateWorking()) Emit(cube, 0f, Color.Black);
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.Fade", e);
            }
        }

        private static bool Emit(MyCubeBlock cube, float glow, Color emissive)
        {
            if (cube.Render == null || cube.Render.RenderObjectIDs == null
                || cube.Render.RenderObjectIDs.Length == 0)
            {
                return false;
            }

            uint id = cube.Render.RenderObjectIDs[0];
            if (id == uint.MaxValue) return false;

            try
            {
                cube.UpdateEmissiveParts(id, glow, emissive, emissive);
                return true;
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.Emit", e);
                return false;
            }
        }

        private MyCubeBlock FatBlockAt(Vector3I position)
        {
            ThermalBlock bound = Get(position);
            if (bound == null || bound.Block == null) return null;

            return bound.Block.FatBlock as MyCubeBlock;
        }
    }
}

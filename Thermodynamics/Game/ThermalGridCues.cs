using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using Thermodynamics.Presentation;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private const int CueInterval = 4;

        private static readonly MySoundPair ApproachingSound =
            new MySoundPair("ArcBlockDestroyedSmall");

        private static readonly MySoundPair CriticalSound =
            new MySoundPair("ArcBlockDestroyed");

        private readonly HeatCueState cueState = new HeatCueState();
        private readonly List<HeatCue> cues = new List<HeatCue>();

        private readonly HashSet<Vector3I> glowing = new HashSet<Vector3I>(Vector3I.Comparer);
        private readonly HashSet<Vector3I> cuedGlow = new HashSet<Vector3I>(Vector3I.Comparer);

        private readonly List<LitBlock> litBlocks = new List<LitBlock>();

        internal struct GlowSurface
        {
            public ThermalBlock Bound;
            public Vector3 Half;
            public Vector4 Emission;
            public double Radius;
        }

        private readonly List<GlowSurface> glowSurfaces = new List<GlowSurface>();
        private GlowRegion glowRegion;
        private bool glowSurfacesDirty, glowRegionDirty, glowPrunePending;

        internal List<GlowSurface> GlowSurfaces
        {
            get
            {
                PruneRemovedGlow();
                if (glowSurfacesDirty) PrepareGlowSurfaces();
                return glowSurfaces;
            }
        }

        internal bool TryGetGlowRegion(out GlowRegion region)
        {
            PruneRemovedGlow();
            if (glowRegionDirty)
            {
                GlowRegion.Reduce(litBlocks, out glowRegion);
                glowRegionDirty = false;
            }
            region = glowRegion;
            return region.Glow > 0f;
        }

        private void PrepareGlowSurfaces()
        {
            glowSurfaces.Clear();
            for (int i = 0; i < litBlocks.Count; i++)
            {
                LitBlock lit = litBlocks[i];
                ThermalBlock bound = Get(lit.Position);
                if (bound == null || bound.Block == null) continue;
                Vector3 half = FaceQuad.HalfExtents(bound.Block.Min, bound.Block.Max, Grid.GridSize);
                glowSurfaces.Add(new GlowSurface
                {
                    Bound = bound,
                    Half = half,
                    Emission = HeatGlowStyle.LinearEmission(Incandescence.Colour(lit.Kelvin), lit.Glow),
                    Radius = HeatGlowStyle.CullRadius(half, Grid.GridSize),
                });
            }
            glowSurfacesDirty = false;
        }

        public IList<LitBlock> LitBlocks
        {
            get { PruneRemovedGlow(); return litBlocks; }
        }

        private void ForgetGlow(Vector3I position)
        {
            if (glowing.Contains(position)) Fade(position);
            if (!cuedGlow.Remove(position)) return;
            glowPrunePending = glowSurfacesDirty = glowRegionDirty = true;
        }

        private void PruneRemovedGlow()
        {
            if (!glowPrunePending) return;
            HeatCueLifetimes.Prune(litBlocks, cuedGlow);
            glowSurfaces.Clear();
            glowPrunePending = false;
        }

        private readonly List<Vector3I> faded = new List<Vector3I>();

        private int stepsSinceCues;
        private float secondsSinceCues;

        private static MyEntity3DSoundEmitter cueEmitter;

        public static void ResetCues()
        {
            for (int i = 0; i < Live.Count; i++)
            {
                Live[i].ClearGlow();
                Live[i].cueState.Clear();
            }
            if (cueEmitter == null) return;
            MyEntity3DSoundEmitter emitter = cueEmitter;
            cueEmitter = null;
            emitter.StopSound(true, true, true);
            emitter.Entity = null;
        }

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

            if (!glow) ClearGlow();
            litBlocks.Clear();
            glowSurfaces.Clear();
            cuedGlow.Clear();
            glowPrunePending = false;
            glowSurfacesDirty = glowRegionDirty = glow;

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

            if (glow)
            {
                FadeBlocksNoLongerCued();
            }
        }

        private void ApplyGlow(HeatCue cue)
        {
            float glow = cue.Glow;
            if (glow <= 0f)
            {
                if (glowing.Contains(cue.Block.Position)) Fade(cue.Block.Position);
                return;
            }

            cuedGlow.Add(cue.Block.Position);

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
            glowing.Add(cue.Block.Position);
        }

        private void Announce(HeatCue cue)
        {
            if (!IsPilotedLocally()) return;

            if (cueEmitter == null) cueEmitter = new MyEntity3DSoundEmitter(null);
            if (cueEmitter.IsPlaying) return;

            cueEmitter.SetPosition(null);
            cueEmitter.Force2D = true;
            cueEmitter.PlaySound(
                cue.Stage == HeatCueStage.Critical ? CriticalSound : ApproachingSound, true);
        }

        private bool IsPilotedLocally()
        {
            if (MyAPIGateway.Session == null) return false;

            IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
            if (player == null) return false;

            IMyCubeBlock seat = player.Controller == null
                ? null
                : player.Controller.ControlledEntity as IMyCubeBlock;

            return seat != null && seat.CubeGrid == Grid;
        }

        private void FadeBlocksNoLongerCued()
        {
            if (glowing.Count == 0) return;

            faded.Clear();
            foreach (Vector3I position in glowing)
            {
                if (!cuedGlow.Contains(position)) faded.Add(position);
            }

            for (int i = 0; i < faded.Count; i++) Fade(faded[i]);
            faded.Clear();
        }

        private void ClearGlow()
        {
            cues.Clear();
            litBlocks.Clear();
            glowSurfaces.Clear();
            cuedGlow.Clear();
            glowRegion = new GlowRegion();
            glowPrunePending = glowSurfacesDirty = glowRegionDirty = false;

            if (glowing.Count == 0) return;

            faded.Clear();
            foreach (Vector3I position in glowing) faded.Add(position);
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
                bool restored = cube.IsWorking ? cube.SetEmissiveStateWorking()
                    : cube.IsFunctional ? cube.SetEmissiveStateDisabled()
                    : cube.SetEmissiveStateDamaged();
                if (!restored) Emit(cube, 0f, Color.Black);
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

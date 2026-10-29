using System;
using System.Collections.Generic;
using Sandbox.Game.Lights;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using Thermodynamics.Presentation;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Thermodynamics
{
    public static class ThermalGlow
    {
        private static readonly MyStringId GlowMaterial = MyStringId.GetOrCompute("GaugeHeatGlow");

        public static int LastQuads { get; private set; }
        public static int LastHotBlocks { get; private set; }
        public static int LastSurfaceBlocks { get; private set; }
        public static int ActiveLights { get { return Lights.Count; } }
        public static string LastState { get; private set; }

        private const float LightIntensity = 2.5f;

        private const float LightReach = 6f;

        private const float LightRangeCap = 120f;

        private const double LightViewRange = 200.0;

        private static readonly Dictionary<long, MyLight> Lights = new Dictionary<long, MyLight>();

        private static readonly List<long> Extinguished = new List<long>();
        private static readonly Vector3D[] Normals = new Vector3D[Face.Count];
        private static readonly Vector3[] Left = new Vector3[Face.Count];
        private static readonly Vector3[] Up = new Vector3[Face.Count];

        public static void Draw()
        {
            LastQuads = LastHotBlocks = LastSurfaceBlocks = 0;
            LastState = "no hot cues";

            Settings settings = Settings.Instance;
            if (settings == null || !settings.HeatGlow)
            {
                Clear();
                LastState = "off";
                return;
            }

            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated
                || MyAPIGateway.Session == null || MyAPIGateway.Session.Camera == null)
            {
                Clear();
                LastState = "no client camera";
                return;
            }

            if (ThermalVisionProbe.ReplacesScene)
            {
                Clear();
                LastState = "thermal vision";
                return;
            }

            Vector3D eye = MyAPIGateway.Session.Camera.WorldMatrix.Translation;

            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;
            int quads = 0;

            Extinguished.Clear();
            foreach (KeyValuePair<long, MyLight> entry in Lights) Extinguished.Add(entry.Key);

            for (int i = 0; i < grids.Count; i++)
            {
                ThermalGrid thermals = grids[i];
                if (thermals == null || thermals.LitBlocks.Count == 0) continue;
                if (thermals.Grid == null || thermals.Grid.MarkedForClose) continue;

                LastHotBlocks += thermals.LitBlocks.Count;
                BoundingBoxD bounds = thermals.Grid.PositionComp.WorldAABB;
                double distance = Math.Max(0.0, Vector3D.Distance(bounds.Center, eye) - bounds.HalfExtents.Length());
                if (distance >= HeatGlowStyle.DrawRange) continue;

                // The weighted cue centre lies inside the grid bounds. Even the largest
                // permitted light cannot reach the viewing range beyond this lower bound.
                Extinguished.Remove(thermals.Grid.EntityId);
                if (distance < LightRangeCap + LightViewRange)
                {
                    UpdateLight(thermals, ref eye);
                }
                else
                {
                    Extinguish(thermals.Grid.EntityId);
                }

                BoundingSphereD sphere = new BoundingSphereD(bounds.Center,
                    HeatGlowStyle.CullRadius((Vector3)bounds.HalfExtents, thermals.Grid.GridSize));
                if (quads < HeatGlowStyle.MaxQuads && MyAPIGateway.Session.Camera.IsInFrustum(ref sphere))
                    quads += DrawGrid(thermals, ref eye, HeatGlowStyle.MaxQuads - quads);
            }

            for (int i = 0; i < Extinguished.Count; i++) Extinguish(Extinguished[i]);
            Extinguished.Clear();

            LastQuads = quads;
            if (LastHotBlocks > 0) LastState = quads > 0 ? "submitted" : "culled";
        }

        public static void Clear()
        {
            LastQuads = LastHotBlocks = LastSurfaceBlocks = 0;
            LastState = "cleared";
            Extinguished.Clear();
            foreach (KeyValuePair<long, MyLight> entry in Lights) Extinguished.Add(entry.Key);
            for (int i = 0; i < Extinguished.Count; i++) Extinguish(Extinguished[i]);
            Extinguished.Clear();
        }

        private static void Extinguish(long entityId)
        {
            MyLight light;
            if (!Lights.TryGetValue(entityId, out light)) return;

            Lights.Remove(entityId);
            if (light == null) return;

            light.LightOn = false;
            light.GlareOn = false;
            light.UpdateLight();
            MyLights.RemoveLight(light);
        }

        private static void UpdateLight(ThermalGrid thermals, ref Vector3D eye)
        {
            GlowRegion region;
            Vector3D centre;
            float radius;
            if (!Region(thermals, out region, out centre, out radius))
            {
                Extinguish(thermals.Grid.EntityId);
                return;
            }

            float lightRange = Math.Min(LightRangeCap, radius + LightReach);
            float fade = HeatGlowStyle.RangeFade(Math.Max(0.0,
                Vector3D.Distance(centre, eye) - lightRange), LightViewRange);
            if (fade <= 0f)
            {
                Extinguish(thermals.Grid.EntityId);
                return;
            }

            MyLight light;
            if (!Lights.TryGetValue(thermals.Grid.EntityId, out light) || light == null)
            {
                if (Lights.Count >= HeatGlowStyle.MaxLights) return;
                light = MyLights.AddLight();
                if (light == null) return;

                light.Start("ThermodynamicsHeat");
                light.LightType = MyLightType.DEFAULT;
                light.CastShadows = false;
                light.Falloff = 1.5f;
                Lights[thermals.Grid.EntityId] = light;
            }

            Vector3 locus = Incandescence.Colour(region.Kelvin);

            light.Position = centre;
            light.Color = new Color(locus);
            light.Intensity = region.Glow * LightIntensity * fade;
            light.Range = lightRange;
            light.LightOn = true;
            light.UpdateLight();
        }

        private static bool Region(ThermalGrid thermals, out GlowRegion region, out Vector3D centre,
            out float radius)
        {
            centre = Vector3D.Zero;
            radius = 0f;

            if (!thermals.TryGetGlowRegion(out region)) return false;

            float gridSize = thermals.Grid.GridSize;
            MatrixD gridMatrix = thermals.Grid.WorldMatrix;

            centre = Vector3D.Transform(region.Centre * gridSize, gridMatrix);
            radius = region.RadiusCells * gridSize;
            return true;
        }

        private static int DrawGrid(ThermalGrid thermals, ref Vector3D eye, int budget)
        {
            MatrixD gridMatrix = thermals.Grid.WorldMatrix;
            float gridSize = thermals.Grid.GridSize;

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3 localLeft, localUp;
                FaceQuad.Tangents(face, out localLeft, out localUp);
                Normals[face] = Vector3D.TransformNormal(Face.Normals[face], gridMatrix);
                Left[face] = (Vector3)Vector3D.TransformNormal(localLeft, gridMatrix);
                Up[face] = (Vector3)Vector3D.TransformNormal(localUp, gridMatrix);
            }

            List<ThermalGrid.GlowSurface> surfaces = thermals.GlowSurfaces;
            int quads = 0;

            for (int i = 0; i < surfaces.Count && quads < budget; i++)
            {
                ThermalGrid.GlowSurface surface = surfaces[i];
                ThermalBlock bound = surface.Bound;
                if (bound == null || bound.SweepSlot < 0 || bound.Block == null || bound.Node == null) continue;

                ThermalNode node = bound.Node;
                if (node.TotalExposedFaces == 0) continue;

                Vector3D centre;
                bound.Block.ComputeWorldCenter(out centre);
                double distance = Vector3D.Distance(centre, eye);
                float fade = HeatGlowStyle.RangeFade(distance, HeatGlowStyle.DrawRange);
                if (fade <= 0f) continue;

                Vector3 half = surface.Half;
                BoundingSphereD sphere = new BoundingSphereD(centre, surface.Radius);
                if (!MyAPIGateway.Session.Camera.IsInFrustum(ref sphere)) continue;
                LastSurfaceBlocks++;
                Vector4 colour = surface.Emission * fade;

                for (int face = 0; face < Face.Count && quads < budget; face++)
                {
                    if (node.GetExposedFaces(face) == 0) continue;
                    if (DrawFace(face, ref centre, ref half, ref eye, ref colour, gridSize))
                    {
                        quads++;
                    }
                }
            }

            return quads;
        }

        private static bool DrawFace(int face, ref Vector3D centre, ref Vector3 half,
            ref Vector3D eye, ref Vector4 colour, float gridSize)
        {
            Vector3 localNormal = Face.Normals[face];
            Vector3D normal = Normals[face];

            float reach = FaceQuad.Extent(ref half, ref localNormal);
            Vector3D position = centre + (normal * (reach + HeatGlowStyle.StandOff(gridSize)));
            Vector3D toEye = eye - position;
            double distance = toEye.Length();
            if (distance <= 0.0001) return false;
            float facing = HeatGlowStyle.FacingFade(Vector3D.Dot(normal, toEye) / distance);
            if (facing <= 0f) return false;

            Vector3 localLeft, localUp;
            FaceQuad.Tangents(face, out localLeft, out localUp);

            MyTransparentGeometry.AddBillboardOriented(
                GlowMaterial,
                HeatGlowStyle.BillboardColour(colour, facing),
                position,
                Left[face],
                Up[face],
                FaceQuad.Extent(ref half, ref localLeft) * HeatGlowStyle.HaloScale,
                FaceQuad.Extent(ref half, ref localUp) * HeatGlowStyle.HaloScale,
                Vector2.Zero,
                BlendTypeEnum.AdditiveBottom);

            return true;
        }

    }

}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Generated;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Thermodynamics
{
    public static class ThermalDebugView
    {
        public enum Mode
        {
            Off = 0,
            Temperature = 1,
            SolarWatts = 2,
            ExposedFaces = 3,
            FrictionWatts = 4,
            Rooms = 5,
        }

        public const int ModeCount = (int)Mode.Rooms + 1;

        public static Mode Current;

        private static readonly MyStringId FaceMaterial = MyStringId.GetOrCompute("Square");
        private static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("Square");

        private static readonly List<ThermalGrid> Targets = new List<ThermalGrid>();

        public static readonly OverlayBudget Budget = new OverlayBudget();

        private static long billboards;

        private static readonly Stopwatch DrawClock = new Stopwatch();

        private static double coneSin, coneCos;

        public static ThermalGrid Focus { get; private set; }

        private const double PickRange = 300;

        private const double BandScale = 0.02;

        private const float FaceAlpha = 0.22f;

        private const float SurfaceAlpha = 0.75f;

        public static bool NeedsWatts
        {
            get { return Current == Mode.SolarWatts || Current == Mode.FrictionWatts; }
        }

        /// <summary>Cycles the block thermal overlay.</summary>
        [ChatCommand("overlay")]
        internal static void Overlay()
        {
            Cycle();
            ThermalChatCommands.Reply("block overlay: " + Describe(Current));
        }

        public static void Cycle()
        {
            Current = (Mode)(((int)Current + 1) % ModeCount);
            Announce();
        }

        public static void Set(Mode mode)
        {
            Current = mode;
            Announce();
        }

        private static void Announce()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            MyAPIGateway.Utilities.ShowNotification("thermal overlay: " + Describe(Current), 2000, "White");
        }

        public static string Describe(Mode mode)
        {
            switch (mode)
            {
                case Mode.Temperature: return "temperature";
                case Mode.SolarWatts: return "solar watts";
                case Mode.ExposedFaces: return "exposed faces";
                case Mode.FrictionWatts: return "friction watts";
                case Mode.Rooms: return "rooms";
                default: return "off";
            }
        }

        public static void Draw()
        {
            if (Current == Mode.Off)
            {
                Focus = null;
                return;
            }

            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (MyAPIGateway.Session == null || MyAPIGateway.Session.Camera == null) return;

            MatrixD camera = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D eye = camera.Translation;

            CollectTargets(ref camera, ref eye);
            Focus = Targets.Count > 0 ? Targets[Targets.Count - 1] : null;

            BeginFrame();

            for (int i = 0; i < Targets.Count; i++)
            {
                DrawGrid(Targets[i], ref camera, ref eye);
            }

            EndFrame();
            Targets.Clear();
        }

        private static void BeginFrame()
        {
            Budget.MaxBoxes = Settings.Instance == null ? 12000 : Settings.Instance.DebugOverlayMaxBoxes;
            Budget.BeginFrame();
            billboards = 0;

            IMyCamera camera = MyAPIGateway.Session.Camera;
            Vector2 viewport = camera.ViewportSize;
            double aspect = viewport.Y > 0 ? viewport.X / viewport.Y : 1.0;
            double half = OverlayBudget.ConeHalfAngle(camera.FovWithZoom, aspect);

            coneSin = Math.Sin(half);
            coneCos = Math.Cos(half);

            if (!Telemetry.Enabled) return;

            DrawClock.Reset();
            DrawClock.Start();
        }

        private static void EndFrame()
        {
            Budget.EndFrame();

            if (!Telemetry.Enabled || Budget.Considered == 0) return;

            DrawClock.Stop();

            Telemetry.Overlay.Frame(
                Describe(Current), Budget.Considered, Budget.Drawn, Budget.OffScreen, Budget.OverBudget,
                billboards, Budget.Radius, DrawClock.Elapsed.TotalMilliseconds);
        }

        private static bool Wanted(ref Vector3D delta, double boxRadius, ref MatrixD camera)
        {
            Vector3D forward = camera.Forward;

            if (!OverlayBudget.InView(ref delta, boxRadius, ref forward, coneSin, coneCos))
            {
                Budget.Cull();
                return false;
            }

            return Budget.Accept(delta.Length());
        }

        private static void Box(
            ref MatrixD box, ref BoundingBoxD local, ref Color colour,
            MySimpleObjectRasterizer rasterizer, double thickness)
        {
            MySimpleObjectDraw.DrawTransparentBox(
                ref box,
                ref local,
                ref colour,
                rasterizer,
                1,
                (float)(thickness * BandScale),
                FaceMaterial,
                LineMaterial,
                false,
                -1,
                BlendTypeEnum.PostPP);

            billboards += rasterizer == MySimpleObjectRasterizer.Wireframe ? 12 : 18;
        }

        private static void CollectTargets(ref MatrixD camera, ref Vector3D eye)
        {
            Targets.Clear();

            IMyEntity controlled = MyAPIGateway.Session.Player == null
                ? null
                : MyAPIGateway.Session.Player.Controller.ControlledEntity as IMyEntity;

            IMyCubeBlock seat = controlled as IMyCubeBlock;
            if (seat != null) Add(seat.CubeGrid as MyCubeGrid);

            IHitInfo hit;
            MyAPIGateway.Physics.CastRay(eye, eye + (camera.Forward * PickRange), out hit);
            if (hit != null) Add(hit.HitEntity as MyCubeGrid);
        }

        private static void Add(MyCubeGrid grid)
        {
            if (grid == null || grid.GameLogic == null) return;

            ThermalGrid thermals = grid.GameLogic.GetAs<ThermalGrid>();
            if (thermals == null || thermals.Simulation == null) return;
            if (Targets.Contains(thermals)) return;

            Targets.Add(thermals);
        }

        private static void DrawGrid(ThermalGrid thermals, ref MatrixD camera, ref Vector3D eye)
        {
            if (Current == Mode.Rooms)
            {
                DrawRooms(thermals, ref camera, ref eye);
                return;
            }

            if (Current == Mode.SolarWatts)
            {
                DrawSolarSurfaces(thermals, ref camera, ref eye);
                return;
            }

            MatrixD gridMatrix = thermals.Grid.WorldMatrix;
            float gridSize = thermals.Grid.GridSize;

            foreach (ThermalBlock bound in thermals.Blocks)
            {
                ThermalNode node = bound.Node;
                if (node == null) continue;

                Vector3D centre;
                bound.Block.ComputeWorldCenter(out centre);

                Vector3D delta = centre - eye;

                Vector3D half = (Vector3D)(bound.Block.Max - bound.Block.Min + Vector3I.One)
                    * (gridSize * 0.5);

                if (!Wanted(ref delta, half.Length(), ref camera)) continue;

                MatrixD box = gridMatrix;
                box.Translation = eye + (delta * BandScale);

                BoundingBoxD local = new BoundingBoxD(-half * BandScale, half * BandScale);
                Color colour = Colour(node);

                Box(ref box, ref local, ref colour, MySimpleObjectRasterizer.SolidAndWireframe, 0.02);
            }
        }

        private static void DrawSolarSurfaces(ThermalGrid thermals, ref MatrixD camera, ref Vector3D eye)
        {
            EnvironmentState state = thermals.LastState;
            ThermalSolver solver = thermals.Simulation.Solver;
            MatrixD gridMatrix = thermals.Grid.WorldMatrix;
            float gridSize = thermals.Grid.GridSize;

            bool lit = Settings.Instance.EnableSolarHeat
                && !state.IsSolarOccluded
                && state.SolarEnergy > 0f;

            Vector3 sun = state.SunDirectionLocal;

            foreach (ThermalBlock bound in thermals.Blocks)
            {
                ThermalNode node = bound.Node;
                if (node == null || node.TotalExposedFaces == 0) continue;

                Vector3D centre;
                bound.Block.ComputeWorldCenter(out centre);

                Vector3D delta = centre - eye;

                Vector3 half = FaceQuad.HalfExtents(bound.Block.Min, bound.Block.Max, gridSize);

                if (!Wanted(ref delta, half.Length(), ref camera)) continue;

                for (int face = 0; face < Face.Count; face++)
                {
                    if (node.GetExposedFaces(face) == 0) continue;

                    Vector3 localNormal = Face.Normals[face];
                    Vector3D normal = Vector3D.TransformNormal(localNormal, gridMatrix);

                    Vector3D position = centre + (normal * FaceQuad.Extent(ref half, ref localNormal));
                    if (Vector3D.Dot(normal, position - eye) >= 0) continue;

                    float dot = Vector3.Dot(localNormal, sun);
                    float irradiance = lit && dot > 0f ? state.SolarEnergy * dot : 0f;

                    irradiance *= solver.SunLitFraction(node.Index, face);

                    Color colour = ColorExtensions.HSVtoColor(
                        TemperatureScale.ToHsv(irradiance, 1400f, 1f, 1000f));
                    colour.A = (byte)(SurfaceAlpha * 255f);

                    Vector3 localLeft, localUp;
                    FaceQuad.Tangents(face, out localLeft, out localUp);

                    Vector3 left = (Vector3)Vector3D.TransformNormal(localLeft, gridMatrix);
                    Vector3 up = (Vector3)Vector3D.TransformNormal(localUp, gridMatrix);

                    MyTransparentGeometry.AddBillboardOriented(
                        FaceMaterial,
                        colour,
                        eye + ((position - eye) * BandScale),
                        left,
                        up,
                        (float)(FaceQuad.Extent(ref half, ref localLeft) * BandScale),
                        (float)(FaceQuad.Extent(ref half, ref localUp) * BandScale),
                        Vector2.Zero,
                        BlendTypeEnum.PostPP);

                    billboards++;
                }
            }
        }

        private static void DrawRooms(ThermalGrid thermals, ref MatrixD camera, ref Vector3D eye)
        {
            RoomMap map = thermals.Simulation.Rooms.Map;
            if (map == null || map.IsEmpty) return;

            MatrixD gridMatrix = thermals.Grid.WorldMatrix;
            float gridSize = thermals.Grid.GridSize;

            Vector3D half = new Vector3D(gridSize * 0.45);

            float min = Settings.Instance.RoomOverlayMinKelvin;
            float max = Settings.Instance.RoomOverlayMaxKelvin;

            for (int room = 0; room < map.RoomCount; room++)
            {
                RoomAirNode air = AirOf(thermals, room);

                bool hasAir = air != null && air.HasAir;

                bool disagrees = Disagrees(thermals, room);

                Color fill = hasAir
                    ? Fill(air.Temperature, min, max)
                    : DryFill(disagrees);

                Color edge = RoomColour(room, map.IsVented(room));

                foreach (Vector3I cell in map.CellsOf(room))
                {
                    Vector3D centre = thermals.Grid.GridIntegerToWorld(cell);
                    Vector3D delta = centre - eye;

                    if (!Wanted(ref delta, half.Length(), ref camera)) continue;

                    MatrixD box = gridMatrix;
                    box.Translation = eye + (delta * BandScale);

                    BoundingBoxD local = new BoundingBoxD(-half * BandScale, half * BandScale);

                    Box(ref box, ref local, ref fill,
                        hasAir || disagrees
                            ? MySimpleObjectRasterizer.SolidAndWireframe
                            : MySimpleObjectRasterizer.Wireframe,
                        0.02);

                    if (!hasAir)
                    {
                        Color dryEdge = edge;
                        dryEdge.A = (byte)(disagrees ? 255 : 90);

                        Box(ref box, ref local, ref dryEdge, MySimpleObjectRasterizer.Wireframe,
                            disagrees ? 0.04 : 0.02);

                        continue;
                    }

                    Box(ref box, ref local, ref edge, MySimpleObjectRasterizer.Wireframe, 0.02);
                }
            }

            DrawLostRooms(thermals, ref camera, ref eye, gridMatrix, half);
        }

        private static void DrawLostRooms(
            ThermalGrid thermals, ref MatrixD camera, ref Vector3D eye, MatrixD gridMatrix, Vector3D half)
        {
            IList<ThermalGrid.LostRoom> lost = thermals.LostRooms;
            if (lost.Count == 0) return;

            for (int i = 0; i < lost.Count; i++)
            {
                ThermalGrid.LostRoom room = lost[i];
                if (room.Cells == null) continue;

                Color fill = LostRoomColour(room.Index, room.VentSaysPressurised);

                foreach (Vector3I cell in room.Cells)
                {
                    Vector3D centre = thermals.Grid.GridIntegerToWorld(cell);
                    Vector3D delta = centre - eye;

                    if (!Wanted(ref delta, half.Length(), ref camera)) continue;

                    MatrixD box = gridMatrix;
                    box.Translation = eye + (delta * BandScale);

                    BoundingBoxD local = new BoundingBoxD(-half * BandScale, half * BandScale);

                    Box(ref box, ref local, ref fill, MySimpleObjectRasterizer.SolidAndWireframe, 0.04);
                }
            }
        }

        private static Color LostRoomColour(int index, bool ventSaysPressurised)
        {
            float hue = ((index * 0.61803399f) % 1f) * 0.13f;

            Color colour = ColorExtensions.HSVtoColor(
                new Vector3(hue, 1f, ventSaysPressurised ? 1f : 0.55f));

            colour.A = (byte)(LostRoomFillAlpha * 255f);
            return colour;
        }

        private const float LostRoomFillAlpha = 0.25f;

        private static bool Disagrees(ThermalGrid thermals, int room)
        {
            IList<ThermalGrid.RoomVerdict> verdicts = thermals.RoomVerdicts;
            if (room < 0 || room >= verdicts.Count) return false;

            return verdicts[room].IsDisagreement;
        }

        private static Color DryFill(bool disagrees)
        {
            if (!disagrees) return new Color(90, 90, 90, 25);

            Color colour = new Color(255, 0, 200);
            colour.A = (byte)(DisagreementFillAlpha * 255f);
            return colour;
        }

        private const float DisagreementFillAlpha = 0.3f;

        private static RoomAirNode AirOf(ThermalGrid thermals, int room)
        {
            IList<RoomAirNode> air = thermals.Simulation.RoomAir;
            for (int i = 0; i < air.Count; i++)
            {
                if (air[i].RoomIndex == room) return air[i];
            }
            return null;
        }

        private static Color Fill(float kelvin, float min, float max)
        {
            if (max <= min) max = min + 1f;

            float span = max - min;
            Color colour = ColorExtensions.HSVtoColor(
                TemperatureScale.ToHsv(kelvin - min, span, span * 0.05f, span * 0.9f));

            colour.A = (byte)(RoomFillAlpha * 255f);
            return colour;
        }

        private const float RoomFillAlpha = 0.35f;

        private static Color RoomColour(int room, bool vented)
        {
            float hue = (room * 0.61803399f) % 1f;
            Color colour = ColorExtensions.HSVtoColor(new Vector3(hue, vented ? 0.3f : 0.9f, 1f));

            colour.A = (byte)(255f * (vented ? 0.35f : 0.8f));
            return colour;
        }

        private static Color Colour(ThermalNode node)
        {
            Vector3 hsv;
            switch (Current)
            {
                case Mode.SolarWatts:
                    hsv = TemperatureScale.ToHsv(Math.Abs(node.LastSolarWatts), 20000, 100, 5000);
                    break;
                case Mode.ExposedFaces:
                    hsv = TemperatureScale.ToHsv(node.TotalExposedFaces, 6, 0, 6);
                    break;
                case Mode.FrictionWatts:
                    hsv = TemperatureScale.ToHsv(Math.Abs(node.LastFrictionWatts), 20000, 100, 5000);
                    break;
                default:
                    hsv = TemperatureScale.ToHsv(node.Temperature);
                    break;
            }

            Color colour = ColorExtensions.HSVtoColor(hsv);
            colour.A = (byte)(FaceAlpha * 255f);
            return colour;
        }
    }
}

using System;
using System.Collections.Generic;
using Generated;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static class WindOverlay
    {
        public enum Mode
        {
            Off = 0,
            Local = 1,
            Planet = 2,
        }

        public const int ModeCount = (int)Mode.Planet + 1;

        public static Mode Current;

        private static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("Square");


        private const double LocalRadius = 5000d;

        private const double LocalSpacing = 250d;

        private const double LocalSurfaceOffset = 10d;


        private const double PlanetLatitudeStep = 7.5d;

        private const double PlanetLongitudeStep = 15d;

        private const double PlanetAltitudeShare = 0.02d;

        private const double PlanetArrowShare = 0.03d;


        private const int SampleInterval = 20;

        private const int LocalSampleInterval = 180;

        private const double ScreenThickness = 0.0035d;

        private struct Arrow
        {
            public Vector3D Position;

            public Vector3 Direction;

            public float Speed;

            public float Share;
        }

        private static readonly List<Arrow> Arrows = new List<Arrow>();

        private static readonly List<Arrow> Building = new List<Arrow>();

        private const int LocalCellsPerFrame = 192;

        private static int buildCell = -1;

        private static int buildSide;

        private static Vector3D buildAnchor;
        private static Vector3D buildCentre;
        private static Vector3 buildEast;
        private static Vector3 buildNorth;
        private static Vector3 buildAxis;
        private static float buildWeather;
        private static float buildWeatherWind;

        private static int sinceSample = int.MaxValue;

        private static Vector3D localAnchor;
        private static long lastPlanetId;
        private static Mode lastMode;

        private static double arrowLength;

        private static float arrowThickness;


        public static Vector3 PlayerWind { get; private set; }

        public static Vector3 PlayerUp { get; private set; }

        private const int PlayerSampleInterval = 10;

        private static int sincePlayerSample = int.MaxValue;

        /// <summary>Cycles the wind map.</summary>
        [ChatCommand("wind")]
        internal static void Wind()
        {
            Cycle();
            ThermalChatCommands.Reply("wind map: " + Describe(Current));
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
            MyAPIGateway.Utilities.ShowNotification("wind map: " + Describe(Current), 2000, "White");
        }

        public static string Describe(Mode mode)
        {
            switch (mode)
            {
                case Mode.Local: return "local";
                case Mode.Planet: return "planet";
                default: return "off";
            }
        }

        public static void Reset()
        {
            Arrows.Clear();
            Building.Clear();
            buildCell = -1;
            sinceSample = int.MaxValue;
            sincePlayerSample = int.MaxValue;
            PlayerWind = Vector3.Zero;
            PlayerUp = Vector3.Zero;
        }

        public static void Draw()
        {
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (MyAPIGateway.Session == null || MyAPIGateway.Session.Camera == null) return;

            MatrixD camera = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D eye = camera.Translation;

            SamplePlayerWind(ref eye);

            if (Current == Mode.Off)
            {
                if (Arrows.Count > 0) Arrows.Clear();
                if (buildCell >= 0) AbandonBuild();
                return;
            }

            PlanetManager.Planet planet = PlanetManager.GetClosestPlanet(eye);
            if (planet == null || planet.Entity == null)
            {
                Arrows.Clear();
                AbandonBuild();
                return;
            }

            if (DueToSample(planet, ref eye)) Rebuild(planet, ref eye);
            if (buildCell >= 0) StepLocalBuild(planet);

            for (int i = 0; i < Arrows.Count; i++)
            {
                Arrow arrow = Arrows[i];

                if (Current == Mode.Planet)
                {
                    Vector3D toEye = eye - arrow.Position;
                    Vector3D up = arrow.Position - planet.Entity.PositionComp.GetPosition();
                    if (Vector3D.Dot(toEye, up) <= 0d) continue;
                }

                DrawArrow(ref arrow, ref eye);
            }
        }

        private static bool DueToSample(PlanetManager.Planet planet, ref Vector3D eye)
        {
            if (Current != lastMode || planet.Entity.EntityId != lastPlanetId) return true;

            if (buildCell >= 0) return false;

            if (Arrows.Count == 0) return true;

            sinceSample++;
            if (sinceSample >= (Current == Mode.Local ? LocalSampleInterval : SampleInterval))
                return true;

            return Current == Mode.Local && Anchor(ref eye) != localAnchor;
        }

        private static Vector3D Anchor(ref Vector3D eye)
        {
            return new Vector3D(
                Math.Round(eye.X / LocalSpacing) * LocalSpacing,
                Math.Round(eye.Y / LocalSpacing) * LocalSpacing,
                Math.Round(eye.Z / LocalSpacing) * LocalSpacing);
        }

        private static void Rebuild(PlanetManager.Planet planet, ref Vector3D eye)
        {
            bool switched = Current != lastMode || planet.Entity.EntityId != lastPlanetId;

            sinceSample = 0;
            localAnchor = Anchor(ref eye);
            lastMode = Current;
            lastPlanetId = planet.Entity.EntityId;

            if (switched) Arrows.Clear();

            float weather;
            float weatherWind;
            SampleWeather(ref eye, out weather, out weatherWind);

            if (Current == Mode.Local)
            {
                StartLocalBuild(planet, weather, weatherWind);
                return;
            }

            Arrows.Clear();
            BuildPlanet(planet, weather, weatherWind);
        }

        private static void SampleWeather(ref Vector3D position, out float weather, out float weatherWind)
        {
            weather = 0f;
            weatherWind = 1f;

            float influence = Settings.Instance.ClimateWeatherInfluence;
            if (influence <= 0f) return;

            weather = MyVisualScriptLogicProvider.GetWeatherIntensity(position);
            if (weather <= 0f)
            {
                weather = 0f;
                return;
            }

            string name = MyVisualScriptLogicProvider.GetWeather(position);
            if (string.IsNullOrEmpty(name)) return;

            weatherWind = WeatherResponse.Soften(
                WeatherResponse.Soften(WeatherResponse.For(name), influence), weather).WindMultiplier;
        }

        private static void StartLocalBuild(
            PlanetManager.Planet planet, float weather, float weatherWind)
        {
            buildCell = -1;

            buildCentre = planet.Entity.PositionComp.GetPosition();
            buildAnchor = localAnchor;

            Vector3D offset = buildAnchor - buildCentre;
            if (offset.LengthSquared() <= 0d) return;

            Vector3 up = (Vector3)Vector3D.Normalize(offset);
            buildAxis = planet.Entity.PositionComp.WorldMatrixRef.Up;

            Vector3 east = Vector3.Cross(buildAxis, up);
            if (east.LengthSquared() < 1e-6f)
            {
                east = Vector3.Cross(up, planet.Entity.PositionComp.WorldMatrixRef.Forward);
                if (east.LengthSquared() < 1e-6f) return;
            }

            buildEast = Vector3.Normalize(east);
            buildNorth = Vector3.Normalize(Vector3.Cross(up, buildEast));

            buildWeather = weather;
            buildWeatherWind = weatherWind;

            buildSide = (2 * (int)(LocalRadius / LocalSpacing)) + 1;
            buildCell = 0;

            Building.Clear();
        }

        private static void AbandonBuild()
        {
            buildCell = -1;
            Building.Clear();
        }

        private static void StepLocalBuild(PlanetManager.Planet planet)
        {
            if (Current != Mode.Local || planet.Entity == null)
            {
                AbandonBuild();
                return;
            }

            int half = buildSide / 2;
            double radiusSquared = LocalRadius * LocalRadius;
            int total = buildSide * buildSide;

            for (int done = 0; done < LocalCellsPerFrame && buildCell < total; done++, buildCell++)
            {
                int i = (buildCell / buildSide) - half;
                int j = (buildCell % buildSide) - half;

                double across = i * LocalSpacing;
                double along = j * LocalSpacing;

                if ((across * across) + (along * along) > radiusSquared) continue;

                Vector3D flat = buildAnchor
                    + ((Vector3D)buildEast * across) + ((Vector3D)buildNorth * along);

                Vector3D surface = planet.Entity.GetClosestSurfacePointGlobal(ref flat);

                Vector3D lift = surface - buildCentre;
                if (lift.LengthSquared() <= 0d) continue;

                Vector3D position = surface + (Vector3D.Normalize(lift) * LocalSurfaceOffset);

                Add(Building, planet, ref position, ref buildCentre, buildAxis,
                    buildWeather, buildWeatherWind);
            }

            if (buildCell < total) return;

            Arrows.Clear();
            Arrows.AddRange(Building);
            Building.Clear();
            buildCell = -1;

            arrowLength = LocalSpacing * 0.8d;
            arrowThickness = (float)(arrowLength * 0.012d);
        }

        private static void BuildPlanet(PlanetManager.Planet planet, float weather, float weatherWind)
        {
            Vector3D centre = planet.Entity.PositionComp.GetPosition();
            MatrixD matrix = planet.Entity.PositionComp.WorldMatrixRef;

            Vector3 axis = matrix.Up;
            Vector3D poleAxis = Vector3D.Normalize(matrix.Up);
            Vector3D prime = Vector3D.Normalize(matrix.Forward);
            Vector3D side = Vector3D.Normalize(Vector3D.Cross(poleAxis, prime));

            double radius = planet.Entity.MaximumRadius * (1d + PlanetAltitudeShare);

            arrowLength = planet.Entity.MaximumRadius * PlanetArrowShare;
            arrowThickness = (float)(arrowLength * 0.06d);

            for (double latitude = -90d + PlanetLatitudeStep;
                latitude <= 90d - PlanetLatitudeStep + 1e-9d;
                latitude += PlanetLatitudeStep)
            {
                double lat = latitude * Math.PI / 180d;
                double ringRadius = Math.Cos(lat);
                double height = Math.Sin(lat);

                double step = ringRadius > 1e-3d
                    ? Math.Min(120d, PlanetLongitudeStep / ringRadius)
                    : 120d;

                for (double longitude = 0d; longitude < 360d - 1e-9d; longitude += step)
                {
                    double lon = longitude * Math.PI / 180d;

                    Vector3D up = (poleAxis * height)
                        + (prime * (ringRadius * Math.Cos(lon)))
                        + (side * (ringRadius * Math.Sin(lon)));

                    Vector3D position = centre + (Vector3D.Normalize(up) * radius);

                    Add(Arrows, planet, ref position, ref centre, axis, weather, weatherWind);
                }
            }
        }

        private static void Add(
            List<Arrow> into, PlanetManager.Planet planet, ref Vector3D position,
            ref Vector3D centre, Vector3 axis, float weather, float weatherWind)
        {
            Vector3 up = (Vector3)Vector3D.Normalize(position - centre);

            Vector3 direction = WindField.Direction(up, axis);
            if (direction.LengthSquared() < 1e-6f) return;

            float ceiling = planet.Entity.GetWindSpeed(position);
            if (ceiling <= 0f) return;

            float speed = WindField.Speed(
                ceiling, weather, WindField.Variation(position), weatherWind);
            if (speed <= 0f) return;

            Arrow arrow = new Arrow();
            arrow.Position = position;
            arrow.Direction = direction;
            arrow.Speed = speed;

            arrow.Share = ThermalMath.Clamp01(speed / (ceiling * WindField.StormFraction));

            into.Add(arrow);
        }

        private static void SamplePlayerWind(ref Vector3D eye)
        {
            sincePlayerSample++;
            if (sincePlayerSample < PlayerSampleInterval) return;
            sincePlayerSample = 0;

            PlayerWind = Vector3.Zero;
            PlayerUp = Vector3.Zero;

            PlanetManager.Planet planet = PlanetManager.GetClosestPlanet(eye);
            if (planet == null || planet.Entity == null || !planet.Entity.HasAtmosphere) return;

            Vector3D centre = planet.Entity.PositionComp.GetPosition();
            Vector3D offset = eye - centre;
            if (offset.LengthSquared() <= 0d) return;

            Vector3 up = (Vector3)Vector3D.Normalize(offset);
            PlayerUp = up;

            float ceiling = planet.Entity.GetWindSpeed(eye);
            if (ceiling <= 0f) return;

            Vector3 direction = WindField.Direction(up, planet.Entity.PositionComp.WorldMatrixRef.Up);
            if (direction.LengthSquared() < 1e-6f) return;

            float weather;
            float weatherWind;
            SampleWeather(ref eye, out weather, out weatherWind);

            PlayerWind = direction
                * WindField.Speed(ceiling, weather, WindField.Variation(eye), weatherWind);
        }

        private static void DrawArrow(ref Arrow arrow, ref Vector3D eye)
        {
            Vector3D direction = (Vector3D)arrow.Direction;

            double length = arrowLength * (0.35d + (0.65d * arrow.Share));

            Vector3D tail = arrow.Position - (direction * (length * 0.5d));
            Vector3D tip = arrow.Position + (direction * (length * 0.5d));

            Vector4 colour = Colour(arrow.Share).ToVector4();

            double thickness = Math.Max(
                arrowThickness, Vector3D.Distance(eye, arrow.Position) * ScreenThickness);
            if (thickness > length * 0.15d) thickness = length * 0.15d;

            MySimpleObjectDraw.DrawLine(tail, tip, LineMaterial, ref colour, (float)thickness);

            Vector3D toEye = eye - arrow.Position;
            Vector3D sweep = Vector3D.Cross(direction, toEye);

            if (sweep.LengthSquared() < 1e-12d) return;
            sweep = Vector3D.Normalize(sweep);

            double head = length * 0.3d;
            Vector3D back = tip - (direction * head);

            MySimpleObjectDraw.DrawLine(
                tip, back + (sweep * head * 0.5d), LineMaterial, ref colour, (float)thickness);
            MySimpleObjectDraw.DrawLine(
                tip, back - (sweep * head * 0.5d), LineMaterial, ref colour, (float)thickness);
        }

        public static Color Colour(float share)
        {
            share = ThermalMath.Clamp01(share);

            if (share < 0.5f) return Lerp(new Color(60, 130, 235), new Color(90, 220, 120), share * 2f);
            return Lerp(new Color(90, 220, 120), new Color(235, 70, 55), (share - 0.5f) * 2f);
        }

        private static Color Lerp(Color from, Color to, float amount)
        {
            return new Color(
                (int)(from.R + ((to.R - from.R) * amount)),
                (int)(from.G + ((to.G - from.G) * amount)),
                (int)(from.B + ((to.B - from.B) * amount)));
        }
    }
}

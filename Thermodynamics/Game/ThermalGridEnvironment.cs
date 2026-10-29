using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private static Vector3 sunDirection = Vector3.Up;
        private static int sunDirectionFrame = -1;

        private static readonly List<MyLineSegmentOverlapResult<MyEntity>> OverlapResults =
            new List<MyLineSegmentOverlapResult<MyEntity>>();

        private static readonly Dictionary<long, PlanetThermalProperties> PlanetProperties =
            new Dictionary<long, PlanetThermalProperties>();

        private long currentPlanetId = -1;
        private float solarOcclusion;

        private bool occlusionTested;
        private int stepsSinceOcclusionTest = int.MaxValue;

        public EnvironmentSample LastSample;

        private HeatSourceState[] heatSourceBuffer;

        public EnvironmentState LastState
        {
            get { return Simulation.Solver.Environment; }
        }

        public static void ResetEnvironmentCaches()
        {
            PlanetProperties.Clear();
            OverlapResults.Clear();
            OccluderScratch.Clear();
            sunDirectionFrame = -1;

            Day.Reset();
        }

        private EnvironmentSample Sample()
        {
            EnvironmentSample sample = new EnvironmentSample();

            Vector3D position = Grid.PositionComp.WorldAABB.Center;
            MatrixD worldToLocal = MatrixD.Transpose(Grid.WorldMatrix.GetOrientation());

            sample.SunDirection = SunDirection();
            sample.DayLengthSeconds = Day.Known ? Day.Seconds : 0f;
            sample.SunDirectionLocal = Vector3.Normalize(
                Vector3D.TransformNormal(sample.SunDirection, worldToLocal));

            sample.GridVelocity = Grid.Physics != null ? Grid.Physics.LinearVelocity : Vector3.Zero;

            PlanetManager.Planet planet = PlanetManager.GetClosestPlanet(position);
            SamplePlanet(ref sample, ref position, planet);
            SampleWind(ref sample, ref position, ref worldToLocal, planet);
            bool buried = sample.IsUnderground || sample.Depth > 0f;
            // A skipped ray pass must not reuse the previous sample's "tested" flag.
            occlusionTested = false;
            sample.SolarOcclusion = buried ? 1f : SolarOcclusion(ref position, ref sample);
            sample.IsSolarOccluded = sample.SolarOcclusion >= 1f;

            RefreshShadowOccluders(ref position, ref sample);

            sample.HeatSourceCount = Simulation.Settings.EnableHeatSources
                ? ThermalHeatSources.Sample(position, ref worldToLocal, ref heatSourceBuffer) : 0;
            sample.HeatSources = heatSourceBuffer;

            LastSample = sample;
            profilePosition = position;
            profilePlanet = planet;

            if (Telemetry.Enabled && Stats != null) Stats.SampleEnvironment(this);

            return sample;
        }

        private static Vector3 SunDirection()
        {
            int frame = MyAPIGateway.Session != null ? MyAPIGateway.Session.GameplayFrameCounter : 0;
            if (frame == sunDirectionFrame) return sunDirection;

            int frames = sunDirectionFrame < 0 ? 1 : frame - sunDirectionFrame;
            if (frames < 1) frames = 1;

            sunDirection = MyVisualScriptLogicProvider.GetSunDirection();
            sunDirectionFrame = frame;

            Day.Observe(sunDirection, frames * ThermalGridScheduler.FrameSeconds);
            return sunDirection;
        }

        public static readonly DayLength Day = new DayLength();

        private void SamplePlanet(ref EnvironmentSample sample, ref Vector3D position, PlanetManager.Planet planet)
        {
            if (planet == null || planet.Entity == null)
            {
                sample.HasPlanet = false;
                sample.UpDirection = Vector3.Up;
                sample.Weather = WeatherResponse.Calm;
                currentPlanetId = -1;
                hasAmbientHistory = false;
                return;
            }

            sample.HasPlanet = true;
            sample.AirDensity = planet.Entity.GetAirDensity(position);
            sample.IsUnderground = planet.Entity.IsUnderGround(position);

            Vector3D up = position - planet.Position;
            sample.UpDirection = up.LengthSquared() > 0 ? Vector3.Normalize(up) : Vector3.Up;

            Vector3 axis = planet.Entity.PositionComp.WorldMatrixRef.Up;
            sample.LatitudeSine = Vector3.Dot(sample.UpDirection, Vector3.Normalize(axis));

            GroundTemperature.Ground ground = GroundUnder(planet.Entity, ref position);
            sample.GroundOffset = ground.Offset;
            sample.GroundSwing = ground.Swing;

            float radius = (float)up.Length();
            sample.Radius = radius;
            sample.MeanRadius = planet.Entity.AverageRadius;
            sample.Altitude = radius - sample.MeanRadius;
            sample.Depth = groundSurfaceRadius > 0f ? groundSurfaceRadius - radius : 0f;

            sample.Weather = WeatherOver(ref position);
            sample.WeatherIntensity = MyVisualScriptLogicProvider.GetWeatherIntensity(position);

            long planetId = planet.Entity.EntityId;
            bool samePlanet = planetId == currentPlanetId;

            sample.PreviousAmbient = LastState.AmbientTemperature;
            sample.HasPreviousAmbient = hasAmbientHistory && samePlanet && LastState.AirDensity > 0f;
            sample.SecondsSincePrevious = TickSeconds;
            hasAmbientHistory = true;

            if (!samePlanet)
            {
                currentPlanetId = planetId;
                Simulation.Planet = PropertiesOf(planet);

                if (Telemetry.Enabled && Stats != null) Stats.NotePlanet(planet.Entity.StorageName);
            }
        }

        private bool hasAmbientHistory;

        private WeatherResponse.Weather WeatherOver(ref Vector3D position)
        {
            float influence = Settings.Instance.ClimateWeatherInfluence;
            if (influence <= 0f)
            {
                weatherName = null;
                return WeatherResponse.Calm;
            }

            string weather = MyVisualScriptLogicProvider.GetWeather(position);
            if (weather != weatherName || influence != weatherInfluence)
            {
                weatherName = weather;
                weatherInfluence = influence;
                weatherResponse = WeatherResponse.Soften(WeatherResponse.For(weather), influence);
            }

            return weatherResponse;
        }

        private float BurialDepth()
        {
            BoundingBoxD box = Grid.PositionComp.WorldAABB;
            double reach = box.HalfExtents.Length();

            return reach < 2.5 ? 2.5f : (float)reach;
        }

        private string weatherName;
        private float weatherInfluence = 1f;
        private WeatherResponse.Weather weatherResponse = WeatherResponse.Calm;

        public string WeatherName
        {
            get { return string.IsNullOrEmpty(weatherName) ? "" : weatherName; }
        }

        private Vector3D profilePosition;
        private PlanetManager.Planet profilePlanet;

        public void ProfileEnvironment()
        {
            if (!Telemetry.Enabled || Stats == null) return;

            Vector3D position = profilePosition;
            EnvironmentSample sample = LastSample;
            PlanetManager.Planet planet = profilePlanet;

            if (stepsSinceProfile < ProfileInterval)
            {
                stepsSinceProfile++;
                return;
            }

            stepsSinceProfile = 0;


            EnvironmentRow row = new EnvironmentRow();
            EnvironmentState state = LastState;

            row.AirDensity = sample.AirDensity;
            row.AtmosphereFactor = state.AtmosphereFactor;
            row.AmbientKelvin = state.AmbientTemperature;
            row.Underground = sample.IsUnderground;
            row.Depth = sample.Depth;
            row.WeatherAmbientOffset = state.WeatherTemperatureOffset;
            row.ConvectionCoefficient = state.EffectiveConvectionCoefficient;
            row.SolarEnergy = state.SolarEnergy;
            row.SolarOcclusion = state.SolarOcclusion;
            row.WindSpeed = state.WindSpeed;
            row.GridMeanKelvin = MeanTemperature();
            row.GridPeakKelvin = HottestNode != null ? HottestNode.Temperature : 0f;

            Vector3 up = sample.UpDirection;
            float sunDot = Vector3.Dot(Vector3.Normalize(up), Vector3.Normalize(sample.SunDirection));
            row.SunElevationDegrees = (float)(Math.Asin(MathHelper.Clamp(sunDot, -1f, 1f)) * 180d / Math.PI);

            if (planet != null && planet.Entity != null)
            {
                MyPlanet entity = planet.Entity;

                row.Planet = entity.StorageName;

                Vector3D centre = entity.PositionComp.WorldMatrixRef.Translation;
                double radius = (position - centre).Length();

                row.AltitudeSealevel = radius - entity.AverageRadius;

                Vector3D surface = entity.GetClosestSurfacePointGlobal(ref position);
                row.AltitudeSurface = radius - (surface - centre).Length();

                Vector3D axis = entity.PositionComp.WorldMatrixRef.Up;
                double axisDot = MathHelper.Clamp(Vector3D.Dot(Vector3D.Normalize(position - centre), axis), -1d, 1d);
                row.LatitudeDegrees = (float)(Math.Asin(axisDot) * 180d / Math.PI);

                row.WeatherIntensity = sample.WeatherIntensity;
                row.Weather = WeatherName;
                row.WindCeiling = entity.GetWindSpeed(position);

                row.WindHeightAboveGround = sample.WindHeightAboveGround;
                row.WindBurial = sample.WindBurial;
                row.WindBandShare = sample.WindBandShare;
                row.WindProfileFactor = sample.WindProfileFactor;
                row.WindHeating = sample.WindHeating;
                row.WindSpeedUp = sample.WindSpeedUp;
                row.WindShelter = sample.WindShelter;
                row.WindChannelDegrees = sample.WindChannelDegrees;
                row.GridSpeed = sample.GridVelocity.Length();

                Vector3 east = Vector3.Cross(axis, up);
                if (east.LengthSquared() > 1e-6f)
                {
                    east = Vector3.Normalize(east);
                    Vector3 north = Vector3.Normalize(Vector3.Cross(up, east));
                    Vector3 wind = sample.WindDirection;

                    row.WindBearingDegrees = (float)(Math.Atan2(
                        Vector3.Dot(wind, east), Vector3.Dot(wind, north)) * 180d / Math.PI);
                }
                row.GameComfort = MyVisualScriptLogicProvider.GetTemperatureInPoint(position);
                row.SurfaceMaterial = MaterialUnder(entity, ref surface);
            }

            Stats.NoteEnvironmentProfile(row);
        }

        private static string MaterialUnder(MyPlanet planet, ref Vector3D surface)
        {
            Vector3D centre = planet.PositionComp.WorldMatrixRef.Translation;
            Vector3D down = Vector3D.Normalize(centre - surface);
            Vector3D probe = surface + (down * MaterialProbeDepth);

            MyVoxelMaterialDefinition material = planet.GetMaterialAt(ref probe);
            return material == null ? "" : material.Id.SubtypeName;
        }

        private const double MaterialProbeDepth = 1.5d;

        private const int ProfileInterval = 60;

        private int stepsSinceProfile = ProfileInterval;

        private float MeanTemperature()
        {
            IList<ThermalNode> nodes = Simulation.Solver.Nodes;
            if (nodes.Count == 0) return 0f;

            float total = 0f;
            for (int i = 0; i < nodes.Count; i++) total += nodes[i].Temperature;
            return total / nodes.Count;
        }

        private GroundTemperature.Ground GroundUnder(MyPlanet planet, ref Vector3D position)
        {
            RefreshSurface(planet, ref position);

            float influence = Settings.Instance.ClimateGroundInfluence;
            if (influence <= 0f) return GroundTemperature.Neutral;

            return new GroundTemperature.Ground(
                ground.Offset * influence,
                1f + ((ground.Swing - 1f) * influence));
        }

        private void RefreshSurface(MyPlanet planet, ref Vector3D position)
        {
            if (Vector3D.DistanceSquared(position, groundSampledAt) <= GroundResampleDistance * GroundResampleDistance)
                return;

            groundSampledAt = position;

            Vector3D surface = planet.GetClosestSurfacePointGlobal(ref position);
            Vector3D centre = planet.PositionComp.WorldMatrixRef.Translation;

            groundSurfaceRadius = (float)(surface - centre).Length();
            ground = GroundTemperature.For(MaterialUnder(planet, ref surface));
        }

        private const double GroundResampleDistance = 40d;

        private Vector3D groundSampledAt = Vector3D.PositiveInfinity;
        private GroundTemperature.Ground ground = GroundTemperature.Neutral;

        private float groundSurfaceRadius;

        private static PlanetThermalProperties PropertiesOf(PlanetManager.Planet planet)
        {
            PlanetThermalProperties properties;
            if (PlanetProperties.TryGetValue(planet.Entity.EntityId, out properties)) return properties;

            PlanetDefinition definition = planet.Definition();

            properties = ThermalBlockCatalog.ToPlanetProperties(definition);

            if (definition != null)
            {
                PlanetProperties[planet.Entity.EntityId] = properties;

                if (Telemetry.Enabled) Telemetry.NotePlanetProperties(
                    planet.Entity.StorageName, properties, definition.Supplied.ToString());
            }

            return properties;
        }

        private void SampleWind(
            ref EnvironmentSample sample, ref Vector3D position, ref MatrixD worldToLocal, PlanetManager.Planet planet)
        {
            if (!sample.HasPlanet || planet == null || sample.AirDensity <= 0f)
            {
                sample.RelativeWindSpeed = 0f;
                sample.RelativeWindDirectionLocal = Vector3.Zero;

                sample.WindBurial = 1f;
                return;
            }

            Settings settings = Settings.Instance;
            float ceiling = settings.EnableWind ? planet.Entity.GetWindSpeed(position) : 0f;

            float weather = sample.WeatherIntensity;
            float weatherWind = WeatherResponse.Soften(sample.Weather, weather).WindMultiplier;

            Vector3 up = sample.UpDirection;
            Vector3 axis = planet.Entity.PositionComp.WorldMatrixRef.Up;

            float height = groundSurfaceRadius > 0f ? sample.Radius - groundSurfaceRadius : 0f;

            float sunSine = Vector3.Dot(up, Vector3.Normalize(sample.SunDirection));
            windHeating = WindProfile.Heating(
                windHeating, sunSine, TickSeconds,
                Simulation.Planet.LagSecondsFor(Day.Known ? Day.Seconds : 0f));

            WindSolver.Inputs inputs = new WindSolver.Inputs();

            inputs.Ceiling = ceiling;
            inputs.Up = up;
            inputs.Axis = axis;
            inputs.WeatherIntensity = weather;
            inputs.WeatherWind = weatherWind;
            inputs.Variation = WindField.Variation(position);
            inputs.HeightAboveGround = height;

            inputs.BurialDepth = BurialDepth();
            inputs.Heating = windHeating;

            inputs.Roughness = ground.Roughness > 0f
                ? ground.Roughness
                : settings.WindRoughnessLength;

            inputs.GradientHeight = WindProfile.GradientHeightIn(
                settings.WindGradientHeight, AirAboveGround(planet, groundSurfaceRadius));
            inputs.DiurnalAmplitude = settings.WindDiurnalAmplitude;
            inputs.DiurnalCrossover = settings.WindDiurnalCrossover;
            inputs.TerrainInfluence = settings.WindTerrainInfluence;
            inputs.TerrainRadius = settings.WindTerrainRadius;
            inputs.SlopeStrength = settings.WindSlopeStrength;

            inputs.Terrain =
                settings.EnableWind
                    && settings.WindTerrainInfluence > 0f && settings.WindTerrainRadius > 0f
                    && height < settings.WindGradientHeight * TerrainFadesBy
                    && ReadTerrain(planet, ref position, up, axis, settings.WindTerrainRadius)
                ? windTerrain : null;

            WindSolver.Result wind = WindSolver.Solve(ref inputs);

            Vector3 direction = wind.Direction;
            float speed = wind.Speed;

            sample.WindBurial = wind.Burial;
            sample.WindHeightAboveGround = height;
            sample.WindHeating = windHeating;
            sample.WindBandShare = wind.BandShare;
            sample.WindProfileFactor = wind.Profile;
            sample.WindSpeedUp = wind.SpeedUp;
            sample.WindShelter = wind.Shelter;
            sample.WindChannelDegrees = wind.ChannelDegrees;

            sample.ComposeRelativeWind(
                direction.LengthSquared() > 0f ? direction : Vector3.Zero,
                direction.LengthSquared() > 0f ? speed : 0f,
                worldToLocal);

            Vector3 relative = (sample.WindDirection * sample.WindSpeed) - sample.GridVelocity;
            DrawWindVector(ref position, ref relative, sample.RelativeWindSpeed);
        }


        private float windHeating = -1f;

        private readonly float[] windTerrain = new float[WindTerrain.SampleCount];

        private Vector3D windTerrainAt;

        private bool hasWindTerrain;

        private const float TerrainFadesBy = 1f;

        private bool ReadTerrain(
            PlanetManager.Planet planet, ref Vector3D position, Vector3 up, Vector3 axis, float radius)
        {
            double moved = radius * 0.25d;

            if (hasWindTerrain && Vector3D.DistanceSquared(position, windTerrainAt) < moved * moved)
            {
                return true;
            }

            Vector3 east = Vector3.Cross(axis, up);
            if (east.LengthSquared() < 1e-6f) return false;

            east = Vector3.Normalize(east);
            Vector3 north = Vector3.Normalize(Vector3.Cross(up, east));

            MyPlanet entity = planet.Entity;
            Vector3D centre = entity.PositionComp.GetPosition();

            Vector3D under = entity.GetClosestSurfacePointGlobal(ref position);
            double siteRadius = (under - centre).Length();

            for (int ring = 0; ring < WindTerrain.Radii; ring++)
            {
                double distance = ring == 0 ? radius * 0.5d : radius;

                for (int bearing = 0; bearing < WindTerrain.Bearings; bearing++)
                {
                    Vector3 offset = WindTerrain.BearingDirection(bearing, north, east);
                    Vector3D at = position + ((Vector3D)offset * distance);

                    Vector3D radial = at - centre;
                    double length = radial.Length();
                    if (length <= 0d) continue;

                    at = centre + (radial / length * siteRadius);

                    Vector3D surface = entity.GetClosestSurfacePointGlobal(ref at);
                    windTerrain[WindTerrain.Index(ring, bearing)] =
                        (float)((surface - centre).Length() - siteRadius);
                }
            }

            windTerrainAt = position;
            hasWindTerrain = true;
            return true;
        }

        private const double WindVectorScale = 0.6d;

        private static void DrawWindVector(ref Vector3D position, ref Vector3 relative, float speed)
        {
            if (!Settings.Instance.DebugWindRaycast) return;
            if (MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated) return;
            if (speed <= 0f) return;

            Vector4 colour = (speed > 0f
                ? new Color(235, 70, 55)
                : new Color(90, 220, 120)).ToVector4();

            MySimpleObjectDraw.DrawLine(
                position,
                position + ((Vector3D)relative * WindVectorScale),
                MyStringId.GetOrCompute("Square"),
                ref colour,
                0.15f);
        }

        private float SolarOcclusion(ref Vector3D position, ref EnvironmentSample sample)
        {
            if (!Settings.Instance.EnableSolarHeat) return 1f;

            if (stepsSinceOcclusionTest < Settings.Instance.SolarOcclusionInterval)
            {
                stepsSinceOcclusionTest++;
                occlusionTested = false;
                return solarOcclusion;
            }

            stepsSinceOcclusionTest = 1;
            occlusionTested = true;

            if (Telemetry.Enabled && Stats != null) Stats.SolarTime.Begin();
            try
            {
                solarOcclusion = MeasureOcclusion(ref position, ref sample);
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.SolarOcclusion", e);
            }
            finally
            {
                if (Telemetry.Enabled && Stats != null) Stats.SolarTime.End();
            }

            return solarOcclusion;
        }

        private float MeasureOcclusion(ref Vector3D position, ref EnvironmentSample sample)
        {
            Settings settings = Settings.Instance;

            BoundingBoxD bounds = Grid.PositionComp.WorldAABB;
            SolarOcclusionSampler.Points(bounds, settings.SolarOcclusionSamples, SamplePoints);

            int occluded = 0;

            for (int i = 0; i < SamplePoints.Count; i++)
            {
                Vector3D origin = SamplePoints[i];
                if (RaycastSun(ref origin, ref sample)) occluded++;
            }

            return SamplePoints.Count == 0 ? 0f : occluded / (float)SamplePoints.Count;
        }

        private bool RaycastSun(ref Vector3D position, ref EnvironmentSample sample)
        {
            Settings settings = Settings.Instance;

            LineD line = new LineD(position, position + ((Vector3D)sample.SunDirection * 15000000d));

            OverlapResults.Clear();
            try
            {
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref line, OverlapResults);

                bool occluded = false;

                for (int i = 0; i < OverlapResults.Count && !occluded; i++)
                {
                    MyEntity entity = OverlapResults[i].Element;

                    MyPlanet planet = entity as MyPlanet;
                    if (planet != null)
                    {
                        if (settings.SolarOcclusionPlanets)
                        {
                            occluded = OcclusionMath.IsOccludedBySphere(
                                position,
                                planet.PositionComp.WorldMatrixRef.Translation,
                                planet.AverageRadius,
                                sample.SunDirection);

                            if (occluded) continue;
                        }

                        occluded = TerrainOccluded(planet, ref position, ref sample);
                        continue;
                    }

                    MyVoxelBase voxel = entity as MyVoxelBase;
                    if (voxel != null)
                    {
                        if (!settings.SolarOcclusionVoxels) continue;
                        if (voxel.RootVoxel is MyPlanet) continue;

                        LineD segment;
                        voxel.PositionComp.WorldAABB.Intersect(ref line, out segment);

                        IHitInfo hit;
                        MyAPIGateway.Physics.CastRay(segment.From, segment.To, out hit, 28);
                        occluded = hit != null;
                        continue;
                    }

                    MyCubeGrid other = entity as MyCubeGrid;
                    if (other != null && other.Physics != null && other.EntityId != Grid.EntityId)
                    {
                        if (settings.SolarGridShadows != (int)GridShadowMode.Basic) continue;

                        LineD segment;
                        other.PositionComp.WorldAABB.Intersect(ref line, out segment);
                        occluded = other.RayCastBlocks(segment.From, segment.To).HasValue;
                    }
                }

                if (settings.DebugSolarRaycast && !MyAPIGateway.Utilities.IsDedicated)
                {
                    Vector4 color = (occluded ? Color.Red : Color.White).ToVector4();
                    MySimpleObjectDraw.DrawLine(line.From, line.To, MyStringId.GetOrCompute("Square"), ref color, 0.1f);
                }

                return occluded;
            }
            finally
            {
                OverlapResults.Clear();
            }
        }

        private static readonly List<Vector3D> SamplePoints = new List<Vector3D>();

        private void RefreshShadowOccluders(ref Vector3D position, ref EnvironmentSample sample)
        {
            Settings settings = Settings.Instance;

            List<SunShadowMap.Occluder> occluders = Simulation.Solver.SunOccluders;

            bool wanted = settings.EnableSolarHeat
                && settings.SolarSelfShadowing
                && settings.SolarGridShadows == (int)GridShadowMode.Full;

            if (!wanted)
            {
                shadowOccluderVersions.Clear();
                if (occluders.Count > 0)
                {
                    occluders.Clear();
                    Simulation.Solver.MarkSunOccludersChanged();
                }
                return;
            }

            // Clearing disabled caches is independent of the expensive native query cadence.
            if (!occlusionTested) return;

            OccluderScratch.Clear();

            try
            {
                LineD line = new LineD(position, position + ((Vector3D)sample.SunDirection * ShadowOccluderRange));

                OverlapResults.Clear();
                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref line, OverlapResults);

                double shadedSize = Grid.GridSize;
                MatrixD cellsToWorld = MatrixD.CreateScale(shadedSize) * Grid.WorldMatrix;

                for (int i = 0; i < OverlapResults.Count; i++)
                {
                    MyCubeGrid other = OverlapResults[i].Element as MyCubeGrid;
                    if (other == null || other.Closed || other.EntityId == Grid.EntityId) continue;
                    if (other.GameLogic == null) continue;

                    ThermalGrid thermals = other.GameLogic.GetAs<ThermalGrid>();
                    if (thermals == null || thermals.Simulation == null) continue;

                    MatrixD toOccluder = cellsToWorld
                        * other.PositionComp.WorldMatrixNormalizedInv
                        * MatrixD.CreateScale(1d / other.GridSize);

                    OccluderScratch.Add(new SunShadowMap.Occluder
                    {
                        Model = thermals.Simulation.Grid,
                        ToOccluder = toOccluder,
                        Id = other.EntityId,
                    });
                }

                if (!OccludersMoved(occluders, OccluderScratch)) return;

                occluders.Clear();
                shadowOccluderVersions.Clear();
                for (int i = 0; i < OccluderScratch.Count; i++)
                {
                    SunShadowMap.Occluder occluder = OccluderScratch[i];
                    occluders.Add(occluder);
                    shadowOccluderVersions.Add(occluder.Model.Version);
                }

                Simulation.Solver.MarkSunOccludersChanged();
            }
            finally
            {
                OverlapResults.Clear();
                OccluderScratch.Clear();
            }
        }

        private bool OccludersMoved(
            List<SunShadowMap.Occluder> current, List<SunShadowMap.Occluder> fresh)
        {
            if (current.Count != fresh.Count || current.Count != shadowOccluderVersions.Count) return true;

            for (int i = 0; i < current.Count; i++)
            {
                if (ThermalOccluderTransform.Changed(current[i], shadowOccluderVersions[i], fresh[i])) return true;
            }

            return false;
        }

        private const double ShadowOccluderRange = 5000d;

        // Model is a live reference; its version must be captured when the cache is published.
        private readonly List<int> shadowOccluderVersions = new List<int>();

        private static readonly List<SunShadowMap.Occluder> OccluderScratch =
            new List<SunShadowMap.Occluder>();

        private bool TerrainOccluded(MyPlanet planet, ref Vector3D position, ref EnvironmentSample sample)
        {
            Settings settings = Settings.Instance;
            if (!settings.SolarOcclusionTerrain || settings.SolarTerrainRange <= 0f) return false;

            Vector3D centre = planet.PositionComp.WorldMatrixRef.Translation;
            double altitude = (position - centre).Length() - planet.AverageRadius;

            if (altitude > MaxTerrainAltitude) return false;

            terrainPlanet = planet;

            return TerrainHorizon.Occluded(
                position,
                sample.SunDirection,
                centre,
                settings.SolarTerrainRange,
                TerrainSamples,
                surfaceRadius ?? (surfaceRadius = SurfaceRadiusAt));
        }

        private static float AirAboveGround(PlanetManager.Planet planet, float groundSurfaceRadius)
        {
            if (planet == null || planet.Entity == null || !planet.Entity.HasAtmosphere) return 0f;
            if (groundSurfaceRadius <= 0f) return 0f;

            double top = planet.Entity.AverageRadius + planet.Entity.AtmosphereAltitude;
            double above = top - groundSurfaceRadius;
            return above <= 0d ? 0f : (float)above;
        }

        private const double MaxTerrainAltitude = 15000d;

        private const int TerrainSamples = 10;

        private Func<Vector3D, double> surfaceRadius;
        private MyPlanet terrainPlanet;

        private double SurfaceRadiusAt(Vector3D point)
        {
            MyPlanet planet = terrainPlanet;
            if (planet == null) return 0d;

            Vector3D surface = planet.GetClosestSurfacePointGlobal(ref point);
            return (surface - planet.PositionComp.WorldMatrixRef.Translation).Length();
        }
    }
}

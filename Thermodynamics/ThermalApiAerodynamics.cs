using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
namespace Thermodynamics
{
    public static partial class ThermalApi
    {
        private static long aeroOwner;
        private static Dictionary<string, Delegate> aeroMethods;
        // Resolved once at registration: SurfaceHeating runs per exposed node per substep, so a
        // by-name lookup there costs a string hash on the solver's hottest loop.
        private static Func<float,float,float,float,float,float,float,float> aeroHeating;
        private static Func<float, bool> aeroHeatingGate;
        private static T AeroMethod<T>(string name) where T : class
        {
            Delegate method;
            return aeroMethods != null && aeroMethods.TryGetValue(name, out method) ? method as T : null;
        }
        private static bool RegisterAerodynamics(long owner, Dictionary<string, Delegate> supplied)
        {
            if (owner == 0 || (aeroOwner != 0 && aeroOwner != owner)) return false;
            if (supplied == null) { ClearAerodynamics(); return true; }
            Delegate heat;
            if (!supplied.TryGetValue("SurfaceHeating", out heat)
                || !(heat is Func<float,float,float,float,float,float,float,float>)) return false;
            aeroMethods = new Dictionary<string, Delegate>(supplied); aeroOwner = owner;
            aeroHeatingGate = AeroMethod<Func<float, bool>>("SurfaceHeatingGate");
            aeroHeating = AeroMethod<Func<float,float,float,float,float,float,float,float>>("SurfaceHeating");
            return true;
        }
        private static void ClearAerodynamics() { aeroHeating = null; aeroHeatingGate = null; aeroMethods = null; aeroOwner = 0; }
        internal static bool SurfaceHeatingActive(float speed)
        {
            if (aeroHeating == null) return false;
            var gate = aeroHeatingGate;
            try { return gate == null ? speed > 0f : gate(speed); }
            catch (Exception e) { Telemetry.Exception("ThermalApi.SurfaceHeatingGate", e); return false; }
        }
        internal static float SurfaceHeating(float area, float density, float speed, float ambient, float temperature, float capacity, float seconds)
        {
            var heat = aeroHeating;
            if (heat == null) return 0f;
            try { return heat(area,density,speed,ambient,temperature,capacity,seconds); }
            catch (Exception e) { Telemetry.Exception("ThermalApi.SurfaceHeating",e); ClearAerodynamics(); MyAPIGateway.Utilities.SendModMessage(ChannelId, false); return 0f; }
        }
        private static MyTuple<bool, Vector3, float, float, float, Vector3> GetGridEnvironment(IMyCubeGrid grid)
        {
            ThermalGrid thermal = GridOf(grid);
            if (thermal == null || !thermal.LastSample.HasPlanet) return new MyTuple<bool,Vector3,float,float,float,Vector3>();
            var sample = thermal.LastSample;
            var at = new MyTuple<bool,Vector3,float,float,float,Vector3>();
            var planet = PlanetManager.GetClosestPlanet(grid.GetPosition());
            if (planet == null || planet.Entity == null) return at;
            at.Item1 = true;
            at.Item3 = planet.Entity.GetAirDensity(grid.GetPosition()) * 1.225f;
            at.Item4 = Settings.Instance.PlanetDayTemperature;
            var up = (Vector3)(grid.GetPosition() - planet.Entity.PositionComp.GetPosition());
            if (up.LengthSquared() > 0f) up.Normalize();
            at.Item6 = up;
            at.Item2 = Settings.Instance.EnableWind ? sample.WindDirection * sample.WindSpeed : Vector3.Zero;
            at.Item4 = thermal.LastState.AirDensity > 0f ? thermal.LastState.AmbientTemperature : at.Item4;
            at.Item5 = sample.WindHeightAboveGround;
            return at;
        }
        private static MyTuple<bool, Vector3, float, float, float, Vector3> GetEnvironmentAt(Vector3D position)
        {
            var result = new MyTuple<bool, Vector3, float, float, float, Vector3>();
            PlanetManager.Planet planet = PlanetManager.GetClosestPlanet(position);
            if (planet == null || planet.Entity == null) return result;
            result.Item1 = true;
            result.Item3 = planet.Entity.GetAirDensity(position) * 1.225f;
            Vector3D ground = planet.Entity.GetClosestSurfacePointGlobal(ref position);
            Vector3 up = (Vector3)(position - planet.Entity.PositionComp.GetPosition());
            if (up.LengthSquared() > 0f) up.Normalize();
            result.Item6 = up;
            result.Item5 = (float)Vector3D.Distance(position,ground);
            // Near a tracked grid use its sampled climate. Otherwise use a fresh planetary sample.
            ThermalGrid nearest = null; double distance = 2500d;
            foreach (ThermalGrid grid in ThermalGrid.LiveGrids)
            {
                double d = Vector3D.DistanceSquared(position,grid.Grid.PositionComp.GetPosition());
                if (d < distance && grid.LastSample.HasPlanet && grid.LastState.AirDensity > 0f) { nearest = grid; distance = d; }
            }
            if (nearest != null)
            {
                result.Item2 = Settings.Instance.EnableWind ? nearest.LastSample.WindDirection * nearest.LastSample.WindSpeed : Vector3.Zero;
                result.Item4 = nearest.LastState.AmbientTemperature;
                return result;
            }
            var settings = Settings.Instance;
            string weather = Sandbox.Game.MyVisualScriptLogicProvider.GetWeather(position);
            float intensity = Sandbox.Game.MyVisualScriptLogicProvider.GetWeatherIntensity(position);
            var input = new WindSolver.Inputs {
                Ceiling = planet.Entity.GetWindSpeed(position), Up = up, Axis = planet.Entity.PositionComp.WorldMatrixRef.Up,
                WeatherIntensity = intensity, WeatherWind = WeatherResponse.Soften(WeatherResponse.For(weather),intensity).WindMultiplier,
                Variation = WindField.Variation(position), HeightAboveGround = result.Item5,
                Roughness = settings.WindRoughnessLength, GradientHeight = settings.WindGradientHeight,
                DiurnalAmplitude = settings.WindDiurnalAmplitude, DiurnalCrossover = settings.WindDiurnalCrossover
            };
            WindSolver.Result wind = WindSolver.Solve(ref input);
            result.Item2 = settings.EnableWind ? wind.Direction * wind.Speed : Vector3.Zero;
            // Fallback is only used away from thermal grids; avoid uninitialised 2.7 K history.
            result.Item4 = settings.PlanetDayTemperature;
            return result;
        }
    }
}

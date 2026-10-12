using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class Descent
    {
        public struct Reading
        {
            public double Height;

            public float Depth;

            public float AmbientKelvin;
            public float WindSpeed;
            public float WindBurial;
            public float SolarWatts;
            public float SolarOcclusion;
            public float ConvectionCoefficient;

            public bool GameUnderground;
        }

        public const float HullReach = 5f;

        public const float Ceiling = 80f;

        public static readonly Vector3 Axis = Vector3.Up;

        public static readonly Vector3 Up = Vector3.Normalize(new Vector3(1f, 1f, 0f));


        public static double[] Heights()
        {

            List<double> heights = new List<double>();

            for (double h = 40; h > 0; h -= 10) heights.Add(h);
            for (double h = 0; h >= -12; h -= 1) heights.Add(h);

            double[] deep = { -20, -50, -100, -200, -500, -1000, -2000, -5000, -10000, -20000, -40000, -59000 };
            heights.AddRange(deep);

            return heights.ToArray();
        }


        public static List<Reading> Run(ThermalSettings settings = null, PlanetThermalProperties planet = null)
        {
            if (settings == null) settings = new HistoricalThermalSettings();
            if (planet == null) planet = new PlanetThermalProperties();


            List<Reading> readings = new List<Reading>();

            double[] heights = Heights();

            for (int i = 0; i < heights.Length; i++)
            {
                double height = heights[i];

                EnvironmentSample sample = Worlds.PlanetSurface(1f, 0.5f);

                sample.UpDirection = Up;
                sample.SunDirection = Up;
                sample.SunDirectionLocal = Up;
                sample.LatitudeSine = Vector3.Dot(Up, Axis);
                sample.IsSolarOccluded = false;

                sample.Radius = (float)(Worlds.EarthlikeRadius + height);
                sample.Altitude = (float)height;
                sample.Depth = height < 0 ? (float)-height : 0f;

                sample.IsUnderground = height < 0;

                WindSolver.Inputs inputs = new WindSolver.Inputs();
                inputs.Ceiling = Ceiling;
                inputs.Up = Up;
                inputs.Axis = Axis;
                inputs.Variation = 1f;
                inputs.WeatherWind = 1f;
                inputs.HeightAboveGround = (float)height;
                inputs.BurialDepth = HullReach;
                inputs.Heating = 1f;
                inputs.Roughness = 0.03f;
                inputs.GradientHeight = 600f;
                inputs.DiurnalAmplitude = 0.35f;
                inputs.DiurnalCrossover = 80f;

                WindSolver.Result wind = WindSolver.Solve(ref inputs);

                sample.WindSpeed = wind.Speed;
                sample.RelativeWindSpeed = wind.Speed;
                sample.RelativeWindDirectionLocal = wind.Speed > 0f ? Vector3.Forward : Vector3.Zero;

                EnvironmentState state = EnvironmentSolver.Solve(settings, planet, sample);


                Reading reading = new Reading();
                reading.Height = height;
                reading.Depth = sample.Depth;
                reading.AmbientKelvin = state.AmbientTemperature;
                reading.WindSpeed = wind.Speed;
                reading.WindBurial = wind.Burial;
                reading.SolarWatts = state.SolarEnergy;
                reading.SolarOcclusion = state.SolarOcclusion;
                reading.ConvectionCoefficient = state.EffectiveConvectionCoefficient;
                reading.GameUnderground = sample.IsUnderground;

                readings.Add(reading);
            }

            return readings;
        }


        public static string Csv(List<Reading> readings)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append("height_m,depth_m,ambient_k,ambient_c,wind_speed,wind_burial,solar_w,solar_occlusion,");
            sb.Append("convection_coeff,game_underground\n");

            for (int i = 0; i < readings.Count; i++)
            {
                Reading r = readings[i];

                sb.Append(r.Height.ToString("0.###")).Append(',');
                sb.Append(r.Depth.ToString("0.###")).Append(',');
                sb.Append(r.AmbientKelvin.ToString("0.###")).Append(',');
                sb.Append(ThermalConstants.KelvinToCelsius(r.AmbientKelvin).ToString("0.###")).Append(',');
                sb.Append(r.WindSpeed.ToString("0.###")).Append(',');
                sb.Append(r.WindBurial.ToString("0.###")).Append(',');
                sb.Append(r.SolarWatts.ToString("0.###")).Append(',');
                sb.Append(r.SolarOcclusion.ToString("0.###")).Append(',');
                sb.Append(r.ConvectionCoefficient.ToString("0.###")).Append(',');
                sb.Append(r.GameUnderground ? 1 : 0).Append('\n');
            }

            return sb.ToString();
        }


        public static Reading At(List<Reading> readings, double height)
        {
            Reading best = readings[0];
            double gap = double.MaxValue;

            for (int i = 0; i < readings.Count; i++)
            {
                double distance = Math.Abs(readings[i].Height - height);
                if (distance >= gap) continue;

                gap = distance;
                best = readings[i];
            }

            return best;
        }
    }
}

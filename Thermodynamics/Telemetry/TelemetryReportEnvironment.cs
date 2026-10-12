using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thermodynamics.Core;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static partial class TelemetryReport
    {
        private static void AppendClimate(StringBuilder sb)
        {
            Dictionary<string, ClimateSummary> planets = new Dictionary<string, ClimateSummary>();

            IList<GridTelemetry> grids = Telemetry.Grids;
            for (int g = 0; g < grids.Count; g++)
            {
                List<EnvironmentRow> rows = grids[g].Environment;

                for (int i = 0; i < rows.Count; i++)
                {
                    EnvironmentRow row = rows[i];
                    string planet = string.IsNullOrEmpty(row.Planet) ? "space" : row.Planet;

                    ClimateSummary summary;
                    if (!planets.TryGetValue(planet, out summary))
                    {

                        summary = new ClimateSummary();
                        planets[planet] = summary;
                    }

                    summary.Add(ref row);
                }
            }

            if (planets.Count == 0 && Telemetry.PlanetProperties.Count == 0) return;

            Section(sb, "Climate");

            foreach (KeyValuePair<string, ClimateSummary> pair in planets)
            {
                sb.Append("  ").Append(pair.Key).Append('\n');
                pair.Value.Write(sb);
            }

            WritePlanetProperties(sb);
        }

        private class ClimateSummary
        {

            private readonly RunningStat ambient = new RunningStat();

            private readonly RunningStat density = new RunningStat();

            private readonly RunningStat altitude = new RunningStat();

            private readonly RunningStat solar = new RunningStat();

            private readonly RunningStat wind = new RunningStat();

            private readonly RunningStat game = new RunningStat();

            private readonly RunningStat convection = new RunningStat();


            private readonly RunningStat depth = new RunningStat();


            private readonly RunningStat weatherOffset = new RunningStat();

            private readonly Dictionary<string, int> weathers = new Dictionary<string, int>();


            private readonly RunningStat day = new RunningStat();

            private readonly RunningStat night = new RunningStat();

            private readonly Dictionary<string, int> materials = new Dictionary<string, int>();


            public void Add(ref EnvironmentRow row)
            {
                ambient.Add(row.AmbientKelvin);
                density.Add(row.AirDensity);
                altitude.Add((float)row.AltitudeSurface);
                solar.Add(row.SolarEnergy);
                wind.Add(row.WindSpeed);
                game.Add(row.GameComfort);
                convection.Add(row.ConvectionCoefficient);

                if (row.SunElevationDegrees > 0f) day.Add(row.AmbientKelvin);
                else night.Add(row.AmbientKelvin);

                if (row.Depth > 0f) depth.Add(row.Depth);

                if (!string.IsNullOrEmpty(row.Weather) && row.WeatherIntensity > 0f)
                {
                    weatherOffset.Add(row.WeatherAmbientOffset);

                    int seen;
                    weathers.TryGetValue(row.Weather, out seen);
                    weathers[row.Weather] = seen + 1;
                }

                if (string.IsNullOrEmpty(row.SurfaceMaterial)) return;

                int count;
                materials.TryGetValue(row.SurfaceMaterial, out count);
                materials[row.SurfaceMaterial] = count + 1;
            }


            public void Write(StringBuilder sb)
            {
                Field(sb, "    ambient C", Celsius(ambient));
                Field(sb, "    by day C", Celsius(day));
                Field(sb, "    by night C", Celsius(night));
                Field(sb, "    air density", density.Format("n4"));
                Field(sb, "    altitude m", altitude.Format("n0"));
                Field(sb, "    solar W", solar.Format("n0"));
                Field(sb, "    wind m/s", wind.Format("n1"));
                Field(sb, "    convection W/m2K", convection.Format("n1"));
                Field(sb, "    game comfort 0..1", game.Format("n3"));

                if (depth.Count > 0)
                {
                    Field(sb, "    depth m", depth.Format("n0"));
                }

                if (weathers.Count > 0)
                {
                    Field(sb, "    weather ambient K", weatherOffset.Format("n1"));
                    Field(sb, "    weather", Counted(weathers));
                }

                if (materials.Count == 0) return;

                Field(sb, "    ground", Counted(materials));
            }


            private static string Counted(Dictionary<string, int> counts)
            {

                StringBuilder seen = new StringBuilder();
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    if (seen.Length > 0) seen.Append(", ");
                    seen.Append(pair.Key).Append(' ').Append(pair.Value);
                }

                return seen.ToString();
            }


            private static string Celsius(RunningStat stat)
            {
                if (stat.Count == 0) return "-";

                return ThermalConstants.KelvinToCelsius(stat.SafeMin).ToString("n1") + " / "
                    + ThermalConstants.KelvinToCelsius((float)stat.Mean).ToString("n1") + " / "
                    + ThermalConstants.KelvinToCelsius(stat.SafeMax).ToString("n1")
                    + " (n " + stat.Count + ")";
            }
        }
    }
}

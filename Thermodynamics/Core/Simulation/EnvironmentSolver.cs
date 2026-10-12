using System;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// Solves environmental conditions for thermal simulation.
    /// Calculates temperature, atmospheric properties, solar radiation, wind effects,
    /// and convection coefficients based on planet configuration and location.
    /// </summary>
    public static class EnvironmentSolver
    {
        /// <summary>
        /// Wind convection enhancement scale factor.
        /// Multiplies sqrt(windSpeed) to increase convection coefficient for windy conditions.
        /// Default: 0.1 (10% convection boost per sqrt(m/s) of wind speed).
        /// </summary>
        public const float WindConvectionScale = 0.1f;


        /// <summary>
        /// Calculates full environmental state for thermal simulation.
        /// Combines planet properties, location data, and weather conditions to determine
        /// ambient temperature, atmospheric density, solar exposure, and convection.
        /// </summary>
        /// <param name="settings">Global thermal settings.</param>
        /// <param name="planet">Planet thermal properties (null for vacuum).</param>
        /// <param name="sample">Location-specific environmental sample data.</param>
        /// <returns>Complete EnvironmentState with all calculated values.</returns>
        /// <remarks>
        /// The calculation follows this sequence:
        /// 1. Initialize environment state with sun/wind directions
        /// 2. Handle solar occlusion and heat sources
        /// 3. If planet is unavailable, return vacuum state
        /// 4. Calculate atmospheric effects:
        ///    - Air density factor
        ///    - Weather-modified convection
        ///    - Solar decay through atmosphere
        /// 5. Calculate ground temperature:
        ///    - Base target from ClimateModel.Target
        ///    - Lapse rate adjustment for altitude
        ///    - Atmospheric thinning
        ///    - Underground temperature if below surface
        /// 6. Apply thermal lag to approach target temperature
        /// 7. Calculate wind-enhanced convection coefficient
        /// 8. Apply friction active flag if conditions met
        /// </remarks>
        public static EnvironmentState Solve(ThermalSettings settings, PlanetThermalProperties planet, EnvironmentSample sample)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            // Initialize state with direction and speed data
            EnvironmentState state = new EnvironmentState();
            state.SunDirectionLocal = sample.SunDirectionLocal;
            state.WindDirectionLocal = sample.RelativeWindDirectionLocal;
            state.WindSpeed = Math.Max(0f, sample.RelativeWindSpeed);

            // Copy heat sources if enabled
            if (settings.EnableHeatSources && sample.HeatSources != null)
            {
                state.HeatSources = sample.HeatSources;
                state.HeatSourceCount = Math.Min(sample.HeatSourceCount, sample.HeatSources.Length);
            }

            // Handle solar occlusion
            state.SolarOcclusion = ThermalMath.Clamp01(sample.SolarOcclusion);
            if (sample.IsSolarOccluded || !settings.EnableSolarHeat) state.SolarOcclusion = 1f;

            state.IsSolarOccluded = state.SolarOcclusion >= 1f;

            // Check if planet environment should be used
            bool usePlanet = settings.EnablePlanets && sample.HasPlanet && planet != null;
            if (!usePlanet)
            {
                state.SetAmbient(settings.VacuumTemperature);
                state.AirDensity = 0f;
                state.AtmosphereFactor = 0f;
                state.ConvectionCoefficient = 0f;
                state.SolarEnergy = settings.SolarEnergy * (1f - state.SolarOcclusion);
                state.FrictionActive = false;
                return state;
            }

            // Clamp air density to valid range
            float density = ThermalMath.Clamp01(sample.AirDensity);
            state.AirDensity = density;

            // Calculate atmosphere factor (fourth-power law)
            state.AtmosphereFactor = AtmosphereFactor(density);

            // Apply weather effects
            WeatherResponse.Weather weather = WeatherResponse.Soften(sample.Weather, ThermalMath.Clamp01(sample.WeatherIntensity));

            state.WeatherIntensity = ThermalMath.Clamp01(sample.WeatherIntensity);
            state.WeatherTemperatureOffset = weather.TemperatureOffset;

            // Calculate sun elevation (dot product of up and sun direction)
            float elevation = Vector3.Dot(SafeNormalize(sample.UpDirection), SafeNormalize(sample.SunDirection));

            // Ground swing multiplier from weather
            float groundSwing = sample.GroundSwing > 0f ? sample.GroundSwing : 1f;

            // Combine ground offset and weather effects
            float offset = sample.GroundOffset + weather.TemperatureOffset;
            float swing = groundSwing * WeatherResponse.SwingMultiplier(weather);

            // Calculate base temperature from climate model
            float target = ClimateModel.Target(planet, sample.LatitudeSine, elevation, offset, swing);

            // Apply altitude lapse rate
            target = ClimateModel.Lapse(target, sample.Altitude, planet.AmbientLapseRate);
            // Apply atmospheric thinning
            target = ClimateModel.Thin(target, density, settings.VacuumTemperature);

            // Underground temperature adjustment
            if (sample.Depth > 0f)
            {
                target = ClimateModel.Underground(
                    planet, target, sample.Depth, sample.Radius, sample.MeanRadius);
            }

            // Underground blocks are always solar-occluded
            if (sample.IsUnderground || sample.Depth > 0f)
            {
                state.IsSolarOccluded = true;
                state.SolarOcclusion = 1f;
            }

            // Apply thermal lag to approach target temperature
            float ambient = sample.HasPreviousAmbient
                ? ClimateModel.Follow(
                    sample.PreviousAmbient, target, sample.SecondsSincePrevious,
                    planet.LagSecondsFor(sample.DayLengthSeconds))
                : target;

            state.SetAmbient(Math.Max(settings.VacuumTemperature, ambient));

            // Wind enhances convection via sqrt relationship
            float windBonus = 1f + (WindConvectionScale * (float)Math.Sqrt(state.WindSpeed));
            float air = planet.ConvectionCoefficient * windBonus
                * Math.Max(0f, weather.ConvectionMultiplier);

            // Underground has different convection characteristics
            float rock = InRock(sample.Depth);
            state.ConvectionCoefficient = rock <= 0f
                ? air
                : air + ((planet.UndergroundConvectionCoefficient - air) * rock);

            // Solar energy through atmosphere with weather modulation
            state.SolarEnergy = settings.SolarEnergy
                * (1f - state.SolarOcclusion)
                * (1f - (planet.SolarDecay * state.AtmosphereFactor))
                * Math.Max(0f, weather.SolarMultiplier);
            if (state.SolarEnergy < 0f) state.SolarEnergy = 0f;

            // Friction heating becomes active when wind is strong enough
            state.FrictionActive = density > 0.01f && settings.AcceptSurfaceHeating(state.WindSpeed);

            return state;
        }


        /// <summary>
        /// Calculates fraction of block that is underground vs above ground.
        /// Returns 0 for surface, 1 for fully underground.
        /// </summary>
        /// <param name="depth">Depth below surface in meters.</param>
        /// <returns>Fraction (0-1) representing underground contact.</returns>
        public static float InRock(float depth)
        {
            if (depth <= 0f) return 0f;

            // Contact increases linearly to full at UndergroundContactDepth
            float share = depth / ThermalConstants.UndergroundContactDepth;
            return share > 1f ? 1f : share;
        }


        /// <summary>
        /// Calculates atmosphere factor from air density.
        /// Uses fourth-power law: factor = 1 - (1 - density)^4
        /// </summary>
        /// <param name="airDensity">Air density as fraction of sea-level (0-1).</param>
        /// <returns>Atmosphere factor (0-1). Higher = more atmosphere.</returns>
        /// <remarks>
        /// The fourth-power relationship means thin atmospheres still have significant
        /// thermal effects, but vacuum (density=0) has zero effect:
        /// - Density 0.0 = factor 0.0 (vacuum)
        /// - Density 0.1 = factor ~0.34
        /// - Density 0.5 = factor ~0.94
        /// - Density 1.0 = factor 1.0 (full atmosphere)
        /// </remarks>
        public static float AtmosphereFactor(float airDensity)
        {
            float inverse = 1f - ThermalMath.Clamp01(airDensity);
            float squared = inverse * inverse;
            return 1f - (squared * squared);
        }


        /// <summary>
        /// Normalizes a vector safely, returning zero vector for very small magnitudes.
        /// </summary>
        /// <param name="v">Input vector.</param>
        /// <returns>Normalized vector, or zero if input magnitude is negligible.</returns>
        private static Vector3 SafeNormalize(Vector3 v)
        {
            float lengthSquared = v.LengthSquared();
            if (lengthSquared <= 1e-12f) return Vector3.Zero;
            return v / (float)Math.Sqrt(lengthSquared);
        }
    }
}

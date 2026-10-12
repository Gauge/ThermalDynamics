using System;
using System.Collections.Generic;

namespace Thermodynamics
{
    public partial class Settings
    {
        public static readonly HashSet<string> ClientOwned = new HashSet<string>
        {
            "DebugTextOnScreen", "DebugSolarRaycast", "DebugWindRaycast",
            "DebugBlockOverlay",
            "DebugWindOverlay", "DebugWindIndicator", "DebugOverlayMaxBoxes",
            "ShowEnvironmentReadout",
            "HeatGlow", "HeatWarningSound", "HeatTerminalPanel",
        };

        private sealed class SettingBinding
        {
            public readonly string Name;
            public readonly Func<Settings, float> Read;
            public readonly Action<Settings, float> Write;

            public SettingBinding(string name, Func<Settings, float> read, Action<Settings, float> write)
            {
                Name = name;
                Read = read;
                Write = write;
            }
        }

        // Keep declaration order stable: menus, chat and telemetry share this catalogue.
        private static readonly SettingBinding[] Bindings =
        {
            new SettingBinding("EnableEnvironment", settings => Flag(settings.EnableEnvironment), (settings, value) => { settings.EnableEnvironment = Flag(value); }),
            new SettingBinding("EnableConduction", settings => Flag(settings.EnableConduction), (settings, value) => { settings.EnableConduction = Flag(value); }),
            new SettingBinding("EnableRadiation", settings => Flag(settings.EnableRadiation), (settings, value) => { settings.EnableRadiation = Flag(value); }),
            new SettingBinding("EnableConvection", settings => Flag(settings.EnableConvection), (settings, value) => { settings.EnableConvection = Flag(value); }),
            new SettingBinding("EnableSolarHeat", settings => Flag(settings.EnableSolarHeat), (settings, value) => { settings.EnableSolarHeat = Flag(value); }),
            new SettingBinding("ShadowDetail", settings => settings.ShadowDetail, (settings, value) => { settings.ShadowDetail = (int)value; }),
            new SettingBinding("SolarTerrainRange", settings => settings.SolarTerrainRange, (settings, value) => { settings.SolarTerrainRange = value; }),
            new SettingBinding("SolarOcclusionSamples", settings => settings.SolarOcclusionSamples, (settings, value) => { settings.SolarOcclusionSamples = (int)value; }),
            new SettingBinding("EnableHeatSources", settings => Flag(settings.EnableHeatSources), (settings, value) => { settings.EnableHeatSources = Flag(value); }),
            new SettingBinding("EnableWasteHeat", settings => Flag(settings.EnableWasteHeat), (settings, value) => { settings.EnableWasteHeat = Flag(value); }),
            new SettingBinding("EnablePlanets", settings => Flag(settings.EnablePlanets), (settings, value) => { settings.EnablePlanets = Flag(value); }),
            new SettingBinding("EnableWind", settings => Flag(settings.EnableWind), (settings, value) => { settings.EnableWind = Flag(value); }),
            new SettingBinding("EnableDamage", settings => Flag(settings.EnableDamage), (settings, value) => { settings.EnableDamage = Flag(value); }),
            new SettingBinding("EnableCoolantLoops", settings => Flag(settings.EnableCoolantLoops), (settings, value) => { settings.EnableCoolantLoops = Flag(value); }),
            new SettingBinding("EnableRoomAir", settings => Flag(settings.EnableRoomAir), (settings, value) => { settings.EnableRoomAir = Flag(value); }),
            new SettingBinding("EnableHeatPumps", settings => Flag(settings.EnableHeatPumps), (settings, value) => { settings.EnableHeatPumps = Flag(value); }),
            new SettingBinding("WellMixedCoolant", settings => Flag(settings.WellMixedCoolant), (settings, value) => { settings.WellMixedCoolant = Flag(value); }),
            new SettingBinding("ClampOvershoot", settings => Flag(settings.ClampOvershoot), (settings, value) => { settings.ClampOvershoot = Flag(value); }),
            new SettingBinding("DamageIsPerSecond", settings => Flag(settings.DamageIsPerSecond), (settings, value) => { settings.DamageIsPerSecond = Flag(value); }),
            new SettingBinding("Frequency", settings => settings.Frequency, (settings, value) => { settings.Frequency = (int)value; }),
            new SettingBinding("HeatTimeScale", settings => settings.HeatTimeScale, (settings, value) => { settings.HeatTimeScale = value; }),
            new SettingBinding("MaxElementVisitsPerStep", settings => settings.MaxElementVisitsPerStep, (settings, value) => { settings.MaxElementVisitsPerStep = (int)value; }),
            new SettingBinding("MaxSubsteps", settings => settings.MaxSubsteps, (settings, value) => { settings.MaxSubsteps = (int)value; }),
            new SettingBinding("MaxSubstepsPerBlock", settings => settings.MaxSubstepsPerBlock, (settings, value) => { settings.MaxSubstepsPerBlock = (int)value; }),
            new SettingBinding("FloorBlocksWhenOverBudget", settings => Flag(settings.FloorBlocksWhenOverBudget), (settings, value) => { settings.FloorBlocksWhenOverBudget = Flag(value); }),
            new SettingBinding("VacuumTemperature", settings => settings.VacuumTemperature, (settings, value) => { settings.VacuumTemperature = value; }),
            new SettingBinding("SolarEnergy", settings => settings.SolarEnergy, (settings, value) => { settings.SolarEnergy = value; }),
            new SettingBinding("EnableWindwardShielding", settings => settings.EnableWindwardShielding ? 1f : 0f, (settings, value) => { settings.EnableWindwardShielding = value != 0f; }),
            new SettingBinding("RoomConvectionCoefficient", settings => settings.RoomConvectionCoefficient, (settings, value) => { settings.RoomConvectionCoefficient = value; }),
            new SettingBinding("RoomAirDensity", settings => settings.RoomAirDensity, (settings, value) => { settings.RoomAirDensity = value; }),
            new SettingBinding("SolarOcclusionInterval", settings => settings.SolarOcclusionInterval, (settings, value) => { settings.SolarOcclusionInterval = (int)value; }),
            new SettingBinding("ClimateGroundInfluence", settings => settings.ClimateGroundInfluence, (settings, value) => { settings.ClimateGroundInfluence = value; }),
            new SettingBinding("ClimateWeatherInfluence", settings => settings.ClimateWeatherInfluence, (settings, value) => { settings.ClimateWeatherInfluence = value; }),
            new SettingBinding("HeatPumpCarnotFraction", settings => settings.HeatPumpCarnotFraction, (settings, value) => { settings.HeatPumpCarnotFraction = value; }),
            new SettingBinding("HeatPumpMaxCoefficient", settings => settings.HeatPumpMaxCoefficient, (settings, value) => { settings.HeatPumpMaxCoefficient = value; }),
            new SettingBinding("EnableSuitDamage", settings => settings.EnableSuitDamage ? 1f : 0f, (settings, value) => { settings.EnableSuitDamage = value != 0f; }),
            new SettingBinding("SuitConductance", settings => settings.SuitConductance, (settings, value) => { settings.SuitConductance = value; }),
            new SettingBinding("SuitHeatCapacity", settings => settings.SuitHeatCapacity, (settings, value) => { settings.SuitHeatCapacity = value; }),
            new SettingBinding("SuitCoolingWatts", settings => settings.SuitCoolingWatts, (settings, value) => { settings.SuitCoolingWatts = value; }),
            new SettingBinding("SuitCriticalTemperature", settings => settings.SuitCriticalTemperature, (settings, value) => { settings.SuitCriticalTemperature = value; }),
            new SettingBinding("SuitDamagePerKelvin", settings => settings.SuitDamagePerKelvin, (settings, value) => { settings.SuitDamagePerKelvin = value; }),
            new SettingBinding("WindRoughnessLength", settings => settings.WindRoughnessLength, (settings, value) => { settings.WindRoughnessLength = value; }),
            new SettingBinding("WindGradientHeight", settings => settings.WindGradientHeight, (settings, value) => { settings.WindGradientHeight = value; }),
            new SettingBinding("WindDiurnalAmplitude", settings => settings.WindDiurnalAmplitude, (settings, value) => { settings.WindDiurnalAmplitude = value; }),
            new SettingBinding("WindDiurnalCrossover", settings => settings.WindDiurnalCrossover, (settings, value) => { settings.WindDiurnalCrossover = value; }),
            new SettingBinding("WindTerrainInfluence", settings => settings.WindTerrainInfluence, (settings, value) => { settings.WindTerrainInfluence = value; }),
            new SettingBinding("WindTerrainRadius", settings => settings.WindTerrainRadius, (settings, value) => { settings.WindTerrainRadius = value; }),
            new SettingBinding("WindSlopeStrength", settings => settings.WindSlopeStrength, (settings, value) => { settings.WindSlopeStrength = value; }),
            new SettingBinding("DebugTextOnScreen", settings => Flag(settings.DebugTextOnScreen), (settings, value) => { settings.DebugTextOnScreen = Flag(value); }),
            new SettingBinding("DebugSolarRaycast", settings => Flag(settings.DebugSolarRaycast), (settings, value) => { settings.DebugSolarRaycast = Flag(value); }),
            new SettingBinding("DebugWindRaycast", settings => Flag(settings.DebugWindRaycast), (settings, value) => { settings.DebugWindRaycast = Flag(value); }),
            new SettingBinding("DebugBlockOverlay", settings => settings.DebugBlockOverlay, (settings, value) => { settings.DebugBlockOverlay = (int)value; ThermalDebugView.Set((ThermalDebugView.Mode)settings.DebugBlockOverlay); }),
            new SettingBinding("DebugWindOverlay", settings => settings.DebugWindOverlay, (settings, value) => { settings.DebugWindOverlay = (int)value; WindOverlay.Set((WindOverlay.Mode)settings.DebugWindOverlay); }),
            new SettingBinding("DebugWindIndicator", settings => Flag(settings.DebugWindIndicator), (settings, value) => { settings.DebugWindIndicator = Flag(value); }),
            new SettingBinding("ShowEnvironmentReadout", settings => Flag(settings.ShowEnvironmentReadout), (settings, value) => { settings.ShowEnvironmentReadout = Flag(value); }),
            new SettingBinding("DebugOverlayMaxBoxes", settings => settings.DebugOverlayMaxBoxes, (settings, value) => { settings.DebugOverlayMaxBoxes = (int)value; }),
            new SettingBinding("HeatGlow", settings => Flag(settings.HeatGlow), (settings, value) => { settings.HeatGlow = Flag(value); }),
            new SettingBinding("HeatWarningSound", settings => Flag(settings.HeatWarningSound), (settings, value) => { settings.HeatWarningSound = Flag(value); }),
            new SettingBinding("HeatTerminalPanel", settings => Flag(settings.HeatTerminalPanel), (settings, value) => { settings.HeatTerminalPanel = Flag(value); }),
            new SettingBinding("RoomOverlayMinKelvin", settings => settings.RoomOverlayMinKelvin, (settings, value) => { settings.RoomOverlayMinKelvin = value; }),
            new SettingBinding("RoomOverlayMaxKelvin", settings => settings.RoomOverlayMaxKelvin, (settings, value) => { settings.RoomOverlayMaxKelvin = value; }),
            new SettingBinding("EnableTemperatureSync", settings => Flag(settings.EnableTemperatureSync), (settings, value) => { settings.EnableTemperatureSync = Flag(value); }),
            new SettingBinding("TemperatureSyncInterval", settings => settings.TemperatureSyncInterval, (settings, value) => { settings.TemperatureSyncInterval = value; }),
            new SettingBinding("ParallelGrids", settings => Flag(settings.ParallelGrids), (settings, value) => { settings.ParallelGrids = Flag(value); }),
            new SettingBinding("EnableTelemetry", settings => Flag(settings.EnableTelemetry), (settings, value) => { settings.EnableTelemetry = Flag(value); Telemetry.SetEnabled(settings.EnableTelemetry); }),
            new SettingBinding("TelemetrySampleStride", settings => settings.TelemetrySampleStride, (settings, value) => { settings.TelemetrySampleStride = (int)value; }),
            new SettingBinding("TelemetryPlanetProbes", settings => settings.TelemetryPlanetProbes, (settings, value) => { settings.TelemetryPlanetProbes = (int)value; }),
            new SettingBinding("LoopFlowRate", settings => settings.LoopFlowRate, (settings, value) => { settings.LoopFlowRate = value; }),
            new SettingBinding("LoopCoolantKilogramsPerCubicMetre", settings => settings.LoopCoolantKilogramsPerCubicMetre, (settings, value) => { settings.LoopCoolantKilogramsPerCubicMetre = value; }),
            new SettingBinding("LoopSpecificHeat", settings => settings.LoopSpecificHeat, (settings, value) => { settings.LoopSpecificHeat = value; }),
            new SettingBinding("LoopHeatTransferCoefficient", settings => settings.LoopHeatTransferCoefficient, (settings, value) => { settings.LoopHeatTransferCoefficient = value; }),
            new SettingBinding("LoopContactMultiplier", settings => settings.LoopContactMultiplier, (settings, value) => { settings.LoopContactMultiplier = value; }),
            new SettingBinding("LoopStagnantTransferFraction", settings => settings.LoopStagnantTransferFraction, (settings, value) => { settings.LoopStagnantTransferFraction = value; }),
            new SettingBinding("LoopRefillEquivalentKelvin", settings => settings.LoopRefillEquivalentKelvin, (settings, value) => { settings.LoopRefillEquivalentKelvin = value; }),
            new SettingBinding("LoopRefillKilogramsPerSecond", settings => settings.LoopRefillKilogramsPerSecond, (settings, value) => { settings.LoopRefillKilogramsPerSecond = value; }),
            new SettingBinding("PlanetDayTemperature", settings => settings.PlanetDayTemperature, (settings, value) => { settings.PlanetDayTemperature = value; }),
            new SettingBinding("PlanetNightTemperature", settings => settings.PlanetNightTemperature, (settings, value) => { settings.PlanetNightTemperature = value; }),
            new SettingBinding("PlanetPoleTemperatureDrop", settings => settings.PlanetPoleTemperatureDrop, (settings, value) => { settings.PlanetPoleTemperatureDrop = value; }),
            new SettingBinding("PlanetAmbientLapseRate", settings => settings.PlanetAmbientLapseRate, (settings, value) => { settings.PlanetAmbientLapseRate = value; }),
            new SettingBinding("PlanetAmbientLagSeconds", settings => settings.PlanetAmbientLagSeconds, (settings, value) => { settings.PlanetAmbientLagSeconds = value; }),
            new SettingBinding("PlanetConvectionCoefficient", settings => settings.PlanetConvectionCoefficient, (settings, value) => { settings.PlanetConvectionCoefficient = value; }),
            new SettingBinding("PlanetUndergroundConvectionCoefficient", settings => settings.PlanetUndergroundConvectionCoefficient, (settings, value) => { settings.PlanetUndergroundConvectionCoefficient = value; }),
            new SettingBinding("PlanetSolarDecay", settings => settings.PlanetSolarDecay, (settings, value) => { settings.PlanetSolarDecay = value; }),
            new SettingBinding("PlanetUndergroundTemperature", settings => settings.PlanetUndergroundTemperature, (settings, value) => { settings.PlanetUndergroundTemperature = value; }),
            new SettingBinding("PlanetUndergroundDampingDepth", settings => settings.PlanetUndergroundDampingDepth, (settings, value) => { settings.PlanetUndergroundDampingDepth = value; }),
            new SettingBinding("PlanetCoreTemperature", settings => settings.PlanetCoreTemperature, (settings, value) => { settings.PlanetCoreTemperature = value; }),
            new SettingBinding("PlanetSealevelDeadzone", settings => settings.PlanetSealevelDeadzone, (settings, value) => { settings.PlanetSealevelDeadzone = value; }),
        };

        private static readonly Dictionary<string, SettingBinding> BindingsByName = IndexBindings();

        private static Dictionary<string, SettingBinding> IndexBindings()
        {
            var result = new Dictionary<string, SettingBinding>(StringComparer.Ordinal);
            foreach (SettingBinding binding in Bindings) result.Add(binding.Name, binding);
            return result;
        }

        public static List<string> Names()
        {
            var names = new List<string>(Bindings.Length);
            foreach (SettingBinding binding in Bindings) names.Add(binding.Name);
            return names;
        }

        public float GetValue(string name)
        {
            SettingBinding binding;
            return name != null && BindingsByName.TryGetValue(name, out binding)
                ? binding.Read(this) : float.NaN;
        }

        public bool SetValue(string name, float value)
        {
            SettingBinding binding;
            if (name == null || !BindingsByName.TryGetValue(name, out binding)) return false;
            binding.Write(this, value);
            return true;
        }

        public static bool IsFlag(string name)
        {
            return name != null && name != "DebugBlockOverlay" && name != "DebugWindOverlay"
                && name != "DebugOverlayMaxBoxes"
                && (name.StartsWith("Enable") || name.StartsWith("Debug")
                || name == "ClampOvershoot"
                || name == "DamageIsPerSecond"

                || name == "HeatGlow" || name == "HeatWarningSound" || name == "HeatTerminalPanel"
                || name == "ShowEnvironmentReadout"
                || name == "FloorBlocksWhenOverBudget" || name == "ParallelGrids"
                || name == "WellMixedCoolant");
        }

        private static float Flag(bool value)
        {
            return value ? 1f : 0f;
        }

        private static bool Flag(float value)
        {
            return value != 0f;
        }


    }
}

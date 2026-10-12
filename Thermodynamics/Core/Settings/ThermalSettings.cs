using System;
using System.Collections.Generic;

namespace Thermodynamics.Core
{
    /// <summary>
    /// Global settings for the thermal simulation.
    /// Controls which physics are enabled, simulation frequency, and various
    /// physical parameters like temperatures, coefficients, and thresholds.
    /// </summary>
    public class ThermalSettings
    {
        /// <summary>
        /// External surface heating law: exposed area, density fraction, speed, ambient and surface K, heat capacity, integration seconds; returns watts.
        /// Null means no aerodynamic heating provider is installed.
        /// </summary>
        public Func<float, float, float, float, float, float, float, float> SurfaceHeating { get; set; }
        public Func<float,bool> SurfaceHeatingGate { get; set; }
        public bool AcceptSurfaceHeating(float speed) { return SurfaceHeating != null && (SurfaceHeatingGate == null ? speed > 0f : SurfaceHeatingGate(speed)); }

        public const int CurrentVersion = 4;

        /// <summary>
        /// Version of these settings (used for save file compatibility).
        /// Should equal CurrentVersion for new simulations.
        /// </summary>
        public int Version = CurrentVersion;

        /// <summary>
        /// Enables/disables environmental simulation (planet atmosphere, solar, etc.).
        /// When false, blocks only exchange heat with vacuum at VacuumTemperature.
        /// </summary>
        public bool EnableEnvironment = true;

        /// <summary>
        /// Enables/disables conductive heat transfer between adjacent blocks.
        /// When false, blocks don't exchange heat through direct contact.
        /// </summary>
        public bool EnableConduction = true;

        /// <summary>
        /// Enables/disables radiative heat transfer between blocks.
        /// When false, blocks don't exchange heat via infrared radiation.
        /// </summary>
        public bool EnableRadiation = true;

        /// <summary>
        /// Enables/disables convective heat transfer to air/ambient.
        /// When false, blocks don't exchange heat with surrounding air.
        /// </summary>
        public bool EnableConvection = true;

        /// <summary>
        /// Enables/disables solar heating from the sun direction.
        /// When false, solar energy is ignored even if sun is visible.
        /// </summary>
        public bool EnableSolarHeat = true;

        /// <summary>
        /// Enables self-shadowing for solar radiation.
        /// When true, blocks can cast shadows on other blocks, blocking solar energy.
        /// </summary>
        public bool SolarSelfShadowing = true;

        /// <summary>
        /// Enables/disables heat source simulation ( reactors, engines, etc.).
        /// When false, heat sources don't generate heat.
        /// </summary>
        public bool EnableHeatSources = true;

        /// <summary>
        /// Enables/disables waste heat from powered devices.
        /// When false, devices don't generate waste heat during operation.
        /// </summary>
        public bool EnableWasteHeat = true;

        /// <summary>
        /// Enables/disables planet-based environmental simulation.
        /// When false, uses vacuum conditions regardless of planet presence.
        /// </summary>
        public bool EnablePlanets = true;

        /// <summary>
        /// Enables/disables thermal damage to blocks.
        /// When true, blocks that overheat sustain damage over time.
        /// </summary>
        public bool EnableDamage = true;

        /// <summary>
        /// Enables/disables thermal damage to the player's suit.
        /// When true, suit occupants can be injured by extreme temperatures.
        /// </summary>
        public bool EnableSuitDamage = true;

        /// <summary>
        /// Enables/disables coolant loop simulation.
        /// When false, coolant pipes don't transfer heat between blocks.
        /// </summary>
        public bool EnableCoolantLoops = true;

        /// <summary>
        /// If true, coolant in each loop is treated as perfectly mixed (single temperature).
        /// When false, coolant flows and has temperature gradients along the loop.
        /// </summary>
        public bool WellMixedCoolant = false;

        /// <summary>
        /// Enables/disables room air simulation (air pockets inside structures).
        /// When true, enclosed spaces have their own air temperature.
        /// </summary>
        public bool EnableRoomAir = true;

        /// <summary>
        /// Enables/disables heat pump simulation.
        /// When false, heat pumps don't transfer heat between loops or environments.
        /// </summary>
        public bool EnableHeatPumps = true;


        /// <summary>
        /// Simulation frequency in updates per second (Hz).
        /// Higher values = more accurate but more expensive.
        /// Default: 4 Hz (one update every 250ms).
        /// </summary>
        public int Frequency = 4;

        /// <summary>
        /// Multiplier for simulation speed.
        /// 1.0 = real-time, 0.5 = half-speed, 2.0 = double-speed.
        /// </summary>
        public float SimulationSpeed = 1f;

        /// <summary>
        /// Time scale factor for heat transfer calculations in seconds.
        /// Larger values = slower heat transfer simulation (more stable).
        /// Smaller values = faster heat transfer (more detailed).
        /// Default: 90 seconds.
        /// </summary>
        public float HeatTimeScale = 90f;


        /// <summary>
        /// Temperature of deep space (vacuum) in Kelvin.
        /// Cosmic microwave background temperature is approximately 2.7K.
        /// Used when environment is disabled or blocks are in space.
        /// </summary>
        public float VacuumTemperature = 2.7f;

        /// <summary>
        /// Solar energy flux at 1 AU in watts per square meter.
        /// Earth receives approximately 1361 W/m² (Solar Constant).
        /// Space Engineers uses 1000 W/m² as a rounded value.
        /// </summary>
        public float SolarEnergy = 1000f;

        /// <summary>
        /// Enables windward shielding (leeward side of blocks receives less wind).
        /// When true, blocks on the leeward side of other blocks have reduced wind effects.
        /// </summary>
        public bool EnableWindwardShielding = false;



        /// <summary>
        /// Convection coefficient for room air heat transfer in W/m²·K.
        /// Higher values = more efficient heat transfer between air and blocks.
        /// Default: 8 W/m²·K (typical for natural convection in air).
        /// </summary>
        public float RoomConvectionCoefficient = 8f;

        /// <summary>
        /// Density of room air in kg/m³.
        /// Used to calculate air thermal mass.
        /// Default: 1.225 kg/m³ (sea-level air density).
        /// </summary>
        public float RoomAirDensity = 1.225f;


        /// <summary>
        /// Fraction of Carnot efficiency that heat pumps achieve.
        /// Real heat pumps are less efficient than ideal Carnot cycles.
        /// Default: 0.4 (40% of Carnot efficiency).
        /// </summary>
        public float HeatPumpCarnotFraction = 0.4f;

        /// <summary>
        /// Maximum coefficient of performance (COP) for heat pumps.
        /// Real heat pumps have practical limits on efficiency.
        /// Default: 8 (typical for good heat pumps).
        /// </summary>
        public float HeatPumpMaxCoefficient = 8f;


        /// <summary>
        /// Conductance of the player's suit in W/K.
        /// Higher values = faster heat transfer through suit.
        /// Default: 2.5 W/K.
        /// </summary>
        public float SuitConductance = 2.5f;

        /// <summary>
        /// Thermal heat capacity of the suit in J/K.
        /// Higher values = suit resists temperature changes more.
        /// Default: 240,000 J/K (about 1000x air capacity).
        /// </summary>
        public float SuitHeatCapacity = 240000f;

        /// <summary>
        /// Minimum valid suit heat capacity in J/K.
        /// Prevents division by zero in calculations.
        /// </summary>
        public const float MinimumSuitHeatCapacity = 1f;

        /// <summary>
        /// Maximum cooling power of the suit in watts.
        /// Maximum heat the suit can remove per second.
        /// Default: 500 W (similar to a small fan).
        /// </summary>
        public float SuitCoolingWatts = 500f;

        /// <summary>
        /// Critical temperature for suit damage in Kelvin.
        /// When suit interior exceeds this temperature, damage begins.
        /// Default: 315.15 K = 42°C (human body temperature + 5°C).
        /// </summary>
        public float SuitCriticalTemperature = 315.15f;

        /// <summary>
        /// Damage rate per Kelvin above SuitCriticalTemperature.
        /// Damage = (temp - critical) * this * time.
        /// Default: 1.0 damage per Kelvin per second.
        /// </summary>
        public float SuitDamagePerKelvin = 1f;


        /// <summary>
        /// If true, clamps conduction temperature updates to prevent overshoot.
        /// Prevents numerical instability in thermal calculations.
        /// </summary>
        public bool ClampConductionOvershoot = true;

        /// <summary>
        /// If true, clamps environment temperature updates to prevent overshoot.
        /// </summary>
        public bool ClampEnvironmentOvershoot = true;

        /// <summary>
        /// Maximum number of substeps per simulation step.
        /// Used for stability when large temperature changes occur.
        /// Higher values = more stable but slower.
        /// Default: 64 substeps.
        /// </summary>
        public int MaxSubsteps = 64;

        /// <summary>
        /// Maximum cell face visits per simulation step.
        /// Limits work to prevent performance issues with large grids.
        /// Default: 4,000,000 face visits.
        /// </summary>
        public int MaxElementVisitsPerStep = 4000000;

        /// <summary>
        /// Cost weight for each link in node chain operations.
        /// Used in workload estimation for solver scheduling.
        /// Default: 4 work units per link.
        /// </summary>
        public const int NodeCostInLinks = 4;

        /// <summary>
        /// Maximum substeps per block (0 = unlimited).
        /// When > 0, limits substeps based on block count.
        /// Default: 0 (no per-block limit).
        /// </summary>
        public int MaxSubstepsPerBlock = 0;

        /// <summary>
        /// If true, floors (deletes) blocks that exceed the work budget.
        /// Used as a last-resort performance protection.
        /// Default: false (don't delete blocks).
        /// </summary>
        public bool FloorBlocksWhenOverBudget = false;

        /// <summary>
        /// If true, damage is calculated per simulation second.
        /// When false, damage is per simulation step.
        /// Default: true (consistent damage rate regardless of simulation speed).
        /// </summary>
        public bool DamageIsPerSecond = true;


        /// <summary>
        /// Gets the duration of each simulation step in seconds.
        /// Calculated as 1 / Frequency.
        /// </summary>
        public float StepSeconds { get; private set; }

        /// <summary>
        /// Gets the number of simulation steps per real second.
        /// Calculated as Frequency * SimulationSpeed.
        /// </summary>
        public float StepsPerSecond { get; private set; }

        /// <summary>
        /// Gets the number of times Derive() has been called.
        /// Used to detect when settings have changed.
        /// </summary>
        public int Revision { get; private set; }


        /// <summary>
        /// Creates a new ThermalSettings with default values.
        /// All fields are initialized to their default values.
        /// </summary>

        public ThermalSettings()
        {
            Derive();
        }


        /// <summary>
        /// Calculates derived values and validates settings.
        /// Updates StepSeconds, StepsPerSecond, and Revision.
        /// Clamps values to valid ranges.
        /// </summary>
        /// <returns>The same instance with derived values calculated.</returns>
        public ThermalSettings Derive()
        {
            if (Frequency < 1) Frequency = 1;
            if (SimulationSpeed <= 0f) SimulationSpeed = 1f;
            if (HeatTimeScale <= 0f) HeatTimeScale = 1f;
            if (VacuumTemperature < 0f) VacuumTemperature = 0f;
            if (SolarEnergy < 0f) SolarEnergy = 0f;
            if (RoomConvectionCoefficient < 0f) RoomConvectionCoefficient = 0f;
            if (RoomAirDensity < 0f) RoomAirDensity = 0f;
            if (HeatPumpCarnotFraction < 0f) HeatPumpCarnotFraction = 0f;
            if (HeatPumpCarnotFraction > 1f) HeatPumpCarnotFraction = 1f;
            if (HeatPumpMaxCoefficient < 0f) HeatPumpMaxCoefficient = 0f;

            if (SuitConductance < 0f) SuitConductance = 0f;
            if (SuitHeatCapacity <= 0f) SuitHeatCapacity = MinimumSuitHeatCapacity;
            if (SuitCoolingWatts < 0f) SuitCoolingWatts = 0f;
            if (SuitCriticalTemperature < 0f) SuitCriticalTemperature = 0f;
            if (SuitDamagePerKelvin < 0f) SuitDamagePerKelvin = 0f;
            if (MaxElementVisitsPerStep < 0) MaxElementVisitsPerStep = 0;
            if (MaxSubsteps < 1) MaxSubsteps = 1;
            if (MaxSubstepsPerBlock < 0) MaxSubstepsPerBlock = 0;

            StepSeconds = 1f / Frequency;
            StepsPerSecond = Frequency * SimulationSpeed;
            Revision++;
            return this;
        }


        /// <summary>
        /// Validates settings for physical consistency.
        /// Checks for impossible or dangerous parameter combinations.
        /// </summary>
        /// <returns>List of validation problems. Empty if all settings are valid.</returns>
        public List<string> Validate()
        {
            List<string> problems = new List<string>();

            if (Frequency < 1) problems.Add("Frequency must be at least 1.");
            if (Frequency > 60) problems.Add("Frequency above 60 costs more than one step per render frame.");
            if (SimulationSpeed <= 0f) problems.Add("SimulationSpeed must be positive.");
            if (HeatTimeScale <= 0f) problems.Add("HeatTimeScale must be positive.");

            float perSubstep = Frequency < 1 ? HeatTimeScale : HeatTimeScale / Frequency;
            if (perSubstep > 4000f)
            {
                problems.Add("HeatTimeScale / Frequency is " + perSubstep.ToString("n0")
                    + "; above about 4000 the overshoot clamps carry the whole step and blocks "
                    + "can be driven to the ambient floor. Raise Frequency or lower HeatTimeScale.");
            }
            if (VacuumTemperature < 0f) problems.Add("VacuumTemperature cannot be negative.");
            if (SuitCriticalTemperature > 0f && SuitCriticalTemperature <= SuitThermal.ComfortKelvin)
            {
                problems.Add("SuitCriticalTemperature is at or below the temperature the suit holds "
                    + "its occupant at, so a player is being damaged even when the suit is working.");
            }
            return problems;
        }


        /// <summary>
        /// Creates a shallow copy of these settings.
        /// Used for saving current state or creating variations.
        /// </summary>
        /// <returns>A copy of this ThermalSettings instance.</returns>
        public ThermalSettings Clone()
        {
            return (ThermalSettings)MemberwiseClone();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace Thermodynamics
{
    public enum GridShadowMode
    {
        None = 0,

        Basic = 1,

        Full = 2,
    }

    public enum ShadowLevel
    {
        None = 0,

        Planets = 1,

        World = 2,

        Everything = 3,
    }

    [ProtoContract]
    public class Settings
    {
        public const string Filename = "ThermodynamicsConfig.cfg";
        public const string Name = "Thermodynamics";

        public const int CurrentVersion = 8;

        public static Settings Instance;

        public static readonly MyStringHash DefaultSubtypeId = MyStringHash.GetOrCompute("DefaultThermodynamics");
        public static readonly MyStringHash DefaultLoopSubtypeId = MyStringHash.GetOrCompute("DefaultThermodynamicsLoop");

        [ProtoMember(1)] public int Version;


        [ProtoMember(10)] public bool EnableEnvironment = true;
        [ProtoMember(11)] public bool EnableConduction = true;
        [ProtoMember(12)] public bool EnableRadiation = true;
        [ProtoMember(13)] public bool EnableConvection = true;
        [ProtoMember(14)] public bool EnableSolarHeat = true;

        [ProtoMember(141)] public int ShadowDetail = (int)ShadowLevel.Everything;

        [XmlIgnore] public bool SolarOcclusionPlanets
        {
            get { return ShadowDetail >= (int)ShadowLevel.Planets; }
        }

        [XmlIgnore] public bool SolarOcclusionTerrain
        {
            get { return ShadowDetail >= (int)ShadowLevel.World; }
        }

        [XmlIgnore] public bool SolarOcclusionVoxels
        {
            get { return ShadowDetail >= (int)ShadowLevel.World; }
        }

        [XmlIgnore] public bool SolarSelfShadowing
        {
            get { return ShadowDetail >= (int)ShadowLevel.World; }
        }

        [XmlIgnore] public int SolarGridShadows
        {
            get
            {
                if (ShadowDetail >= (int)ShadowLevel.Everything) return (int)GridShadowMode.Full;
                if (ShadowDetail >= (int)ShadowLevel.World) return (int)GridShadowMode.Basic;

                return (int)GridShadowMode.None;
            }
        }

        [ProtoMember(75)] public float SolarTerrainRange = 4000f;

        [ProtoMember(73)] public int SolarOcclusionSamples = 1;
        [ProtoMember(15)] public bool EnableHeatSources = true;
        [ProtoMember(16)] public bool EnableWasteHeat = true;
        [ProtoMember(17)] public bool EnablePlanets = true;
        [ProtoMember(18)] public bool EnableFriction = true;

        [ProtoMember(128)] public bool EnableWind = true;
        [ProtoMember(19)] public bool EnableDamage = true;
        [ProtoMember(20)] public bool EnableCoolantLoops = true;

        [ProtoMember(126)] public bool WellMixedCoolant = false;
        [ProtoMember(21)] public bool EnableRoomAir = true;
        [ProtoMember(22)] public bool EnableHeatPumps = true;

        [ProtoMember(23)] public bool EnableSuitDamage = true;


        [ProtoMember(142)] public bool ClampOvershoot = true;
        [ProtoMember(31)] public bool DamageIsPerSecond = true;

        [ProtoMember(32)] public int Frequency = 4;
        [ProtoMember(34)] public float HeatTimeScale = 90f;

        [ProtoMember(35)] public int MaxElementVisitsPerStep = 4000000;

        [ProtoMember(36)] public int MaxSubsteps = 64;

        [ProtoMember(84)] public int MaxSubstepsPerBlock = 0;

        [ProtoMember(130)] public bool FloorBlocksWhenOverBudget = false;

        [ProtoMember(127)] public bool ParallelGrids = false;


        [ProtoMember(40)] public float VacuumTemperature = 2.7f;
        [ProtoMember(41)] public float SolarEnergy = 1000f;
        [ProtoMember(42)] public float FrictionAtSpeedsAbove = 0f;
        [ProtoMember(43)] public float FrictionScale = 0.001f;


        [ProtoMember(137)] public bool EnableWindwardShielding = false;
        [ProtoMember(44)] public float RoomConvectionCoefficient = 8f;
        [ProtoMember(45)] public float RoomAirDensity = 1.225f;

        [ProtoMember(46)] public int SolarOcclusionInterval = 12;

        [ProtoMember(82)] public float ClimateGroundInfluence = 1f;

        [ProtoMember(83)] public float ClimateWeatherInfluence = 1f;


        [ProtoMember(133)] public float LoopCoolantKilogramsPerCubicMetre = 33f;

        [ProtoMember(131)] public float LoopRefillEquivalentKelvin = 100f;

        [ProtoMember(132)] public float LoopRefillKilogramsPerSecond = 5f;

        [ProtoMember(125)] public float LoopHeatTransferCoefficient = 1000f;

        [ProtoMember(86)] public float LegacyLoopConductivity = -1f;

        [ProtoMember(87)] public float LoopSpecificHeat = 3400f;

        [ProtoMember(143)] public float LoopContactMultiplier = 1f;

        [ProtoMember(144)] public float LoopFlowRate = 10f;

        [ProtoMember(92)] public float LoopStagnantTransferFraction = 0.16f;


        [ProtoMember(93)] public float PlanetDayTemperature = 294.261f;
        [ProtoMember(94)] public float PlanetNightTemperature = 283.15f;
        [ProtoMember(95)] public float PlanetPoleTemperatureDrop = 40f;
        [ProtoMember(96)] public float PlanetAmbientLapseRate = 4f;
        [ProtoMember(97)] public float PlanetAmbientLagSeconds = 45f;
        [ProtoMember(98)] public float PlanetConvectionCoefficient = 50f;

        [ProtoMember(120)] public float PlanetUndergroundConvectionCoefficient = 2f;
        [ProtoMember(99)] public float PlanetSolarDecay = 0.5f;
        [ProtoMember(100)] public float PlanetUndergroundTemperature = 280f;
        [ProtoMember(101)] public float PlanetUndergroundDampingDepth = 20f;
        [ProtoMember(102)] public float PlanetCoreTemperature = 3000f;
        [ProtoMember(103)] public float PlanetSealevelDeadzone = 2000f;


        [ProtoMember(104)] public float WindRoughnessLength = 0.03f;

        [ProtoMember(105)] public float WindGradientHeight = 600f;

        [ProtoMember(106)] public float WindDiurnalAmplitude = 0.35f;

        [ProtoMember(107)] public float WindDiurnalCrossover = 80f;

        [ProtoMember(108)] public float WindTerrainInfluence = 1f;

        [ProtoMember(109)] public float WindTerrainRadius = 300f;

        [ProtoMember(111)] public float WindSlopeStrength = 1f;


        [ProtoMember(47)] public float HeatPumpCarnotFraction = 0.4f;

        [ProtoMember(48)] public float HeatPumpMaxCoefficient = 8f;


        [ProtoMember(115)] public float SuitConductance = 2.5f;

        [ProtoMember(116)] public float SuitHeatCapacity = 240000f;

        [ProtoMember(117)] public float SuitCoolingWatts = 500f;

        [ProtoMember(118)] public float SuitCriticalTemperature = 315.15f;

        [ProtoMember(119)] public float SuitDamagePerKelvin = 1f;


        [ProtoMember(50)] public bool DebugTextOnScreen = false;

        [ProtoMember(51)] public bool DebugSolarRaycast = false;

        [ProtoMember(52)] public bool DebugWindRaycast = false;

        [ProtoMember(164)] public bool DebugAeroOverlay = false;

        [ProtoMember(57)] public int DebugBlockOverlay = 0;

        [ProtoMember(114)] public int DebugOverlayMaxBoxes = 12000;

        [ProtoMember(112)] public int DebugWindOverlay = 0;

        [ProtoMember(113)] public bool DebugWindIndicator = true;

        [ProtoMember(134)] public bool ShowEnvironmentReadout = true;

        [ProtoMember(121)] public bool HeatGlow = true;

        [ProtoMember(122)] public bool HeatWarningSound = true;

        [ProtoMember(129)] public bool HeatTerminalPanel = true;

        [ProtoMember(78)] public float RoomOverlayMinKelvin = 253.15f;

        [ProtoMember(79)] public float RoomOverlayMaxKelvin = 323.15f;


        [ProtoMember(123)] public bool EnableTemperatureSync = true;

        [ProtoMember(124)] public float TemperatureSyncInterval = 5f;


        [ProtoMember(80)] public bool EnableTelemetry = false;
        [ProtoMember(81)] public int TelemetrySampleStride = 4;

        [ProtoMember(110)] public int TelemetryPlanetProbes = 0;

        public static Settings GetDefaults()
        {
            Settings s = new Settings();
            s.Version = CurrentVersion;
            s.Clamp();
            return s;
        }

        private void MigrateLoopCoupling()
        {
            if (LegacyLoopConductivity < 0f) return;

            LoopHeatTransferCoefficient = LegacyLoopConductivity * ShippedLoopCoefficient;
            LegacyLoopConductivity = -1f;
        }

        private const float ShippedLoopCoefficient = 1000f;

        private void Clamp()
        {
            MigrateLoopCoupling();

            if (Frequency < 1) Frequency = 1;

            if (TemperatureSyncInterval < 0.5f) TemperatureSyncInterval = 0.5f;
            if (HeatTimeScale <= 0f) HeatTimeScale = 1f;
            if (MaxElementVisitsPerStep < 0) MaxElementVisitsPerStep = 0;
            if (MaxSubsteps < 1) MaxSubsteps = 1;
            if (MaxSubstepsPerBlock < 0) MaxSubstepsPerBlock = 0;
            if (TelemetrySampleStride < 1) TelemetrySampleStride = 1;
            if (SolarOcclusionInterval < 1) SolarOcclusionInterval = 1;
            if (SolarTerrainRange < 0f) SolarTerrainRange = 0f;
            if (ClimateGroundInfluence < 0f) ClimateGroundInfluence = 0f;
            if (ClimateGroundInfluence > 1f) ClimateGroundInfluence = 1f;
            if (ClimateWeatherInfluence < 0f) ClimateWeatherInfluence = 0f;
            if (ClimateWeatherInfluence > 1f) ClimateWeatherInfluence = 1f;
            if (ShadowDetail < 0) ShadowDetail = 0;
            if (ShadowDetail > (int)ShadowLevel.Everything) ShadowDetail = (int)ShadowLevel.Everything;
            if (SolarOcclusionSamples < 1) SolarOcclusionSamples = 1;
            if (SolarOcclusionSamples > Core.SolarOcclusionSampler.MaxSamples)
                SolarOcclusionSamples = Core.SolarOcclusionSampler.MaxSamples;
            if (RoomOverlayMaxKelvin <= RoomOverlayMinKelvin)
                RoomOverlayMaxKelvin = RoomOverlayMinKelvin + 1f;
            if (DebugOverlayMaxBoxes < 0) DebugOverlayMaxBoxes = 0;
            if (DebugBlockOverlay < 0) DebugBlockOverlay = 0;
            if (DebugBlockOverlay >= ThermalDebugView.ModeCount)
                DebugBlockOverlay = ThermalDebugView.ModeCount - 1;
            if (DebugWindOverlay < 0) DebugWindOverlay = 0;
            if (DebugWindOverlay >= WindOverlay.ModeCount)
                DebugWindOverlay = WindOverlay.ModeCount - 1;
            if (RoomConvectionCoefficient < 0f) RoomConvectionCoefficient = 0f;
            if (RoomAirDensity < 0f) RoomAirDensity = 0f;
            if (HeatPumpCarnotFraction < 0f) HeatPumpCarnotFraction = 0f;
            if (HeatPumpCarnotFraction > 1f) HeatPumpCarnotFraction = 1f;
            if (HeatPumpMaxCoefficient < 0f) HeatPumpMaxCoefficient = 0f;
            if (SuitConductance < 0f) SuitConductance = 0f;
            if (SuitHeatCapacity <= 0f) SuitHeatCapacity = Core.ThermalSettings.MinimumSuitHeatCapacity;
            if (SuitCoolingWatts < 0f) SuitCoolingWatts = 0f;
            if (SuitCriticalTemperature < 0f) SuitCriticalTemperature = 0f;
            if (SuitDamagePerKelvin < 0f) SuitDamagePerKelvin = 0f;
            if (WindRoughnessLength <= 0f) WindRoughnessLength = 0.0002f;
            if (WindGradientHeight < Core.WindProfile.ReferenceHeight)
                WindGradientHeight = Core.WindProfile.ReferenceHeight;
            if (WindDiurnalAmplitude < 0f) WindDiurnalAmplitude = 0f;
            if (WindDiurnalAmplitude > 1f) WindDiurnalAmplitude = 1f;
            if (WindDiurnalCrossover < 0f) WindDiurnalCrossover = 0f;
            if (WindTerrainInfluence < 0f) WindTerrainInfluence = 0f;
            if (WindTerrainInfluence > 1f) WindTerrainInfluence = 1f;
            if (WindTerrainRadius < 0f) WindTerrainRadius = 0f;
            if (WindSlopeStrength < 0f) WindSlopeStrength = 0f;
            if (WindSlopeStrength > 1f) WindSlopeStrength = 1f;
            if (TelemetryPlanetProbes < 0) TelemetryPlanetProbes = 0;
        }


        [XmlIgnore]
        private Core.ThermalSettings core;

        public Core.ThermalSettings ToCore()
        {
            if (core == null) core = new Core.ThermalSettings();
            Apply();
            return core;
        }

        public void RestoreDefaults()
        {
            Settings shipped = GetDefaults();
            List<string> names = Names();

            for (int i = 0; i < names.Count; i++)
            {
                string setting = names[i];
                if (ClientOwned.Contains(setting)) continue;

                SetValue(setting, shipped.GetValue(setting));
            }

            Apply();
        }

        private string definitionState;

        private string DefinitionState()
        {
            StringBuilder state = new StringBuilder();
            List<string> names = Names();

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (!name.StartsWith("Loop", StringComparison.Ordinal)
                    && !name.StartsWith("Planet", StringComparison.Ordinal)) continue;

                state.Append(name).Append('=')
                     .Append(GetValue(name).ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            return state.ToString();
        }

        private static void RebuildGrids()
        {
            try
            {
                ThermalBlockCatalog.Clear();

                IList<ThermalGrid> grids = ThermalGrid.LiveGrids;
                for (int i = 0; grids != null && i < grids.Count; i++)
                {
                    if (grids[i] != null) grids[i].RefreshDefinitions();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] failed to rebuild grids after a definition change\n" + e);
            }
        }

        public void Apply()
        {
            Clamp();

            SettingsSync.Publish(this);
            if (this == Instance && !SettingsSync.Applying) SavePending = true;
            if (core == null) core = new Core.ThermalSettings();

            core.EnableEnvironment = EnableEnvironment;
            core.EnableConduction = EnableConduction;
            core.EnableRadiation = EnableRadiation;
            core.EnableConvection = EnableConvection;
            core.EnableSolarHeat = EnableSolarHeat;
            core.SolarSelfShadowing = SolarSelfShadowing;
            core.EnableHeatSources = EnableHeatSources;
            core.EnableWasteHeat = EnableWasteHeat;
            core.EnablePlanets = EnablePlanets;
            core.EnableFriction = EnableFriction;
            core.EnableDamage = EnableDamage;
            core.EnableCoolantLoops = EnableCoolantLoops;
            core.WellMixedCoolant = WellMixedCoolant;
            core.EnableRoomAir = EnableRoomAir;
            core.EnableHeatPumps = EnableHeatPumps;

            core.ClampConductionOvershoot = ClampOvershoot;
            core.ClampEnvironmentOvershoot = ClampOvershoot;
            core.DamageIsPerSecond = DamageIsPerSecond;
            core.Frequency = Frequency;
            core.SimulationSpeed = 1f;
            core.HeatTimeScale = HeatTimeScale;
            core.MaxElementVisitsPerStep = MaxElementVisitsPerStep;
            core.MaxSubsteps = MaxSubsteps;
            core.MaxSubstepsPerBlock = MaxSubstepsPerBlock;
            core.FloorBlocksWhenOverBudget = FloorBlocksWhenOverBudget;

            core.VacuumTemperature = VacuumTemperature;
            core.SolarEnergy = SolarEnergy;
            core.FrictionAtSpeedsAbove = FrictionAtSpeedsAbove;
            core.FrictionScale = FrictionScale;
            core.EnableWindwardShielding = EnableWindwardShielding;
            core.RoomConvectionCoefficient = RoomConvectionCoefficient;
            core.RoomAirDensity = RoomAirDensity;
            core.HeatPumpCarnotFraction = HeatPumpCarnotFraction;
            core.HeatPumpMaxCoefficient = HeatPumpMaxCoefficient;
            core.EnableSuitDamage = EnableSuitDamage;
            core.SuitConductance = SuitConductance;
            core.SuitHeatCapacity = SuitHeatCapacity;
            core.SuitCoolingWatts = SuitCoolingWatts;
            core.SuitCriticalTemperature = SuitCriticalTemperature;
            core.SuitDamagePerKelvin = SuitDamagePerKelvin;

            core.Derive();

            Core.ThermalValidation.Check(core);

            Telemetry.SampleStride = TelemetrySampleStride;

            string state = DefinitionState();
            bool moved = definitionState != null && definitionState != state;
            definitionState = state;
            if (moved) RebuildGrids();
        }

        [XmlIgnore]
        public float StepsPerSecond
        {
            get { return core == null ? Frequency : core.StepsPerSecond; }
        }


        public static readonly HashSet<string> ClientOwned = new HashSet<string>
        {
            "DebugTextOnScreen", "DebugSolarRaycast", "DebugWindRaycast", "DebugAeroOverlay",
            "DebugBlockOverlay",
            "DebugWindOverlay", "DebugWindIndicator", "DebugOverlayMaxBoxes",
            "ShowEnvironmentReadout",
            "HeatGlow", "HeatWarningSound", "HeatTerminalPanel",
        };

        public static List<string> Names()
        {
            return new List<string>
            {
                "EnableEnvironment", "EnableConduction", "EnableRadiation", "EnableConvection",
                "EnableSolarHeat", "ShadowDetail", "SolarTerrainRange", "SolarOcclusionSamples",
                "EnableHeatSources", "EnableWasteHeat", "EnablePlanets",
                "EnableFriction", "EnableWind", "EnableDamage", "EnableCoolantLoops", "EnableRoomAir",
                "EnableHeatPumps", "WellMixedCoolant",
                "ClampOvershoot", "DamageIsPerSecond",
                "Frequency", "HeatTimeScale", "MaxElementVisitsPerStep",
                "MaxSubsteps", "MaxSubstepsPerBlock", "FloorBlocksWhenOverBudget",
                "VacuumTemperature", "SolarEnergy", "FrictionAtSpeedsAbove", "FrictionScale",
                "EnableWindwardShielding",
                "RoomConvectionCoefficient", "RoomAirDensity", "SolarOcclusionInterval",
                "ClimateGroundInfluence", "ClimateWeatherInfluence",
                "HeatPumpCarnotFraction", "HeatPumpMaxCoefficient",
                "EnableSuitDamage", "SuitConductance", "SuitHeatCapacity", "SuitCoolingWatts",
                "SuitCriticalTemperature", "SuitDamagePerKelvin",
                "WindRoughnessLength", "WindGradientHeight",
                "WindDiurnalAmplitude", "WindDiurnalCrossover",
                "WindTerrainInfluence", "WindTerrainRadius", "WindSlopeStrength",
                "DebugTextOnScreen", "DebugSolarRaycast", "DebugWindRaycast", "DebugAeroOverlay",
                "DebugBlockOverlay", "DebugWindOverlay", "DebugWindIndicator",
                "ShowEnvironmentReadout",
                "DebugOverlayMaxBoxes",
                "HeatGlow", "HeatWarningSound", "HeatTerminalPanel",
                "RoomOverlayMinKelvin", "RoomOverlayMaxKelvin",
                "EnableTemperatureSync", "TemperatureSyncInterval", "ParallelGrids",
                "EnableTelemetry", "TelemetrySampleStride", "TelemetryPlanetProbes",

                "LoopFlowRate", "LoopCoolantKilogramsPerCubicMetre",
                "LoopSpecificHeat", "LoopHeatTransferCoefficient", "LoopContactMultiplier",
                "LoopStagnantTransferFraction",
                "LoopRefillEquivalentKelvin", "LoopRefillKilogramsPerSecond",

                "PlanetDayTemperature", "PlanetNightTemperature", "PlanetPoleTemperatureDrop",
                "PlanetAmbientLapseRate", "PlanetAmbientLagSeconds", "PlanetConvectionCoefficient",
                "PlanetUndergroundConvectionCoefficient",
                "PlanetSolarDecay", "PlanetUndergroundTemperature",
                "PlanetUndergroundDampingDepth", "PlanetCoreTemperature",
                "PlanetSealevelDeadzone",
            };
        }

        public float GetValue(string name)
        {
            switch (name)
            {
                case "EnableEnvironment": return Flag(EnableEnvironment);
                case "EnableConduction": return Flag(EnableConduction);
                case "EnableRadiation": return Flag(EnableRadiation);
                case "EnableConvection": return Flag(EnableConvection);
                case "EnableSolarHeat": return Flag(EnableSolarHeat);
                case "ShadowDetail": return ShadowDetail;
                case "SolarTerrainRange": return SolarTerrainRange;
                case "SolarOcclusionSamples": return SolarOcclusionSamples;
                case "EnableHeatSources": return Flag(EnableHeatSources);
                case "EnableWasteHeat": return Flag(EnableWasteHeat);
                case "EnablePlanets": return Flag(EnablePlanets);
                case "EnableFriction": return Flag(EnableFriction);
                case "EnableWind": return Flag(EnableWind);
                case "EnableDamage": return Flag(EnableDamage);
                case "EnableCoolantLoops": return Flag(EnableCoolantLoops);
                case "WellMixedCoolant": return Flag(WellMixedCoolant);
                case "EnableRoomAir": return Flag(EnableRoomAir);
                case "EnableHeatPumps": return Flag(EnableHeatPumps);
                case "ClampOvershoot": return Flag(ClampOvershoot);
                case "DamageIsPerSecond": return Flag(DamageIsPerSecond);
                case "Frequency": return Frequency;
                case "HeatTimeScale": return HeatTimeScale;
                case "MaxElementVisitsPerStep": return MaxElementVisitsPerStep;
                case "MaxSubsteps": return MaxSubsteps;
                case "MaxSubstepsPerBlock": return MaxSubstepsPerBlock;
                case "FloorBlocksWhenOverBudget": return Flag(FloorBlocksWhenOverBudget);
                case "VacuumTemperature": return VacuumTemperature;
                case "SolarEnergy": return SolarEnergy;
                case "FrictionAtSpeedsAbove": return FrictionAtSpeedsAbove;
                case "FrictionScale": return FrictionScale;
                case "EnableWindwardShielding": return EnableWindwardShielding ? 1f : 0f;
                case "RoomConvectionCoefficient": return RoomConvectionCoefficient;
                case "RoomAirDensity": return RoomAirDensity;
                case "HeatPumpCarnotFraction": return HeatPumpCarnotFraction;
                case "HeatPumpMaxCoefficient": return HeatPumpMaxCoefficient;
                case "EnableSuitDamage": return EnableSuitDamage ? 1f : 0f;
                case "SuitConductance": return SuitConductance;
                case "SuitHeatCapacity": return SuitHeatCapacity;
                case "SuitCoolingWatts": return SuitCoolingWatts;
                case "SuitCriticalTemperature": return SuitCriticalTemperature;
                case "SuitDamagePerKelvin": return SuitDamagePerKelvin;
                case "WindRoughnessLength": return WindRoughnessLength;
                case "WindGradientHeight": return WindGradientHeight;
                case "WindDiurnalAmplitude": return WindDiurnalAmplitude;
                case "WindDiurnalCrossover": return WindDiurnalCrossover;
                case "WindTerrainInfluence": return WindTerrainInfluence;
                case "WindTerrainRadius": return WindTerrainRadius;
                case "WindSlopeStrength": return WindSlopeStrength;
                case "SolarOcclusionInterval": return SolarOcclusionInterval;
                case "ClimateGroundInfluence": return ClimateGroundInfluence;
                case "ClimateWeatherInfluence": return ClimateWeatherInfluence;
                case "DebugTextOnScreen": return Flag(DebugTextOnScreen);
                case "DebugSolarRaycast": return Flag(DebugSolarRaycast);
                case "DebugWindRaycast": return Flag(DebugWindRaycast);
                case "DebugAeroOverlay": return Flag(DebugAeroOverlay);
                case "DebugBlockOverlay": return DebugBlockOverlay;
                case "DebugOverlayMaxBoxes": return DebugOverlayMaxBoxes;
                case "DebugWindOverlay": return DebugWindOverlay;
                case "DebugWindIndicator": return Flag(DebugWindIndicator);
                case "ShowEnvironmentReadout": return Flag(ShowEnvironmentReadout);
                case "HeatGlow": return Flag(HeatGlow);
                case "HeatTerminalPanel": return Flag(HeatTerminalPanel);
                case "HeatWarningSound": return Flag(HeatWarningSound);
                case "RoomOverlayMinKelvin": return RoomOverlayMinKelvin;
                case "RoomOverlayMaxKelvin": return RoomOverlayMaxKelvin;
                case "EnableTemperatureSync": return Flag(EnableTemperatureSync);
                case "ParallelGrids": return Flag(ParallelGrids);
                case "TemperatureSyncInterval": return TemperatureSyncInterval;
                case "EnableTelemetry": return Flag(EnableTelemetry);
                case "TelemetrySampleStride": return TelemetrySampleStride;
                case "TelemetryPlanetProbes": return TelemetryPlanetProbes;

                case "LoopCoolantKilogramsPerCubicMetre": return LoopCoolantKilogramsPerCubicMetre;
                case "LoopRefillEquivalentKelvin": return LoopRefillEquivalentKelvin;
                case "LoopRefillKilogramsPerSecond": return LoopRefillKilogramsPerSecond;
                case "LoopHeatTransferCoefficient": return LoopHeatTransferCoefficient;
                case "LoopSpecificHeat": return LoopSpecificHeat;
                case "LoopContactMultiplier": return LoopContactMultiplier;
                case "LoopFlowRate": return LoopFlowRate;
                case "LoopStagnantTransferFraction": return LoopStagnantTransferFraction;

                case "PlanetDayTemperature": return PlanetDayTemperature;
                case "PlanetNightTemperature": return PlanetNightTemperature;
                case "PlanetPoleTemperatureDrop": return PlanetPoleTemperatureDrop;
                case "PlanetAmbientLapseRate": return PlanetAmbientLapseRate;
                case "PlanetAmbientLagSeconds": return PlanetAmbientLagSeconds;
                case "PlanetConvectionCoefficient": return PlanetConvectionCoefficient;
                case "PlanetUndergroundConvectionCoefficient": return PlanetUndergroundConvectionCoefficient;
                case "PlanetSolarDecay": return PlanetSolarDecay;
                case "PlanetUndergroundTemperature": return PlanetUndergroundTemperature;
                case "PlanetUndergroundDampingDepth": return PlanetUndergroundDampingDepth;
                case "PlanetCoreTemperature": return PlanetCoreTemperature;
                case "PlanetSealevelDeadzone": return PlanetSealevelDeadzone;
                default: return float.NaN;
            }
        }

        public bool SetValue(string name, float value)
        {
            switch (name)
            {
                case "EnableEnvironment": EnableEnvironment = Flag(value); return true;
                case "EnableConduction": EnableConduction = Flag(value); return true;
                case "EnableRadiation": EnableRadiation = Flag(value); return true;
                case "EnableConvection": EnableConvection = Flag(value); return true;
                case "EnableSolarHeat": EnableSolarHeat = Flag(value); return true;
                case "ShadowDetail": ShadowDetail = (int)value; return true;
                case "SolarTerrainRange": SolarTerrainRange = value; return true;
                case "SolarOcclusionSamples": SolarOcclusionSamples = (int)value; return true;
                case "EnableHeatSources": EnableHeatSources = Flag(value); return true;
                case "EnableWasteHeat": EnableWasteHeat = Flag(value); return true;
                case "EnablePlanets": EnablePlanets = Flag(value); return true;
                case "EnableFriction": EnableFriction = Flag(value); return true;
                case "EnableWind": EnableWind = Flag(value); return true;
                case "EnableDamage": EnableDamage = Flag(value); return true;
                case "EnableCoolantLoops": EnableCoolantLoops = Flag(value); return true;
                case "WellMixedCoolant": WellMixedCoolant = Flag(value); return true;
                case "EnableRoomAir": EnableRoomAir = Flag(value); return true;
                case "EnableHeatPumps": EnableHeatPumps = Flag(value); return true;
                case "ClampOvershoot": ClampOvershoot = Flag(value); return true;
                case "DamageIsPerSecond": DamageIsPerSecond = Flag(value); return true;
                case "Frequency": Frequency = (int)value; return true;
                case "HeatTimeScale": HeatTimeScale = value; return true;
                case "MaxElementVisitsPerStep": MaxElementVisitsPerStep = (int)value; return true;
                case "MaxSubsteps": MaxSubsteps = (int)value; return true;
                case "MaxSubstepsPerBlock": MaxSubstepsPerBlock = (int)value; return true;
                case "FloorBlocksWhenOverBudget": FloorBlocksWhenOverBudget = Flag(value); return true;
                case "VacuumTemperature": VacuumTemperature = value; return true;
                case "SolarEnergy": SolarEnergy = value; return true;
                case "FrictionAtSpeedsAbove": FrictionAtSpeedsAbove = value; return true;
                case "FrictionScale": FrictionScale = value; return true;
                case "EnableWindwardShielding": EnableWindwardShielding = value != 0f; return true;
                case "RoomConvectionCoefficient": RoomConvectionCoefficient = value; return true;
                case "RoomAirDensity": RoomAirDensity = value; return true;
                case "HeatPumpCarnotFraction": HeatPumpCarnotFraction = value; return true;
                case "HeatPumpMaxCoefficient": HeatPumpMaxCoefficient = value; return true;
                case "EnableSuitDamage": EnableSuitDamage = value != 0f; return true;
                case "SuitConductance": SuitConductance = value; return true;
                case "SuitHeatCapacity": SuitHeatCapacity = value; return true;
                case "SuitCoolingWatts": SuitCoolingWatts = value; return true;
                case "SuitCriticalTemperature": SuitCriticalTemperature = value; return true;
                case "SuitDamagePerKelvin": SuitDamagePerKelvin = value; return true;
                case "WindRoughnessLength": WindRoughnessLength = value; return true;
                case "WindGradientHeight": WindGradientHeight = value; return true;
                case "WindDiurnalAmplitude": WindDiurnalAmplitude = value; return true;
                case "WindDiurnalCrossover": WindDiurnalCrossover = value; return true;
                case "WindTerrainInfluence": WindTerrainInfluence = value; return true;
                case "WindTerrainRadius": WindTerrainRadius = value; return true;
                case "WindSlopeStrength": WindSlopeStrength = value; return true;
                case "SolarOcclusionInterval": SolarOcclusionInterval = (int)value; return true;
                case "ClimateGroundInfluence": ClimateGroundInfluence = value; return true;
                case "ClimateWeatherInfluence": ClimateWeatherInfluence = value; return true;
                case "DebugTextOnScreen": DebugTextOnScreen = Flag(value); return true;
                case "DebugSolarRaycast": DebugSolarRaycast = Flag(value); return true;
                case "DebugWindRaycast": DebugWindRaycast = Flag(value); return true;
                case "DebugAeroOverlay": DebugAeroOverlay = Flag(value); return true;
                case "RoomOverlayMinKelvin": RoomOverlayMinKelvin = value; return true;
                case "RoomOverlayMaxKelvin": RoomOverlayMaxKelvin = value; return true;
                case "DebugBlockOverlay":
                    DebugBlockOverlay = (int)value;
                    ThermalDebugView.Set((ThermalDebugView.Mode)DebugBlockOverlay);
                    return true;
                case "DebugWindOverlay":
                    DebugWindOverlay = (int)value;
                    WindOverlay.Set((WindOverlay.Mode)DebugWindOverlay);
                    return true;
                case "DebugOverlayMaxBoxes": DebugOverlayMaxBoxes = (int)value; return true;
                case "DebugWindIndicator": DebugWindIndicator = Flag(value); return true;
                case "ShowEnvironmentReadout": ShowEnvironmentReadout = Flag(value); return true;
                case "HeatGlow": HeatGlow = Flag(value); return true;
                case "HeatTerminalPanel": HeatTerminalPanel = Flag(value); return true;
                case "HeatWarningSound": HeatWarningSound = Flag(value); return true;
                case "EnableTemperatureSync": EnableTemperatureSync = Flag(value); return true;
                case "ParallelGrids": ParallelGrids = Flag(value); return true;
                case "TemperatureSyncInterval": TemperatureSyncInterval = value; return true;
                case "EnableTelemetry": EnableTelemetry = Flag(value); Telemetry.SetEnabled(EnableTelemetry); return true;
                case "TelemetrySampleStride": TelemetrySampleStride = (int)value; return true;
                case "TelemetryPlanetProbes": TelemetryPlanetProbes = (int)value; return true;

                case "LoopCoolantKilogramsPerCubicMetre":
                    LoopCoolantKilogramsPerCubicMetre = value; return true;
                case "LoopRefillEquivalentKelvin": LoopRefillEquivalentKelvin = value; return true;
                case "LoopRefillKilogramsPerSecond": LoopRefillKilogramsPerSecond = value; return true;
                case "LoopHeatTransferCoefficient": LoopHeatTransferCoefficient = value; return true;
                case "LoopSpecificHeat": LoopSpecificHeat = value; return true;
                case "LoopContactMultiplier": LoopContactMultiplier = value; return true;
                case "LoopFlowRate": LoopFlowRate = value; return true;
                case "LoopStagnantTransferFraction": LoopStagnantTransferFraction = value; return true;

                case "PlanetDayTemperature": PlanetDayTemperature = value; return true;
                case "PlanetNightTemperature": PlanetNightTemperature = value; return true;
                case "PlanetPoleTemperatureDrop": PlanetPoleTemperatureDrop = value; return true;
                case "PlanetAmbientLapseRate": PlanetAmbientLapseRate = value; return true;
                case "PlanetAmbientLagSeconds": PlanetAmbientLagSeconds = value; return true;
                case "PlanetConvectionCoefficient": PlanetConvectionCoefficient = value; return true;
                case "PlanetUndergroundConvectionCoefficient": PlanetUndergroundConvectionCoefficient = value; return true;
                case "PlanetSolarDecay": PlanetSolarDecay = value; return true;
                case "PlanetUndergroundTemperature": PlanetUndergroundTemperature = value; return true;
                case "PlanetUndergroundDampingDepth": PlanetUndergroundDampingDepth = value; return true;
                case "PlanetCoreTemperature": PlanetCoreTemperature = value; return true;
                case "PlanetSealevelDeadzone": PlanetSealevelDeadzone = value; return true;
                default: return false;
            }
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


        public static Settings EnsureLoaded()
        {
            if (Instance != null) return Instance;

            Instance = CanReadWorldStorage() ? Load() : GetDefaults();
            Instance.Apply();
            return Instance;
        }

        private static bool CanReadWorldStorage()
        {
            try
            {
                return MyAPIGateway.Utilities != null
                    && MyAPIGateway.Session != null
                    && MyAPIGateway.Session.IsServer;
            }
            catch
            {
                return false;
            }
        }

        public static Settings Load()
        {
            Settings settings = GetDefaults();
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    Settings loaded = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);

                    if (text.IndexOf("MaxLinkVisitsPerStep", StringComparison.Ordinal) >= 0)
                    {
                        MyLog.Default.Info("[" + Name + "] MaxLinkVisitsPerStep is now"
                            + " MaxElementVisitsPerStep and counts nodes as well as links;"
                            + " the old value was not carried over. Default "
                            + settings.MaxElementVisitsPerStep + " is in force.");
                    }

                    if (loaded.Version != CurrentVersion)
                    {
                        MyLog.Default.Info("[" + Name + "] config version " + loaded.Version
                            + " replaced with " + CurrentVersion);
                        Save(settings);
                    }
                    else
                    {
                        settings = loaded;
                    }
                }
                else
                {
                    Save(settings);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] failed to load configuration, using defaults\n" + e);
            }

            settings.Clamp();
            return settings;
        }

        public static bool SavePending;

        public static void FlushPending()
        {
            if (!SavePending || Instance == null) return;

            try
            {
                if (MyAPIGateway.Session == null || !MyAPIGateway.Session.IsServer) return;

                SavePending = false;
                Save(Instance);
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] deferred save failed\n" + e);
            }
        }

        public static void Save(Settings settings)
        {
            try
            {
                TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] failed to save settings\n" + e);
            }
        }
    }
}

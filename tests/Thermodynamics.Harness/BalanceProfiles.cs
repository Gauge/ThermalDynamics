using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public class BalanceProfile
    {
        public string Name;
        public string Intent;


        public float HeatTimeScale = 90f;

        public float ConductionPace = ThermalConstants.ConductionScale;



        public int Frequency = new HistoricalThermalSettings().Frequency;


        public int MaxSubsteps = new HistoricalThermalSettings().MaxSubsteps;

        public bool ClampOvershoot = true;


        public float SolarEnergy = 1000f;

        public float VacuumTemperature = 2.7f;

        public float RoomConvectionCoefficient = 8f;


        public bool EnableRoomAir = true;
        public bool SolarSelfShadowing = true;
        public bool EnableFriction = true;
        public bool EnableCoolantLoops = true;

        public bool WellMixedCoolant = false;


        public float ReactorWasteFraction = 0.25f;

        public float FlowRate = 10f;



        public HistoricalThermalSettings ToSettings()
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                Frequency = Frequency,
                SimulationSpeed = 1f,
                HeatTimeScale = HeatTimeScale,
                MaxSubsteps = MaxSubsteps,
                ClampConductionOvershoot = ClampOvershoot,
                ClampEnvironmentOvershoot = ClampOvershoot,
                SolarEnergy = SolarEnergy,
                VacuumTemperature = VacuumTemperature,
                RoomConvectionCoefficient = RoomConvectionCoefficient,
                EnableRoomAir = EnableRoomAir,
                SolarSelfShadowing = SolarSelfShadowing,
                EnableFriction = EnableFriction,
                EnableCoolantLoops = EnableCoolantLoops,
                WellMixedCoolant = WellMixedCoolant,
            };
            return settings.Derive();
        }


        public BlockThermalProperties Material(BlockThermalProperties source)
        {
            BlockThermalProperties copy = source.Clone();
            copy.Conductivity = source.Conductivity * (ConductionPace / ThermalConstants.ConductionScale);
            return copy;
        }



        public static BalanceProfile Physical()
        {
            return new BalanceProfile
            {
                Name = "physical",
                Intent = "every constant its real value; nothing clamped",
                HeatTimeScale = 1f,
                ConductionPace = 1f,
                Frequency = 8,
                MaxSubsteps = 64,
                SolarEnergy = 1361f,
                VacuumTemperature = 2.725f,
                RoomConvectionCoefficient = 8f,
                WellMixedCoolant = false,
                ReactorWasteFraction = 2f,
                FlowRate = 10f,
            };
        }


        public static BalanceProfile Shipped()
        {

            HistoricalThermalSettings shipped = new HistoricalThermalSettings();

            return new BalanceProfile
            {
                Name = "shipped",
                Intent = "the current default",
                HeatTimeScale = shipped.HeatTimeScale,
                ConductionPace = ThermalConstants.ConductionScale,
                Frequency = shipped.Frequency,
                MaxSubsteps = shipped.MaxSubsteps,
            };
        }


        public static BalanceProfile Arcade()
        {
            return new BalanceProfile
            {
                Name = "arcade",
                Intent = "one clamped substep, mechanisms off, ring lumped",
                HeatTimeScale = 20000f,
                ConductionPace = ThermalConstants.ConductionScale,
                Frequency = 6,
                MaxSubsteps = 1,
                EnableRoomAir = false,
                SolarSelfShadowing = false,
                EnableFriction = false,
                WellMixedCoolant = true,
            };
        }


        public static BalanceProfile Candidate()
        {

            HistoricalThermalSettings shipped = new HistoricalThermalSettings();

            return new BalanceProfile
            {
                Name = "candidate",
                Intent = "real materials and constants, game clock",
                HeatTimeScale = shipped.HeatTimeScale,
                ConductionPace = 1f,
                Frequency = shipped.Frequency,
                MaxSubsteps = shipped.MaxSubsteps,
                SolarEnergy = 1361f,
                VacuumTemperature = 2.725f,
                WellMixedCoolant = false,
                ReactorWasteFraction = 0.25f,
            };
        }


        public static List<BalanceProfile> All()
        {
            return new List<BalanceProfile> { Physical(), Candidate(), Shipped(), Arcade() };
        }

        public static readonly string[] RealismGaps =
        {
            "Grey body by default: a block absorbs at its emissivity unless a definition declares "
            + "a SolarAbsorptivity of its own. Selective surfaces are expressible now — that was "
            + "the gap — but no shipped block uses one, and the wavelength dependence a real "
            + "selective surface has is still two constants rather than a spectrum.",

            "No inter-block radiation: a face radiates to the sky or to nothing. Two hot blocks "
            + "facing each other across a gap do not see each other, and no view factors exist.",

            "Conduction treats every block as a solid billet of one material, with the path length "
            + "taken from its half-depth. A real block is a shell around a void, and a long thin "
            + "panel conducts far better along its skin than through its middle.",

            "No contact resistance at a joint. Two bolted blocks conduct as if welded, where a real "
            + "mechanical joint is often the dominant resistance in the path.",

            "The coolant's fluid-to-wall coupling is modelled as conduction through a slab, using a "
            + "conductivity. The real quantity is a convective heat transfer coefficient in "
            + "W/(m^2 K), which depends on flow speed — so in this model circulating faster moves "
            + "heat around the ring but does not improve the exchange with the pipe wall.",

            "Room air is one well-mixed mass per compartment: no stratification, no draughts, and "
            + "a fixed convection coefficient regardless of geometry or temperature difference.",

            "Waste heat is a fraction of electrical throughput, capped at 1. A real fission plant "
            + "sheds about two watts per watt delivered, which this cannot express without the "
            + "validator calling it energy from nothing.",

            "Thruster heat is a fraction of thrust power with no exhaust: a real rocket carries the "
            + "great majority of its waste heat away in the plume rather than into the ship.",
        };
    }
}

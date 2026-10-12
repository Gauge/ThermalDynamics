using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public class ShipProfile
    {
        public string Name;
        public long WorkshopId;
        public bool Large;
        public int Blocks;

        public float Mass;

        public float HeatCapacity;

        public float ExposedArea;

        public float ExposedFraction;

        public float WasteWatts;

        public float PowerOutputWatts;

        public float ThrustNewtons;

        public float ThrustNewtonsAllDirections;

        public int HeatVents;

        public float ThermalStress
        {
            get { return ExposedArea <= 0f ? 0f : WasteWatts / ExposedArea; }
        }


        public float EquilibriumKelvin(float emissivity = 0.15f)
        {
            if (ThermalStress <= 0f) return 0f;
            return (float)Math.Pow(ThermalStress / (emissivity * ThermalConstants.StefanBoltzmann), 0.25d);
        }

        public float PeakSubstepDemand;

        public float SubstepDemandP95;

        public float SubstepDemandMedian;

        public string StiffestBlock;

        public float PeakSubstepDemandInAir;

        public string StiffestBlockInAir;

        public int Rooms;

        public int Grids;
        public int Joints;

        public double[] Features
        {
            get
            {
                return new double[]
                {
                    Log(Blocks),
                    Log(Mass),
                    Log(ExposedArea),
                    ExposedFraction,
                    Log(WasteWatts),
                    Log(ThermalStress),
                    Log(PeakSubstepDemand),
                    Large ? 1d : 0d,
                };
            }
        }

        public static readonly string[] FeatureNames =
        {
            "log blocks", "log mass", "log area", "exposed fraction",
            "log waste W", "log stress", "log stiffness", "large grid",
        };


        private static double Log(double value)
        {
            return Math.Log10(value > 0d ? value + 1d : 1d);
        }


        public static ShipProfile Measure(Blueprints.Ship ship, ThermalSettings settings = null)
        {

            ThermalSettings effective = settings ?? new HistoricalThermalSettings();
            ShipAssembly assembly = ship.Build(effective);

            ShipProfile profile = new ShipProfile
            {
                Name = ship.Name,
                WorkshopId = ship.WorkshopId,
                Large = ship.Large,
                Blocks = ship.Blocks,
                Grids = assembly.Simulations.Count,
                Joints = assembly.Bridges.Count,
                Rooms = assembly.RoomCount,
            };


            List<float> demands = new List<float>(assembly.NodeCount);

            EnvironmentState air = SeaLevelAir(effective);
            float peakInAir = 0f;
            float[] thrustByDirection = new float[Face.Count];
            float draw = 0f;
            float installed = 0f;
            int exposed = 0;
            float peak = 0f;

            for (int g = 0; g < assembly.Simulations.Count; g++)
            {
                ThermalSolver solver = assembly.Simulations[g].Solver;

                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    ThermalNode node = solver.Nodes[i];

                    profile.Mass += node.Block.Mass;
                    profile.HeatCapacity += node.ThermalMass;
                    profile.ExposedArea += node.ExposedArea;
                    if (node.TotalExposedFaces > 0) exposed++;

                    float demand = solver.NodeSubstepDemand(i);
                    demands.Add(demand);

                    if (demand > peak)
                    {
                        peak = demand;
                        profile.StiffestBlock = node.Block.Name;
                    }

                    float inAir = solver.NodeSubstepDemand(i, ref air);
                    if (inAir > peakInAir)
                    {
                        peakInAir = inAir;
                        profile.StiffestBlockInAir = node.Block.Name;
                    }

                    Rate(profile, node.Block, thrustByDirection, ref draw, ref installed);
                }
            }

            float thrust = 0f;
            for (int i = 0; i < thrustByDirection.Length; i++)
            {
                profile.ThrustNewtonsAllDirections += thrustByDirection[i];
                if (thrustByDirection[i] > thrust) thrust = thrustByDirection[i];
            }

            profile.ThrustNewtons = thrust;
            profile.PowerOutputWatts = installed;

            float fromGenerators = draw < installed ? draw : installed;
            float shortfall = draw - fromGenerators;
            float fromStores = shortfall < profile.StoreReserveWatts ? shortfall : profile.StoreReserveWatts;

            profile.WasteWatts =
                (fromGenerators * ShippedBlocks.FunctionOf("Reactor").ProducerWasteEnergy)
                + (fromStores * ShippedBlocks.FunctionOf("BatteryBlock").ProducerWasteEnergy)
                + profile.ConsumerWasteWatts
                + (thrust * ShippedBlocks.FunctionOf("Thrust").ConsumerWasteEnergy);

            profile.ExposedFraction = assembly.NodeCount == 0
                ? 0f
                : exposed / (float)assembly.NodeCount;

            profile.PeakSubstepDemand = peak;
            profile.PeakSubstepDemandInAir = peakInAir;
            demands.Sort();

            profile.SubstepDemandMedian = Percentile(demands, 0.50f);

            profile.SubstepDemandP95 = Percentile(demands, 0.95f);

            return profile;
        }


        private static EnvironmentState SeaLevelAir(ThermalSettings settings)
        {
            return EnvironmentSolver.Solve(
                settings, PlanetThermalProperties.Default(), Worlds.PlanetSurface(1f, 0.5f));
        }

        public float ConsumerWasteWatts;

        public float StoreReserveWatts;


        private static void Rate(ShipProfile profile, BlockInstance block, float[] thrustByDirection,
            ref float draw, ref float installed)
        {
            GameBlocks.Definition definition;
            if (!GameBlocks.ByModelName().TryGetValue(block.Name, out definition)) return;

            ShippedBlocks.Function function =
                ShippedBlocks.FunctionOf(definition.TypeId);

            if (definition.TypeId == "HeatVentBlock") profile.HeatVents++;

            if (ShipLoad.IsStore(definition.TypeId))
            {
                profile.StoreReserveWatts += definition.PowerOutputWatts;
                return;
            }

            if (definition.PowerOutputWatts > 0f)
            {
                installed += definition.PowerOutputWatts;
                return;
            }

            draw += definition.PowerDrawWatts;

            if (definition.ThrustNewtons > 0f)
            {
                thrustByDirection[Direction(block)] += definition.ThrustNewtons;
            }
            else
            {
                profile.ConsumerWasteWatts += definition.PowerDrawWatts * function.ConsumerWasteEnergy;
            }
        }


        private static int Direction(BlockInstance block)
        {
            Vector3I facing = block.Orientation.Rotate(Vector3I.Forward);

            for (int face = 0; face < Face.Count; face++)
            {
                if (Face.Offsets[face] == facing) return face;
            }
            return 0;
        }


        private static float Percentile(List<float> sorted, float fraction)
        {
            return LabStats.PercentileOfSorted(sorted, fraction);
        }


        public override string ToString()
        {
            return Name + " (" + Blocks + " blocks, " + ThermalStress.ToString("n0") + " W/m²)";
        }
    }
}

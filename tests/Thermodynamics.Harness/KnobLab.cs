using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public static class KnobLab
    {
        private static readonly string[] Core = { "idle", "full-electrical", "burn-forward" };

        private static readonly string[] Sunlit = { "vacuum-sunlit", "surface-hot-noon" };

        private static readonly string[] Moving = { "flight-100", "reentry" };

        private static readonly string[] CoreAndSun =
            { "idle", "full-electrical", "burn-forward", "vacuum-sunlit" };

        public class Knob
        {
            public string Name;

            public string Intent;

            public bool Multiplier;

            public float Shipped;

            public float[] Levels;

            public string[] Scenarios;

            public string OnlyType;

            public Func<float, Func<string, string, BlockThermalProperties, BlockThermalProperties>> Material;

            public Action<HistoricalThermalSettings, float> World;
        }

        public class Configuration
        {
            public Knob Knob;
            public float Level;
            public string[] Scenarios;

            public bool IsShipped;


            public HistoricalThermalSettings Settings()
            {

                HistoricalThermalSettings settings = new HistoricalThermalSettings();
                if (Knob.World != null) Knob.World(settings, Level);
                return settings.Derive();
            }


            public Func<string, string, BlockThermalProperties, BlockThermalProperties> Material()
            {
                return Knob.Material == null ? null : Knob.Material(Level);
            }


            public override string ToString()
            {
                return Knob.Name + "=" + Level.ToString("0.###");
            }
        }


        private static Func<float, Func<string, string, BlockThermalProperties, BlockThermalProperties>> Block(
            Action<BlockThermalProperties, float> set)
        {
            return level => (typeId, subtype, source) =>
            {
                BlockThermalProperties copy = source.Clone();
                set(copy, level);
                return copy;
            };
        }


        private static Func<float, Func<string, string, BlockThermalProperties, BlockThermalProperties>> OnlyOn(
            string typeId, Action<BlockThermalProperties, float> set)
        {
            return level => (blockType, subtype, source) =>
            {
                if (!string.Equals(blockType, typeId, StringComparison.Ordinal)) return source;

                BlockThermalProperties copy = source.Clone();
                set(copy, level);
                return copy;
            };
        }

        private static readonly float[] Quarters = { 0.25f, 0.5f, 1f, 2f, 4f };


        public static List<Knob> Knobs()
        {

            List<Knob> knobs = new List<Knob>();


            knobs.Add(new Knob
            {
                Name = "specific-heat",
                Intent = "how long a block takes to move — the time constant",
                Multiplier = true, Shipped = 1f, Levels = Quarters, Scenarios = Core,

                Material = Block((p, x) => p.SpecificHeat *= x),
            });

            knobs.Add(new Knob
            {
                Name = "emissivity",
                Intent = "radiating strength, and solar absorption with it",
                Multiplier = true, Shipped = 1f, Levels = Quarters, Scenarios = CoreAndSun,

                Material = Block((p, x) => p.Emissivity = Math.Min(1f, p.Emissivity * x)),
            });

            knobs.Add(new Knob
            {
                Name = "conductivity",
                Intent = "how fast heat leaves the block that made it",
                Multiplier = true, Shipped = 1f, Levels = Quarters, Scenarios = Core,

                Material = Block((p, x) => p.Conductivity *= x),
            });

            knobs.Add(new Knob
            {
                Name = "exposed-surface",
                Intent = "radiating area per block, apart from emissivity",
                Multiplier = true, Shipped = 1f, Levels = new[] { 0.5f, 1f, 2f, 4f },
                Scenarios = Core,

                Material = Block((p, x) => p.ExposedSurfaceMultiplier *= x),
            });

            knobs.Add(new Knob
            {
                Name = "producer-waste",
                Intent = "share of generated power that becomes heat",
                Multiplier = true, Shipped = 1f, Levels = Quarters, Scenarios = Core,

                Material = Block((p, x) => p.ProducerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "consumer-waste",
                Intent = "share of drawn power that becomes heat",
                Multiplier = true, Shipped = 1f, Levels = Quarters, Scenarios = Core,

                Material = Block((p, x) => p.ConsumerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "critical-temperature",
                Intent = "where damage begins — moves the verdict, not the heat",
                Multiplier = true, Shipped = 1f, Levels = new[] { 0.5f, 0.75f, 1f, 1.5f, 2f },
                Scenarios = Core,

                Material = Block((p, x) => p.CriticalTemperature *= x),
            });


            float[] cuts = { 0.1f, 0.25f, 0.5f, 1f, 2f };

            knobs.Add(new Knob
            {
                Name = "jumpdrive-waste",
                Intent = "waste fraction of jump drives alone — 67 % of the corpus's load heat",
                Multiplier = true, Shipped = 1f, Levels = cuts, Scenarios = Core,
                OnlyType = "JumpDrive",

                Material = OnlyOn("JumpDrive", (p, x) => p.ConsumerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "jumpdrive-capacity",
                Intent = "heat capacity of jump drives alone — how long one takes to cook",
                Multiplier = true, Shipped = 1f, Levels = new[] { 1f, 2f, 4f, 8f }, Scenarios = Core,
                OnlyType = "JumpDrive",

                Material = OnlyOn("JumpDrive", (p, x) => p.SpecificHeat *= x),
            });

            knobs.Add(new Knob
            {
                Name = "engine-waste",
                Intent = "waste fraction of hydrogen engines alone — ships at 0.60",
                Multiplier = true, Shipped = 1f, Levels = cuts, Scenarios = Core,
                OnlyType = "HydrogenEngine",

                Material = OnlyOn("HydrogenEngine", (p, x) => p.ProducerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "engine-conductivity",
                Intent = "how fast a hydrogen engine sheds into its neighbours",
                Multiplier = true, Shipped = 1f, Levels = new[] { 1f, 2f, 4f, 8f }, Scenarios = Core,
                OnlyType = "HydrogenEngine",

                Material = OnlyOn("HydrogenEngine", (p, x) => p.Conductivity *= x),
            });

            knobs.Add(new Knob
            {
                Name = "thruster-waste",
                Intent = "waste fraction of thrusters alone — charged against thrust, not draw",
                Multiplier = true, Shipped = 1f, Levels = cuts, Scenarios = Core,
                OnlyType = "Thrust",

                Material = OnlyOn("Thrust", (p, x) => p.ConsumerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "oxygen-waste",
                Intent = "waste fraction of oxygen generators alone — ships at 0.40, was 0.60",
                Multiplier = true, Shipped = 1f, Levels = new[] { 0.5f, 1f, 1.5f, 2f },
                Scenarios = Core,
                OnlyType = "OxygenGenerator",

                Material = OnlyOn("OxygenGenerator", (p, x) => p.ConsumerWasteEnergy *= x),
            });

            knobs.Add(new Knob
            {
                Name = "reactor-waste",
                Intent = "waste fraction of reactors alone — ships at 0.01",
                Multiplier = true, Shipped = 1f, Levels = new[] { 0.5f, 1f, 2f, 4f, 8f },
                Scenarios = Core,
                OnlyType = "Reactor",

                Material = OnlyOn("Reactor", (p, x) => p.ProducerWasteEnergy *= x),
            });


            knobs.Add(new Knob
            {
                Name = "heat-time-scale",
                Intent = "seconds of physical time per second of play",
                Multiplier = false, Shipped = 90f,
                Levels = new[] { 11f, 22f, 45f, 90f, 180f, 360f }, Scenarios = Core,
                World = (s, x) => s.HeatTimeScale = x,
            });

            knobs.Add(new Knob
            {
                Name = "solar-energy",
                Intent = "watts per square metre from the sun",
                Multiplier = false, Shipped = 1000f,
                Levels = new[] { 0f, 500f, 1000f, 1361f, 2000f }, Scenarios = Sunlit,
                World = (s, x) => s.SolarEnergy = x,
            });

            knobs.Add(new Knob
            {
                Name = "vacuum-temperature",
                Intent = "the floor a hull radiates towards",
                Multiplier = false, Shipped = 2.7f,
                Levels = new[] { 2.7f, 50f, 150f, 250f },
                Scenarios = new[] { "idle", "vacuum-sunlit" },
                World = (s, x) => s.VacuumTemperature = x,
            });

            knobs.Add(new Knob
            {
                Name = "friction-scale",
                Intent = "how hard airflow heats the leading face",
                Multiplier = true, Shipped = 1f,
                Levels = new[] { 0f, 0.5f, 1f, 2f, 4f }, Scenarios = Moving,
                World = (s, x) => s.FrictionScale *= x,
            });

            knobs.Add(new Knob
            {
                Name = "friction-threshold",
                Intent = "the floor below which the whole aero term is off, m/s; 0 ships",
                Multiplier = false, Shipped = 0f,
                Levels = new[] { 0f, 25f, 50f, 100f }, Scenarios = Moving,
                World = (s, x) => s.FrictionAtSpeedsAbove = x,
            });

            return knobs;
        }

        public static readonly string[] Environments =
        {
            "vacuum-shadow", "vacuum-sunlit", "orbit-cycling", "surface-hot-noon",
            "surface-cold-night", "surface-windy", "underground", "storm-parked", "reentry",
        };


        public static List<Configuration> All()
        {

            List<Configuration> configurations = new List<Configuration>();

            foreach (Knob knob in Knobs())
            {
                foreach (float level in knob.Levels)
                {
                    configurations.Add(new Configuration
                    {
                        Knob = knob,
                        Level = level,
                        Scenarios = knob.Scenarios,
                        IsShipped = knob.Multiplier
                            ? Math.Abs(level - 1f) < 1e-6f
                            : Math.Abs(level - knob.Shipped) < 1e-6f,
                    });
                }
            }

            return configurations;
        }
    }
}

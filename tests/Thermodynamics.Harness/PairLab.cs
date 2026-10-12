using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public static class PairLab
    {
        public const float ShippedClock = 225f;

        private static readonly float[][] Grid =
        {
            new[] { 1f, 225f },

            new[] { 1f, 112f }, new[] { 1f, 56f }, new[] { 1f, 25f },
            new[] { 1f, 15f }, new[] { 1f, 11f },

            new[] { 2f, 225f }, new[] { 4f, 225f }, new[] { 8f, 225f },

            new[] { 2f, 112f }, new[] { 4f, 56f }, new[] { 8f, 25f },

            new[] { 4f, 15f },
            new[] { 2f, 15f }, new[] { 8f, 15f }, new[] { 4f, 25f }, new[] { 4f, 11f },

            new[] { 8f, 112f }, new[] { 8f, 90f }, new[] { 8f, 80f }, new[] { 8f, 70f },

            new[] { 4f, 120f }, new[] { 4f, 100f }, new[] { 4f, 90f }, new[] { 4f, 80f },
        };

        public static readonly string[] Scenarios = { "idle", "full-electrical", "recovery" };

        public static readonly string[] LoadScenarios =
        {
            "idle", "full-electrical", "full-electrical-charged", "jump-charge", "recovery",
        };

        public static readonly string[] AirScenarios =
        {
            "vacuum-shadow", "surface-hot-noon", "storm-parked", "reentry",
        };

        private static readonly float[][] AirGrid =
        {
            new[] { 1f, 225f, 1f },
            new[] { 4f, 120f, 1f }, new[] { 4f, 100f, 1f }, new[] { 4f, 90f, 1f }, new[] { 4f, 80f, 1f },
            new[] { 1f, 110f, 0.5f }, new[] { 1f, 100f, 0.5f },
            new[] { 1f, 90f, 0.5f }, new[] { 1f, 80f, 0.5f },
        };

        public class Cell
        {
            public float Conductivity;

            public float Clock;

            public float Waste = 1f;

            public bool IsShipped
            {
                get { return Conductivity == 1f && Clock == ShippedClock && Waste == 1f; }
            }

            public float ProjectedDemandRatio
            {
                get { return Conductivity * Clock / ShippedClock; }
            }

            public string Name
            {
                get
                {
                    string name = "k" + Conductivity.ToString("0.##",
                                      System.Globalization.CultureInfo.InvariantCulture)
                        + "-h" + Clock.ToString("0.##",
                               System.Globalization.CultureInfo.InvariantCulture);

                    if (Waste != 1f)
                    {
                        name += "-w" + Waste.ToString("0.####",
                            System.Globalization.CultureInfo.InvariantCulture);
                    }

                    return name;
                }
            }


            public ThermalSettings Settings()
            {

                ThermalSettings settings = new HistoricalThermalSettings();
                settings.HeatTimeScale = Clock;
                return settings.Derive();
            }


            public Func<string, string, BlockThermalProperties, BlockThermalProperties> Material()
            {
                if (Conductivity == 1f && Waste == 1f) return null;

                float conduction = Conductivity;
                float waste = Waste;
                return (typeId, subtype, source) =>
                {
                    BlockThermalProperties copy = source.Clone();
                    copy.Conductivity *= conduction;

                    copy.ProducerWasteEnergy *= waste;
                    copy.ConsumerWasteEnergy *= waste;
                    return copy;
                };
            }

            public float ClockStretch
            {
                get { return ShippedClock / Clock; }
            }
        }


        public static List<Cell> All()
        {

            return Cells(Grid);
        }

        private static readonly float[][] LoadGrid =
        {
            new[] { 1f, 225f, 1f },

            new[] { 1f, 225f, 2f },
            new[] { 1f, 225f, 0.5f }, new[] { 1f, 225f, 0.25f }, new[] { 1f, 225f, 0.125f },

            new[] { 1f, 120f, 1f }, new[] { 1f, 90f, 1f },

            new[] { 1f, 120f, 0.125f }, new[] { 1f, 90f, 0.125f }, new[] { 1f, 80f, 0.125f },
            new[] { 1f, 90f, 0.25f }, new[] { 1f, 60f, 0.25f },
            new[] { 1f, 90f, 0.5f }, new[] { 1f, 45f, 0.5f },
            new[] { 1f, 90f, 2f },

            new[] { 1f, 110f, 0.5f }, new[] { 1f, 100f, 0.5f }, new[] { 1f, 80f, 0.5f },

            new[] { 4f, 120f, 1f }, new[] { 4f, 100f, 1f },
            new[] { 4f, 90f, 1f }, new[] { 4f, 80f, 1f },
        };


        public static List<Cell> Load()
        {

            return Cells(LoadGrid);
        }


        public static List<Cell> Decision()
        {

            return Cells(AirGrid);
        }


        private static List<Cell> Cells(float[][] grid)
        {

            List<Cell> cells = new List<Cell>();

            for (int i = 0; i < grid.Length; i++)
            {
                Cell cell = new Cell { Conductivity = grid[i][0], Clock = grid[i][1] };
                if (grid[i].Length > 2) cell.Waste = grid[i][2];
                cells.Add(cell);
            }

            return cells;
        }

        public const float Ceiling = 7200f;


        public static Battery.Scenario Stretch(Battery.Scenario scenario, Cell cell)
        {
            return new Battery.Scenario
            {
                Name = scenario.Name,
                Question = scenario.Question,
                Environment = scenario.Environment,
                Load = scenario.Load,
                Seconds = Math.Min(Ceiling, scenario.Seconds * cell.ClockStretch),
            };
        }
    }
}

using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class BuildCostLab
    {
        public class Row
        {
            public string Subtype;
            public bool Large;

            public float Kilograms;
            public int Pcu;
            public float BuildSeconds;
            public float CubicMetres;

            public float KilogramsPerCubicMetre
            {
                get { return CubicMetres <= 0f ? 0f : Kilograms / CubicMetres; }
            }

            public float PcuPerCubicMetre
            {
                get { return CubicMetres <= 0f ? 0f : Pcu / CubicMetres; }
            }

            public float PcuPerBlock
            {
                get { return Pcu; }
            }

            public float SecondsPerKilogram
            {
                get { return Kilograms <= 0f ? 0f : BuildSeconds / Kilograms; }
            }
        }


        public static List<Row> Vanilla(out int dropped)
        {

            List<Row> rows = new List<Row>();
            dropped = 0;

            foreach (KeyValuePair<string, GameBlocks.Definition> entry in GameBlocks.BySubtype())
            {
                GameBlocks.Definition block = entry.Value;

                if (block.Mass <= 0f || block.VolumeCubicMetres <= 0f
                    || block.Pcu <= 0 || block.BuildSeconds <= 0f)
                {
                    dropped++;
                    continue;
                }

                rows.Add(new Row
                {
                    Subtype = block.SubtypeId,
                    Large = block.Large,
                    Kilograms = block.Mass,
                    Pcu = block.Pcu,
                    BuildSeconds = block.BuildSeconds,
                    CubicMetres = block.VolumeCubicMetres
                });
            }

            return rows;
        }


        public static List<Row> Shipped()
        {

            List<Row> rows = new List<Row>();

            foreach (KeyValuePair<string, ShippedBlocks.Definition> entry in ShippedBlocks.All())
            {
                ShippedBlocks.Definition block = entry.Value;
                if (block.Components.Count == 0) continue;

                rows.Add(new Row
                {
                    Subtype = block.Subtype,
                    Large = block.Large,
                    Kilograms = block.Mass,
                    Pcu = block.Pcu,
                    BuildSeconds = block.BuildSeconds,
                    CubicMetres = block.CellCount * block.GridSize * block.GridSize * block.GridSize
                });
            }

            rows.Sort(delegate (Row a, Row b)
            {
                return string.CompareOrdinal(a.Subtype, b.Subtype);
            });

            return rows;
        }


        public static double Percentile(List<float> sortedAscending, float value)
        {
            if (sortedAscending.Count == 0) return 0d;

            int below = 0;
            while (below < sortedAscending.Count && sortedAscending[below] <= value) below++;

            return below / (double)sortedAscending.Count;
        }


        public static List<float> Column(List<Row> rows, Func<Row, float> of)
        {

            List<float> values = new List<float>(rows.Count);
            foreach (Row row in rows)
            {

                float value = of(row);
                if (value > 0f) values.Add(value);
            }

            values.Sort();
            return values;
        }

        public class MassRung
        {
            public float Factor;

            public float Kilograms;

            public float SettledKelvin;

            public float TopSettledKelvin;

            public float SecondsToSettle;
        }


        public static List<MassRung> MassLadder(float[] factors, int radiators = 8,
            float seconds = 3600f)
        {

            List<MassRung> rungs = new List<MassRung>();

            GameBlocks.Definition reactor = null;
            foreach (GameBlocks.Definition definition in GameBlocks.All())
            {
                if (definition.TypeId != "Reactor") continue;
                if (!definition.SubtypeId.StartsWith("Large", StringComparison.Ordinal)) continue;

                if (reactor == null || definition.PowerOutputWatts > reactor.PowerOutputWatts)
                {
                    reactor = definition;
                }
            }

            if (reactor == null) return rungs;

            BlockModel radiator;
            try
            {
                radiator = ShippedBlocks.Model("Gauge_LG_Radiator");
            }
            catch (Exception)
            {
                return rungs;
            }

            foreach (float factor in factors)
            {
                GridBuilder builder = GridBuilder.Large();
                builder.Place(Blueprints.Model(reactor), Vector3I.Zero);
                builder.Last.PowerProducedWatts = reactor.PowerOutputWatts;
                BlockInstance source = builder.Last;

                int height = Math.Max(1, reactor.Size.Y);
                float kilograms = 0f;
                BlockInstance top = null;

                for (int i = 0; i < radiators; i++)
                {
                    builder.Place(radiator, new Vector3I(0, height, 0));
                    builder.Last.Mass = radiator.Mass * factor;
                    kilograms += builder.Last.Mass;
                    top = builder.Last;
                    height += Math.Max(1, radiator.Size.Y);
                }

                ThermalSimulation simulation =
                    builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);

                const float Interval = 1f;
                int perSample = Math.Max(1, (int)(Interval / simulation.Settings.StepSeconds));
                int samples = Math.Max(1, (int)(seconds / Interval));


                List<float> trace = new List<float>(samples);

                List<float> hot = new List<float>(samples);

                for (int i = 0; i < samples; i++)
                {
                    simulation.StepExact(perSample, Worlds.Shadow());

                    ThermalNode reading = simulation.Solver.GetNode(source);
                    trace.Add(reading == null ? 0f : reading.Temperature);

                    ThermalNode far = top == null ? null : simulation.Solver.GetNode(top);
                    hot.Add(far == null ? 0f : far.Temperature);
                }

                float settled = trace[trace.Count - 1];
                float topSettled = hot[hot.Count - 1];

                float reached = float.PositiveInfinity;
                for (int i = 0; i < hot.Count; i++)
                {
                    if (Math.Abs(hot[i] - topSettled) > 1f) continue;

                    reached = (i + 1) * Interval;
                    break;
                }

                rungs.Add(new MassRung
                {
                    Factor = factor,
                    Kilograms = kilograms,
                    SettledKelvin = settled,
                    TopSettledKelvin = topSettled,
                    SecondsToSettle = reached
                });
            }

            return rungs;
        }


        public static float Quantile(List<float> sortedAscending, double quantile)
        {
            if (sortedAscending.Count == 0) return 0f;

            int index = (int)Math.Ceiling(quantile * sortedAscending.Count) - 1;
            if (index < 0) index = 0;
            if (index >= sortedAscending.Count) index = sortedAscending.Count - 1;

            return sortedAscending[index];
        }
    }
}

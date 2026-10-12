using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class CoolingLadder
    {
        private static readonly int[] Counts = { 1, 2, 4, 8, 16, 32 };

        private const float Seconds = 14400f;

        public class Row
        {
            public string Block;
            public int Count;

            public float SourceKelvin;

            public float PeakKelvin;

            public float VentedWatts;

            public float Mass;

            public float Saved;

            public float SavedPerTonne
            {
                get { return Mass <= 0f ? 0f : Saved / (Mass / 1000f); }
            }

            public float Marginal;

            public float TopKelvin;
        }


        private static List<KeyValuePair<string, BlockModel>> Candidates()
        {
            List<KeyValuePair<string, BlockModel>> candidates =
                new List<KeyValuePair<string, BlockModel>>();

            BlockModel radiator = null;
            try
            {
                radiator = ShippedBlocks.Model("Gauge_LG_Radiator");
            }
            catch
            {
                radiator = null;
            }

            if (radiator != null)
            {
                candidates.Add(new KeyValuePair<string, BlockModel>("Gauge_LG_Radiator", radiator));
            }

            string[] vanilla =
            {
                "LargeHeatVentBlock",
                "LargeExhaustPipe",
                "LargeBlockLargeThrust",
                "LargeBlockWindTurbine",
                "LadderShaft",
                "LargeBlockArmorBlock",
            };

            Dictionary<string, GameBlocks.Definition> definitions = GameBlocks.BySubtype();

            foreach (string subtype in vanilla)
            {
                GameBlocks.Definition definition;
                if (!definitions.TryGetValue(subtype, out definition))
                {
                    missing.Add(subtype);
                    continue;
                }

                candidates.Add(new KeyValuePair<string, BlockModel>(
                    subtype, Blueprints.Model(definition)));
            }

            return candidates;
        }


        private static readonly List<string> missing = new List<string>();


        private static GameBlocks.Definition BiggestReactor()
        {
            GameBlocks.Definition biggest = null;

            foreach (GameBlocks.Definition definition in GameBlocks.All())
            {
                if (definition.TypeId != "Reactor") continue;
                if (!definition.SubtypeId.StartsWith("Large")) continue;

                if (biggest == null || definition.PowerOutputWatts > biggest.PowerOutputWatts)
                {
                    biggest = definition;
                }
            }

            return biggest;
        }


        private static Row Rung(string name, BlockModel cooler, GameBlocks.Definition reactor,
            int count, float bare)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Blueprints.Model(reactor), Vector3I.Zero);
            builder.Last.PowerProducedWatts = reactor.PowerOutputWatts;
            BlockInstance source = builder.Last;

            int height = Math.Max(1, reactor.Size.Y);
            float mass = 0f;
            BlockInstance top = null;

            for (int i = 0; i < count; i++)
            {
                builder.Place(cooler, new Vector3I(0, height, 0));
                mass += builder.Last.Mass;
                top = builder.Last;
                height += Math.Max(1, cooler.Size.Y);
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            simulation.Solver.CollectDiagnostics = true;
            simulation.StepExact((int)(Seconds / simulation.Settings.StepSeconds), Worlds.Shadow());

            ThermalNode node = simulation.Solver.GetNode(source);
            float peak = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float kelvin = simulation.Solver.Nodes[i].Temperature;
                if (kelvin > peak) peak = kelvin;
            }

            Row row = new Row
            {
                Block = name,
                Count = count,
                SourceKelvin = node == null ? 0f : node.Temperature,
                PeakKelvin = peak,
                VentedWatts = simulation.VentedWatts,
                Mass = mass,
            };

            ThermalNode far = top == null ? null : simulation.Solver.GetNode(top);
            row.TopKelvin = far == null ? 0f : far.Temperature;

            row.Saved = bare - row.SourceKelvin;
            return row;
        }


        public static List<Row> Run(out float bare)
        {

            List<Row> rows = new List<Row>();
            bare = 0f;


            GameBlocks.Definition reactor = BiggestReactor();
            if (reactor == null) return rows;


            Row alone = Rung("(bare reactor)", null, reactor, 0, 0f);
            bare = alone.SourceKelvin;
            alone.Saved = 0f;
            rows.Add(alone);

            foreach (KeyValuePair<string, BlockModel> candidate in Candidates())
            {
                float previous = 0f;

                foreach (int count in Counts)
                {

                    Row row = Rung(candidate.Key, candidate.Value, reactor, count, bare);
                    row.Marginal = row.Saved - previous;
                    previous = row.Saved;
                    rows.Add(row);
                }
            }

            return rows;
        }


        public static string Report()
        {
            if (!GameBlocks.IsInstalled)
            {
                return "COOLING LADDER\n\n  needs a game install; set SE_BIN.\n";
            }

            float bare;

            List<Row> rows = Run(out bare);


            StringBuilder sb = new StringBuilder();
            sb.AppendLine("COOLING LADDER");
            sb.AppendLine();
            sb.Append("  the largest reactor at its plate rating, in shadow, with coolers stacked ")
                .AppendLine("against it");
            sb.Append("  bare, it settles at ").Append(bare.ToString("n0")).AppendLine(" K");
            sb.AppendLine();
            sb.AppendLine("  block                       n   source K   saved K   per tonne   marginal      top K");

            foreach (Row row in rows)
            {
                sb.Append("  ").Append(row.Block.PadRight(26));
                sb.Append(row.Count.ToString().PadLeft(3));
                sb.Append(row.SourceKelvin.ToString("n1").PadLeft(11));

                sb.Append(row.Saved.ToString("n2").PadLeft(10));
                sb.Append(row.SavedPerTonne.ToString("n3").PadLeft(12));
                sb.Append(row.Marginal.ToString("n3").PadLeft(11));
                sb.Append(row.TopKelvin.ToString("n1").PadLeft(11));
                sb.AppendLine();
            }

            if (missing.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  candidates this install does not carry under that name:");
                foreach (string subtype in missing) sb.Append("    ").AppendLine(subtype);
                sb.AppendLine("  Until those are named correctly the comparison is short of contenders.");
            }

            sb.AppendLine();
            sb.Append("  A candidate that beats the radiator is a finding about the radiator. ")
                .AppendLine("A row whose");
            sb.AppendLine("  marginal saving has gone flat is the point past which more of it buys nothing —");
            sb.Append("  and the top K column says which flat it is: hot at the far end means the ")
                .AppendLine("stack");
            sb.AppendLine("  saturated, cold means the heat never reached it.");
            return sb.ToString();
        }
    }
}

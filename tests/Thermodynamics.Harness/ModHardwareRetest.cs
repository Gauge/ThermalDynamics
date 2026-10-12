using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class ModHardwareRetest
    {
        private const float Seconds = 14400f;

        public const float SourceWatts = 75000f;

        public class Row
        {
            public string Rig;

            public int Count;

            public string World;

            public float Conductance;

            public float SourceKelvin;

            public float PeakKelvin;

            public float FarKelvin;
        }


        public static Row RadiatorStack(int count, bool preConversion)
        {

            return RadiatorStack(count, preConversion, SourceWatts);
        }


        public static Row RadiatorStack(int count, bool preConversion, float watts)
        {

            BlockModel radiator = Cooler(Catalog.Radiator, preConversion);

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LargeReactor(), Vector3I.Zero);
            if (watts > 0f) builder.Wasting(watts);
            BlockInstance source = builder.Last;

            int height = 3;
            BlockInstance top = null;

            for (int i = 0; i < count; i++)
            {
                builder.Place(radiator, new Vector3I(0, height, 0));
                top = builder.Last;
                height += radiator.Size.Y;
            }

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            simulation.StepExact((int)(Seconds / simulation.Settings.StepSeconds), Worlds.Shadow());

            return Read("radiator-stack", count, preConversion, radiator.Thermal.Conductivity,
                simulation, source, top);
        }


        public static Row CoolantRing(int width, int depth, bool preConversion)
        {
            List<Vector3I> cells = PipeFitter.RectangleXZ(Vector3I.Zero, width, depth);

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            sinks[2] = Vector3I.Down;

            GridBuilder builder = GridBuilder.Large();

            float conductance;

            using (Pre(preConversion))
            {
                PipeFitter.BuildRing(builder, cells, -1, sinks);
                conductance = Catalog.CoolantThermal().Conductivity;
            }

            builder.Place(Catalog.LargeReactor(), cells[2] + Vector3I.Down * 3)
                .Wasting(SourceWatts);
            BlockInstance source = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new HistoricalThermalSettings(), 293.15f);
            simulation.StepExact((int)(Seconds / simulation.Settings.StepSeconds), Worlds.Shadow());

            BlockInstance far = null;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Block.Position == cells[cells.Count / 2]) far = nodes[i].Block;
            }


            Row row = Read("coolant-ring", cells.Count, preConversion, conductance,
                simulation, source, far);
            return row;
        }

        private static readonly int[] Stacks = { 1, 2, 4, 8, 16, 32 };

        private static readonly int[] Rings = { 4, 6, 8 };


        public static List<Row> Measure()
        {

            List<Row> rows = new List<Row>();

            for (int i = 0; i < Stacks.Length; i++)
            {
                rows.Add(RadiatorStack(Stacks[i], false));
                rows.Add(RadiatorStack(Stacks[i], true));
            }

            for (int i = 0; i < Rings.Length; i++)
            {
                rows.Add(CoolantRing(Rings[i], Rings[i], false));
                rows.Add(CoolantRing(Rings[i], Rings[i], true));
            }

            return rows;
        }



        public static string Table()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            List<ConductanceRetest.Move> moves = ConductanceRetest.Moves();

            text.AppendLine("What the conversion to real W/(m K) did, block by block");
            text.AppendLine();
            text.AppendLine("Before it, every block took one of two conductances: 120 from the 0.6");
            text.AppendLine("fall-through, or 200 for Thrust, Reactor and the mod's own blocks at quality 1.");
            text.AppendLine();
            text.AppendLine(string.Format("{0,-36} {1,8} {2,8} {3,8}",
                "block", "before", "now", "ratio"));

            for (int i = 0; i < moves.Count; i++)
            {
                ConductanceRetest.Move move = moves[i];
                text.AppendLine(string.Format("{0,-36} {1,8:0} {2,8:0.0} {3,8:0.00}x",
                    move.Subtype, move.Before, move.After, move.Ratio));
            }

            List<string> missing = ConductanceRetest.Missing();
            if (missing.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("named here and not in the installed game: "
                    + string.Join(", ", missing.ToArray()));
            }

            else if (moves.Count == 0)
            {
                text.AppendLine("  (no game installed, so nothing could be derived)");
            }

            text.AppendLine();
            return text.ToString();
        }


        public static string Report()
        {

            List<Row> rows = Measure();
            System.Text.StringBuilder text = new System.Text.StringBuilder();

            text.Append(Table());
            text.AppendLine("What the real-unit conversion did to the mod's own cooling hardware");
            text.AppendLine();
            text.AppendLine("One family moves per rig and everything else stays shipped, because the");
            text.AppendLine("composite is already measured on the retest set of workshop hulls — and not");
            text.AppendLine("one of those forty carries a coolant pipe or a radiator, which is why these");
            text.AppendLine("two moves need a rig at all. Source is " + (SourceWatts / 1000f)
                + " kW in shadow, run to " + (Seconds / 3600f) + " h.");
            text.AppendLine();
            text.AppendLine(string.Format("{0,-15} {1,5} {2,10} {3,10} {4,10} {5,10} {6,9}",
                "rig", "n", "W/(m K)", "source K", "peak K", "far K", "saved K"));

            for (int i = 0; i + 1 < rows.Count; i += 2)
            {
                Row shipped = rows[i];
                Row before = rows[i + 1];

                text.AppendLine(string.Format("{0,-15} {1,5} {2,10:0} {3,10:0.0} {4,10:0.0} {5,10:0.0} {6,9}",
                    before.Rig, before.Count, before.Conductance, before.SourceKelvin,
                    before.PeakKelvin, before.FarKelvin, "-"));

                text.AppendLine(string.Format("{0,-15} {1,5} {2,10:0} {3,10:0.0} {4,10:0.0} {5,10:0.0} {6,9:+0.00;-0.00}",
                    shipped.Rig, shipped.Count, shipped.Conductance, shipped.SourceKelvin,
                    shipped.PeakKelvin, shipped.FarKelvin,
                    before.SourceKelvin - shipped.SourceKelvin));
            }

            text.AppendLine();
            text.AppendLine("saved K is the shipped world against the pre-conversion one: positive means");
            text.AppendLine("the conversion left the source cooler.");
            return text.ToString();
        }



        private static BlockModel Cooler(Func<BlockModel> make, bool preConversion)
        {
            using (Pre(preConversion)) return make();
        }


        private static IDisposable Pre(bool preConversion)
        {
            if (!preConversion) return new Restore(null);

            Func<BlockThermalProperties, BlockThermalProperties> previous = Catalog.MaterialOverride;

            Catalog.MaterialOverride = properties =>
            {
                BlockThermalProperties copy = properties.Clone();
                copy.Conductivity = ConductanceRetest.Authored(ConductanceRetest.PreBest);
                return copy;
            };

            return new Restore(previous);
        }

        private class Restore : IDisposable
        {
            private readonly Func<BlockThermalProperties, BlockThermalProperties> previous;


            public Restore(Func<BlockThermalProperties, BlockThermalProperties> previous)
            {
                this.previous = previous;
            }


            public void Dispose()
            {
                Catalog.MaterialOverride = previous;
            }
        }


        private static Row Read(string rig, int count, bool preConversion, float conductance,
            ThermalSimulation simulation, BlockInstance source, BlockInstance far)
        {
            float peak = 0f;
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Temperature > peak) peak = nodes[i].Temperature;
            }

            ThermalNode sourceNode = simulation.Solver.GetNode(source);
            ThermalNode farNode = far == null ? null : simulation.Solver.GetNode(far);

            return new Row
            {
                Rig = rig,
                Count = count,
                World = preConversion ? "pre-units" : "shipped",
                Conductance = conductance * ThermalConstants.ConductionScale,
                SourceKelvin = sourceNode == null ? 0f : sourceNode.Temperature,
                PeakKelvin = peak,
                FarKelvin = farNode == null ? 0f : farNode.Temperature,
            };
        }
    }
}

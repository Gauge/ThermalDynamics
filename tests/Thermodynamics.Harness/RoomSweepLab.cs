using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class RoomSweepLab
    {
        public const int SweepIntervalSteps = 8;

        public class Row
        {
            public string Shape;
            public int Blocks;
            public int Compartments;
            public float BlocksPerCompartment;
            public double MapMilliseconds;
            public double SweepMilliseconds;
            public double MicrosecondsPerRoom;

            public double ShareOfRealTime;
        }


        public static Row Measure(string shape, IEnumerable<Vector3I> cells, int repeats = 20)
        {
            GridBuilder builder = GridBuilder.Large();


            List<Vector3I> ordered = new List<Vector3I>(cells);
            ordered.Sort(delegate (Vector3I a, Vector3I b)
            {
                if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
                if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
                return a.X.CompareTo(b.X);
            });

            BlockModel armour = Catalog.LightArmor();
            foreach (Vector3I cell in ordered) builder.Place(armour, cell);


            ThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableRoomAir = true;
            settings.Derive();

            Stopwatch watch = Stopwatch.StartNew();
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());
            watch.Stop();

            double mapMs = watch.Elapsed.TotalMilliseconds;

            RoomMap rooms = simulation.Rooms.Map;


            List<Vector3I> anchors = new List<Vector3I>();
            for (int i = 0; i < rooms.RoomCount; i++)
            {
                if (rooms.CellsInRoom(i) > 0) anchors.Add(rooms.CellsOf(i)[0]);
            }

            for (int i = 0; i < anchors.Count; i++) simulation.SetRoomPressure(anchors[i], 1f);

            watch.Restart();
            for (int pass = 0; pass < repeats; pass++)
            {
                float level = (pass & 1) == 0 ? 1f : 0.5f;
                for (int i = 0; i < anchors.Count; i++) simulation.SetRoomPressure(anchors[i], level);
            }
            watch.Stop();

            double sweepMs = watch.Elapsed.TotalMilliseconds / repeats;

            int blocks = simulation.Solver.Nodes.Count;
            float stepSeconds = simulation.Settings.StepSeconds;
            double sweepsPerSecond = stepSeconds <= 0f ? 0d : 1d / (stepSeconds * SweepIntervalSteps);

            return new Row
            {
                Shape = shape,
                Blocks = blocks,
                Compartments = anchors.Count,
                BlocksPerCompartment = anchors.Count == 0 ? 0f : blocks / (float)anchors.Count,
                MapMilliseconds = mapMs,
                SweepMilliseconds = sweepMs,
                MicrosecondsPerRoom = anchors.Count == 0 ? 0d : (sweepMs * 1000d) / anchors.Count,
                ShareOfRealTime = sweepMs * sweepsPerSecond / 1000d,
            };
        }


        public static List<Row> Ladder()
        {

            List<Row> rows = new List<Row>();

            rows.Add(Measure("ship 40x9", GridShapes.Ship(40, 9, 12)));

            Vector3I[] sizes =
            {

                new Vector3I(17, 15, 19),

                new Vector3I(21, 19, 23),

                new Vector3I(27, 23, 27),

                new Vector3I(35, 31, 35),

                new Vector3I(43, 39, 43),
            };

            for (int i = 0; i < sizes.Length; i++)
            {
                rows.Add(Measure("station " + sizes[i].X + "x" + sizes[i].Y + "x" + sizes[i].Z,
                    GridShapes.Station(sizes[i], new Vector3I(3, 3, 3))));
            }

            return rows;
        }


        public static string Report()
        {

            List<Row> rows = Ladder();


            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ROOM SWEEP AT STATION SCALE  (A4)");
            sb.AppendLine();
            sb.AppendLine("The two host calls a room costs are not measured here — they are the");
            sb.AppendLine("game's, and priced in a session. What is measured is how many rooms");
            sb.AppendLine("multiply them, and the core per-room work the sweep does itself.");
            sb.AppendLine();
            sb.AppendFormat("{0,-20}{1,9}{2,7}{3,10}{4,10}{5,11}{6,11}\n",
                "shape", "blocks", "rooms", "blocks/rm", "map ms", "sweep us", "% real");

            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                sb.AppendFormat("{0,-20}{1,9}{2,7}{3,10}{4,10}{5,11}{6,11}\n",
                    r.Shape,
                    r.Blocks.ToString("n0"),
                    r.Compartments.ToString("n0"),
                    r.BlocksPerCompartment.ToString("n1"),
                    r.MapMilliseconds.ToString("n1"),
                    (r.SweepMilliseconds * 1000d).ToString("n1"),
                    (100d * r.ShareOfRealTime).ToString("n4"));
            }

            sb.AppendLine();
            sb.AppendLine("A sweep runs every " + SweepIntervalSteps + " solver steps and costs two");
            sb.AppendLine("host calls a room, so the last column is the core half only.");

            return sb.ToString();
        }
    }
}

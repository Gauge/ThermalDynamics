using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **A room's air is a function of the room, not of the path the flood took through it.**
    ///
    /// <para>
    /// `BuildRoomLinks` walks a room's cells and counts the faces each bounding block presents to
    /// it in a `Dictionary&lt;int, int&gt;`, which enumerates by insertion — so the links came out in
    /// the order the flood happened to reach the cells, and the mean wall temperature a new room's
    /// air starts at was a sum of floats in that same order. Two floods that agree exactly about
    /// which cells are in a room would then disagree in the last bits about how warm its air is.
    /// </para>
    ///
    /// <para>
    /// **This is the property a faster flood needs.** A flood that enqueued runs of cells rather
    /// than single cells reaches a room's cells in a different order, and is worth having
    /// (performance.md, *what is designed and not built*). It cannot be written while the answer
    /// depends on that order. Sorting the contacts by node index makes both the links and the sum a
    /// function of the room's contents; these tests are what says so.
    /// </para>
    /// </summary>
    public class RoomAirCanonicalTests
    {

        private static ThermalSimulation SealedBox()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-3, -3, -3), new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings().Derive());

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            Assert.True(nodes.Count > 100, "the shell built " + nodes.Count + " nodes, too few to sum badly");

            uint state = 0x1234567u;
            for (int i = 0; i < nodes.Count; i++)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                nodes[i].Temperature = 150f + (state % 100000u) * 0.004f;
            }

            return simulation;
        }


        private static RoomMap Reordered(ThermalSimulation simulation, bool reversed)
        {
            RoomMap source = simulation.Rooms.Map;

            RoomMap map = new RoomMap();
            map.SetSearchBounds(simulation.Grid.Min - Vector3I.One, simulation.Grid.Max + new Vector3I(2, 2, 2));

            for (int r = 0; r < source.RoomCount; r++)
            {
                RoomMap.RoomCells cells = source.CellsOf(r);
                int room = map.BeginRoom();

                for (int i = 0; i < cells.Count; i++)
                {
                    map.AddToRoom(room, cells[reversed ? cells.Count - 1 - i : i]);
                }
            }

            map.DropEmptyRooms();
            map.RefreshVenting();
            return map;
        }

        [Fact]

        public void ARoomsAirIsTheSameWhicheverOrderItsCellsArriveIn()
        {

            ThermalSimulation forwardSim = SealedBox();

            ThermalSimulation reverseSim = SealedBox();


            RoomMap forwardMap = Reordered(forwardSim, false);

            RoomMap reverseMap = Reordered(reverseSim, true);

            Assert.True(forwardMap.RoomCount > 0, "no rooms were mapped, so nothing is compared");
            RoomMap.RoomCells a = forwardMap.CellsOf(0);
            RoomMap.RoomCells b = reverseMap.CellsOf(0);
            Assert.True(a.Count > 1, "the first room has one cell, so reversing it changes nothing");
            Assert.NotEqual(a[0], b[0]);
            Assert.Equal(a[0], b[b.Count - 1]);

            Pressurise(forwardSim, forwardMap);
            Pressurise(reverseSim, reverseMap);

            IList<RoomAirNode> forward = forwardSim.Solver.RoomAir;
            IList<RoomAirNode> reverse = reverseSim.Solver.RoomAir;

            Assert.True(forward.Count > 0, "the sealed box produced no room air, so nothing is compared");
            Assert.Equal(forward.Count, reverse.Count);

            int linksJudged = 0;
            for (int i = 0; i < forward.Count; i++)
            {
                RoomAirNode f = forward[i];
                RoomAirNode r = reverse[i];

                Assert.Equal(f.Anchor, r.Anchor);
                Assert.Equal(f.CellCount, r.CellCount);
                Assert.Equal(Bits(f.Volume), Bits(r.Volume));

                Assert.True(Bits(f.Temperature) == Bits(r.Temperature),
                    "room " + i + " starts at " + f.Temperature.ToString("R") + " forwards and "
                    + r.Temperature.ToString("R") + " backwards, so the mean over its walls is being"
                    + " summed in the order the flood reached its cells");

                Assert.Equal(f.Links.Count, r.Links.Count);
                Assert.True(f.Links.Count > 1, "room " + i + " has " + f.Links.Count + " links");

                for (int l = 0; l < f.Links.Count; l++)
                {
                    Assert.True(f.Links[l].NodeIndex == r.Links[l].NodeIndex,
                        "room " + i + " link " + l + " is node " + f.Links[l].NodeIndex
                        + " forwards and " + r.Links[l].NodeIndex + " backwards");
                    Assert.Equal(Bits(f.Links[l].Conductance), Bits(r.Links[l].Conductance));
                    linksJudged++;
                }
            }

            Assert.True(linksJudged > 10, "only " + linksJudged + " links were compared");
        }

        [Fact]

        public void EveryRoomsLinksAreInNodeOrder()
        {

            ThermalSimulation simulation = SealedBox();
            Pressurise(simulation, Reordered(simulation, false));

            IList<RoomAirNode> air = simulation.Solver.RoomAir;
            Assert.True(air.Count > 0, "no room air, so nothing is checked");

            int judged = 0;
            for (int i = 0; i < air.Count; i++)
            {
                IList<RoomLink> links = air[i].Links;
                for (int l = 1; l < links.Count; l++)
                {
                    Assert.True(links[l - 1].NodeIndex < links[l].NodeIndex,
                        "room " + i + " has node " + links[l - 1].NodeIndex + " before "
                        + links[l].NodeIndex + ", so its links are not in node order");
                    judged++;
                }
            }

            Assert.True(judged > 10, "only " + judged + " neighbouring pairs of links were compared");
        }


        private static void Pressurise(ThermalSimulation simulation, RoomMap map)
        {
            simulation.Solver.RebuildRoomAir(map);

            int filled = 0;
            for (int r = 0; r < map.RoomCount; r++)
            {
                if (map.CellsInRoom(r) == 0) continue;
                if (simulation.Solver.SetRoomPressure(map, map.CellsOf(r)[0], 1f)) filled++;
            }

            Assert.True(filled > 0, "no room took air, so its links were never built");
        }

        [Fact]

        public void BuildingARoomsAirTwiceGivesTheSameAnswer()
        {

            ThermalSimulation simulation = SealedBox();

            RoomMap map = Reordered(simulation, false);

            Pressurise(simulation, map);

            List<float> first = Conductances(simulation);

            Pressurise(simulation, map);

            List<float> second = Conductances(simulation);

            Assert.True(first.Count > 10, "only " + first.Count + " links were compared");
            Assert.Equal(first.Count, second.Count);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.True(Bits(first[i]) == Bits(second[i]),
                    "link " + i + " is " + first[i].ToString("R") + " on the first build and "
                    + second[i].ToString("R") + " on the second, so the face counters carried"
                    + " something between builds");
            }
        }


        private static List<float> Conductances(ThermalSimulation simulation)
        {

            List<float> all = new List<float>();
            IList<RoomAirNode> air = simulation.Solver.RoomAir;

            for (int a = 0; a < air.Count; a++)
            {
                IList<RoomLink> links = air[a].Links;
                for (int l = 0; l < links.Count; l++) all.Add(links[l].Conductance);
            }

            return all;
        }


        private static int Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Walking the external air a run at a time classifies the same cells as walking it a cell at
    /// a time.**
    ///
    /// <para>
    /// Most of a room-mapping pass is the open space around the hull: 5.1 million of the 7.2 million
    /// cells visited on a 505,566-block hull, in runs averaging 84 cells. The run walk takes a whole
    /// run of them per dequeue (performance.md, Pass 4, Iteration 7). It is a different algorithm,
    /// not a tuning of the old one — a scanline fill against a breadth-first one — so what says it
    /// is right is that it produces the same map, on hulls with real compartments, doors and hollows.
    /// </para>
    ///
    /// <para>
    /// The cell walk stays as `SpanFlood = false` for exactly this reason: it is the simpler
    /// statement of what a flood is, and it is the oracle here (`E7`, `D3`).
    /// </para>
    /// </summary>
    public class RoomSpanFloodTests
    {

        private static ThermalSimulation Census(bool spans, int blocks)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));


            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            simulation.Rooms.SpanFlood = spans;
            for (int i = 0; i < builder.Placed.Count; i++) simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            simulation.RebuildAll();
            return simulation;
        }


        private static ThermalSimulation Compartments(bool spans)
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();

            builder.Shell(armour, new Vector3I(0, 0, 0), new Vector3I(5, 5, 5));
            builder.Shell(armour, new Vector3I(7, 0, 0), new Vector3I(14, 4, 4));

            builder.Remove(new Vector3I(9, 3, 2));


            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            simulation.Rooms.SpanFlood = spans;
            for (int i = 0; i < builder.Placed.Count; i++) simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            simulation.RebuildAll();
            return simulation;
        }


        private static void AssertSameClassification(ThermalSimulation cells, ThermalSimulation spans, string what)
        {
            RoomMap a = cells.Rooms.Map;
            RoomMap b = spans.Rooms.Map;

            Assert.True(a.ExternalCellCount > 0, what + ": the cell walk found no external air, so agreement proves nothing");
            Assert.True(a.RoomCount > 0, what + ": the cell walk found no rooms, so agreement proves nothing");

            Assert.True(a.ExternalCellCount == b.ExternalCellCount,
                what + ": " + a.ExternalCellCount + " external cells by the cell walk and "
                + b.ExternalCellCount + " by the run walk");
            Assert.Equal(a.SolidCellCount, b.SolidCellCount);
            Assert.Equal(a.RoomCellCount, b.RoomCellCount);
            Assert.Equal(a.RoomCount, b.RoomCount);

            int judged = 0;
            for (int r = 0; r < a.RoomCount; r++)
            {
                RoomMap.RoomCells room = a.CellsOf(r);
                Assert.Equal(room.Count, b.CellsInRoom(r));

                for (int i = 0; i < room.Count; i++)
                {
                    Assert.True(b.RoomIndexOf(room[i]) == r,
                        what + ": cell " + room[i] + " is in room " + r + " by the cell walk and room "
                        + b.RoomIndexOf(room[i]) + " by the run walk");
                    Assert.False(b.IsExternal(room[i]));
                    judged++;
                }
            }

            Assert.True(judged > 0, what + ": no room cell was compared");
        }

        [Fact]

        public void TheRunWalkAndTheCellWalkAgreeOnCompartments()
        {
            AssertSameClassification(Compartments(false), Compartments(true), "compartments");
        }

        [Fact]

        public void TheRunWalkAndTheCellWalkAgreeOnACensusHull()
        {
            AssertSameClassification(Census(false, 8000), Census(true, 8000), "census hull");
        }

        [Fact]

        public void EveryCellOfTheBoxIsClassifiedTheSameWay()
        {

            ThermalSimulation cells = Compartments(false);

            ThermalSimulation spans = Compartments(true);

            GridModel grid = cells.Grid;
            Vector3I min = grid.Min - Vector3I.One;

            Vector3I maxExclusive = grid.Max + new Vector3I(2, 2, 2);

            RoomMap a = cells.Rooms.Map;
            RoomMap b = spans.Rooms.Map;

            int external = 0;
            int solid = 0;
            int inRoom = 0;

            for (int z = min.Z; z < maxExclusive.Z; z++)
            {
                for (int y = min.Y; y < maxExclusive.Y; y++)
                {
                    for (int x = min.X; x < maxExclusive.X; x++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);

                        Assert.True(a.IsSolid(cell) == b.IsSolid(cell), "solid disagreement at " + cell);
                        Assert.True(a.IsExternal(cell) == b.IsExternal(cell), "external disagreement at " + cell);
                        Assert.True(a.RoomIndexOf(cell) == b.RoomIndexOf(cell), "room disagreement at " + cell);

                        if (a.IsSolid(cell)) solid++;
                        else if (a.RoomIndexOf(cell) >= 0) inRoom++;
                        else external++;
                    }
                }
            }

            Assert.True(solid > 0 && inRoom > 0 && external > 0,
                "the box held " + solid + " solid, " + inRoom + " room and " + external
                + " external cells, so at least one classification was never judged");
        }
    }
}

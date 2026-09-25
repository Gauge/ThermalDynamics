using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every room's cells live in one array, and the things that makes safe.
    ///
    /// <para>
    /// The map used to hold each room as a `HashSet&lt;Vector3I&gt;` and pay about seventeen bytes
    /// a cell over a list for it. Nothing searched a room — the map answers "which room is this
    /// cell in" from a sorted index that answers for every room at once — so the set was buying
    /// deduplication and a lookup, and neither was wanted. On a 500,000-block hull that was 39 MB.
    /// Then the per-room lists became ranges into one store, so a rebuild sized from the pass
    /// before it copies nothing (performance.md, Pass 4, Iteration 2).
    /// </para>
    ///
    /// <para>
    /// Neither a list nor an array carries a duplicate check, so the property the set was silently
    /// providing has to be asserted instead: the flood offers each cell exactly once, because every
    /// `AddToRoom` is behind a visited bitset that is tested and set in the same breath. These pin
    /// that, the agreement between a room's own cells and the index that finds them, and the two
    /// properties one store depends on — that a room's range is contiguous and that a completed
    /// pass carries no slack — on hulls with real compartments rather than on a shape built to
    /// have one.
    /// </para>
    /// </summary>
    public class RoomCellStorageTests
    {

        private static ThermalSimulation CompartmentedHull(int blocks)
        {
            ThermalSettings settings = Hulls.Uncapped();
            ThermalSimulation simulation = Hulls.Driven(settings, blocks);

            Assert.True(simulation.Rooms.Map.RoomCount > 0,
                "the hull mapped no rooms, so nothing below is being checked");

            return simulation;
        }

        [Fact]

        public void NoRoomCarriesACellTwice()
        {

            RoomMap map = CompartmentedHull(4000).Rooms.Map;


            HashSet<Vector3I> seen = new HashSet<Vector3I>(Vector3I.Comparer);

            for (int r = 0; r < map.RoomCount; r++)
            {
                RoomMap.RoomCells cells = map.CellsOf(r);
                for (int i = 0; i < cells.Count; i++)
                {
                    Assert.True(seen.Add(cells[i]),
                        "cell " + cells[i] + " appears more than once across the room ranges,"
                        + " so an array without a duplicate check is the wrong place to hold them");
                }
            }

            Assert.Equal(map.RoomCellCount, seen.Count);
        }

        [Fact]

        public void TheIndexAndTheRoomsDescribeTheSameCells()
        {

            RoomMap map = CompartmentedHull(4000).Rooms.Map;

            int listed = 0;

            for (int r = 0; r < map.RoomCount; r++)
            {
                RoomMap.RoomCells cells = map.CellsOf(r);
                Assert.Equal(cells.Count, map.CellsInRoom(r));
                listed += cells.Count;

                for (int i = 0; i < cells.Count; i++)
                {
                    Assert.Equal(r, map.RoomIndexOf(cells[i]));
                    Assert.False(map.IsExternal(cells[i]));
                    Assert.False(map.IsSolid(cells[i]));
                }
            }

            Assert.Equal(map.RoomCellCount, listed);
        }

        [Fact]

        public void ACompletedPassLeavesNoSpareCapacityInTheCellStore()
        {

            RoomMap map = CompartmentedHull(4000).Rooms.Map;

            Assert.True(map.RoomCellCount > 0, "the hull mapped no room cells, so nothing was checked");
            Assert.Equal(map.RoomCellCount, map.RoomCellCapacity);
        }

        [Fact]

        public void TheRoomsTileTheCellStoreWithoutGapOrOverlap()
        {

            RoomMap map = CompartmentedHull(4000).Rooms.Map;

            int expected = 0;
            for (int r = 0; r < map.RoomCount; r++)
            {
                RoomMap.RoomCells cells = map.CellsOf(r);
                Assert.NotEqual(0, cells.Count);

                for (int i = 0; i < cells.Count; i++)
                {
                    Assert.Equal(r, map.RoomIndexOf(cells[i]));
                }

                expected += cells.Count;
            }

            Assert.Equal(map.RoomCellCount, expected);
        }

        [Fact]

        public void AHintedFillNeitherGrowsNorTrimsItsCellStore()
        {
            const int Cells = 3000;


            long untold = FillAndPublish(0, Cells);

            long told = FillAndPublish(Cells, Cells);

            long oneStore = Cells * 12L;
            Assert.True(untold - told >= oneStore,
                "a fill told its size saved " + (untold - told).ToString("n0")
                + " bytes where one store of " + Cells.ToString("n0") + " cells is "
                + oneStore.ToString("n0") + "; the hint is reaching nothing");
        }


        private static long FillAndPublish(int hint, int cells)
        {

            RoomMap map = new RoomMap();
            map.SetSearchBounds(new Vector3I(0, 0, 0), new Vector3I(20, 20, 20));
            if (hint > 0) map.HintRoomCells(hint);

            long before = GC.GetAllocatedBytesForCurrentThread();

            int room = map.BeginRoom();
            int added = 0;
            for (int z = 0; z < 20 && added < cells; z++)
            {
                for (int y = 0; y < 20 && added < cells; y++)
                {
                    for (int x = 0; x < 20 && added < cells; x++)
                    {
                        map.AddToRoom(room, new Vector3I(x, y, z));
                        added++;
                    }
                }
            }

            map.DropEmptyRooms();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(cells, map.RoomCellCount);
            Assert.Equal(cells, map.RoomCellCapacity);
            return allocated;
        }

        [Fact]

        public void TheMapperSizesARebuildFromThePassBeforeIt()
        {

            ThermalSimulation simulation = Unmapped(4000);

            long before = GC.GetAllocatedBytesForCurrentThread();
            RunPass(simulation);
            long first = GC.GetAllocatedBytesForCurrentThread() - before;

            int cells = simulation.Rooms.Map.RoomCellCount;
            Assert.True(cells > 0, "the hull mapped no room cells, so nothing below is measured");

            before = GC.GetAllocatedBytesForCurrentThread();
            RunPass(simulation);
            long second = GC.GetAllocatedBytesForCurrentThread() - before;

            RoomMap map = simulation.Rooms.Map;
            Assert.Equal(cells, map.RoomCellCount);
            Assert.Equal(map.RoomCellCount, map.RoomCellCapacity);

            long bound = cells * 34L;
            Assert.True(second < bound,
                "the rebuild allocated " + second.ToString("n0") + " bytes for "
                + cells.ToString("n0") + " room cells — " + (second / (double)cells).ToString("n1")
                + " a cell, against a bar of 34 — so it grew into its store and trimmed it back"

                + " rather than being sized from the pass before it (the first pass, buffers and"
                + " all, allocated " + first.ToString("n0") + ")");
        }


        private static ThermalSimulation Unmapped(int blocks)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));


            ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings().Derive(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }

            simulation.Surfaces.Rebuild(simulation.Grid);
            return simulation;
        }


        private static void RunPass(ThermalSimulation simulation)
        {
            simulation.Rooms.RequestRestart(simulation.Grid);
            Assert.True(simulation.Rooms.RunToCompletion(), "the room pass did not finish");
        }

        [Fact]

        public void AClosedRoomCannotBeAddedTo()
        {

            RoomMap map = new RoomMap();
            map.SetSearchBounds(new Vector3I(0, 0, 0), new Vector3I(8, 8, 8));

            int first = map.BeginRoom();
            map.AddToRoom(first, new Vector3I(1, 1, 1));
            int second = map.BeginRoom();
            map.AddToRoom(second, new Vector3I(2, 2, 2));

            Assert.Throws<InvalidOperationException>(delegate
            {
                map.AddToRoom(first, new Vector3I(3, 3, 3));
            });
        }

        [Fact]

        public void SolidRoomAndExternalCellsAccountForTheWholeSearchVolume()
        {

            ThermalSimulation simulation = CompartmentedHull(4000);
            RoomMap map = simulation.Rooms.Map;

            int classified = map.SolidCellCount + map.RoomCellCount + map.ExternalCellCount;

            Assert.Equal(RoomAuditor.SearchVolumeOf(simulation.Grid), classified);
        }
    }
}

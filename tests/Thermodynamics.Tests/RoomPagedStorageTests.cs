using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Sparse room bounds retain exact cell/room/portal answers while allocating touched pages.
    /// Tiny boundary and recycled-map comparisons use the existing dense path as the oracle;
    /// the billion-cell case executes one work unit only, never traversing its empty volume.
    /// </summary>
    public class RoomPagedStorageTests
    {
        [Fact]
        public void PagedBitsMatchDenseWordsRanksAndResetsAtThePageBoundary()
        {
            var dense = new CellBitset();
            var paged = new PagedCellBitset();
            int[] cells = { 0, 1, 63, 64, 4094, 4095, 4096, 4099, 12299 };
            foreach (bool usePages in new[] { false, true, true, false, true })
            {
                Vector3I min = new Vector3I(-3, 2, 5), max = min + new Vector3I(12300, 1, 1);
                dense.Reset(min, max); paged.Reset(min, max, usePages);
                foreach (int index in cells)
                {
                    Assert.False(paged.ContainsIndex(index));
                    Assert.Equal(dense.AddIndex(index), paged.AddIndex(index));
                    Assert.False(paged.AddIndex(index));
                }
                Assert.Equal(dense.Count, paged.Count);
                dense.BuildRanks(); paged.BuildRanks();
                foreach (int index in cells)
                {
                    Assert.Equal(dense.RankOfIndex(index), paged.RankOfIndex(index));
                    Assert.Equal(dense.IndexOf(min + new Vector3I(index, 0, 0)),
                        paged.IndexOf(min + new Vector3I(index, 0, 0)));
                }
                foreach (int index in new[] { -1, 2, 60, 65, 4100, 8191, 8192, 12300, 12351, 12352 })
                    Assert.Equal(dense.RankOfIndex(index), paged.RankOfIndex(index));
                foreach (int start in new[] { -5, 4095, 4100, 8192, 12300, 12351, 12352, 12400 })
                    foreach (int end in new[] { -1, 0, 4100, 12300, 12360, 12500 })
                    {
                        int expectedWords, actualWords;
                        Assert.Equal(dense.NextClearIndex(start, end, out expectedWords),
                            paged.NextClearIndex(start, end, out actualWords));
                        Assert.Equal(expectedWords, actualWords);
                    }
            }
        }

        [Fact]
        public void SparseSealingPagesDiscardOldEntriesWithoutLosingSharedPageCells()
        {
            var snapshot = new RoomSealingSnapshot();
            snapshot.Reset(); snapshot.Write(4095, 63); snapshot.Write(4096, 17);
            Assert.Equal((byte)63, snapshot.Read(4095));
            Assert.Equal((byte)17, snapshot.Read(4096));
            snapshot.Reset(); snapshot.Write(4094, 7); snapshot.Write(4096, 0);
            Assert.Equal((byte)7, snapshot.Read(4094));
            Assert.Equal((byte)0, snapshot.Read(4095));
            Assert.Equal((byte)0, snapshot.Read(4096));
        }

        [Fact]
        public void ShiftedSparsePagesReuseTheirHighWaterPoolInsteadOfRetainingEveryHistoricalKey()
        {
            var snapshot = new RoomSealingSnapshot();
            var bits = new PagedCellBitset();
            Vector3I max = new Vector3I(16 * 4096, 1, 1);
            snapshot.Reset(); bits.Reset(Vector3I.Zero, max, true);
            snapshot.Write(0, 63); snapshot.Write(4096, 17);
            bits.AddIndex(0); bits.AddIndex(4096);
            for (int pass = 1; pass < 16; pass++)
            {
                long index = (long)pass * 4096;
                snapshot.Reset(); bits.Reset(Vector3I.Zero, max, true);
                snapshot.Write(index, 7); bits.AddIndex(index);
                Assert.Equal((byte)7, snapshot.Read(index));
                Assert.Equal((byte)0, snapshot.Read(index - 4096));
                Assert.True(bits.ContainsIndex(index));
                Assert.False(bits.ContainsIndex(index - 4096));
                bits.BuildRanks();
                Assert.Equal(0, bits.RankOfIndex(index));
                Assert.Equal(1, bits.Count);
                Assert.Equal(2, snapshot.RetainedPageCount);
                Assert.Equal(2, bits.RetainedPageCount);
            }
        }

        [Fact]
        public void RecycledSparseAndDenseMapsPublishIdenticalRoomsAndKeepThePriorMapReadable()
        {
            var builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3));
            builder.Place(Catalog.LightArmor(), new Vector3I(31, 0, 0));
            var surfaces = new SurfaceMap(); surfaces.Rebuild(builder.Grid);
            var dense = new RoomMapper(surfaces) { DenseCellLimit = int.MaxValue };
            var paged = new RoomMapper(surfaces);
            for (int pass = 0; pass < 7; pass++)
            {
                paged.DenseCellLimit = pass % 2 == 0 ? 0 : int.MaxValue;
                RoomMap held = paged.Map;
                int heldRooms = held.RoomCount, heldCells = held.RoomCellCount;
                dense.RequestRestart(builder.Grid); paged.RequestRestart(builder.Grid);
                dense.Step(1); paged.Step(1);
                Assert.Same(held, paged.Map);
                Assert.Equal(heldRooms, held.RoomCount);
                Assert.Equal(heldCells, held.RoomCellCount);
                Assert.True(dense.RunToCompletion());
                Assert.True(paged.RunToCompletion());
                Assert.Equal(1, paged.Map.RoomCount);
                Assert.Equal(1, paged.Map.RoomCellCount);
                RoomMapAssert.SameMap(dense.Map, paged.Map, "tiny room, pass " + pass);
            }
        }

        [Fact]
        public void FirstSparseSliceAllocatesTouchedPagesRatherThanTheBoundingVolume()
        {
            var builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1000));
            var surfaces = new SurfaceMap(); surfaces.Rebuild(builder.Grid);
            var mapper = new RoomMapper(surfaces);
            mapper.RequestRestart(builder.Grid);
            RoomMap held = mapper.Map;
            long before = GC.GetAllocatedBytesForCurrentThread();
            mapper.Step(1);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 1, 64 * 1024);
            Assert.True(mapper.HasWorkPending);
            Assert.Equal(0, mapper.CompletedPasses);
            Assert.Same(held, mapper.Map);
            Assert.InRange(mapper.Work.RoomCellsVisited, 0, 1);
        }
    }
}

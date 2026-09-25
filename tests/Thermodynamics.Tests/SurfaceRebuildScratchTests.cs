using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The rebuild's scratch buffers are kept, and a rebuild is the same rebuild for it.**
    ///
    /// <para>
    /// `SurfaceMap.Rebuild` snapshots the table into two `long[cells]` so the derived neighbour half
    /// can be written back without enumerating a dictionary it is mutating. It allocated both every
    /// time — **2 MB on a 126,731-block hull, the whole of what the `surfaces` stage allocated** —
    /// and a rebuild runs on every structural change. They are scratch, so they are kept.
    /// </para>
    ///
    /// <para>
    /// **The buffers outlive the hull that sized them**, which is the one thing that could go wrong:
    /// both loops are bounded by the table's count rather than by the array's length, so a smaller
    /// grid rebuilt into buffers a larger one sized must not read the larger one's leftovers.
    /// </para>
    /// </summary>
    public class SurfaceRebuildScratchTests
    {

        private static GridBuilder Hull(int side, int hole)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(side, side, side));

            if (hole > 0)
            {
                for (int x = 1; x <= hole; x++)
                    for (int y = 1; y <= hole; y++)
                        for (int z = 1; z <= hole; z++)
                            builder.Remove(new Vector3I(x, y, z));
            }

            return builder;
        }


        private static string State(SurfaceMap map, GridModel grid)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            foreach (BlockInstance block in grid.Blocks)
            {
                Vector3I[] cells = block.Cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    text.Append(GridMath.Key(cells[i])).Append(':')
                        .Append(map.GetState(cells[i])).Append(';')
                        .Append(map.GetStructuralState(cells[i])).Append('\n');
                }
            }

            return text.ToString();
        }

        [Fact]

        public void ASecondRebuildProducesTheSameMap()
        {

            GridBuilder source = Hull(8, 4);
            GridModel grid = source.Grid;


            SurfaceMap once = new SurfaceMap();
            once.Rebuild(grid);

            string first = State(once, grid);

            once.Rebuild(grid);
            Assert.Equal(first, State(once, grid));


            SurfaceMap fresh = new SurfaceMap();
            fresh.Rebuild(grid);
            Assert.Equal(first, State(fresh, grid));
        }

        [Fact]

        public void ASmallHullDoesNotReadTheBuffersALargeOneSized()
        {

            SurfaceMap map = new SurfaceMap();


            GridBuilder big = Hull(10, 5);
            map.Rebuild(big.Grid);


            GridBuilder small = Hull(4, 0);
            map.Rebuild(small.Grid);


            SurfaceMap fresh = new SurfaceMap();
            fresh.Rebuild(small.Grid);

            Assert.Equal(State(fresh, small.Grid), State(map, small.Grid));
        }

        [Fact]

        public void ARepeatedRebuildStopsAllocating()
        {

            GridBuilder source = Hull(12, 6);

            SurfaceMap map = new SurfaceMap();

            map.Rebuild(source.Grid);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetAllocatedBytesForCurrentThread();
            map.Rebuild(source.Grid);
            long after = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(after < 4096,
                "a repeated rebuild allocated " + after + " B, so the scratch is not being reused");
        }
    }
}

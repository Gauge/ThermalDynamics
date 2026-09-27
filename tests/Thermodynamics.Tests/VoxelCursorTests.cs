using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Resumable DDA against the unchanged whole-ray traversal, including ties, offsets and long gaps (D8).</summary>
    public class VoxelCursorTests
    {
        [Fact]
        public void ResumingMatchesWholeRaysAcrossSparseTranslatedGeometry()
        {
            Random random = new Random(17319);
            GridModel grid = new GridModel(2.5f);
            Vector3I offset = new Vector3I(11, -23, 8);
            for (int x = -4; x <= 4; x++)
                for (int y = -4; y <= 4; y++)
                    for (int z = -4; z <= 4; z++)
                        if (random.Next(4) == 0) grid.Add(new BlockModel(), offset + new Vector3I(x, y, z));
            int hits = 0, misses = 0;
            for (int i = 0; i < 3000; i++)
            {
                Vector3D origin = (Vector3D)offset + new Vector3D(random.Next(-8, 9), random.Next(-8, 9), random.Next(-8, 9));
                Vector3D direction = new Vector3D(random.Next(-2, 3), random.Next(-2, 3), random.Next(-2, 3));
                double start = (i % 4) * 0.5d;
                bool expected = VoxelWalk.Blocked(grid, origin, direction, start);
                VoxelWalk.Cursor cursor = new VoxelWalk.Cursor();
                cursor.Start(grid, origin, direction, grid.Min, grid.Max, start);
                for (int step = 0; step < 1000 && !cursor.Complete; step++) cursor.Step();
                Assert.True(cursor.Complete);
                Assert.Equal(expected, cursor.Blocked);
                cursor.Step();
                Assert.Equal(expected, cursor.Blocked);
                if (expected) hits++; else misses++;
            }
            Assert.True(hits > 100 && misses > 100);
        }

        [Fact]
        public void ALongSparseRayConsumesOneCellAtATime()
        {
            GridModel grid = new GridModel(1f);
            grid.Add(new BlockModel(), Vector3I.Zero);
            grid.Add(new BlockModel(), new Vector3I(1000, 0, 0));
            VoxelWalk.Cursor cursor = new VoxelWalk.Cursor();
            cursor.Start(grid, new Vector3D(1, 0, 0), Vector3D.UnitX, grid.Min, grid.Max);
            for (int i = 0; i < 999; i++)
            {
                cursor.Step();
                Assert.False(cursor.Complete);
            }
            cursor.Step();
            Assert.True(cursor.Complete);
            Assert.True(cursor.Blocked);
            cursor.Start(null, Vector3D.Zero, Vector3D.UnitX, Vector3I.Zero, Vector3I.Zero);
            Assert.True(cursor.Complete);
            Assert.False(cursor.Blocked);
        }
    }
}

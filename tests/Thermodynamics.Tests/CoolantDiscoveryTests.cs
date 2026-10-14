using System;
using System.Collections.Generic;
using System.Linq;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Discovery must retain the old ring order, pump direction and per-direction diagnostics,
    /// while open chains and broken rings allocate linearly instead of retracing every suffix.
    /// The frozen pre-optimization builder is the oracle, including for custom plumbing.
    /// </summary>
    public class CoolantDiscoveryTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(27)]
        [InlineData(12345)]
        public void MixedDirectionsAndDisconnectedComponentsMatchTheOriginal(int seed)
        {
            GridBuilder builder = Chain(90, seed);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(-1, 0, 0));
            // One end is blocked; the other encounters a pipe facing the wrong direction.
            builder.Place(Catalog.CoolantPipeStraight(), new Vector3I(90, 0, 0));
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(new Vector3I(0, 4, 0), 9, 6));
            PipeFitter.BuildPumplessRing(builder, PipeFitter.RectangleXZ(new Vector3I(0, 8, 0), 5, 4));
            builder.ReorderPlacement(seed);
            Compare(builder.Grid);
        }

        [Fact]
        public void EveryBreakAndRepairOfARingMatchesTheOriginal()
        {
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring = PipeFitter.BuildRing(builder,
                PipeFitter.RectangleXZ(Vector3I.Zero, 13, 9));
            for (int i = 0; i < ring.Count; i++)
            {
                builder.Grid.Remove(ring[i]);
                Compare(builder.Grid);
                builder.Grid.Add(ring[i]);
                Compare(builder.Grid);
            }
        }

        [Fact]
        public void MultiCellPumpsKeepTheirOrderAndDirection()
        {
            GridBuilder builder = GridBuilder.Large();
            // Force cached discovery before reaching the multi-cell ring.
            builder.Place(Catalog.CoolantPipeStraight(), new Vector3I(10, 0, 0));
            List<Vector3I> path = PipeFitter.RectangleXZ(Vector3I.Zero, 2, 5);
            BlockModel pump = Catalog.CoolantPumpLong();
            builder.Place(pump, new Vector3I(0, 0, 1));
            foreach (Vector3I cell in path)
            {
                if (cell.X == 0 && cell.Z >= 1 && cell.Z <= 3) continue;
                int i = path.IndexOf(cell);
                Vector3I a = path[(i + path.Count - 1) % path.Count] - cell;
                Vector3I b = path[(i + 1) % path.Count] - cell;
                BlockModel model = a == -b ? Catalog.CoolantPipeStraight() : Catalog.CoolantPipeCorner();
                builder.Place(model, cell, PipeFitter.Orient(model, a, b));
            }
            Assert.Single(CoolantLoopBuilder.FindLoops(builder.Grid, LoopThermalProperties.Default(), 300f));
            Compare(builder.Grid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void CustomAndDegeneratePortsRetainGeneralTraversal(int shape)
        {
            GridBuilder builder = Chain(9, 42);
            CoolantPort[] ports;
            switch (shape)
            {
                case 0: ports = new CoolantPort[0]; break;
                case 1: ports = new[] { new CoolantPort(Vector3I.Left) }; break;
                case 2: ports = new[] { new CoolantPort(Vector3I.Left), new CoolantPort(Vector3I.Left) }; break;
                case 3: ports = new[] { new CoolantPort(Vector3I.Left), new CoolantPort(Vector3I.Right), new CoolantPort(Vector3I.Up) }; break;
                default: ports = new[] { new CoolantPort(Vector3I.Left), new CoolantPort(Vector3I.Zero) }; break;
            }
            builder.Place(new BlockModel { Name = "Custom", Coolant = new CoolantShape { LinkPorts = ports } },
                new Vector3I(9, 0, 0));
            Compare(builder.Grid);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void OpenDiscoveryAllocationGrowsLinearly(bool brokenRing, bool diagnose)
        {
            GridModel small = OpenGrid(128, brokenRing);
            GridModel large = OpenGrid(1024, brokenRing);
            long before = Allocated(small, diagnose, true);
            long smallBytes = Allocated(small, diagnose, false);
            long largeBytes = Allocated(large, diagnose, false);
            Assert.True(smallBytes < before / 4, "The new search must remove most of the old allocation.");
            Assert.True(largeBytes < smallBytes * 10,
                "An eightfold chain length must stay near linear allocation: " + smallBytes + " -> " + largeBytes);
        }

        private static GridModel OpenGrid(int size, bool brokenRing)
        {
            if (!brokenRing) return Chain(size, 7).Grid;
            GridBuilder builder = GridBuilder.Large();
            List<BlockInstance> ring = PipeFitter.BuildRing(builder,
                PipeFitter.RectangleXZ(Vector3I.Zero, size / 2, 3));
            builder.Grid.Remove(ring[ring.Count / 2]);
            return builder.Grid;
        }

        private static long Allocated(GridModel grid, bool diagnose, bool legacy)
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();
            long before = GC.GetAllocatedBytesForCurrentThread();
            CoolantLoopDiagnostics diagnostics = diagnose ? new CoolantLoopDiagnostics() : null;
            List<CoolantLoop> loops = legacy
                ? LegacyCoolantLoopBuilder.FindLoops(grid, properties, 300f, null, diagnostics)
                : CoolantLoopBuilder.FindLoops(grid, properties, 300f, null, diagnostics);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Empty(loops);
            if (diagnose) Assert.Equal(grid.CoolantBlockCount, diagnostics.PipesAdrift);
            return bytes;
        }

        private static GridBuilder Chain(int size, int seed)
        {
            Random random = new Random(seed);
            GridBuilder builder = GridBuilder.Large();
            BlockModel model = Catalog.CoolantPipeStraight();
            for (int i = 0; i < size; i++)
            {
                Vector3I inlet = random.Next(2) == 0 ? Vector3I.Left : Vector3I.Right;
                builder.Place(model, new Vector3I(i, 0, 0), PipeFitter.OrientDirected(model, inlet, -inlet));
            }
            return builder;
        }

        private static void Compare(GridModel grid)
        {
            LoopThermalProperties properties = LoopThermalProperties.Default();
            CoolantLoopDiagnostics expected = new CoolantLoopDiagnostics();
            CoolantLoopDiagnostics actual = new CoolantLoopDiagnostics();
            List<CoolantLoop> oldLoops = LegacyCoolantLoopBuilder.FindLoops(grid, properties, 300f, null, expected);
            List<CoolantLoop> newLoops = CoolantLoopBuilder.FindLoops(grid, properties, 300f, null, actual);
            Assert.Equal(oldLoops.Count, newLoops.Count);
            for (int i = 0; i < oldLoops.Count; i++)
            {
                Assert.Equal(oldLoops[i].Pipes.Select(p => p.Key), newLoops[i].Pipes.Select(p => p.Key));
                Assert.Equal(oldLoops[i].Pumps.Select(p => p.Direction), newLoops[i].Pumps.Select(p => p.Direction));
                Assert.Equal(oldLoops[i].Signature, newLoops[i].Signature);
                Assert.Equal(oldLoops[i].FlowSegmentsPerSecond, newLoops[i].FlowSegmentsPerSecond);
            }
            Assert.Equal(expected.Counts, actual.Counts);
            Assert.Equal(expected.PipesInLoops, actual.PipesInLoops);
            Assert.Equal(expected.PipesAdrift, actual.PipesAdrift);
            Assert.Equal(expected.Examples, actual.Examples);
        }
    }
}

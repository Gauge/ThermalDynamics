using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;
using Xunit;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Tests
{
    /// <summary>Fair scan work, conservative group heat, atomic publication and failure cleanup.</summary>
    public class ThermalVisionRegionScanTests
    {
        [Fact]

        public void AdaptiveScanFinishesSceneThatCannotFitAtTwentyMetresWithoutLosingSamples()
        {

            var input = new List<Region>();
            for (int z = 0; z < 20; z++) for (int y = 0; y < 20; y++) for (int x = 0; x < 20; x++)
            {

                var min = new Vector3D(x * 40 - 400 + .25, y * 40 - 400 + .25, z * 40 - 400 + .25);
                input.Add(new Region(min, min + new Vector3D(.5), 250 + input.Count % 701));
            }

            var scan = new ThermalVisionRegionScan(5, 1536, 2, 5120);
            Assert.True(scan.Start(new List<IEnumerable<Region>> { input }));
            int updates = 0;
            while (scan.Running)
            {
                Assert.Equal(0, scan.Count);
                Assert.InRange(scan.Advance(128), 1, 128);
                Assert.True(++updates < 2000);
            }
            Assert.Null(scan.Failure);
            Assert.Equal(input.Count, scan.SampleCount);
            Assert.True(scan.CellSize > 20);
            Assert.True(scan.Coarsenings >= 3);
            Assert.InRange(scan.Count, 1, 1536);
            var expected = new Dictionary<Vector3I, float>();
            foreach (Region sample in input)
            {

                var key = new Vector3I((int)Math.Floor(sample.Min.X / scan.CellSize),
                    (int)Math.Floor(sample.Min.Y / scan.CellSize), (int)Math.Floor(sample.Min.Z / scan.CellSize));
                float old;
                expected[key] = expected.TryGetValue(key, out old) ? Math.Max(old, sample.Kelvin) : sample.Kelvin;
            }
            Assert.Equal(expected.Count, scan.Count);
            for (int i = 0; i < scan.Count; i++)
            {

                var key = new Vector3I((int)Math.Round(scan[i].Min.X / scan.CellSize),
                    (int)Math.Round(scan[i].Min.Y / scan.CellSize), (int)Math.Round(scan[i].Min.Z / scan.CellSize));
                Assert.Equal(expected[key], scan[i].Kelvin);
            }
        }

        [Fact]

        public void DenseMultiGridScanCompletesWithoutPrefixStarvationOrPartialPublication()
        {
            const int grids = 113, samplesPerGrid = 2348;
            var sources = new List<IEnumerable<Region>>();
            for (int grid = 0; grid < grids; grid++) sources.Add(DenseGrid(grid, samplesPerGrid));

            var scan = new ThermalVisionRegionScan(1, 2048, 128);
            Assert.True(scan.Start(sources));
            int work = 0, updates = 0;
            while (scan.Running)
            {
                Assert.Equal(0, scan.Count);
                work += scan.Advance(4096);
                Assert.True(++updates < 200);
            }
            Assert.Equal(grids * samplesPerGrid, scan.SampleCount);
            Assert.Equal(grids * 16, scan.Count);
            Assert.Equal(grids * samplesPerGrid * 2 + grids + grids * 16 + 1, work);
            Assert.Equal(131, updates);
            bool lastGridPresent = false;
            for (int i = 0; i < scan.Count; i++) lastGridPresent |= scan[i].Min.X == grids - 1;
            Assert.True(lastGridPresent);
        }


        private static IEnumerable<Region> DenseGrid(int grid, int count)
        {
            for (int i = 0; i < count; i++)
            {

                var min = new Vector3D(grid, i % 16, 0);
                yield return new Region(min, min + new Vector3D(.5), 300 + i % 250);
            }
        }

        [Fact]

        public void LaterSourcesAdvanceEvenWhileFirstSourceRasterizesLargeBounds()
        {
            int firstRead = 0, lastRead = 0;

            var scan = new ThermalVisionRegionScan(1, 256, 2);
            Assert.True(scan.Start(new List<IEnumerable<Region>> {
                Counted(new Region(Vector3D.Zero, new Vector3D(100, 1, 1), 300), () => firstRead++),
                Counted(new Region(new Vector3D(-1), Vector3D.Zero, 600), () => lastRead++) }));
            Assert.Equal(2, scan.Advance(2));
            Assert.Equal(1, firstRead);
            Assert.Equal(1, lastRead);
            Assert.Equal(0, scan.Count);
            while (scan.Running) Assert.InRange(scan.Advance(3), 1, 3);
            Assert.Equal(101, scan.Count);
            Assert.Equal(1, scan.Generation);
        }

        [Fact]

        public void CompletedSnapshotsPreserveHotspotsAndHalfOpenNegativeCellBounds()
        {

            var scan = new ThermalVisionRegionScan(2, 32, 2);
            Assert.True(scan.Start(new List<IEnumerable<Region>> {

                new[] { new Region(new Vector3D(-2), Vector3D.Zero, 300),

                    new Region(new Vector3D(-1.5), new Vector3D(-.5), 600) } }));
            while (scan.Running) scan.Advance(1);
            Assert.Equal(1, scan.Count);
            Assert.Equal(new Vector3D(-2), scan[0].Min);
            Assert.Equal(Vector3D.Zero, scan[0].Max);
            Assert.Equal(600f, scan[0].Kelvin);
            Assert.True(scan.Start(new List<IEnumerable<Region>> { new[] { new Region(Vector3D.Zero, new Vector3D(2), 400) } }));
            Assert.Equal(600f, scan[0].Kelvin);
            scan.Advance(4);
            Assert.Equal(600f, scan[0].Kelvin);
            scan.Advance(1);
            Assert.Equal(400f, scan[0].Kelvin);
            Assert.Equal(2, scan.Generation);
        }

        [Fact]

        public void CapacityOrSourceFailureDoesNotReplaceLastCompleteSnapshot()
        {

            var scan = new ThermalVisionRegionScan(1, 1, 2);
            Assert.True(scan.Start(new List<IEnumerable<Region>> { new[] { new Region(Vector3D.Zero, Vector3D.One, 300) } }));
            while (scan.Running) scan.Advance(10);
            Assert.True(scan.Start(new List<IEnumerable<Region>> { new[] { new Region(Vector3D.Zero, new Vector3D(2), 600) } }));
            scan.Advance(20);
            Assert.Equal("cell-limit", scan.Failure);
            Assert.Equal(1, scan.Generation);
            Assert.Equal(300f, scan[0].Kelvin);
            int disposed = 0;
            Assert.True(scan.Start(new List<IEnumerable<Region>> { Failing(() => disposed++) }));
            scan.Advance(20);
            Assert.Equal("source-or-raster-error", scan.Failure);
            Assert.Equal(1, disposed);
            Assert.Equal(300f, scan[0].Kelvin);
            scan.Dispose();
            Assert.Equal(0, scan.Count);
        }


        private static IEnumerable<Region> Counted(Region region, Action read)
        { read(); yield return region; }

        private static IEnumerable<Region> Failing(Action disposed)
        {
            try
            {
                yield return new Region(Vector3D.Zero, Vector3D.One, 600);
                throw new InvalidOperationException("Source mutated");
            }

            finally { disposed(); }
        }
    }
}

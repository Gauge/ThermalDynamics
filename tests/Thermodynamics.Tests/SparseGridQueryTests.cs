using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Three blocks exercise sparse topology, room queries and shadow storage without traversing
    /// empty grid volumes. Dense and sparse shadow answers share the independent ray-box oracle.
    /// </summary>
    public class SparseGridQueryTests
    {
        private static ThermalSimulation ThreeBlocks(int remote)
        {
            var settings = new ThermalSettings { HeatTimeScale = 1f };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var thermal = new BlockThermalProperties { Conductivity = 10f, SpecificHeat = 500f };
            var model = BlockModel.Solid("query", Vector3I.One, 100f, thermal);
            foreach (var cell in new[] { Vector3I.Zero, Vector3I.UnitX, new Vector3I(remote) })
                simulation.AddBlock(new BlockInstance(model, cell, BlockOrientation.Identity), 300f);
            return simulation;
        }

        [Fact]
        public void OptionalDenseOccupancyFollowsSparseAndDenseTopologyChanges()
        {
            var simulation = ThreeBlocks(40);
            var grid = simulation.Grid;
            Assert.True(grid.PreferSparseCellStorage);
            Assert.Null(grid.OccupancyForQueries());
            var neighbours = new List<BlockInstance>();
            var faces = new List<int>();
            grid.GetNeighbours(grid.Blocks[0], neighbours, faces, grid.OccupancyForQueries());
            Assert.Single(neighbours);
            Assert.Same(grid.Blocks[1], neighbours[0]);
            Assert.Equal(Face.Right, faces[0]);

            simulation.RemoveBlock(grid.Blocks[2]);
            Assert.False(grid.PreferSparseCellStorage);
            var occupancy = grid.OccupancyForQueries();
            Assert.NotNull(occupancy);
            Assert.True(occupancy.Contains(Vector3I.UnitX));
            neighbours.Clear(); faces.Clear();
            grid.GetNeighbours(grid.Blocks[0], neighbours, faces, occupancy);
            Assert.Single(neighbours);
            Assert.Same(grid.Blocks[1], neighbours[0]);
        }

        [Fact]
        public void SparseTopologyAndRoomQueriesAllocateByBlocksRatherThanEmptyBounds()
        {
            var simulation = ThreeBlocks(1000);
            var solver = simulation.Solver;
            // A single known room cell isolates contact/exposure queries from room discovery.
            var rooms = new RoomMap();
            rooms.SetSearchBounds(new Vector3I(-1), new Vector3I(1002), true);
            rooms.BeginRoom(); rooms.AddToRoom(0, new Vector3I(0, 1, 0));
            rooms.DropEmptyRooms();
            long before = GC.GetAllocatedBytesForCurrentThread();
            solver.RebuildLinks();
            solver.RebuildRoomAir(rooms);
            Assert.True(solver.SetRoomPressure(rooms, new Vector3I(0, 1, 0), 1f));
            solver.RefreshExposureAround(rooms, new[] { 0 });
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 0L, 64L * 1024);
            Assert.Single(solver.Links);
            Assert.Single(solver.RoomAir);
            Assert.Single(solver.RoomAir[0].Links);
            Assert.Equal(0, solver.RoomAir[0].Links[0].NodeIndex);
            Assert.Equal(simulation.Settings.RoomConvectionCoefficient * 6.25f,
                solver.RoomAir[0].Links[0].Conductance);
            var expected = new int[Face.Count];
            simulation.Surfaces.GetExposedFaces(simulation.Grid.Blocks[0], rooms, expected);
            for (int face = 0; face < Face.Count; face++)
                Assert.Equal(expected[face], solver.Nodes[0].GetExposedFaces(face));
        }

        [Fact]
        public void SparseDiagnosticNormalsUseOnlyTheLocalNeighbourhood()
        {
            var simulation = ThreeBlocks(1000);
            var grid = simulation.Grid;
            // Warm the constant one-cell direction table before measuring the query itself.
            ShapeNormal.OfGrid(grid, grid.Blocks[0]);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Vector3 normal = ShapeNormal.OfGrid(grid, grid.Blocks[0]);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0L, allocated);
            Assert.Equal(-Vector3.UnitX, normal);
            Assert.Equal(Vector3.Zero, ShapeNormal.OfGrid(grid, grid.Blocks[2]));

            var model = BlockModel.Solid("wide", new Vector3I(2, 1, 1), 100f,
                new BlockThermalProperties { Conductivity = 10f, SpecificHeat = 500f });
            var wide = new BlockInstance(model, new Vector3I(4, 0, 0), BlockOrientation.Identity);
            grid.Add(wide);
            grid.Add(new BlockInstance(grid.Blocks[0].Model, new Vector3I(3, 0, 0), BlockOrientation.Identity));
            Assert.Equal(Vector3.UnitX, ShapeNormal.OfGrid(grid, wide));
        }

        [Fact]
        public void SparseShadowRestartDoesNotAllocateTheBoundingVolume()
        {
            var simulation = ThreeBlocks(1000);
            var map = new SunShadowMap();
            var occluders = new[] { new SunShadowMap.Occluder {
                Model = simulation.Grid, ToOccluder = MatrixD.Identity } };
            long before = GC.GetAllocatedBytesForCurrentThread();
            map.Restart(simulation.Grid, Vector3.UnitX, occluders);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 1L, 64L * 1024);
            Assert.InRange(map.PendingCells, 1, 18);
            Assert.Equal(1, map.OccluderCount);
            Assert.True(map.IsRunning);
            map.Clear();
            Assert.False(map.IsRunning);
            Assert.Equal(0, map.ShadowedCount);
            Assert.Equal(0, map.OccluderCount);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(0)]
        [InlineData(-1)]
        public void MultiCellShadowSkinMatchesTheOccupiedCellOracleAfterRotation(int length)
        {
            var grid = new GridModel(2.5f);
            var thermal = new BlockThermalProperties { Conductivity = 10f, SpecificHeat = 500f };
            var wide = new BlockInstance(BlockModel.Solid("wide", new Vector3I(length, 2, 2), 100f, thermal),
                new Vector3I(-3, -2, -1), new BlockOrientation(Base6Directions.Direction.Up,
                    Base6Directions.Direction.Right));
            grid.Add(wide);
            grid.Add(new BlockInstance(BlockModel.Solid("one", Vector3I.One, 100f, thermal),
                wide.Min - Vector3I.UnitX, BlockOrientation.Identity));

            // Build the expected skin independently by visiting all occupied cells, including
            // internal neighbours and the adjoining one-cell block, in their original order.
            var skin = new HashSet<Vector3I>();
            foreach (var block in grid.Blocks)
                foreach (var cell in block.Cells)
                    for (int face = 0; face < Face.Count; face++)
                    {
                        Vector3I outside = cell + Face.Offsets[face];
                        if (!grid.IsOccupied(outside)) skin.Add(outside);
                    }

            var map = new SunShadowMap();
            foreach (Vector3 sun in new[] { Vector3.Normalize(new Vector3(1f, .3f, -.2f)), -Vector3.UnitX })
            {
                map.Restart(grid, sun);
                Assert.Equal(skin.Count, map.PendingCells);
                while (map.IsRunning) map.Step(2);
                Assert.True(map.IsBuilt);
                int shadowed = 0;
                foreach (Vector3I outside in skin)
                {
                    bool lit = Reference.Lit(grid, outside, sun);
                    Assert.Equal(lit, map.IsLit(outside));
                    if (!lit) shadowed++;
                }
                Assert.Equal(shadowed, map.ShadowedCount);
            }
        }

        [Fact]
        public void ShadowPagesMatchIndependentRaysAcrossClearAndDenseSparseReuse()
        {
            var simulation = ThreeBlocks(40);
            var grid = simulation.Grid;
            var remote = grid.Blocks[2];
            var map = new SunShadowMap();
            for (int pass = 0; pass < 3; pass++)
            {
                if (pass == 1) grid.Remove(remote);
                if (pass == 2) grid.Add(remote);
                Vector3 sun = pass == 1 ? -Vector3.UnitX : Vector3.Normalize(new Vector3(1f, .3f, .1f));
                map.Restart(grid, sun);
                while (map.IsRunning) map.Step(1);
                Assert.True(map.IsBuilt);
                foreach (var block in grid.Blocks)
                {
                    for (int face = 0; face < Face.Count; face++)
                    {
                        Vector3I outside = block.Position + Face.Offsets[face];
                        bool expected = !grid.IsOccupied(outside) && Reference.Lit(grid, outside, sun);
                        Assert.Equal(expected, map.IsFaceLit(block.Position, face));
                    }
                }
                map.Clear();
                Assert.False(map.IsBuilt);
                Assert.Equal(0, map.ShadowedCount);
                Assert.True(map.IsLit(grid.Blocks[0].Position));
            }
        }
    }
}

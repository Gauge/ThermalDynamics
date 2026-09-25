using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The per-cell surface word: which faces seal, which carry a mount, and which of those belong to the neighbour.
    ///
    /// <para>
    /// The neighbour half is derived and must always mirror the other cell's self half, in both
    /// directions, after an edit as well as after a rebuild. Everything the model calls exposed falls
    /// out of these bits, so a stale one is a block radiating through a wall.
    /// </para>
    /// </summary>
    public class SurfaceMapTests
    {

        private static SurfaceMap MapOf(GridModel grid)
        {

            SurfaceMap map = new SurfaceMap();
            map.Rebuild(grid);
            return map;
        }

        [Fact]

        public void ALoneBlockHasSelfBitsAndNoNeighbourBits()
        {

            GridModel grid = new GridModel(2.5f);
            grid.Add(Catalog.LightArmor(), Vector3I.Zero);

            SurfaceMap map = MapOf(grid);

            int state = map.GetState(Vector3I.Zero);
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.True(CellSurface.SelfAirtight(state, face));
                Assert.True(CellSurface.SelfMount(state, face));
                Assert.False(CellSurface.NeighbourAirtight(state, face));
                Assert.False(CellSurface.NeighbourMount(state, face));
            }
        }

        [Fact]

        public void NeighbourBitsMirrorTheOtherCellsSelfBits()
        {

            GridModel grid = new GridModel(2.5f);
            grid.Add(Catalog.LightArmor(), Vector3I.Zero);
            grid.Add(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            SurfaceMap map = MapOf(grid);

            int left = map.GetState(Vector3I.Zero);
            int right = map.GetState(new Vector3I(1, 0, 0));

            Assert.True(CellSurface.NeighbourAirtight(left, Face.Right));
            Assert.True(CellSurface.NeighbourMount(left, Face.Right));
            Assert.True(CellSurface.NeighbourAirtight(right, Face.Left));
            Assert.True(CellSurface.NeighbourMount(right, Face.Left));

            Assert.False(CellSurface.NeighbourAirtight(left, Face.Up));
            Assert.False(CellSurface.NeighbourAirtight(right, Face.Up));
        }

        [Fact]

        public void EveryCellPairIsConsistentInBothDirections()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            builder.Place(Catalog.Grating(), new Vector3I(3, 1, 1));

            SurfaceMap map = MapOf(builder.Grid);

            foreach (Vector3I cell in new List<Vector3I>(map.Cells))
            {
                int state = map.GetState(cell);
                for (int face = 0; face < Face.Count; face++)
                {
                    Vector3I neighbour = cell + Face.Offsets[face];
                    if (!map.HasCell(neighbour)) continue;

                    int neighbourState = map.GetState(neighbour);
                    int opposite = Face.Opposite(face);

                    Assert.Equal(CellSurface.SelfAirtight(neighbourState, opposite), CellSurface.NeighbourAirtight(state, face));
                    Assert.Equal(CellSurface.SelfMount(neighbourState, opposite), CellSurface.NeighbourMount(state, face));
                }
            }
        }

        [Fact]

        public void RemovingABlockClearsTheNeighbourBitsItProduced()
        {

            GridModel grid = new GridModel(2.5f);
            BlockInstance keep = grid.Add(Catalog.LightArmor(), Vector3I.Zero);
            BlockInstance drop = grid.Add(Catalog.LightArmor(), new Vector3I(1, 0, 0));


            SurfaceMap map = new SurfaceMap();
            map.AddBlock(keep);
            map.AddBlock(drop);
            Assert.True(CellSurface.NeighbourAirtight(map.GetState(Vector3I.Zero), Face.Right));

            grid.Remove(drop);
            map.RemoveBlock(drop);

            Assert.False(CellSurface.NeighbourAirtight(map.GetState(Vector3I.Zero), Face.Right));
            Assert.False(map.HasCell(new Vector3I(1, 0, 0)));
        }

        [Fact]

        public void IncrementalEditsMatchAFullRebuild()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 2, 3));
            builder.Place(Catalog.Grating(), new Vector3I(1, 2, 1));


            SurfaceMap incremental = new SurfaceMap();
            foreach (BlockInstance block in builder.Placed) incremental.AddBlock(block);


            SurfaceMap rebuilt = MapOf(builder.Grid);

            foreach (Vector3I cell in new List<Vector3I>(rebuilt.Cells))
            {
                Assert.Equal(rebuilt.GetState(cell), incremental.GetState(cell));
            }
            Assert.Equal(rebuilt.CellCount, incremental.CellCount);
        }

        [Fact]

        public void AFaceIsSealedIfEitherSideSeals()
        {

            GridModel grid = new GridModel(2.5f);
            grid.Add(Catalog.LightArmor(), Vector3I.Zero);
            grid.Add(Catalog.Grating(), new Vector3I(1, 0, 0));

            SurfaceMap map = MapOf(grid);

            Assert.True(map.IsFaceSealed(Vector3I.Zero, Face.Right));
            Assert.True(map.IsFaceSealed(new Vector3I(1, 0, 0), Face.Left));

            Assert.False(map.IsFaceSealed(new Vector3I(1, 0, 0), Face.Right));
        }

        [Fact]

        public void ALoneBlockExposesAllSixFaces()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            SurfaceMap map = MapOf(builder.Grid);

            int[] exposed = map.GetExposedFaces(builder.Placed[0], RoomMap.AllExternal);
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(1, exposed[face]);
            }
        }

        [Fact]

        public void BoltedFacesAreNotExposed()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            SurfaceMap map = MapOf(builder.Grid);

            int[] exposed = map.GetExposedFaces(builder.Placed[0], RoomMap.AllExternal);
            Assert.Equal(0, exposed[Face.Right]);
            Assert.Equal(1, exposed[Face.Left]);
            Assert.Equal(5, Total(exposed));
        }

        [Fact]

        public void InteriorFacesOfAMultiCellBlockNeverCount()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel slab = BlockModel.Solid("Slab", new Vector3I(1, 3, 1), 900f, Catalog.DefaultThermal());
            builder.Place(slab, Vector3I.Zero);

            SurfaceMap map = MapOf(builder.Grid);

            int[] exposed = map.GetExposedFaces(builder.Placed[0], RoomMap.AllExternal);

            Assert.Equal(1, exposed[Face.Up]);
            Assert.Equal(1, exposed[Face.Down]);
            Assert.Equal(3, exposed[Face.Left]);
            Assert.Equal(3, exposed[Face.Right]);
            Assert.Equal(14, Total(exposed));
        }


        private static int Total(int[] faces)
        {
            int sum = 0;
            for (int i = 0; i < faces.Length; i++) sum += faces[i];
            return sum;
        }
    }

    /// <summary>
    /// The flood fill that decides which cells are inside a ship.
    ///
    /// <para>
    /// Two properties beyond the classification itself, and both exist because the fill is budgeted
    /// across frames: a pass stepped in small budgets gives the same answer as one run to completion,
    /// and the previous map stays readable while a new pass runs. The last case closes the loop to the
    /// physics — a sealed interior stops radiating.
    /// </para>
    /// </summary>
    public class RoomMapperTests
    {

        private static RoomMapper MapperFor(GridModel grid, out SurfaceMap surfaces)
        {

            surfaces = new SurfaceMap();
            surfaces.Rebuild(grid);


            RoomMapper mapper = new RoomMapper(surfaces);
            mapper.RequestRestart(grid);
            mapper.RunToCompletion();
            return mapper;
        }

        [Fact]

        public void ASolidBlockIsStructureAndTheSpaceAroundItIsExternal()
        {

            GridModel grid = new GridModel(2.5f);
            grid.Add(Catalog.LightArmor(), Vector3I.Zero);

            SurfaceMap surfaces;

            RoomMapper mapper = MapperFor(grid, out surfaces);
            RoomMap map = mapper.Map;

            Assert.True(map.IsSolid(Vector3I.Zero));
            Assert.False(map.IsExternal(Vector3I.Zero));
            Assert.True(map.IsExternal(new Vector3I(1, 0, 0)));
            Assert.Equal(0, map.RoomCount);
        }

        [Fact]

        public void ASealedShellCreatesOneInteriorRoom()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            SurfaceMap surfaces;

            RoomMapper mapper = MapperFor(builder.Grid, out surfaces);
            RoomMap map = mapper.Map;

            Assert.Equal(1, map.RoomCount);
            Assert.False(map.IsExternal(Vector3I.Zero));
            Assert.Equal(0, map.RoomIndexOf(Vector3I.Zero));
            Assert.True(map.IsExternal(new Vector3I(3, 0, 0)));
        }

        [Fact]

        public void AHoleInTheShellMakesTheInteriorExternal()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(1, 0, 0));
            builder.Grid.Remove(plug);

            SurfaceMap surfaces;

            RoomMapper mapper = MapperFor(builder.Grid, out surfaces);

            Assert.Equal(0, mapper.Map.RoomCount);
            Assert.True(mapper.Map.IsExternal(Vector3I.Zero));
        }

        [Fact]

        public void AnOpenLatticeDoesNotSealARoom()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.Grating(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            SurfaceMap surfaces;

            RoomMapper mapper = MapperFor(builder.Grid, out surfaces);

            Assert.Equal(0, mapper.Map.RoomCount);
            Assert.True(mapper.Map.IsExternal(Vector3I.Zero));
        }

        [Fact]

        public void SteppingInSmallBudgetsGivesTheSameAnswerAsRunningItAllAtOnce()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-2, -2, -2), new Vector3I(3, 3, 3));


            SurfaceMap surfaces = new SurfaceMap();
            surfaces.Rebuild(builder.Grid);


            RoomMapper full = new RoomMapper(surfaces);
            full.RequestRestart(builder.Grid);
            full.RunToCompletion();


            RoomMapper incremental = new RoomMapper(surfaces);
            incremental.RequestRestart(builder.Grid);
            int guard = 100000;
            while (incremental.HasWorkPending && guard-- > 0)
            {
                incremental.Step(3);
            }

            Assert.Equal(1, incremental.CompletedPasses);
            Assert.Equal(full.Map.RoomCount, incremental.Map.RoomCount);
            Assert.Equal(full.Map.ExternalCellCount, incremental.Map.ExternalCellCount);
            Assert.Equal(full.Map.SolidCellCount, incremental.Map.SolidCellCount);

            for (int x = -3; x <= 3; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    for (int z = -3; z <= 3; z++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);
                        Assert.Equal(full.Map.IsExternal(cell), incremental.Map.IsExternal(cell));
                        Assert.Equal(full.Map.IsSolid(cell), incremental.Map.IsSolid(cell));
                    }
                }
            }
        }

        [Fact]

        public void ManyRestartRequestsCollapseIntoOnePass()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));


            SurfaceMap surfaces = new SurfaceMap();
            surfaces.Rebuild(builder.Grid);

            RoomMapper mapper = new RoomMapper(surfaces);

            for (int i = 0; i < 500; i++)
            {
                mapper.RequestRestart(builder.Grid);
            }

            mapper.RunToCompletion();
            Assert.Equal(1, mapper.CompletedPasses);
        }

        [Fact]

        public void TheOldMapIsReadableWhileANewPassIsRunning()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));


            SurfaceMap surfaces = new SurfaceMap();
            surfaces.Rebuild(builder.Grid);


            RoomMapper mapper = new RoomMapper(surfaces);
            mapper.RequestRestart(builder.Grid);
            mapper.RunToCompletion();
            RoomMap first = mapper.Map;

            mapper.RequestRestart(builder.Grid);
            mapper.Step(2);

            Assert.Same(first, mapper.Map);
            Assert.Equal(1, first.RoomCount);
        }

        [Fact]

        public void ExposureUsesTheRoomMapSoSealedInteriorsDoNotRadiate()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            builder.Place(Catalog.Reactor(), Vector3I.Zero);

            SurfaceMap surfaces;

            RoomMapper mapper = MapperFor(builder.Grid, out surfaces);

            int[] exposed = surfaces.GetExposedFaces(builder.Last, mapper.Map);
            int total = 0;
            for (int i = 0; i < exposed.Length; i++) total += exposed[i];

            Assert.Equal(0, total);
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// `GridModel.GetNeighbours` answers a one-cell block with six probes and no deduplication, and
    /// is held to the boundary-walking query it short-cuts — the same neighbours in the same order,
    /// for every block of a census hull and of a grid that mixes one-cell blocks with bars and cubes
    /// (`D8`). Order matters here beyond correctness: the link list is built in this order, and the
    /// conduction sum accumulates in link order, so a permutation would move the last bit of a
    /// temperature.
    /// </summary>
    public class GridModelAdjacencyTests
    {

        private static void AssertSame(GridModel grid, string what)
        {

            List<BlockInstance> fast = new List<BlockInstance>();

            List<BlockInstance> walked = new List<BlockInstance>();
            int answers = 0;
            int oneCell = 0;

            for (int b = 0; b < grid.Blocks.Count; b++)
            {
                BlockInstance block = grid.Blocks[b];
                fast.Clear(); walked.Clear();
                grid.GetNeighbours(block, fast);
                grid.GetNeighboursWalkingTheBoundary(block, walked);

                Assert.True(fast.Count == walked.Count,
                    what + ": " + block + " has " + fast.Count + " neighbours by the short path and " + walked.Count + " by the walk");
                for (int i = 0; i < fast.Count; i++)
                {
                    Assert.True(ReferenceEquals(fast[i], walked[i]),
                        what + ": " + block + " neighbour " + i + " is " + fast[i] + " by the short path and " + walked[i] + " by the walk");
                }
                answers += fast.Count;
                if (block.CellCount == 1) oneCell++;
            }

            Assert.True(answers > 0, what + ": no block has a neighbour, so nothing was compared");
            Assert.True(oneCell > 0, what + ": no one-cell block, so the short path never ran");
        }

        [Fact]

        public void TheWalkReportsTheFaceContactFaceWouldFind()
        {
            int compared = 0;
            int multiCell = 0;

            foreach (GridModel grid in new[] { CensusGrid(), MixedGrid() })
            {

                List<BlockInstance> neighbours = new List<BlockInstance>();

                List<int> faces = new List<int>();

                for (int b = 0; b < grid.Blocks.Count; b++)
                {
                    BlockInstance block = grid.Blocks[b];
                    neighbours.Clear(); faces.Clear();
                    grid.GetNeighbours(block, neighbours, faces);

                    Assert.Equal(neighbours.Count, faces.Count);
                    for (int i = 0; i < neighbours.Count; i++)
                    {
                        int expected = ConductionBuilder.ContactFace(block, neighbours[i]);
                        Assert.True(expected == faces[i],
                            block + " meets " + neighbours[i] + " across " + Face.Name(faces[i])
                            + " by the walk and " + Face.Name(expected) + " by the boxes");
                        compared++;
                    }
                    if (block.CellCount > 1) multiCell++;
                }
            }

            Assert.True(compared > 0, "no neighbour was compared");
            Assert.True(multiCell > 0, "no multi-cell block across either fixture, so the boundary walk's face was never checked");
        }


        private static GridModel CensusGrid()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            return builder.Grid;
        }

        [Fact]

        public void ACensusHullAnswersTheSameNeighboursByBothPaths()
        {
            AssertSame(CensusGrid(), "census hull");
        }


        private static GridModel MixedGrid()
        {

            GridBuilder builder = MixedBuilder();
            return builder.Grid;
        }


        private static GridBuilder MixedBuilder()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel unit = Catalog.LightArmor();
            BlockModel bar = Catalog.LightArmorBar(3);
            BlockModel cube = Catalog.LightArmorCube(3);

            builder.Place(cube, new Vector3I(0, 0, 0));
            builder.Place(bar, new Vector3I(3, 0, 0));
            builder.Place(bar, new Vector3I(3, 1, 0), new BlockOrientation(Base6Directions.Direction.Up, Base6Directions.Direction.Forward));
            for (int x = -1; x <= 6; x++)
            for (int z = -1; z <= 3; z++)
            {
                if (builder.Grid.IsOccupied(new Vector3I(x, 3, z))) continue;
                builder.Place(unit, new Vector3I(x, 3, z));
            }
            for (int y = 0; y < 3; y++) builder.Place(unit, new Vector3I(-1, y, 1));

            return builder;
        }

        [Fact]

        public void AMixedGridAnswersTheSameNeighboursByBothPaths()
        {

            GridBuilder builder = MixedBuilder();

            int multi = 0;
            for (int b = 0; b < builder.Grid.Blocks.Count; b++) if (builder.Grid.Blocks[b].CellCount > 1) multi++;
            Assert.True(multi >= 3, "the mixed grid holds " + multi + " multi-cell blocks");

            AssertSame(builder.Grid, "mixed grid");
        }

        [Fact]

        public void TheOccupancyFilteredWalkFindsExactlyWhatThePlainWalkFinds()
        {
            ThermalSimulation simulation = Hulls.Driven(Hulls.Uncapped(), 4000);
            GridModel grid = simulation.Grid;
            CellBitset occupied = grid.Occupancy();


            List<BlockInstance> plain = new List<BlockInstance>();

            List<int> plainFaces = new List<int>();

            List<BlockInstance> filtered = new List<BlockInstance>();

            List<int> filteredFaces = new List<int>();

            IList<BlockInstance> blocks = grid.Blocks;
            int judged = 0;
            int neighbours = 0;

            for (int b = 0; b < blocks.Count; b++)
            {
                plain.Clear();
                plainFaces.Clear();
                filtered.Clear();
                filteredFaces.Clear();

                grid.GetNeighbours(blocks[b], plain, plainFaces, null);
                grid.GetNeighbours(blocks[b], filtered, filteredFaces, occupied);

                Assert.True(plain.Count == filtered.Count,
                    "block " + b + " has " + plain.Count + " neighbours unfiltered and "
                    + filtered.Count + " filtered");

                for (int n = 0; n < plain.Count; n++)
                {
                    Assert.Same(plain[n], filtered[n]);
                    Assert.Equal(plainFaces[n], filteredFaces[n]);
                }

                neighbours += plain.Count;
                judged++;
            }

            Assert.True(judged > 3000, "only " + judged + " blocks were walked");
            Assert.True(neighbours > judged * 2, "the hull averages fewer than two neighbours a block");
        }

        [Fact]

        public void GetAtKeyAnswersForEveryCellOfABlockAndGetByKeyOnlyForItsLowest()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmorCube(3), new Vector3I(0, 0, 0));
            builder.Place(Catalog.LightArmor(), new Vector3I(5, 0, 0));

            GridModel grid = builder.Grid;

            int interior = 0;
            for (int z = 0; z < 3; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);
                        long key = GridMath.Key(cell);

                        Assert.Same(grid.GetAtCell(cell), grid.GetAtKey(key));
                        Assert.NotNull(grid.GetAtKey(key));

                        if (x == 0 && y == 0 && z == 0) continue;

                        Assert.Null(grid.GetByKey(key));
                        interior++;
                    }
                }
            }

            Assert.Equal(26, interior);
            Assert.NotNull(grid.GetByKey(GridMath.Key(new Vector3I(0, 0, 0))));

            Assert.Null(grid.GetAtKey(GridMath.Key(new Vector3I(4, 0, 0))));
            Assert.Null(grid.GetAtCell(new Vector3I(4, 0, 0)));
        }
    }
}

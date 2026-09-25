using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The one-cell fast path against the walk it replaces, bit for bit.**
    ///
    /// <para>
    /// A one-cell block's neighbourhood is the same 26 offsets wherever it sits — its centre is its
    /// own cell — so their unit vectors are constants that the general walk arrives at by 26 square
    /// roots and 26 divisions per block. The fast path reads them from a table instead. A census
    /// hull is mostly one-cell blocks, so this is most of the pass.
    /// </para>
    ///
    /// <para>
    /// **Equal is asserted exactly rather than to a tolerance**, because the claim is that the two
    /// do the same arithmetic in the same order and not that they agree closely. Float addition is
    /// not associative, so a table walked in a different order from the loop would be a different
    /// answer — and one close enough to pass a tolerance is exactly the failure that would survive
    /// into the corpus figures.
    /// </para>
    /// </summary>
    public class ShapeNormalOneCellTests
    {

        private static ThermalSimulation Hull()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableShapeDrag = true;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    for (int z = 0; z < 6 - y; z++)
                    {
                        if (x == 3 && y == 1 && z == 1) continue;
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));
                    }
                }
            }

            builder.Place(Catalog.HeavyArmor(), new Vector3I(20, 20, 20));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }

        [Fact]

        public void TheOneCellPathAgreesWithTheWalkExactly()
        {

            ThermalSimulation hull = Hull();
            CellBitset occupancy = hull.Grid.Occupancy();

            int oneCell = 0;
            int moved = 0;

            for (int i = 0; i < hull.Solver.Nodes.Count; i++)
            {
                BlockInstance block = hull.Solver.Nodes[i].Block;
                if (block.CellCount != 1) continue;

                oneCell++;

                Vector3 fast = ShapeNormal.Of(occupancy, block);
                Vector3 walked = ShapeNormal.Walking(occupancy, block, 1);

                Assert.Equal(walked.X, fast.X);
                Assert.Equal(walked.Y, fast.Y);
                Assert.Equal(walked.Z, fast.Z);

                if (fast != Vector3.Zero) moved++;
            }

            Assert.True(oneCell > 100, "only " + oneCell + " one-cell blocks, so this compared little");
            Assert.True(moved > 50, "only " + moved + " of them produced a normal at all");
        }

        [Fact]

        public void ABlockWithNoNeighboursReadsZeroBothWays()
        {

            ThermalSimulation hull = Hull();
            CellBitset occupancy = hull.Grid.Occupancy();

            BlockInstance alone = hull.Grid.GetAtCell(new Vector3I(20, 20, 20));

            Assert.Equal(Vector3.Zero, ShapeNormal.Of(occupancy, alone));
            Assert.Equal(Vector3.Zero, ShapeNormal.Walking(occupancy, alone, 1));
        }

        [Fact]

        public void AMultiCellBlockStillTakesTheWalk()
        {

            ThermalSimulation hull = Hull();
            CellBitset occupancy = hull.Grid.Occupancy();

            for (int i = 0; i < hull.Solver.Nodes.Count; i++)
            {
                BlockInstance block = hull.Solver.Nodes[i].Block;
                if (block.CellCount == 1) continue;

                Assert.Equal(ShapeNormal.Walking(occupancy, block, 1), ShapeNormal.Of(occupancy, block));
            }
        }
    }
}

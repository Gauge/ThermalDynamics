using Aerodynamics.Core;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The flight profile's mass properties against uniform cuboid integrals and the parallel-axis
    /// theorem, including rotation, translation and decomposition into equal-density cells (E7).
    /// These check the diagonal inertia used by the offline flight rig, not the engine's mass tensor.
    /// </summary>
    [Trait("scope", "integration")]
    public class AeroProfileMassTests
    {
        [Theory]
        [InlineData(1, 1, 1, 2.5f, 12500f, 12500f, 12500f)]
        [InlineData(1, 2, 3, 2.5f, 81250f, 62500f, 31250f)]
        [InlineData(3, 1, 2, 2.5f, 31250f, 81250f, 62500f)]
        [InlineData(1, 2, 3, 0.5f, 3250f, 2500f, 1250f)]
        public void ACuboidHasItsAnalyticDiagonalInertia(int x, int y, int z, float cell,
            float ix, float iy, float iz)
        {
            GridModel grid = new GridModel(cell);
            grid.Add(new BlockModel { Size = new Vector3I(x, y, z), Mass = 12000f }, Vector3I.Zero);
            AeroProfile profile = AeroProfile.Build(grid, AeroProfile.CoarseDirections);

            Assert.Equal(12000f, profile.Mass);
            Assert.Equal(ix, profile.Inertia.X, 2);
            Assert.Equal(iy, profile.Inertia.Y, 2);
            Assert.Equal(iz, profile.Inertia.Z, 2);
            Assert.Equal(new Vector3(x - 1, y - 1, z - 1) * (cell / 2f), profile.CentreOfMass);
        }

        [Fact]
        public void RotatingABlockRotatesItsInertiaAxes()
        {
            GridModel grid = new GridModel(2.5f);
            grid.Add(new BlockModel { Size = new Vector3I(1, 2, 3), Mass = 12000f }, Vector3I.Zero,
                new BlockOrientation(Base6Directions.Direction.Right, Base6Directions.Direction.Up));
            AeroProfile profile = AeroProfile.Build(grid, AeroProfile.CoarseDirections);

            Assert.Equal(31250f, profile.Inertia.X, 2);
            Assert.Equal(62500f, profile.Inertia.Y, 2);
            Assert.Equal(81250f, profile.Inertia.Z, 2);
        }

        [Fact]
        public void UnequalMassesUseTheMassCentreAndParallelAxisTheorem()
        {
            GridModel grid = new GridModel(1f);
            grid.Add(new BlockModel { Mass = 2f }, Vector3I.Zero);
            grid.Add(new BlockModel { Mass = 6f }, new Vector3I(4, 0, 0));
            AeroProfile profile = AeroProfile.Build(grid, AeroProfile.CoarseDirections);

            Assert.Equal(8f, profile.Mass);
            Assert.Equal(new Vector3(3f, 0f, 0f), profile.CentreOfMass);
            Assert.Equal(8f / 6f, profile.Inertia.X, 5);
            Assert.Equal((8f / 6f) + 24f, profile.Inertia.Y, 5);
            Assert.Equal((8f / 6f) + 24f, profile.Inertia.Z, 5);
        }

        [Fact]
        public void SplittingACuboidIntoEqualDensityCellsKeepsItsMassProperties()
        {
            GridModel whole = new GridModel(2.5f);
            whole.Add(new BlockModel { Size = new Vector3I(2, 3, 4), Mass = 24000f }, Vector3I.Zero);
            GridModel split = new GridModel(2.5f);
            Vector3I offset = new Vector3I(13, -7, 21);
            BlockModel cell = new BlockModel { Mass = 1000f };
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 3; y++)
                    for (int z = 0; z < 4; z++) split.Add(cell, offset + new Vector3I(x, y, z));

            AeroProfile a = AeroProfile.Build(whole, AeroProfile.CoarseDirections);
            AeroProfile b = AeroProfile.Build(split, AeroProfile.CoarseDirections);
            Assert.Equal(a.Mass, b.Mass);
            Assert.Equal(a.CentreOfMass + ((Vector3)offset * 2.5f), b.CentreOfMass);
            Assert.Equal(a.Inertia.X, b.Inertia.X, 1);
            Assert.Equal(a.Inertia.Y, b.Inertia.Y, 1);
            Assert.Equal(a.Inertia.Z, b.Inertia.Z, 1);
        }
    }
}

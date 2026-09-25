using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The wind's roughness length comes from the ground under the grid.
    ///
    /// <para>
    /// It used to be one number for a whole world, while the ground material under a grid was
    /// already being classified for the temperature model — and roughness is exactly what it should
    /// vary with. backlog.md `B16`.
    /// </para>
    ///
    /// <para>
    /// **It is also the one figure in that table that is not an opinion.** The temperature offsets
    /// and the swing multipliers are numbers chosen to look like Earth and are recorded as such
    /// (`C7`); roughness length is a standard wind-engineering quantity with a published table
    /// behind it, so these check the model against that table rather than against itself.
    /// </para>
    /// </summary>
    public class GroundRoughnessTests
    {
        private const float WorldSetting = 0.03f;

        [Fact]

        public void TheOrderingMatchesTheWindEngineeringTable()
        {
            float ice = GroundTemperature.RoughnessFor("Ice_01", WorldSetting);
            float snow = GroundTemperature.RoughnessFor("Snow", WorldSetting);
            float sand = GroundTemperature.RoughnessFor("Sand_01", WorldSetting);
            float grass = GroundTemperature.RoughnessFor("Grass", WorldSetting);
            float rock = GroundTemperature.RoughnessFor("Stone_02", WorldSetting);
            float forest = GroundTemperature.RoughnessFor("Woods", WorldSetting);

            Assert.True(ice < snow, "open ice is smoother than snow");
            Assert.True(snow < sand, "snow is smoother than sand");
            Assert.True(sand < grass, "sand is smoother than grassland");
            Assert.True(grass < rock, "grassland is smoother than broken rock");
            Assert.True(rock < forest, "rock is smoother than forest");

            Assert.True(forest > ice * 1000f,
                "forest should be orders of magnitude rougher than ice: " + forest + " against " + ice);
        }

        [Fact]

        public void UnknownGroundKeepsTheWorldSetting()
        {
            Assert.Equal(WorldSetting, GroundTemperature.RoughnessFor("Unobtainium", WorldSetting), 6);
            Assert.Equal(WorldSetting, GroundTemperature.RoughnessFor("", WorldSetting), 6);
            Assert.Equal(WorldSetting, GroundTemperature.RoughnessFor(null, WorldSetting), 6);
        }

        [Fact]

        public void RougherGroundSlowsTheWindNearIt()
        {
            float snow = GroundTemperature.RoughnessFor("Snow", WorldSetting);
            float forest = GroundTemperature.RoughnessFor("Woods", WorldSetting);

            Assert.True(WindProfile.Multiplier(2f, forest, 600f) < WindProfile.Multiplier(2f, snow, 600f),
                "two metres over forest should be slower than two metres over snow");

            Assert.True(WindProfile.Multiplier(600f, forest, 600f) > WindProfile.Multiplier(600f, snow, 600f),
                "at the gradient height the rough profile should have climbed further");
        }

        [Fact]

        public void EveryClassifiedMaterialCarriesOne()
        {
            string[] materials =
            {
                "snow", "ice", "frozen", "tundra", "sand", "desert", "dune",
                "lava", "magma", "grass", "soil", "dirt", "rock", "stone", "woods", "forest",
            };

            foreach (string material in materials)
            {
                float roughness = GroundTemperature.RoughnessFor(material, WorldSetting);
                Assert.True(roughness > 0f, material + " has no roughness");
                Assert.True(roughness < 2f, material + " is rougher than any real ground: " + roughness);
            }
        }
    }
}

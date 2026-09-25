using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Whether the surface normal reconstructed from occupancy finds the surface a hull actually
    /// has.** `ShapeNormal` reads the cells around a block and returns the direction its surface
    /// faces, which a stair-stepped slope needs because its six axis faces only ever point along
    /// the axes.
    ///
    /// <para>
    /// The normal is surface data rather than a term: the friction row stopped reading it when the
    /// aerodynamic force was stripped, and the force profile that replaces it reads it again (see
    /// aerodynamics.md, `AR4` and `AR9`). So these pin the geometry and nothing downstream of it.
    /// Each reading is expressed as the squared incidence against a flow, `max(0, n·w)²`, because
    /// that is the quantity every consumer of a normal has wanted from it and what the pinned
    /// figures were first measured as.
    /// </para>
    /// </summary>
    public class ShapeNormalGeometryTests
    {
        private static ThermalSettings Settings()
        {
            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Built(GridBuilder builder)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float Incidence(Vector3 normal, Vector3 flow)
        {
            float dot = Vector3.Dot(normal, flow);
            return dot > 0f ? dot * dot : 0f;
        }


        private static ThermalSimulation Wedge()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    for (int z = 0; z < 4 - y; z++)
                    {
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));
                    }
                }
            }

            return Built(builder);
        }

        [Fact]

        public void AStairSteppedSlopeReconstructsToItsMeanSurface()
        {
            ThermalSimulation wedge = Wedge();
            CellBitset occupancy = wedge.Grid.Occupancy();

            foreach (Vector3I cell in new[] { new Vector3I(1, 1, 2), new Vector3I(1, 2, 1) })
            {
                Vector3 normal = ShapeNormal.Of(occupancy, wedge.Grid.GetAtCell(cell));

                Assert.Equal(0f, normal.X, 3);
                Assert.Equal(0.707f, normal.Y, 3);
                Assert.Equal(0.707f, normal.Z, 3);
                Assert.Equal(0.5f, Incidence(normal, Vector3.Backward), 3);
            }
        }

        [Fact]

        public void AFlatFaceSquareToTheFlowReadsSquareOn()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));
            ThermalSimulation brick = Built(builder);

            Vector3 middle = ShapeNormal.Of(brick.Grid.Occupancy(), brick.Grid.GetAtCell(new Vector3I(1, 1, 3)));

            Assert.Equal(1f, Incidence(middle, Vector3.Backward), 3);
        }

        [Theory]
        [InlineData(1, 0.500f)]
        [InlineData(2, 0.500f)]
        [InlineData(3, 0.500f)]

        public void AFortyFiveDegreeSlopeReadsTheSameAtEveryRadius(int radius, float expected)
        {
            ThermalSimulation ramp = Ramp(1);

            Vector3 normal = ShapeNormal.Of(ramp.Grid.Occupancy(), SlopeCell(ramp, 1), radius);

            Assert.Equal(0f, normal.X, 3);
            Assert.Equal(expected, Incidence(normal, Vector3.Backward), 3);
        }

        [Fact]

        public void RadiusOneCannotSeparateTheShallowSlopes()
        {
            float shallow = IncidenceOf(Ramp(2), 2, 1);

            float shallower = IncidenceOf(Ramp(3), 3, 1);

            Assert.Equal(0.134f, shallow, 3);
            Assert.Equal(0.134f, shallower, 3);
        }


        private static float IncidenceOf(ThermalSimulation ramp, int run, int radius)
        {
            return Incidence(
                ShapeNormal.Of(ramp.Grid.Occupancy(), SlopeCell(ramp, run), radius),
                Vector3.Backward);
        }

        private const int RampWidth = 16;
        private const int RampHeight = 8;


        private static ThermalSimulation Ramp(int run)
        {
            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < RampHeight; y++)
            {
                for (int x = 0; x < RampWidth; x++)
                {
                    for (int z = 0; z < (RampHeight - y) * run; z++)
                    {
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));
                    }
                }
            }

            return Built(builder);
        }


        private static BlockInstance SlopeCell(ThermalSimulation ramp, int run)
        {
            int y = RampHeight / 2;
            return ramp.Grid.GetAtCell(new Vector3I(RampWidth / 2, y, (RampHeight - y) * run - 1));
        }
    }
}

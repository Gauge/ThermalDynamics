using Thermodynamics.Fixtures;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The friction row is a projected area, and a projected area cannot tell a wedge from a
    /// brick.**
    ///
    /// <para>
    /// What the solver sums is, per node, the exposed cell faces weighted by `max(0, dot(faceNormal,
    /// windDirection))` over the six axis-aligned faces. A stair-stepped wedge presents exactly the
    /// same count of wind-facing cell faces as the brick that shares its frontal cross-section — the
    /// steps project onto the same square — so the two heat *identically*, while their real drag
    /// coefficients differ by an order of magnitude.
    /// </para>
    ///
    /// <para>
    /// That is the counter-example any shape-aware aerodynamic term has to pass, which is why it is
    /// kept as a test rather than an argument: the rebuilt force reads reconstructed normals and a
    /// silhouette rather than this sum (aerodynamics.md, `AR3` and `AR4`), and this pair is what says
    /// whether that made a difference.
    /// </para>
    /// </summary>
    public class DragShapeTests
    {
        private const float ThickAir = 1f;
        private const float Speed = 120f;


        private static HistoricalThermalSettings Settings()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableRecoveryHeating = false; // Historical cubic-source control.
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Brick()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
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

            ThermalSimulation simulation = builder.BuildSimulation(Settings(), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float DragWatts(ThermalSimulation simulation)
        {
            simulation.StepExact(1, Worlds.Flight(ThickAir, Speed));
            return simulation.Solver.LastFrictionWatts;
        }

        [Fact]

        public void TheWedgeIsNotJustTheBrickAgain()
        {

            ThermalSimulation brick = Brick();

            ThermalSimulation wedge = Wedge();

            Assert.Equal(64, brick.Solver.Nodes.Count);
            Assert.Equal(40, wedge.Solver.Nodes.Count);
        }

        [Fact]

        public void ABrickAndAWedgeOfTheSameFrontalAreaDragIdentically()
        {

            float brick = DragWatts(Brick());

            float wedge = DragWatts(Wedge());

            Assert.True(brick > 0f, "the brick took no drag, so this compares nothing");

            Assert.Equal(brick, wedge, 3);
        }

        [Fact]

        public void HalvingTheFrontalAreaHalvesTheDrag()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 2, 4));

            ThermalSimulation narrow = builder.BuildSimulation(Settings(), 293.15f);
            narrow.Planet = PlanetThermalProperties.Default();


            float full = DragWatts(Brick());

            float half = DragWatts(narrow);

            Assert.True(half > 0f);
            Assert.Equal(full / 2f, half, 2);
        }
    }
}

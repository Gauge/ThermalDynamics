using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Whether a drag coefficient can be derived from what this model computes. It cannot, and
    /// this is the pair of hulls that shows why.**
    ///
    /// <para>
    /// The friction term is `FrictionScale x rho x v_rel^3 x area x windward exposure`, and real
    /// drag power is `1/2 C_d rho A v^3` — the same expression, so matching them makes
    /// `FrictionScale` exactly `1/2 C_d` times the fraction of the work that lands in the surface.
    /// That invites an obvious question: if the model already computes a windward area, can it
    /// compute the coefficient too, and spare an authored number (`P7`)?
    /// </para>
    ///
    /// <para>
    /// **The windward exposure is a projected area, and a projected area is not a shape.** What the
    /// solver sums is, per node, the exposed cell faces weighted by `max(0, dot(faceNormal,
    /// windDirection))` over the six axis-aligned faces. A stair-stepped wedge presents exactly the
    /// same count of wind-facing cell faces as the brick that shares its frontal cross-section —
    /// the steps project onto the same square — so the two read *identically*, while their real
    /// drag coefficients differ by an order of magnitude.
    /// </para>
    ///
    /// <para>
    /// So the drag milestone's coefficient has to be authored until there is a shape term to derive it from,
    /// which is `K6` and the drag milestone's subject. This test is here so that when somebody proposes deriving
    /// it, the counter-example is a test rather than an argument.
    /// </para>
    /// </summary>
    public class DragShapeTests
    {
        private const float ThickAir = 1f;
        private const float Speed = 120f;


        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableShapeDrag = false;
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

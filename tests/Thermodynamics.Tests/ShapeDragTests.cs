using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The shape term, and whether it separates the pair a projected area cannot.**
    ///
    /// <para>
    /// `DragShapeTests` is the counter-example this exists to answer: a brick and a stair-stepped
    /// wedge sharing a frontal cross-section compute identical drag, because the sum is over six
    /// axis normals and the steps project onto the square the brick presents. `ShapeNormal` gives
    /// each node an effective normal read from the cells around it, and the Newtonian `sin²θ` that
    /// the projected area is missing. These are the readings that say whether it worked.
    /// See thermal-model.md, The shape term, and backlog.md `K22`.
    /// </para>
    /// </summary>
    public class ShapeDragTests
    {
        private const float ThickAir = 1f;
        private const float Speed = 120f;


        private static ThermalSettings Settings(bool shape)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableShapeDrag = shape;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Brick(bool shape)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            return Built(builder, shape);
        }


        private static ThermalSimulation Wedge(bool shape)
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


            return Built(builder, shape);
        }


        private static ThermalSimulation Built(GridBuilder builder, bool shape)
        {
            ThermalSimulation simulation = builder.BuildSimulation(Settings(shape), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float DragWatts(ThermalSimulation simulation)
        {
            simulation.StepExact(1, Worlds.Flight(ThickAir, Speed));
            return simulation.Solver.LastFrictionWatts;
        }

        [Fact]

        public void TheShapeTermSeparatesTheWedgeFromTheBrick()
        {

            float flatBrick = DragWatts(Brick(false));

            float flatWedge = DragWatts(Wedge(false));
            Assert.Equal(flatBrick, flatWedge, 3);


            float brick = DragWatts(Brick(true));

            float wedge = DragWatts(Wedge(true));

            Assert.True(brick > 0f, "the brick took no drag, so this compares nothing");
            Assert.True(wedge < brick,
                "the wedge must take less drag than the brick it projects onto: "
                    + wedge + " against " + brick);

            Assert.Equal(0.816f, wedge / brick, 3);
        }

        [Fact]

        public void AStairSteppedSlopeReconstructsToItsMeanSurface()
        {

            ThermalSimulation wedge = Wedge(true);
            CellBitset occupancy = wedge.Grid.Occupancy();

            foreach (Vector3I cell in new[] { new Vector3I(1, 1, 2), new Vector3I(1, 2, 1) })
            {
                Vector3 normal = ShapeNormal.Of(occupancy, wedge.Grid.GetAtCell(cell));

                Assert.Equal(0f, normal.X, 3);
                Assert.Equal(0.707f, normal.Y, 3);
                Assert.Equal(0.707f, normal.Z, 3);
                Assert.Equal(0.5f, ShapeNormal.Factor(normal, Vector3.Backward), 3);
            }
        }

        [Fact]

        public void AFlatFaceSquareToTheFlowIsUnchangedAndTheEdgesAreNot()
        {

            ThermalSimulation brick = Brick(true);
            CellBitset occupancy = brick.Grid.Occupancy();

            Vector3 middle = ShapeNormal.Of(occupancy, brick.Grid.GetAtCell(new Vector3I(1, 1, 3)));
            Assert.Equal(1f, ShapeNormal.Factor(middle, Vector3.Backward), 3);

            Assert.Equal(0.583f, DragWatts(Brick(true)) / DragWatts(Brick(false)), 3);
        }

        [Fact]

        public void TheShapeTermMovesTemperaturesAndNotOnlyDrag()
        {

            ThermalSimulation off = Heated(false);

            ThermalSimulation on = Heated(true);

            off.StepExact(600, Worlds.Flight(ReentryDensity, ReentrySpeed));
            on.StepExact(600, Worlds.Flight(ReentryDensity, ReentrySpeed));


            float hot = Peak(off);

            float shaped = Peak(on);

            Assert.True(hot > 0f && shaped > 0f);
            Assert.True(shaped < hot,
                "the shaped hull is not cooler, so the term is not reaching the friction that heats"
                    + " it — off " + hot + " K, on " + shaped + " K");

            Assert.Equal(9.96f, hot - shaped, 1);
        }


        private static ThermalSimulation Heated(bool shape)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableSolarHeat = false;
            settings.EnableDamage = false;
            settings.EnableShapeDrag = shape;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static float Peak(ThermalSimulation simulation)
        {
            float peak = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                float t = simulation.Solver.Nodes[i].Temperature;
                if (t > peak) peak = t;
            }

            return peak;
        }

        private const float ReentryDensity = 0.8f;
        private const float ReentrySpeed = 300f;

        [Theory]
        [InlineData(1, 0.500f)]
        [InlineData(2, 0.500f)]
        [InlineData(3, 0.500f)]

        public void AFortyFiveDegreeSlopeReadsTheSameAtEveryRadius(int radius, float expected)
        {

            ThermalSimulation ramp = Ramp(1);
            CellBitset occupancy = ramp.Grid.Occupancy();

            Vector3 normal = ShapeNormal.Of(occupancy, SlopeCell(ramp, 1), radius);

            Assert.Equal(0f, normal.X, 3);
            Assert.Equal(expected, ShapeNormal.Factor(normal, Vector3.Backward), 3);
        }

        [Fact]

        public void RadiusOneCannotSeparateTheShallowSlopes()
        {

            float shallow = FactorOf(Ramp(2), 2, 1);

            float shallower = FactorOf(Ramp(3), 3, 1);

            Assert.Equal(0.134f, shallow, 3);
            Assert.Equal(0.134f, shallower, 3);
        }


        private static float FactorOf(ThermalSimulation ramp, int run, int radius)
        {
            return ShapeNormal.Factor(
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

            ThermalSimulation simulation = builder.BuildSimulation(Settings(true), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static BlockInstance SlopeCell(ThermalSimulation ramp, int run)
        {
            int y = RampHeight / 2;
            return ramp.Grid.GetAtCell(

                new Vector3I(RampWidth / 2, y, (RampHeight - y) * run - 1));
        }

        [Fact]

        public void AnUnbuiltNormalReadsAsNoCorrection()
        {
            Assert.Equal(1f, ShapeNormal.Factor(Vector3.Zero, Vector3.Backward), 4);
            Assert.Equal(1f, ShapeNormal.Factor(Vector3.Zero, Vector3.Up), 4);
        }

        [Fact]

        public void ASurfaceInTheLeeTakesNothingAndNothingExceedsOne()
        {
            Assert.Equal(0f, ShapeNormal.Factor(Vector3.Forward, Vector3.Backward), 4);
            Assert.Equal(1f, ShapeNormal.Factor(Vector3.Backward, Vector3.Backward), 4);
        }

        [Fact]

        public void TheShapeNormalBudgetIsSizedAgainstWhatANodeCosts()
        {
            Assert.Equal(452, SimulationScheduler.ShapeNormalBudget(126731));
            Assert.Equal(3168, SimulationScheduler.ExposureBudget(126731));

            Assert.Equal(32, SimulationScheduler.ShapeNormalBudget(1));
            Assert.Equal(512, SimulationScheduler.ShapeNormalBudget(10000000));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]

        public void TheShapeTermOnlyEverReduces(bool wedge)
        {

            float without = DragWatts(wedge ? Wedge(false) : Brick(false));

            float with = DragWatts(wedge ? Wedge(true) : Brick(true));

            Assert.True(with <= without + 1e-3f,
                "the shape term raised drag, which it must never do: "
                    + with + " against " + without);
        }
    }
}

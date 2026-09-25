using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **A block telling the drag model it is a different shape than its faces suggest.**
    ///
    /// <para>
    /// The model's only shape term is a projected area — six exposed-face fractions weighted by
    /// their incidence — so it cannot tell a jet engine from a box of the same size. the drag milestone is the
    /// interface for a mod that knows its own block's shape to say so, and the mechanism is a
    /// multiplier on the exposure the solver already computes rather than a parallel model.
    /// </para>
    ///
    /// <para>
    /// **The risk here is larger than the heat API's and these tests are mostly about the bound.** A
    /// registered heat source changes a temperature; a registered profile changes how a ship flies,
    /// so a bad registration is a handling bug in somebody else's mod that looks like one in this
    /// one. A profile may only *reduce*, anything unusable reads as no change, and it must not
    /// reach the sun.
    /// </para>
    /// </summary>
    public class DragProfileTests
    {
        private const float ThickAir = 1f;


        private static ThermalSettings Settings(bool solar = false)
        {

            ThermalSettings settings = new ThermalSettings();

            settings.EnableEnvironment = solar;
            settings.EnableSolarHeat = solar;
            settings.EnableDamage = false;
            settings.Derive();
            return settings;
        }


        private static ThermalSimulation Hull(bool solar = false)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(solar), 293.15f);

            if (solar) simulation.Solver.CollectDiagnostics = true;
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static void Profile(ThermalSimulation simulation, DragProfile profile)
        {
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                simulation.Solver.Nodes[i].Drag = profile;
            }
        }


        private static float DragWatts(ThermalSimulation simulation)
        {
            simulation.StepExact(1, Worlds.Flight(ThickAir, 120f));
            return simulation.Solver.LastFrictionWatts;
        }

        [Fact]

        public void AProfileReducesTheDrag()
        {

            float plain = DragWatts(Hull());


            ThermalSimulation slippery = Hull();
            Profile(slippery, DragProfile.Of(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f));

            Assert.True(plain > 0f);
            Assert.Equal(plain * 0.5f, DragWatts(slippery), 2);
        }

        [Fact]

        public void OnlyTheFacesTheWindIsOnMatter()
        {

            float plain = DragWatts(Hull());


            ThermalSimulation sides = Hull();
            Profile(sides, DragProfile.Of(0f, 0f, 1f, 1f, 0f, 0f));


            ThermalSimulation windward = Hull();
            Profile(windward, DragProfile.Of(1f, 1f, 0f, 0f, 1f, 1f));


            float sidesWatts = DragWatts(sides);

            float windwardWatts = DragWatts(windward);

            Assert.True(sidesWatts == plain || windwardWatts == plain,
                "neither axis was the windward one, so this test is not aimed at the flow");
            Assert.NotEqual(sidesWatts, windwardWatts);
        }

        [Fact]

        public void AProfileCannotAddDrag()
        {

            float plain = DragWatts(Hull());


            ThermalSimulation greedy = Hull();
            Profile(greedy, DragProfile.Of(5f, 5f, 5f, 5f, 5f, 5f));

            Assert.Equal(plain, DragWatts(greedy), 2);
        }

        [Fact]

        public void NonsenseReadsAsNoChange()
        {

            float plain = DragWatts(Hull());


            ThermalSimulation nonsense = Hull();
            Profile(nonsense, DragProfile.Of(
                float.NaN, float.NegativeInfinity, -1f, float.PositiveInfinity, float.NaN, -0.5f));

            Assert.Equal(plain, DragWatts(nonsense), 2);
        }

        [Fact]

        public void AnUnsetProfileChangesNothing()
        {
            Assert.False(default(DragProfile).IsSet);
            for (int f = 0; f < Face.Count; f++) Assert.Equal(1f, default(DragProfile)[f]);


            ThermalSimulation cleared = Hull();
            Profile(cleared, default(DragProfile));

            Assert.Equal(DragWatts(Hull()), DragWatts(cleared), 2);
        }

        [Fact]

        public void AProfileDoesNotDimTheSun()
        {

            ThermalSimulation plain = Hull(solar: true);

            ThermalSimulation slippery = Hull(solar: true);
            Profile(slippery, DragProfile.Of(0f, 0f, 0f, 0f, 0f, 0f));

            plain.StepExact(4, Worlds.Space(Vector3.Forward));
            slippery.StepExact(4, Worlds.Space(Vector3.Forward));

            float plainSolar = 0f, slipperySolar = 0f;
            for (int i = 0; i < plain.Solver.Nodes.Count; i++)
            {
                plainSolar += plain.Solver.Nodes[i].LastSolarWatts;
                slipperySolar += slippery.Solver.Nodes[i].LastSolarWatts;
            }

            Assert.True(plainSolar > 0f, "no sunlight was absorbed, so this proves nothing");
            Assert.Equal(plainSolar, slipperySolar, 2);
        }
    }
}

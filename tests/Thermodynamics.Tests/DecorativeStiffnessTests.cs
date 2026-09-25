using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Why a light fitting still sets a warship's substep count after being given a light
    /// fitting's material properties.
    ///
    /// <para>
    /// A block's stability demand is its conductance over its heat capacity, and the conductance
    /// has two halves. Conduction is what the block is made of and what it is bolted to; the
    /// environment is radiation and convection over the block's exposed area. The `Cubes.xml`
    /// entries for lights, neon and cameras addressed the first half. The second is untouched by
    /// them: exposed area comes from the cell a block occupies, so a 16 kg fitting radiates and
    /// convects as though it were a 2.5 m cube.
    /// </para>
    ///
    /// <para>
    /// That is why the predicted saving arrived in vacuum and very nearly did not in air. In the
    /// 2026-08-20 fleet dump — flown in an atmosphere averaging 0.73 air density — `SmallLight`
    /// demands 22.7 substeps at its worst with 3 % of that demand coming from conduction, and
    /// 12,764 of 123,784 blocks are stiff mostly through radiation and convection. See
    /// stiffness.md.
    /// </para>
    /// </summary>
    public class DecorativeStiffnessTests
    {

        private static BlockThermalProperties AsSteel()
        {
            BlockThermalProperties t = Catalog.DefaultThermal();
            t.Conductivity = 50f;
            t.SpecificHeat = 450f;
            t.Emissivity = 0.9f;
            return t;
        }


        private static BlockThermalProperties AsLight()
        {

            BlockThermalProperties t = AsSteel();
            t.Conductivity = 2f;
            t.SpecificHeat = 900f;
            return t;
        }


        private static ThermalSolver.SubstepProfile Profile(
            BlockThermalProperties fitting, EnvironmentSample world)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                MaxSubsteps = 4096,
                MaxSubstepsPerBlock = 0,
                MaxElementVisitsPerStep = 0,
            };
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 1, 1));
            builder.Place(BlockModel.Solid("Fitting", Vector3I.One, 16f, fitting),

                new Vector3I(4, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

            simulation.StepExact(1, world);
            return simulation.Solver.ProfileSubsteps();
        }


        private static EnvironmentSample Vacuum() { return Worlds.Shadow(); }


        private static EnvironmentSample Air() { return Worlds.PlanetSurface(1f, 0.5f); }

        [Fact]

        public void InVacuumTheMaterialPropertiesCarryTheFitting()
        {

            ThermalSolver.SubstepProfile steel = Profile(AsSteel(), Vacuum());

            ThermalSolver.SubstepProfile light = Profile(AsLight(), Vacuum());

            Assert.True(steel.WorstNodeConductionShare > 0.6f,
                "steel conduction share " + steel.WorstNodeConductionShare);
            Assert.True(light.WorstNodeDemand < steel.WorstNodeDemand * 0.25f,
                "steel " + steel.WorstNodeDemand + ", light " + light.WorstNodeDemand);
        }

        [Fact]

        public void InAirTheEnvironmentIsMostOfWhatIsLeft()
        {

            ThermalSolver.SubstepProfile steel = Profile(AsSteel(), Air());

            ThermalSolver.SubstepProfile light = Profile(AsLight(), Air());

            Assert.True(steel.WorstNodeConductionShare < 0.5f,
                "even as steel the fitting is environment-limited in air: "
                + steel.WorstNodeConductionShare);

            Assert.True(light.WorstNodeDemand < steel.WorstNodeDemand * 0.5f,
                "the light fitting demands " + light.WorstNodeDemand + " against the steel one's "
                + steel.WorstNodeDemand + ", so the definitions have stopped reaching a fitting in"
                + " air and the note above needs rewriting");

            Assert.True(light.WorstNodeDemand > 4f,
                "a fitting with real material properties demands only "
                + light.WorstNodeDemand + " substeps in air, so the environment half has gone too"
                + " and this rig is no longer about a decorative block in atmosphere");
            Assert.True(light.WorstNodeConductionShare < 0.1f,
                "light conduction share " + light.WorstNodeConductionShare);
            Assert.True(light.WorstNodeConductionShare < steel.WorstNodeConductionShare * 0.5f,
                "the light fitting is " + light.WorstNodeConductionShare + " conduction against"
                + " the steel one's " + steel.WorstNodeConductionShare
                + ", so the material has stopped deciding which term carries the fitting");
        }

        [Fact]

        public void ExposedAreaIsTheKnobThatReachesIt()
        {

            BlockThermalProperties smaller = AsLight();
            smaller.ExposedSurfaceMultiplier = 0.1f;


            ThermalSolver.SubstepProfile full = Profile(AsLight(), Air());

            ThermalSolver.SubstepProfile reduced = Profile(smaller, Air());

            Assert.True(reduced.WorstNodeDemand < full.WorstNodeDemand * 0.2f,
                "full " + full.WorstNodeDemand + ", reduced " + reduced.WorstNodeDemand);
        }
    }
}

using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Why the same retune costs more in vacuum and less in air**, which is the arithmetic under
    /// the finding that decided `C12`.
    ///
    /// <para>
    /// Substep demand is a conductance over a heat capacity. `HeatTimeScale` divides every
    /// capacity and touches nothing else, so lowering it divides *every* stiffness term at once;
    /// multiplying a block's conductivity restores the conduction term and only that one. A block
    /// whose stiffness comes from its neighbours therefore gets dearer by
    /// <c>conductivity × clock / 225</c>, and a block whose stiffness comes from the air around it
    /// gets cheaper by <c>clock / 225</c>. Two rigs, one of each, are what these tests are.
    /// </para>
    ///
    /// <para>
    /// **The population figures are not pinned here and cannot be.** That air is where the budget
    /// goes — p99 73.4 substeps at 200 m/s against the 64 the caps grant, against 7.8 in vacuum —
    /// is a measurement over 49 workshop hulls that live outside the repository, and it is scored
    /// by `tools/corpus/air.py`. What is checkable without them is the mechanism that produces it,
    /// and a rig that isolates one term is a better test of that than a hull where the two are
    /// mixed in proportions nobody chose (`E7`). See
    /// balance.md.
    /// </para>
    /// </summary>
    public class AirCostTests
    {
        private const float CandidateClock = 100f;

        private const float CandidateConductivity = 4f;

        private const float ShippedClock = 225f;

        private const float ThickAir = 1f;

        private const float BaseConductivity = 120f;


        private static ThermalSettings Settings(float clock, int frequency = 0)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.HeatTimeScale = clock;
            if (frequency > 0) settings.Frequency = frequency;

            settings.MaxSubsteps = Hulls.Unbounded;
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static BlockModel Block(float conductivity, float mass, bool radiates = true)
        {
            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.Conductivity = BaseConductivity * conductivity;

            if (!radiates) thermal.Emissivity = 0f;

            return BlockModel.Solid("rig", Vector3I.One, mass, thermal);
        }


        private static float Demand(ThermalSimulation simulation, EnvironmentSample world)
        {
            for (int i = 0; i < 40; i++) simulation.StepExact(1, world);
            return simulation.Solver.LastRequiredSubsteps;
        }


        private static ThermalSimulation Conduction(float conductivity, float clock)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Block(conductivity, 4000f, false), Vector3I.Zero);
            builder.Place(Block(conductivity, 40f, false), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Settings(clock), 293.15f);
            simulation.RebuildAll();
            return simulation;
        }


        private static ThermalSimulation Convection(float conductivity, float clock, int frequency = 0)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Block(conductivity, 40f), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(Settings(clock, frequency), 293.15f);
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void AConductionLimitedBlockCostsConductivityTimesTheClock()
        {

            float shipped = Demand(Conduction(1f, ShippedClock), Worlds.Shadow());

            float candidate = Demand(Conduction(CandidateConductivity, CandidateClock),
                Worlds.Shadow());

            Assert.True(shipped > 0f);
            Assert.Equal(CandidateConductivity * CandidateClock / ShippedClock,
                candidate / shipped, 2);
        }

        [Fact]

        public void AConvectionLimitedBlockCostsTheClockAloneAndTheRetuneMakesItCheaper()
        {
            EnvironmentSample air = Worlds.Flight(ThickAir, 200f);


            float shipped = Demand(Convection(1f, ShippedClock), air);

            float candidate = Demand(Convection(CandidateConductivity, CandidateClock), air);

            Assert.True(shipped > 0f);
            Assert.True(candidate < shipped,
                "a block whose stiffness is the air around it went " + shipped + " to " + candidate
                + " substeps; the G6 argument for shipping the retune rests on that falling");

            Assert.Equal(CandidateClock / ShippedClock, candidate / shipped, 2);
        }

        [Fact]

        public void ConductivityDoesNotReachTheConvectiveTermAtAll()
        {
            EnvironmentSample air = Worlds.Flight(ThickAir, 200f);


            float plain = Demand(Convection(1f, ShippedClock), air);

            float conductive = Demand(Convection(CandidateConductivity, ShippedClock), air);

            Assert.Equal(plain, conductive, 3);
        }

        [Fact]

        public void AirIsWhereTheSubstepBudgetGoes()
        {

            float vacuum = Demand(Convection(1f, ShippedClock), Worlds.Shadow());

            float air = Demand(Convection(1f, ShippedClock), Worlds.Flight(ThickAir, 200f));

            Assert.True(air > vacuum * 4f,
                "the same block demanded " + air + " substeps in air against " + vacuum
                + " in vacuum; if convection has stopped being the term that sets the cost then"
                + " every atmospheric figure in balance.md needs re-reading");
        }

        [Fact]

        public void TheShippedClockSpendsMostOfTheCapOnAirAndTheRetunePullsItBack()
        {

            ThermalSettings shipped = new ThermalSettings().Derive();
            EnvironmentSample air = Worlds.Flight(ThickAir, 200f);


            float before = Demand(Convection(1f, ShippedClock), air);

            float after = Demand(Convection(CandidateConductivity, CandidateClock), air);

            Assert.True(before > shipped.MaxSubsteps / 4f,
                "a light exposed block at 200 m/s in thick air demanded " + before
                + " substeps against a cap of " + shipped.MaxSubsteps + ", which is a small enough"
                + " share that air is no longer where the budget goes");

            Assert.True(after < before / 2f,
                "the retune took the air demand from " + before + " to " + after
                + ", and the G6 argument for shipping it is that this roughly halves");
        }

        [Fact]

        public void DemandIsExactlyProportionalToTheStep()
        {
            EnvironmentSample air = Worlds.Flight(ThickAir, 200f);


            float quarterSecond = Demand(Convection(1f, ShippedClock, 4), air);

            float eighthSecond = Demand(Convection(1f, ShippedClock, 8), air);

            Assert.True(eighthSecond > 0f);
            Assert.Equal(2.0, quarterSecond / eighthSecond, 2);
        }

        [Fact]

        public void TheShippedPairIsNotOverTheCapInVacuum()
        {

            ThermalSettings shipped = new ThermalSettings().Derive();


            float vacuum = Demand(Convection(1f, ShippedClock), Worlds.Shadow());

            Assert.True(vacuum < shipped.MaxSubsteps / 4f,
                "the rig block demanded " + vacuum + " substeps in vacuum, which is close enough to"
                + " the cap that the atmospheric reading is not isolating the air");
        }
    }
}

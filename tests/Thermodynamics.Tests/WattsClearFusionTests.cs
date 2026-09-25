using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The watts row is written by the environment pass rather than zeroed first, and the only thing
    /// that makes that safe is an ordering claim in three parts: the pass runs first, reaches
    /// **every** node including buried ones and a switched-off environment, and still does so when
    /// sliced across frames.
    ///
    /// <para>
    /// The assertion is **bit-identical** rather than close enough (`D8`), because a node whose watts
    /// were not reset integrates a doubled source term — a slow drift found in a save months later.
    /// A memset that is *nearly* redundant is a correctness defect at any price, whatever it saves.
    /// See benchmarks.md, The watts row is written.
    /// </para>
    /// </summary>
    public class WattsClearFusionTests
    {

        private static ThermalSimulation Build(bool fused)
        {
            ThermalSimulation simulation = Hulls.Driven();
            simulation.Solver.FuseWattsClear = fused;
            return simulation;
        }


        private static void AssertIdentical(ThermalSimulation a, ThermalSimulation b, string what)
        {
            SolverAb.AssertIdentical(
                SolverAb.Temperatures(a), SolverAb.Temperatures(b), what,
                "with the row cleared first", "with the environment pass writing it");
        }

        [Fact]

        public void WritingTheWattsRowIsBitIdenticalInFlight()
        {

            ThermalSimulation cleared = Build(false);

            ThermalSimulation fused = Build(true);

            EnvironmentSample sample = Worlds.Ab.EveryTermLive();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            AssertIdentical(cleared, fused, "flight");
        }

        [Fact]

        public void WritingTheWattsRowIsBitIdenticalInAnAtmosphere()
        {

            ThermalSimulation cleared = Build(false);

            ThermalSimulation fused = Build(true);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            AssertIdentical(cleared, fused, "atmosphere");
        }

        [Fact]

        public void WritingTheWattsRowIsBitIdenticalInVacuum()
        {

            ThermalSimulation cleared = Build(false);

            ThermalSimulation fused = Build(true);

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            AssertIdentical(cleared, fused, "vacuum");
        }

        [Fact]

        public void WritingTheWattsRowIsBitIdenticalWithTheEnvironmentOff()
        {
            ThermalSettings settings = Hulls.Uncapped();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.Derive();

            ThermalSimulation cleared = Hulls.Driven(settings);
            ThermalSimulation fused = Hulls.Driven(settings);
            cleared.Solver.FuseWattsClear = false;
            fused.Solver.FuseWattsClear = true;

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            AssertIdentical(cleared, fused, "environment off");
        }

        [Fact]

        public void WritingTheWattsRowIsBitIdenticalWithNothingToWrite()
        {
            ThermalSettings settings = Hulls.Uncapped();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableWasteHeat = false;
            settings.Derive();

            ThermalSimulation cleared = Hulls.Driven(settings);
            ThermalSimulation fused = Hulls.Driven(settings);
            cleared.Solver.FuseWattsClear = false;
            fused.Solver.FuseWattsClear = true;

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            AssertIdentical(cleared, fused, "nothing to write");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(97)]
        [InlineData(5000)]

        public void WritingTheWattsRowSurvivesTheStepBeingSpread(int budget)
        {

            ThermalSimulation whole = Build(false);

            ThermalSimulation spread = Build(true);

            EnvironmentState state = EnvironmentSolver.Solve(
                whole.Settings, whole.Planet,
                Worlds.Ab.MildAtmosphere());

            for (int step = 0; step < 8; step++)
            {
                whole.Solver.Step(whole.Settings.StepSeconds, state);

                spread.Solver.BeginStep(spread.Settings.StepSeconds, state);
                while (!spread.Solver.AdvanceStep(budget)) { }
            }

            AssertIdentical(whole, spread, "spread over " + budget);
        }

        [Fact]

        public void TheDiagnosticsAgreeAsWell()
        {

            ThermalSimulation cleared = Build(false);

            ThermalSimulation fused = Build(true);

            cleared.Solver.CollectDiagnostics = true;
            fused.Solver.CollectDiagnostics = true;

            EnvironmentSample sample = Worlds.Ab.EveryTermLive();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            SolverAb.AssertIdentical(
                SolverAb.Diagnostics(cleared), SolverAb.Diagnostics(fused), "diagnostics",
                "with the row cleared first", "with the environment pass writing it");
        }

        [Fact]

        public void TheGridHeatTotalsAgree()
        {

            ThermalSimulation cleared = Build(false);

            ThermalSimulation fused = Build(true);

            EnvironmentSample sample = Worlds.Ab.EveryTermLive();

            cleared.StepExact(40, sample);
            fused.StepExact(40, sample);

            Assert.Equal(cleared.Solver.LastHeatGainWatts, fused.Solver.LastHeatGainWatts);
            Assert.Equal(cleared.Solver.LastEnvironmentWatts, fused.Solver.LastEnvironmentWatts);

            Assert.True(fused.Solver.LastHeatGainWatts > 0f,
                "the driven hull generated no heat, so the totals prove nothing");
        }
    }
}

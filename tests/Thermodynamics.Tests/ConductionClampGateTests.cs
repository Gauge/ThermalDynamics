using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The conduction pass skips the overshoot clamp on any step where it cannot bind, and the only
    /// thing that makes that safe is that running it would have produced **the same bits** (`D8`) — a
    /// gate that is nearly right drops clamping on a grid that needed it and diverges silently.
    ///
    /// <para>
    /// The last two tests are what keep the rest honest: without them a gate that never engaged, or
    /// one that always did, would pass every equivalence assertion above (`E8`).
    /// See benchmarks.md, The overshoot clamp A/B.
    /// </para>
    /// </summary>
    public class ConductionClampGateTests
    {

        private static ThermalSimulation Build(bool gate, int maxSubsteps)
        {
            ThermalSimulation simulation = Hulls.Driven(Hulls.Uncapped(maxSubsteps));
            simulation.Solver.GateConductionClamp = gate;
            return simulation;
        }


        private static void AssertIdentical(ThermalSimulation always, ThermalSimulation gated, string what)
        {
            SolverAb.AssertIdentical(
                SolverAb.Temperatures(always), SolverAb.Temperatures(gated), what,
                "with the clamp always on", "with it gated");
        }

        [Fact]

        public void SkippingTheClampIsBitIdenticalWhenTheGridIsResolved()
        {

            ThermalSimulation always = Build(gate: false, maxSubsteps: 4096);

            ThermalSimulation gated = Build(gate: true, maxSubsteps: 4096);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            always.StepExact(40, sample);
            gated.StepExact(40, sample);

            Assert.False(gated.Solver.ConductionClampLive);
            AssertIdentical(always, gated, "resolved");
        }

        [Fact]

        public void TheClampStillRunsWhenTheSubstepCountWasRefused()
        {

            ThermalSimulation always = Build(gate: false, maxSubsteps: 1);

            ThermalSimulation gated = Build(gate: true, maxSubsteps: 1);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            always.StepExact(40, sample);
            gated.StepExact(40, sample);

            Assert.True(gated.Solver.ConductionClampLive);
            AssertIdentical(always, gated, "clamped");
        }

        [Fact]

        public void SkippingTheClampIsBitIdenticalInVacuum()
        {

            ThermalSimulation always = Build(gate: false, maxSubsteps: 4096);

            ThermalSimulation gated = Build(gate: true, maxSubsteps: 4096);

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            always.StepExact(40, sample);
            gated.StepExact(40, sample);

            AssertIdentical(always, gated, "vacuum");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(24)]
        [InlineData(64)]

        public void TheGateNeverChangesTheAnswerAtAnySubstepCap(int cap)
        {

            ThermalSimulation always = Build(gate: false, maxSubsteps: cap);

            ThermalSimulation gated = Build(gate: true, maxSubsteps: cap);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            always.StepExact(20, sample);
            gated.StepExact(20, sample);

            AssertIdentical(always, gated, "cap " + cap);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(97)]
        [InlineData(5000)]

        public void TheGateSurvivesTheStepBeingSpread(int budget)
        {

            ThermalSimulation whole = Build(gate: false, maxSubsteps: 4096);

            ThermalSimulation spread = Build(gate: true, maxSubsteps: 4096);

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

        public void TheGateEngagesOnAResolvedGridAndNotOnAStiffOne()
        {

            ThermalSimulation resolved = Build(gate: true, maxSubsteps: 4096);

            ThermalSimulation stiff = Build(gate: true, maxSubsteps: 1);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            resolved.StepExact(4, sample);
            stiff.StepExact(4, sample);

            Assert.False(resolved.Solver.ConductionClampLive);
            Assert.True(stiff.Solver.ConductionClampLive);
        }

        [Fact]

        public void TheSettingStillWins()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubsteps = 1;
            settings.MaxElementVisitsPerStep = 0;
            settings.ClampConductionOvershoot = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 500));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();
            Census.DriveCensus(simulation);
            LoadBenchmarks.SeedSpread(simulation);

            simulation.StepExact(4, Worlds.Ab.MildAtmosphere());

            Assert.False(simulation.Solver.ConductionClampLive);
        }
    }
}

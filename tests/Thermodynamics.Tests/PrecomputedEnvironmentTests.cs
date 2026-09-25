using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The environment pass reuses per-node terms across the substeps of a step instead of
    /// recomputing them, and the only thing that makes that safe is that recomputing them would
    /// have produced the same bits.
    ///
    /// <para>
    /// Solar gain, friction and the convection coefficient are functions of exposure, area,
    /// emissivity and the sun and wind directions — none of which a substep changes, because a
    /// substep changes temperature and nothing else. The relaxation factor is
    /// <c>mass / (h * conductance)</c>, whose three inputs are fixed for the length of a step.
    /// So sixteen substeps were doing the same arithmetic sixteen times, including a float divide
    /// per node per substep.
    /// </para>
    ///
    /// <para>
    /// The assertion is therefore <b>bit-identical</b>, not "close enough". Anything less means
    /// something in that list does move between substeps, and a cache that is nearly right is
    /// worse than none: it would drift silently, on exactly the grids nobody benchmarks.
    /// </para>
    /// </summary>
    public class PrecomputedEnvironmentTests
    {

        private static ThermalSimulation Build(bool precompute)
        {
            ThermalSimulation simulation = Hulls.Driven();
            simulation.Solver.PrecomputeEnvironment = precompute;
            return simulation;
        }


        private static void AssertIdentical(ThermalSimulation a, ThermalSimulation b, string what)
        {
            SolverAb.AssertIdentical(
                SolverAb.Temperatures(a), SolverAb.Temperatures(b), what,
                "without the terms reused", "with them reused");
        }

        [Fact]

        public void ReusingThePerStepTermsIsBitIdenticalInAnAtmosphere()
        {

            ThermalSimulation recomputed = Build(false);

            ThermalSimulation reused = Build(true);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            recomputed.StepExact(40, sample);
            reused.StepExact(40, sample);

            AssertIdentical(recomputed, reused, "atmosphere");
        }

        [Fact]

        public void ReusingThePerStepTermsIsBitIdenticalInFlight()
        {

            ThermalSimulation recomputed = Build(false);

            ThermalSimulation reused = Build(true);

            EnvironmentSample sample = Worlds.Ab.EveryTermLive();

            recomputed.StepExact(40, sample);
            reused.StepExact(40, sample);

            Assert.True(recomputed.Solver.Nodes[0].LastFrictionWatts >= 0f);
            AssertIdentical(recomputed, reused, "flight");
        }

        [Fact]

        public void ReusingThePerStepTermsIsBitIdenticalInVacuum()
        {

            ThermalSimulation recomputed = Build(false);

            ThermalSimulation reused = Build(true);

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            recomputed.StepExact(40, sample);
            reused.StepExact(40, sample);

            AssertIdentical(recomputed, reused, "vacuum");
        }

        [Fact]

        public void TheCacheIsInvalidatedWhenTheSelfShadowPassPublishes()
        {

            ThermalSimulation recomputed = Build(false);

            ThermalSimulation reused = Build(true);

            recomputed.Solver.SunLitBudget = 64;
            reused.Solver.SunLitBudget = 64;
            recomputed.Solver.SunShadowBudget = 128;
            reused.Solver.SunShadowBudget = 128;

            for (int i = 0; i < 30; i++)
            {
                float angle = i * 0.35f;

                Vector3 sun = new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0.2f);

                EnvironmentSample sample = Worlds.Space(sun);
                recomputed.StepExact(1, sample);
                reused.StepExact(1, sample);
            }

            AssertIdentical(recomputed, reused, "moving sun");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(97)]
        [InlineData(5000)]

        public void ReusingThePerStepTermsSurvivesTheStepBeingSpread(int budget)
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
    }
}

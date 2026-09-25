using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The per-mechanism watt figures are written on the last substep of a step and on no other.
    ///
    /// <para>
    /// Each substep overwrites what the one before it wrote, and every consumer — the telemetry
    /// report, the crosshair readout, the debug overlay, the mod API — reads between steps. So
    /// only the last substep's writes were ever observed, and the earlier ones were five stores
    /// into a node object per node and two into another per link, discarded by the next substep.
    /// A step at nineteen substeps did that eighteen times for nothing, and switching diagnostics
    /// on nearly doubled the step: 3.93 ms to 7.51 ms on a 32,800-block hull.
    /// </para>
    ///
    /// <para>
    /// The assertion is bit-identical against writing on every substep, because "the last substep's
    /// figures" has to mean exactly that. Anything less means the batching changed which substep is
    /// being reported, which would make every watt figure in a telemetry dump describe a different
    /// moment than the one it claims.
    /// </para>
    /// </summary>
    public class DiagnosticBatchingTests
    {

        private static ThermalSimulation Build(bool everySubstep, int maxSubsteps)
        {
            ThermalSimulation simulation = Hulls.Driven(Hulls.Uncapped(maxSubsteps));
            simulation.Solver.CollectDiagnostics = true;
            simulation.Solver.DiagnosticsOnEverySubstep = everySubstep;
            return simulation;
        }


        private static void AssertIdentical(ThermalSimulation every, ThermalSimulation last, string what)
        {
            SolverAb.AssertIdentical(
                SolverAb.Diagnostics(every), SolverAb.Diagnostics(last), what,
                "written on every substep", "written on the last substep",
                SolverAb.Mechanisms);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(4096)]

        public void OnlyTheLastSubstepIsPublishedAndItIsTheSameOne(int maxSubsteps)
        {

            ThermalSimulation every = Build(everySubstep: true, maxSubsteps: maxSubsteps);

            ThermalSimulation last = Build(everySubstep: false, maxSubsteps: maxSubsteps);

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();

            every.StepExact(20, sample);
            last.StepExact(20, sample);

            AssertIdentical(every, last, "atmosphere at " + maxSubsteps);
        }

        [Fact]

        public void TheSameHoldsInVacuum()
        {

            ThermalSimulation every = Build(everySubstep: true, maxSubsteps: 4096);

            ThermalSimulation last = Build(everySubstep: false, maxSubsteps: 4096);

            EnvironmentSample sample = Worlds.Ab.SunlitVacuum();

            every.StepExact(20, sample);
            last.StepExact(20, sample);

            AssertIdentical(every, last, "vacuum");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(97)]
        [InlineData(5000)]

        public void BatchingSurvivesTheStepBeingSpread(int budget)
        {

            ThermalSimulation whole = Build(everySubstep: true, maxSubsteps: 4096);

            ThermalSimulation spread = Build(everySubstep: false, maxSubsteps: 4096);

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

        public void NothingIsPublishedWhenDiagnosticsAreOff()
        {

            ThermalSimulation simulation = Build(everySubstep: false, maxSubsteps: 4096);
            simulation.Solver.CollectDiagnostics = false;

            simulation.StepExact(20, Worlds.Ab.MildAtmosphere());

            float[] published = SolverAb.Diagnostics(simulation);
            for (int i = 0; i < published.Length; i++)
            {
                Assert.Equal(0f, published[i]);
            }
        }
    }
}

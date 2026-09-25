using Thermodynamics.Core;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A step has three terms, not two.
    ///
    /// <para>
    /// The report fits a step as fixed work plus one lot of per-substep work, through two points at
    /// known substep counts. That fit has nowhere to put the first substep, which is the one that
    /// fills the per-step environment rows every later substep reads — so it charges the fill to
    /// the intercept, and the intercept gets read as the prologue and write-back. Measured on a
    /// 32,800-block hull in flight, the fill is about half of it.
    /// </para>
    ///
    /// <para>
    /// The timing lives in `bench report`. What is pinned here is the structural fact underneath
    /// it, which a stopwatch cannot assert: the rows are filled once a step however many substeps
    /// the step is cut into, and once a substep when the cache is switched off.
    /// </para>
    /// </summary>
    public class StepTermsTests
    {

        private static ThermalSimulation Hull(int maxSubsteps)
        {
            return Hulls.Driven(Hulls.Uncapped(maxSubsteps), 600);
        }


        private static EnvironmentSample Flight()
        {
            return Worlds.Ab.EveryTermLive();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(32)]

        public void TheEnvironmentRowsAreFilledOnceAStep(int maxSubsteps)
        {

            ThermalSimulation simulation = Hull(maxSubsteps);
            simulation.StepExact(1, Flight());

            simulation.Work.Reset();
            simulation.StepExact(6, Flight());

            Assert.Equal(6, simulation.Work.SolverSteps);
            Assert.True(simulation.Work.SolverSubsteps >= 6);
            Assert.Equal(6, simulation.Work.EnvironmentRowFills);
        }

        [Fact]

        public void SwitchingTheCacheOffFillsOnEverySubstep()
        {

            ThermalSimulation simulation = Hull(32);
            simulation.Solver.PrecomputeEnvironment = false;
            simulation.StepExact(1, Flight());

            simulation.Work.Reset();
            simulation.StepExact(4, Flight());

            Assert.True(simulation.Work.SolverSubsteps > 4,
                "the hull took one substep a step, so this asserts nothing");
            Assert.Equal(simulation.Work.SolverSubsteps, simulation.Work.EnvironmentRowFills);
        }

        [Fact]

        public void AFillSpreadAcrossFramesIsStillOneFill()
        {

            ThermalSimulation simulation = Hull(32);
            EnvironmentState state = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, Flight());

            simulation.Solver.Step(simulation.Settings.StepSeconds, state);

            simulation.Work.Reset();
            simulation.Solver.BeginStep(simulation.Settings.StepSeconds, state);
            while (!simulation.Solver.AdvanceStep(97)) { }

            Assert.True(simulation.Work.StepAdvances > 1, "the step landed in one piece");
            Assert.Equal(1, simulation.Work.EnvironmentRowFills);
        }
    }
}

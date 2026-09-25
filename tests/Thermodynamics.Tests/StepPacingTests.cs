using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// How a step is spread across the frames of its window.
    ///
    /// <para>
    /// Pacing exists so one grid's step does not arrive as a lump. It has a floor because
    /// re-entering the resumable stage machine costs the same whatever it carries, and a grid
    /// small enough that its whole step is a dozen element visits was paying that entry on every
    /// frame of its window. The floor must remove the entries without moving the rate: a grid that
    /// completes four steps a second before must complete four after.
    /// </para>
    /// </summary>
    public class StepPacingTests
    {
        private const float Frame = 1f / 60f;


        private static ThermalSimulation Ship(int side)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                MaxSubsteps = 64,
                MaxSubstepsPerBlock = 0,
                MaxElementVisitsPerStep = 0,
            };
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(side, side, side));

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            while (simulation.HasPendingWork) simulation.Update(Frame, Worlds.Shadow());
            return simulation;
        }


        private static SimulationWork RunSeconds(ThermalSimulation simulation, int seconds)
        {
            simulation.Work.Reset();
            for (int frame = 0; frame < 60 * seconds; frame++)
            {
                simulation.Update(Frame, Worlds.Shadow());
            }

            return simulation.Work;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(8)]

        public void TheStepRateIsWhatFrequencySays(int side)
        {

            SimulationWork work = RunSeconds(Ship(side), 4);

            Assert.InRange(work.SolverSteps, 15, 17);
        }

        [Fact]

        public void ASmallGridsStepArrivesInOnePiece()
        {

            SimulationWork work = RunSeconds(Ship(1), 4);

            Assert.True(work.SolverSteps > 0);
            Assert.True(work.StepAdvances <= work.SolverSteps + 1,
                work.StepAdvances + " advances for " + work.SolverSteps + " steps");
        }

        [Fact]

        public void ALargeGridsStepIsStillSpreadOverItsWindow()
        {

            SimulationWork work = RunSeconds(Ship(16), 4);

            Assert.True(work.SolverSteps > 0);
            Assert.True(work.StepAdvances >= work.SolverSteps * 5,
                work.StepAdvances + " advances for " + work.SolverSteps + " steps");
        }

        [Fact]

        public void BankedCreditIsSpentRatherThanLost()
        {

            ThermalSimulation small = Ship(1);

            ThermalSimulation large = Ship(8);

            RunSeconds(small, 4);
            RunSeconds(large, 4);

            Assert.Equal(large.SimulatedSecondsRun, small.SimulatedSecondsRun, 3);
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Why grids need a step phase, and that it holds.
    ///
    /// <para>
    /// Step credit accrues at the same fraction of a step per frame for every grid, whatever its
    /// size, so grids that start together reach every step boundary on the same frame. Each
    /// boundary carries unsliced work (the environment sample, the step prologue, after-step, and
    /// all of a small grid's step), so a fleet stacks it into one hitch at the step rate.
    /// </para>
    /// </summary>
    public class StepPhaseTests
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

        /// <summary>Abandons both grids' steps on one frame, as a settings rebuild does, then
        /// returns the frames on which each began a step over four seconds.</summary>
        private static List<int>[] StepStarts(ThermalSimulation a, ThermalSimulation b)
        {
            a.Update(Frame, Worlds.Shadow());
            b.Update(Frame, Worlds.Shadow());
            a.Solver.AbandonStep();
            b.Solver.AbandonStep();

            var starts = new[] { new List<int>(), new List<int>() };
            var grids = new[] { a, b };
            for (int frame = 0; frame < 240; frame++)
            {
                for (int g = 0; g < 2; g++)
                {
                    long before = grids[g].Work.SolverSteps;
                    grids[g].Update(Frame, Worlds.Shadow());
                    if (grids[g].Work.SolverSteps != before) starts[g].Add(frame);
                }
            }
            return starts;
        }

        [Fact]
        public void GridsOfDifferentSizesStartedTogetherStayInStep()
        {
            List<int>[] starts = StepStarts(Ship(2), Ship(10));

            Assert.InRange(starts[0].Count, 15, 17);
            Assert.Equal(starts[0], starts[1]);
        }

        [Fact]
        public void APhaseKeepsStepStartsApartAcrossAnAbandonWithoutCostingRate()
        {
            ThermalSimulation small = Ship(2), large = Ship(10);
            large.StepPhaseFrames = 7;

            List<int>[] starts = StepStarts(small, large);

            Assert.InRange(starts[0].Count, 15, 17);
            Assert.InRange(starts[1].Count, 15, 17);
            foreach (int frame in starts[1]) Assert.DoesNotContain(frame, starts[0]);
        }
    }
}

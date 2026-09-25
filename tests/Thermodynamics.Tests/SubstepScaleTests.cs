using Thermodynamics.Core;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A substep count is a number about a step, and quoting one without the step it belongs to is
    /// the mistake this suite exists to make expensive.
    ///
    /// <para>
    /// A step of <c>dt</c> simulated seconds needs <c>dt · r_max / safety</c> substeps, so demand
    /// is <b>proportional to the step length</b>. Halve the step and every figure halves with it:
    /// what the grid asks for, which blocks a given cap reaches, and what that cap is worth.
    /// </para>
    ///
    /// <para>
    /// The cap tables in stiffness.md were taken at <c>Frequency 4</c>
    /// and read for a year as if they described the shipped default, which had moved to
    /// <c>Frequency 8</c>. Every speed-up on the page was therefore about twice what a default
    /// world would see, and the backlog item arguing for a shipped cap was reasoned from it. The
    /// tables were not wrong; they were unlabelled, which turned out to be the same thing.
    /// </para>
    ///
    /// <para>
    /// Nothing caught it because nothing asserted the relationship. These tests do, and they are
    /// cheap — no stepping, only the estimate — so the invariant that makes a table readable is
    /// checked on every run.
    /// </para>
    /// </summary>
    public class SubstepScaleTests
    {

        private static ThermalSimulation Hull(int frequency, int cap = 0, int blocks = 4000)
        {
            ThermalSettings settings = new ThermalSettings { Frequency = frequency };
            settings.MaxSubstepsPerBlock = cap;

            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();
            return simulation;
        }


        private static float Demand(ThermalSimulation simulation)
        {
            return simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(4, 8)]
        [InlineData(8, 16)]

        public void DemandIsProportionalToTheStepLength(int slow, int fast)
        {

            float slower = Demand(Hull(slow));

            float faster = Demand(Hull(fast));

            Assert.True(slower > 0f, "the census hull asked for no substeps at Frequency " + slow
                + ", so there is no proportionality to test");

            float ratio = slower / faster;
            Assert.True(ratio > 1.98f && ratio < 2.02f,
                "Frequency " + slow + " asked for " + slower.ToString("n3")
                + " substeps and Frequency " + fast + " for " + faster.ToString("n3")
                + " — a ratio of " + ratio.ToString("n4") + " where halving the step should halve"
                + " the demand exactly. A substep count is no longer proportional to the step it"
                + " is counted against, which makes every cap table in the docs unreadable.");
        }

        [Theory]
        [InlineData(2, 4)]
        [InlineData(1, 2)]

        public void ACapMeansTheSameFloorAtTwiceTheRateAndTwiceTheCap(int fastCap, int slowCap)
        {

            ThermalSimulation fast = Hull(8, fastCap);

            ThermalSimulation slow = Hull(4, slowCap);

            Demand(fast);
            Demand(slow);

            Assert.Equal(fast.Solver.FlooredNodes, slow.Solver.FlooredNodes);

            Assert.True(fast.Solver.FlooredNodes > 0,
                "cap " + fastCap + " at Frequency 8 raised no blocks at all, so the two sides agree"
                + " on nothing rather than on a floor");
        }

        [Fact]

        public void ACapAboveWhatTheHullAsksForDoesNothingAtTheShippedRate()
        {

            ThermalSimulation uncapped = Hull(8);

            float demand = Demand(uncapped);

            Assert.InRange(demand, 2f, 4f);


            ThermalSimulation capped = Hull(8, 4);

            Assert.Equal(demand, Demand(capped), 3);
            Assert.Equal(0, capped.Solver.FlooredNodes);
        }
    }
}

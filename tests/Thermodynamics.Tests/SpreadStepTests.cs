using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A step spread across many frames must produce exactly the same answer as the same step run
    /// in one go.
    ///
    /// This is the whole safety argument for spreading, and it is not a small claim. The original
    /// mod also spread its work across frames and that was a defect: it advanced <em>different
    /// blocks on different frames</em>, so a block's neighbours could be a frame ahead of or
    /// behind it, the result depended on iteration order, and it had to alternate sweep direction
    /// every pass to keep the bias fair.
    ///
    /// What is spread here is the arithmetic of a single substep, never the simulation. Every
    /// exchange is computed from the temperatures at the start of the substep and summed into a
    /// watts buffer that is applied to every node together at the end — and a sum has the same
    /// value however many pieces it is computed in. So the assertion is not "close enough", it is
    /// <b>bit-identical</b>, and anything less means a slice is reading state another slice has
    /// already moved.
    /// </summary>
    [Trait("speed", "slow")]
    public class SpreadStepTests
    {

        private static ThermalSimulation Build()
        {
            GridBuilder builder = GridBuilder.Large();

            int index = 0;
            foreach (Vector3I cell in GridShapes.Ship(fuselageLength: 24, fuselageWidth: 7, bulkheadSpacing: 6))
            {
                builder.Place((index++ % 8) == 0 ? Catalog.Grating() : Catalog.HeavyArmor(), cell);
            }

            BlockInstance occupant = builder.Grid.GetAtCell(new Vector3I(3, 3, 6));
            if (occupant != null)
            {
                builder.Grid.Remove(occupant);
                builder.Placed.Remove(occupant);
            }
            builder.Place(Catalog.Reactor(), new Vector3I(3, 3, 6))
                   .Producing(8f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());
            return simulation;
        }


        private static void Seed(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3I at = nodes[i].Block.Position;
                nodes[i].Temperature = 250f + 40f * ((at.X * 7 + at.Y * 13 + at.Z * 23) % 13);
            }
        }


        private static EnvironmentSample Sky()
        {
            return Worlds.PlanetSurface(0.6f, timeOfDay: 0.4f, windSpeed: 15f);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(97)]
        [InlineData(1000)]

        public void ProfilingAStepInFlightDoesNotChangeIt(int budget)
        {

            ThermalSimulation clean = Build();

            ThermalSimulation observed = Build();
            Seed(clean);
            Seed(observed);

            EnvironmentState state = EnvironmentSolver.Solve(clean.Settings, clean.Planet, Sky());

            clean.Solver.BeginStep(clean.Settings.StepSeconds, state);
            while (!clean.Solver.AdvanceStep(budget)) { }

            observed.Solver.BeginStep(observed.Settings.StepSeconds, state);

            int profiles = 0;
            while (!observed.Solver.AdvanceStep(budget))
            {
                observed.Solver.ProfileSubsteps();
                profiles++;
            }

            Assert.True(profiles > 0, "the step finished in one slice, so nothing was observed mid-step");

            SolverAb.AssertIdentical(
                SolverAb.Temperatures(clean), SolverAb.Temperatures(observed),
                profiles + " mid-step profiles", "unobserved", "observed");

            for (int i = 0; i < clean.Solver.Nodes.Count; i++)
            {
                Assert.Equal(clean.Solver.Nodes[i].LastDeltaTemperature,
                    observed.Solver.Nodes[i].LastDeltaTemperature);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(64)]
        [InlineData(1000)]

        public void ASpreadStepIsBitIdenticalToAWholeOne(int budget)
        {

            ThermalSimulation whole = Build();

            ThermalSimulation spread = Build();
            Seed(whole);
            Seed(spread);

            EnvironmentState state = EnvironmentSolver.Solve(whole.Settings, whole.Planet, Sky());

            whole.Solver.Step(whole.Settings.StepSeconds, state);

            Assert.True(spread.Solver.BeginStep(spread.Settings.StepSeconds, state));
            int slices = 0;
            while (!spread.Solver.AdvanceStep(budget))
            {
                if (++slices > 10000000) throw new InvalidOperationException("step never finished");
            }

            SolverAb.AssertIdentical(
                SolverAb.Temperatures(whole), SolverAb.Temperatures(spread),
                "spread over " + slices + " slices", "run whole", "run in slices");
        }

        [Fact]

        public void ManySpreadStepsStayIdenticalToManyWholeOnes()
        {

            ThermalSimulation whole = Build();

            ThermalSimulation spread = Build();
            Seed(whole);
            Seed(spread);

            EnvironmentState state = EnvironmentSolver.Solve(whole.Settings, whole.Planet, Sky());
            float step = whole.Settings.StepSeconds;

            for (int s = 0; s < 30; s++)
            {
                whole.Solver.Step(step, state);

                spread.Solver.BeginStep(step, state);
                while (!spread.Solver.AdvanceStep(97)) { }
            }

            SolverAb.AssertIdentical(
                SolverAb.Temperatures(whole), SolverAb.Temperatures(spread),
                "thirty steps", "run whole", "run in slices");

            Assert.Equal(whole.Solver.StepCount, spread.Solver.StepCount);
            Assert.Equal(whole.Solver.LastSubsteps, spread.Solver.LastSubsteps);
        }

        [Fact]

        public void ThePublishedDeltaDescribesTheWholeStep()
        {

            ThermalSimulation whole = Build();

            ThermalSimulation spread = Build();
            Seed(whole);
            Seed(spread);

            EnvironmentState state = EnvironmentSolver.Solve(whole.Settings, whole.Planet, Sky());

            whole.Solver.Step(whole.Settings.StepSeconds, state);
            spread.Solver.BeginStep(spread.Settings.StepSeconds, state);
            while (!spread.Solver.AdvanceStep(11)) { }

            for (int i = 0; i < whole.Solver.Nodes.Count; i++)
            {
                Assert.True(whole.Solver.Nodes[i].LastDeltaTemperature
                    .Equals(spread.Solver.Nodes[i].LastDeltaTemperature),
                    "block " + i + " reported a different change over the step");
            }
        }

        [Fact]

        public void EventsRaisedDuringASpreadStepAllSurvive()
        {

            ThermalSimulation whole = Build();

            ThermalSimulation spread = Build();

            foreach (ThermalSimulation simulation in new[] { whole, spread })
            {
                IList<ThermalNode> nodes = simulation.Solver.Nodes;
                for (int i = 0; i < nodes.Count; i++) nodes[i].Temperature = 1500f;
            }

            EnvironmentState state = EnvironmentSolver.Solve(whole.Settings, whole.Planet, Sky());

            whole.Solver.Step(whole.Settings.StepSeconds, state);
            spread.Solver.BeginStep(spread.Settings.StepSeconds, state);
            while (!spread.Solver.AdvanceStep(5)) { }

            Assert.True(whole.Solver.Overheats.Count > 0, "the fixture should be overheating");
            Assert.Equal(whole.Solver.Overheats.Count, spread.Solver.Overheats.Count);
        }

        [Fact]

        public void AnAbandonedStepLeavesTheGridWhereTheLastSubstepLeftIt()
        {

            ThermalSimulation simulation = Build();
            Seed(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, Sky());

            simulation.Solver.BeginStep(simulation.Settings.StepSeconds, state);

            simulation.Solver.AdvanceStep(simulation.Solver.StepWorkUnits / 3);
            Assert.True(simulation.Solver.StepInFlight);

            float[] before = SolverAb.Temperatures(simulation);
            simulation.Solver.AbandonStep();

            Assert.False(simulation.Solver.StepInFlight);
            SolverAb.AssertIdentical(before, SolverAb.Temperatures(simulation),
                "abandoning a step", "before", "after");
        }


        [Fact]

        public void EveryFrameDoesItsShareAndNoFrameDoesTheLot()
        {

            ThermalSimulation simulation = Build();
            Seed(simulation);

            const float frame = 1f / 60f;
            const int frames = 120;


            List<long> perFrame = new List<long>();
            for (int i = 0; i < frames; i++)
            {
                simulation.Update(frame, Sky());
                perFrame.Add(simulation.Solver.LastAdvanceWork);
            }

            long total = 0;
            long worst = 0;
            int idle = 0;
            for (int i = 0; i < perFrame.Count; i++)
            {
                total += perFrame[i];
                if (perFrame[i] > worst) worst = perFrame[i];
                if (perFrame[i] == 0) idle++;
            }

            double mean = total / (double)frames;

            Assert.True(total > 0, "the grid should have simulated");
            Assert.Equal(0, idle);
            Assert.True(worst < mean * 2d,
                "the busiest frame did " + worst + " units against a mean of " + mean.ToString("n0")
                + "; a step landing whole on one frame would be about fifteen times the mean");
        }

        [Theory]
        [InlineData(1, 1f)]
        [InlineData(4, 1f)]
        [InlineData(10, 1f)]
        [InlineData(4, 2f)]

        public void TheConfiguredRateSurvivesBeingSpread(int frequency, float speed)
        {

            ThermalSimulation simulation = Build();

            simulation.Settings.Frequency = frequency;
            simulation.Settings.SimulationSpeed = speed;
            simulation.Settings.Derive();

            long before = simulation.Solver.StepCount;

            for (int i = 0; i < 120; i++)
            {
                simulation.Update(1f / 60f, Sky());
            }

            long steps = simulation.Solver.StepCount - before;
            long expected = (long)(2f * frequency * speed);

            Assert.True(Math.Abs(steps - expected) <= 1,
                "Frequency " + frequency + " at speed " + speed + " should give about " + expected
                + " steps in two seconds, got " + steps);
        }

        [Fact]

        public void AGridTooSmallToOweAWholeElementPerFrameStillAdvances()
        {

            GridModel grid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings(), grid);
            simulation.AddBlock(new BlockInstance(Catalog.LightArmor(), Vector3I.Zero,
                BlockOrientation.Identity), 900f);
            simulation.RebuildAll();

            long before = simulation.Solver.StepCount;
            for (int i = 0; i < 120; i++) simulation.Update(1f / 60f, Sky());

            Assert.True(simulation.Solver.StepCount - before >= 7,
                "a one-block grid ran " + (simulation.Solver.StepCount - before)
                + " steps in two seconds, and four a second was asked for");
        }

        [Fact]

        public void TheWorkEstimateMatchesWhatTheStepActuallyCosts()
        {

            ThermalSimulation simulation = Build();
            Seed(simulation);

            EnvironmentState state = EnvironmentSolver.Solve(

                simulation.Settings, simulation.Planet, Sky());

            simulation.Solver.BeginStep(simulation.Settings.StepSeconds, state);
            long estimate = simulation.Solver.StepWorkUnits;

            long spent = 0;
            while (!simulation.Solver.AdvanceStep(64)) spent += 64;

            Assert.True(spent <= estimate * 1.5 && spent >= estimate * 0.5,
                "the step estimated " + estimate + " units of work and took about " + spent);
        }
    }
}

using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a step reports about itself, as opposed to what it computes.
    ///
    /// Nothing in the simulation reads these figures, which is exactly why they can drift: they
    /// only ever surface in the cockpit readout, the debug overlay and the telemetry report, and
    /// all three of those are believed. A rate of change that is quietly a sixth of the truth is
    /// worse than none, because it reads like a measurement.
    /// </summary>
    public class SolverReportingTests
    {

        private static ThermalSimulation StiffGrid(float hot = 900f)
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 6; x++) builder.Place(Fixture.Foil(), new Vector3I(x, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly(1));
            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = hot;
            return simulation;
        }

        [Fact]

        public void TheReportedChangeSpansTheWholeStepAndNotItsLastSubstep()
        {

            ThermalSimulation simulation = StiffGrid();
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            float[] before = new float[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) before[i] = nodes[i].Temperature;

            simulation.StepExact(1, Worlds.Shadow());

            Assert.True(simulation.Solver.LastSubsteps > 1,
                "the fixture has to substep or this test proves nothing: "
                + simulation.Solver.LastSubsteps);

            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.Equal(nodes[i].Temperature - before[i], nodes[i].LastDeltaTemperature, 3);
            }
        }

        [Fact]

        public void TheReportedChangeIsTheSumOfWhatTheSubstepsDid()
        {

            ThermalSimulation simulation = StiffGrid();
            ThermalNode hottest = simulation.Solver.GetNodeAt(Vector3I.Zero);

            float start = hottest.Temperature;
            simulation.StepExact(1, Worlds.Shadow());

            int substeps = simulation.Solver.LastSubsteps;
            Assert.True(substeps > 1);

            float moved = Math.Abs(hottest.Temperature - start);
            float reported = Math.Abs(hottest.LastDeltaTemperature);

            Assert.Equal(moved, reported, 3);

            Assert.True(reported > moved / substeps * 1.5f,
                "reported " + reported + " K looks like one substep of " + moved + " K");
        }

        [Fact]

        public void ASettledGridReportsNoChange()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(Fixture.ConductionOnly());
            simulation.StepExact(1, Worlds.Shadow());

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Assert.Equal(0f, nodes[i].LastDeltaTemperature, 5);
            }
        }

        [Fact]

        public void AHostWriteIsTheBaselineForTheNextStepsChange()
        {

            ThermalSimulation simulation = StiffGrid();
            simulation.StepExact(1, Worlds.Shadow());

            ThermalNode node = simulation.Solver.GetNodeAt(new Vector3I(5, 0, 0));
            node.Temperature = 400f;

            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(node.Temperature - 400f, node.LastDeltaTemperature, 3);
        }


        [Fact]

        public void ClampingIsReportedExactlyWhenTheCapBinds()
        {

            ThermalSimulation simulation = StiffGrid();
            simulation.Settings.MaxSubsteps = 1;
            simulation.Settings.Derive();

            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(1, simulation.Solver.LastSubsteps);
            Assert.True(simulation.Solver.LastStepWasClamped,
                "a grid needing more than one substep and allowed one is clamped");
        }

        [Fact]

        public void AGridInsideTheCapIsNotReportedAsClamped()
        {

            ThermalSimulation simulation = StiffGrid();

            float required = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);
            simulation.Settings.MaxSubsteps = (int)Math.Ceiling(required) + 8;
            simulation.Settings.Derive();

            simulation.StepExact(1, Worlds.Shadow());

            Assert.True(simulation.Solver.LastSubsteps > 1);
            Assert.True(simulation.Solver.LastSubsteps < simulation.Solver.MaxSubsteps);
            Assert.False(simulation.Solver.LastStepWasClamped);
        }

        [Fact]

        public void TheSubstepCountCoversWhatTheEstimateAskedFor()
        {

            ThermalSimulation simulation = StiffGrid();

            float required = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);
            simulation.StepExact(1, Worlds.Shadow());

            int expected = required <= 1f
                ? 1
                : Math.Min(simulation.Solver.MaxSubsteps, (int)Math.Ceiling(required));

            Assert.Equal(expected, simulation.Solver.LastSubsteps);
        }
    }
}

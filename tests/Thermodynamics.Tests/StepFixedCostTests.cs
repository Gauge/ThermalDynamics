using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A step's fixed cost — the passes over every node that run once per step rather than once
    /// per substep.
    ///
    /// There are two of them: mirroring the node objects into the flat rows, and the stability
    /// estimate that decides how many substeps the step gets. Both are O(nodes) and neither does
    /// any physics, so a step that runs either twice pays a full node walk for an answer it
    /// already holds. On a grid taking three substeps — the field average — the fixed part is
    /// roughly half the step, which is what makes the count worth pinning.
    /// </summary>
    public class StepFixedCostTests
    {

        private static ThermalSimulation Ship(int blocks, int budgetVisits)
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                MaxSubsteps = 64,
                MaxSubstepsPerBlock = 0,
                MaxElementVisitsPerStep = budgetVisits,
            };
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(blocks, 2, 2));
            return builder.BuildSimulation(settings);
        }


        private static void RunOneStep(ThermalSimulation simulation)
        {
            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

            simulation.Work.Reset();
            long steps = simulation.Work.SolverSteps;

            for (int frame = 0; frame < 600 && simulation.Work.SolverSteps == steps; frame++)
            {
                simulation.Update(1f / 60f, Worlds.Shadow());
            }

            Assert.Equal(steps + 1, simulation.Work.SolverSteps);
        }

        [Fact]

        public void AStepEstimatesItsStiffnessOnce()
        {

            ThermalSimulation simulation = Ship(16, 1000000);

            RunOneStep(simulation);

            Assert.Equal(1, simulation.Work.StabilityEstimates);
        }

        [Fact]

        public void AStepMirrorsTheNodeObjectsOnce()
        {

            ThermalSimulation simulation = Ship(16, 1000000);

            RunOneStep(simulation);

            Assert.Equal(1, simulation.Work.NodeStateSyncs);
        }

        [Fact]

        public void TheCountDoesNotDependOnTheVisitBudget()
        {

            ThermalSimulation unbounded = Ship(16, 0);

            RunOneStep(unbounded);

            Assert.Equal(1, unbounded.Work.StabilityEstimates);
            Assert.Equal(1, unbounded.Work.NodeStateSyncs);
        }

        [Fact]

        public void AStepHandedItsEstimateIntegratesTheSameFloats()
        {

            ThermalSimulation given = Ship(12, 1000000);

            ThermalSimulation taken = Ship(12, 1000000);

            while (given.HasPendingWork) given.Update(1f / 60f, Worlds.Shadow());
            while (taken.HasPendingWork) taken.Update(1f / 60f, Worlds.Shadow());

            EnvironmentSample sample = Worlds.Space(new Vector3(0f, 1f, 0f));
            EnvironmentState state = EnvironmentSolver.Solve(
                given.Settings, given.Planet, sample);

            float step = given.Settings.StepSeconds;

            for (int i = 0; i < 8; i++)
            {
                given.Solver.Step(step, state, given.Solver.RequiredSubsteps(step, state));
                taken.Solver.Step(step, state);
            }

            Assert.Equal(taken.Solver.LastSubsteps, given.Solver.LastSubsteps);

            for (int i = 0; i < taken.Solver.Nodes.Count; i++)
            {
                Assert.Equal(taken.Solver.Nodes[i].Temperature,
                    given.Solver.Nodes[i].Temperature);
            }
        }

        [Fact]

        public void TheMassFloorSeesTheEnvironmentTheStepWillRunIn()
        {
            ThermalSettings settings = new ThermalSettings
            {
                Frequency = 4,
                MaxSubsteps = 64,
                MaxSubstepsPerBlock = 1,
                MaxElementVisitsPerStep = 1000000,
            };
            settings.Derive();

            BlockModel fitting = BlockModel.Solid(
                "Fitting", Vector3I.One, 500f, Catalog.DefaultThermal());

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(fitting, Vector3I.Zero, new Vector3I(6, 2, 2));
            ThermalSimulation simulation = builder.BuildSimulation(settings);

            while (simulation.HasPendingWork) simulation.Update(1f / 60f, Worlds.Shadow());

            simulation.StepExact(2, Worlds.Shadow());
            int inVacuum = simulation.Solver.FlooredNodes;

            simulation.StepExact(1, Worlds.PlanetSurface(1f, 0.5f));
            int inAir = simulation.Solver.FlooredNodes;

            Assert.True(inVacuum < simulation.Solver.Nodes.Count,
                "the floor saturated in vacuum, so the count cannot rise in air");
            Assert.True(inAir > inVacuum,
                "floored in vacuum " + inVacuum + ", floored in air " + inAir);
        }

        [Fact]

        public void AStepSpreadOverManyFramesStillEstimatesOnce()
        {

            ThermalSimulation simulation = Ship(64, 4000);

            RunOneStep(simulation);

            Assert.True(simulation.Work.SolverSubsteps >= 1);
            Assert.Equal(1, simulation.Work.StabilityEstimates);
            Assert.Equal(1, simulation.Work.NodeStateSyncs);
        }
    }
}

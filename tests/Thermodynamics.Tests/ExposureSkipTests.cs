using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The exposure refresh writes only the nodes whose faces moved, and what makes that safe is
    /// that writing all of them would have produced **the same bits** (`D8`).
    ///
    /// <para>
    /// The refresh runs over every node whenever the room map republishes. On a hull nobody is
    /// building, every one of those nodes gets the answer it already had — and before Pass 9's
    /// seventh iteration each was written anyway and marked `StateDirty`, so the next
    /// `SyncNodeState` re-mirrored the whole grid and, on a server, resent it. Skipping is only
    /// correct if a node that was skipped was genuinely identical, and the failure it would cause is
    /// silent: a stale radiating area looks like physics.
    /// </para>
    ///
    /// <para>
    /// The reference here is the code the skip replaced, not the skip's own intent — six per-face
    /// writes and a `RefreshExposure`, which is unconditional and therefore cannot skip anything.
    /// The unchanged-node and changed-face tests keep the rest honest: without them a skip that never engaged, or
    /// one that swallowed a real change, would pass every equivalence assertion above (`E8`).
    /// </para>
    /// <para>Two adjacent blocks also pin maximum-budget completion and removal during a partial exposure sweep.</para>
    /// </summary>
    public class ExposureSkipTests
    {

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MaximumBudgetDrainsAPartialSweepWithoutOverflowOrNegativeWork(bool removeBeforeDrain)
        {
            var simulation = new ThermalSimulation(new ThermalSettings(), new GridModel(2.5f));
            var model = BlockModel.Solid("exposure", Vector3I.One, 10f, BlockThermalProperties.Default());
            var first = simulation.AddBlock(new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity), 300f);
            var second = simulation.AddBlock(new BlockInstance(model, Vector3I.UnitX, BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            Assert.Equal(5, first.TotalExposedFaces);
            Assert.Equal(5, second.TotalExposedFaces);
            first.SetExposedFaces(new int[Face.Count]);
            second.SetExposedFaces(new int[Face.Count]);
            long visits = simulation.Work.ExposureNodeVisits;
            long writes = simulation.Work.ExposureNodeWrites;
            var solver = simulation.Solver;

            solver.BeginExposureRefresh(simulation.Rooms.Map);
            Assert.True(solver.StepExposureRefresh(0));
            Assert.True(solver.StepExposureRefresh(-1));
            Assert.Equal(visits, simulation.Work.ExposureNodeVisits);
            Assert.Equal(writes, simulation.Work.ExposureNodeWrites);
            Assert.True(solver.StepExposureRefresh(1));
            Assert.Equal(5, first.TotalExposedFaces);
            Assert.Equal(0, second.TotalExposedFaces);

            if (removeBeforeDrain)
            {
                simulation.RemoveBlock(first.Block);
                simulation.RemoveBlock(second.Block);
                Assert.Empty(solver.Nodes);
            }

            // A zero budget still leaves even an exhausted cursor pending until
            // a positive-budget call acknowledges completion.
            Assert.True(solver.StepExposureRefresh(0));
            Assert.True(solver.ExposureRefreshPending);
            Assert.False(solver.StepExposureRefresh(int.MaxValue));
            Assert.False(solver.ExposureRefreshPending);
            long processed = removeBeforeDrain ? 1L : 2L;
            Assert.Equal(visits + processed, simulation.Work.ExposureNodeVisits);
            Assert.Equal(writes + processed, simulation.Work.ExposureNodeWrites);
            if (!removeBeforeDrain) Assert.Equal(5, second.TotalExposedFaces);

            Assert.False(solver.StepExposureRefresh(int.MaxValue));
            Assert.False(solver.StepExposureRefresh(0));
            Assert.Equal(visits + processed, simulation.Work.ExposureNodeVisits);
            Assert.Equal(writes + processed, simulation.Work.ExposureNodeWrites);
        }

        private static void RewriteEveryNodeTheOldWay(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ThermalNode node = nodes[i];
                for (int f = 0; f < Face.Count; f++)
                {
                    node.SetExposedFaces(f, node.GetExposedFaces(f));
                }
                node.RefreshExposure();
            }
        }


        private static void Step(ThermalSimulation simulation, int steps)
        {
            EnvironmentState state = EnvironmentSolver.Solve(
                simulation.Settings, simulation.Planet, Worlds.Flight(1f, 300f));

            for (int i = 0; i < steps; i++)
            {
                simulation.Solver.RefreshExposure(simulation.Rooms.Map);
                simulation.Solver.Step(simulation.Settings.StepSeconds, state);
            }
        }

        [Fact]

        public void SkippingAnUnchangedNodeProducesTheSameStepAsWritingIt()
        {
            ThermalSimulation skipping = Hulls.Driven(Hulls.Uncapped());
            ThermalSimulation writing = Hulls.Driven(Hulls.Uncapped());

            skipping.Solver.CollectDiagnostics = true;
            writing.Solver.CollectDiagnostics = true;

            Step(skipping, 20);

            EnvironmentState state = EnvironmentSolver.Solve(
                writing.Settings, writing.Planet, Worlds.Flight(1f, 300f));
            for (int i = 0; i < 20; i++)
            {
                RewriteEveryNodeTheOldWay(writing);
                writing.Solver.RefreshExposure(writing.Rooms.Map);
                writing.Solver.Step(writing.Settings.StepSeconds, state);
            }

            SolverAb.AssertIdentical(
                SolverAb.Temperatures(writing), SolverAb.Temperatures(skipping),
                "twenty steps over a hull whose exposure never moves",
                "writing every node every refresh", "skipping the unchanged ones");

            SolverAb.AssertIdentical(
                SolverAb.Diagnostics(writing), SolverAb.Diagnostics(skipping),
                "the per-mechanism watts after twenty such steps",
                "writing every node every refresh", "skipping the unchanged ones",
                SolverAb.Mechanisms);
        }

        [Fact]

        public void ASecondRefreshOverAnUnchangedHullWritesNothing()
        {
            ThermalSimulation simulation = Hulls.Driven(Hulls.Uncapped());

            Step(simulation, 1);
            Assert.Equal(0, DirtyNodes(simulation));

            long visitsBefore = simulation.Work.ExposureNodeVisits;
            long writesBefore = simulation.Work.ExposureNodeWrites;

            simulation.Solver.RefreshExposure(simulation.Rooms.Map);

            long visited = simulation.Work.ExposureNodeVisits - visitsBefore;
            long written = simulation.Work.ExposureNodeWrites - writesBefore;

            Assert.Equal(simulation.Solver.Nodes.Count, (int)visited);
            Assert.Equal(0L, written);

            Assert.Equal(0, DirtyNodes(simulation));
        }

        [Fact]

        public void AFaceThatMovedIsStillWritten()
        {
            ThermalSimulation simulation = Hulls.Driven(Hulls.Uncapped());

            Step(simulation, 1);
            Assert.Equal(0, DirtyNodes(simulation));


            ThermalNode node = FirstExposedNode(simulation);
            int[] counts = new int[Face.Count];
            for (int f = 0; f < Face.Count; f++) counts[f] = node.GetExposedFaces(f) + 1;
            Assert.True(node.SetExposedFaces(counts), "moving every face is a change");

            long writesBefore = simulation.Work.ExposureNodeWrites;
            simulation.Solver.RefreshExposure(simulation.Rooms.Map);

            Assert.Equal(1L, simulation.Work.ExposureNodeWrites - writesBefore);
            Assert.Equal(1, DirtyNodes(simulation));

            for (int f = 0; f < Face.Count; f++)
            {
                Assert.Equal(counts[f] - 1, node.GetExposedFaces(f));
            }
        }


        private static int DirtyNodes(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int dirty = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].StateDirty) dirty++;
            }
            return dirty;
        }


        private static ThermalNode FirstExposedNode(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].TotalExposedFaces > 0) return nodes[i];
            }

            throw new System.InvalidOperationException(
                "no node on the hull has an exposed face, so this suite would assert nothing");
        }
    }
}

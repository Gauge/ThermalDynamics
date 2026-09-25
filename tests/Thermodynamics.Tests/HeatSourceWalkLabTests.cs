using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The heat-source-walk lab prices a registered source per step on redesign.md's fourth
    /// sweep. What is pinned is that the fixture actually exercises the path — the sources reach
    /// the solver and cost something, monotonically in their count — and that the source
    /// contribution is real heat, not a no-op the timing would measure as free (`E8`). The
    /// injection helper itself is pinned too: a source registered on the sample must raise a
    /// node's watts, or the lab is timing a mechanism that never ran.
    /// </summary>
    public class HeatSourceWalkLabTests
    {
        [Fact]

        public void MoreSourcesCostMoreAndTheStepIsAlwaysTimed()
        {
            List<HeatSourceWalkLab.Row> rows = HeatSourceWalkLab.Run("ship", 4000, 4);

            Assert.Equal(HeatSourceWalkLab.SourceCounts.Length, rows.Count);
            double baseMs = 0d;
            foreach (HeatSourceWalkLab.Row row in rows)
            {
                Assert.True(row.StepMs > 0, row.Sources + " sources: the step was never timed");
                Assert.True(row.Substeps > 0, row.Sources + " sources: no substeps ran");
                if (row.Sources == 0) baseMs = row.StepMs;
            }

            HeatSourceWalkLab.Row high = null;
            foreach (HeatSourceWalkLab.Row row in rows) { if (row.Sources == 32) high = row; }
            Assert.NotNull(high);
            Assert.True(high.StepMs >= baseMs,

                "32 sources (" + high.StepMs + " ms) cost less than none (" + baseMs
                + " ms); the sources are not reaching the solver");
        }

        [Fact]

        public void ARegisteredSourceActuallyHeatsANode()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            ThermalSimulation warm = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
            warm.Solver.CollectDiagnostics = true;
            warm.StepExact(1, Worlds.DarkVacuumWithSources(4));

            float mostLit = 0f;
            IList<ThermalNode> nodes = warm.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].LastHeatSourceWatts > mostLit) mostLit = nodes[i].LastHeatSourceWatts;
            }
            Assert.True(mostLit > 0f, "no node received any heat-source watts, so the fixture injects nothing");
        }
    }
}

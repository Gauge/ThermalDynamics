using System.Collections.Generic;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The environment-walk lab prices the buried-branch residue against a compacted walk on
    /// redesign.md's second sweep, and its own agreement check — both shapes must produce the
    /// same watts row exactly, or it throws before timing anything — is the load-bearing half.
    /// What is pinned here is that the check can actually fire's precondition: the fixture holds
    /// both classes of node, so agreement is over buried and exposed alike, and both timed rows
    /// come back with a clock on them (`E8`).
    /// </summary>
    public class EnvironmentWalkLabTests
    {
        [Fact]

        public void TheFixtureHoldsBothClassesAndBothShapesGetTimed()
        {
            List<EnvironmentWalkLab.Row> rows = EnvironmentWalkLab.Run("ship", 4000, 3);

            Assert.Equal(2, rows.Count);
            foreach (EnvironmentWalkLab.Row row in rows)
            {
                Assert.True(row.Nodes > 1000, row.Shape + " judged only " + row.Nodes + " nodes");
                Assert.True(row.ExposedNodes > 0 && row.ExposedNodes < row.Nodes,
                    row.Shape + ": " + row.ExposedNodes + " exposed of " + row.Nodes

                    + " — the fixture is missing one of the two classes the comparison is about (`E8`)");
                Assert.True(row.BestNs > 0, row.Shape + " was never timed");
            }

        }
    }
}

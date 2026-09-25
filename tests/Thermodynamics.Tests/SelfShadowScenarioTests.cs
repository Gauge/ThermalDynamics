using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The self-shadow scenario, checked against geometry worked out independently of the model.
    ///
    /// The scenario exists to answer "which faces of a solid hull are lit"; a test that asked the
    /// model the same question twice would agree with whatever the model happened to do. So the
    /// expected shares are computed here by ray-versus-cube against the same geometry, and the
    /// scenario's own figures have to match them.
    /// </summary>
    [Collection("alone")]
    public class SelfShadowScenarioTests
    {

        private static readonly Vector3 Sun = new Vector3(0.9004f, 0.1619f, -0.4038f);

        [Fact]

        public void EveryDirectionMatchesGeometryWorkedOutIndependently()
        {
            ScenarioResult result = Scenarios.Run("self-shadow");
            ThermalSimulation simulation = result.Runner.Simulation;
            SunShadowMap shadow = simulation.Solver.SunShadow;

            Assert.True(shadow.IsBuilt, "the pass should have completed inside the run");

            GridModel grid = simulation.Grid;

            for (int face = 0; face < Face.Count; face++)
            {
                int exposed = 0;
                int lit = 0;
                int expected = 0;

                IList<BlockInstance> blocks = grid.Blocks;
                for (int i = 0; i < blocks.Count; i++)
                {
                    Vector3I cell = blocks[i].Min;
                    Vector3I outside = cell + Face.Offsets[face];
                    if (grid.IsOccupied(outside)) continue;

                    exposed++;
                    if (shadow.IsFaceLit(cell, face)) lit++;
                    if (Reference.Lit(grid, outside, Sun)) expected++;
                }

                Assert.True(exposed > 0, Face.Name(face) + " should have exposed faces");
                Assert.Equal(expected, lit);
            }
        }

        [Fact]

        public void TheFaceTurnedToTheSunIsLitWholeAndTheRecessIsDark()
        {
            ScenarioResult result = Scenarios.Run("self-shadow");
            ThermalSimulation simulation = result.Runner.Simulation;
            SunShadowMap shadow = simulation.Solver.SunShadow;

            for (int y = 0; y < 7; y++)
            {
                for (int z = 0; z < 4; z++)
                {
                    ThermalNode node = simulation.Solver.GetNodeAt(new Vector3I(3, y, z));
                    Assert.NotNull(node);
                    Assert.Equal(1f, shadow.FaceLitFraction(node.Block, Face.Right), 5);
                }
            }

            for (int y = 2; y < 5; y++)
            {
                for (int z = 1; z < 3; z++)
                {
                    ThermalNode node = simulation.Solver.GetNodeAt(new Vector3I(2, y, z));
                    Assert.NotNull(node);
                    Assert.Equal(0f, shadow.FaceLitFraction(node.Block, Face.Left), 5);
                }
            }
        }

        [Fact]

        public void TheHeadlineSharesAreTheOnesTheGeometryImplies()
        {
            string summary = Scenarios.Run("self-shadow").Summary;

            Assert.Contains("sunward 100%", summary);
            Assert.Contains("recess floor 0%", summary);

            Assert.True(Share(summary, "top ") >= 70f, summary);
            Assert.True(Share(summary, "flank ") >= 70f, summary);
        }


        private static float Share(string summary, string after)
        {
            int start = summary.IndexOf(after) + after.Length;
            int end = summary.IndexOf('%', start);
            return float.Parse(summary.Substring(start, end - start),
                System.Globalization.CultureInfo.InvariantCulture);
        }

        [Fact]

        public void SelfShadowingTakesSolarGainAwayAndNeverAddsIt()
        {
            ScenarioResult result = Scenarios.Run("self-shadow");


            float shadowed = Extract(result.Summary, "self-shadowing ");

            float cheap = Extract(result.Summary, "against ");

            Assert.True(shadowed < cheap,
                "self-shadowing should remove solar gain, not add it: " + result.Summary);
        }

        [Fact]

        public void SelfShadowingCostsNothingPerStepBetweenPasses()
        {
            string summary = Scenarios.Run("shadow-cost").Summary;


            float off = Milliseconds(summary, "self-shadowing off ");

            float on = Milliseconds(summary, ", on ");

            Assert.True(on < (off * 1.5f) + 0.005f,
                "per-step cost should be near identical between passes: " + summary);
        }


        private static float Milliseconds(string summary, string after)
        {
            int start = summary.IndexOf(after) + after.Length;
            int end = summary.IndexOf(" ms", start);
            return float.Parse(summary.Substring(start, end - start),
                System.Globalization.CultureInfo.InvariantCulture);
        }


        private static float Extract(string summary, string after)
        {
            int start = summary.IndexOf(after) + after.Length;
            int end = summary.IndexOf(" kW", start);
            return float.Parse(summary.Substring(start, end - start).Replace(",", ""),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

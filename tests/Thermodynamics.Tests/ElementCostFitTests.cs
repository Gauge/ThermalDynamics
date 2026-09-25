using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The element cost lab's conclusions are a least-squares fit, and a fit that is wrong is
    /// wrong quietly — it returns numbers either way. These recover known coefficients from
    /// synthetic rows, so the arithmetic behind a budget recommendation is not itself the thing
    /// being trusted on sight.
    /// </summary>
    public class ElementCostFitTests
    {

        private static ElementCostLab.Row Row(int nodes, int links, int faces, double cost)
        {
            return new ElementCostLab.Row
            {
                Nodes = nodes,
                Links = links,
                Faces = faces,
                ConductionNanoseconds = cost,
            };
        }

        [Fact]

        public void TheTwoPredictorFitRecoversKnownCoefficients()
        {
            List<ElementCostLab.Row> rows = new List<ElementCostLab.Row>
            {
                Row(1000, 0, 0, 7000),
                Row(1000, 1000, 0, 9000),
                Row(1000, 2000, 0, 11000),
                Row(2000, 6000, 0, 26000),
            };

            double a, b, r2;
            ElementCostLab.Solve2(rows, r => r.Nodes, r => r.Links,
                r => r.ConductionNanoseconds, out a, out b, out r2);

            Assert.Equal(7d, a, 6);
            Assert.Equal(2d, b, 6);
            Assert.Equal(1d, r2, 6);
        }

        [Fact]

        public void TheFitHasNoInterceptSoOverheadIsChargedToTheElements()
        {
            List<ElementCostLab.Row> rows = new List<ElementCostLab.Row>
            {
                Row(0, 1000, 0, 5000 + 3000),
                Row(0, 2000, 0, 5000 + 6000),
                Row(0, 4000, 0, 5000 + 12000),
            };

            double b, r2;
            ElementCostLab.Solve1(rows, r => r.Links, r => r.ConductionNanoseconds, out b, out r2);

            Assert.True(b > 3d, "overhead must inflate the per-link figure, not vanish");
            Assert.True(b < 5d);

            List<ElementCostLab.Row> clean = new List<ElementCostLab.Row>
            {
                Row(0, 1000, 0, 3000),
                Row(0, 2000, 0, 6000),
                Row(0, 4000, 0, 12000),
            };

            ElementCostLab.Solve1(clean, r => r.Links, r => r.ConductionNanoseconds, out b, out r2);
            Assert.Equal(3d, b, 6);
        }

        [Fact]

        public void CollinearPredictorsAreRefusedRatherThanInvented()
        {
            List<ElementCostLab.Row> rows = new List<ElementCostLab.Row>
            {
                Row(100, 200, 0, 1000),
                Row(200, 400, 0, 2000),
                Row(400, 800, 0, 4000),
            };

            double a, b, r2;
            ElementCostLab.Solve2(rows, r => r.Nodes, r => r.Links,
                r => r.ConductionNanoseconds, out a, out b, out r2);

            Assert.Equal(0d, a);
            Assert.Equal(0d, b);
            Assert.Equal(0d, r2);
        }

        [Fact]

        public void TheSinglePredictorFitRecoversItsCoefficient()
        {
            List<ElementCostLab.Row> rows = new List<ElementCostLab.Row>
            {
                Row(0, 0, 100, 250),
                Row(0, 0, 200, 500),
                Row(0, 0, 400, 1000),
            };

            double a, r2;
            ElementCostLab.Solve1(rows, r => r.Faces, r => r.ConductionNanoseconds, out a, out r2);

            Assert.Equal(2.5d, a, 6);
            Assert.Equal(1d, r2, 6);
        }

        [Fact]

        public void AWeightIsLinksPerNodeAndSurvivesAChangeOfUnits()
        {
            ElementCostLab.Fit fast = new ElementCostLab.Fit
            {
                PerNode = 6.4,
                PerLink = 2.7,
                PerEnvironmentNode = 2.6,
            };
            ElementCostLab.Fit slow = new ElementCostLab.Fit
            {
                PerNode = 64,
                PerLink = 27,
                PerEnvironmentNode = 26,
            };

            Assert.Equal(fast.NodeWeight, slow.NodeWeight, 6);
            Assert.Equal(fast.NodeWeightWithEnvironment, slow.NodeWeightWithEnvironment, 6);
        }
    }
}

using System;
using System.Collections.Generic;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The block overlay's two cost bounds: dropping what the camera cannot see, and fitting a
    /// radius to a box budget.
    ///
    /// The cull must never drop a box that is on screen — an overlay that hides part of a hull is
    /// worse than a slow one, because it reads as a hull that is not there. The fit must settle
    /// rather than oscillate, since a radius that pulses is a picture that flickers.
    /// </summary>
    public class OverlayBudgetTests
    {

        private static readonly Vector3D Forward = new Vector3D(0, 0, -1);


        private static void Cone(out double sin, out double cos)
        {
            double half = OverlayBudget.ConeHalfAngle(70.0 * Math.PI / 180.0, 16.0 / 9.0);
            sin = Math.Sin(half);
            cos = Math.Cos(half);
        }


        private static bool InView(Vector3D delta, double radius)
        {
            double sin, cos;
            Cone(out sin, out cos);

            Vector3D forward = Forward;
            return OverlayBudget.InView(ref delta, radius, ref forward, sin, cos);
        }

        [Fact]

        public void ABoxStraightAheadIsDrawn()
        {
            Assert.True(InView(new Vector3D(0, 0, -50), 1.25));
        }

        [Fact]

        public void ABoxBehindTheCameraIsNot()
        {
            Assert.False(InView(new Vector3D(0, 0, 50), 1.25));
            Assert.False(InView(new Vector3D(10, 4, 30), 1.25));
        }

        [Fact]

        public void ABoxAroundTheCameraIsDrawn()
        {
            Assert.True(InView(new Vector3D(0, 0, 0.5), 2.5));
        }

        [Fact]

        public void ABoxOffToTheSideIsNot()
        {
            Assert.False(InView(new Vector3D(200, 0, -20), 1.25));
            Assert.False(InView(new Vector3D(0, 200, -20), 1.25));
        }

        [Fact]

        public void EveryPointOfTheScreenSurvivesTheCull()
        {
            double fov = 70.0 * Math.PI / 180.0;
            double aspect = 16.0 / 9.0;
            double tangent = Math.Tan(fov * 0.5);

            for (double x = -1; x <= 1.0001; x += 0.1)
            {
                for (double y = -1; y <= 1.0001; y += 0.1)
                {

                    Vector3D delta = new Vector3D(x * tangent * aspect * 100, y * tangent * 100, -100);

                    Assert.True(InView(delta, 0.0), "screen point " + x + "," + y + " was culled");
                }
            }
        }

        [Fact]

        public void ABoxOutsideTheConeSurvivesByItsOwnRadius()
        {

            Vector3D delta = new Vector3D(0, 200, -100);

            Assert.False(InView(delta, 0.0));
            Assert.True(InView(delta, 40.0));
        }



        private static int Frame(OverlayBudget budget, int side, double spacing)
        {
            budget.BeginFrame();

            double sin, cos;
            Cone(out sin, out cos);
            Vector3D forward = Forward;

            for (int x = 0; x < side; x++)
            {
                for (int y = 0; y < side; y++)
                {
                    for (int z = 0; z < side; z++)
                    {

                        Vector3D delta = new Vector3D(
                            (x - (side * 0.5)) * spacing,
                            (y - (side * 0.5)) * spacing,
                            -((z + 1) * spacing));

                        if (!OverlayBudget.InView(ref delta, spacing * 0.5, ref forward, sin, cos))
                        {
                            budget.Cull();
                            continue;
                        }

                        budget.Accept(delta.Length());
                    }
                }
            }

            budget.EndFrame();
            return budget.Drawn;
        }

        [Fact]

        public void AGridInsideTheBudgetIsDrawnWholeAndNeverLimited()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 12000;


            int drawn = Frame(budget, 10, 2.5);

            Assert.Equal(budget.Considered - budget.OffScreen, drawn);
            Assert.Equal(0, budget.OverBudget);
            Assert.False(budget.IsLimiting);
        }

        [Fact]

        public void TheFirstFrameOverTheBudgetIsStillCapped()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 500;

            Assert.Equal(500, Frame(budget, 30, 2.5));
            Assert.True(budget.OverBudget > 0);
        }

        [Fact]

        public void TheRadiusSettlesNearTheBudgetWithinAFewFrames()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 2000;


            List<int> drawn = new List<int>();
            for (int frame = 0; frame < 8; frame++)
            {
                drawn.Add(Frame(budget, 40, 1.0));
            }

            for (int frame = 4; frame < drawn.Count; frame++)
            {
                Assert.InRange(drawn[frame], 1500, 2000);
            }
        }

        [Fact]

        public void ASettledFrameDropsNothingToTheHardStop()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 2000;

            for (int frame = 0; frame < 8; frame++) Frame(budget, 40, 1.0);

            Assert.Equal(0, budget.Capped);
            Assert.True(budget.Beyond > 0);
        }

        [Fact]

        public void TheRadiusLiftsAgainWhenTheGridNoLongerNeedsIt()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 2000;

            for (int frame = 0; frame < 8; frame++) Frame(budget, 40, 1.0);
            Assert.True(budget.IsLimiting);

            for (int frame = 0; frame < 10; frame++) Frame(budget, 8, 1.0);

            Assert.False(budget.IsLimiting);
            Assert.Equal(0, budget.OverBudget);
        }

        [Fact]

        public void TheRadiusNeverFallsBelowSomethingWorthDrawing()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 1;

            for (int frame = 0; frame < 40; frame++) Frame(budget, 30, 1.0);

            Assert.True(budget.Radius >= OverlayBudget.MinimumRadius);
            Assert.True(budget.Drawn >= 1);
        }

        [Fact]

        public void ABudgetOfZeroDrawsNothingAndDoesNotDivideByIt()
        {

            OverlayBudget budget = new OverlayBudget();
            budget.MaxBoxes = 0;

            Assert.Equal(0, Frame(budget, 10, 1.0));
            Assert.True(budget.OverBudget > 0);
        }

        [Fact]

        public void TheConeContainsTheFrustumAtEveryAspect()
        {
            double fov = 60.0 * Math.PI / 180.0;

            foreach (double aspect in new[] { 0.5, 1.0, 16.0 / 9.0, 32.0 / 9.0 })
            {
                double half = OverlayBudget.ConeHalfAngle(fov, aspect);
                double tangent = Math.Tan(fov * 0.5);


                Vector3D corner = new Vector3D(tangent * aspect, tangent, -1);
                double angle = Math.Acos(Vector3D.Dot(Vector3D.Normalize(corner), Forward));

                Assert.True(half >= angle - 1e-9, "aspect " + aspect + " corner outside the cone");
            }
        }
    }
}

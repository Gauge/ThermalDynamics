using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Whether the aerodynamic view sees the surface the air meets**, checked against geometry that
    /// can be worked out by hand rather than against the model (`E7`). A box's silhouette along any
    /// direction is `A_yz|uₓ| + A_xz|u_y| + A_xy|u_z|`; a closed convex hull shows the wake exactly the
    /// area it shows the flow; a face behind another face is not reached; and the hull's length along
    /// the flow is its extent projected on the flow. See aerodynamics.md, AR3 and AR4.
    /// </summary>
    public class AeroGeometryTests
    {
        private const float Cell = 2.5f;

        private static float BoxSilhouette(int x, int y, int z, Vector3 u)
        {
            u = Vector3.Normalize(u);
            float a = Cell * Cell;
            return a * ((y * z * Math.Abs(u.X)) + (x * z * Math.Abs(u.Y)) + (x * y * Math.Abs(u.Z)));
        }

        [Theory]
        [InlineData(0f, 0f, 1f)]
        [InlineData(1f, 0f, 0f)]
        [InlineData(1f, 0f, 1f)]
        [InlineData(1f, 1f, 1f)]
        [InlineData(-0.3f, 0.8f, 0.5f)]

        public void ABoxShowsTheAirTheAreaItsProjectionHas(float x, float y, float z)
        {
            Vector3 u = new Vector3(x, y, z);
            AeroView view = AeroGeometry.View(AeroCalibrationLab.Box(6, 4, 8), u);

            Assert.Equal(BoxSilhouette(6, 4, 8, u), view.Silhouette, 2);
        }

        [Theory]
        [InlineData(0f, 0f, 1f)]
        [InlineData(1f, 1f, 1f)]

        public void AClosedConvexHullShowsTheWakeWhatItShowsTheFlow(float x, float y, float z)
        {
            AeroView view = AeroGeometry.View(AeroCalibrationLab.Box(5, 5, 5), new Vector3(x, y, z));

            float wake = 0f;
            foreach (AeroElement element in view.Base) wake += element.Area;

            Assert.True(view.Silhouette > 0f);
            Assert.Equal(view.Silhouette, wake, 2);
        }

        [Fact]

        public void AFaceBehindAnotherIsNotReached()
        {
            GridModel grid = AeroCalibrationLab.Box(4, 4, 1);
            grid.Add(Catalog.HeavyArmor(), new Vector3I(1, 1, -3));

            AeroView view = AeroGeometry.View(grid, Vector3.Backward);

            Assert.Equal(BoxSilhouette(4, 4, 1, Vector3.Backward), view.Silhouette, 2);
        }

        [Fact]

        public void ASquareFaceIsSquareToTheFlowAndTheLengthIsTheExtent()
        {
            AeroView view = AeroGeometry.View(AeroCalibrationLab.Box(8, 8, 8), Vector3.Backward);

            float squarest = 0f;
            foreach (AeroElement element in view.Front) squarest = Math.Max(squarest, element.Incidence);

            Assert.Equal(1f, squarest, 3);
            Assert.Equal(8 * Cell, view.Length, 3);
            Assert.Equal(1f, AeroDrag.Fineness(view), 3);
        }

        [Fact]

        public void TheWettedSkinIsTheOutsideAndNotAHollow()
        {
            GridModel shell = AeroCalibrationLab.Box(5, 5, 5);
            shell.Remove(shell.GetAtCell(new Vector3I(2, 2, 2)));

            AeroView hollow = AeroGeometry.View(shell, Vector3.Backward);
            AeroView solid = AeroGeometry.View(AeroCalibrationLab.Box(5, 5, 5), Vector3.Backward);

            Assert.Equal(6 * 25 * Cell * Cell, solid.Wetted, 2);
            Assert.Equal(solid.Wetted, hollow.Wetted, 2);
        }
    }
}

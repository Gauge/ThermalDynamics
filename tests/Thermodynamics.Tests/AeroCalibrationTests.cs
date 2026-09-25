using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What the drag model's calibration is, and what it is not.** Round 5 fitted the six
    /// coefficients to every sourced body at radius 24 and predicted each body from a fit that did not
    /// see it. It failed its registered bars — 14 of 22 bodies within 25 % out of sample against 18
    /// needed, three worse than 50 % — and the registered fallback is to ship the in-sample fit with
    /// its errors stated (aerodynamics.md, Round 5). These pin that outcome so that a change which
    /// improves it or degrades it fails a test and is read (`D5`), and hold the shipped constants to
    /// the fit that produced them (`D3`).
    /// </summary>
    [Trait("speed", "slow")]
    public class AeroCalibrationTests
    {
        private static readonly Lazy<AeroCalibrationLab.Result> Round5 = new Lazy<AeroCalibrationLab.Result>(
            () => AeroCalibrationLab.Fit(AeroCalibrationLab.Rigs(AeroCalibrationLab.CalibrationRadius)));

        [Fact]

        public void TheShippedCoefficientsAreTheFit()
        {
            AeroCoefficients fit = Round5.Value.Coefficients;
            AeroCoefficients shipped = AeroCoefficients.Fitted;

            Assert.Equal(fit.Offset, shipped.Offset, 4);
            Assert.Equal(fit.Stagnation, shipped.Stagnation, 4);
            Assert.Equal(fit.BaseAttached, shipped.BaseAttached, 4);
            Assert.Equal(fit.BaseSeparated, shipped.BaseSeparated, 4);
            Assert.Equal(fit.ReattachLength, shipped.ReattachLength, 4);
        }

        [Fact]

        public void RoundFiveMissesItsBarsByWhatWasRecorded()
        {
            AeroCalibrationLab.Result result = Round5.Value;

            Assert.Equal(22, result.Scored);
            Assert.Equal(14, result.Within(0.25f));
            Assert.Equal(19, result.Within(0.5f));
        }

        [Fact]

        public void ATaperedTailTakesDragOffABluntBase()
        {
            AeroCalibrationLab.Result result = Round5.Value;
            float blunt = result.InSample["W3 blunt base"];
            float tapered = result.InSample["W3 tapered tail"];

            Assert.True(tapered < blunt * 0.95f, "tapered " + tapered + " against blunt " + blunt);
        }

        [Fact]

        public void ARoundedNoseReadsTooDraggyWhichIsTheStatedBias()
        {
            AeroCalibrationLab.Result result = Round5.Value;

            Assert.True(result.InSample["hemisphere, curve first"] > 0.42f * 1.5f);
            Assert.True(result.InSample["hemisphere, curve first"] < result.InSample["hemisphere, flat first"]);
        }
    }

    /// <summary>
    /// **The reconstructed surface normal, measured against exact geometry and not against drag.** On
    /// blunt bodies the reading converges as a rig gets finer, because a thin rig is all rim; on slender
    /// ones it has a floor, the radius-one reconstruction's. Four calibration rounds were partly fitted
    /// to the first before this was measured (aerodynamics.md, Round 4).
    /// </summary>
    public class AeroIncidenceTests
    {
        private static float MeanSquaredIncidence(GridModel grid)
        {
            AeroView view = AeroGeometry.View(grid, Vector3.Backward);
            double area = 0d, weighted = 0d;
            foreach (AeroElement e in view.Front)
            {
                area += e.Area;
                weighted += e.Area * e.Incidence * e.Incidence;
            }
            return (float)(weighted / area);
        }

        [Fact]

        public void AHemisphereReadsItsExactMeanFromRadiusSixteen()
        {
            Assert.Equal(0.5f, MeanSquaredIncidence(AeroCalibrationLab.Hemisphere(16)), 1);
            Assert.True(Math.Abs(MeanSquaredIncidence(AeroCalibrationLab.Hemisphere(16)) - 0.5f) < 0.015f);
        }

        [Fact]

        public void ABluntConeConvergesAsTheRigGetsFiner()
        {
            float coarse = MeanSquaredIncidence(AeroCalibrationLab.Cone(75f, 8));
            float fine = MeanSquaredIncidence(AeroCalibrationLab.Cone(75f, 24));
            float exact = (float)Math.Pow(Math.Sin(75d * Math.PI / 180d), 2d);

            Assert.True(Math.Abs(fine - exact) < Math.Abs(coarse - exact) / 2f,
                "coarse " + coarse + ", fine " + fine + ", exact " + exact);
        }

        [Fact]

        public void ASlenderConeHasAFloorThatResolutionDoesNotMove()
        {
            float coarse = MeanSquaredIncidence(AeroCalibrationLab.Cone(10f, 8));
            float fine = MeanSquaredIncidence(AeroCalibrationLab.Cone(10f, 24));

            Assert.True(coarse > 0.05f && fine > 0.05f, "a 10° cone's exact mean is 0.030");
            Assert.Equal(coarse, fine, 1);
        }
    }
}

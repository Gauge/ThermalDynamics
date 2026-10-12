using Aerodynamics.Core;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What the drag model's calibration is, and what it is not.** Round 6 fitted the six
    /// coefficients to every sourced body at radius 24, through the surface-read normal, and predicted
    /// each body from a fit that did not see it. It misses round 5's bars — 15 of 22 bodies within 25 %
    /// out of sample against 18 needed, three worse than 50 %, all of them rounded — and ships its
    /// in-sample fit with the errors stated; it holds the resolution invariance a game-scale hull needs
    /// (aerodynamics.md, Round 6). These pin that outcome so that a change which improves it or degrades
    /// it fails a test and is read (`D5`), and hold the shipped constants to the fit (`D3`).
    /// </summary>
    [Trait("speed", "slow")]
    [Trait("scope", "integration")]
    public class AeroCalibrationTests
    {
        private static readonly Lazy<AeroCalibrationLab.Result> Fitted = new Lazy<AeroCalibrationLab.Result>(
            () => AeroCalibrationLab.Fit(AeroCalibrationLab.Rigs(AeroCalibrationLab.CalibrationRadius)));

        [Fact]

        public void TheShippedCoefficientsAreTheFit()
        {
            AeroCoefficients fit = Fitted.Value.Coefficients;
            AeroCoefficients shipped = AeroCoefficients.Fitted;

            Assert.Equal(fit.Offset, shipped.Offset, 4);
            Assert.Equal(fit.Stagnation, shipped.Stagnation, 4);
            Assert.Equal(fit.BaseAttached, shipped.BaseAttached, 4);
            Assert.Equal(fit.BaseSeparated, shipped.BaseSeparated, 4);
            Assert.Equal(fit.ReattachLength, shipped.ReattachLength, 4);
        }

        [Fact]

        public void TheFitMissesItsBarsByWhatWasRecorded()
        {
            AeroCalibrationLab.Result result = Fitted.Value;

            Assert.Equal(22, result.Scored);
            Assert.Equal(15, result.Within(0.25f));
            Assert.Equal(19, result.Within(0.5f));
        }

        [Fact]

        public void ABodyReadsTheSameAtEveryScaleAHullIsBuiltAt()
        {
            AeroCalibrationLab.Result result = Fitted.Value;

            foreach (var row in AeroCalibrationLab.Invariance(result.Coefficients, result.LabFriction))
            {
                float reference = row.Value[2];
                for (int i = 0; i < 2; i++)
                {
                    Assert.True(Math.Abs((row.Value[i] / reference) - 1f) <= 0.10f,
                        row.Key + " reads " + row.Value[i] + " against " + reference + " at radius 24");
                }
            }
        }

        [Fact]

        public void ATaperedTailTakesDragOffABluntBase()
        {
            AeroCalibrationLab.Result result = Fitted.Value;
            float blunt = result.InSample["W3 blunt base"];
            float tapered = result.InSample["W3 tapered tail"];

            Assert.True(tapered < blunt * 0.95f, "tapered " + tapered + " against blunt " + blunt);
        }

        [Fact]

        public void ARoundedNoseReadsTooDraggyWhichIsTheStatedBias()
        {
            AeroCalibrationLab.Result result = Fitted.Value;

            Assert.True(result.InSample["hemisphere, curve first"] > 0.42f * 1.5f);
            Assert.True(result.InSample["hemisphere, curve first"] < result.InSample["hemisphere, flat first"]);
        }
    }

    /// <summary>
    /// **The surface normal the aerodynamic view reads, measured against exact geometry and not against
    /// drag** (aerodynamics.md, Round 6, X1 to X3). It is read from the surface around each face, so a
    /// flat face is flat to its rim — the block-gradient normal it replaced tilted every rim cell, and
    /// a game-scale plate is mostly rim.
    /// </summary>
    [Trait("scope", "integration")]
    public class AeroIncidenceTests
    {
        private static float MeanSquaredIncidence(GridModel grid, Vector3 upstream)
        {
            AeroView view = AeroGeometry.View(grid, upstream);
            double area = 0d, weighted = 0d;
            foreach (AeroElement e in view.Front)
            {
                area += e.Area;
                weighted += e.Area * e.Incidence * e.Incidence;
            }
            return (float)(weighted / area);
        }

        [Theory]
        [InlineData(7, 7, 1)]
        [InlineData(8, 8, 8)]

        public void EveryFrontFaceOfAFlatFaceIsSquareRimsIncluded(int x, int y, int z)
        {
            AeroView view = AeroGeometry.View(AeroCalibrationLab.Box(x, y, z), Vector3.Backward);

            Assert.Equal(x * y, view.Front.Count);
            foreach (AeroElement e in view.Front) Assert.True(e.Incidence * e.Incidence >= 0.999f, "a face read " + e.Incidence);
        }

        [Fact]

        public void AFortyFiveDegreeStaircaseReadsHalf()
        {
            GridModel wedge = new GridModel(2.5f);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    for (int z = 0; z < 8 - y; z++)
                        wedge.Add(Catalog.HeavyArmor(), new Vector3I(x, y, z));

            AeroView view = AeroGeometry.View(wedge, Vector3.Backward);
            int judged = 0;
            foreach (AeroElement e in view.Front)
            {
                float y = (e.Centre.Y / 2.5f);
                float x = (e.Centre.X / 2.5f);
                if (y < 1.5f || y > 5.5f || x < 1f || x > 6f) continue;
                judged++;
                Assert.Equal(0.5f, e.Incidence * e.Incidence, 1);
                Assert.True(Math.Abs((e.Incidence * e.Incidence) - 0.5f) <= 0.02f, "read " + (e.Incidence * e.Incidence));
            }
            Assert.True(judged > 10, "judged " + judged + " staircase faces");
        }

        [Theory]
        [InlineData(0f, 4, 0.5f)]
        [InlineData(0f, 12, 0.5f)]
        [InlineData(30f, 8, 0.25f)]
        [InlineData(45f, 16, 0.5f)]
        [InlineData(60f, 8, 0.75f)]

        public void BodiesTheFixWasNotTunedOnReadWithinAFewPerCent(float cone, int radius, float exact)
        {
            GridModel body = cone > 0f ? AeroCalibrationLab.Cone(cone, radius) : AeroCalibrationLab.Hemisphere(radius);

            float mean = MeanSquaredIncidence(body, Vector3.Backward);

            Assert.True(Math.Abs(mean - exact) <= 0.05f, "mean " + mean + " against exact " + exact);
        }

        [Fact]

        public void AHemisphereReadsItsExactMeanAtRadiusEight()
        {
            float mean = MeanSquaredIncidence(AeroCalibrationLab.Hemisphere(8), Vector3.Backward);

            Assert.True(Math.Abs(mean - 0.5f) <= 0.03f, "mean " + mean + " against 0.5");
        }
    }
}

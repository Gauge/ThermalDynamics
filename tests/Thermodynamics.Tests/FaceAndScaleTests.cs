using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The six-face index every other structure in the model is laid out against.
    ///
    /// <para>
    /// Offsets, normals, opposites and axes have to agree with each other exactly, because a surface
    /// word, a node's exposure array and a solar weight are all six-element arrays keyed on this one
    /// numbering. A disagreement here would read as a physics fault everywhere else.
    /// </para>
    /// </summary>
    public class FaceTests
    {
        [Fact]

        public void OppositeIsAnInvolutionAndReversesTheOffset()
        {
            for (int face = 0; face < Face.Count; face++)
            {
                int opposite = Face.Opposite(face);

                Assert.Equal(face, Face.Opposite(opposite));
                Assert.Equal(-Face.Offsets[face], Face.Offsets[opposite]);
            }
        }

        [Fact]

        public void IndexOfInvertsOffsets()
        {
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(face, Face.IndexOf(Face.Offsets[face]));
            }

            Assert.Equal(-1, Face.IndexOf(new Vector3I(1, 1, 0)));
            Assert.Equal(-1, Face.IndexOf(Vector3I.Zero));
        }

        [Fact]

        public void NormalsMatchOffsets()
        {
            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I offset = Face.Offsets[face];
                Assert.Equal(new Vector3(offset.X, offset.Y, offset.Z), Face.Normals[face]);
            }
        }

        [Fact]

        public void OppositeFacesShareAnAxis()
        {
            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(Face.Axis(face), Face.Axis(Face.Opposite(face)));
            }

            Assert.Equal(0, Face.Axis(Face.Left));
            Assert.Equal(1, Face.Axis(Face.Up));
            Assert.Equal(2, Face.Axis(Face.Forward));
        }
    }

    /// <summary>
    /// The temperature-to-colour ramp shared by the HUD, the terminal and every overlay, and the unit conversions beside it.
    ///
    /// <para>
    /// A ramp is presentation and cannot make a ship wrong, which is why it went untested for so long
    /// in the copy that had the callers. What it can do is produce a colour that is not a colour: the
    /// last two cases here are the degenerate anchors an overlay actually asks for.
    /// </para>
    /// </summary>
    public class TemperatureScaleTests
    {
        [Fact]

        public void ConversionsRoundTrip()
        {
            Assert.Equal(0f, ThermalConstants.KelvinToCelsius(273.15f), 3);
            Assert.Equal(273.15f, ThermalConstants.CelsiusToKelvin(0f), 3);
            Assert.Equal(100f, ThermalConstants.KelvinToCelsius(373.15f), 3);
            Assert.Equal(373.15f, ThermalConstants.CelsiusToKelvin(100f), 3);
        }

        [Fact]

        public void RampIsBlackAtZeroAndBlueAtTheLowAnchor()
        {
            Vector3 cold = TemperatureScale.ToHsv(0f);
            Assert.Equal(-1f, cold.Z, 3);

            Vector3 low = TemperatureScale.ToHsv(TemperatureScale.DefaultLow);
            Assert.Equal(240f / 360f, low.X, 3);
        }

        [Fact]

        public void HueSweepsDownFromBlueToRedBetweenTheAnchors()
        {
            float previousHue = float.MaxValue;
            for (float t = TemperatureScale.DefaultLow; t <= TemperatureScale.DefaultHigh; t += 10f)
            {
                float hue = TemperatureScale.ToHsv(t).X;
                Assert.True(hue <= previousHue + 1e-5f, "hue must not increase at " + t);
                previousHue = hue;
            }

            Assert.Equal(0f, TemperatureScale.ToHsv(TemperatureScale.DefaultHigh).X, 3);
        }

        [Fact]

        public void AboveTheHighAnchorSaturationFallsTowardWhite()
        {
            float atHigh = TemperatureScale.ToHsv(TemperatureScale.DefaultHigh).Y;
            float atMax = TemperatureScale.ToHsv(TemperatureScale.DefaultMax).Y;

            Assert.Equal(1f, atHigh, 3);
            Assert.True(atMax < atHigh);
        }

        [Fact]

        public void ValuesAreClampedRatherThanExtrapolated()
        {
            Vector3 beyond = TemperatureScale.ToHsv(999999f);
            Vector3 atMax = TemperatureScale.ToHsv(TemperatureScale.DefaultMax);
            Assert.Equal(atMax, beyond);

            Vector3 belowZero = TemperatureScale.ToHsv(-500f);
            Assert.Equal(TemperatureScale.ToHsv(0f), belowZero);
        }

        [Fact]

        public void DegenerateAnchorsDoNotProduceInfinities()
        {
            Vector3 colour = TemperatureScale.ToHsv(100f, 0f, 0f, 0f);
            Assert.False(float.IsNaN(colour.X) || float.IsInfinity(colour.X));
            Assert.False(float.IsNaN(colour.Y) || float.IsInfinity(colour.Y));
            Assert.False(float.IsNaN(colour.Z) || float.IsInfinity(colour.Z));
        }

        [Theory]
        [InlineData(1000f, 267f, 500f)]
        [InlineData(1400f, 1f, 1000f)]
        [InlineData(20000f, 100f, 5000f)]
        [InlineData(100f, 5f, 90f)]

        public void TheCoreRampReproducesTheOneTheOverlaysUsedToCarry(float max, float low, float high)
        {
            for (float t = -50f; t <= max * 1.2f; t += max / 200f)
            {
                Vector3 legacy = LegacyFormulas.TemperatureColor(t, max, low, high);
                Vector3 current = TemperatureScale.ToHsv(t, max, low, high);

                Assert.Equal(legacy.X, current.X, 5);
                Assert.Equal(legacy.Y, current.Y, 5);
                Assert.Equal(legacy.Z, current.Z, 5);
            }
        }

        [Fact]

        public void AFullyExposedBlockUsedToColourToNaN()
        {
            Vector3 legacy = LegacyFormulas.TemperatureColor(6f, 6f, 0f, 6f);
            Assert.True(float.IsNaN(legacy.Y));

            Vector3 current = TemperatureScale.ToHsv(6f, 6f, 0f, 6f);
            Assert.False(float.IsNaN(current.X) || float.IsNaN(current.Y) || float.IsNaN(current.Z));
            Assert.Equal(0f, current.X, 5);
            Assert.Equal(1f, current.Y, 5);
        }
    }

    /// <summary>
    /// Whether a planet stands between a grid and the sun, worked out analytically rather than by casting a ray across millions of metres.
    ///
    /// <para>
    /// Three regimes, because the fitted horizon curve behaves differently in each: just above the
    /// surface, where most of the sky is below ground; far away, where the terminator is sharp; and
    /// the plain case of the sun behind the body.
    /// </para>
    /// </summary>
    public class OcclusionMathTests
    {
        [Fact]

        public void VisualSizeShrinksWithDistance()
        {
            double near = OcclusionMath.VisualSize(1000d, 60000d);
            double far = OcclusionMath.VisualSize(10000000d, 60000d);
            Assert.True(near > far);
        }

        [Fact]

        public void SunBehindThePlanetIsOccluded()
        {
            Vector3D planet = Vector3D.Zero;
            double radius = 60000d;

            Vector3D observer = new Vector3D(0d, 0d, radius + 1000d);


            Vector3 sunBehind = new Vector3(0f, 0f, -1f);
            Assert.True(OcclusionMath.IsOccludedBySphere(observer, planet, radius, sunBehind));
        }

        [Fact]

        public void SunOverheadIsNotOccluded()
        {
            Vector3D planet = Vector3D.Zero;
            double radius = 60000d;

            Vector3D observer = new Vector3D(0d, 0d, radius + 1000d);


            Vector3 sunAbove = new Vector3(0f, 0f, 1f);
            Assert.False(OcclusionMath.IsOccludedBySphere(observer, planet, radius, sunAbove));
        }

        [Fact]

        public void FarFromThePlanetTheTerminatorIsSharp()
        {
            Vector3D planet = Vector3D.Zero;
            double radius = 60000d;

            Vector3D observer = new Vector3D(0d, 0d, 100000000d);

            Vector3 sideways = Vector3.Normalize(new Vector3(1f, 0f, -0.01f));
            Assert.False(OcclusionMath.IsOccludedBySphere(observer, planet, radius, sideways));
        }
    }
}

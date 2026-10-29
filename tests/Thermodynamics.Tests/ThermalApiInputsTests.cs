using Thermodynamics.Core;

namespace Thermodynamics.Tests
{
    /// <summary>Scalar API-boundary checks: invalid mod values must not enter live heat state.</summary>
    public class ThermalApiInputsTests
    {
        [Fact]
        public void NonfiniteTemperatureHeatAndThresholdValuesAreRejected()
        {
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                float result;
                Assert.False(ThermalApiInputs.IsFinite(invalid));
                Assert.False(ThermalApiInputs.TryTemperature(invalid, out result));
                Assert.False(ThermalApiInputs.TryAddHeat(300f, 4f, invalid, out result));
                Assert.False(ThermalApiInputs.TryAddHeat(invalid, 4f, 16f, out result));
                Assert.False(ThermalApiInputs.TryAddHeat(300f, invalid, 16f, out result));
            }
        }

        [Fact]
        public void SignedHeatKeepsTheExistingArithmeticAndTemperatureFloor()
        {
            float result;
            Assert.True(ThermalApiInputs.TryTemperature(-50f, out result));
            Assert.Equal(ThermalConstants.MinimumTemperature, result);
            Assert.True(ThermalApiInputs.TryTemperature(400f, out result));
            Assert.Equal(400f, result);
            Assert.True(ThermalApiInputs.TryAddHeat(300f, 4f, 16f, out result));
            Assert.Equal(304f, result);
            Assert.True(ThermalApiInputs.TryAddHeat(300f, 4f, -16f, out result));
            Assert.Equal(296f, result);
            Assert.True(ThermalApiInputs.TryAddHeat(300f, 4f, -2000f, out result));
            Assert.Equal(ThermalConstants.MinimumTemperature, result);
            Assert.True(ThermalApiInputs.TryAddHeat(300f, .001f, -float.MaxValue, out result));
            Assert.Equal(ThermalConstants.MinimumTemperature, result);
        }

        [Fact]
        public void HeatingOverflowAndInvalidCapacityAreRejected()
        {
            float result;
            Assert.False(ThermalApiInputs.TryAddHeat(300f, .001f, float.MaxValue, out result));
            Assert.False(ThermalApiInputs.TryAddHeat(float.MaxValue, 1f, float.MaxValue, out result));
            Assert.False(ThermalApiInputs.TryAddHeat(300f, 0f, 16f, out result));
            Assert.False(ThermalApiInputs.TryAddHeat(300f, -1f, 16f, out result));
        }
    }
}

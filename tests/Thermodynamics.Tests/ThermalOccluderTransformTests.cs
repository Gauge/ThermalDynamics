using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Mixed-size grids retain stationary shadows and invalidate actual motion at the existing thresholds.</summary>
    public class ThermalOccluderTransformTests
    {
        [Theory]
        [InlineData(0.2d)]
        [InlineData(1d)]
        [InlineData(5d)]
        public void CellScaleDoesNotChangeMotionThresholds(double cellScale)
        {
            MatrixD previous = MatrixD.CreateScale(cellScale);
            MatrixD current = previous;
            Assert.False(ThermalOccluderTransform.Changed(ref previous, ref current));

            current = previous * MatrixD.CreateRotationY(0.02d);
            Assert.False(ThermalOccluderTransform.Changed(ref previous, ref current));
            current = previous * MatrixD.CreateRotationY(0.05d);
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
            current = previous * MatrixD.CreateRotationZ(0.05d);
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));

            current = previous;
            current.Translation = new Vector3D(1d, 0d, 0d);
            Assert.False(ThermalOccluderTransform.Changed(ref previous, ref current));
            current.Translation = new Vector3D(1.01d, 0d, 0d);
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));

            current = MatrixD.CreateScale(cellScale * 1.01d);
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
        }

        [Fact]
        public void InvalidOrDegenerateTransformsInvalidateConservatively()
        {
            MatrixD previous = MatrixD.Identity;
            MatrixD current = new MatrixD();
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
            current = previous;
            current.Up = current.Forward;
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
            current = previous;
            current.M11 = double.NaN;
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
            current = previous;
            current.Translation = new Vector3D(double.PositiveInfinity, 0d, 0d);
            Assert.True(ThermalOccluderTransform.Changed(ref previous, ref current));
        }

        [Fact]
        public void StationaryBlockChangesAndSameIdModelReplacementInvalidate()
        {
            var model = new GridModel(2.5f);
            var previous = new SunShadowMap.Occluder
            {
                Id = 7L, Model = model, ToOccluder = MatrixD.CreateScale(0.2d)
            };
            int capturedVersion = model.Version;
            var current = previous;
            Assert.False(ThermalOccluderTransform.Changed(previous, capturedVersion, current));

            var block = model.Add(BlockModel.Solid("blocker", Vector3I.One, 100f,
                BlockThermalProperties.Default()), Vector3I.Zero);
            Assert.True(ThermalOccluderTransform.Changed(previous, capturedVersion, current));
            capturedVersion = model.Version;
            Assert.False(ThermalOccluderTransform.Changed(previous, capturedVersion, current));
            model.Remove(block);
            Assert.True(ThermalOccluderTransform.Changed(previous, capturedVersion, current));

            // Same ID and version do not give a replacement model ownership of the old cache.
            current.Model = new GridModel(2.5f);
            Assert.Equal(0, current.Model.Version);
            Assert.True(ThermalOccluderTransform.Changed(previous, 0, current));
        }
    }
}

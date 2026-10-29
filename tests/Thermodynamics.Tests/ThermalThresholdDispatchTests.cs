using System;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Exercise the production callback boundary with core blocks, without a native session.</summary>
    public class ThermalThresholdDispatchTests
    {
        private static BlockInstance Block()
        {
            return new BlockInstance(BlockModel.Solid("block", Vector3I.One, 1f,
                new BlockThermalProperties()), Vector3I.Zero, BlockOrientation.Identity);
        }

        [Fact]
        public void AReplacementAtTheSameCellCannotReceiveTheOldCrossing()
        {
            BlockInstance original = Block();
            BlockInstance replacement = Block();
            var crossing = new ThresholdCrossing(7, original, 450f, 500f, true);
            int calls = 0;
            Action<int, int, float, float, bool> handler = (block, id, threshold, temperature, rising) =>
            {
                calls++;
                Assert.Equal(123, block);
                Assert.Equal(7, id);
                Assert.Equal(450f, threshold);
                Assert.Equal(500f, temperature);
                Assert.True(rising);
            };
            Func<int, bool> remove = id => throw new InvalidOperationException("unexpected removal");
            Action<Exception> report = error => throw new InvalidOperationException("unexpected report", error);

            Assert.True(ThermalThresholdDispatch.Raise(crossing, original, 123, handler, remove, report));
            Assert.False(ThermalThresholdDispatch.Raise(crossing, replacement, 123, handler, remove, report));
            Assert.False(ThermalThresholdDispatch.Raise(crossing, null, 123, handler, remove, report));
            Assert.Equal(1, calls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CallbackFailureRemovesItsThresholdBeforeReporting(bool removesItself)
        {
            var first = new ThermalThresholds();
            var second = new ThermalThresholds();
            var threshold = new ThermalThreshold(7, 450f, ThresholdDirection.Rising);
            first.Add(threshold);
            second.Add(threshold);
            Func<int, bool> remove = id =>
            {
                bool removed = first.Remove(id);
                second.Remove(id);
                return removed;
            };
            BlockInstance original = Block();
            var crossing = new ThresholdCrossing(7, original, 450f, 500f, true);
            var failure = new InvalidOperationException("consumer failure");
            int reports = 0;
            Action<int, int, float, float, bool> handler = (block, id, level, temperature, rising) =>
            {
                if (removesItself) remove(id);
                throw failure;
            };
            Action<Exception> report = error =>
            {
                Assert.Same(failure, error);
                Assert.Equal(0, first.Count);
                Assert.Equal(0, second.Count);
                reports++;
            };

            Assert.False(ThermalThresholdDispatch.Raise(crossing, original, 123, handler, remove, report));
            Assert.Equal(1, reports);
        }
    }
}

using System;
using Thermodynamics.Core;

namespace Thermodynamics.Tests
{
    /// <summary>Four parcels compare fused heat application and advection with frozen arithmetic across flow and size changes.</summary>
    public class CoolantApplyTests
    {
        [Fact]
        public void FusedApplyMatchesFrozenParcelArithmeticThroughFlowAndSizeChanges()
        {
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 300f);
            float[] expected = { 3f, 311.75f, 976.25f, 128f };
            for (int i = 0; i < expected.Length; i++) loop.Pipes.Add(null);
            loop.RefreshThermalMass();
            loop.EnsureSegmentWatts();
            for (int i = 0; i < expected.Length; i++) loop.SetSegmentTemperature(i, expected[i]);

            const float h = .5f, mass = 31.75f;
            float[] flows = { 0f, 1.5f, 2f, 5.5f, -7.25f, -1.5f, 0f, 3.25f };
            int[] sizes = { 4, 2, 4, 1, 0 };
            foreach (int size in sizes)
            {
                int previousSize = expected.Length;
                while (loop.Pipes.Count > size) loop.Pipes.RemoveAt(loop.Pipes.Count - 1);
                while (loop.Pipes.Count < size) loop.Pipes.Add(null);
                loop.RefreshThermalMass();
                Array.Resize(ref expected, size);
                for (int i = previousSize; i < size; i++) expected[i] = 300f;
                int shift = 0;
                float travelled = 0f;

                // Leave the previous size's watts buffer in place, as a public topology edit can.
                foreach (float flow in flows)
                {
                    loop.FlowSegmentsPerSecond = flow;
                    for (int i = 0; i < loop.SegmentWatts.Length; i++)
                        loop.SegmentWatts[i] = i == 0 ? -10000f : (i % 2 == 0 ? -1f : 1f) * 123.456f;

                    for (int i = 0; i < loop.SegmentWatts.Length; i++)
                    {
                        if (i >= expected.Length) continue;
                        float updated = expected[i] + (loop.SegmentWatts[i] * h / mass);
                        expected[i] = updated < ThermalConstants.MinimumTemperature
                            ? ThermalConstants.MinimumTemperature : updated;
                    }
                    FrozenAdvect(expected, flow, h, ref shift, ref travelled);
                    loop.ApplyWattsAndAdvect(h, mass);
                    AssertParcels(loop, expected, shift);
                }

                // Standalone public advection still computes its own mean.
                loop.FlowSegmentsPerSecond = -5.25f;
                FrozenAdvect(expected, loop.FlowSegmentsPerSecond, h, ref shift, ref travelled);
                loop.Advect(h);
                AssertParcels(loop, expected, shift);
                loop.EnsureSegmentWatts();
            }
        }

        private static void AssertParcels(CoolantLoop loop, float[] expected, int shift)
        {
            Assert.Equal(expected.Length, loop.ParcelCount);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]),
                    BitConverter.SingleToInt32Bits(loop.ParcelTemperature(i)));
                int parcel = (i - shift) % expected.Length;
                if (parcel < 0) parcel += expected.Length;
                Assert.Equal(parcel, loop.ParcelOf(i));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[parcel]),
                    BitConverter.SingleToInt32Bits(loop.SegmentTemperature(i)));
            }
        }

        // Frozen pre-fusion implementation: apply above, then a separate ordered sum and mix.
        private static void FrozenAdvect(float[] temperatures, float flow, float h,
            ref int shift, ref float travelled)
        {
            if (temperatures.Length < 2 || flow == 0f) return;
            float parcels = flow * h;
            if (parcels == 0f) return;
            travelled += parcels;
            int whole = (int)travelled;
            if (whole != 0)
            {
                travelled -= whole;
                shift = (shift + whole) % temperatures.Length;
                if (shift < 0) shift += temperatures.Length;
            }
            float magnitude = parcels < 0f ? -parcels : parcels;
            if (magnitude <= 1f) return;
            float fraction = 1f - (1f / magnitude);
            if (fraction <= 0f) return;
            if (fraction > 1f) fraction = 1f;
            float total = 0f;
            for (int i = 0; i < temperatures.Length; i++) total += temperatures[i];
            float mean = total / temperatures.Length;
            for (int i = 0; i < temperatures.Length; i++)
                temperatures[i] += fraction * (mean - temperatures[i]);
        }
    }
}

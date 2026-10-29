using System;

namespace Thermodynamics.Presentation
{
    /// <summary>Bounds display arithmetic while keeping absent data distinct from finite over-range heat.</summary>
    public struct ThermalVisionTemperatureData
    {
        public const float MaximumMeasuredKelvin=100000f;
        // Above every allowed manual/autorange high, but safe for float cap sums.
        // This value is a saturated display marker, not a measured temperature.
        public const float OverRangeKelvin=1000000f;
        private double sum;
        public int Count { get; private set; }

        public static bool TryDisplay(float kelvin,out float display)
        {
            display=float.NaN;
            if(float.IsNaN(kelvin) || float.IsInfinity(kelvin) || kelvin<0) return false;
            display=kelvin>MaximumMeasuredKelvin ? OverRangeKelvin : kelvin;
            return true;
        }

        public bool Add(float kelvin,out float display)
        {
            if(!TryDisplay(kelvin,out display)) return false;
            sum+=display; Count++; return true;
        }

        public bool TryMean(out float mean)
        {
            mean=Count>0 ? (float)(sum/Count) : float.NaN;
            return Count>0;
        }
    }
}

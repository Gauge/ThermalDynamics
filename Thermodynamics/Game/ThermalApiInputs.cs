using Thermodynamics.Core;

namespace Thermodynamics
{
    /// <summary>Validate mod-supplied heat mutations before changing live simulation state.</summary>
    internal static class ThermalApiInputs
    {
        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool TryTemperature(float kelvin, out float temperature)
        {
            temperature = 0f;
            if (!IsFinite(kelvin)) return false;
            temperature = kelvin < ThermalConstants.MinimumTemperature
                ? ThermalConstants.MinimumTemperature : kelvin;
            return true;
        }

        internal static bool TryAddHeat(float current, float capacity, float joules, out float temperature)
        {
            temperature = 0f;
            if (!IsFinite(current) || !IsFinite(capacity) || capacity <= 0f || !IsFinite(joules))
                return false;

            // Preserve the existing float arithmetic and signed cooling floor, including a very
            // large finite cooling request whose quotient is negative infinity.
            float updated = current + (joules / capacity);
            if (updated < ThermalConstants.MinimumTemperature)
            {
                temperature = ThermalConstants.MinimumTemperature;
                return true;
            }
            if (!IsFinite(updated)) return false;
            temperature = updated;
            return true;
        }
    }
}

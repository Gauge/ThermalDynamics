using System;
using Thermodynamics.Core;

namespace Thermodynamics
{
    /// <summary>Engine-independent event identity and callback-failure boundary.</summary>
    internal static class ThermalThresholdDispatch
    {
        internal static bool Raise<TBlock>(ThresholdCrossing crossing, BlockInstance current,
            TBlock block, Action<TBlock, int, float, float, bool> handler,
            Func<int, bool> remove, Action<Exception> report)
        {
            if (current == null || !ReferenceEquals(current, crossing.Block)) return false;
            try
            {
                handler(block, crossing.ThresholdId, crossing.Threshold, crossing.Temperature, crossing.Rising);
                return true;
            }
            catch (Exception error)
            {
                // Release every registration before reporting, including when a callback already
                // removed itself. A reporting failure must not keep a dead threshold scanning.
                remove(crossing.ThresholdId);
                report(error);
                return false;
            }
        }
    }
}

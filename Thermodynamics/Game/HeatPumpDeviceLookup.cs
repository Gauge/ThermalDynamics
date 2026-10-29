using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Thermodynamics.Core;

namespace Thermodynamics
{
    /// <summary>Build a short-lived adapter lookup without caching the Core's mutable device list.</summary>
    internal static class HeatPumpDeviceLookup
    {
        private sealed class IdentityComparer : IEqualityComparer<BlockInstance>
        {
            public bool Equals(BlockInstance left, BlockInstance right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(BlockInstance block)
            {
                return RuntimeHelpers.GetHashCode(block);
            }
        }

        internal static readonly IEqualityComparer<BlockInstance> Comparer = new IdentityComparer();

        internal static void Fill(IList<HeatPumpDevice> devices,
            Dictionary<BlockInstance, HeatPumpDevice> lookup)
        {
            lookup.Clear();
            for (int i = 0; i < devices.Count; i++)
            {
                HeatPumpDevice device = devices[i];
                BlockInstance block = device.Block;
                // GetHeatPump returns the first matching instance, including when another
                // block occupies the same coordinates or a consumer appends a duplicate.
                if (block != null && !lookup.ContainsKey(block)) lookup.Add(block, device);
            }
        }
    }
}

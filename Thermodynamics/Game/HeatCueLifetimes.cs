using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics
{
    /// <summary>Compact removed cues once after a batch of block removals, preserving draw order.</summary>
    internal static class HeatCueLifetimes
    {
        internal static void Prune(List<LitBlock> lit, HashSet<Vector3I> live)
        {
            int write = 0;
            for (int read = 0; read < lit.Count; read++)
            {
                LitBlock cue = lit[read];
                if (!live.Contains(cue.Position)) continue;
                if (write != read) lit[write] = cue;
                write++;
            }
            if (write < lit.Count) lit.RemoveRange(write, lit.Count - write);
        }
    }
}

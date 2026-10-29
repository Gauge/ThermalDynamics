using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Small draw snapshots cannot retain removed blocks or rebind their heat to replacements.</summary>
    public class HeatCueLifetimeTests
    {
        [Fact]
        public void RemovedCuesDisappearBeforeLazyGeometryAndLightReduction()
        {
            var lit = new List<LitBlock>
            {
                new LitBlock { Position = Vector3I.Zero, Kelvin = 1800f, Glow = 1f },
                new LitBlock { Position = new Vector3I(2, 0, 0), Kelvin = 1000f, Glow = .5f },
                new LitBlock { Position = new Vector3I(9, 0, 0), Kelvin = 1700f, Glow = 1f },
                new LitBlock { Position = new Vector3I(4, 0, 0), Kelvin = 900f, Glow = .5f },
            };
            var live = new HashSet<Vector3I>(Vector3I.Comparer);
            foreach (LitBlock cue in lit) live.Add(cue.Position);

            // Removed cells stay absent from this cue snapshot even if another block occupies them.
            live.Remove(Vector3I.Zero);
            live.Remove(new Vector3I(9, 0, 0));
            HeatCueLifetimes.Prune(lit, live);
            Assert.Equal(2, lit.Count);
            Assert.Equal(new Vector3I(2, 0, 0), lit[0].Position);
            Assert.Equal(new Vector3I(4, 0, 0), lit[1].Position);
            GlowRegion region;
            Assert.True(GlowRegion.Reduce(lit, out region));
            Assert.Equal(new Vector3D(3, 0, 0), region.Centre);
            Assert.Equal(1f, region.RadiusCells);
            Assert.Equal(1000f, region.Kelvin);
            Assert.Equal(.5f, region.Glow);

            live.Clear();
            HeatCueLifetimes.Prune(lit, live);
            Assert.Empty(lit);
            Assert.False(GlowRegion.Reduce(lit, out region));
        }
    }
}

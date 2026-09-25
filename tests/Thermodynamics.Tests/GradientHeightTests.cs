using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A boundary layer cannot be taller than the atmosphere it is in.
    ///
    /// <para>
    /// `WindGradientHeight` ships at 600 m and several of the game's own worlds have less air than
    /// that standing over their ground — Titan's atmosphere is 285 m deep, Europa's and the Moon's
    /// are 570 and under, and Triton's peaks stand in vacuum outright. The vertical profile was
    /// being asked about heights with no air at them, and the only thing keeping the answer sane
    /// was the engine's own wind ceiling reaching zero first. backlog.md
    /// `B20`.
    /// </para>
    /// </summary>
    public class GradientHeightTests
    {
        [Fact]

        public void AThickAtmosphereLeavesTheConfiguredHeightAlone()
        {
            Assert.Equal(600f, WindProfile.GradientHeightIn(600f, 12000f), 3);
            Assert.Equal(600f, WindProfile.GradientHeightIn(600f, 600f), 3);
        }

        [Fact]

        public void AThinAtmosphereCapsIt()
        {
            Assert.Equal(285f, WindProfile.GradientHeightIn(600f, 285f), 3);
        }

        [Fact]

        public void ItNeverFallsBelowTheReferenceHeight()
        {
            Assert.Equal(WindProfile.ReferenceHeight, WindProfile.GradientHeightIn(600f, 1f), 3);
            Assert.Equal(WindProfile.ReferenceHeight, WindProfile.GradientHeightIn(600f, 0.001f), 3);
        }

        [Fact]

        public void AnAirlessWorldLeavesItAlone()
        {
            Assert.Equal(600f, WindProfile.GradientHeightIn(600f, 0f), 3);
            Assert.Equal(600f, WindProfile.GradientHeightIn(600f, -100f), 3);
        }

        [Fact]

        public void OnAShallowWorldTheProfileStopsWhereTheAirDoes()
        {
            const float Shallow = 285f;

            float capped = WindProfile.GradientHeightIn(600f, Shallow);
            float roughness = 0.03f;

            float atTop = WindProfile.Multiplier(Shallow, roughness, capped);
            float wayAbove = WindProfile.Multiplier(Shallow * 4f, roughness, capped);
            Assert.Equal(atTop, wayAbove, 4);

            float uncapped = WindProfile.Multiplier(Shallow * 2f, roughness, 600f);
            Assert.True(uncapped > atTop,
                "the uncapped profile should have been higher, or there was nothing to fix");
        }
    }
}

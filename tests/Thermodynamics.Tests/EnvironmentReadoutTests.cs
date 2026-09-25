using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The line the mod puts on screen as a matter of course**, and the one thing about it that
    /// can be checked outside a session: what it says.
    ///
    /// <para>
    /// The intent page decides that basic environmental information should be on screen without
    /// waiting for a failure, and sets the bar at *clear* rather than *present* — somebody who has
    /// never read this repository should be able to look at the screen and tell their ship is
    /// getting hotter. What that means in code is a decision about wording and thresholds, and a
    /// decision is a thing to pin; the drawing is a shell over it that only a session can judge
    /// (`B41`).
    /// </para>
    /// </summary>
    public class EnvironmentReadoutTests
    {
        private const float Critical = 1000f;

        private const float Ambient = 293.15f;

        [Fact]

        public void HotBeginsExactlyWhereTheGlowBegins()
        {
            float glow = Incandescence.GlowStartKelvin(Critical);

            Assert.Equal(EnvironmentReadout.Heat.Hot,
                EnvironmentReadout.State(Ambient, glow, Critical));

            Assert.Equal(EnvironmentReadout.Heat.Warm,
                EnvironmentReadout.State(Ambient, glow - 1f, Critical));

            Assert.Equal(0f, Incandescence.Glow(glow - 1f, Critical));
            Assert.True(Incandescence.Glow(glow + 1f, Critical) > 0f);
        }

        [Fact]

        public void WarmIsHalfTheClimbFromTheAirRatherThanHalfTheRating()
        {
            float glow = Incandescence.GlowStartKelvin(Critical);
            float half = Ambient + ((glow - Ambient) * EnvironmentReadout.WarmShare);

            Assert.Equal(EnvironmentReadout.Heat.Warm, EnvironmentReadout.State(Ambient, half + 1f, Critical));
            Assert.Equal(EnvironmentReadout.Heat.Cool, EnvironmentReadout.State(Ambient, half - 1f, Critical));

            float hotterAir = Ambient + 200f;
            Assert.Equal(EnvironmentReadout.Heat.Hot,
                EnvironmentReadout.State(hotterAir, hotterAir + ((glow - hotterAir) * 1.1f), Critical));

            Assert.Equal(EnvironmentReadout.Heat.Cool, EnvironmentReadout.State(Ambient, Ambient, Critical));
            Assert.Equal(EnvironmentReadout.Heat.Cool, EnvironmentReadout.State(hotterAir, hotterAir, Critical));
        }

        [Fact]

        public void TheClimbKeepsCountingPastTheGlow()
        {
            float glow = Incandescence.GlowStartKelvin(Critical);

            Assert.Equal(0f, EnvironmentReadout.Climb(Ambient, Ambient, Critical), 4);
            Assert.Equal(1f, EnvironmentReadout.Climb(Ambient, glow, Critical), 4);
            Assert.True(EnvironmentReadout.Climb(Ambient, Critical, Critical) > 1f);
        }

        [Fact]

        public void AWorldWithNoClimbToMeasureReadsCoolRatherThanInventingOne()
        {
            Assert.Equal(0f, EnvironmentReadout.Climb(Ambient, 5000f, 0f));
            Assert.Equal(EnvironmentReadout.Heat.Cool, EnvironmentReadout.State(Ambient, 5000f, 0f));

            float furnace = Incandescence.GlowStartKelvin(Critical) + 50f;
            Assert.Equal(0f, EnvironmentReadout.Climb(furnace, furnace + 500f, Critical));

            Assert.Equal(0f, EnvironmentReadout.Climb(Ambient, Ambient - 80f, Critical));

            Assert.Equal(0f, EnvironmentReadout.Climb(float.NaN, 400f, Critical));
            Assert.Equal(0f, EnvironmentReadout.Climb(Ambient, float.NaN, Critical));
            Assert.Equal(0f, EnvironmentReadout.Climb(Ambient, 400f, float.NaN));
        }

        [Fact]

        public void TheLineDropsTheShipsHalfRatherThanGuessingIt()
        {
            string whole = EnvironmentReadout.Line(Ambient, 400f, Critical);
            Assert.Contains("hull", whole);
            Assert.Contains(TemperatureScale.ToCelsiusString(Ambient), whole);

            string airOnly = EnvironmentReadout.Line(Ambient, 400f, 0f);
            Assert.DoesNotContain("hull", airOnly);
            Assert.Contains(TemperatureScale.ToCelsiusString(Ambient), airOnly);
        }

        [Fact]

        public void TheThreeStatesReadDifferently()
        {
            float glow = Incandescence.GlowStartKelvin(Critical);

            string cool = EnvironmentReadout.Line(Ambient, Ambient, Critical);
            string warm = EnvironmentReadout.Line(Ambient, Ambient + ((glow - Ambient) * 0.75f), Critical);
            string hot = EnvironmentReadout.Line(Ambient, glow + 10f, Critical);

            Assert.NotEqual(cool, warm);
            Assert.NotEqual(warm, hot);
            Assert.NotEqual(cool, hot);
        }
    }
}

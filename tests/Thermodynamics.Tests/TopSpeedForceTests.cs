using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The force that holds a ship under its cruise speed, and the switch that decides how far past
    /// it a ship may be pushed.
    ///
    /// <para>
    /// **This suite exists because `EnableSpeedBoost` shipped dead.** `ThermalGridTopSpeed` returned
    /// at <c>speed &lt;= cruise</c> and then asked, on the next line, whether boosting was off *and*
    /// the speed was at or below cruise — a condition that could never be true. The switch appeared
    /// in the settings file, in the settings menu and in a documented ladder of three rungs, and
    /// changed nothing in any world. Nothing caught it: the file needs a session, a physics
    /// component and a grid group, so every decision it made was made where no check could look.
    /// </para>
    ///
    /// <para>
    /// So the tests that matter here are the ones that ask **what the switch changes**, not whether
    /// the arithmetic is pretty. A dead switch passes any test that only exercises one side of it.
    /// </para>
    /// </summary>
    public class TopSpeedForceTests
    {
        private const float Resistance = 1.5f;
        private const float BoostCeiling = 140f;

        [Fact]

        public void TurningBoostOffChangesWhereTheShipIsHeld()
        {
            float on = TopSpeedForce.Ceiling(true, 80f, BoostCeiling);
            float off = TopSpeedForce.Ceiling(false, 80f, BoostCeiling);

            Assert.NotEqual(on, off);

            Assert.Equal(80f, off);
            Assert.Equal(BoostCeiling, on);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(60f)]
        [InlineData(140f)]
        [InlineData(1000f)]

        public void WithBoostOffTheBoostDialDoesNothing(float boostCeiling)
        {
            Assert.Equal(110f, TopSpeedForce.Ceiling(false, 110f, boostCeiling));
        }

        [Fact]

        public void ABoostCeilingBelowCruiseDoesNotHoldAShipUnderItsOwnCruise()
        {
            Assert.Equal(110f, TopSpeedForce.Ceiling(true, 110f, 60f));
            Assert.Equal(110f, TopSpeedForce.Ceiling(true, 110f, 0f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(40f)]
        [InlineData(79.9f)]
        [InlineData(80f)]

        public void NoForceAtOrBelowCruise(float speed)
        {
            Assert.Equal(0f, TopSpeedForce.Newtons(Resistance, 1000000f, 80f, speed));
        }

        [Fact]

        public void TheForceIsTheOriginalsAtAWorkedPoint()
        {
            Assert.Equal(300000f, TopSpeedForce.Newtons(1.5f, 1000000f, 80f, 100f), 1);
        }

        [Fact]

        public void TheForceRisesWithTheOverspeedAndApproachesItsAsymptote()
        {
            float previous = -1f;
            for (int speed = 81; speed <= 400; speed++)
            {
                float newtons = TopSpeedForce.Newtons(Resistance, 1000f, 80f, speed);
                Assert.True(newtons > previous, "the force fell off at " + speed + " m/s");
                previous = newtons;
            }

            Assert.True(previous < Resistance * 1000f);
            Assert.Equal(Resistance * 1000f, TopSpeedForce.Newtons(Resistance, 1000f, 80f, 1e9f), 0);
        }

        [Fact]

        public void GarbageInputsProduceNoForceRatherThanGarbageForce()
        {
            Assert.Equal(0f, TopSpeedForce.Newtons(Resistance, 1000f, 80f, float.NaN));
            Assert.Equal(0f, TopSpeedForce.Newtons(Resistance, float.NaN, 80f, 200f));
            Assert.Equal(0f, TopSpeedForce.Newtons(Resistance, 0f, 80f, 200f));
            Assert.Equal(0f, TopSpeedForce.Newtons(0f, 1000f, 80f, 200f));
            Assert.Equal(0f, TopSpeedForce.Newtons(Resistance, 1000f, 80f, -200f));
        }
    }
}

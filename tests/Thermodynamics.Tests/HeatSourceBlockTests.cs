using Thermodynamics;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The debug heat source block's dial, which is the half of that block a session cannot check.
    ///
    /// <para>
    /// The block itself is two sliders and a call into <c>ThermalHeatSources</c>, and whether it
    /// radiates is answered by looking at it. What cannot be answered by looking is whether the
    /// number under the slider is the number the player set: a <b>clamp</b> that saturates without
    /// saying so and a <b>codec</b> that reloads a world at a different output both look exactly
    /// like a correct block, because the readout echoes whatever the dial ended up holding.
    /// </para>
    ///
    /// <para>
    /// The third thing pinned here is that the block and <c>/thermal heat</c> agree about what a
    /// heat source is. They are two ways of driving one registry and each has its own defaults; a
    /// pair like that is the drift <c>wired-to-nothing</c> keeps finding, so the agreement is
    /// asserted rather than assumed.
    /// </para>
    /// </summary>
    public class HeatSourceBlockTests
    {
        [Fact]

        public void TheDefaultDialSitsInsideItsOwnLimits()
        {
            HeatSourceBlockSetting setting = HeatSourceBlockSetting.Default();

            Assert.InRange(setting.Watts, HeatSourceBlockSetting.MinWatts, HeatSourceBlockSetting.MaxWatts);
            Assert.InRange(setting.Range, HeatSourceBlockSetting.MinRange, HeatSourceBlockSetting.MaxRange);

            Assert.Equal(setting.Watts, setting.Clamped().Watts);
            Assert.Equal(setting.Range, setting.Clamped().Range);
        }

        [Fact]

        public void TheBlockAndTheChatCommandAgreeOnWhatAHeatSourceReaches()
        {
            Assert.Equal(HeatSourceCommand.DefaultRange, HeatSourceBlockSetting.DefaultRange);
        }

        [Fact]

        public void BothDialsAreHeldInsideTheirLimits()
        {

            HeatSourceBlockSetting low = new HeatSourceBlockSetting(-5f, -5f).Clamped();
            Assert.Equal(HeatSourceBlockSetting.MinWatts, low.Watts);
            Assert.Equal(HeatSourceBlockSetting.MinRange, low.Range);
            Assert.Equal(0f, low.Watts);


            HeatSourceBlockSetting high = new HeatSourceBlockSetting(1e12f, 1e12f).Clamped();
            Assert.Equal(HeatSourceBlockSetting.MaxWatts, high.Watts);
            Assert.Equal(HeatSourceBlockSetting.MaxRange, high.Range);


            HeatSourceBlockSetting inside = new HeatSourceBlockSetting(2.5e6f, 350f).Clamped();
            Assert.Equal(2.5e6f, inside.Watts);
            Assert.Equal(350f, inside.Range);
        }

        [Fact]

        public void ANotANumberIsRejectedRatherThanPassedThrough()
        {
            HeatSourceBlockSetting setting =

                new HeatSourceBlockSetting(float.NaN, float.NaN).Clamped();

            Assert.False(float.IsNaN(setting.Watts));
            Assert.False(float.IsNaN(setting.Range));
            Assert.Equal(HeatSourceBlockSetting.MinWatts, setting.Watts);
            Assert.Equal(HeatSourceBlockSetting.MinRange, setting.Range);
        }


        [Fact]

        public void ADialAtZeroAsksForNoSourceAtAll()
        {
            Assert.False(new HeatSourceBlockSetting(0f, 200f).HasOutput);
            Assert.False(new HeatSourceBlockSetting(-1f, 200f).Clamped().HasOutput);
            Assert.False(new HeatSourceBlockSetting(float.NaN, 200f).Clamped().HasOutput);

            Assert.True(new HeatSourceBlockSetting(1f, 200f).HasOutput);
            Assert.True(HeatSourceBlockSetting.Default().HasOutput);
        }

        [Fact]

        public void TheOutputDialReachesZeroAndItsMaximumExactly()
        {
            Assert.Equal(0f, HeatSourceBlockSetting.WattsAtPosition(0f));
            Assert.Equal(HeatSourceBlockSetting.MaxWatts, HeatSourceBlockSetting.WattsAtPosition(1f));

            Assert.Equal(0f, HeatSourceBlockSetting.PositionOfWatts(0f));
            Assert.Equal(1f, HeatSourceBlockSetting.PositionOfWatts(HeatSourceBlockSetting.MaxWatts));

            Assert.Equal(0f, HeatSourceBlockSetting.WattsAtPosition(-1f));
            Assert.Equal(HeatSourceBlockSetting.MaxWatts, HeatSourceBlockSetting.WattsAtPosition(2f));
            Assert.Equal(0f, HeatSourceBlockSetting.WattsAtPosition(float.NaN));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.1f)]
        [InlineData(0.25f)]
        [InlineData(0.5f)]
        [InlineData(0.75f)]
        [InlineData(0.9f)]
        [InlineData(1f)]

        public void APositionSurvivesBeingTurnedIntoWattsAndBack(float position)
        {
            float watts = HeatSourceBlockSetting.WattsAtPosition(position);

            Assert.Equal(position, HeatSourceBlockSetting.PositionOfWatts(watts), 3);
        }

        [Fact]

        public void TheDialSpendsItsTravelWhereTheUsefulOutputsAre()
        {
            float previous = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float watts = HeatSourceBlockSetting.WattsAtPosition(i / 100f);
                Assert.True(watts > previous, "the dial doubled back at position " + (i / 100f));
                previous = watts;
            }

            Assert.InRange(HeatSourceBlockSetting.WattsAtPosition(0.1f), 2.9e3f, 3.1e3f);
            Assert.InRange(HeatSourceBlockSetting.WattsAtPosition(0.5f), 0.99e6f, 1.01e6f);

            float defaultPosition = HeatSourceBlockSetting.PositionOfWatts(
                HeatSourceBlockSetting.DefaultWatts);
            Assert.InRange(defaultPosition, 0.5f, 0.75f);
        }

        [Theory]
        [InlineData(0f, 10f)]
        [InlineData(1000f, 10f)]
        [InlineData(5000000f, 200f)]
        [InlineData(5371234.5f, 337f)]
        [InlineData(1000000000f, 1000f)]

        public void TheDialSurvivesASaveAndReload(float watts, float range)
        {

            HeatSourceBlockSetting saved = new HeatSourceBlockSetting(watts, range);

            HeatSourceBlockSetting loaded;
            Assert.True(HeatSourceBlockSetting.TryLoad(saved.Save(), out loaded));

            Assert.Equal(watts, loaded.Watts);
            Assert.Equal(range, loaded.Range);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("2|5000000|200")]
        [InlineData("1|5000000")]
        [InlineData("1|not-a-number|200")]

        public void AnUnreadableSaveLeavesTheBlockAtItsDefault(string stored)
        {
            HeatSourceBlockSetting loaded;
            Assert.False(HeatSourceBlockSetting.TryLoad(stored, out loaded));

            Assert.Equal(HeatSourceBlockSetting.DefaultWatts, loaded.Watts);
            Assert.Equal(HeatSourceBlockSetting.DefaultRange, loaded.Range);
        }

        [Fact]

        public void ASaveFromOutsideTheDialLoadsClamped()
        {
            HeatSourceBlockSetting loaded;
            Assert.True(HeatSourceBlockSetting.TryLoad("1|1E+15|999999", out loaded));

            Assert.Equal(HeatSourceBlockSetting.MaxWatts, loaded.Watts);
            Assert.Equal(HeatSourceBlockSetting.MaxRange, loaded.Range);
        }

        [Fact]

        public void TheReadoutsIrradianceIsTheSolversOwn()
        {

            HeatSourceBlockSetting setting = new HeatSourceBlockSetting(5e6f, 200f);

            Assert.Equal(
                HeatSourceMath.Irradiance(VRageMath.Vector3D.Zero, 5e6f, 200f, new VRageMath.Vector3D(50.0, 0.0, 0.0)),
                setting.IrradianceAt(50f));

            Assert.Equal(0f, setting.IrradianceAt(201f));
        }

        [Fact]

        public void AMegawattIsTwoWattsPerSquareMetreAtTwoHundredMetres()
        {

            HeatSourceBlockSetting setting = new HeatSourceBlockSetting(1e6f, 1000f);

            Assert.Equal(2f, setting.IrradianceAt(200f), 1);
            Assert.Equal(78f, setting.IrradianceAt(32f), 0);
        }
    }
}

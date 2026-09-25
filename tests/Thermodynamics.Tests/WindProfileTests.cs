using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The wind's vertical structure and its daily cycle.
    ///
    /// The game supplies neither: <c>MyPlanet.GetWindSpeed</c> is the planet's maximum scaled
    /// linearly by air density, so it falls monotonically from the ground to the edge of the
    /// atmosphere and is the same at midnight as at noon. These pin the two curves that replace it —
    /// a logarithmic rise to a plateau, and a daily swing that reverses with height.
    /// </summary>
    public class WindProfileTests
    {
        private const float Roughness = 0.03f;
        private const float Gradient = 600f;


        private static float At(float height)
        {
            return WindProfile.Multiplier(height, Roughness, Gradient);
        }

        [Fact]

        public void TheReferenceHeightIsTheUnit()
        {
            Assert.Equal(1f, At(WindProfile.ReferenceHeight), 4);
        }

        [Fact]

        public void WindStrengthensWithHeightThroughTheSurfaceLayer()
        {

            float previous = At(1f);

            for (float height = 2f; height <= 500f; height *= 1.5f)
            {

                float current = At(height);
                Assert.True(current > previous,
                    "wind at " + height + " m should exceed the height below it");
                previous = current;
            }
        }

        [Fact]

        public void AndThenStopsStrengthening()
        {
            Assert.Equal(At(Gradient), At(Gradient * 2f), 5);
            Assert.Equal(At(Gradient), At(50000f), 5);
        }

        [Fact]

        public void GroundLevelIsCalmerThanHeadHeightIsCalmerThanAMast()
        {
            Assert.InRange(At(0.5f), 0.4f, 0.6f);
            Assert.InRange(At(2f), 0.7f, 0.8f);
            Assert.InRange(At(100f), 1.3f, 1.5f);
        }

        [Fact]

        public void RoughGroundSlowsTheSurfaceAndSteepensTheClimb()
        {
            float meadow = WindProfile.Multiplier(2f, 0.03f, Gradient);
            float forest = WindProfile.Multiplier(2f, 0.5f, Gradient);

            Assert.True(forest < meadow, "rough ground should be calmer at head height");

            float meadowClimb = WindProfile.Multiplier(200f, 0.03f, Gradient) / meadow;
            float forestClimb = WindProfile.Multiplier(200f, 0.5f, Gradient) / forest;

            Assert.True(forestClimb > meadowClimb, "and should gain more by climbing out of it");
        }

        [Fact]

        public void ABlockOnTheGroundStillFeelsSomeWind()
        {
            Assert.True(At(0f) > 0.3f);
            Assert.Equal(At(0f), At(WindProfile.MinimumHeight), 5);
        }

        [Fact]

        public void NonsenseInputsDoNotProduceNonsenseWind()
        {
            Assert.True(WindProfile.Multiplier(10f, 0f, Gradient) > 0f);
            Assert.True(WindProfile.Multiplier(-5f, Roughness, Gradient) > 0f);
            Assert.True(WindProfile.Multiplier(100f, Roughness, 0f) > 0f);
        }


        private const float Amplitude = 0.35f;
        private const float Crossover = 80f;


        private static float Diurnal(float heating, float height)
        {
            return WindProfile.Diurnal(heating, height, Amplitude, Crossover, Gradient);
        }

        [Fact]

        public void SurfaceWindPeaksInTheAfternoonAndDropsBeforeDawn()
        {

            float afternoon = Diurnal(1f, 2f);

            float predawn = Diurnal(0f, 2f);

            Assert.True(afternoon > 1f, "the ground is windiest when it is warmest");
            Assert.True(predawn < 1f, "and calmest at the coldest hour");
            Assert.True(afternoon > predawn * 1.5f);
        }

        [Fact]

        public void AloftItIsTheOtherWayRound()
        {

            float afternoon = Diurnal(1f, 400f);

            float predawn = Diurnal(0f, 400f);

            Assert.True(predawn > 1f, "wind aloft should strengthen overnight");
            Assert.True(afternoon < 1f, "and slacken by day");
        }

        [Fact]

        public void ThereIsAHeightAtWhichTheDayDoesNotMatter()
        {
            Assert.Equal(1f, Diurnal(0f, Crossover), 5);
            Assert.Equal(1f, Diurnal(0.5f, Crossover), 5);
            Assert.Equal(1f, Diurnal(1f, Crossover), 5);
        }

        [Fact]

        public void MiddayAndMidnightStraddleTheMean()
        {
            for (float height = 0f; height < 500f; height += 60f)
            {

                float sum = Diurnal(0f, height) + Diurnal(1f, height);
                Assert.Equal(2f, sum, 4);
            }
        }

        [Fact]

        public void TheReversalDoesNotDeepenPastFull()
        {
            Assert.Equal(Diurnal(0f, Crossover * 2f), Diurnal(0f, Gradient), 5);
        }

        [Fact]

        public void TheCycleIsGoneAboveTheBoundaryLayer()
        {
            Assert.True(Diurnal(0f, Gradient) > 1.3f, "still a jet at the top of the boundary layer");
            Assert.True(Diurnal(0f, Gradient * 1.5f) < Diurnal(0f, Gradient), "and fading above it");
            Assert.Equal(1f, Diurnal(0f, Gradient * 2f), 5);
            Assert.Equal(1f, Diurnal(0f, 7237f), 5);
            Assert.Equal(1f, Diurnal(1f, 7237f), 5);
        }

        [Fact]

        public void ADisabledCycleChangesNothing()
        {
            Assert.Equal(1f, WindProfile.Diurnal(0f, 2f, 0f, Crossover, Gradient), 5);
            Assert.Equal(1f, WindProfile.Diurnal(1f, 400f, 0f, Crossover, Gradient), 5);
            Assert.Equal(1f, WindProfile.Diurnal(1f, 400f, Amplitude, 0f, Gradient), 5);
        }

        [Fact]

        public void TheCycleNeverTurnsTheWindNegative()
        {
            for (float heating = 0f; heating <= 1f; heating += 0.1f)
            {
                for (float height = 0f; height < 1000f; height += 50f)
                {
                    Assert.True(WindProfile.Diurnal(heating, height, 1f, Crossover, Gradient) >= 0f);
                }
            }
        }


        [Fact]

        public void HeatingWithNoHistoryTakesTheSunItFinds()
        {
            Assert.Equal(0.8f, WindProfile.Heating(-1f, 0.8f, 0.1f, 45f), 4);
        }

        [Fact]

        public void HeatingLagsTheSunRatherThanFollowingIt()
        {
            float heating = WindProfile.Heating(0.2f, 1f, 1f, 45f);

            Assert.True(heating > 0.2f, "it should be climbing toward the sun");
            Assert.True(heating < 0.5f, "but nowhere near reaching it in one second");
        }

        [Fact]

        public void TheSunBelowTheHorizonIsNoHeatingAtAll()
        {
            Assert.Equal(WindProfile.Heating(-1f, -0.1f, 1f, 45f), WindProfile.Heating(-1f, -1f, 1f, 45f), 5);
            Assert.Equal(0f, WindProfile.Heating(-1f, -0.5f, 1f, 45f), 5);
        }

        [Fact]

        public void HeatingStaysInsideItsRange()
        {
            float heating = 0.5f;
            for (int i = 0; i < 200; i++)
            {
                heating = WindProfile.Heating(heating, i % 2 == 0 ? 1f : -1f, 1f, 45f);
                Assert.InRange(heating, 0f, 1f);
            }
        }
    }
}

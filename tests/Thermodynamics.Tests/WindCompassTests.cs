using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The wind turned into the viewer's frame, which is the only form of it a player can act on.
    ///
    /// The field itself is pinned by <see cref="WindFieldTests"/>. What these hold is the handedness
    /// — that "to the right" on screen is the same side as the viewer's right hand — and the cases
    /// where there is no answer to give, since a needle that swings wildly when the player looks at
    /// the sky is worse than one that stops.
    /// </summary>
    public class WindCompassTests
    {

        private static readonly Vector3 Up = new Vector3(0f, 1f, 0f);


        private static readonly Vector3 North = new Vector3(0f, 0f, -1f);


        private static readonly Vector3 East = new Vector3(1f, 0f, 0f);


        private static float BearingOf(Vector3 wind, Vector3 forward)
        {
            float degrees;
            Assert.True(WindCompass.Bearing(wind, forward, Up, out degrees),
                "expected a bearing for this wind and facing");
            return degrees;
        }

        [Fact]

        public void WindGoingTheWayYouFaceReadsDeadAhead()
        {
            Assert.Equal(0f, BearingOf(North * 12f, North), 3);
        }

        [Fact]

        public void WindGoingRightOfYouReadsNinetyDegrees()
        {
            Assert.Equal(90f, BearingOf(East * 12f, North), 3);
        }

        [Fact]

        public void WindInYourFaceReadsAstern()
        {
            Assert.Equal(180f, BearingOf(-North * 12f, North), 3);
        }

        [Fact]

        public void WindGoingLeftOfYouReadsTwoSeventy()
        {
            Assert.Equal(270f, BearingOf(-East * 12f, North), 3);
        }

        [Fact]

        public void TheBearingIsRelativeToTheFacingRatherThanToTheWorld()
        {
            Assert.Equal(0f, BearingOf(North * 5f, North), 3);
            Assert.Equal(180f, BearingOf(North * 5f, -North), 3);
        }

        [Fact]

        public void LookingUpOrDownDoesNotSwingTheNeedle()
        {
            Vector3 wind = East * 9f;


            float level = BearingOf(wind, North);

            float pitched = BearingOf(wind, Vector3.Normalize(North + (Up * 4f)));

            float steep = BearingOf(wind, Vector3.Normalize(North - (Up * 9f)));

            Assert.Equal(level, pitched, 2);
            Assert.Equal(level, steep, 2);
        }

        [Fact]

        public void AViewerLookingStraightUpHasNoBearingToGive()
        {
            float degrees;
            Assert.False(WindCompass.Bearing(East * 9f, Up, Up, out degrees));
            Assert.Equal(0f, degrees);
        }

        [Fact]

        public void StillAirHasNoBearingToGive()
        {
            float degrees;
            Assert.False(WindCompass.Bearing(Vector3.Zero, North, Up, out degrees));
        }

        [Fact]

        public void AVerticalWindHasNoBearingEitherAlthoughTheFieldMakesNone()
        {
            float degrees;
            Assert.False(WindCompass.Bearing(Up * 40f, North, Up, out degrees));
        }

        [Fact]

        public void HeadAndCrossComponentsSplitTheWindBetweenThem()
        {
            Vector3 wind = Vector3.Normalize(North + East) * 20f;

            float along = WindCompass.Along(wind, North, Up);
            float across = WindCompass.Across(wind, North, Up);

            Assert.Equal(20f, (float)Math.Sqrt((along * along) + (across * across)), 3);
            Assert.True(along > 0f, "wind going the way you face is a tailwind, so positive");
            Assert.True(across > 0f, "wind going right of you is positive across");
        }

        [Fact]

        public void AHeadwindIsNegativeAlongTheFacing()
        {
            Assert.True(WindCompass.Along(-North * 15f, North, Up) < 0f);
        }

        [Fact]

        public void TheVerticalPartOfTheWindIsIgnoredByBothComponents()
        {
            Vector3 flat = East * 10f;
            Vector3 climbing = flat + (Up * 30f);

            Assert.Equal(WindCompass.Along(flat, North, Up), WindCompass.Along(climbing, North, Up), 4);
            Assert.Equal(WindCompass.Across(flat, North, Up), WindCompass.Across(climbing, North, Up), 4);
        }

        [Fact]

        public void ASectorIsCentredOnItsOwnBearingRatherThanStartingAtIt()
        {
            Assert.Equal(0, WindCompass.Sector(0f));
            Assert.Equal(0, WindCompass.Sector(20f));
            Assert.Equal(0, WindCompass.Sector(340f));
            Assert.Equal(1, WindCompass.Sector(45f));
            Assert.Equal(2, WindCompass.Sector(90f));
            Assert.Equal(7, WindCompass.Sector(315f));
        }

        [Fact]

        public void EverySectorHasAName()
        {
            for (int i = 0; i < 8; i++)
            {
                Assert.False(string.IsNullOrEmpty(WindCompass.SectorName(i)));
            }

            Assert.Equal("ahead", WindCompass.SectorName(WindCompass.Sector(0f)));
            Assert.Equal("right", WindCompass.SectorName(WindCompass.Sector(90f)));
            Assert.Equal("astern", WindCompass.SectorName(WindCompass.Sector(180f)));
            Assert.Equal("left", WindCompass.SectorName(WindCompass.Sector(270f)));
        }

        [Fact]

        public void AnAngleIsAlwaysFoldedIntoOneTurn()
        {
            Assert.Equal(10f, WindCompass.Normalise(370f), 3);
            Assert.Equal(350f, WindCompass.Normalise(-10f), 3);
            Assert.Equal(0f, WindCompass.Normalise(float.NaN), 3);
        }

        [Fact]

        public void TheFieldsOwnWindReadsAsABearingWhereverItBlows()
        {

            Vector3 axis = new Vector3(0f, 1f, 0f);
            int read = 0;
            int calm = 0;

            for (int latitude = -85; latitude <= 85; latitude += 5)
            {
                double radians = latitude * Math.PI / 180d;
                Vector3 up = Vector3.Normalize(new Vector3(
                    (float)Math.Cos(radians), (float)Math.Sin(radians), 0f));

                Vector3 wind = WindField.Direction(up, axis);

                Vector3 facing = Vector3.Normalize(Vector3.Cross(axis, up));

                float degrees;
                bool bearing = WindCompass.Bearing(wind, facing, up, out degrees);

                if (WindField.BandStrength(up, axis) < 1e-3f)
                {
                    Assert.False(bearing, "a calm at latitude " + latitude + " reported a bearing");
                    calm++;
                    continue;
                }

                Assert.True(bearing, "no bearing at latitude " + latitude);
                Assert.InRange(degrees, 0f, 360f);
                read++;
            }

            Assert.True(read > 20, "only " + read + " latitudes had a wind to read");
            Assert.True(calm > 0, "no band edge was swept, so the calm case is untested");
        }
    }
}

using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What the ground does to the wind over it: speeds it up over a rise, shelters behind a ridge,
    /// and steers it along a valley.
    ///
    /// Every case here is a piece of terrain built by hand as a ring of relative heights — a cone, a
    /// wall, a trench, an even slope — so the shapes are unambiguous and the expected answer is the
    /// one anybody who has stood on that shape would give.
    /// </summary>
    public class WindTerrainTests
    {

        private static readonly Vector3 North = new Vector3(0f, 0f, -1f);

        private static readonly Vector3 East = new Vector3(1f, 0f, 0f);

        private const float Inner = 150f;
        private const float Outer = 300f;


        private static float[] Flat()
        {
            return new float[WindTerrain.SampleCount];
        }


        private static float[] Hill(float fall)
        {
            float[] heights = new float[WindTerrain.SampleCount];
            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                heights[WindTerrain.Index(0, i)] = -fall * 0.5f;
                heights[WindTerrain.Index(1, i)] = -fall;
            }
            return heights;
        }


        private static float[] Hollow(float rise)
        {

            float[] heights = Hill(rise);
            for (int i = 0; i < heights.Length; i++) heights[i] = -heights[i];
            return heights;
        }


        private static float[] Slope(int towards, float rise)
        {
            float[] heights = new float[WindTerrain.SampleCount];
            double axis = towards * (2d * Math.PI / WindTerrain.Bearings);

            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                double angle = i * (2d * Math.PI / WindTerrain.Bearings);
                float along = (float)Math.Cos(angle - axis);

                heights[WindTerrain.Index(0, i)] = rise * 0.5f * along;
                heights[WindTerrain.Index(1, i)] = rise * along;
            }
            return heights;
        }


        private static float[] Valley(int axis, float wallHeight)
        {
            float[] heights = new float[WindTerrain.SampleCount];

            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                double angle = (i - axis) * (2d * Math.PI / WindTerrain.Bearings);

                float across = -(float)Math.Cos(2d * angle);
                float height = wallHeight * 0.5f * (1f + across);

                heights[WindTerrain.Index(0, i)] = height * 0.5f;
                heights[WindTerrain.Index(1, i)] = height;
            }
            return heights;
        }


        private static float[] Wall(int bearing, float height)
        {
            float[] heights = new float[WindTerrain.SampleCount];
            heights[WindTerrain.Index(0, bearing)] = height;
            heights[WindTerrain.Index(1, bearing)] = height;
            return heights;
        }


        [Fact]

        public void FlatGroundChangesNothing()
        {
            Assert.Equal(0f, WindTerrain.Relief(Flat(), Outer), 5);
            Assert.Equal(1f, WindTerrain.SpeedUp(WindTerrain.Relief(Flat(), Outer)), 5);
        }

        [Fact]

        public void ASummitIsWindierThanThePlainBelowIt()
        {
            float relief = WindTerrain.Relief(Hill(100f), Outer);
            Assert.True(relief > 0f);

            float factor = WindTerrain.SpeedUp(relief);
            Assert.True(factor > 1.4f, "a hundred metres of relief in three hundred should be a real gain");
        }

        [Fact]

        public void AHollowIsSheltered()
        {
            Assert.True(WindTerrain.SpeedUp(WindTerrain.Relief(Hollow(100f), Outer)) < 0.8f);
        }

        [Fact]

        public void AHillsideIsNeither()
        {
            Assert.Equal(0f, WindTerrain.Relief(Slope(0, 200f), Outer), 4);
        }

        [Fact]

        public void NeitherSpeedUpNorSlowDownRunsAway()
        {
            Assert.Equal(1f + WindTerrain.MaximumSpeedUp, WindTerrain.SpeedUp(10f), 4);
            Assert.Equal(1f - WindTerrain.MaximumSlowDown, WindTerrain.SpeedUp(-10f), 4);
            Assert.True(WindTerrain.SpeedUp(-10f) > 0f, "sheltering must never reverse the wind");
        }


        [Fact]

        public void AWallUpwindTakesTheWindAway()
        {

            float[] terrain = Wall(0, 120f);
            Vector3 fromTheNorth = -North;

            float sheltered = WindTerrain.Shelter(terrain, Inner, Outer, fromTheNorth, North, East);
            Assert.True(sheltered < 0.6f, "a wall in the way should cost most of the wind");
        }

        [Fact]

        public void TheSameWallDoesNothingToAWindComingTheOtherWay()
        {

            float[] terrain = Wall(0, 120f);

            Assert.Equal(1f, WindTerrain.Shelter(terrain, Inner, Outer, North, North, East), 4);
        }

        [Fact]

        public void ShelterTurnsWithTheWind()
        {

            float[] terrain = Wall(2, 120f);

            float worst = 2f;
            int worstBearing = -1;

            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                Vector3 from = WindTerrain.BearingDirection(i, North, East);
                float factor = WindTerrain.Shelter(terrain, Inner, Outer, -from, North, East);

                if (factor < worst)
                {
                    worst = factor;
                    worstBearing = i;
                }
            }

            Assert.Equal(2, worstBearing);
        }

        [Fact]

        public void ANearObstructionSheltersMoreThanAFarOneOfTheSameHeight()
        {
            float[] near = new float[WindTerrain.SampleCount];
            near[WindTerrain.Index(0, 0)] = 60f;

            float[] far = new float[WindTerrain.SampleCount];
            far[WindTerrain.Index(1, 0)] = 60f;

            Vector3 fromTheNorth = -North;

            Assert.True(
                WindTerrain.Shelter(near, Inner, Outer, fromTheNorth, North, East)
                < WindTerrain.Shelter(far, Inner, Outer, fromTheNorth, North, East));
        }

        [Fact]

        public void SaturationKeepsTheGradientAndTheBound()
        {
            const float Bound = 0.6f;

            foreach (float value in new[] { 0.001f, 0.01f, 0.05f })
            {
                float saturated = WindTerrain.Saturate(value, Bound);
                Assert.True(Math.Abs(saturated - value) < value * 0.02f,
                    "gentle ground moved by more than a per cent: " + value + " to " + saturated);
            }

            foreach (float value in new[] { 1f, 10f, 1000f })
            {
                float saturated = WindTerrain.Saturate(value, Bound);

                Assert.True(saturated <= Bound + 1e-6f, "the bound was crossed at " + value);
                Assert.True(saturated > Bound * 0.9f, "the bound was not approached at " + value);
            }

            Assert.True(WindTerrain.Saturate(1f, Bound) < Bound,
                "a relief of one should still be short of the bound");

            Assert.Equal(0f, WindTerrain.Saturate(0f, Bound), 6);
            Assert.Equal(0f, WindTerrain.Saturate(1f, 0f), 6);
        }

        [Fact]

        public void ASteeperRidgeIsStillWindierThanASteepOne()
        {
            float previous = 0f;

            for (float relief = 0.1f; relief <= 2f; relief += 0.1f)
            {
                float speedUp = WindTerrain.SpeedUp(relief);

                Assert.True(speedUp > previous,
                    "speed-up stopped rising at a relief of " + relief.ToString("n1"));
                Assert.True(speedUp <= 1f + WindTerrain.MaximumSpeedUp,
                    "speed-up passed its bound at " + relief.ToString("n1") + ": " + speedUp);

                previous = speedUp;
            }

            previous = float.MaxValue;
            for (float relief = -0.1f; relief >= -2f; relief -= 0.1f)
            {
                float speedUp = WindTerrain.SpeedUp(relief);

                Assert.True(speedUp < previous,
                    "a deeper hollow stopped being calmer at " + relief.ToString("n1"));
                Assert.True(speedUp >= 1f - WindTerrain.MaximumSlowDown,
                    "a hollow passed its bound at " + relief.ToString("n1") + ": " + speedUp);

                previous = speedUp;
            }
        }

        [Fact]

        public void OpenGroundShelltersNothingAndTheFloorHolds()
        {
            Assert.Equal(1f, WindTerrain.Shelter(Flat(), Inner, Outer, North, North, East), 4);


            float[] cliff = Wall(0, 100000f);
            float least = WindTerrain.Shelter(cliff, Inner, Outer, -North, North, East);

            float steepest = (float)System.Math.Tanh(90d / WindTerrain.FullShelterDegrees);
            float reachable = 1f - (WindTerrain.MaximumShelter * steepest);

            Assert.Equal(reachable, least, 0.001f);
            Assert.True(least > 1f - WindTerrain.MaximumShelter,
                "the floor is a bound, not a value the terrain can sit on: " + least);
            Assert.True(least > 0f, "even a wind shadow leaves something behind");
        }

        [Fact]

        public void GroundThatFallsAwayUpwindDoesNotShelter()
        {
            Assert.Equal(1f, WindTerrain.Shelter(Hill(100f), Inner, Outer, -North, North, East), 4);
        }


        [Fact]

        public void AValleySteersTheWindAlongItself()
        {

            float[] terrain = Valley(0, 150f);

            Vector3 channelled = WindTerrain.Channel(terrain, Outer, East, North, East, 1f);

            float alongValley = Math.Abs(Vector3.Dot(channelled, North));
            Assert.True(alongValley > 0.7f,
                "wind across a walled valley should be turned along it, got " + alongValley);
        }

        [Fact]

        public void ItPicksTheEndTheWindWasAlreadyHeadingFor()
        {

            float[] terrain = Valley(0, 150f);

            Vector3 leaning = Vector3.Normalize(East + (North * 0.3f));
            Vector3 opposite = Vector3.Normalize(East - (North * 0.3f));

            float a = Vector3.Dot(WindTerrain.Channel(terrain, Outer, leaning, North, East, 1f), North);
            float b = Vector3.Dot(WindTerrain.Channel(terrain, Outer, opposite, North, East, 1f), North);

            Assert.True(a > 0f && b < 0f, "the two should leave by opposite ends: " + a + ", " + b);
        }

        [Fact]

        public void FlatGroundSteersNothing()
        {
            Vector3 channelled = WindTerrain.Channel(Flat(), Outer, East, North, East, 1f);
            Assert.Equal(East.X, channelled.X, 4);
            Assert.Equal(East.Z, channelled.Z, 4);
        }

        [Fact]

        public void AShallowValleySteersLessThanADeepOne()
        {
            Vector3 shallow = WindTerrain.Channel(Valley(0, 20f), Outer, East, North, East, 1f);
            Vector3 deep = WindTerrain.Channel(Valley(0, 150f), Outer, East, North, East, 1f);

            Assert.True(Math.Abs(Vector3.Dot(deep, North)) > Math.Abs(Vector3.Dot(shallow, North)));
        }

        [Fact]

        public void TurningItDownTurnsItDown()
        {

            float[] terrain = Valley(0, 150f);

            Vector3 full = WindTerrain.Channel(terrain, Outer, East, North, East, 1f);
            Vector3 half = WindTerrain.Channel(terrain, Outer, East, North, East, 0.4f);
            Vector3 none = WindTerrain.Channel(terrain, Outer, East, North, East, 0f);

            Assert.True(Math.Abs(Vector3.Dot(full, North)) > Math.Abs(Vector3.Dot(half, North)));
            Assert.Equal(East.X, none.X, 4);
        }

        [Fact]

        public void AChannelledWindIsStillAUnitVector()
        {
            for (int axis = 0; axis < WindTerrain.Bearings; axis++)
            {
                Vector3 channelled = WindTerrain.Channel(
                    Valley(axis, 150f), Outer, East, North, East, 1f);

                Assert.Equal(1f, channelled.Length(), 4);
            }
        }

        [Fact]

        public void AWindWithNoDirectionStaysThatWay()
        {
            Assert.Equal(Vector3.Zero, WindTerrain.Channel(Valley(0, 150f), Outer, Vector3.Zero, North, East, 1f));
        }

        [Fact]

        public void AWindOnARidgeCrestIsSteeredOverItRatherThanAlongIt()
        {

            float[] valley = Valley(0, 150f);

            float[] ridge = new float[WindTerrain.SampleCount];
            for (int i = 0; i < ridge.Length; i++) ridge[i] = -valley[i];

            Vector3 inValley = WindTerrain.Channel(valley, Outer, East, North, East, 1f);
            Vector3 onCrest = WindTerrain.Channel(ridge, Outer, East, North, East, 1f);

            Assert.True(Math.Abs(Vector3.Dot(inValley, North)) > 0.7f,
                "the valley should turn the wind along itself");
            Assert.True(Math.Abs(Vector3.Dot(onCrest, North)) < 0.2f,
                "the crest should leave it crossing, not turn it along the ridge");
        }

        [Fact]

        public void TheRuleIsThatWindTurnsTowardTheLowestGround()
        {
            for (int axis = 0; axis < WindTerrain.Bearings; axis++)
            {

                float[] terrain = Valley(axis, 150f);

                Vector3 low = WindTerrain.BearingDirection(axis, North, East);

                Vector3 across = WindTerrain.BearingDirection(axis + 2, North, East);
                Vector3 wind = Vector3.Normalize(across + (low * 0.2f));

                Vector3 channelled = WindTerrain.Channel(terrain, Outer, wind, North, East, 1f);

                Assert.True(Vector3.Dot(channelled, low) > Vector3.Dot(wind, low),
                    "axis " + axis + " should have leaned further toward its open end");
            }
        }


        [Fact]

        public void BearingsRunClockwiseFromNorth()
        {
            Assert.Equal(North.Z, WindTerrain.BearingDirection(0, North, East).Z, 4);
            Assert.Equal(East.X, WindTerrain.BearingDirection(2, North, East).X, 4);
            Assert.Equal(-North.Z, WindTerrain.BearingDirection(4, North, East).Z, 4);
            Assert.Equal(-East.X, WindTerrain.BearingDirection(6, North, East).X, 4);
        }

        [Fact]

        public void EverySampleHasItsOwnSlot()
        {
            bool[] seen = new bool[WindTerrain.SampleCount];

            for (int radius = 0; radius < WindTerrain.Radii; radius++)
            {
                for (int bearing = 0; bearing < WindTerrain.Bearings; bearing++)
                {
                    int index = WindTerrain.Index(radius, bearing);
                    Assert.False(seen[index], "slot " + index + " was claimed twice");
                    seen[index] = true;
                }
            }
        }

        [Fact]

        public void AShortSampleIsRefusedRatherThanRead()
        {
            float[] stunted = new float[WindTerrain.SampleCount - 1];

            Assert.Equal(0f, WindTerrain.Relief(stunted, Outer), 5);
            Assert.Equal(1f, WindTerrain.Shelter(stunted, Inner, Outer, North, North, East), 5);
            Assert.Equal(1f, WindTerrain.Channel(stunted, Outer, East, North, East, 1f).Length(), 4);

            Assert.Equal(0f, WindTerrain.Relief(null, Outer), 5);
            Assert.Equal(1f, WindTerrain.Shelter(null, Inner, Outer, North, North, East), 5);
        }
    }
}

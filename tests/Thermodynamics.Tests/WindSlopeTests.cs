using System;
using System.Diagnostics;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Slope winds: air running up a mountain by day and draining back down it at night.
    ///
    /// <para>Anabatic and katabatic flow, and they are a <b>different mechanism</b> from everything
    /// else terrain does in this model. Speed-up, sheltering and channelling are mechanical — what
    /// the ground does to a wind that was already blowing. This one the ground <i>makes</i>, out of
    /// sunlight and gravity, and it blows on a day when nothing else does.</para>
    ///
    /// <para>The figures come from the meteorological literature: anabatic 3–5 m/s and hundreds of
    /// metres deep; katabatic 3–8 m/s and only 10–100 m deep; both forming under calm, clear
    /// conditions and both overrun by a real synoptic wind.</para>
    /// </summary>
    [Collection("alone")]
    public class WindSlopeTests
    {

        private static readonly Vector3 North = new Vector3(0f, 0f, -1f);

        private static readonly Vector3 East = new Vector3(1f, 0f, 0f);


        private static readonly Vector3 Downhill = new Vector3(0f, 0f, 1f);

        private const float Steep = 0.25f;


        private static Vector3 At(float heating, float height, float ambient = 0f, float slope = Steep)
        {
            return WindSlope.Velocity(Downhill, slope, heating, height, ambient, 1f);
        }

        [Fact]

        public void ByDayTheAirRunsUpTheMountain()
        {

            Vector3 wind = At(1f, 2f);

            Assert.True(wind.Length() > 1f, "there should be a real upslope flow at midday");
            Assert.True(Vector3.Dot(Vector3.Normalize(wind), -Downhill) > 0.99f,
                "and it should point uphill");
        }

        [Fact]

        public void AtNightItDrainsBackDownToTheBase()
        {

            Vector3 wind = At(0f, 2f);

            Assert.True(wind.Length() > 1f, "there should be a real drainage flow at night");
            Assert.True(Vector3.Dot(Vector3.Normalize(wind), Downhill) > 0.99f,
                "and it should point downhill");
        }

        [Fact]

        public void TheDrainageFlowIsTheStrongerOfTheTwo()
        {
            Assert.True(At(0f, 2f).Length() > At(1f, 2f).Length());
        }

        [Fact]

        public void BothSitInsideTheSpeedsTheLiteratureReports()
        {
            Assert.InRange(At(1f, 2f).Length(), 3f, 5f);
            Assert.InRange(At(0f, 2f).Length(), 3f, 8f);
        }

        [Fact]

        public void TheFlowTurnsOverAtTheMiddleOfTheDay()
        {
            Assert.Equal(0f, At(0.5f, 2f).Length(), 4);

            Assert.True(Vector3.Dot(At(0.55f, 2f), -Downhill) > 0f);
            Assert.True(Vector3.Dot(At(0.45f, 2f), Downhill) > 0f);
        }

        [Fact]

        public void TheNightFlowIsMuchShallowerThanTheDayFlow()
        {
            Assert.Equal(0f, At(0f, 200f).Length(), 4);
            Assert.True(At(1f, 200f).Length() > 0f);

            Assert.Equal(0f, At(1f, WindSlope.AnabaticDepth + 1f).Length(), 4);
            Assert.Equal(0f, At(0f, WindSlope.KatabaticDepth + 1f).Length(), 4);
        }

        [Fact]

        public void ItFadesWithHeightRatherThanStopping()
        {

            float low = At(0f, 5f).Length();

            float mid = At(0f, 40f).Length();

            float high = At(0f, 75f).Length();

            Assert.True(low > mid && mid > high && high > 0f);
        }

        [Fact]

        public void ARealWindOverrunsIt()
        {

            float calm = At(0f, 2f, 0f).Length();

            float breezy = At(0f, 2f, 5f).Length();

            float gale = At(0f, 2f, 40f).Length();

            Assert.True(breezy < calm * 0.6f, "a 5 m/s wind should already halve it");
            Assert.True(gale < calm * 0.15f, "a gale should all but erase it");
            Assert.True(gale > 0f, "but smoothly, not as a cliff");
        }

        [Fact]

        public void FlatGroundMakesNoSlopeWind()
        {
            Assert.Equal(Vector3.Zero, At(0f, 2f, 0f, 0f));
            Assert.Equal(Vector3.Zero, At(1f, 2f, 0f, 0f));
            Assert.Equal(Vector3.Zero,
                WindSlope.Velocity(Vector3.Zero, Steep, 0f, 2f, 0f, 1f));
        }

        [Fact]

        public void AGentleSlopeMakesAGentlerWind()
        {
            Assert.True(At(0f, 2f, 0f, 0.05f).Length() < At(0f, 2f, 0f, 0.25f).Length());
        }

        [Fact]

        public void ASteeperSlopeThanTheModelKnowsDoesNotRunAway()
        {
            Assert.Equal(At(0f, 2f, 0f, WindSlope.FullSlope).Length(),
                At(0f, 2f, 0f, 10f).Length(), 3);
        }

        [Fact]

        public void TurningItOffTurnsItOff()
        {
            Assert.Equal(Vector3.Zero, WindSlope.Velocity(Downhill, Steep, 0f, 2f, 0f, 0f));
        }



        private static float[] Hillside(int towards, float rise)
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

        [Fact]

        public void TheFallLinePointsAwayFromTheHighGround()
        {
            float slope;
            Vector3 downhill = WindTerrain.Downhill(Hillside(0, 100f), 300f, North, East, out slope);

            Assert.True(slope > 0f);
            Assert.True(Vector3.Dot(downhill, -North) > 0.98f,
                "downhill should be due south, got " + downhill);
        }

        [Fact]

        public void TheFallLineTurnsWithTheHillside()
        {
            for (int towards = 0; towards < WindTerrain.Bearings; towards++)
            {
                float slope;
                Vector3 downhill = WindTerrain.Downhill(
                    Hillside(towards, 100f), 300f, North, East, out slope);

                Vector3 uphill = WindTerrain.BearingDirection(towards, North, East);
                Assert.True(Vector3.Dot(downhill, -uphill) > 0.9f,
                    "bearing " + towards + " gave " + downhill);
            }
        }

        [Fact]

        public void AValleyFloorHasNoFallLineAlthoughItHasAnAxis()
        {
            float[] valley = new float[WindTerrain.SampleCount];
            for (int i = 0; i < WindTerrain.Bearings; i++)
            {
                double angle = i * (2d * Math.PI / WindTerrain.Bearings);
                float across = -(float)Math.Cos(2d * angle);
                float height = 150f * 0.5f * (1f + across);

                valley[WindTerrain.Index(0, i)] = height * 0.5f;
                valley[WindTerrain.Index(1, i)] = height;
            }

            float slope;
            WindTerrain.Downhill(valley, 300f, North, East, out slope);

            Assert.True(slope < 0.02f, "a symmetric valley should have almost no fall line: " + slope);

            Vector3 channelled = WindTerrain.Channel(valley, 300f, East, North, East, 1f);
            Assert.True(Math.Abs(Vector3.Dot(channelled, North)) > 0.7f);
        }

        [Fact]

        public void FlatGroundHasNoFallLineEither()
        {
            float slope;
            Vector3 downhill = WindTerrain.Downhill(
                new float[WindTerrain.SampleCount], 300f, North, East, out slope);

            Assert.Equal(0f, slope, 5);
            Assert.Equal(Vector3.Zero, downhill);
        }



        private static WindSolver.Inputs Base(float[] terrain)
        {
            WindSolver.Inputs inputs = new WindSolver.Inputs();
            inputs.Ceiling = 74f;

            inputs.Up = new Vector3(0f, 1f, 0f);

            inputs.Axis = new Vector3(0f, 1f, 0f);
            inputs.WeatherIntensity = 0f;
            inputs.WeatherWind = 1f;
            inputs.Variation = 0.5f;
            inputs.HeightAboveGround = 2f;
            inputs.Roughness = 0.03f;
            inputs.GradientHeight = 600f;
            inputs.DiurnalAmplitude = 0.35f;
            inputs.DiurnalCrossover = 80f;
            inputs.TerrainInfluence = 1f;
            inputs.TerrainRadius = 300f;
            inputs.SlopeStrength = 1f;
            inputs.Terrain = terrain;
            return inputs;
        }

        [Fact]

        public void TheSolverReportsWhichWayTheSlopeWindIsRunning()
        {

            WindSolver.Inputs inputs = Base(Hillside(0, 150f));
            inputs.Up = Vector3.Normalize(new Vector3(1f, 0.3f, 0f));

            inputs.Heating = 1f;
            WindSolver.Result day = WindSolver.Solve(ref inputs);

            inputs.Heating = 0f;
            WindSolver.Result night = WindSolver.Solve(ref inputs);

            Assert.Equal(1, day.SlopeSense);
            Assert.Equal(-1, night.SlopeSense);
            Assert.True(day.SlopeSpeed > 0f && night.SlopeSpeed > 0f);
            Assert.True(day.Gradient > 0f);
        }

        [Fact]

        public void SwitchingSlopeWindOffChangesNothingElse()
        {

            WindSolver.Inputs on = Base(Hillside(0, 150f));
            on.Up = Vector3.Normalize(new Vector3(1f, 0.3f, 0f));
            on.Heating = 0f;

            WindSolver.Inputs off = on;
            off.SlopeStrength = 0f;

            WindSolver.Result a = WindSolver.Solve(ref on);
            WindSolver.Result b = WindSolver.Solve(ref off);

            Assert.Equal(a.BandShare, b.BandShare, 5);
            Assert.Equal(a.Profile, b.Profile, 5);
            Assert.Equal(a.SpeedUp, b.SpeedUp, 5);
            Assert.Equal(a.Shelter, b.Shelter, 5);

            Assert.Equal(0f, b.SlopeSpeed, 5);
            Assert.True(a.SlopeSpeed > 0f);
            Assert.NotEqual(a.Speed, b.Speed);
        }

        [Fact]

        public void OnFlatGroundTheSolverBehavesExactlyAsItDidBefore()
        {

            WindSolver.Inputs inputs = Base(new float[WindTerrain.SampleCount]);
            inputs.Up = Vector3.Normalize(new Vector3(1f, 0.3f, 0f));
            inputs.Heating = 0f;

            WindSolver.Result r = WindSolver.Solve(ref inputs);

            Assert.Equal(0f, r.SlopeSpeed, 5);
            Assert.Equal(0, r.SlopeSense);
            Assert.Equal(0f, r.Gradient, 5);
        }


        [Fact]

        public void SlopeWindCostsAlmostNothingBecauseTheTerrainIsAlreadyRead()
        {

            float[] terrain = Hillside(0, 150f);


            WindSolver.Inputs on = Base(terrain);
            on.Up = Vector3.Normalize(new Vector3(1f, 0.3f, 0f));
            on.Heating = 0f;

            WindSolver.Inputs off = on;
            off.SlopeStrength = 0f;

            const int Warm = 20000;
            const int Runs = 400000;

            for (int i = 0; i < Warm; i++) { WindSolver.Solve(ref on); WindSolver.Solve(ref off); }

            double without = double.MaxValue, with = double.MaxValue;

            Stopwatch clock = new Stopwatch();

            for (int pass = 0; pass < 3; pass++)
            {
                clock.Restart();
                for (int i = 0; i < Runs; i++) WindSolver.Solve(ref off);
                without = Math.Min(without, clock.Elapsed.TotalMilliseconds);

                clock.Restart();
                for (int i = 0; i < Runs; i++) WindSolver.Solve(ref on);
                with = Math.Min(with, clock.Elapsed.TotalMilliseconds);
            }

            Assert.True(with < without * 1.6d,
                "slope wind should be a small fraction of a solve, not a doubling: "
                + without.ToString("n1") + " ms without, " + with.ToString("n1") + " ms with");
        }
    }
}

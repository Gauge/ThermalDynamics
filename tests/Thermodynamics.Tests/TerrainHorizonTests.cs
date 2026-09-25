using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Shadow cast by the ground itself.
    ///
    /// The planet test treats a world as a smooth ball, which is right for night and for orbit and
    /// blind to the mountain next door. These build terrain out of arithmetic — a plain, a ridge, a
    /// canyon — and check the walk against ground that was never rendered.
    /// </summary>
    public class TerrainHorizonTests
    {
        private const double Radius = 60000d;
        private static readonly Vector3D Centre = Vector3D.Zero;


        private static Vector3D Surface(double height = 2d)
        {
            return new Vector3D(0, Radius + height, 0);
        }


        private static Func<Vector3D, double> Plain()
        {
            return point => Radius;
        }


        private static Func<Vector3D, double> Ridge(double from, double height)
        {
            return point => point.X >= from ? Radius + height : Radius;
        }


        private static bool Occluded(Vector3D origin, Vector3D sun, Func<Vector3D, double> terrain,
            double range = 4000d, int samples = 10)
        {
            return TerrainHorizon.Occluded(origin, sun, Centre, range, samples, terrain);
        }


        private static Vector3D Sun(double degrees)
        {
            double radians = degrees * Math.PI / 180d;
            return new Vector3D(Math.Cos(radians), Math.Sin(radians), 0);
        }

        [Fact]

        public void FlatGroundDoesNotShadowItself()
        {
            for (int degrees = 1; degrees <= 90; degrees += 7)
            {
                Assert.False(Occluded(Surface(), Sun(degrees), Plain()),
                    "flat ground shadowed itself at " + degrees + " degrees");
            }
        }

        [Fact]

        public void ARidgeShadowsALowSun()
        {
            Assert.True(Occluded(Surface(), Sun(5), Ridge(200d, 100d)));
            Assert.True(Occluded(Surface(), Sun(15), Ridge(200d, 100d)));
        }

        [Fact]

        public void TheSameRidgeDoesNotShadowAHighSun()
        {
            Assert.False(Occluded(Surface(), Sun(60), Ridge(200d, 100d)));
            Assert.False(Occluded(Surface(), Sun(85), Ridge(200d, 100d)));
        }

        [Fact]

        public void StandingOnTopOfTheRidgeIsNeverShadowedByIt()
        {
            Assert.False(Occluded(Surface(120d), Sun(5), Ridge(200d, 100d)));
        }

        [Fact]

        public void ATallerRidgeShadowsAHigherSun()
        {
            Assert.False(Occluded(Surface(), Sun(40), Ridge(200d, 100d)));
            Assert.True(Occluded(Surface(), Sun(40), Ridge(200d, 400d)));
        }

        [Fact]

        public void GroundBeyondTheRangeIsNotConsulted()
        {

            Func<Vector3D, double> distant = Ridge(8000d, 3000d);

            Assert.False(Occluded(Surface(), Sun(10), distant, 4000d));
            Assert.True(Occluded(Surface(), Sun(10), distant, 20000d));
        }

        [Fact]

        public void ACanyonWallShadowsTheFloorAndNotTheRim()
        {

            Func<Vector3D, double> canyon = Ridge(30d, 60d);

            Assert.True(Occluded(Surface(), Sun(30), canyon));
            Assert.False(Occluded(Surface(70d), Sun(30), canyon));
        }

        [Fact]

        public void NoTerrainFunctionMeansNoOpinion()
        {
            Assert.False(TerrainHorizon.Occluded(Surface(), Sun(10), Centre, 4000d, 10, null));
        }

        [Fact]

        public void ADegenerateWalkIsRefusedRatherThanGuessed()
        {

            Func<Vector3D, double> wall = Ridge(10d, 500d);

            Assert.False(Occluded(Surface(), Sun(10), wall, 0d));
            Assert.False(Occluded(Surface(), Sun(10), wall, 4000d, 0));
            Assert.False(Occluded(Surface(), Vector3D.Zero, wall));
        }

        [Fact]

        public void SamplesAreSpacedGeometricallySoNearGroundIsWatchedClosest()
        {
            int consulted = 0;
            Func<Vector3D, double> counting = point =>
            {
                consulted++;
                return point.X >= 60d && point.X <= 90d ? Radius + 40d : Radius;
            };

            Assert.True(Occluded(Surface(), Sun(8), counting));
            Assert.True(consulted <= 10, "should not have cost more lookups than it was allowed");
        }
    }
}

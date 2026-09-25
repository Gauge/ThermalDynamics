using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Occlusion by everything that is not the grid itself: a planet's night side, an asteroid, a
    /// station overhead.
    ///
    /// The raycasts belong to the game and cannot run here. What can, and what these cover, is the
    /// part that decides what the raycasts mean: the share of a ship that ends up in shadow, and
    /// where the rays are cast from.
    /// </summary>
    public class SolarOcclusionTests
    {

        private static ThermalSettings Solar()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableSolarHeat = true;
            settings.SolarEnergy = 1000f;
            return settings;
        }

        [Fact]

        public void HalfOccludedIsHalfTheSunlight()
        {
            EnvironmentSample sample = Worlds.Space(new Vector3(1f, 0f, 0f));
            sample.SolarOcclusion = 0.5f;

            EnvironmentState state = EnvironmentSolver.Solve(Solar(), null, sample);

            Assert.Equal(500f, state.SolarEnergy, 3);

            Assert.False(state.IsSolarOccluded);
        }

        [Fact]

        public void FullyOccludedSetsTheFlagAndTakesAllOfIt()
        {
            EnvironmentSample sample = Worlds.Space(new Vector3(1f, 0f, 0f));
            sample.SolarOcclusion = 1f;

            EnvironmentState state = EnvironmentSolver.Solve(Solar(), null, sample);

            Assert.Equal(0f, state.SolarEnergy, 5);
            Assert.True(state.IsSolarOccluded);
        }

        [Fact]

        public void TheFlagAloneStillMeansNoSun()
        {
            EnvironmentSample sample = Worlds.Space(new Vector3(1f, 0f, 0f));
            sample.IsSolarOccluded = true;

            EnvironmentState state = EnvironmentSolver.Solve(Solar(), null, sample);

            Assert.Equal(1f, state.SolarOcclusion, 5);
            Assert.Equal(0f, state.SolarEnergy, 5);
        }

        [Fact]

        public void PartialOcclusionCompoundsWithTheAtmosphere()
        {
            PlanetThermalProperties planet = PlanetThermalProperties.Default();

            EnvironmentSample clear = Worlds.PlanetSurface(1f, 0.5f);
            EnvironmentSample half = Worlds.PlanetSurface(1f, 0.5f);
            half.SolarOcclusion = 0.5f;

            float open = EnvironmentSolver.Solve(Solar(), planet, clear).SolarEnergy;
            float shaded = EnvironmentSolver.Solve(Solar(), planet, half).SolarEnergy;

            Assert.True(open > 0f);
            Assert.Equal(open * 0.5f, shaded, 3);
        }

        [Fact]

        public void GoingUndergroundIsAlwaysFullShadow()
        {
            EnvironmentSample sample = Worlds.Underground();
            sample.SolarOcclusion = 0f;

            EnvironmentState state = EnvironmentSolver.Solve(Solar(), PlanetThermalProperties.Default(), sample);

            Assert.Equal(1f, state.SolarOcclusion, 5);
            Assert.Equal(0f, state.SolarEnergy, 5);
        }

        [Fact]

        public void SolarHeatOffReadsAsFullyOccludedWhateverTheSampleSays()
        {

            ThermalSettings settings = Solar();
            settings.EnableSolarHeat = false;

            EnvironmentSample sample = Worlds.Space(new Vector3(1f, 0f, 0f));
            sample.SolarOcclusion = 0f;

            EnvironmentState state = EnvironmentSolver.Solve(settings, null, sample);

            Assert.Equal(0f, state.SolarEnergy, 5);
            Assert.True(state.IsSolarOccluded);
        }

        [Fact]

        public void AnOutOfRangeFractionIsClampedRatherThanTrusted()
        {
            EnvironmentSample sample = Worlds.Space(new Vector3(1f, 0f, 0f));
            sample.SolarOcclusion = 4f;

            Assert.Equal(0f, EnvironmentSolver.Solve(Solar(), null, sample).SolarEnergy, 5);

            sample.SolarOcclusion = -2f;
            Assert.Equal(1000f, EnvironmentSolver.Solve(Solar(), null, sample).SolarEnergy, 3);
        }
    }

    /// <summary>
    /// Where to cast from when asking whether the sun reaches a grid.
    ///
    /// <para>
    /// One ray from the centre makes a large ship flip from fully lit to fully dark the instant its
    /// centre crosses a terminator. These pin the properties that turn that step into a ramp: the
    /// centre comes first so one sample behaves as it always did, opposite corners are paired so any
    /// even count straddles the hull, and every point is inside the hull rather than beside it.
    /// </para>
    /// </summary>
    public class SolarOcclusionSamplerTests
    {
        private static readonly BoundingBoxD Ship =

            new BoundingBoxD(new Vector3D(-50, -10, -20), new Vector3D(50, 10, 20));


        private static List<Vector3D> Points(int samples)
        {

            List<Vector3D> results = new List<Vector3D>();
            SolarOcclusionSampler.Points(Ship, samples, results);
            return results;
        }

        [Fact]

        public void OneSampleIsTheCentreAndNothingElse()
        {

            List<Vector3D> points = Points(1);

            Assert.Single(points);
            Assert.Equal(Ship.Center, points[0]);
        }

        [Fact]

        public void MorePointsStraddleTheShipRatherThanCrowdingOneEnd()
        {

            List<Vector3D> points = Points(3);

            Assert.Equal(3, points.Count);

            Vector3D first = points[1] - Ship.Center;
            Vector3D second = points[2] - Ship.Center;

            Assert.Equal(-first, second);
        }

        [Fact]

        public void EverySamplePointIsInsideTheHullRatherThanBesideIt()
        {

            List<Vector3D> points = Points(SolarOcclusionSampler.MaxSamples);

            Assert.Equal(SolarOcclusionSampler.MaxSamples, points.Count);

            foreach (Vector3D point in points)
            {
                Assert.True(Ship.Contains(point) != ContainmentType.Disjoint);

                Vector3D offset = point - Ship.Center;
                Assert.True(System.Math.Abs(offset.X) <= Ship.HalfExtents.X * 0.75);
            }
        }

        [Fact]

        public void AskingForMoreThanExistOrFewerThanOneIsClamped()
        {
            Assert.Equal(SolarOcclusionSampler.MaxSamples, Points(50).Count);
            Assert.Single(Points(0));
            Assert.Single(Points(-3));
        }

        [Fact]

        public void APointGridDegeneratesToItsCentre()
        {

            List<Vector3D> results = new List<Vector3D>();
            SolarOcclusionSampler.Points(new BoundingBoxD(Vector3D.Zero, Vector3D.Zero), 9, results);

            foreach (Vector3D point in results)
            {
                Assert.Equal(Vector3D.Zero, point);
            }
        }
    }
}

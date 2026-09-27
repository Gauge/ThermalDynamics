using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Centre changes prepare bounded face work and publish atomically. Completed forces and damping
    /// are bit-identical to the legacy surface pass; interrupted preparation must leave live forces intact.
    /// </summary>
    public class AeroCentreUpdateTests
    {
        [Theory]
        [InlineData(26, 1)]
        [InlineData(26, 7)]
        [InlineData(26, 100000)]
        [InlineData(98, 1)]
        [InlineData(98, 7)]
        [InlineData(98, 100000)]
        public void AShiftPublishesOnlyAfterEveryFaceAndMatchesLegacy(int directions, int budget)
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroProfile profile = AeroProfile.Build(grid, directions);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, directions);
            Vector3 original = profile.CentreOfMass;
            Vector3 target = original + new Vector3(0.7f, -1.1f, 0.3f);
            Vector3[] initial = Read(profile);
            int work = 0;
            bool ready;
            do
            {
                ready = profile.PrepareCentreOfMass(target, budget, out int used);
                Assert.InRange(used, 1, budget);
                work += used;
                Assert.InRange(work, 1, 54);
                if (!ready)
                {
                    Assert.Equal(original, profile.CentreOfMass);
                    Same(initial, Read(profile));
                }
            } while (!ready);
            Assert.Equal(54, work);
            Assert.Equal(target, profile.CentreOfMass);
            legacy.SetCentreOfMass(target);
            Same(Read(legacy), Read(profile));
            Assert.True(Read(profile)[2].LengthSquared() > 0f);
            Assert.True(profile.PrepareCentreOfMass(target, 0, out int repeated));
            Assert.Equal(0, repeated);
        }

        [Fact]
        public void ReplacingOrCancellingAPartialTargetCannotPublishMixedMoments()
        {
            GridModel grid = AeroFlightLab.Shuttlecock();
            AeroProfile profile = AeroProfile.Build(grid, 98);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, 98);
            Vector3 a = profile.CentreOfMass + new Vector3(2f, -3f, 1f);
            Vector3 b = a + Vector3.One;
            Assert.False(profile.PrepareCentreOfMass(a, 5, out _));
            Assert.False(profile.PrepareCentreOfMass(b, 5, out _));
            Assert.True(profile.PrepareCentreOfMass(b, int.MaxValue, out int remaining));
            Assert.True(remaining > 0);
            legacy.SetCentreOfMass(b);
            Same(Read(legacy), Read(profile));
            Assert.False(profile.PrepareCentreOfMass(a, 5, out _));
            profile.SetCentreOfMass(b + Vector3.One);
            legacy.SetCentreOfMass(b + Vector3.One);
            Same(Read(legacy), Read(profile));
            Assert.True(profile.PrepareCentreOfMass(a, int.MaxValue, out _));
            legacy.SetCentreOfMass(a);
            Same(Read(legacy), Read(profile));
        }

        [Fact]
        public void TinyMovesKeepTheLegacyCumulativeCacheThreshold()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroProfile profile = AeroProfile.Build(grid, 26);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, 26);
            Vector3 original = profile.CentreOfMass;
            Same(Read(legacy), Read(profile));
            for (int i = 1; i <= 3; i++)
            {
                Vector3 target = original + new Vector3(0.0004f * i, 0f, 0f);
                Assert.True(profile.PrepareCentreOfMass(target, 54, out int used));
                Assert.Equal(i < 3 ? 0 : 54, used);
                legacy.SetCentreOfMass(target);
                Same(Read(legacy), Read(profile));
            }
        }

        [Fact]
        public void TheFirstTinyShiftPreparesItsOwnCentreAndEmptyProfilesNeedNoWork()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroProfile profile = AeroProfile.Build(grid, 26);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, 26);
            Vector3 target = profile.CentreOfMass + new Vector3(0.0004f, 0f, 0f);
            Assert.True(profile.PrepareCentreOfMass(target, 54, out int used));
            Assert.Equal(54, used);
            legacy.SetCentreOfMass(target);
            Same(Read(legacy), Read(profile));
            AeroProfile empty = AeroProfile.Build(null, 26);
            Assert.True(empty.PrepareCentreOfMass(target, 0, out used));
            Assert.Equal(0, used);
            Assert.Equal(target, empty.CentreOfMass);
        }

        [Fact]
        public void ZeroBudgetAndInvalidTargetsLeaveTheLiveProfileUnchanged()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(3, 3, 3), 26);
            Vector3 centre = profile.CentreOfMass;
            Vector3 target = centre + Vector3.One;
            Vector3[] before = Read(profile);
            Assert.False(profile.PrepareCentreOfMass(target, 0, out int used));
            Assert.Equal(0, used);
            Assert.False(profile.PrepareCentreOfMass(target, -1, out used));
            Assert.Equal(0, used);
            Assert.False(profile.PrepareCentreOfMass(target, 5, out _));
            foreach (Vector3 bad in new[] { new Vector3(float.NaN, 0, 0), new Vector3(0, float.PositiveInfinity, 0),
                new Vector3(0, 0, float.NegativeInfinity) })
                Assert.Throws<ArgumentException>(() => profile.PrepareCentreOfMass(bad, 10, out _));
            Assert.Equal(centre, profile.CentreOfMass);
            Same(before, Read(profile));
            Assert.True(profile.PrepareCentreOfMass(target, 49, out used));
            Assert.Equal(49, used);
        }

        [Fact]
        public void InterruptedPreparationAndItsFirstEvaluationAllocateNothing()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(3, 3, 3), 98);
            Vector3 target = profile.CentreOfMass + Vector3.One;
            profile.PrepareCentreOfMass(target, int.MaxValue, out _);
            Evaluate(profile, out _);
            target += Vector3.One;
            long start = GC.GetAllocatedBytesForCurrentThread();
            int work = 0;
            bool ready;
            do
            {
                ready = profile.PrepareCentreOfMass(target, 7, out int used);
                work += used;
            } while (!ready);
            Evaluate(profile, out Vector3 damping);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.Equal(54, work);
            Assert.Equal(0, allocated);
            Assert.True(damping.LengthSquared() > 0f);
        }

        private static void Evaluate(AeroProfile profile, out Vector3 damping)
        {
            profile.Evaluate(new Vector3(-12f, 3f, -50f), 1.225f, new Vector3(-0.1f, 0.2f, -0.3f),
                AeroCoefficients.Fitted, 1.5e-5f, out _, out _, out damping, out _);
        }

        private static Vector3[] Read(AeroProfile profile)
        {
            profile.Evaluate(new Vector3(-12f, 3f, -50f), 1.225f, new Vector3(-0.1f, 0.2f, -0.3f),
                AeroCoefficients.Fitted, 1.5e-5f, out Vector3 f, out Vector3 t, out Vector3 d, out Vector3 p);
            return new[] { f, t, d, p };
        }

        private static Vector3[] Read(LegacyAeroProfile profile)
        {
            profile.Evaluate(new Vector3(-12f, 3f, -50f), 1.225f, new Vector3(-0.1f, 0.2f, -0.3f),
                AeroCoefficients.Fitted, 1.5e-5f, out Vector3 f, out Vector3 t, out Vector3 d, out Vector3 p);
            return new[] { f, t, d, p };
        }

        private static void Same(Vector3[] expected, Vector3[] actual)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].X), BitConverter.SingleToInt32Bits(actual[i].X));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].Y), BitConverter.SingleToInt32Bits(actual[i].Y));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i].Z), BitConverter.SingleToInt32Bits(actual[i].Z));
            }
        }
    }
}

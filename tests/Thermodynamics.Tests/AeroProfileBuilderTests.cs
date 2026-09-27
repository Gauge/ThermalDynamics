using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Budgeted geometry must reproduce the pre-budgeting implementation bit for bit (D8), publish
    /// only a complete current hull, and prepare first-tick damping without per-tick allocations (C4).
    /// </summary>
    public class AeroProfileBuilderTests
    {
        [Theory]
        [InlineData(26, 1)]
        [InlineData(26, 17)]
        [InlineData(26, 4096)]
        [InlineData(98, 1)]
        [InlineData(98, 17)]
        [InlineData(98, 4096)]
        public void EverySliceSizePreservesTheLegacyArithmetic(int directions, int budget)
        {
            GridModel[] grids = { AeroCalibrationLab.Box(4, 3, 2), AeroFlightLab.Shuttlecock(),
                MixedHull(), Cavity(), AeroCalibrationLab.Cone(30f, 4) };
            foreach (GridModel grid in grids)
            {
                LegacyAeroProfile before = LegacyAeroProfile.Build(grid, directions);
                AeroProfileBuilder job = new AeroProfileBuilder(grid, directions, 7);
                AeroProfile after = Finish(job, budget);
                Assert.Equal(before.Mass, after.Mass);
                Same(before.CentreOfMass, after.CentreOfMass);
                Same(before.Inertia, after.Inertia);
                Assert.Equal(before.Directions, after.Directions);
                Assert.True(after.Mass > 0f);
                foreach (Vector3 direction in AeroProfile.Shell(2)) Same(before.Summary(direction), after.Summary(direction));
                for (int i = 0; i < 12; i++)
                {
                    Vector3 centre = after.CentreOfMass + new Vector3(i * 0.125f, -0.37f, 0.5f);
                    before.SetCentreOfMass(centre);
                    after.SetCentreOfMass(centre);
                    Vector3 velocity = new Vector3(3f - i, i * 0.7f, -50f);
                    Vector3 spin = new Vector3(0.1f * (i - 4), 0.5f, -0.3f);
                    before.Evaluate(velocity, 1.225f, spin, AeroCoefficients.Fitted, 1.5e-5f,
                        out Vector3 f0, out Vector3 t0, out Vector3 d0, out Vector3 p0);
                    after.Evaluate(velocity, 1.225f, spin, AeroCoefficients.Fitted, 1.5e-5f,
                        out Vector3 f1, out Vector3 t1, out Vector3 d1, out Vector3 p1);
                    Same(f0, f1); Same(t0, t1); Same(d0, d1); Same(p0, p1);
                    Assert.True(f1.LengthSquared() > 0f);
                }
                AeroProfileBuilder whole = new AeroProfileBuilder(grid, directions, 7);
                Finish(whole, int.MaxValue);
                Assert.Equal(whole.Work, job.Work);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TopologyChangesInvalidateIncompleteAndCompletedJobs(bool completed)
        {
            GridModel grid = MixedHull();
            AeroProfileBuilder job = new AeroProfileBuilder(grid, 26, 7);
            if (completed) Finish(job, 4096); else job.Advance(100, 7);
            grid.Remove(grid.Blocks[0]);
            Assert.True(job.Invalidated);
            Assert.False(job.Complete);
            Assert.False(job.TryGetResult(7, out AeroProfile stale));
            Assert.Null(stale);
            Assert.Equal(0, job.Advance(100, 7));
            job = new AeroProfileBuilder(grid, 26, 7);
            grid.Add(new BlockModel { Mass = 3f }, new Vector3I(100, 0, 0));
            Assert.False(job.TryGetResult(7, out stale));
            Assert.Equal(0, job.Advance(100, 7));
            Assert.True(job.Invalidated);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MassRevisionChangesCannotPublishStaleResults(bool completed)
        {
            GridModel grid = MixedHull();
            AeroProfileBuilder job = new AeroProfileBuilder(grid, 26, 7);
            if (completed) Finish(job, 4096); else job.Advance(1, 7);
            grid.Blocks[0].Mass *= 2f;
            Assert.False(job.TryGetResult(8, out AeroProfile stale));
            Assert.Null(stale);
            Assert.Equal(0, job.Advance(0, 8));
            Assert.True(job.Invalidated);
            Assert.False(job.TryGetResult(7, out stale));
        }

        [Fact]
        public void ZeroBudgetDoesNoWorkAndEmptyGridsPublishEmptyProfiles()
        {
            AeroProfileBuilder job = new AeroProfileBuilder(MixedHull(), 26, 7);
            Assert.Equal(0, job.Advance(0, 7));
            Assert.Equal(0, job.Advance(-1, 7));
            Assert.Equal(0, job.Work);
            Assert.False(job.TryGetResult(7, out AeroProfile partial));
            foreach (GridModel grid in new GridModel[] { null, new GridModel(2.5f) })
            {
                job = new AeroProfileBuilder(grid, 98, 7);
                Assert.True(job.TryGetResult(7, out AeroProfile profile));
                Assert.Equal(0f, profile.Mass);
                Assert.Equal(Vector3.Zero, profile.Damping(Vector3.One, 1f));
                Assert.Equal(0, job.Work);
            }
        }

        [Fact]
        public void TheFirstEvaluationAlreadyHasDampingAndAllocatesNothing()
        {
            AeroProfile warm = AeroProfile.Build(MixedHull(), 98);
            Evaluate(warm);
            AeroProfile fresh = AeroProfile.Build(MixedHull(), 98);
            long start = GC.GetAllocatedBytesForCurrentThread();
            Vector3 damping = Evaluate(fresh);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.Equal(0, allocated);
            Assert.True(damping.LengthSquared() > 0f);
            Same(Evaluate(warm), damping);
        }

        [Fact]
        public void ASubMillimetreShiftBeforeTheFirstReadKeepsLegacyDamping()
        {
            GridModel grid = MixedHull();
            AeroProfile profile = AeroProfile.Build(grid, 98);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, 98);
            Vector3 centre = profile.CentreOfMass;
            for (int i = 1; i <= 4; i++)
            {
                Vector3 target = centre + new Vector3(0.0004f * i, 0f, 0f);
                profile.SetCentreOfMass(target);
                legacy.SetCentreOfMass(target);
                Assert.Equal(Vector3.Zero, profile.Damping(Vector3.One, 0f));
                Same(legacy.Damping(Vector3.One, 1.225f), profile.Damping(Vector3.One, 1.225f));
            }
        }

        private static Vector3 Evaluate(AeroProfile profile)
        {
            profile.Evaluate(new Vector3(3f, -4f, 50f), 1.225f, new Vector3(0.1f, -0.2f, 0.3f),
                AeroCoefficients.Fitted, 1.5e-5f, out _, out _, out Vector3 damping, out _);
            return damping;
        }

        private static AeroProfile Finish(AeroProfileBuilder job, int budget)
        {
            long charged = 0;
            while (!job.Complete)
            {
                Assert.False(job.TryGetResult(7, out AeroProfile partial));
                Assert.Null(partial);
                int used = job.Advance(budget, 7);
                Assert.InRange(used, 1, budget);
                charged += used;
                Assert.Equal(charged, job.Work);
                Assert.True(charged < 10000000, "Construction failed to terminate.");
            }
            Assert.True(job.TryGetResult(7, out AeroProfile profile));
            Assert.Equal(0, job.Advance(budget, 7));
            return profile;
        }

        private static GridModel MixedHull()
        {
            GridModel grid = new GridModel(0.5f);
            grid.Add(new BlockModel { Size = new Vector3I(2, 3, 4), Mass = 13f }, new Vector3I(-8, 3, 1));
            grid.Add(new BlockModel { Mass = 7f }, new Vector3I(3, -2, 5));
            grid.Add(new BlockModel { Size = new Vector3I(4, 1, 2), Mass = 31f }, new Vector3I(4, 5, -3));
            return grid;
        }

        private static GridModel Cavity()
        {
            GridModel grid = new GridModel(2.5f);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0) grid.Add(new BlockModel { Mass = 1f }, new Vector3I(x, y, z));
            return grid;
        }

        private static void Same(float expected, float actual)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));
        }

        private static void Same(Vector3 expected, Vector3 actual)
        {
            Same(expected.X, actual.X); Same(expected.Y, actual.Y); Same(expected.Z, actual.Z);
        }

        private static void Same(AeroSummary expected, AeroSummary actual)
        {
            Same(expected.Silhouette, actual.Silhouette); Same(expected.SilhouetteMoment, actual.SilhouetteMoment);
            Same(expected.Facing, actual.Facing); Same(expected.FacingMoment, actual.FacingMoment);
            Same(expected.Base, actual.Base); Same(expected.BaseMoment, actual.BaseMoment);
            Same(expected.Wetted, actual.Wetted); Same(expected.WettedMoment, actual.WettedMoment);
            Same(expected.Fineness, actual.Fineness);
        }
    }
}

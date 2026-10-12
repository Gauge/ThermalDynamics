using Aerodynamics.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Detached drag-profile contract, independent view/interpolation controls and explicit consumer demand.</summary>
    [Trait("scope", "integration")]
    public class AeroProfileApiTests
    {
        [Theory]
        [InlineData(26)]
        [InlineData(98)]
        public void ExportReconstructsDirectionalGeometryAndSignedDamping(int count)
        {
            GridModel grid = AeroFlightLab.Shuttlecock();
            AeroRuntime runtime = Finish(grid, count);
            Dictionary<string, object> copy = runtime.CopyProfile();
            Assert.Equal(1, (int)copy["Schema"]);
            Vector3[] directions = (Vector3[])copy["Directions"];
            float[] areas = (float[])copy["Areas"];
            Vector3[] moments = (Vector3[])copy["FirstMoments"];
            Assert.Equal(count, directions.Length);
            Assert.Equal(count * 5, areas.Length);
            Assert.Equal(count * 4, moments.Length);
            for (int i = 0; i < count; i++)
            {
                AeroSummary reference = AeroDrag.Summarise(AeroGeometry.View(grid, directions[i]));
                Check(reference, Row(areas, moments, i));
            }
            foreach (Vector3 wind in new[] { new Vector3(1, 2, 3), new Vector3(-3, 1, -2) })
            {
                Vector3 upstream = Vector3.Normalize(wind);
                double total = 0;
                double[] sums = new double[5];
                Vector3D[] positions = new Vector3D[4];
                for (int i = 0; i < count; i++)
                {
                    double w = Math.Pow(Math.Max(0, Vector3.Dot(upstream, directions[i])), (float)copy["InterpolationExponent"]);
                    if (w < 1e-6) continue;
                    total += w;
                    for (int j = 0; j < 5; j++) sums[j] += areas[i * 5 + j] * w;
                    for (int j = 0; j < 4; j++) positions[j] += (Vector3D)moments[i * 4 + j] * w;
                }
                Assert.True(total > 0);
                float[] interpolated = new float[5];
                Vector3[] first = new Vector3[4];
                for (int j = 0; j < 5; j++) interpolated[j] = (float)(sums[j] / total);
                for (int j = 0; j < 4; j++) first[j] = (Vector3)(positions[j] / total);
                Check(runtime.Profile.Summary(upstream), Row(interpolated, first, 0));
            }
            float[] damping = (float[])copy["DampingMoments"];
            Assert.Equal(6, damping.Length);
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 actual = runtime.Profile.Damping(new Vector3(sign * 2), 0.7f);
                for (int axis = 0; axis < 3; axis++)
                {
                    float integral = damping[axis * 2 + (sign > 0 ? 0 : 1)];
                    Assert.True(integral > 0);
                    float expected = -0.5f * 0.7f * (float)copy["RotationalDrag"] * integral * sign * 4;
                    Near(expected, axis == 0 ? actual.X : axis == 1 ? actual.Y : actual.Z);
                }
            }
            Assert.Equal(runtime.Profile.Mass, (float)copy["Mass"]);
            Assert.Equal(runtime.Profile.Inertia, (Vector3)copy["Inertia"]);
            Assert.Equal(runtime.Profile.CentreOfMass, (Vector3)copy["CentreOfMass"]);
        }

        [Fact]
        public void ArraysAndDictionaryAreDetachedAndRetainedCopiesDoNotChange()
        {
            GridModel grid = AeroFlightLab.Shuttlecock();
            AeroRuntime runtime = Finish(grid, 26);
            Dictionary<string, object> original = runtime.CopyProfile();
            Dictionary<string, object> changed = runtime.CopyProfile();
            Dictionary<string, object> frozen = new Dictionary<string, object>();
            foreach (string key in original.Keys)
            {
                Array a = original[key] as Array;
                frozen[key] = a == null ? original[key] : a.Clone();
                if (a != null) Assert.NotSame(a, changed[key]);
            }
            foreach (object value in changed.Values)
            {
                Array a = value as Array;
                if (a != null) Array.Clear(a, 0, a.Length);
            }
            changed.Clear();
            Dictionary<string, object> next = runtime.CopyProfile();
            foreach (string key in original.Keys)
            {
                Array a = original[key] as Array;
                if (a != null)
                {
                    Assert.Equal((Array)frozen[key], a);
                    Assert.Equal((Array)frozen[key], (Array)next[key]);
                }
                else Assert.Equal(original[key], next[key]);
            }
            Vector3 savedCentre = (Vector3)original["CentreOfMass"];
            Assert.True(runtime.Prepare(grid, true, 26, 0, new Vector3(30), 1, 1));
            Assert.True(runtime.Rebuilding);
            Assert.Equal(savedCentre, (Vector3)runtime.CopyProfile()["CentreOfMass"]);
            for (int i = 0; runtime.Rebuilding && i < 10000; i++)
                runtime.Prepare(grid, true, 26, 0, new Vector3(30), 4096, 256);
            Assert.True(runtime.Ready);
            Assert.Equal(savedCentre, (Vector3)original["CentreOfMass"]);
            Assert.Equal(new Vector3(30), (Vector3)runtime.CopyProfile()["CentreOfMass"]);
            grid.Add(new BlockModel { Mass = 1 }, new Vector3I(50, 50, 50));
            Assert.True(runtime.Prepare(grid, true, 26, 1, new Vector3(30), 1, 1));
            Assert.True(runtime.Rebuilding);
            Assert.NotNull(runtime.CopyProfile());
            Assert.Equal(savedCentre, (Vector3)original["CentreOfMass"]);
        }

        [Fact]
        public void ConsumersAreIdempotentIndependentAndPrepareWithForcesDisabled()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            runtime.SetConsumer(41, true);
            runtime.SetConsumer(41, true);
            runtime.SetConsumer(42, true);
            runtime.SetConsumer(41, false);
            Assert.True(runtime.Requested);
            for (int i = 0; !runtime.Ready && i < 100000; i++)
            {
                runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 17, 7);
                Assert.InRange(runtime.BuildWork, 0, 17);
                Assert.InRange(runtime.CentreWork, 0, 7);
                if (!runtime.Ready) Assert.Null(runtime.CopyProfile());
            }
            Assert.NotNull(runtime.CopyProfile());
            runtime.Reset();
            Assert.True(runtime.Requested);
            for (int i = 0; !runtime.Ready && i < 10000; i++)
                runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 4096, 256);
            Assert.True(runtime.Ready);
            runtime.SetConsumer(42, false);
            Assert.False(runtime.Requested);
            runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 17, 7);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 17, 7);
                Assert.Null(runtime.CopyProfile());
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(0, runtime.BuildWork);
            Assert.Equal(0, runtime.CentreWork);
        }

        [Fact]
        public void PartialPreparationGetterDoesNotSpendWorkOrAllocate()
        {
            AeroRuntime runtime = new AeroRuntime();
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            Assert.False(runtime.Prepare(grid, true, 98, 0, Vector3.Zero, 1, 1));
            Assert.Null(runtime.CopyProfile());
            int work = runtime.BuildWork;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Assert.Null(runtime.CopyProfile());
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.Equal(work, runtime.BuildWork);
            Assert.Equal(0, runtime.CentreWork);
            Assert.False(runtime.Ready);
        }

        [Fact]
        public void ExternalDemandCannotEnablePhysicsWritesAndCloseDropsIt()
        {
            string root = ShippedBlocks.RepoRoot();
            string adapter=File.ReadAllText(Path.Combine(root,"../Aerodynamics/Source/FlightGrid.cs"));
            adapter = System.Text.RegularExpressions.Regex.Replace(adapter, @"\s+", "");
            int prepared=adapter.IndexOf("prepared=PrepareProfile(session,centre)",StringComparison.Ordinal);
            int gate=adapter.IndexOf("if(!settings.EnableForces||physics.IsStatic)return;",StringComparison.Ordinal);
            int write=adapter.IndexOf("physics.AddForce",StringComparison.Ordinal);
            Assert.True(prepared>=0 && gate>prepared && write>gate);
            string session=File.ReadAllText(Path.Combine(root,"../Aerodynamics/Source/Session.cs"));
            session = System.Text.RegularExpressions.Regex.Replace(session, @"\s+", "");
            Assert.Contains("grid.Dispose();grids.Remove",session);
            Assert.Contains("flight.Runtime.CopyProfile()",session);

        }

        private static AeroRuntime Finish(GridModel grid, int count)
        {
            AeroRuntime runtime = new AeroRuntime();
            for (int i = 0; !runtime.Ready && i < 10000; i++)
                runtime.Prepare(grid, true, count, 0, Vector3.Zero, 4096, 256);
            Assert.True(runtime.Ready);
            return runtime;
        }

        private static AeroSummary Row(float[] a, Vector3[] m, int i)
        {
            return new AeroSummary
            {
                Silhouette = a[i * 5], Facing = a[i * 5 + 1], Base = a[i * 5 + 2],
                Wetted = a[i * 5 + 3], Fineness = a[i * 5 + 4],
                SilhouetteMoment = m[i * 4], FacingMoment = m[i * 4 + 1],
                BaseMoment = m[i * 4 + 2], WettedMoment = m[i * 4 + 3]
            };
        }

        private static void Check(AeroSummary expected, AeroSummary actual)
        {
            Near(expected.Silhouette, actual.Silhouette); Near(expected.Facing, actual.Facing);
            Near(expected.Base, actual.Base); Near(expected.Wetted, actual.Wetted);
            Near(expected.Fineness, actual.Fineness);
            Near(expected.SilhouetteMoment, actual.SilhouetteMoment); Near(expected.FacingMoment, actual.FacingMoment);
            Near(expected.BaseMoment, actual.BaseMoment); Near(expected.WettedMoment, actual.WettedMoment);
        }

        private static void Near(Vector3 expected, Vector3 actual)
        {
            Near(expected.X, actual.X); Near(expected.Y, actual.Y); Near(expected.Z, actual.Z);
        }

        private static void Near(float expected, float actual)
        {
            Assert.InRange(Math.Abs(expected - actual), 0, 2e-5f * Math.Max(1, Math.Abs(expected)));
        }
    }
}

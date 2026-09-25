using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Shared extraction and projection against independent source-index and software-depth oracles.</summary>
    public class ThermalVisionLabTests
    {
        [Fact]

        public void PartialFaceCullingRejectsOnlyOffscreenPatchesIncludingRenderOffset()
        {
            var projection=MatrixD.CreatePerspectiveFieldOfView(1,1.6,.05,5000);
            int rejected=0;
            for(int trial=0;trial<20;trial++)
            {
                var camera=MatrixD.CreateFromYawPitchRoll(trial*.02,trial*.01,trial*.03);
                var viewProjection=MatrixD.Invert(camera)*projection;

                var frustum=new BoundingFrustumD(viewProjection);
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {

                    var a=new Vector3D(-40+x*20,-40+y*20,-10);

                    var c=a+new Vector3D(20,20,0);
                    bool culled=frustum.Contains(ThermalVisionGeometry.PatchBounds(a,c))==ContainmentType.Disjoint;
                    if(trial==0 && culled)rejected++;
                    for(int j=0;j<=20;j++)for(int i=0;i<=20;i++)
                    {

                        var point=a+new Vector3D(i,j,0)+camera.Backward*.001;
                        var clip=Vector4D.Transform(new Vector4D(point,1),viewProjection);
                        bool inside=clip.W>0 && clip.X>=-clip.W && clip.X<=clip.W
                            && clip.Y>=-clip.W && clip.Y<=clip.W && clip.Z>=0 && clip.Z<=clip.W;
                        if(inside)Assert.False(culled);
                    }
                }
            }
            Assert.Equal(12,rejected);

            var full=new BoundingFrustumD(projection);
            Assert.Equal(ContainmentType.Contains,full.Contains(ThermalVisionGeometry.PatchBounds(new Vector3D(-1,-1,-10),new Vector3D(1,1,-10))));
        }

        [Fact]

        public void SharedFaceNormalMatchesEverySubpatchUnderRigidMotion()
        {

            var random=new Random(7721);
            for(int trial=0;trial<200;trial++)
            {
                var world=MatrixD.CreateFromYawPitchRoll(random.NextDouble()*6,random.NextDouble()*6,random.NextDouble()*6);

                world.Translation=new Vector3D(1e6,-2e6,3e6);
                Vector3D u=world.Right*8,v=world.Up*5;
                Vector3D eye=world.Translation+world.Backward*(trial%2==0?10:-10);
                Vector3 normal;bool reverse;
                Assert.True(ThermalVisionGeometry.FaceNormal(u,v,eye-world.Translation,out normal,out reverse));
                for(int x=0;x<4;x++)for(int y=0;y<4;y++)
                {
                    Vector3D a=world.Translation+u*(x/4d)+v*(y/4d),b=a+u/4,c=b+v/4;
                    var reference=Vector3D.Cross(b-a,c-a);
                    bool expectedReverse=Vector3D.Dot(reference,eye-a)<0;
                    if(expectedReverse)reference=-reference;
                    Assert.Equal(expectedReverse,reverse);
                    Assert.InRange(Vector3D.Distance(normal,Vector3D.Normalize(reference)),0,1e-6);
                }
            }
            Vector3 unused;bool ignored;
            Assert.False(ThermalVisionGeometry.FaceNormal(Vector3D.Right,Vector3D.Right,Vector3D.Up,out unused,out ignored));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.7)]
        [InlineData(1.5)]

        public void NearCapMatchesRegionMembershipAcrossCameraRotation(double angle)
        {
            var region = new ThermalVisionRegionPartition.Region(new Vector3D(-.02, -.04, -.2), new Vector3D(.08, .03, .2), 600);
            MatrixD camera = MatrixD.CreateRotationY(angle);
            MatrixD inverse = MatrixD.Transpose(camera);
            MatrixD projection = MatrixD.CreatePerspectiveFieldOfView(1.2, 1.6, .05, 5000);
            var cap = ThermalVisionRegionPartition.NearCap(region, camera, projection, .0525);
            Assert.InRange(cap.Length, 0, 10);
            var local = new Vector3D[cap.Length];
            for (int i = 0; i < cap.Length; i++)
            {
                local[i] = Vector3D.Transform(cap[i], inverse);
                Assert.InRange(Math.Abs(local[i].Z + .0525), 0, 1e-9);
            }
            Vector3D centre;
            float width, height;
            ThermalVisionDepthLayers.Plane(.0525, projection, camera, out centre, out width, out height);
            for (int y = 0; y < 31; y++) for (int x = 0; x < 47; x++)
            {

                var point = new Vector3D((2 * (x + .37) / 47 - 1) * width, (2 * (y + .41) / 31 - 1) * height, -.0525);
                bool positive = false, negative = false;
                for (int i = 0; i < local.Length; i++)
                {
                    Vector3D a = local[i], b = local[(i + 1) % local.Length];
                    double cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
                    positive |= cross > 1e-12; negative |= cross < -1e-12;
                }
                Assert.Equal(Within(region, Vector3D.Transform(point, camera)), cap.Length >= 3 && !(positive && negative));
            }
        }

        [Fact]

        public void NearCapScratchBuffersDoNotLeakPreviousPolygonsOrAllocateAfterWarmup()
        {

            var a=new List<Vector3D>(16); var b=new List<Vector3D>(16);
            var projection=MatrixD.CreatePerspectiveFieldOfView(1.2,1.6,.05,5000);
            var regions=new[] {
                new ThermalVisionRegionPartition.Region(new Vector3D(-1),new Vector3D(1),300),
                new ThermalVisionRegionPartition.Region(new Vector3D(100),new Vector3D(101),300),
                new ThermalVisionRegionPartition.Region(new Vector3D(-.02,-.04,-.2),new Vector3D(.08,.03,.2),600) };
            int expected=0;
            for(int i=0;i<1000;i++)
            {
                var camera=MatrixD.CreateRotationY(i*.013);
                var reference=ThermalVisionRegionPartition.NearCap(regions[i%3],camera,projection,.0525);
                var result=ThermalVisionRegionPartition.NearCap(regions[i%3],camera,projection,.0525,a,b);
                Assert.Equal(reference.Length,result.Count);
                for(int j=0;j<result.Count;j++) Assert.Equal(reference[j],result[j]);
                expected+=result.Count;
            }
            long before=GC.GetAllocatedBytesForCurrentThread();
            int total=0;
            for(int i=0;i<1000;i++)
                total+=ThermalVisionRegionPartition.NearCap(regions[i%3],MatrixD.CreateRotationY(i*.013),projection,.0525,a,b).Count;
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.Equal(expected,total);
            Assert.Equal(0,bytes);
            Assert.Throws<ArgumentException>(()=>ThermalVisionRegionPartition.NearCap(regions[0],MatrixD.Identity,projection,.0525,a,a));
        }

        [Fact]

        public void RegionPartitionPreservesMaximumHeatAndHasNoOverlappingInteriors()
        {
            var inputs = new[] {
                new ThermalVisionRegionPartition.Region(new Vector3D(-2), new Vector3D(2), 300),
                new ThermalVisionRegionPartition.Region(new Vector3D(-1), new Vector3D(3), 600),
                new ThermalVisionRegionPartition.Region(new Vector3D(-3), new Vector3D(1), 400) };
            foreach (bool reverse in new[] { false, true })
            {

                var field = new ThermalVisionRegionPartition(128);
                for (int i = 0; i < inputs.Length; i++) Assert.True(field.TryAdd(inputs[reverse ? inputs.Length - 1 - i : i]));
                for (double x = -3.25; x < 3.5; x += .5)
                for (double y = -3.25; y < 3.5; y += .5)
                for (double z = -3.25; z < 3.5; z += .5)
                {

                    var point = new Vector3D(x, y, z);
                    float expected = 0, actual = 0;
                    int covering = 0;
                    foreach (var input in inputs) if (Within(input, point)) expected = Math.Max(expected, input.Kelvin);
                    for (int i = 0; i < field.Count; i++) if (Within(field[i], point)) { covering++; actual = field[i].Kelvin; }
                    Assert.InRange(covering, 0, 1);
                    Assert.Equal(expected, actual);
                }
            }
        }


        private static bool Within(ThermalVisionRegionPartition.Region region, Vector3D point)
        {
            return point.X >= region.Min.X && point.X < region.Max.X
                && point.Y >= region.Min.Y && point.Y < region.Max.Y
                && point.Z >= region.Min.Z && point.Z < region.Max.Z;
        }

        [Fact]

        public void RegionCapacityFailureKeepsPreviousFieldIntact()
        {

            var field = new ThermalVisionRegionPartition(1);
            Assert.True(field.TryAdd(new ThermalVisionRegionPartition.Region(new Vector3D(-2), new Vector3D(2), 300)));
            Assert.False(field.TryAdd(new ThermalVisionRegionPartition.Region(new Vector3D(-1), new Vector3D(1), 600)));
            Assert.Equal(1, field.Count);
            Assert.Equal(300f, field[0].Kelvin);
            Assert.Equal(new Vector3D(-2), field[0].Min);
            Assert.Equal(new Vector3D(2), field[0].Max);
        }

        [Theory]
        [InlineData("overlap-exit")]
        [InlineData("camera-inside")]

        public void RegionOverwriteNeedsPartitioningAndNearPlaneInitialization(string scene)
        {
            var raw = ThermalVisionVolumeLab.RunBoundaryStress(scene, false);
            Assert.True(raw.HotReference > 0);
            Assert.True(raw.HotMissing > 0);
            var corrected = ThermalVisionVolumeLab.RunBoundaryStress(scene, true);
            Assert.Equal(raw.HotReference, corrected.HotReference);
            Assert.True(corrected.Pass);
        }

        [Fact]

        public void AdjacentRegionsResolveSharedBoundaryBeforeEnteringNextRegion()
        {
            var result = ThermalVisionVolumeLab.RunBoundaryStress("adjacent", true);
            Assert.True(result.HotReference > 0);
            Assert.True(result.Pass);
        }

        [Fact]

        public void OrderedRegionFacesLocalizeHeatWithoutPaintingExternalForeground()
        {
            var isolated = ThermalVisionVolumeLab.Run("isolated", "ordered-box-faces");
            Assert.True(isolated.HotReference > 0);
            Assert.Equal(isolated.HotReference, isolated.HotCorrect);
            var foreground = ThermalVisionVolumeLab.Run("foreground", "ordered-box-faces");
            Assert.True(foreground.ColdReference > 0);
            Assert.Equal(0, foreground.ColdFalseHot);
            Assert.Equal(foreground.HotReference, foreground.HotCorrect);
            var intrusion = ThermalVisionVolumeLab.Run("open-frame-intrusion", "ordered-box-faces");
            Assert.True(intrusion.ColdFalseHot > 0);
            Assert.Equal(intrusion.HotReference, intrusion.HotCorrect);
        }

        [Fact]

        public void ExactCellBoundariesRecoverIsolatedHeatButCannotIdentifyForeignSurfaces()
        {
            var isolated = ThermalVisionVolumeLab.Run("isolated", "exact-cell-boundary");
            Assert.True(isolated.HotReference > 0);
            Assert.True(isolated.Pass);
            var foreground = ThermalVisionVolumeLab.Run("foreground", "exact-cell-boundary");
            Assert.True(foreground.ColdReference > 0);
            Assert.True(foreground.Pass);
            var intrusion = ThermalVisionVolumeLab.Run("open-frame-intrusion", "exact-cell-boundary");
            Assert.True(intrusion.HotCorrect > 0);
            Assert.True(intrusion.ColdFalseHot > 0);
            Assert.False(intrusion.Pass);
        }

        [Fact]

        public void ExpandingCoarseSlicesRecoversHeatAtTheCostOfForegroundFalseHeat()
        {
            var sparse = ThermalVisionVolumeLab.Run("isolated", "preceding-slice");
            Assert.True(sparse.HotMissing > 0);
            var expanded = ThermalVisionVolumeLab.Run("isolated", "expanded-slice");
            Assert.True(expanded.HotCorrect > sparse.HotCorrect);
            var foreground = ThermalVisionVolumeLab.Run("foreground", "expanded-slice");
            Assert.True(foreground.ColdFalseHot > 0);
        }

        [Fact]

        public void ReuseFixturePreservesAStationarySceneWithinTheDeclaredQueryBudget()
        {
            var result = ThermalVisionReuseLab.Run(64, 36, false, false);
            Assert.Equal(2304, result.WarmupQueries);
            Assert.Equal(128, result.MaxQueries);
            Assert.Equal(60 * 128, result.TotalQueries);
            Assert.Equal(0, result.PeakHoles);
            Assert.Equal(0, result.PeakWrongSurface);
        }

        [Fact]

        public void ReuseFixtureDetectsBothCameraHolesAndNewOccluderFalseHeat()
        {
            var pan = ThermalVisionReuseLab.Run(64, 36, true, false);
            Assert.True(pan.PeakHoles > 0);
            Assert.Equal(128, pan.MaxQueries);
            var wall = ThermalVisionReuseLab.Run(128, 72, false, true);
            Assert.Equal(0, wall.PeakHoles);
            Assert.True(wall.PeakWrongSurface > 0);
            Assert.True(wall.PeakFalseHot > 0, "Fixture must catch hot cached surfaces behind the new cold wall.");
            Assert.True(wall.FinalWrongSurface > 0, "One second of this budget does not refresh all detail samples.");
        }

        [Theory]
        [InlineData(640, 480)]
        [InlineData(1280, 720)]
        [InlineData(1920, 1080)]
        [InlineData(3440, 1440)]

        public void SurveyDockStaysOnScreenAndClearOfTheLiveCrosshair(int width, int height)
        {
            Vector2 size, offset;
            ThermalVisionSensorOptics.Layout(new Vector2(width, height), (float)width / height, out size, out offset);
            Assert.InRange(size.X / size.Y, (float)width / height - .0001f, (float)width / height + .0001f);
            Assert.True(offset.X - size.X / 2 - 5 > 24, "Snapshot frame intrudes on centre reticle.");
            Assert.True(offset.X + size.X / 2 + 5 < width / 2f);
            Assert.True(offset.Y + size.Y / 2 + 5 < height / 2f);
            Assert.True(offset.Y - size.Y / 2 - 5 > -height / 2f);
        }

        [Fact]

        public void DetailedSurveyNeedsSeventyTwoUpdatesWithoutRaisingTheQueryAllowance()
        {

            var scan = new ThermalVisionRayScan<int>(128 * 72, 1, 128);
            scan.Begin();
            for (int frame = 1; frame <= 72; frame++)
            {
                scan.AdvanceFrame(frame);
                int issued = 0;
                ThermalVisionRayScan<int>.Ticket ticket;
                while (scan.TryIssue(out ticket))
                {
                    Assert.True(scan.Complete(ticket, ticket.Pixel));
                    issued++;
                }
                Assert.Equal(128, issued);
                Assert.Equal(frame == 72, scan.HasFrame);
            }
            int last;
            Assert.True(scan.TryRead(9215, out last));
            Assert.Equal(9215, last);
        }

        [Fact]

        public void SensorProjectionAccountsForAspectCameraRotationAndLensOffset()
        {
            var projection = MatrixD.CreatePerspectiveFieldOfView(Math.PI / 2, 2, .1, 1000);
            var ray = ThermalVisionSensorOptics.Ray(0, 0, 2, 2, projection, MatrixD.Identity);
            Assert.InRange(Vector3D.Distance(ray, new Vector3D(-2d / 3, 1d / 3, -2d / 3)), 0, 1e-12);
            var world = MatrixD.CreateRotationY(.7);

            world.Translation = new Vector3D(100, 200, 300);
            ray = ThermalVisionSensorOptics.Ray(0, 0, 1, 1, projection, world);
            Assert.InRange(Vector3D.Distance(ray, world.Forward), 0, 1e-12);
            projection.M31 = .2;
            projection.M32 = -.1;
            ray = ThermalVisionSensorOptics.Ray(0, 0, 1, 1, projection, MatrixD.Identity);
            Assert.Equal(.4, ray.X / -ray.Z, 10);
            Assert.Equal(-.1, ray.Y / -ray.Z, 10);
            projection.M11 = double.NaN;
            Assert.Throws<ArgumentException>(() => ThermalVisionSensorOptics.Ray(0, 0, 1, 1, projection, world));
        }

        [Theory]
        [InlineData(ThermalVisionState.Mode.Cividis)]
        [InlineData(ThermalVisionState.Mode.WhiteHot)]

        public void SensorUnknownsAreHatchedAndNeverBecomeMeasuredColdPixels(ThermalVisionState.Mode mode)
        {
            var empty = ThermalVisionSensorOptics.Shade(false, 900, 1, 0, 0, mode, 225, 625);
            Assert.Equal(new Color(12, 12, 12), empty);
            var unknown = ThermalVisionSensorOptics.Shade(true, float.NaN, .5f, 0, 0, mode, 225, 625);
            var adjacent = ThermalVisionSensorOptics.Shade(true, float.NaN, .5f, 1, 0, mode, 225, 625);
            Assert.Equal(unknown.R, unknown.G);
            Assert.Equal(unknown.G, unknown.B);
            Assert.NotEqual(unknown, adjacent);
            Assert.NotEqual(unknown, empty);
            var cold = ThermalVisionSensorOptics.Shade(true, 250, 1, 0, 0, mode, 225, 625);
            Assert.NotEqual(unknown, cold);
            Assert.Equal(cold, ThermalVisionSensorOptics.Shade(true, 250, 0, 1, 0, mode, 225, 625));
            if (mode == ThermalVisionState.Mode.WhiteHot)
            { Assert.Equal(cold.R, cold.G); Assert.Equal(cold.G, cold.B); }
        }

        [Fact]

        public void SensorScanPublishesOnlyCompleteImagesUnderOutOfOrderCallbacks()
        {

            var scan = new ThermalVisionRayScan<int>(5, 3, 3);
            scan.Begin();
            ThermalVisionRayScan<int>.Ticket a, b, c, d, e;
            Assert.False(scan.TryIssue(out a));
            scan.AdvanceFrame(10);
            Assert.True(scan.TryIssue(out a));
            Assert.True(scan.TryIssue(out b));
            Assert.True(scan.TryIssue(out c));
            Assert.False(scan.TryIssue(out d));
            Assert.True(scan.Complete(c, 30));
            Assert.True(scan.Complete(a, 10));
            Assert.False(scan.Complete(a, 999));
            Assert.False(scan.TryIssue(out d));
            scan.AdvanceFrame(10);
            Assert.False(scan.TryIssue(out d));
            int value;
            Assert.False(scan.TryRead(0, out value));
            scan.AdvanceFrame(11);
            Assert.True(scan.TryIssue(out d));
            Assert.True(scan.TryIssue(out e));
            Assert.False(scan.TryIssue(out a));
            Assert.True(scan.Complete(e, 50));
            Assert.True(scan.Complete(d, 40));
            Assert.False(scan.HasFrame);
            Assert.True(scan.Complete(b, 20));
            for (int i = 0; i < 5; i++)
            {
                Assert.True(scan.TryRead(i, out value));
                Assert.Equal((i + 1) * 10, value);
            }
            Assert.Equal(0, scan.Outstanding);
        }

        [Fact]

        public void ViewRestartsDoNotEvadeOutstandingOrPerFrameLimits()
        {

            var scan = new ThermalVisionRayScan<int>(2, 1, 2);
            ThermalVisionRayScan<int>.Ticket old, fresh, extra;
            scan.Begin();
            scan.AdvanceFrame(1);
            Assert.True(scan.TryIssue(out old));
            for (int i = 0; i < 100; i++)
            {
                scan.Begin();
                Assert.False(scan.TryIssue(out fresh));
                Assert.Equal(1, scan.Outstanding);
            }
            Assert.False(scan.Complete(old, 999));
            Assert.True(scan.TryIssue(out fresh));
            Assert.False(scan.Complete(old, 999));
            Assert.Equal(1, scan.Outstanding);
            Assert.True(scan.Complete(fresh, 100));
            Assert.False(scan.TryIssue(out extra));
            scan.AdvanceFrame(0);
            Assert.False(scan.TryIssue(out extra));
            scan.AdvanceFrame(2);
            Assert.True(scan.TryIssue(out extra));
            Assert.True(scan.Complete(extra, 200));
            int value;
            Assert.True(scan.TryRead(0, out value));
            Assert.Equal(100, value);
            scan.Invalidate();
            Assert.False(scan.TryRead(0, out value));
            Assert.False(scan.TryIssue(out fresh));
        }

        [Fact]

        public void ForeignOrDefaultSensorTicketsCannotReleaseCapacity()
        {

            var scan = new ThermalVisionRayScan<int>(1, 1, 1);

            var other = new ThermalVisionRayScan<int>(1, 1, 1);
            scan.Begin(); other.Begin();
            scan.AdvanceFrame(1); other.AdvanceFrame(1);
            ThermalVisionRayScan<int>.Ticket a, b;
            Assert.True(scan.TryIssue(out a));
            Assert.True(other.TryIssue(out b));
            Assert.False(scan.Complete(b, 999));
            Assert.False(scan.Complete(default(ThermalVisionRayScan<int>.Ticket), 999));
            Assert.Equal(1, scan.Outstanding);
            Assert.True(scan.Complete(a, 1));
            Assert.Equal(1, other.Outstanding);
        }

        [Fact]

        public void SensorFixtureResolvesNearestOpaqueSurfaceRegardlessOfOrder()
        {
            var direction = Vector3D.Normalize(new Vector3D(-.6, -.3, -7));
            var a = ThermalVisionRayLab.Trace(direction);
            var b = ThermalVisionRayLab.Trace(direction, true);
            Assert.Equal(2, a.Surface);
            Assert.Equal(280f, a.Kelvin);
            Assert.Equal(a.Surface, b.Surface);
            Assert.Equal(a.Distance, b.Distance);
            Assert.Equal(-6.8 / direction.Z, a.Distance, 10);
        }

        [Fact]

        public void SensorFixturePreservesNoReturnAndEstimatedTemperatureSemantics()
        {
            var sky = ThermalVisionRayLab.Trace(Vector3D.UnitY);
            Assert.Equal(0, sky.Surface);
            Assert.True(float.IsNaN(sky.Kelvin));
            var ground = ThermalVisionRayLab.Trace(-Vector3D.UnitY);
            Assert.Equal(1, ground.Surface);
            Assert.Equal(1.8, ground.Distance, 10);
            Assert.True(ground.Estimated);
            var centre = ThermalVisionRayLab.Direction(0, 0, 1, 1);
            Assert.Equal(new Vector3D(0, 0, -1), centre);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8192)]
        [InlineData(8193)]
        [InlineData(26914)]
        [InlineData(61199)]
        [InlineData(65536)]

        public void LargeModelsFinishAcrossVariableSlicesWithoutRepeatingOrLosingTriangles(int count)
        {
            var source = new ThermalVisionLab.Source(count, 7);

            var build = new ThermalVisionMeshBuild(source);
            int slice = 0;
            int[] budgets = { 0, 1, 64, 8192, 37 };
            while (!build.Complete)
            {
                Assert.Null(build.Result);
                int budget = budgets[slice++ % budgets.Length];
                Assert.InRange(build.Advance(budget), 0, budget);
                Assert.True(slice < 1000, "Extraction failed to make bounded progress.");
            }
            int expectedUnsupported = (count + 6) / 7;
            Assert.Equal(count, source.Reads);
            Assert.Equal(expectedUnsupported, build.Unsupported);
            Assert.Equal(count - expectedUnsupported, build.Result.Length);
            int covered = 0;
            foreach (var batch in build.Mesh.Batches)
            {
                Assert.Equal(covered, batch.Start);
                Assert.InRange(batch.Count, 1, ThermalVisionMeshBatch.Size);
                covered += batch.Count;
            }
            Assert.Equal(build.Retained, covered);
            var completed = build.Result;
            Assert.Equal(0, build.Advance(8192));
            Assert.Same(completed, build.Result);
        }

        [Fact]

        public void BatchCullingPreservesEveryFrontFaceOfADenseHullAcrossCameraDirections()
        {

            var build = new ThermalVisionMeshBuild(new ThermalVisionLab.HullSource(72));
            while (!build.Complete) build.Advance(8192);
            Assert.Equal(62208, build.Retained);
            int tested = 0;
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;

                Vector3D eye = new Vector3D(x * 5, y * 5, z * 5);
                int candidates = 0, visible = 0;
                foreach (var batch in build.Mesh.Batches)
                {
                    bool rejected = batch.IsEntirelyBackFacing(eye);
                    if (!rejected) candidates += batch.Count;
                    for (int i = batch.Start; i < batch.Start + batch.Count; i++)
                    {
                        var triangle = build.Result[i];
                        Vector3D a = triangle.A, b = triangle.B, c = triangle.C;
                        bool front = Vector3D.Dot(Vector3D.Cross(b - a, c - a), eye - a) > 0;
                        if (front) { visible++; Assert.False(rejected); }
                    }
                }
                Assert.True(visible > 0);
                Assert.Equal(visible, candidates);
                Assert.InRange(candidates, 1, ThermalVisionScenePolicy.TriangleLimit);
                tested++;
            }
            Assert.Equal(26, tested);
        }

        [Fact]

        public void MixedAndDegenerateBatchesRemainConservative()
        {

            var batch = new ThermalVisionMeshBatch();
            var triangle = new ThermalVisionTriangle { A = Vector3.Zero, B = Vector3.UnitX, C = Vector3.UnitY, LocalNormal = Vector3.UnitZ };
            batch.Add(0, triangle);
            triangle.LocalNormal = -Vector3.UnitZ;
            batch.Add(1, triangle);
            Assert.False(batch.IsEntirelyBackFacing(new Vector3D(0, 0, 5)));
            Assert.False(batch.IsEntirelyBackFacing(new Vector3D(0, 0, -5)));

            var degenerate = new ThermalVisionMeshBatch();
            degenerate.Add(0, new ThermalVisionTriangle());
            Assert.False(degenerate.IsEntirelyBackFacing(new Vector3D(1e8, 1e8, 1e8)));
        }

        [Fact]

        public void BatchAndReferenceProjectionProduceTheSameSoftwareImage()
        {
            var a = ThermalVisionLab.HullFrame(false);
            var b = ThermalVisionLab.HullFrame(true);
            Assert.Equal(a.Temperature, b.Temperature);
            Assert.Contains(450f, a.Temperature);
        }

        [Fact]

        public void OversizedSourcesAreRejectedBeforeAnyRead()
        {
            var source = new ThermalVisionLab.Source(65537);
            Assert.Throws<ArgumentException>(() => new ThermalVisionMeshBuild(source));
            Assert.Equal(0, source.Reads);
        }

        [Fact]

        public void DepthOracleOccludesRearHeatIndependentlyOfSubmissionOrder()
        {
            var first = ThermalVisionLab.OcclusionScene(false);
            var second = ThermalVisionLab.OcclusionScene(true);
            Assert.Equal(first.Temperature, second.Temperature);
            Assert.DoesNotContain(750f, first.Temperature);
            Assert.Contains(300f, first.Temperature);
            Assert.Contains(650f, first.Temperature);
            int center = ThermalVisionLab.Frame.Width * (ThermalVisionLab.Frame.Height / 2) + ThermalVisionLab.Frame.Width / 2;
            Assert.Equal(300f, first.Temperature[center]);
        }

        [Fact]

        public void ProductionProjectionRejectsDegenerateAndBackFacingSurfaces()
        {

            var eye = new Vector3D(0, 0, 5);
            var triangle = new ThermalVisionTriangle { A = Vector3.Zero, B = Vector3.UnitY, C = Vector3.UnitX, LocalNormal = -Vector3.UnitZ };
            ThermalVisionWorldTriangle output;
            Assert.Equal(ThermalVisionProjection.Backface, ThermalVisionGeometry.Project(triangle, MatrixD.Identity, eye, true, eye, out output));
            triangle.B = triangle.C = triangle.A;
            triangle.LocalNormal = Vector3.Zero;
            Assert.Equal(ThermalVisionProjection.Degenerate, ThermalVisionGeometry.Project(triangle, MatrixD.Identity, eye, true, eye, out output));
        }
    }
}

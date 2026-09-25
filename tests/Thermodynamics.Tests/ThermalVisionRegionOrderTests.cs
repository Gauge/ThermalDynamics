using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;
using Xunit;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Tests
{
    /// <summary>Spatial ordering against independent ray intervals, field preservation and fragmentation bounds.</summary>
    public class ThermalVisionRegionOrderTests
    {
        [Fact]

        public void SolidBlockersPruneHiddenRegionsAndImmediatelyRevealOnRemoval()
        {

            var cells=new List<Region>();
            for(int x=-8;x<8;x++)for(int y=-8;y<8;y++)for(int z=10;z<14;z++)

            {var p=new Vector3D(x,y,z);cells.Add(new Region(p,p+Vector3D.One,300));}
            ThermalVisionRegionOrder order;Assert.True(ThermalVisionRegionOrder.TryBuild(cells,2048,out order));
            var eye=Vector3D.Zero;

            var frustum=new BoundingFrustumD(MatrixD.CreateLookAt(eye,new Vector3D(0,0,1),Vector3D.Up)
                *MatrixD.CreatePerspectiveFieldOfView(1.5,1.8,.1,100));

            var all=new List<Region>();var visible=new List<Region>();
            order.WriteVisibleNearToFar(eye,all,frustum);

            var blocker=new BoundingBoxD(new Vector3D(-1,-1,2),new Vector3D(1,1,3));
            var blockers=new List<BoundingBoxD>{blocker};int hidden;
            order.WriteVisibleNearToFar(eye,visible,frustum,blockers,out hidden);
            Assert.True(hidden>0);Assert.True(visible.Count<all.Count);
            Assert.Equal(all.FindAll(r=>!ThermalVisionOcclusion.Hidden(eye,new BoundingBoxD(r.Min,r.Max),MatrixD.Identity,blocker)),visible);
            blockers.Clear();order.WriteVisibleNearToFar(eye,visible,frustum,blockers,out hidden);
            Assert.Equal(0,hidden);Assert.Equal(all,visible);
        }

        [Fact]

        public void HierarchicalFrustumTraversalMatchesOrderedLeafFilteringForMovingViews()
        {

            var cells=new List<Region>();
            for(int x=0;x<8;x++) for(int y=0;y<8;y++) for(int z=0;z<8;z++)

            { var p=new Vector3D(x,y,z); cells.Add(new Region(p,p+Vector3D.One,300+x)); }
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(cells,1024,out order));

            var all=new List<Region>();var visible=new List<Region>();
            for(int i=0;i<80;i++)
            {
                var grid=MatrixD.CreateFromYawPitchRoll(i*.13,i*.03,i*.07);

                grid.Translation=new Vector3D(10000,-1000,4000);

                var localEye=new Vector3D(4+Math.Sin(i)*15,4,4+Math.Cos(i)*15);
                var worldEye=Vector3D.Transform(localEye,grid);
                var target=Vector3D.Transform(i%3==0?new Vector3D(50):new Vector3D(4),grid);
                var view=MatrixD.CreateLookAt(worldEye,target,grid.Up);

                var frustum=new BoundingFrustumD(grid*view*MatrixD.CreatePerspectiveFieldOfView(.7,1.8,.1,100));
                order.WriteNearToFar(localEye,all);
                var expected=all.FindAll(r=>frustum.Contains(new BoundingBoxD(r.Min,r.Max))!=ContainmentType.Disjoint);
                order.WriteVisibleNearToFar(localEye,visible,frustum);
                Assert.Equal(expected,visible);

                var worldFrustum=new BoundingFrustumD(view*MatrixD.CreatePerspectiveFieldOfView(.7,1.8,.1,100));
                foreach(var cell in visible)
                {

                    var lo=new Vector3D(double.PositiveInfinity);var hi=new Vector3D(double.NegativeInfinity);
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=Vector3D.Transform(new Vector3D((corner&1)==0?cell.Min.X:cell.Max.X,
                            (corner&2)==0?cell.Min.Y:cell.Max.Y,(corner&4)==0?cell.Min.Z:cell.Max.Z),grid);
                        lo=Vector3D.Min(lo,p);hi=Vector3D.Max(hi,p);
                    }
                    Assert.NotEqual(ContainmentType.Disjoint,worldFrustum.Contains(new BoundingBoxD(lo,hi)));
                }
            }
        }

        [Fact]

        public void InteriorOccupancyRemovesCoarseRoomAirButKeepsWallsAndDistantCoverage()
        {
            using(var coarse=new ThermalVisionRegionScan(20,8,1))
            using(var fine=new ThermalVisionRegionScan(1,4096,1))
            {
                coarse.Start(new List<IEnumerable<Region>> { new[] { new Region(new Vector3D(-10),new Vector3D(10),700) } });
                while(coarse.Running) coarse.Advance(128);
                fine.Start(new List<IEnumerable<Region>> { new[] {

                    new Region(new Vector3D(-3,-3,-3),new Vector3D(3,-2,3),300),

                    new Region(new Vector3D(-3,-2,-3),new Vector3D(-2,3,3),900) } });
                while(fine.Running) fine.Advance(128);
                ThermalVisionRegionPartition focused;
                Assert.True(ThermalVisionRegionPartition.TryReplace(ThermalVisionRegionPartition.FromScan(coarse,8192),fine,new Vector3D(-4),new Vector3D(4),8192,out focused));
                ThermalVisionRegionOrder order; Assert.True(ThermalVisionRegionOrder.TryBuild(focused,8192,out order));
                Region found;
                Assert.False(order.TryFind(Vector3D.Zero,out found));
                Assert.True(order.TryFind(new Vector3D(0,-2.5,0),out found)); Assert.Equal(300f,found.Kelvin);
                Assert.True(order.TryFind(new Vector3D(-2.5,0,0),out found)); Assert.Equal(900f,found.Kelvin);
                Assert.True(order.TryFind(new Vector3D(8,0,0),out found)); Assert.Equal(700f,found.Kelvin);
            }
        }

        [Fact]

        public void SnapshotPointLookupMatchesLinearScanIncludingBoundariesAndGaps()
        {

            var cells=new List<Region>();
            for(int x=-3;x<=3;x++) for(int y=-2;y<=2;y++)
                if(x!=0 || y!=0) cells.Add(new Region(new Vector3D(x,y,0),new Vector3D(x+1,y+1,1),300+10*x+y));
            ThermalVisionRegionOrder order; Assert.True(ThermalVisionRegionOrder.TryBuild(cells,256,out order));

            var sorted=new List<Region>(); order.WriteNearToFar(Vector3D.Zero,sorted);
            for(int x=-8;x<=10;x++) for(int y=-6;y<=8;y++) for(int z=-1;z<=3;z++)
            {

                var p=new Vector3D(x*.5,y*.5,z*.5); Region expected=default(Region); bool found=false;
                foreach(var cell in sorted)
                    if(p.X>=cell.Min.X && p.Y>=cell.Min.Y && p.Z>=cell.Min.Z && p.X<=cell.Max.X && p.Y<=cell.Max.Y && p.Z<=cell.Max.Z)
                    { expected=cell; found=true; break; }
                Region actual; Assert.Equal(found,order.TryFind(p,out actual));
                if(found) Assert.Equal(expected,actual);
            }
        }

        [Fact]

        public void AdjacencySkipsOnlyExactOpposingFaces()
        {
            var paired=ThermalVisionFacePlan.PairedFaces(new[] {

                new Region(Vector3D.Zero,Vector3D.One,300),

                new Region(new Vector3D(1,0,0),new Vector3D(2,1,1),900) });
            Assert.Equal(2,paired[0]); Assert.Equal(1,paired[1]);
            var partial=ThermalVisionFacePlan.PairedFaces(new[] {

                new Region(Vector3D.Zero,new Vector3D(2),300),

                new Region(new Vector3D(2,0,0),new Vector3D(3,1,1),900) });
            Assert.Equal(0,partial[0]); Assert.Equal(0,partial[1]);
        }

        [Fact]

        public void DenseViewportBudgetDoesNotStickWhenReturningToFourShips()
        {
            Assert.Equal(1400, ThermalVisionFleetBudget.ForViewport(68, 68, 4));
            Assert.Equal(68, ThermalVisionFleetBudget.ForViewport(68, 4, 4));
            Assert.Equal(68, ThermalVisionFleetBudget.ForViewport(68, 4, 3));
            Assert.Equal(68, ThermalVisionFleetBudget.ForViewport(68, 68, 0));
        }

        [Fact]

        public void SharedFacePlanPreservesHeatAndCoolingAlongIndependentRays()
        {

            var cells = new List<Region>();
            for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) for (int z = 0; z < 4; z++)
                cells.Add(new Region(new Vector3D(x,y,z), new Vector3D(x+1,y+1,z+1),
                    x == 2 && y == 2 && z == 2 ? 900 : x == 1 && y == 1 ? 250 : 500));
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(cells, 256, out order));

            var sorted = new List<Region>(); var plan = new ThermalVisionFacePlan(); var random = new Random(391);
            foreach (var eye in new[] {new Vector3D(-3,2,5), new Vector3D(6,5,-2), new Vector3D(1.3,1.2,1.1)})
            {
                order.WriteNearToFar(eye, sorted); plan.Build(sorted, eye);
                Assert.True(plan.Removed > 0);
                var paired=ThermalVisionFacePlan.PairedFaces(sorted);
                for (int sample = 0; sample < 1000; sample++)
                {

                    var point = new Vector3D(random.NextDouble()*6-1, random.NextDouble()*6-1, random.NextDouble()*6-1);
                    float expected = -1;
                    foreach (var cell in cells) if (Contains(cell, point)) expected = cell.Kelvin;
                    Assert.Equal(expected, FaceRay(sorted, null, eye, point));
                    Assert.Equal(expected, FaceRay(sorted, plan, eye, point));
                    Assert.Equal(expected, FaceRay(sorted, null, eye, point,paired));
                }
            }
            plan.Build(new[] {cells[0]}, Vector3D.Zero);
            Assert.Equal(0, plan.Removed);
        }

        [Fact]

        public void UniformThousandCellFieldNeedsOnlyItsOuterSixHundredFaces()
        {

            var cells = new List<Region>();
            for (int x = 0; x < 10; x++) for (int y = 0; y < 10; y++) for (int z = 0; z < 10; z++)
                cells.Add(new Region(new Vector3D(x,y,z), new Vector3D(x+1,y+1,z+1), 500));

            var plan = new ThermalVisionFacePlan(); plan.Build(cells, new Vector3D(-10));
            Assert.Equal(5400, plan.Removed);
            int skippedExits=0;
            foreach(int mask in ThermalVisionFacePlan.PairedFaces(cells))
                for(int axis=0;axis<3;axis++) if((mask & (1<<(axis*2+1)))!=0) skippedExits++;
            Assert.Equal(2700,skippedExits);
            plan.Build(new[] {new Region(Vector3D.Zero, new Vector3D(2), 500),

                new Region(new Vector3D(2,0,0),new Vector3D(3,1,1),500)}, new Vector3D(-10));
            Assert.Equal(0, plan.Removed);
        }


        private static float FaceRay(IList<Region> cells, ThermalVisionFacePlan plan, Vector3D eye, Vector3D surface, int[] paired=null)
        {
            float result = -1; Vector3D ray = surface-eye;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (Contains(cell, eye)) result = cell.Kelvin;
                for (int pass = 0; pass < 2; pass++)
                    for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
                    {

                        double plane = Axis(side == 0 ? cell.Min : cell.Max, axis);
                        bool entry = (Axis(eye,axis)-plane)*(side == 1 ? 1 : -1)>0;
                        if (entry != (pass == 0) || (plan != null && plan.Skip(i,axis,side == 1))) continue;
                        if(!entry && paired!=null && (paired[i] & (1<<(axis*2+side)))!=0) continue;

                        double direction = Axis(ray,axis); if (Math.Abs(direction)<1e-12) continue;
                        double t = (plane-Axis(eye,axis))/direction; if (t<=0 || t>=1) continue;
                        Vector3D hit = eye+ray*t; bool onFace = true;
                        for (int a = 0; a < 3; a++) if (a != axis)

                            onFace &= Axis(hit,a)>=Axis(cell.Min,a) && Axis(hit,a)<Axis(cell.Max,a);
                        if (onFace) result = entry ? cell.Kelvin : -1;
                    }
            }
            return result;
        }

        [Fact]

        public void CapacityFailuresConvergeWithoutDroppingGridReservations()
        {
            int capacity = 1400;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int next = ThermalVisionFleetBudget.BackOff(capacity, 3);
                Assert.InRange(next, 3, capacity);
                var allocations = ThermalVisionFleetBudget.Allocate(new[] {1.0, .1, .01}, next);
                foreach (int value in allocations) Assert.True(value >= 1);
                capacity = next;
            }
            Assert.Equal(3, capacity);
        }

        [Fact]

        public void FullScreenContextSurvivesAcquisitionAndEmptyViewportButYieldsToChat()
        {
            foreach (bool hasField in new[] {true, false, false, true})
                Assert.True(ThermalVisionViewPolicy.DrawContext(true, hasField));
            Assert.False(ThermalVisionViewPolicy.DrawContext(false, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, false, false, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, false, true, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, true, true, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, true, false, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, false, false, false));
            Assert.False(ThermalVisionViewPolicy.SuppressForUi(true, false, false, true));
            Assert.True(ThermalVisionViewPolicy.SuppressForUi(false, false, false, false));
        }

        [Fact]

        public void SmallGridReturnsUnusedFleetCapacity()
        {
            int[] budgets = ThermalVisionFleetBudget.Allocate(new[] {1.0, .1, .1}, new[] {20, 1400, 1400}, 1400);
            Assert.Equal(20, budgets[0]);
            Assert.Equal(1400, budgets[0] + budgets[1] + budgets[2]);
            Assert.True(budgets[1] >= 64 && budgets[2] >= 64);
            Assert.Equal(new[] {2, 3}, ThermalVisionFleetBudget.Allocate(new[] {1.0, 1.0}, new[] {2, 3}, 100));
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(240)]
        [InlineData(256)]
        [InlineData(512)]

        public void MixedDetailRestoresCoolNearBlocksWithoutLosingDistantCoverage(int budget)
        {

            var blocks = new List<Region>();
            for (int i = 0; i < 1000; i++)
                blocks.Add(new Region(new Vector3D(i, 0, 0), new Vector3D(i+1, 1, 1), i % 2 == 0 ? 300 : 800));

            var detail = new ThermalVisionBlockDetail(budget, new Vector3D(-1, .5, .5));
            foreach (var block in blocks) detail.Observe(block, true);

            var scan = new ThermalVisionRegionScan(1, detail.CoarseCapacity, 1, 4096);
            scan.Start(new List<IEnumerable<Region>> {blocks});
            while (scan.Running) scan.Advance(1024);
            Assert.Null(scan.Failure);
            var regions = detail.Build(scan, true);
            Assert.False(detail.AllExact);
            Assert.True(detail.RefinedBlocks > 0);
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(regions, budget, out order));
            for (int i = 0; i < blocks.Count; i++)
            {
                var point = (blocks[i].Min + blocks[i].Max) * .5;
                int owners = 0; float value = 0;
                foreach (var region in regions)
                    if (point.X >= region.Min.X && point.X < region.Max.X && point.Y >= region.Min.Y
                        && point.Y < region.Max.Y && point.Z >= region.Min.Z && point.Z < region.Max.Z)
                    { owners++; value = region.Kelvin; }
                Assert.Equal(1, owners);
                if (i < detail.RefinedBlocks) Assert.Equal(blocks[i].Kelvin, value);
                else Assert.InRange(value, blocks[i].Kelvin, 800);
            }
        }

        [Fact]

        public void FailedCoolOverwriteLeavesOldFieldIntact()
        {

            var field = new ThermalVisionRegionPartition(1);
            Assert.True(field.TryAdd(new Region(Vector3D.Zero, new Vector3D(10), 800)));
            Assert.False(field.TryOverwrite(new Region(new Vector3D(2), new Vector3D(3), 300)));
            Assert.Equal(1, field.Count);
            Assert.Equal(800, field[0].Kelvin);
            Assert.Equal(new Vector3D(10), field[0].Max);
        }

        [Fact]

        public void MeanCellsUseIntersectionVolumeAndRemainOrderIndependent()
        {
            var blocks = new List<Region> {

                new Region(Vector3D.Zero, new Vector3D(1, 1, 1), 300),

                new Region(new Vector3D(1, 0, 0), new Vector3D(4, 1, 1), 700)
            };
            for (int pass = 0; pass < 2; pass++)
            {
                using (var scan = new ThermalVisionRegionScan(2, 2, 1, average: true))
                {
                    scan.Start(new[] { (IEnumerable<Region>)blocks });
                    while (scan.Running) scan.Advance(1);
                    Assert.Null(scan.Failure);
                    Assert.Equal(2, scan.Count);
                    for (int i = 0; i < scan.Count; i++) Assert.Equal(scan[i].Min.X == 0 ? 500 : 700, scan[i].Kelvin);
                }
                blocks.Reverse();
            }
            Assert.Throws<ArgumentException>(() => new ThermalVisionRegionScan(1, 8, 1, 2, true));
        }

        [Fact]

        public void SmallestGridBudgetCoversBlocksAcrossLocalOrigin()
        {

            var block = new Region(new Vector3D(-.5), new Vector3D(.5), 600);
            using (var scan = new ThermalVisionRegionScan(1, 1, 1, 1024, origin: block.Min))
            {
                scan.Start(new[] { (IEnumerable<Region>)new[] { block } });
                while (scan.Running) scan.Advance(1);
                Assert.Null(scan.Failure);
                Assert.Equal(1, scan.Count);
                Assert.Equal(block.Min, scan[0].Min);
                Assert.Equal(block.Max, scan[0].Max);
                Assert.Equal(600, scan[0].Kelvin);
            }
        }

        [Theory]
        [InlineData(15000, 5000, 15000)]
        [InlineData(3000, 15000, 3000)]
        [InlineData(50000, 15000, 50000)]
        [InlineData(double.NaN, 20000, 20000)]
        [InlineData(double.PositiveInfinity, 20000, 20000)]
        [InlineData(0, 0, 15000)]

        public void FleetReachFollowsActiveCameraAndOnlyFallsBackWhenInvalid(double camera, double session, double expected)
        {
            double far = ThermalVisionViewPolicy.FarDistance(camera, session, .1);
            Assert.Equal(expected, far);
            Assert.InRange(ThermalVisionViewPolicy.BackdropDistance(far), far - .1, far);
            Assert.True(ThermalVisionViewPolicy.BackdropDistance(far) < far);
        }

        [Fact]

        public void FleetBudgetReservesEveryGridBeforeNearbyDetail()
        {
            int[] budgets = ThermalVisionFleetBudget.Allocate(new[] {400.0, 1.0, 1.0}, 1400);
            Assert.InRange(budgets[0], 1124, 1272);
            Assert.True(budgets[1] >= 64 && budgets[2] >= 64);
            Assert.Equal(1400, budgets[0]+budgets[1]+budgets[2]);
            var many = ThermalVisionFleetBudget.Allocate(new double[1000], 1400);
            int total = 0;
            foreach (int budget in many) { Assert.True(budget >= 1); total += budget; }
            Assert.Equal(1400, total);
            Assert.Throws<ArgumentException>(() => ThermalVisionFleetBudget.Allocate(new double[4], 3));
        }

        [Fact]

        public void ThreeGridFieldKeepsNearBlockTemperaturesAndBothDistantGrids()
        {
            int[] budgets = ThermalVisionFleetBudget.Allocate(new[] {400.0, 1.0, 1.0}, 1400);

            var field = new ThermalVisionRegionPartition(1536);

            var checks = new List<Region>();
            for (int grid = 0; grid < 3; grid++)
            {

                var input = new List<Region>();
                for (int x = 0; x < 10; x++) for (int y = 0; y < 10; y++) for (int z = 0; z < 10; z++)
                {

                    Vector3D lo = new Vector3D(x*2.5, y*2.5, z*2.5);
                    input.Add(new Region(lo, lo+new Vector3D(2.5), 300+x*30));
                }
                var cells = input;
                if (grid > 0)
                {

                    var scan = new ThermalVisionRegionScan(2.5, budgets[grid]/2, 1, 5120);
                    scan.Start(new List<IEnumerable<Region>> {input});
                    while (scan.Running) scan.Advance(128);
                    Assert.Null(scan.Failure);

                    cells = new List<Region>();
                    for (int i = 0; i < scan.Count; i++) cells.Add(scan[i]);
                    Assert.True(cells.Count > 1);
                }
                foreach (var cell in cells)
                {

                    var shifted = new Region(cell.Min+new Vector3D(grid*100,0,0), cell.Max+new Vector3D(grid*100,0,0), cell.Kelvin);
                    Assert.True(field.TryAdd(shifted)); checks.Add(shifted);
                }
            }
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(field, 1536, out order));

            var output = new List<Region>();
            order.WriteNearToFar(new Vector3D(-20), output);
            foreach (var cell in checks)
            {
                var owners = output.FindAll(r => Contains(r, (cell.Min+cell.Max)*.5));
                Assert.Single(owners); Assert.Equal(cell.Kelvin, owners[0].Kelvin);
            }
            Assert.True(order.LeafCount*14+2 < 32768);
        }

        [Fact]

        public void ExactBlockOrderingPreservesTemperaturesAndRejectsPartialCapacity()
        {

            var blocks = new List<Region>();
            for (int x = 0; x < 40; x++)
                blocks.Add(new Region(new Vector3D(x * 2, 0, 0), new Vector3D(x * 2 + 2, 3, 1), 300 + x * 7));
            ThermalVisionRegionOrder order;
            Assert.False(ThermalVisionRegionOrder.TryBuild(blocks, 10, out order));
            Assert.Null(order);
            Assert.True(ThermalVisionRegionOrder.TryBuild(blocks, 1536, out order));

            var output = new List<Region>();
            order.WriteNearToFar(new Vector3D(-10, 4, 9), output);
            foreach (var block in blocks)
            {
                var owners = output.FindAll(r => Contains(r, (block.Min + block.Max) * .5));
                Assert.Single(owners);
                Assert.Equal(block.Kelvin, owners[0].Kelvin);
            }
            Assert.InRange(order.LeafCount * 14 + 2, 1, 32767);
        }

        [Fact]

        public void SurfaceAnchoredDetailReachesShipOutsideCameraFineBox()
        {
            var eye = Vector3D.Zero;

            var input = new[] { new Region(new Vector3D(30, 1, 1), new Vector3D(31, 2, 2), 300),

                new Region(new Vector3D(35, 1, 1), new Vector3D(36, 2, 2), 700) };

            var coarse = new ThermalVisionRegionScan(80, 384, 1);
            coarse.Start(new List<IEnumerable<Region>> { input });
            while (coarse.Running) coarse.Advance(128);
            using (var lod = new ThermalVisionRegionLod(eye, input[0].Min))
            {
                foreach (var sample in input) lod.Observe(sample);
                while (lod.Running) lod.Advance(128);
                ThermalVisionRegionOrder order;
                Assert.True(lod.TryBuild(coarse, 1536, out order));
                Assert.True(lod.Bands[2].Applied);
                Assert.True(lod.Bands[2].Scan.Count > 0);

                var regions = new List<Region>();
                order.WriteNearToFar(eye, regions);
                foreach (var sample in input)
                {
                    var owners = regions.FindAll(r => Contains(r, (sample.Min + sample.Max) * .5));
                    Assert.Single(owners);
                    Assert.Equal(sample.Kelvin, owners[0].Kelvin);
                }
            }
        }

        [Theory]
        [InlineData(20, 2.5)]
        [InlineData(60, 5)]
        [InlineData(120, 10)]
        [InlineData(240, 20)]
        [InlineData(1000, 40)]

        public void SurfaceDetailScalesWithApproach(double distance, double expectedCell)
        {
            using (var lod = new ThermalVisionRegionLod(Vector3D.Zero, new Vector3D(distance, 0, 0)))
                Assert.Equal(expectedCell, lod.Bands[2].Scan.CellSize);
        }

        [Fact]

        public void DistanceLodRefinesHotAndColdNeighboursOnApproach()
        {

            var input = new[] { new Region(new Vector3D(1), new Vector3D(2), 300),

                new Region(new Vector3D(6, 1, 1), new Vector3D(7, 2, 2), 700) };
            foreach (double distance in new[] { 100.0, 30.0, 3.0 })
            {

                var coarse = new ThermalVisionRegionScan(80, 384, 1);
                coarse.Start(new List<IEnumerable<Region>> { input });
                while (coarse.Running) coarse.Advance(128);
                using (var lod = new ThermalVisionRegionLod(new Vector3D(distance, 0, 0)))
                {
                    foreach (Region sample in input) lod.Observe(sample);
                    while (lod.Running) lod.Advance(128);
                    ThermalVisionRegionOrder order;
                    Assert.True(lod.TryBuild(coarse, 1536, out order));

                    var output = new List<Region>();
                    order.WriteNearToFar(new Vector3D(distance, 0, 0), output);
                    foreach (var target in new[] { new Vector3D(1.5), new Vector3D(6.5, 1.5, 1.5) })
                    {
                        var owners = output.FindAll(r => Contains(r, target));
                        Assert.Single(owners);
                        Assert.Equal(distance == 3 && target.X < 2 ? 300f : 700f, owners[0].Kelvin);
                    }
                    Assert.All(lod.Bands, b => Assert.True(b.Applied));
                }
            }
        }

        [Fact]

        public void DenseDistanceLodCoarsensWithinEachBudgetAndPreservesCoverage()
        {

            var input = new Region(new Vector3D(-500), new Vector3D(500), 420);

            var coarse = new ThermalVisionRegionScan(40, 384, 1, 5120);
            coarse.Start(new List<IEnumerable<Region>> { new[] { input } });
            while (coarse.Running) coarse.Advance(4096);
            using (var lod = new ThermalVisionRegionLod(new Vector3D(-1)))
            {
                lod.Observe(input);
                while (lod.Running) lod.Advance(4096);
                ThermalVisionRegionOrder order;
                Assert.True(lod.TryBuild(coarse, 1536, out order));
                Assert.True(lod.Bands[2].Applied);
                Assert.InRange(order.LeafCount, 1, 1536);

                var output = new List<Region>();
                order.WriteNearToFar(new Vector3D(-1), output);
                for (int z = -450; z < 500; z += 37)
                {

                    var point = new Vector3D(z + .123, z / 2.0 + .321, -1.234);
                    var owners = output.FindAll(r => Contains(r, point));
                    Assert.Single(owners);
                    Assert.Equal(420f, owners[0].Kelvin);
                }
            }
        }

        [Fact]

        public void FineFocusReplacesCoarseHeatIncludingCooledTargets()
        {

            var coarse = new ThermalVisionRegionScan(40, 8, 1);
            coarse.Start(new List<IEnumerable<Region>> { new[] { new Region(Vector3D.Zero, new Vector3D(40), 700) } });
            while (coarse.Running) coarse.Advance(128);

            var fine = new ThermalVisionRegionScan(2.5, 512, 1);
            fine.Start(new List<IEnumerable<Region>> { new[] {

                new Region(new Vector3D(10), new Vector3D(12.5), 300),

                new Region(new Vector3D(15), new Vector3D(17.5), 600) } });
            while (fine.Running) fine.Advance(128);
            ThermalVisionRegionPartition focused;
            Assert.True(ThermalVisionRegionPartition.TryFocus(coarse, fine, new Vector3D(10), new Vector3D(30), 1536, out focused));
            foreach (var pair in new[] { Tuple.Create(new Vector3D(11), 300f), Tuple.Create(new Vector3D(16), 600f),
                Tuple.Create(new Vector3D(5), 700f), Tuple.Create(new Vector3D(25), -1f) })
            {
                float value = -1;
                int owners = 0;
                for (int i = 0; i < focused.Count; i++) if (Contains(focused[i], pair.Item1)) { value = focused[i].Kelvin; owners++; }
                Assert.InRange(owners, 0, 1);
                Assert.Equal(pair.Item2, value);
            }
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(focused, 1536, out order));
        }

        [Fact]

        public void FocusCapacityFailureDoesNotPublishPartialFieldOrAlterSources()
        {

            var coarse = new ThermalVisionRegionScan(40, 8, 1);
            coarse.Start(new List<IEnumerable<Region>> { new[] { new Region(new Vector3D(-40), Vector3D.Zero, 700) } });
            while (coarse.Running) coarse.Advance(128);

            var fine = new ThermalVisionRegionScan(2.5, 512, 1);
            fine.Start(new List<IEnumerable<Region>> { new[] { new Region(new Vector3D(-20), new Vector3D(-17.5), 300) } });
            while (fine.Running) fine.Advance(128);
            ThermalVisionRegionPartition focused;
            Assert.False(ThermalVisionRegionPartition.TryFocus(coarse, fine, new Vector3D(-30), new Vector3D(-10), 2, out focused));
            Assert.Null(focused);
            Assert.Equal(1, coarse.Count);
            Assert.Equal(700f, coarse[0].Kelvin);
            Assert.True(ThermalVisionRegionPartition.TryFocus(coarse, fine, new Vector3D(-30), new Vector3D(-10), 32, out focused));
            Assert.Equal(7, focused.Count);
        }

        [Fact]

        public void DenseFocusFitsBesideCoarseContextWithoutLosingEitherField()
        {

            var background = new List<Region>();
            for (int z = 0; z < 8; z++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {

                Vector3D lo = new Vector3D(x * 40, y * 40, z * 40);
                background.Add(new Region(lo, lo + new Vector3D(40), 700));
            }

            var coarse = new ThermalVisionRegionScan(40, 896, 1);
            coarse.Start(new List<IEnumerable<Region>> { background });
            while (coarse.Running) coarse.Advance(4096);

            var fine = new ThermalVisionRegionScan(2.5, 512, 1);
            fine.Start(new List<IEnumerable<Region>> { new[] { new Region(new Vector3D(110), new Vector3D(130), 300) } });
            while (fine.Running) fine.Advance(4096);
            ThermalVisionRegionPartition focused;
            Assert.True(ThermalVisionRegionPartition.TryFocus(coarse, fine, new Vector3D(110), new Vector3D(130), 1536, out focused));
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(focused, 1536, out order));
        }

        [Fact]

        public void CompletedScanConnectsToSpatialOrderingWithinNativeBillboardCeiling()
        {

            var scan = new ThermalVisionRegionScan(5, ThermalVisionScenePolicy.RegionCellLimit, 2);
            Assert.True(scan.Start(new List<IEnumerable<Region>> { new[] {

                new Region(new Vector3D(-1), new Vector3D(1), 600),

                new Region(new Vector3D(10), new Vector3D(11), 300) } }));
            while (scan.Running) scan.Advance(32);
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(scan, ThermalVisionScenePolicy.RegionCellLimit, out order));
            Assert.Equal(scan.Count, order.LeafCount);

            var ordered = new List<Region>();
            order.WriteNearToFar(new Vector3D(-20), ordered);
            Assert.Equal(9, ordered.Count);
            Assert.InRange(ThermalVisionScenePolicy.RegionWorstCaseBillboards, 1, 32767);
        }

        [Fact]

        public void AlignedSceneDoesNotMultiplyRegionCountDuringOrdering()
        {

            var field = new ThermalVisionRegionPartition(512);
            for (int z = 0; z < 8; z++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {

                var lo = new Vector3D(x * 3, y * 3, z * 3);
                Assert.True(field.TryAdd(new Region(lo, lo + new Vector3D(2), 300 + x * 20)));
            }
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(field, 512, out order));
            Assert.Equal(512, order.LeafCount);
            Assert.Equal(1023, order.NodeCount);
        }

        [Fact]

        public void SpatialTraversalAgreesWithIndependentRayIntervalsAndFieldSamples()
        {

            var field = new ThermalVisionRegionPartition(256);
            Assert.True(field.TryAdd(new Region(new Vector3D(-4, -3, -2), new Vector3D(4, 3, 2), 300)));
            Assert.True(field.TryAdd(new Region(new Vector3D(-3, -1, -4), new Vector3D(1, 4, 4), 600)));
            Assert.True(field.TryAdd(new Region(new Vector3D(0, -4, -3), new Vector3D(3, 2, 3), 450)));
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(field, 4096, out order));

            var sorted = new List<Region>();

            var random = new Random(81243);
            foreach (Vector3D eye in new[] { new Vector3D(-8, 2, 3), Vector3D.Zero, new Vector3D(8, -4, -6) })
            {
                order.WriteNearToFar(eye, sorted);
                Assert.Equal(order.LeafCount, sorted.Count);
                Assert.InRange(order.NodeCount, 1, order.LeafCount * 2 - 1);
                for (int ray = 0; ray < 400; ray++)
                {

                    var direction = new Vector3D(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);
                    double previousExit = 0;
                    foreach (Region region in sorted)
                    {
                        double enter, exit;
                        if (!Intersect(region, eye, direction, out enter, out exit)) continue;
                        Assert.True(enter >= previousExit - 1e-8, "Submission order reverses along a camera ray");
                        previousExit = exit;
                    }
                    for (int sample = 0; sample < 16; sample++)
                    {
                        Vector3D point = eye + direction * (sample + .137);
                        float expected = -1, actual = -1;
                        for (int i = 0; i < field.Count; i++) if (Contains(field[i], point)) expected = field[i].Kelvin;
                        int owners = 0;
                        foreach (Region region in sorted)
                            if (Contains(region, point)) { actual = region.Kelvin; owners++; }
                        Assert.InRange(owners, 0, 1);
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }

        [Fact]

        public void FailedPreparationReturnsNoPartialTreeAndEmptyFieldIsValid()
        {

            var field = new ThermalVisionRegionPartition(8);
            Assert.True(field.TryAdd(new Region(new Vector3D(-2), new Vector3D(-1), 300)));
            Assert.True(field.TryAdd(new Region(new Vector3D(1), new Vector3D(2), 400)));
            ThermalVisionRegionOrder order;
            Assert.False(ThermalVisionRegionOrder.TryBuild(field, 1, out order));
            Assert.Null(order);
            Assert.True(ThermalVisionRegionOrder.TryBuild(new ThermalVisionRegionPartition(1), 1, out order));
            var result = new List<Region> { field[0] };
            order.WriteNearToFar(Vector3D.Zero, result);
            Assert.Empty(result);
        }


        private static bool Contains(Region r, Vector3D p)
        { return p.X >= r.Min.X && p.X < r.Max.X && p.Y >= r.Min.Y && p.Y < r.Max.Y && p.Z >= r.Min.Z && p.Z < r.Max.Z; }

        private static double Axis(Vector3D v, int axis) { return axis == 0 ? v.X : axis == 1 ? v.Y : v.Z; }

        private static bool Intersect(Region region, Vector3D eye, Vector3D ray, out double enter, out double exit)
        {
            enter = 0; exit = double.PositiveInfinity;
            for (int axis = 0; axis < 3; axis++)
            {

                double d = Axis(ray, axis), origin = Axis(eye, axis);

                double lo = Axis(region.Min, axis), hi = Axis(region.Max, axis);
                if (Math.Abs(d) < 1e-12) { if (origin < lo || origin >= hi) return false; continue; }
                double a = (lo - origin) / d, b = (hi - origin) / d;
                enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
                if (exit <= enter) return false;
            }
            return true;
        }
    }
}

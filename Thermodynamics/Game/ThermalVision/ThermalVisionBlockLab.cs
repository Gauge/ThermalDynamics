using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private sealed class BlockFleetJob
        {
            public ThermalGrid Grid;
            public MatrixD Matrix;
            public int Budget, BlockCount;
            public double Weight;
            public ThermalVisionRegionScan Scan, Mean;
            public ThermalVisionBlockDetail Detail;
            public List<Region> SmoothSamples;
            public ThermalVisionBlockField TemperatureField;
            public ThermalVisionTemperatureData Temperatures;
            public double SurfaceSpacing;
            public int Refined;
            public bool AllExact;
        }
        private static bool blockLabMode;
        private static MatrixD regionRenderMatrix = MatrixD.Identity;
        private static double blockLabNext;

        private static readonly List<BlockFleetJob> blockFleet = new List<BlockFleetJob>();
        private static bool blockFleetScanning;
        private static IEnumerator<bool> blockFleetComposition;
        private static int blockFleetCapacity = 1400;
        private static int blockFleetPressureGridCount;
        private static int blockFleetCursor;
        private static double blockFleetStarted;
        private static MatrixD blockFleetBasis = MatrixD.Identity;


        private static void StopBlockFleet()
        {
            foreach (var job in blockFleet) { if (job.Scan != null) job.Scan.Dispose(); if (job.Mean != null) job.Mean.Dispose(); }
            if (blockFleetComposition != null) blockFleetComposition.Dispose();
            blockFleetComposition = null;
            blockFleet.Clear(); blockFleetScanning = false; blockLabNext = 0;
        }


        private static IEnumerable<Region> FleetSamples(BlockFleetJob job, bool collectDetail = true)
        {
            double size = job.Grid.Grid.GridSize;
            foreach (ThermalBlock block in job.Grid.Blocks)
            {
                if (block == null || block.Node == null) continue;
                float t;
                bool usable=collectDetail
                    ? ThermalVisionTemperatureData.TryDisplay(block.Node.Temperature,out t)
                    : job.Temperatures.Add(block.Node.Temperature,out t);
                if(!usable) continue;

                var sample = new Region(((Vector3D)block.Block.Min - new Vector3D(.5)) * size,
                    ((Vector3D)block.Block.Max + new Vector3D(.5)) * size, t);
                if (collectDetail)
                {

                    var visibleBounds = FleetTransform(sample, job.Matrix);

                    var worldBox = new BoundingBoxD(visibleBounds.Min, visibleBounds.Max);
                    job.Detail.Observe(sample, MyAPIGateway.Session.Camera.IsInFrustum(ref worldBox));
                }
                if (!collectDetail && smoothFleet) job.TemperatureField.Add(sample);
                yield return sample;
            }
        }


        private static void StartBlockFleet(double now)
        {
            StopBlockFleet();
            var camera = MyAPIGateway.Session.Camera;
            Vector3D eye = camera.WorldMatrix.Translation;
            foreach (ThermalGrid thermal in ThermalGrid.LiveGrids)
            {
                var grid = thermal.Grid;
                if (grid == null || grid.MarkedForClose || thermal.Simulation == null || thermal.BlockCount == 0) continue;
                var bounds = grid.PositionComp.WorldAABB;
                if (!camera.IsInFrustum(ref bounds)) continue;
                double distance = Vector3D.Distance(eye, Vector3D.Max(bounds.Min, Vector3D.Min(eye, bounds.Max)));
                double radius = bounds.HalfExtents.Length();
                blockFleet.Add(new BlockFleetJob { Grid = thermal, Matrix = grid.WorldMatrix, BlockCount = thermal.BlockCount,
                    Weight = Math.Min(1, radius * radius / Math.Max(25, distance * distance)) });
            }
            int recovered = ThermalVisionFleetBudget.ForViewport(blockFleetCapacity, blockFleetPressureGridCount, blockFleet.Count);
            if (recovered != blockFleetCapacity)
                RecordEvent("fleet viewport recovery: grids=" + blockFleetPressureGridCount + " -> " + blockFleet.Count
                    + " budget=" + blockFleetCapacity + " -> " + recovered);
            blockFleetCapacity = recovered;
            if (recovered == 1400) blockFleetPressureGridCount = 0;
            if (blockFleet.Count == 0 || blockFleet.Count > 1024)
            { regionOrder = null; regionStatus = blockFleet.Count == 0 ? "NO MEASURED GRIDS IN VIEW" : "VIEWPORT EXCEEDS CAPACITY"; blockLabNext = now + .5; return; }
            blockFleet.Sort((a,b) => {
                int compared = b.Weight.CompareTo(a.Weight);
                return compared != 0 ? compared : a.Grid.Grid.EntityId.CompareTo(b.Grid.Grid.EntityId);
            });
            blockFleetBasis = blockFleet[0].Matrix;
            var weights = new double[blockFleet.Count];
            var demands = new int[blockFleet.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = blockFleet[i].Weight;
                demands[i] = (int)Math.Min(1400L, Math.Max(1L, blockFleet[i].BlockCount) * 2);
            }
            int[] budgets = ThermalVisionFleetBudget.Allocate(weights, demands, Math.Max(blockFleet.Count, blockFleetCapacity));
            for (int i = 0; i < blockFleet.Count; i++)
            {
                var job = blockFleet[i]; job.Budget = budgets[i];

                job.Detail = new ThermalVisionBlockDetail(job.Budget, Vector3D.Transform(eye, MatrixD.Invert(job.Matrix)));

                job.Scan = new ThermalVisionRegionScan(job.Grid.Grid.GridSize, job.Detail.CoarseCapacity, 1, 10485760,
                    origin: ((Vector3D)job.Grid.Grid.Min - new Vector3D(.5)) * job.Grid.Grid.GridSize);
                job.Scan.Start(new List<IEnumerable<Region>> { FleetSamples(job) });
            }
            blockFleetStarted = now; blockFleetCursor = 0; blockFleetScanning = true;
        }


        private static bool FleetSourceValid(BlockFleetJob job)
        {
            var grid = job.Grid.Grid;
            if (grid == null || grid.MarkedForClose || job.Grid.BlockCount != job.BlockCount) return false;
            return true;
        }


        private static Region FleetTransform(Region region, MatrixD matrix)
        {
            Vector3D centre = Vector3D.Transform((region.Min+region.Max)*.5, matrix);
            Vector3D half = (region.Max-region.Min)*.5;
            Vector3D extent = Vector3D.Abs(matrix.Right)*half.X + Vector3D.Abs(matrix.Up)*half.Y + Vector3D.Abs(matrix.Backward)*half.Z;
            return new Region(centre-extent, centre+extent, region.Kelvin);
        }


        private static void UpdateBlockField(double now)
        {
            if (regionOrder != null && regionPublishedSources.Count == 1)
            {
                var source = regionPublishedSources[0];
                var grid = source.Grid.Grid;
                if (grid != null && !grid.MarkedForClose && source.Blocks == source.Grid.BlockCount)
                { regionRenderMatrix = grid.WorldMatrix; source.Matrix = grid.WorldMatrix; }
            }
            if (regionOrder != null && (now-regionPublished > 8 || !RegionsStillValid(regionPublishedSources)))
                regionOrder = null;
            if (!blockFleetScanning && now >= blockLabNext) StartBlockFleet(now);
            if (!blockFleetScanning) return;
            foreach (var job in blockFleet)
                if (!FleetSourceValid(job) || now-blockFleetStarted > 8)

                { StopBlockFleet(); regionOrder = null; regionStatus = "FLEET REFRESH / scene moved"; blockLabNext = now + .1; return; }
            int work = 0, idle = 0;
            while (work < 32768 && ProbeClock.Elapsed.TotalMilliseconds < 2 && idle < blockFleet.Count)
            {
                var job = blockFleet[blockFleetCursor]; blockFleetCursor = (blockFleetCursor+1)%blockFleet.Count;
                if (job.Scan.Running) { work += job.Scan.Advance(128); idle = 0; }

                else if (job.Scan.Failure == null && job.Mean == null)
                {
                    double distance = Vector3D.Distance(MyAPIGateway.Session.Camera.WorldMatrix.Translation,
                        Vector3D.Max(job.Grid.Grid.PositionComp.WorldAABB.Min, Vector3D.Min(MyAPIGateway.Session.Camera.WorldMatrix.Translation, job.Grid.Grid.PositionComp.WorldAABB.Max)));
                    double focalPixels = Math.Max(1, MyAPIGateway.Session.Camera.ViewportSize.Y * .5 * Math.Abs(MyAPIGateway.Session.Camera.ProjectionMatrix.M22));
                    job.SurfaceSpacing = Math.Max(job.Grid.Grid.GridSize, distance * 4 / focalPixels);

                    job.TemperatureField = new ThermalVisionBlockField(job.Grid.Grid.GridSize, job.SurfaceSpacing * .6);

                    job.Mean = new ThermalVisionRegionScan(job.Scan.CellSize, job.Detail.CoarseCapacity, 1, average: true, origin: job.Scan.Origin);
                    job.Mean.Start(new List<IEnumerable<Region>> { FleetSamples(job, false) }); idle = 0;
                }

                else if (job.Mean != null && job.Mean.Running) { work += job.Mean.Advance(128); idle = 0; }
                else idle++;
            }
            foreach (var job in blockFleet) if (job.Scan.Running || (job.Scan.Failure == null && (job.Mean == null || job.Mean.Running))) return;
            foreach (var job in blockFleet) if (job.Scan.Failure != null || job.Mean.Failure != null)
            { regionOrder = null; regionStatus = "FLEET SCAN FAILED / " + (job.Scan.Failure ?? job.Mean.Failure); StopBlockFleet(); blockLabNext = now+.5; return; }
            if (blockFleetComposition == null) blockFleetComposition = ComposeBlockFleet().GetEnumerator();
            while (ProbeClock.Elapsed.TotalMilliseconds < 2)
                if (!blockFleetComposition.MoveNext())
                {
                    blockFleetComposition.Dispose(); blockFleetComposition = null; break;
                }
        }


        private static IEnumerable<bool> ComposeBlockFleet()
        {
            foreach (var job in blockFleet) job.Matrix = job.Grid.Grid.WorldMatrix;
            blockFleetBasis = blockFleet[0].Matrix;
            MatrixD inverse = MatrixD.Invert(blockFleetBasis);
            ThermalVisionRegionOrder ordered = null;
            bool built = false;
            int exactCount = 0;
            for (int pass = 0; pass < 2 && !built; pass++)
            {
                int leafLimit = 1536;

                var field = new ThermalVisionRegionPartition(leafLimit);
                bool valid = true; exactCount = 0;
                foreach (var job in blockFleet)
                {
                    var regions = job.Detail.Build(job.Mean, pass == 0);

                    job.SmoothSamples = new List<Region>(regions);
                    yield return true;
                    job.AllExact = job.Detail.AllExact; job.Refined = job.Detail.RefinedBlocks;
                    if (job.AllExact) exactCount++;
                    MatrixD transform = job == blockFleet[0] ? MatrixD.Identity : job.Matrix * inverse;
                    foreach (Region region in regions)
                    {
                        bool added = field.TryAdd(FleetTransform(region, transform));
                        yield return true;
                        if (!added) { valid = false; break; }
                    }
                    if (!valid) break;
                }
                built = valid && ThermalVisionRegionOrder.TryBuild(field, leafLimit, out ordered);
                yield return true;
            }
            double now = RegionTime.Elapsed.TotalSeconds;
            blockLabNext = now + .3;
            if (!built)
            {
                blockFleetScanning = false;
                int previous = blockFleetCapacity;
                blockFleetPressureGridCount = blockFleet.Count;
                blockFleetCapacity = ThermalVisionFleetBudget.BackOff(blockFleetCapacity, blockFleet.Count);
                regionStatus = "REDUCING DETAIL / " + blockFleetCapacity + " source budget";
                RecordEvent("fleet capacity backoff: " + previous + " -> " + blockFleetCapacity);
                blockLabNext = now + (previous == blockFleetCapacity ? 1 : .1);
                yield break;
            }

            var pendingCorners = new Dictionary<Region,ThermalVisionSurfaceField>();
            int unmatchedCells=0, preparedTriangles=0;
            if (smoothFleet)
            {

                var cells = new List<Region>();
                ordered.WriteNearToFar(Vector3D.Zero,cells);
                foreach(var cell in cells)
                {
                    int owner=-1;
                    Vector3D midpoint=(cell.Min+cell.Max)*.5;
                    for(int j=0;j<blockFleet.Count && owner<0;j++)
                    {
                        MatrixD transform=blockFleet[j].Matrix*inverse;
                        foreach(var sample in blockFleet[j].SmoothSamples)
                        {
                            var bound=FleetTransform(sample,transform);
                            if(sample.Kelvin==cell.Kelvin && midpoint.X>=bound.Min.X && midpoint.Y>=bound.Min.Y && midpoint.Z>=bound.Min.Z
                                && midpoint.X<=bound.Max.X && midpoint.Y<=bound.Max.Y && midpoint.Z<=bound.Max.Z){owner=j;break;}
                        }
                    }
                    if(owner<0) unmatchedCells++;

                    var temperatures=new ThermalVisionSurfaceField(cell, owner<0 ? double.MaxValue : blockFleet[owner].SurfaceSpacing);
                    MatrixD toGrid=owner<0?MatrixD.Identity:blockFleetBasis*MatrixD.Invert(blockFleet[owner].Matrix);
                    for(int z=0;z<=temperatures.Steps.Z;z++) for(int y=0;y<=temperatures.Steps.Y;y++) for(int x=0;x<=temperatures.Steps.X;x++)
                    {
                        temperatures.Values[temperatures.Index(x,y,z)]=owner<0?cell.Kelvin:
                            blockFleet[owner].TemperatureField.Sample(Vector3D.Transform(temperatures.Point(x,y,z),toGrid),cell.Kelvin);
                        yield return true;
                    }
                    temperatures.BuildFaces(20);
                    for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++) preparedTriangles+=2*temperatures.Face(axis,side==1).Count;
                    pendingCorners.Add(cell,temperatures);
                    yield return true;
                }
            }
            now = RegionTime.Elapsed.TotalSeconds;
            blockFleetScanning = false; blockLabNext = now + .3;

            var sources = new List<RegionGrid>();
            foreach (var job in blockFleet) sources.Add(new RegionGrid { Grid = job.Grid, Matrix = job.Matrix,
                Blocks = job.BlockCount, Radius = job.Grid.Grid.PositionComp.WorldAABB.HalfExtents.Length(), DetailCellSize = job.Grid.Grid.GridSize });
            if (sources.Count == 1) { blockFleetBasis = sources[0].Grid.Grid.WorldMatrix; sources[0].Matrix = blockFleetBasis; }
            else if (!RegionsStillValid(sources))
            { regionStatus = "REFRESHING MOVING FLEET"; blockLabNext = now + .1; yield break; }
            smoothCorners.Clear(); foreach(var entry in pendingCorners) smoothCorners.Add(entry.Key,entry.Value);
            regionRenderMatrix = blockFleetBasis; regionOrder = ordered; regionPublished = now;
            regionPublishedSources.Clear();
            regionPublishedSources.AddRange(sources);
            regionPublishedDetail = blockFleet.Count + " GRIDS / " + exactCount + " exact / " + (blockFleet.Count-exactCount) + " mixed/group";
            regionStatus = smoothFleet ? "VIEWPORT GRADIENT / uncapped submission stress test" : "VIEWPORT FLEET / bounded detail";
            RecordEvent("block fleet published: grids=" + blockFleet.Count + " exact=" + exactCount
                + " fragments=" + ordered.LeafCount + " surface-field=" + (smoothFleet ? "original-blocks" : "region-means")
                + " prepared-face-triangles=" + preparedTriangles + " unmatched-field-cells=" + unmatchedCells
                + " submission-cap=" + (smoothFleet ? "none-stress-test" : "none")
                + " source-budget=" + blockFleetCapacity + " build-age-ms=" + ((now-blockFleetStarted)*1000)

                + " view-distance-m=" + BlockFleetViewDistance(), false);
            foreach (var job in blockFleet) RecordEvent("block fleet grid=" + job.Grid.Grid.EntityId + " blocks=" + job.BlockCount
                + " budget=" + job.Budget + " exact-blocks=" + job.Refined + " all-exact=" + job.AllExact + " coarse-cells=" + job.Scan.Count + " coarse-m=" + job.Scan.CellSize + " thermal-samples=" + (job.TemperatureField == null ? 0 : job.TemperatureField.Count) + " field-queries=" + (job.TemperatureField == null ? 0 : job.TemperatureField.Queries) + " field-fallbacks=" + (job.TemperatureField == null ? 0 : job.TemperatureField.Fallbacks) + " surface-spacing-m=" + job.SurfaceSpacing, false);
        }


        private static double BlockFleetViewDistance()
        {
            var session = MyAPIGateway.Session;
            return ThermalVisionViewPolicy.FarDistance(session.Camera.FarPlaneDistance,
                session.SessionSettings == null ? 15000 : session.SessionSettings.ViewDistance,
                session.Camera.NearPlaneDistance);
        }
    }
}

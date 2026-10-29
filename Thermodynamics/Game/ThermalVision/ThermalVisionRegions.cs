using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRageMath;
using VRage.Game;
using VRageRender;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private sealed class RegionGrid
        {
            public ThermalGrid Grid;
            public MatrixD Matrix;
            public int Blocks;
            public double Radius;
            public double DetailCellSize;
        }
        private static bool regionMode;
        private static double regionCellSize = 5;
        private static ThermalVisionRegionScan regionScan;
        private static ThermalVisionRegionLod regionLod;
        private static bool regionLodPending;
        private static bool regionHasSurface, regionCandidateSurface;
        private static Vector3D regionSurface, regionSurfaceEye, regionCandidatePoint, regionScanEye;
        private static double regionCandidateDistance;
        private static string regionPublishedDetail = "";
        private static ThermalVisionRegionOrder regionOrder;

        private static readonly List<RegionGrid> regionSources = new List<RegionGrid>();

        private static readonly List<RegionGrid> regionPublishedSources = new List<RegionGrid>();

        private static readonly List<Region> regionDrawOrder = new List<Region>();

        private static readonly List<BoundingBoxD> regionVisibleGrids = new List<BoundingBoxD>();

        private static readonly Stopwatch RegionTime = new Stopwatch();
        private static double regionStarted, regionPublished, regionNext;
        private static string regionStatus = "ACQUIRING";

        private static readonly Vector4 RegionNeutral = new Vector4(.12f, .12f, .12f, 1);
        private static int regionDrawnCells, regionBillboards, regionCulledFaces, regionCulledPatches, regionUniformPatches;
        private static bool countUniformPatches;

        private static readonly List<Region> regionVisibleOrder = new List<Region>();

        private static readonly ThermalVisionFacePlan regionFacePlan = new ThermalVisionFacePlan();


        private static void StopRegions()
        {

            blockLabMode = false; StopBlockFleet(); blockFleetCapacity = 1400; blockFleetPressureGridCount = 0; regionRenderMatrix = MatrixD.Identity;
            if (regionScan != null) regionScan.Dispose();
            if (regionLod != null) regionLod.Dispose();
            regionLod = null; regionLodPending = false; regionPublishedDetail = "";
            regionHasSurface = regionCandidateSurface = false;
            regionScan = null; regionOrder = null;
            regionVisibleOrder.Clear(); regionFacePlan.Build(regionVisibleOrder, Vector3D.Zero);
            regionSources.Clear(); regionPublishedSources.Clear(); regionDrawOrder.Clear(); regionVisibleGrids.Clear();
            RegionTime.Reset(); regionStarted = regionPublished = regionNext = 0;
            regionCellSize = 5; regionStatus = "ACQUIRING";
        }


        private static bool RegionsStillValid(List<RegionGrid> sources)
        {
            foreach (RegionGrid source in sources)
            {
                var grid = source.Grid.Grid;
                if (grid == null || grid.MarkedForClose || source.Grid.BlockCount != source.Blocks) return false;
                MatrixD now = grid.WorldMatrix;
                double angular = Math.Sqrt((now.Right - source.Matrix.Right).LengthSquared()
                    + (now.Up - source.Matrix.Up).LengthSquared() + (now.Backward - source.Matrix.Backward).LengthSquared());
                if (Vector3D.Distance(now.Translation, source.Matrix.Translation) + source.Radius * angular > Math.Min(source.DetailCellSize, regionCellSize) * .25)
                    return false;
            }
            return true;
        }


        private static IEnumerable<Region> ReadRegionGrid(RegionGrid source, Vector3D eye)
        {
            foreach (ThermalBlock block in source.Grid.Blocks)
            {
                if (block == null || block.Node == null) continue;
                float temperature = block.Node.Temperature;
                if (float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature < 0 || temperature > 100000) continue;
                Vector3D centre;
                Vector3 half;
                block.Block.ComputeWorldCenter(out centre);
                block.Block.ComputeScaledHalfExtents(out half);
                if (Vector3D.DistanceSquared(centre, eye) > 25000000) continue;
                Vector3D extent = Vector3D.Abs(source.Matrix.Right) * half.X
                    + Vector3D.Abs(source.Matrix.Up) * half.Y + Vector3D.Abs(source.Matrix.Backward) * half.Z;

                var sample = new Region(centre - extent, centre + extent, temperature);
                Vector3D closest = Vector3D.Max(sample.Min, Vector3D.Min(eye, sample.Max));
                double distance = Vector3D.DistanceSquared(eye, closest);
                if (distance < regionCandidateDistance)
                {

                    var bounds = new BoundingBoxD(sample.Min, sample.Max);
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref bounds))
                    { regionCandidateDistance = distance; regionCandidatePoint = closest; regionCandidateSurface = true; }
                }
                regionLod.Observe(sample);
                yield return sample;
            }
        }


        private static void StartRegionScan(double now)
        {
            regionSources.Clear();
            regionLodPending = false;
            if (regionLod != null) regionLod.Dispose();
            var input = new List<IEnumerable<Region>>();
            Vector3D eye = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            regionScanEye = eye;

            var anchorBounds = new BoundingBoxD(regionSurface - new Vector3D(1), regionSurface + new Vector3D(1));
            bool useSurface = regionHasSurface && Vector3D.DistanceSquared(eye, regionSurfaceEye) < 400
                && MyAPIGateway.Session.Camera.IsInFrustum(ref anchorBounds);

            regionLod = useSurface ? new ThermalVisionRegionLod(eye, regionSurface) : new ThermalVisionRegionLod(eye);
            regionCandidateSurface = false; regionCandidateDistance = double.PositiveInfinity;

            var reach = new BoundingSphereD(eye, 5000);
            foreach (ThermalGrid thermal in ThermalGrid.LiveGrids)
            {
                var grid = thermal.Grid;
                if (grid == null || grid.MarkedForClose || thermal.Simulation == null
                    || !grid.PositionComp.WorldAABB.Intersects(reach)) continue;
                var gridBounds = grid.PositionComp.WorldAABB;
                if (!MyAPIGateway.Session.Camera.IsInFrustum(ref gridBounds)) continue;
                var source = new RegionGrid { Grid = thermal, Matrix = grid.WorldMatrix, Blocks = thermal.BlockCount,
                    Radius = grid.PositionComp.WorldAABB.HalfExtents.Length(),
                    DetailCellSize = regionCellSize };
                foreach (var band in regionLod.Bands)
                    if (grid.PositionComp.WorldAABB.Intersects(new BoundingBoxD(band.Min, band.Max)))
                        source.DetailCellSize = Math.Min(source.DetailCellSize, band.Scan.CellSize);
                regionSources.Add(source);
                input.Add(ReadRegionGrid(source, eye));
            }
            if (regionScan == null) regionScan = new ThermalVisionRegionScan(regionCellSize, 384, 256, 5120);
            regionStarted = now;
            if (!regionScan.Start(input))

            { regionStatus = "SCAN FAILED: " + regionScan.Failure; regionNext = now + 1; RecordEvent(regionStatus); }
        }


        private static void UpdateRegionField(double now)
        {
            if (regionOrder != null && (now - regionPublished > 8 || !RegionsStillValid(regionPublishedSources)))

            { regionOrder = null; regionStatus = "REFRESHING / movement or age"; RecordEvent("region field invalidated: movement/topology/age"); }
            if (!regionLodPending && (regionScan == null || !regionScan.Running) && now >= regionNext) StartRegionScan(now);
            if (regionScan == null || (!regionScan.Running && !regionLodPending)) return;
            if (now - regionStarted > 12 || !RegionsStillValid(regionSources))
            { regionScan.Cancel(); if (regionLod != null) regionLod.Dispose(); regionLodPending = false;
                regionNext = now + .1; regionStatus = "REFRESHING / scene changed"; return; }
            int work = 0;
            while (regionScan.Running && work < 32768 && ProbeClock.Elapsed.TotalMilliseconds < 2)
                work += regionScan.Advance(128);
            if (regionScan.Running)
            {
                if (regionOrder == null) regionStatus = "ACQUIRING " + regionScan.SampleCount
                    + " samples / groups " + regionScan.CellSize + " m / normal view";
                return;
            }
            if (regionScan.Failure != null)
            {
                regionStatus = "SCAN FAILED: " + regionScan.Failure;
                RecordEvent("regions " + regionStatus + "; size=" + regionCellSize);
                regionNext = now + 1;
                return;
            }
            regionLodPending = true;
            while (regionLod.Running && work < 32768 && ProbeClock.Elapsed.TotalMilliseconds < 2)
                work += regionLod.Advance(128);
            if (regionLod.Running) return;
            ThermalVisionRegionOrder next;
            if (!regionLod.TryBuild(regionScan, ThermalVisionScenePolicy.RegionCellLimit, out next))

            { regionLodPending = false; regionStatus = "ORDER CAPACITY / field retained"; regionNext = now + 1; RecordEvent(regionStatus); return; }
            regionLodPending = false;
            regionHasSurface = regionCandidateSurface;
            if (regionHasSurface) { regionSurface = regionCandidatePoint; regionSurfaceEye = regionScanEye; }
            regionPublishedDetail = "LOD near/mid/far: ";
            for (int i = 2; i >= 0; i--)
                regionPublishedDetail += (regionLod.Bands[i].Applied ? regionLod.Bands[i].Scan.CellSize + " m (" + regionLod.Bands[i].Scan.Count + " cells)" : "fallback")
                    + (i > 0 ? " / " : "");
            regionCellSize = regionScan.CellSize;
            regionOrder = next; regionPublished = now; regionNext = now + .15;
            regionPublishedSources.Clear(); regionPublishedSources.AddRange(regionSources);
            regionStatus = "LIVE GROUP ESTIMATE";
            RecordEvent("regions published: grids=" + regionSources.Count + " samples=" + regionScan.SampleCount
                + " cells=" + regionScan.Count + " fragments=" + next.LeafCount + " group-m=" + regionCellSize
                + " " + regionPublishedDetail + " nearest-sample-m=" + (regionCandidateSurface ? Math.Sqrt(regionCandidateDistance) : -1) + " coarsenings=" + regionScan.Coarsenings + " scan-ms=" + (now - regionStarted) * 1000 + " adapter-ms=" + ProbeClock.Elapsed.TotalMilliseconds, false);
        }


        private static void RegionBackdrop(double distance, Vector4 colour)
        {
            var camera = MyAPIGateway.Session.Camera;
            Vector3D centre; float width, height;
            ThermalVisionDepthLayers.Plane(distance, camera.ProjectionMatrix, camera.WorldMatrix, out centre, out width, out height);
            regionBillboards++;
            MyTransparentGeometry.AddBillboardOriented(DepthMaterial, colour, centre,
                (Vector3)camera.WorldMatrix.Right, (Vector3)camera.WorldMatrix.Up,
                width * 1.25f, height * 1.25f, Vector2.Zero, MyBillboard.BlendTypeEnum.PostPP);
        }


        private static void RegionTriangle(Vector3D a, Vector3D b, Vector3D c, Vector4 colour, Vector3D eye, bool bias = true)
        {
            a = Vector3D.Transform(a, regionRenderMatrix); b = Vector3D.Transform(b, regionRenderMatrix);
            c = Vector3D.Transform(c, regionRenderMatrix); eye = Vector3D.Transform(eye, regionRenderMatrix);
            Vector3D n = Vector3D.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-20) return;
            if (Vector3D.Dot(n, eye - a) < 0) { Vector3D swap = b; b = c; c = swap; n = -n; }
            n.Normalize();
            Vector3D offset = bias ? MyAPIGateway.Session.Camera.WorldMatrix.Backward * .001 : Vector3D.Zero;
            MyTransparentGeometry.AddTriangleBillboard(a + offset, b + offset, c + offset,
                (Vector3)n, (Vector3)n, (Vector3)n, Vector2.Zero, Vector2.UnitX, Vector2.UnitY,
                CompositeSurfaceMaterial, uint.MaxValue, (a + b + c) / 3, colour, MyBillboard.BlendTypeEnum.PostPP);
            regionBillboards++; drawn++; examined++;
        }


        private static double RegionAxis(Vector3D v, int axis) { return axis == 0 ? v.X : axis == 1 ? v.Y : v.Z; }

        private static void RegionFace(Region region, int axis, bool upper, Vector4 colour, Vector3D eye, bool gradient = false, BoundingFrustumD patchFrustum = null, ThermalVisionSurfaceField knownField = null, bool coarse = false)
        {
            Vector3D a = region.Min, u = Vector3D.Zero, v = Vector3D.Zero;
            if (axis == 0) { a.X = upper ? region.Max.X : region.Min.X; u.Y = region.Max.Y - region.Min.Y; v.Z = region.Max.Z - region.Min.Z; }

            else if (axis == 1) { a.Y = upper ? region.Max.Y : region.Min.Y; u.X = region.Max.X - region.Min.X; v.Z = region.Max.Z - region.Min.Z; }
            else { a.Z = upper ? region.Max.Z : region.Min.Z; u.X = region.Max.X - region.Min.X; v.Y = region.Max.Y - region.Min.Y; }
            // Independent fleets already have a local frustum. Reject a face
            // before world-space transforms and avoid another camera API round-trip.
            ContainmentType faceRelation=patchFrustum==null?ContainmentType.Intersects
                :patchFrustum.Contains(ThermalVisionGeometry.PatchBounds(a,a+u+v));
            if(faceRelation==ContainmentType.Disjoint)
            { regionCulledFaces++; return; }
            Vector3D faceCentre = Vector3D.Transform(a + (u + v) * .5, regionRenderMatrix);
            Vector3D worldU=Vector3D.TransformNormal(u,regionRenderMatrix);
            Vector3D worldV=Vector3D.TransformNormal(v,regionRenderMatrix);
            if(patchFrustum==null)
            {
                Vector3D faceHalf = (Vector3D.Abs(worldU) + Vector3D.Abs(worldV)) * .5 + new Vector3D(.002);
                var faceBounds = new BoundingBoxD(faceCentre - faceHalf, faceCentre + faceHalf);
                if(!MyAPIGateway.Session.Camera.IsInFrustum(ref faceBounds)) { regionCulledFaces++; return; }
            }
            if (gradient)
            {
                ThermalVisionSurfaceField field = knownField;
                if (field == null && !smoothCorners.TryGetValue(region,out field)) return;
                Vector3D worldEye=Vector3D.Transform(eye,regionRenderMatrix);
                Vector3D offset=MyAPIGateway.Session.Camera.WorldMatrix.Backward*.001;
                Vector3 normal; bool reverse;
                if(!ThermalVisionGeometry.FaceNormal(worldU,worldV,worldEye-faceCentre,out normal,out reverse)) return;
                if(coarse)
                {
                    ThermalVisionSurfaceField previous=null;
                    if(previousSurface!=null && surfaceBlend<1) previousSurface.TryGetValue(region,out previous);
                    SmoothPatch(field.WholeFace(axis,upper,previous),offset,normal,reverse,null);
                    return;
                }
                var patches=field.Face(axis,upper);
                if(patches.Count<=1 || faceRelation==ContainmentType.Contains)
                    patchFrustum=null;
                for(int i=0;i<patches.Count;i++) SmoothPatch(patches[i],offset,normal,reverse,patchFrustum);
                return;
            }
            if (blockLabMode)
            {
                Vector3D right = worldU;
                Vector3D up = worldV;
                float width = (float)right.Length(), height = (float)up.Length();
                right /= width; up /= height;
                Vector3D centre = faceCentre;
                Vector3D worldEye = Vector3D.Transform(eye, regionRenderMatrix);
                if (Vector3D.Dot(Vector3D.Cross(right, up), worldEye - centre) < 0) right = -right;
                centre += MyAPIGateway.Session.Camera.WorldMatrix.Backward * .001;
                MyTransparentGeometry.AddBillboardOriented(CompositeSurfaceMaterial, colour, centre,
                    (Vector3)right, (Vector3)up, width * .5f, height * .5f, Vector2.Zero, MyBillboard.BlendTypeEnum.PostPP);
                regionBillboards++; drawn += 2; examined += 2;
                return;
            }
            RegionTriangle(a, a + u, a + u + v, colour, eye);
            RegionTriangle(a, a + u + v, a + v, colour, eye);
        }


        private static void DrawRegions()
        {
            if (smoothFleet) { DrawIndependentFleet(); return; }
            if (!RegionTime.IsRunning) RegionTime.Start();
            double now = RegionTime.Elapsed.TotalSeconds;
            if (blockLabMode) UpdateBlockField(now); else UpdateRegionField(now);
            var camera = MyAPIGateway.Session.Camera;
            MatrixD localCamera = camera.WorldMatrix * MatrixD.Invert(regionRenderMatrix);
            Vector3D eye = localCamera.Translation;
            double near = camera.NearPlaneDistance * 1.05;
            regionBillboards = regionCulledFaces = 0;
            bool drawContext = ThermalVisionViewPolicy.DrawContext(blockLabMode, regionOrder != null);
            if (drawContext) RegionBackdrop(near, RegionNeutral);
            double elapsed = Math.Min(.25, ExposureClock.Elapsed.TotalSeconds); ExposureClock.Restart();
            if (automaticRange && SceneRange.Update(elapsed)) { lowKelvin = SceneRange.Low; highKelvin = SceneRange.High; }
            regionVisibleGrids.Clear();
            foreach (RegionGrid source in regionPublishedSources)
            {
                var grid = source.Grid.Grid;
                if (grid == null || grid.MarkedForClose) continue;
                var bounds = grid.PositionComp.WorldAABB;
                if (camera.IsInFrustum(ref bounds)) regionVisibleGrids.Add(bounds);
            }
            regionDrawnCells = 0;
            float visibleLow = float.PositiveInfinity, visibleHigh = float.NegativeInfinity;
            if (automaticRange) SceneRange.BeginSamples();
            regionVisibleOrder.Clear();
            if (regionOrder != null)
            {
                regionOrder.WriteNearToFar(eye, regionDrawOrder);
                foreach (Region region in regionDrawOrder)
                {
                    Vector3D worldCentre = Vector3D.Transform((region.Min + region.Max) * .5, regionRenderMatrix);
                    Vector3D localHalf = (region.Max - region.Min) * .5;
                    Vector3D worldHalf = Vector3D.Abs(regionRenderMatrix.Right) * localHalf.X
                        + Vector3D.Abs(regionRenderMatrix.Up) * localHalf.Y + Vector3D.Abs(regionRenderMatrix.Backward) * localHalf.Z;

                    var box = new BoundingBoxD(worldCentre - worldHalf, worldCentre + worldHalf);
                    if (!camera.IsInFrustum(ref box)) continue;
                    bool touchesVisibleGrid = false;
                    foreach (BoundingBoxD bounds in regionVisibleGrids)
                        if (bounds.Intersects(box)) { touchesVisibleGrid = true; break; }
                    if (!touchesVisibleGrid) continue;
                    regionVisibleOrder.Add(region);
                }
                if (!smoothFleet) regionFacePlan.Build(regionVisibleOrder, eye);
                for (int regionIndex = 0; regionIndex < regionVisibleOrder.Count; regionIndex++)
                {
                    Region region = regionVisibleOrder[regionIndex];
                    visibleLow = Math.Min(visibleLow, region.Kelvin);
                    visibleHigh = Math.Max(visibleHigh, region.Kelvin);
                    ThermalVisionSurfaceField temperatureField;
                    if (smoothFleet && smoothCorners.TryGetValue(region,out temperatureField))
                    {
                        foreach(float kelvin in temperatureField.Values)
                        {
                            visibleLow=Math.Min(visibleLow,kelvin); visibleHigh=Math.Max(visibleHigh,kelvin);
                            if(automaticRange) SceneRange.Observe(kelvin);
                        }
                    }
                    else if (automaticRange) SceneRange.Observe(region.Kelvin);
                    Vector3 srgb;
                    if (!ThermalVisionPalette.TrySample(region.Kelvin, State.Current, lowKelvin, highKelvin, out srgb)) continue;

                    Vector4 colour = new Vector4(srgb, 1);
                    Vector3D middle = (region.Min + region.Max) * .5, half = (region.Max - region.Min) * .5;
                    double depth = Vector3D.Dot(middle - eye, localCamera.Forward);
                    double radius = Vector3D.Dot(half, Vector3D.Abs(localCamera.Forward));
                    if (depth - radius <= near && depth + radius >= near)
                    {
                        Vector3D[] cap = ThermalVisionRegionPartition.NearCap(region, localCamera, camera.ProjectionMatrix, near);
                        for (int i = 1; i + 1 < cap.Length; i++)
                            if(smoothFleet) SmoothTriangle(region,cap[0],cap[i],cap[i+1],eye,false);

                            else RegionTriangle(cap[0], cap[i], cap[i + 1], colour, eye, false);
                    }
                    for (int pass = 0; pass < 2; pass++)
                        for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
                        {
                            bool upper = side == 1;

                            double plane = RegionAxis(upper ? region.Max : region.Min, axis);
                            bool entering = (RegionAxis(eye, axis) - plane) * (upper ? 1 : -1) > 0;
                            if (entering == (pass == 0) && (smoothFleet || !regionFacePlan.Skip(regionIndex, axis, upper)))
                                RegionFace(region, axis, upper, entering ? colour : RegionNeutral, eye, smoothFleet && entering);
                        }
                    regionDrawnCells++;
                }
            }

            double viewDistance = blockLabMode ? BlockFleetViewDistance() : 5000;
            if (drawContext) RegionBackdrop(blockLabMode ? ThermalVisionViewPolicy.BackdropDistance(viewDistance) : viewDistance, new Vector4(0, 0, 0, 1));
            incomplete = regionOrder == null;
            rowIdentity = blockLabMode ? (smoothFleet ? "smooth fleet / palette UVs / " : "block fleet lab / shared bounds / quads / ") + viewDistance.ToString("0") + " m game view" : "regions / " + regionCellSize + " m groups / 5000 m"; rowKey = null;
            Outcome(regionOrder == null ? (blockLabMode ? "thermal-context-awaiting-field" : "region-acquiring-normal-view") : blockLabMode ? "per-block-bounds" : "region-estimate");
            if (regionOrder == null && !blockLabMode && frames % 120 == 0)
                MyAPIGateway.Utilities.ShowNotification("Thermal: " + regionStatus, 2500);
            if (frames++ % 30 == 0)
            {

                panel.Text = new RichText((blockLabMode ? "THERMAL LAB / BLOCKS / " : "THERMAL LAB / REGIONS / ") + (State.Current == ThermalVisionState.Mode.Cividis ? "CIVIDIS" : "WHITE HOT")
                    + (blockLabMode ? " / VIEWPORT FLEET\n" : " / GROUP ESTIMATE\n") + (lowKelvin - 273.15f).ToString("0") + " to " + (highKelvin - 273.15f).ToString("0")
                    + " C / " + (automaticRange ? "AUTO" : "LOCK") + (blockLabMode ? " / BLOCK + GROUP TEMPERATURES" : " / groups " + regionCellSize + " m")
                    + (blockLabMode ? " / GAME VIEW " + (viewDistance / 1000).ToString("0.##") + " km" : "")
                    + ("\n" + regionPublishedDetail + "\n")
                    + regionStatus + " / " + regionDrawnCells + " regions / age "
                    + (regionOrder == null ? "—" : (now - regionPublished).ToString("0.0") + " s")
                    + "\nNeutral grey: unmeasured / off to close");
                if (blockLabMode)

                    panel.Text = new RichText("THERMAL / " + (State.Current == ThermalVisionState.Mode.Cividis ? "CIVIDIS" : "WHITE HOT")
                        + " / " + (automaticRange ? "AUTO" : "LOCK")
                        + "\n" + (lowKelvin - 273.15f).ToString("0") + " to " + (highKelvin - 273.15f).ToString("0") + " C"
                        + " / " + (viewDistance / 1000).ToString("0.##") + " km"
                        + "\n" + (regionOrder == null ? regionStatus : "BLOCK + GROUP ESTIMATE / " + regionVisibleGrids.Count + " grids")
                        + "\n" + (State.Current == ThermalVisionState.Mode.Cividis ? "Blue cool / yellow hot" : "Dark cool / bright hot")
                        + " / neutral: unmeasured");
                RecordEvent("region draw: context-active=" + drawContext + " viewport-grids=" + regionVisibleGrids.Count + " regions=" + regionDrawnCells + " billboards=" + regionBillboards
                    + " shared-faces-removed=" + (regionOrder == null || smoothFleet ? 0 : regionFacePlan.Removed) + " offscreen-faces=" + regionCulledFaces
                    + " triangles=" + drawn + " age="
                    + (regionOrder == null ? -1 : now - regionPublished) + " status=" + regionStatus
                    + " visible-K=" + (regionDrawnCells == 0 ? "none" : visibleLow + ".." + visibleHigh)
                    + " display-K=" + lowKelvin + ".." + highKelvin, false);
            }
            panel.Visible = true;
        }
    }
}

using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using RichHudFramework.UI;
using Thermodynamics.Core;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private sealed class GridView
        {
            public ThermalGrid Grid;
            public ThermalVisionRegionOrder Order;

            public Dictionary<Region,ThermalVisionSurfaceField> Fields = new Dictionary<Region,ThermalVisionSurfaceField>();

            public Dictionary<Region,ThermalVisionSurfaceField> Previous = new Dictionary<Region,ThermalVisionSurfaceField>();
            public IEnumerator<bool> Work;
            public double Published, Next, Distance;
            public int Blocks, DetailBudget;
            public long TopologyRevision;
            public double Spacing;
            public bool Interior;
            public Vector3D InteriorCentre;
            public float Mean;
            public bool Detailed;
        }

        private static readonly Dictionary<ThermalGrid,GridView> gridViews = new Dictionary<ThermalGrid,GridView>();

        private static readonly List<GridView> visibleViews = new List<GridView>();

        private static readonly List<ThermalGrid> deadViews = new List<ThermalGrid>();
        private static int viewWorkCursor;

        private static readonly List<BoundingBoxD> localVisionBlockers=new List<BoundingBoxD>(32);
        private static double preparationPriorityDebt;

        private static readonly List<Vector3D> nearCapPolygon=new List<Vector3D>(16);

        private static readonly List<Vector3D> nearCapScratch=new List<Vector3D>(16);

        // Reused per grid each frame: nothing retains the frustum past the draw call.
        private static readonly BoundingFrustumD fleetFrustum=new BoundingFrustumD();
        private static readonly System.Diagnostics.Stopwatch FleetClock = System.Diagnostics.Stopwatch.StartNew();
        private static Dictionary<Region,ThermalVisionSurfaceField> previousSurface;
        private static float surfaceBlend = 1;


        private static void PauseIndependentFleet()
        {
            preparationPriorityDebt=0;
            foreach(var view in gridViews.Values)
            {
                if(view.Work!=null) view.Work.Dispose();
                view.Work=null; view.Next=0;
            }
            previousSurface=null;
        }

        private static void ClearIndependentFleet()
        {
            preparationPriorityDebt=0;
            foreach(var view in gridViews.Values) if(view.Work!=null) view.Work.Dispose();
            gridViews.Clear(); visibleViews.Clear(); visionOccluders.Clear(); skyPlanets.Clear(); previousSurface=null;
        }

        private static float OldTemperature(GridView view,Vector3D p)
        {
            Region cell; ThermalVisionSurfaceField field;
            if(view.Order!=null && view.Order.TryFind(p,out cell) && view.Fields.TryGetValue(cell,out field)) return field.Sample(p);
            return view.Mean;
        }

        private static GridView FirstView(ThermalGrid grid,double now)
        {
            double sum=0; int count=0, visited=0;
            foreach(var block in grid.Blocks)
            {
                if(visited++>=32) break;
                if(block==null || block.Node==null) continue;
                float t=block.Node.Temperature;
                if(!float.IsNaN(t) && !float.IsInfinity(t) && t>=0 && t<=100000) { sum+=t; count++; }

            }
            var view=new GridView { Grid=grid, Mean=count>0?(float)(sum/count):293.15f, Published=now, Blocks=grid.BlockCount };
            double size=grid.Grid.GridSize;

            var box=new Region(((Vector3D)grid.Grid.Min-new Vector3D(.5))*size,((Vector3D)grid.Grid.Max+new Vector3D(.5))*size,view.Mean);

            var field=new ThermalVisionSurfaceField(box,double.MaxValue);
            for(int i=0;i<field.Values.Length;i++) field.Values[i]=view.Mean;
            field.BuildFaces(20); view.Fields.Add(box,field);
            ThermalVisionRegionOrder.TryBuild(new List<Region>{box},16,out view.Order);
            return view;
        }

        private static IEnumerable<Region> InteriorSamples(ThermalGrid thermal,Vector3D min,Vector3D max)
        {
            double size=thermal.Grid.GridSize;
            foreach(var block in thermal.Blocks)
            {
                if(block==null || block.Node==null) continue;
                float t=block.Node.Temperature;
                if(float.IsNaN(t)||float.IsInfinity(t)||t<0||t>100000) continue;
                Vector3D lo=Vector3D.Max(min,((Vector3D)block.Block.Min-new Vector3D(.5))*size);
                Vector3D hi=Vector3D.Min(max,((Vector3D)block.Block.Max+new Vector3D(.5))*size);
                if(lo.X<hi.X && lo.Y<hi.Y && lo.Z<hi.Z) yield return new Region(lo,hi,t);
            }
        }

        private static IEnumerable<bool> RefreshGrid(GridView view)
        {
            double refreshStarted=FleetClock.Elapsed.TotalSeconds;
            var grid=view.Grid.Grid;
            var job=new BlockFleetJob { Grid=view.Grid, Matrix=grid.WorldMatrix, BlockCount=view.Grid.BlockCount, Budget=512 };
            long revision=view.Grid.TopologyRevision;
            bool reuse=view.Detailed && view.TopologyRevision==revision && view.Blocks==job.BlockCount;
            Vector3D eye=MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            Vector3D localEye=Vector3D.Transform(eye,MatrixD.Invert(job.Matrix));
            Vector3D origin=((Vector3D)grid.Min-new Vector3D(.5))*grid.GridSize;
            Vector3D end=((Vector3D)grid.Max+new Vector3D(.5))*grid.GridSize;
            bool interior=localEye.X>=origin.X && localEye.Y>=origin.Y && localEye.Z>=origin.Z
                && localEye.X<=end.X && localEye.Y<=end.Y && localEye.Z<=end.Z;
            Vector3D centre=view.InteriorCentre;
            if(!view.Interior || Vector3D.Abs(localEye-centre).AbsMax()>grid.GridSize*4)
            {
                Vector3D q=(localEye-origin)/(grid.GridSize*4);

                centre=origin+new Vector3D(Math.Floor(q.X)+.5,Math.Floor(q.Y)+.5,Math.Floor(q.Z)+.5)*(grid.GridSize*4);
                if(interior) reuse=false;
            }
            if(interior!=view.Interior) reuse=false;
            double focal=Math.Max(1,MyAPIGateway.Session.Camera.ViewportSize.Y*.5*Math.Abs(MyAPIGateway.Session.Camera.ProjectionMatrix.M22));
            double diameter=(end-origin).Length()*focal/Math.Max(grid.GridSize,view.Distance);
            job.Budget=ThermalVisionViewPolicy.DetailBudget(diameter,view.DetailBudget,interior);
            if(job.Budget!=view.DetailBudget) reuse=false;
            if(!reuse) job.Detail=new ThermalVisionBlockDetail(job.Budget,localEye);
            double desired=Math.Max(grid.GridSize,view.Distance*4/focal);
            job.SurfaceSpacing=grid.GridSize;
            while(job.SurfaceSpacing<desired) job.SurfaceSpacing*=2;
            if(view.Spacing>0 && desired>=view.Spacing*.75 && desired<=view.Spacing*2) job.SurfaceSpacing=view.Spacing;

            job.TemperatureField=new ThermalVisionBlockField(grid.GridSize,job.SurfaceSpacing*.6,

                new Region(((Vector3D)grid.Min-new Vector3D(.5))*grid.GridSize,((Vector3D)grid.Max+new Vector3D(.5))*grid.GridSize,0),job.BlockCount);

            var cells=new List<Region>();
            ThermalVisionRegionOrder order=view.Order;
            if(reuse)
            {
                foreach(var sample in FleetSamples(job,false)) yield return true;
            }
            else
            {
                using(var scan=new ThermalVisionRegionScan(grid.GridSize,job.Detail.CoarseCapacity,1,10485760,
                    origin:((Vector3D)grid.Min-new Vector3D(.5))*grid.GridSize))
                {
                    scan.Start(new List<IEnumerable<Region>> { FleetSamples(job) });
                    while(scan.Running) { scan.Advance(128); yield return true; }
                    if(scan.Failure!=null) yield break;
                    using(var mean=new ThermalVisionRegionScan(scan.CellSize,job.Detail.CoarseCapacity,1,average:true,origin:scan.Origin))
                    {
                        mean.Start(new List<IEnumerable<Region>> { FleetSamples(job,false) });
                        while(mean.Running) { mean.Advance(128); yield return true; }
                        if(mean.Failure!=null) yield break;
                        cells=job.Detail.Build(mean,true);
                        if(interior)
                        {

                            Vector3D min=centre-new Vector3D(grid.GridSize*6), max=centre+new Vector3D(grid.GridSize*6);
                            using(var fine=new ThermalVisionRegionScan(grid.GridSize,4096,1,origin:origin))
                            {
                                fine.Start(new List<IEnumerable<Region>> { InteriorSamples(view.Grid,min,max) });
                                while(fine.Running) { fine.Advance(128); yield return true; }
                                ThermalVisionRegionPartition focused;
                                if(!ThermalVisionRegionPartition.TryReplace(ThermalVisionRegionPartition.FromScan(mean,8192),fine,min,max,8192,out focused)
                                    || !ThermalVisionRegionOrder.TryBuild(focused,8192,out order)) yield break;
                            }
                        }
                        else if(!ThermalVisionRegionOrder.TryBuild(cells,1536,out order)) yield break;
                    }
                }
            }
            cells.Clear(); order.WriteNearToFar(Vector3D.Zero,cells);
            yield return true;
            var paired=reuse?null:ThermalVisionFacePlan.PairedFaces(cells); int cellIndex=0;

            var fields=new Dictionary<Region,ThermalVisionSurfaceField>(cells.Count);

            var prior=new Dictionary<Region,ThermalVisionSurfaceField>(cells.Count);
            int patchTrianglesSaved=0;
            foreach(var cell in cells)
            {

                var field=new ThermalVisionSurfaceField(cell,job.SurfaceSpacing);
                field.PairedFaceMask=reuse?view.Fields[cell].PairedFaceMask:paired[cellIndex];
                cellIndex++;

                var before=new ThermalVisionSurfaceField(cell,job.SurfaceSpacing);
                for(int z=0;z<=field.Steps.Z;z++) for(int y=0;y<=field.Steps.Y;y++) for(int x=0;x<=field.Steps.X;x++)
                {
                    var p=field.Point(x,y,z); int i=field.Index(x,y,z);
                    field.Values[i]=job.TemperatureField.SampleCached(p,cell.Kelvin);
                    before.Values[i]=OldTemperature(view,p);
                    yield return true;
                }
                field.BuildFaces(20,true,before,job.Budget>=512?20:0); patchTrianglesSaved+=field.SavedPatches*2; field.PrepareTransition(before); fields.Add(cell,field); prior.Add(cell,before);
                yield return true;
            }
            if(grid.MarkedForClose || view.Grid.BlockCount!=job.BlockCount || view.Grid.TopologyRevision!=revision) yield break;
            view.Order=order; view.Fields=fields; view.Previous=prior; view.Blocks=job.BlockCount;
            view.Interior=interior; view.InteriorCentre=centre;
            view.TopologyRevision=revision; view.Spacing=job.SurfaceSpacing; view.DetailBudget=job.Budget;
            view.Detailed=true; view.Published=FleetClock.Elapsed.TotalSeconds;
            RecordEvent("independent grid published: grid="+grid.EntityId+" blocks="+job.BlockCount+" regions="+cells.Count+" prepared-tri-saved="+patchTrianglesSaved+" detail-budget="+job.Budget+" diameter-px="+diameter.ToString("F1")
                +" refresh-wall-ms="+((FleetClock.Elapsed.TotalSeconds-refreshStarted)*1000).ToString("F1")+" interior-occupancy="+interior+" geometry-reused="+reuse+" refresh-path="+(reuse?"temperature-only":"topology")+" spacing-m="+job.SurfaceSpacing+" nearest-samples="+job.TemperatureField.NearestSamples
                +" cache-hits="+job.TemperatureField.CacheHits+" cache-entries="+job.TemperatureField.CacheEntries+" thermal-samples="+job.TemperatureField.Count+" field-fallbacks="+job.TemperatureField.Fallbacks+" field-queries="+job.TemperatureField.Queries,false);
        }

        private static int FindFleetPreparationPriority(double now)
        {
            int refresh=-1;
            for(int i=0;i<visibleViews.Count;i++)
            {
                var view=visibleViews[i];
                if(view.Work==null && now<view.Next) continue;
                if(!view.Detailed) return i;
                if(refresh<0) refresh=i;
            }
            return refresh;
        }

        private static void DrawIndependentFleet()
        {
            double now=FleetClock.Elapsed.TotalSeconds;
            var camera=MyAPIGateway.Session.Camera; Vector3D eyeWorld=camera.WorldMatrix.Translation;
            visibleViews.Clear(); deadViews.Clear();
            foreach(var pair in gridViews) if(pair.Key.Grid==null || pair.Key.Grid.MarkedForClose) deadViews.Add(pair.Key);
            foreach(var key in deadViews) { if(gridViews[key].Work!=null) gridViews[key].Work.Dispose(); gridViews.Remove(key); }
            foreach(var thermal in ThermalGrid.LiveGrids)
            {
                var grid=thermal.Grid;
                if(grid==null || grid.MarkedForClose || thermal.Simulation==null || thermal.BlockCount==0) continue;
                var bounds=grid.PositionComp.WorldAABB;
                if(!camera.IsInFrustum(ref bounds)) continue;
                GridView view;
                if(!gridViews.TryGetValue(thermal,out view)) { view=FirstView(thermal,now); gridViews.Add(thermal,view); }
                view.Distance=Vector3D.Distance(eyeWorld,Vector3D.Clamp(eyeWorld,bounds.Min,bounds.Max));
                visibleViews.Add(view);
            }
            visibleViews.Sort((a,b)=> { int d=a.Distance.CompareTo(b.Distance); return d!=0?d:a.Grid.Grid.EntityId.CompareTo(b.Grid.Grid.EntityId); });
            double occlusionStart=ProbeClock.Elapsed.TotalMilliseconds;
            int hiddenGrids=CullHiddenFleet(eyeWorld);
            double occlusionMs=ProbeClock.Elapsed.TotalMilliseconds-occlusionStart;
            double preparationStart=ProbeClock.Elapsed.TotalMilliseconds;
            double priorityMs=0,backgroundMs=0;
            int priority=FindFleetPreparationPriority(now);
            long priorityGrid=priority>=0?visibleViews[priority].Grid.Grid.EntityId:0;
            double priorityDistance=priority>=0?visibleViews[priority].Distance:0;
            int visited=0, idle=0;
            while(ThermalVisionPreparationBudget.CanAdvance(ProbeClock.Elapsed.TotalMilliseconds,preparationStart,visited,visibleViews.Count,idle))
            {
                bool background=priority<0 || ThermalVisionPreparationBudget.PreferBackground(preparationPriorityDebt,0);
                int index=priority;
                if(background) { index=viewWorkCursor%visibleViews.Count; viewWorkCursor=(index+1)%visibleViews.Count; }
                var view=visibleViews[index]; visited++;
                double stepStart=ProbeClock.Elapsed.TotalMilliseconds;
                if(view.Work==null && now>=view.Next) view.Work=RefreshGrid(view).GetEnumerator();
                if(view.Work!=null)
                {
                    idle=0;

                    try { if(!view.Work.MoveNext()) { view.Work.Dispose(); view.Work=null; view.Next=now+.5; } }
                    catch(Exception error)
                    {
                        view.Work.Dispose(); view.Work=null; view.Next=now+1;
                        RecordEvent("grid refresh failed, retaining snapshot: grid="+view.Grid.Grid.EntityId+" "+error.Message);
                    }
                }
                else idle++;
                double stepMs=ProbeClock.Elapsed.TotalMilliseconds-stepStart;
                if(background) { backgroundMs+=stepMs; preparationPriorityDebt-=4*stepMs; }
                else { priorityMs+=stepMs; preparationPriorityDebt+=stepMs; }
                if(priority<0) preparationPriorityDebt=0;
                if(view.Work==null && index==priority) priority=FindFleetPreparationPriority(now);
            }
            double preparationEnd=ProbeClock.Elapsed.TotalMilliseconds;
            if(frames%30==0) RecordEvent("independent preparation: priority-grid="+priorityGrid+" distance-m="+priorityDistance.ToString("F1")
                +" priority-ms="+priorityMs.ToString("F3")+" background-ms="+backgroundMs.ToString("F3")+" steps="+visited,false);
            double near=camera.NearPlaneDistance*1.05;
            regionBillboards=regionCulledFaces=regionCulledPatches=regionUniformPatches=regionDrawnCells=regionCapTriangles=0;
            countUniformPatches=frames%30==0;
            RegionBackdrop(near,RegionNeutral);
            if(automaticRange && SceneRange.Update(Math.Min(.25,ExposureClock.Elapsed.TotalSeconds))) { lowKelvin=SceneRange.Low; highKelvin=SceneRange.High; }
            ExposureClock.Restart(); if(automaticRange) SceneRange.BeginSamples();
            int previews=0, sharedExitsSkipped=0, hiddenSubtrees=0;
            MatrixD inverseCamera=MatrixD.Invert(camera.WorldMatrix);
            foreach(var view in visibleViews)
            {
                if(view.Order==null) continue;
                if(!view.Detailed) previews++;
                regionRenderMatrix=view.Grid.Grid.WorldMatrix;
                smoothCorners=view.Fields; previousSurface=view.Previous;
                surfaceBlend=(float)MathHelper.Clamp((now-view.Published)/.3,0,1);
                if(surfaceBlend>=1 && view.Previous.Count>0) view.Previous.Clear();
                MatrixD localCamera=camera.WorldMatrix*MatrixD.Invert(regionRenderMatrix); Vector3D eye=localCamera.Translation;

                var localFrustum=fleetFrustum; localFrustum.Matrix=regionRenderMatrix*inverseCamera*camera.ProjectionMatrix;
                localVisionBlockers.Clear();
                for(int i=0;i<visionOccluders.Count;i++)
                    if(visionOccluders[i].Owner==view.Grid)localVisionBlockers.Add(visionOccluders[i].Bounds);
                int gridHiddenSubtrees;
                view.Order.WriteVisibleNearToFar(eye,regionDrawOrder,localFrustum,
                    localVisionBlockers.Count==0?null:localVisionBlockers,out gridHiddenSubtrees);
                hiddenSubtrees+=gridHiddenSubtrees;
                foreach(var cell in regionDrawOrder)
                {
                    ThermalVisionSurfaceField cellField;
                    if(!view.Fields.TryGetValue(cell,out cellField)) continue;
                    if(automaticRange) { SceneRange.Observe(cellField.Minimum); SceneRange.Observe(cellField.Maximum); }
                    Vector3D middle=(cell.Min+cell.Max)*.5, half=(cell.Max-cell.Min)*.5;
                    double depth=Vector3D.Dot(middle-eye,localCamera.Forward), radius=Vector3D.Dot(half,Vector3D.Abs(localCamera.Forward));
                    if(depth-radius<=near && depth+radius>=near)
                    {
                        var cap=ThermalVisionRegionPartition.NearCap(cell,localCamera,camera.ProjectionMatrix,near,nearCapPolygon,nearCapScratch);
                        for(int i=1;i+1<cap.Count;i++) SmoothTriangle(cell,cap[0],cap[i],cap[i+1],eye,false);
                    }
                    for(int pass=0;pass<2;pass++) for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
                    {
                        bool upper=side==1;
                        bool entering=(RegionAxis(eye,axis)-RegionAxis(upper?cell.Max:cell.Min,axis))*(upper?1:-1)>0;
                        if(entering!=(pass==0)) continue;
                        if(!entering && (cellField.PairedFaceMask & (1<<(axis*2+side)))!=0) { sharedExitsSkipped++; continue; }
                        RegionFace(cell,axis,upper,RegionNeutral,eye,entering,localFrustum,cellField);
                    }
                    regionDrawnCells++;
                }
            }
            previousSurface=null; surfaceBlend=1;
            RegionBackdrop(ThermalVisionViewPolicy.BackdropDistance(BlockFleetViewDistance()),new Vector4(0,0,0,1));
            DrawThermalSky();
            incomplete=previews>0;
            rowIdentity="independent moving grids / "+(automaticRange?"AUTO":"LOCK"); rowKey=null;
            Outcome(previews>0?"independent-preview":"independent-detail");
            if(frames++%30==0)
            {
                RecordEvent("independent occlusion: own-grid-hidden-subtrees="+hiddenSubtrees,false);

                panel.Text=new RichText("THERMAL / "+(State.Current==ThermalVisionState.Mode.WhiteHot?"WHITE HOT":"CIVIDIS")
                    +" / "+(lowKelvin-273.15f).ToString("0")+" to "+(highKelvin-273.15f).ToString("0")+" C"
                    +(previews>0?" / refining "+previews+" grids":""));
                RecordEvent("independent draw: grids="+visibleViews.Count+" hidden-grids="+hiddenGrids+" solid-occluders="+visionOccluders.Count+" occlusion-ms="+occlusionMs.ToString("F3")+" previews="+previews+" regions="+regionDrawnCells+" billboards="+regionBillboards+" cap-triangles="+regionCapTriangles+" uniform-patches="+regionUniformPatches+" offscreen-patches="+regionCulledPatches+" shared-exits-skipped="+sharedExitsSkipped+" discovery-ms="+occlusionStart.ToString("F3")+" preparation-ms="+(preparationEnd-preparationStart).ToString("F3")
                    +" draw-ms="+(ProbeClock.Elapsed.TotalMilliseconds-preparationEnd).ToString("F3")+" submission-cap=none-stress-test",false);
            }
        }
    }
}

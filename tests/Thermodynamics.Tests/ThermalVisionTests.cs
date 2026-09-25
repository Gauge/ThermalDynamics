using Thermodynamics.Presentation;
using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Palette ordering and viewpoint-loss behavior; these do not validate engine rendering.</summary>
    public class ThermalVisionTests
    {
        [Fact]

        public void NearCameraCapResolvesCurvedHeatAndPreservesCoverage()
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300);

            var hot=new ThermalVisionSurfaceField(bounds,1);

            var cold=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=1;z++)for(int y=0;y<=1;y++)for(int x=0;x<=1;x++)
            {
                hot.Values[hot.Index(x,y,z)]=300+400*(x+y-2*x*y);
                cold.Values[cold.Index(x,y,z)]=300;
            }
            var triangles=new System.Collections.Generic.List<ThermalVisionSurfaceField.TemperatureTriangle>();
            for(int f=0;f<=4;f++)
            {
                triangles.Clear();float blend=f/4f;
                cold.AppendCapTriangles(Vector3D.Zero,Vector3D.UnitX,new Vector3D(1,1,0),hot,blend,20,triangles);
                Assert.InRange(triangles.Count,1,64);
                if(f==0)Assert.True(triangles.Count>1);
                if(f==4)Assert.Single(triangles);
                double area=0;
                foreach(var t in triangles)
                {
                    double signed=Vector3D.Cross(t.B-t.A,t.C-t.A).Z;
                    Assert.True(signed>0);area+=signed*.5;
                    for(int u=0;u<=16;u++)for(int v=0;v<=16-u;v++)
                    {
                        double b=u/16d,c=v/16d,a=1-b-c;
                        var p=t.A*a+t.B*b+t.C*c;
                        double estimate=t.Temperatures.X*a+t.Temperatures.Y*b+t.Temperatures.Z*c;
                        Assert.InRange(Math.Abs(estimate-cold.BlendSample(hot,p,blend)),0,20.001);
                    }
                }
                Assert.InRange(Math.Abs(area-.5),0,1e-10);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]

        public void CloseLeafSaddlesRetainGradientDuringHeatingAndCooling(bool cooling)
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300);

            var before=new ThermalVisionSurfaceField(bounds,1);

            var after=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=1;z++)for(int y=0;y<=1;y++)for(int x=0;x<=1;x++)
            {
                float saddle=300+400*(x+y-2*x*y);
                before.Values[before.Index(x,y,z)]=cooling?saddle:300;
                after.Values[after.Index(x,y,z)]=cooling?300:saddle;
            }
            after.BuildFaces(20,true,before,20);after.PrepareTransition(before);
            Assert.Equal(16,after.Face(2,false).Count);
            foreach(var patch in after.Face(2,false))for(int f=0;f<=4;f++)
                for(int y=0;y<=8;y++)for(int x=0;x<=8;x++)
                {
                    double u=x/8d,v=y/8d;float blend=f/4f;
                    var p=patch.A+(patch.B-patch.A)*u+(patch.D-patch.A)*v;
                    double rendered=PatchEstimate(patch,ThermalVisionSurfaceField.PatchTemperatures(patch,blend),u,v);
                    Assert.InRange(Math.Abs(rendered-after.BlendSample(before,p,blend)),0,12.501);
                }
            for(int i=0;i<after.Values.Length;i++)after.Values[i]=300;
            after.BuildFaces(20,true,null,20);
            Assert.Single(after.Face(2,false));
        }

        [Fact]

        public void SmallVisibleFleetCanUseRemainingPreparationTime()
        {
            Assert.True(ThermalVisionPreparationBudget.CanAdvance(.6,0,256,2,0));
            Assert.True(ThermalVisionPreparationBudget.CanAdvance(1.9,0,4095,1,0));
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(2,0,256,2,0));
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(.6,0,4096,2,0));
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(.6,0,256,2,2));
        }

        [Theory]
        [InlineData(.01,.01)]
        [InlineData(.04,.005)]
        [InlineData(.005,.04)]

        public void PreparationPrioritizesNearbyWorkWithoutStarvingBackground(double priorityCost,double otherCost)
        {
            double priority=0,background=0;
            var progress=new int[32];int cursor=0;
            for(int step=0;step<10000;step++)
            {
                if(ThermalVisionPreparationBudget.PreferBackground(priority,background))
                {
                    progress[cursor++%progress.Length]++;background+=otherCost;
                }
                else priority+=priorityCost;
            }
            Assert.InRange(priority/(priority+background),.799,.801);
            Assert.All(progress,count=>Assert.True(count>0));
            Assert.InRange(priority-4*background,-4*otherCost-.000001,priorityCost+.000001);
        }

        [Fact]

        public void UniformQuadEligibilityPreservesBothFadeEndpointsWithoutQuantization()
        {

            var patch=new ThermalVisionSurfaceField.Patch { Temperatures=new Vector4(500),PreviousTemperatures=new Vector4(300) };
            Assert.True(ThermalVisionSurfaceField.HasUniformEndpoints(patch));
            patch.PreviousTemperatures.Y=300.001f;
            Assert.False(ThermalVisionSurfaceField.HasUniformEndpoints(patch));

            patch.PreviousTemperatures=new Vector4(300);patch.Temperatures.W=500.001f;
            Assert.False(ThermalVisionSurfaceField.HasUniformEndpoints(patch));

            patch.Temperatures=new Vector4(float.PositiveInfinity);
            Assert.False(ThermalVisionSurfaceField.HasUniformEndpoints(patch));

            patch.Temperatures=new Vector4(float.NaN);
            Assert.False(ThermalVisionSurfaceField.HasUniformEndpoints(patch));
        }


        private static double PatchEstimate(ThermalVisionSurfaceField.Patch patch,Vector4 t,double u,double v)
        {
            if(patch.AlternateDiagonal)return u+v<=1?t.X*(1-u-v)+t.Y*u+t.W*v:t.Y*(1-v)+t.Z*(u+v-1)+t.W*(1-u);
            return u>=v?t.X*(1-u)+t.Y*(u-v)+t.Z*v:t.X*(1-v)+t.Z*u+t.W*(v-u);
        }

        [Fact]

        public void OppositeDiagonalGradientUsesTwoTrianglesAndRespectsTransition()
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);

            var before=new ThermalVisionSurfaceField(bounds,1);var after=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
                before.Values[before.Index(x,y,z)]=after.Values[after.Index(x,y,z)]=300+60*Math.Max(0,x+y-4);
            before.BuildFaces(20);after.BuildFaces(20,true);
            Assert.True(before.Face(2,false).Count>1);
            var face=Assert.Single(after.Face(2,false));
            Assert.True(face.AlternateDiagonal);
            for(int y=0;y<=100;y++)for(int x=0;x<=100;x++)
                Assert.InRange(Math.Abs(PatchEstimate(face,face.Temperatures,x/100d,y/100d)-(300+60*Math.Max(0,(x+y)/25d-4))),0,.0001);
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
                after.Values[after.Index(x,y,z)]=300+100*Math.Max(0,x+y-4);
            after.BuildFaces(20,true);
            Assert.True(after.Face(2,false).Count>1);
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
            {
                before.Values[before.Index(x,y,z)]=300+100*Math.Max(0,x-y);
                after.Values[after.Index(x,y,z)]=300+60*Math.Max(0,x+y-4);
            }
            after.BuildFaces(20,true,before);
            Assert.True(after.Face(2,false).Count>1);
        }

        [Fact]

        public void CoolingTransitionRetainsOldHotspotUntilItsFadeCompletes()
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);

            var previous=new ThermalVisionSurfaceField(bounds,1);var current=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
            {
                previous.Values[previous.Index(x,y,z)]=x==1&&y==2?800:300;
                current.Values[current.Index(x,y,z)]=300;
            }
            current.BuildFaces(20,true,previous);current.PrepareTransition(previous);
            Assert.True(current.Face(2,false).Count>1);
            foreach(float blend in new[]{0f,.25f,.5f,.75f,1f})
            {
                float weight=blend*blend*(3-2*blend);
                foreach(var patch in current.Face(2,false))
                    for(int y=(int)patch.A.Y;y<=patch.C.Y;y++)for(int x=(int)patch.A.X;x<=patch.C.X;x++)
                    {
                        double u=(x-patch.A.X)/(patch.C.X-patch.A.X),v=(y-patch.A.Y)/(patch.C.Y-patch.A.Y);
                        var t=ThermalVisionSurfaceField.PatchTemperatures(patch,blend);
                        double actual=PatchEstimate(patch,t,u,v);
                        double expected=previous.Values[previous.Index(x,y,0)]*(1-weight)+300*weight;
                        Assert.InRange(Math.Abs(actual-expected),0,20.0001);
                    }
            }
            current.BuildFaces(20,true,current);
            Assert.Single(current.Face(2,false));
            Assert.Throws<ArgumentException>(()=>current.BuildFaces(20,true,new ThermalVisionSurfaceField(bounds,100)));
        }

        [Theory]
        [InlineData(0,10,6)]
        [InlineData(1,18,14)]
        [InlineData(2,2,2)]

        public void SelectivePatchPlanningReducesStructuredFacesWithoutMovingHeat(int fixture,int oldTriangles,int newTriangles)
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);

            var before=new ThermalVisionSurfaceField(bounds,1);var after=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
            {
                float temperature=fixture==0?(x==1?800:300):fixture==1?(x==1&&y==2?300:800):300+x*100+y*10;
                before.Values[before.Index(x,y,z)]=after.Values[after.Index(x,y,z)]=temperature;
            }
            before.BuildFaces(20);after.BuildFaces(20,true);
            Assert.Equal(oldTriangles,before.Face(2,false).Count*2);
            Assert.Equal(newTriangles,after.Face(2,false).Count*2);
            Assert.Equal(before.Minimum,after.Minimum);Assert.Equal(before.Maximum,after.Maximum);
            foreach(var patch in after.Face(2,false))
                for(int y=(int)patch.A.Y;y<=patch.C.Y;y++)for(int x=(int)patch.A.X;x<=patch.C.X;x++)
                {
                    double u=(x-patch.A.X)/(patch.C.X-patch.A.X),v=(y-patch.A.Y)/(patch.C.Y-patch.A.Y);
                    var t=patch.Temperatures;
                    double estimate=PatchEstimate(patch,t,u,v);
                    Assert.InRange(Math.Abs(estimate-after.Values[after.Index(x,y,0)]),0,20.0001);
                }
        }

        [Fact]

        public void ApparentSizeLodKeepsCoverageCloseDetailAndStableThresholds()
        {
            Assert.Equal(16,ThermalVisionViewPolicy.DetailBudget(0,0,false));
            Assert.Equal(16,ThermalVisionViewPolicy.DetailBudget(20,0,false));
            Assert.Equal(64,ThermalVisionViewPolicy.DetailBudget(60,0,false));
            Assert.Equal(256,ThermalVisionViewPolicy.DetailBudget(100,0,false));
            Assert.Equal(512,ThermalVisionViewPolicy.DetailBudget(1000,0,false));
            Assert.Equal(512,ThermalVisionViewPolicy.DetailBudget(1,16,true));
            int prior=16;
            for(int pixels=0;pixels<1000;pixels++)
            {
                int next=ThermalVisionViewPolicy.DetailBudget(pixels,prior,false);
                Assert.InRange(next,prior,512); prior=next;
            }
            for(int pixels=1000;pixels>=0;pixels--)
            {
                int next=ThermalVisionViewPolicy.DetailBudget(pixels,prior,false);
                Assert.InRange(next,16,prior); prior=next;
            }
            for(int i=0;i<100;i++)Assert.Equal(64,ThermalVisionViewPolicy.DetailBudget(i%2==0?63:65,64,false));
        }

        [Fact]

        public void SurfaceSamplingPreservesTrilinearFieldsAcrossAllLatticeShapes()
        {

            var random=new Random(2197);
            for(int nx=1;nx<=4;nx++) for(int ny=1;ny<=4;ny++) for(int nz=1;nz<=4;nz++)
            {
                var bounds=new ThermalVisionRegionPartition.Region(new Vector3D(-13,7,-4),new Vector3D(-13+nx,7+ny,-4+nz),300);

                var field=new ThermalVisionSurfaceField(bounds,1);
                for(int z=0;z<=nz;z++)for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++)
                    field.Values[field.Index(x,y,z)]=AnalyticTemperature(new Vector3D(x,y,z));
                for(int i=0;i<200;i++)
                {

                    var local=new Vector3D(random.NextDouble()*(nx+2)-1,random.NextDouble()*(ny+2)-1,random.NextDouble()*(nz+2)-1);
                    var clamped=Vector3D.Clamp(local,Vector3D.Zero,new Vector3D(nx,ny,nz));
                    Assert.InRange(Math.Abs(field.Sample(bounds.Min+local)-AnalyticTemperature(clamped)),0,.0002);
                }
            }
        }

        private static float AnalyticTemperature(Vector3D p)
        { return (float)(200+11*p.X+7*p.Y+3*p.Z+2*p.X*p.Y+4*p.X*p.Z+5*p.Y*p.Z+6*p.X*p.Y*p.Z); }

        [Fact]

        public void PreparationMakesFairProgressDespiteSustainedDiscoveryOverhead()
        {
            var completed=new int[32]; int cursor=0, baselineSteps=0;
            for(int frame=0;frame<60;frame++)
            {
                double start=3+(frame%5), elapsed=start;
                int visited=0;
                while(ThermalVisionPreparationBudget.CanAdvance(elapsed,start,visited,completed.Length,0))
                {
                    completed[cursor++%completed.Length]++;
                    visited++; elapsed+=.01;
                }
                if(start<2) baselineSteps++;
                Assert.InRange(elapsed-start,2,2.011);
            }
            Assert.Equal(0,baselineSteps);
            foreach(int steps in completed) Assert.True(steps>=360);
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(0,0,0,0,0));
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(0,0,0,32,32));
            Assert.False(ThermalVisionPreparationBudget.CanAdvance(0,0,32*128,32,0));
        }

        [Fact]

        public void CachedHeatPreservesFallbacksAndInvalidatesWhenSourcesChange()
        {
            var box=new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300);

            var empty=new ThermalVisionBlockField(1,.6,box);
            Assert.Equal(270,empty.SampleCached(Vector3D.Zero,270));
            Assert.Equal(400,empty.SampleCached(Vector3D.Zero,400));
            empty.Add(box);

            var far=new Vector3D(100);
            Assert.Equal(300,empty.SampleCached(far,270));
            Assert.Equal(300,empty.SampleCached(far,400));
            Assert.Equal(1,empty.CacheHits);
            empty.Add(new ThermalVisionRegionPartition.Region(far-Vector3D.One,far+Vector3D.One,600));
            Assert.Equal(600,empty.SampleCached(far,270));

            var noTree=new ThermalVisionBlockField(1,.6);
            noTree.Add(box);
            Assert.Equal(270,noTree.SampleCached(far,270));
            Assert.Equal(400,noTree.SampleCached(far,400));
        }

        [Fact]

        public void HeatCacheIsBoundedAndDisablesForSparseQueries()
        {
            var box=new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300);

            var dense=new ThermalVisionBlockField(1,.6,box);

            var sparse=new ThermalVisionBlockField(1,.6,box);
            dense.Add(box);sparse.Add(box);
            for(int i=0;i<10000;i++)
            {

                var p=new Vector3D(i*.01,0,0);
                Assert.Equal(dense.Sample(p,293),dense.SampleCached(p,293));
                Assert.Equal(dense.Sample(p,293),dense.SampleCached(p,293));
                Assert.Equal(sparse.Sample(p,293),sparse.SampleCached(p,293));
            }
            Assert.InRange(dense.CacheEntries,1,4096);
            Assert.Equal(0,sparse.CacheEntries);
        }

        [Fact]

        public void DirectHeatCollectionMatchesCoarseScanPathWithFewerSourceVisits()
        {
            var samples=new System.Collections.Generic.List<ThermalVisionRegionPartition.Region>();
            for(int x=0;x<8;x++) for(int y=0;y<8;y++) for(int z=0;z<8;z++)
            {

                var min=new Vector3D(x,y,z);
                samples.Add(new ThermalVisionRegionPartition.Region(min,min+Vector3D.One,250+(x*19+y*7+z*31)%400));
            }
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(8),0);

            var oldField=new ThermalVisionBlockField(1,.6,bounds);

            var directField=new ThermalVisionBlockField(1,.6,bounds);
            int oldVisits=0, directVisits=0;

            System.Collections.Generic.IEnumerable<ThermalVisionRegionPartition.Region> Source(bool heat)
            {
                foreach(var sample in samples)
                {
                    oldVisits++;
                    if(heat) oldField.Add(sample);
                    yield return sample;
                }
            }
            using(var scan=new ThermalVisionRegionScan(1,128,1,10485760,origin:Vector3D.Zero))
            {
                scan.Start(new[] { Source(false) });
                while(scan.Running) scan.Advance(128);
                Assert.Null(scan.Failure);
                using(var mean=new ThermalVisionRegionScan(scan.CellSize,128,1,average:true,origin:scan.Origin))
                {
                    mean.Start(new[] { Source(true) });
                    while(mean.Running) mean.Advance(128);
                    Assert.Null(mean.Failure);
                }
            }
            foreach(var sample in samples) { directField.Add(sample); directVisits++; }
            Assert.Equal(512,directVisits);
            Assert.True(oldVisits>=directVisits*2);

            var random=new Random(712);
            for(int i=0;i<300;i++)
            {

                var p=new Vector3D(random.NextDouble()*12-2,random.NextDouble()*12-2,random.NextDouble()*12-2);
                Assert.Equal(oldField.Sample(p,293),directField.Sample(p,293));
            }
        }

        [Fact]

        public void OutsideSupportUsesNearestOriginalBlockInsteadOfGridAverage()
        {
            var region=new ThermalVisionRegionPartition.Region(new Vector3D(-20),new Vector3D(20),0);

            var field=new ThermalVisionBlockField(1,.6,region);
            var samples=new System.Collections.Generic.List<ThermalVisionRegionPartition.Region>();

            var random=new Random(817);
            for(int i=0;i<100;i++)
            {

                var centre=new Vector3D(random.NextDouble()*30-15,random.NextDouble()*30-15,random.NextDouble()*30-15);
                var sample=new ThermalVisionRegionPartition.Region(centre-new Vector3D(.5),centre+new Vector3D(.5),300+i*5);
                samples.Add(sample); field.Add(sample);
            }
            for(int i=0;i<200;i++)
            {

                var point=new Vector3D(random.NextDouble()*60-30,random.NextDouble()*60-30,50);
                double best=double.PositiveInfinity; float expected=0;
                foreach(var sample in samples)
                {
                    double distance=Vector3D.DistanceSquared(point,Vector3D.Clamp(point,sample.Min,sample.Max));
                    if(distance<best) { best=distance; expected=sample.Kelvin; }
                }
                Assert.Equal(expected,field.Sample(point,123));
            }
            Assert.Equal(200,field.NearestSamples); Assert.Equal(0,field.Fallbacks);
        }

        [Fact]

        public void CachedPatchTemperaturesMatchOriginalSamplingDuringTransitions()
        {
            var box=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);

            var current=new ThermalVisionSurfaceField(box,1);

            var previous=new ThermalVisionSurfaceField(box,2);
            for(int i=0;i<current.Values.Length;i++) current.Values[i]=300+(i*137)%700;
            for(int i=0;i<previous.Values.Length;i++) previous.Values[i]=250+(i*71)%400;
            current.BuildFaces(20); current.PrepareTransition(previous);
            for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
                foreach(var patch in current.Face(axis,side==1))
                    foreach(float blend in new[]{0f,.1f,.5f,.9f,1f})
                    {
                        var cached=ThermalVisionSurfaceField.PatchTemperatures(patch,blend);
                        Assert.InRange(Math.Abs(cached.X-current.BlendSample(previous,patch.A,blend)),0,.001f);
                        Assert.InRange(Math.Abs(cached.Y-current.BlendSample(previous,patch.B,blend)),0,.001f);
                        Assert.InRange(Math.Abs(cached.Z-current.BlendSample(previous,patch.C,blend)),0,.001f);
                        Assert.InRange(Math.Abs(cached.W-current.BlendSample(previous,patch.D,blend)),0,.001f);
                    }
        }

        [Fact]

        public void CachedExtremaProduceTheSameAutomaticRangeAsEveryLatticeSample()
        {

            var all=new ThermalVisionAutoRange(); var extrema=new ThermalVisionAutoRange();
            var box=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);

            var field=new ThermalVisionSurfaceField(box,1);
            for(int frame=0;frame<40;frame++)
            {
                for(int i=0;i<field.Values.Length;i++) field.Values[i]=frame<20?200+(i*137)%800:350+(i*73)%200;
                field.BuildFaces(20);
                all.BeginSamples(); extrema.BeginSamples();
                foreach(float temperature in field.Values) all.Observe(temperature);
                extrema.Observe(field.Minimum); extrema.Observe(field.Maximum);
                all.Update(1d/60); extrema.Update(1d/60);
                Assert.Equal(all.Low,extrema.Low); Assert.Equal(all.High,extrema.High);
            }
        }

        [Fact]

        public void PreparedTemperatureRefreshBlendsWithoutBlankingOrOvershoot()
        {
            var region=new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300);

            var old=new ThermalVisionSurfaceField(region,1);

            var next=new ThermalVisionSurfaceField(region,1);
            for(int i=0;i<8;i++) { old.Values[i]=300; next.Values[i]=900; }
            Assert.Equal(300f,next.BlendSample(old,new Vector3D(.5),0));
            Assert.Equal(600f,next.BlendSample(old,new Vector3D(.5),.5f));
            Assert.Equal(900f,next.BlendSample(old,new Vector3D(.5),1));
            for(int i=-10;i<=110;i++) Assert.InRange(next.BlendSample(old,new Vector3D(.5),i/100f),300f,900f);
            Assert.Equal(300f,old.Sample(new Vector3D(.5)));
        }

        [Fact]

        public void GridLocalThermalOrderingSurvivesTranslationAndRotation()
        {
            var cells=new System.Collections.Generic.List<ThermalVisionRegionPartition.Region> {
                new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,300),
                new ThermalVisionRegionPartition.Region(new Vector3D(2,0,0),new Vector3D(3,1,1),900) };
            ThermalVisionRegionOrder order;
            Assert.True(ThermalVisionRegionOrder.TryBuild(cells,16,out order));
            var expected=new System.Collections.Generic.List<ThermalVisionRegionPartition.Region>();
            var actual=new System.Collections.Generic.List<ThermalVisionRegionPartition.Region>();

            var localEye=new Vector3D(-10,2,1);
            order.WriteNearToFar(localEye,expected);
            var moved=MatrixD.CreateRotationY(.72); moved.Translation=new Vector3D(10000,-900,500);
            var worldEye=Vector3D.Transform(localEye,moved);
            order.WriteNearToFar(Vector3D.Transform(worldEye,MatrixD.Invert(moved)),actual);
            Assert.Equal(expected.Count,actual.Count);
            for(int i=0;i<expected.Count;i++) Assert.Equal(expected[i],actual[i]);
            Assert.Equal(2,order.LeafCount);
        }

        [Fact]

        public void OriginalBlockFieldKeepsHotspotInsideCoarseGeometry()
        {

            var field = new ThermalVisionBlockField(1, .6);
            for(int x=-2;x<=2;x++) for(int y=-2;y<=2;y++)
            {

                var centre=new Vector3D(x,y,0);
                field.Add(new ThermalVisionRegionPartition.Region(centre-new Vector3D(.5),centre+new Vector3D(.5),x==0&&y==0?900:300));
            }
            var bounds=new ThermalVisionRegionPartition.Region(new Vector3D(-2,-2,0),new Vector3D(2,2,1),324);

            var surface=new ThermalVisionSurfaceField(bounds,1);
            for(int z=0;z<=surface.Steps.Z;z++) for(int y=0;y<=surface.Steps.Y;y++) for(int x=0;x<=surface.Steps.X;x++)
                surface.Values[surface.Index(x,y,z)]=field.Sample(surface.Point(x,y,z),bounds.Kelvin);
            Assert.InRange(surface.Sample(Vector3D.Zero),899.9f,900.01f);
            Assert.InRange(surface.Sample(new Vector3D(2,2,0)),299.99f,300.1f);
            Assert.Equal(25,field.Count);
        }

        [Fact]

        public void OriginalBlockFieldIsContinuousAcrossBucketsAndIsolatesGrids()
        {

            var field=new ThermalVisionBlockField(1,.6);
            field.Add(new ThermalVisionRegionPartition.Region(new Vector3D(3,-.5,-.5),new Vector3D(4,.5,.5),300));
            field.Add(new ThermalVisionRegionPartition.Region(new Vector3D(4,-.5,-.5),new Vector3D(5,.5,.5),900));
            Assert.InRange(field.Sample(new Vector3D(4,0,0),0),599.99f,600.01f);
            Assert.InRange(Math.Abs(field.Sample(new Vector3D(4-1e-6,0,0),0)-field.Sample(new Vector3D(4+1e-6,0,0),0)),0,.01f);
            Assert.Equal(42f,new ThermalVisionBlockField(1,.6).Sample(new Vector3D(4,0,0),42));
            Assert.Equal(42f,field.Sample(new Vector3D(100,0,0),42));
            for(int i=0;i<=100;i++) Assert.InRange(field.Sample(new Vector3D(3.5+i*.01,0,0),600),300f,900f);
        }

        [Fact]

        public void SurfaceLatticeResolvesInteriorPeakAndReducesDistantDetail()
        {
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4,4,4),300);

            var near=new ThermalVisionSurfaceField(bounds,1);

            var far=new ThermalVisionSurfaceField(bounds,100);
            Assert.Equal(new Vector3I(4),near.Steps);
            Assert.Equal(new Vector3I(1),far.Steps);
            for(int z=0;z<=4;z++) for(int y=0;y<=4;y++) for(int x=0;x<=4;x++)
                near.Values[near.Index(x,y,z)]=300+10*x+20*y+30*z;
            Assert.Equal(360f,near.Sample(new Vector3D(1,1,1)));
            Assert.Equal(540f,near.Sample(new Vector3D(4,4,4)));
            near.BuildFaces(20);
            Assert.Single(near.Face(2,false));
            near.Values[near.Index(2,2,0)]=900;
            Assert.Equal(900f,near.Sample(new Vector3D(2,2,0)));
            near.BuildFaces(20);
            Assert.True(near.Face(2,false).Count>1);
            bool hotspotVertex=false;
            foreach(var patch in near.Face(2,false))
                if(near.Sample(patch.A)==900 || near.Sample(patch.B)==900 || near.Sample(patch.C)==900 || near.Sample(patch.D)==900) hotspotVertex=true;
            Assert.True(hotspotVertex);
        }

        [Fact]

        public void SmoothFieldPreservesConstantTemperatureAndGridIsolation()
        {
            var a = new ThermalVisionRegionPartition.Region(Vector3D.Zero, Vector3D.One, 300);
            var b = new ThermalVisionRegionPartition.Region(Vector3D.Zero, Vector3D.One, 900);

            var cold = new ThermalVisionSmoothField(new[] { a });

            var hot = new ThermalVisionSmoothField(new[] { b });
            Assert.Equal(300f, cold.Sample(Vector3D.One * .5, 0));
            Assert.Equal(900f, hot.Sample(Vector3D.One * .5, 0));
            Assert.Equal(123f, cold.Sample(new Vector3D(100), 123));
        }

        [Fact]

        public void SmoothFieldBlendsNeighbourTemperaturesWithoutExceedingTheirRange()
        {
            var a = new ThermalVisionRegionPartition.Region(Vector3D.Zero, Vector3D.One, 300);
            var b = new ThermalVisionRegionPartition.Region(new Vector3D(1,0,0), new Vector3D(2,1,1), 900);

            var field = new ThermalVisionSmoothField(new[] { a, b });
            Assert.Equal(600f, field.Sample(new Vector3D(1,.5,.5), 0));
            float previous = 0;
            for(int i=0;i<=20;i++)
            {
                float value=field.Sample(new Vector3D(i*.1,.5,.5),300);
                Assert.InRange(value,300,900);
                Assert.True(value>=previous);
                previous=value;
            }
        }

        [Fact]

        public void SmoothCellCapsAndFacesUseTheSameLinearTemperatureField()
        {
            var region = new ThermalVisionRegionPartition.Region(new Vector3D(-2),new Vector3D(2),300);
            var values=new float[8];
            for(int i=0;i<8;i++)values[i]=(float)(500+20*ThermalVisionSmoothField.Corner(region,i).X);
            Assert.Equal(500f,ThermalVisionSmoothField.Interpolate(region,values,Vector3D.Zero));
            Assert.Equal(520f,ThermalVisionSmoothField.Interpolate(region,values,new Vector3D(1,.6,-.4)));
            Assert.Equal(540f,ThermalVisionSmoothField.Interpolate(region,values,new Vector3D(2,0,0)));
        }

        [Fact]

        public void MeshCacheRetainsWarmModelsUnderTrianglePressure()
        {

            var cache = new ThermalVisionMeshCache<string>(4, 12);

            var mesh = new ThermalVisionMesh(new ThermalVisionTriangle[4], new ThermalVisionMeshBatch[0]);
            cache.Add("cold", mesh, true);
            cache.Add("warm", mesh, false);
            cache.Add("other", mesh, true);
            ThermalVisionMesh found;
            bool partial;
            Assert.True(cache.TryGetValue("warm", out found, out partial));
            Assert.False(partial);
            cache.Add("new", mesh, false);
            Assert.False(cache.TryGetValue("cold", out found, out partial));
            Assert.True(cache.TryGetValue("warm", out found, out partial));
            Assert.True(cache.TryGetValue("other", out found, out partial));
            Assert.True(partial);
            Assert.Equal(3, cache.Count);
            Assert.Equal(12, cache.Triangles);
            Assert.Equal(1, cache.Evictions);
        }

        [Fact]

        public void MeshCacheBoundsEmptyEntriesAndRejectsOversizeWithoutEviction()
        {

            var cache = new ThermalVisionMeshCache<string>(2, 4);

            var empty = new ThermalVisionMesh(new ThermalVisionTriangle[0], new ThermalVisionMeshBatch[0]);
            cache.Add("first", empty, true);
            cache.Add("second", empty, true);
            cache.Add("third", empty, true);
            Assert.Equal(2, cache.Count);
            Assert.Equal(1, cache.Evictions);
            Assert.Throws<ArgumentException>(() => cache.Add("oversize",

                new ThermalVisionMesh(new ThermalVisionTriangle[5], new ThermalVisionMeshBatch[0]), false));
            Assert.Equal(2, cache.Count);
            Assert.Equal(1, cache.Evictions);
            cache.Clear();
            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.Triangles);
            Assert.Equal(0, cache.Evictions);
        }

        [Fact]

        public void DepthLayersAreStrictlyNearToFarAndEndWithNeutralBackground()
        {
            double previous = 0;
            for (int i = 0; i < ThermalVisionDepthLayers.Count; i++)
            {
                double d = ThermalVisionDepthLayers.Distance(i, .0525);
                Assert.True(d > previous);
                previous = d;
            }
            Assert.Equal(5000, previous, 8);
            Assert.Equal(new Vector4(0, 0, 0, 1), ThermalVisionDepthLayers.Colour(
                ThermalVisionDepthLayers.Count - 1, ThermalVisionState.Mode.Cividis));
            Assert.Throws<ArgumentException>(() => ThermalVisionDepthLayers.Distance(0, double.NaN));
            Assert.Throws<ArgumentException>(() => ThermalVisionDepthLayers.Distance(0, 5000));
        }

        [Theory]
        [InlineData(16.0 / 9, 0, 0)]
        [InlineData(32.0 / 9, .15, -.1)]
        [InlineData(4.0 / 3, -.2, .1)]

        public void DepthPlanesCoverTheActualProjectionIncludingOffsetAndCameraRotation(double aspect, double ox, double oy)
        {
            MatrixD projection = MatrixD.CreatePerspectiveFieldOfView(Math.PI / 3, aspect, .05, 20000);
            projection.M31 = ox; projection.M32 = oy;
            MatrixD world = MatrixD.CreateWorld(new Vector3D(100, -70, 20), Vector3D.Normalize(new Vector3D(1, 2, -3)), Vector3D.Up);
            MatrixD view = MatrixD.Invert(world);
            foreach (int i in new[] { 0, 12, ThermalVisionDepthLayers.Count - 1 })
            {
                Vector3D centre; float width, height;
                ThermalVisionDepthLayers.Plane(ThermalVisionDepthLayers.Distance(i, .0525), projection, world,
                    out centre, out width, out height);
                foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                {
                    Vector3D corner = centre + world.Right * width * x + world.Up * height * y;
                    Vector4D clip = Vector4D.Transform(new Vector4D(corner, 1), view * projection);
                    Assert.InRange(Math.Abs(clip.X / clip.W - x), 0, .000001);
                    Assert.InRange(Math.Abs(clip.Y / clip.W - y), 0, .000001);
                }
            }
        }

        [Fact]

        public void DistantDepthSilhouettesRemainDistinctFromBlackBackground()
        {
            var distant = ThermalVisionDepthLayers.Colour(ThermalVisionDepthLayers.Count - 2,
                ThermalVisionState.Mode.WhiteHot);
            Assert.InRange(distant.X, .3f, .6f);
            Assert.Equal(1, distant.W);
            Assert.Equal(0, ThermalVisionDepthLayers.Colour(ThermalVisionDepthLayers.Count - 1,
                ThermalVisionState.Mode.WhiteHot).X);
        }

        [Fact]

        public void OrderedDepthCompositionSelectsLastVisibleLayerAndWrongOrderDestroysDepth()
        {
            double a = ThermalVisionDepthLayers.Distance(22, .0525);
            double b = ThermalVisionDepthLayers.Distance(23, .0525);
            double surface = (a + b) * .5;
            double colour = .123, reversed = .123;
            for (int j = 0; j < ThermalVisionDepthLayers.Count; j++)
            {
                foreach (bool reverse in new[] { false, true })
                {
                    int i = reverse ? ThermalVisionDepthLayers.Count - 1 - j : j;
                    double distance = ThermalVisionDepthLayers.Distance(i, .0525);
                    if (distance > surface) continue;
                    double alpha = Math.Min(1, (surface - distance) / (.02 * .3));
                    double source = 1.0 - (double)i / (ThermalVisionDepthLayers.Count - 1);
                    if (reverse) reversed = source * alpha + reversed * (1 - alpha);
                    else colour = source * alpha + colour * (1 - alpha);
                }
            }
            Assert.Equal(1.0 - 22.0 / (ThermalVisionDepthLayers.Count - 1), colour, 10);
            Assert.Equal(1, reversed, 10);
        }


        private static ThermalVisionViewpoint SuitView()
        {
            return new ThermalVisionViewpoint
            {
                IsClient = true, HasSessionCamera = true, HasLocalPlayer = true,
                ControllerEntityId = 42, CharacterEntityId = 42, ControlledEntityId = 42,
                FirstPerson = true,
            };
        }


        private static ThermalVisionViewpoint CameraView()
        {

            var view = SuitView();
            view.ControllerEntityId = 100;
            view.ControllerIsCamera = true;
            view.CameraActiveLocal = true;
            view.CameraWorking = true;
            view.FirstPerson = false;
            return view;
        }

        [Fact]

        public void OnlyOnFootFirstPersonAndLocallyActiveWorkingCamerasQualify()
        {
            Assert.Equal(0, default(ThermalVisionViewpoint).EligibleEntityId);
            Assert.Equal(42, SuitView().EligibleEntityId);
            Assert.Equal(100, CameraView().EligibleEntityId);

            var view = SuitView();
            view.FirstPerson = false;
            Assert.Equal(0, view.EligibleEntityId);

            view = SuitView();
            view.ControlledEntityId = 99;
            Assert.Equal(0, view.EligibleEntityId);

            view = SuitView();
            view.ControllerEntityId = 99;
            Assert.Equal(0, view.EligibleEntityId);

            view = SuitView();
            view.CharacterClosing = true;
            Assert.Equal(0, view.EligibleEntityId);

            view = SuitView();
            view.CharacterEntityId = 0;
            Assert.Equal(0, view.EligibleEntityId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]

        public void MissingClientContextOrDeathRejectsBothKindsOfView(bool camera)
        {

            var original = camera ? CameraView() : SuitView();
            var view = original;
            view.IsClient = false;
            Assert.Equal(0, view.EligibleEntityId);
            view = original;
            view.HasSessionCamera = false;
            Assert.Equal(0, view.EligibleEntityId);
            view = original;
            view.HasLocalPlayer = false;
            Assert.Equal(0, view.EligibleEntityId);
            view = original;
            view.PlayerCharacterDead = true;
            Assert.Equal(0, view.EligibleEntityId);
            view = original;
            view.ControllerEntityId = 0;
            Assert.Equal(0, view.EligibleEntityId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]

        public void CameraLossSuspendsWithoutClearingSelection(int cause)
        {

            var view = CameraView();

            var state = new ThermalVisionState();
            Assert.True(state.Enable(ThermalVisionState.Mode.Cividis, view.EligibleEntityId));
            if (cause == 0) view.CameraActiveLocal = false;
            if (cause == 1) view.CameraWorking = false;
            if (cause == 2) view.CameraClosing = true;
            Assert.Equal(0, view.EligibleEntityId);
            Assert.False(state.Validate(view.EligibleEntityId));
            Assert.True(state.Validate(CameraView().EligibleEntityId));
            Assert.True(state.Enable(ThermalVisionState.Mode.WhiteHot, CameraView().EligibleEntityId));
        }

        [Fact]

        public void LosingViewPreservesSelectionUntilExplicitOff()
        {

            var state = new ThermalVisionState();
            Assert.True(state.Enable(ThermalVisionState.Mode.Cividis, 42));
            Assert.True(state.Validate(42));
            Assert.False(state.Validate(0));
            Assert.True(state.Validate(42));
            Assert.True(state.Enable(ThermalVisionState.Mode.WhiteHot, 42));
            Assert.True(state.Validate(42));
            state.Disable();
            Assert.False(state.Validate(42));
            Assert.False(state.Validate(43));
        }

        [Fact]

        public void SwitchingDirectlyBetweenEligibleCamerasPreservesSelection()
        {

            var state = new ThermalVisionState();
            state.Enable(ThermalVisionState.Mode.Cividis, 42);
            Assert.True(state.Validate(43));
            Assert.True(state.Validate(42));
            Assert.False(state.Enable(ThermalVisionState.Mode.Cividis, 0));
            Assert.Equal(ThermalVisionState.Mode.Off, state.Current);
        }

        [Theory]
        [InlineData(ThermalVisionState.Mode.Cividis)]
        [InlineData(ThermalVisionState.Mode.WhiteHot)]

        public void EveryStepInTheWindowGetsBrighter(ThermalVisionState.Mode mode)
        {
            double previous = -1;
            for (int i = 0; i <= 4096; i++)
            {
                float temperature = ThermalVisionPalette.LowKelvin
                    + i / 4096f * (ThermalVisionPalette.HighKelvin - ThermalVisionPalette.LowKelvin);
                Vector3 colour;
                Assert.True(ThermalVisionPalette.TrySample(temperature, mode, out colour));
                Assert.InRange(colour.X, 0f, 1f);
                Assert.InRange(colour.Y, 0f, 1f);
                Assert.InRange(colour.Z, 0f, 1f);

                double y = .2126 * Decode(colour.X) + .7152 * Decode(colour.Y) + .0722 * Decode(colour.Z);
                Assert.True(y > previous, "Brightness reversal at sample " + i);
                previous = y;
                if (mode == ThermalVisionState.Mode.WhiteHot)
                    Assert.Equal(new Vector3(colour.X), colour);
            }
        }

        [Fact]

        public void MissingDataDoesNotTurnIntoAColdColour()
        {
            Vector3 colour;
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
                Assert.False(ThermalVisionPalette.TrySample(invalid, ThermalVisionState.Mode.Cividis, out colour));
            Assert.False(ThermalVisionPalette.TrySample(500f, ThermalVisionState.Mode.Off, out colour));
        }

        [Fact]

        public void FiniteTemperaturesOutsideTheWindowClampToItsEndpoints()
        {
            Vector3 low, high, below, above;
            var mode = ThermalVisionState.Mode.Cividis;
            ThermalVisionPalette.TrySample(ThermalVisionPalette.LowKelvin, mode, out low);
            ThermalVisionPalette.TrySample(ThermalVisionPalette.HighKelvin, mode, out high);
            ThermalVisionPalette.TrySample(0f, mode, out below);
            ThermalVisionPalette.TrySample(10000f, mode, out above);
            Assert.Equal(low, below);
            Assert.Equal(high, above);
            Assert.InRange(low.Z, .30f, .31f);
            Assert.InRange(high.X, .99f, 1f);
        }

        [Theory]
        [InlineData(ThermalVisionState.Mode.Cividis)]
        [InlineData(ThermalVisionState.Mode.WhiteHot)]

        public void ColdTestTemperaturesHaveContrastAndCustomWindowsAreValidated(ThermalVisionState.Mode mode)
        {
            Vector3 cold, warm, custom;
            Assert.True(ThermalVisionPalette.TrySample(236.90f, mode, out cold));
            Assert.True(ThermalVisionPalette.TrySample(263.50f, mode, out warm));
            Assert.True(Decode(warm.X) + Decode(warm.Y) + Decode(warm.Z)

                > Decode(cold.X) + Decode(cold.Y) + Decode(cold.Z));
            Assert.True(ThermalVisionPalette.TrySample(300f, mode, 250f, 350f, out custom));
            Assert.True(ThermalVisionPalette.TrySample(273.15f, mode, out warm));
            Assert.Equal(warm, custom);
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, 350f, 400f })
                Assert.False(ThermalVisionPalette.TrySample(300f, mode, invalid, 350f, out custom));
            Assert.False(ThermalVisionPalette.TrySample(300f, mode, 250f, float.NaN, out custom));
            Assert.False(ThermalVisionPalette.TrySample(300f, mode, 250f, float.PositiveInfinity, out custom));
        }

        [Fact]

        public void SceneRangeRejectsMissingDataAndPreservesExposureForEmptyViews()
        {

            var range = new ThermalVisionAutoRange();
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f, 100001f })
                Assert.False(range.Observe(invalid));
            Assert.False(range.Update(1));
            Assert.False(range.HasSamples);
            range.Observe(208.6f); range.Observe(676.05f); range.Update(0);
            Assert.True(range.Low < 208.6f);
            Assert.True(range.High > 676.05f);
            float low = range.Low, high = range.High;
            range.BeginSamples();
            Assert.False(range.Update(20));
            Assert.Equal(low, range.Low); Assert.Equal(high, range.High);
            range.Reset();
            Assert.False(range.HasSamples);
            range.Observe(0); range.Update(0);
            Assert.Equal(0f, range.Low);
            Assert.True(range.High - range.Low >= 50);
        }

        [Fact]

        public void SceneExposureExpandsQuicklyAndContractsGradually()
        {

            var range = new ThermalVisionAutoRange();
            range.Observe(300); range.Update(0);
            float initialHigh = range.High;
            range.BeginSamples(); range.Observe(300); range.Observe(675);
            range.Update(.4);
            Assert.InRange(range.High, initialHigh + 200, 699);
            float expandedHigh = range.High;
            range.BeginSamples(); range.Observe(300); range.Update(.4);
            Assert.InRange(range.High, expandedHigh - 60, expandedHigh - 1);
            for (int i = 0; i < 100; i++) range.Update(.2);
            Assert.InRange(range.High, 325, 326);
            Assert.True(range.High - range.Low >= 50);
        }

        [Fact]

        public void ExposureResponseIsIndependentOfFrameRate()
        {

            var a = new ThermalVisionAutoRange(); var b = new ThermalVisionAutoRange();
            a.Observe(300); b.Observe(300); a.Update(0); b.Update(0);
            a.BeginSamples(); b.BeginSamples(); a.Observe(675); b.Observe(675);
            a.Update(1);
            for (int i = 0; i < 60; i++) b.Update(1.0 / 60);
            Assert.InRange(Math.Abs(a.Low - b.Low), 0, .001);
            Assert.InRange(Math.Abs(a.High - b.High), 0, .001);
            float high = a.High;
            a.Update(double.NaN); a.Update(double.PositiveInfinity); a.Update(-1);
            Assert.Equal(high, a.High);
        }

        [Fact]

        public void EarlyBackfaceRejectionMatchesWorldWindingAfterRotationScaleAndTranslation()
        {

            Vector3 a = new Vector3(1, 2, 3), b = a + Vector3.UnitX, c = a + Vector3.UnitY;
            MatrixD world = MatrixD.CreateScale(2, 3, 4) * MatrixD.CreateRotationY(1.1)
                * MatrixD.CreateTranslation(10000, -5000, 20000);
            Vector3D wa = Vector3D.Transform(a, world), wb = Vector3D.Transform(b, world), wc = Vector3D.Transform(c, world);
            foreach (float side in new[] { -2f, 2f })
            {
                Vector3D eye = Vector3D.Transform(a + Vector3.UnitZ * side, world);
                Vector3D localEye;
                Assert.True(ThermalVisionGeometry.TryGetLocalEye(world, eye, out localEye));
                bool worldBackface = Vector3D.Dot(Vector3D.Cross(wb - wa, wc - wa), eye - wa) <= 0;
                Assert.Equal(worldBackface, ThermalVisionGeometry.IsBackFacing(Vector3.Cross(b - a, c - a), a, localEye));
            }
            Vector3D unused;
            Assert.False(ThermalVisionGeometry.TryGetLocalEye(MatrixD.CreateScale(-1), Vector3D.Zero, out unused));
            Assert.False(ThermalVisionGeometry.TryGetLocalEye(MatrixD.CreateScale(0), Vector3D.Zero, out unused));
            Assert.False(ThermalVisionGeometry.IsBackFacing(Vector3.Zero, a, Vector3D.Zero));
        }

        [Fact]

        public void LinearConversionMatchesStandardReferenceValues()
        {
            Vector3 converted = ThermalVisionPalette.ToLinear(new Vector3(0f, .5f, 1f));
            Assert.Equal(0f, converted.X);
            Assert.Equal(.214041f, converted.Y, 5);
            Assert.Equal(1f, converted.Z);
        }


        private static double Decode(double value)
        {
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
    }
}

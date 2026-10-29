using System.Collections.Generic;
using Thermodynamics.Presentation;
using VRageMath;
using Region=Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Tests
{
    /// <summary>One block crosses finite, over-range, missing and recovered input states without inventing heat.</summary>
    public class ThermalVisionTemperatureDataTests
    {
        [Fact]
        public void TemperatureAppearingAfterEmptyDetailScanProducesDrawableCoarseGeometry()
        {
            // The first pass observed no usable block. A temperature arrives
            // before the later mean scan, while topology remains unchanged.
            var detail=new ThermalVisionBlockDetail(4,Vector3D.Zero);
            var block=new Region(Vector3D.Zero,Vector3D.One,350);
            using(var mean=new ThermalVisionRegionScan(1,1,1,average:true))
            {
                Assert.True(mean.Start(new List<IEnumerable<Region>> { new Region[0] }));
                for(int slices=0;mean.Running;slices++) { mean.Advance(4); Assert.True(slices<8); }
                Assert.Null(mean.Failure);
                Assert.Empty(detail.Build(mean,true));

                Assert.True(mean.Start(new List<IEnumerable<Region>> { new[] { block } }));
                for(int slices=0;mean.Running;slices++) { mean.Advance(4); Assert.True(slices<8); }
                Assert.Null(mean.Failure);
                var cells=detail.Build(mean,true);
                Assert.Single(cells); Assert.False(detail.AllExact);
                ThermalVisionRegionOrder order;
                Assert.True(ThermalVisionRegionOrder.TryBuild(cells,4,out order));
                Assert.Equal(1,order.LeafCount);
                var visible=new List<Region>();
                order.WriteNearToFar(new Vector3D(-1,.5,.5),visible);
                Assert.Single(visible);
                Assert.Equal(block.Min,visible[0].Min); Assert.Equal(block.Max,visible[0].Max);
                Assert.Equal(block.Kelvin,visible[0].Kelvin);
                Region recovered;
                Assert.True(order.TryFind(new Vector3D(.5),out recovered));
                Assert.Equal(block.Kelvin,recovered.Kelvin);
            }
        }

        [Fact]
        public void FiniteOverRangeHeatSaturatesWhileEmptySamplesHaveNoMeanOrExposure()
        {
            var range=new ThermalVisionAutoRange();
            Assert.True(range.Observe(300)); Assert.True(range.Update(0));
            float low=range.Low,high=range.High;
            Vector3 hottest;
            Assert.True(ThermalVisionPalette.TrySample(400,ThermalVisionState.Mode.WhiteHot,250,400,out hottest));
            float?[] readings={300f,1e10f,float.MaxValue,null,float.NaN,float.PositiveInfinity,float.NegativeInfinity,300f};
            foreach(var reading in readings)
            {
                var data=new ThermalVisionTemperatureData();
                float display;
                if(reading.HasValue) data.Add(reading.Value,out display);
                float mean;
                range.BeginSamples();
                if(!data.TryMean(out mean))
                {
                    Assert.Equal(0,data.Count); Assert.True(float.IsNaN(mean));
                    Assert.False(range.Update(0));
                }
                else
                {
                    Assert.Equal(1,data.Count);
                    bool overRange=reading.Value>ThermalVisionTemperatureData.MaximumMeasuredKelvin;
                    Assert.Equal(overRange?ThermalVisionTemperatureData.OverRangeKelvin:reading.Value,mean);
                    var box=new Region(Vector3D.Zero,Vector3D.One,mean);
                    using(var scan=new ThermalVisionRegionScan(1,1,1))
                    {
                        Assert.True(scan.Start(new List<IEnumerable<Region>> { new[] { box } }));
                        for(int slices=0;scan.Running;slices++) { scan.Advance(4); Assert.True(slices<8); }
                        Assert.Null(scan.Failure); Assert.Equal(1,scan.Count);
                        var field=new ThermalVisionBlockField(1,.6,box,1);
                        field.Add(scan[0]);
                        Assert.Equal(mean,field.SampleCached(new Vector3D(.5),300));
                    }
                    Vector3 colour;
                    Assert.True(ThermalVisionPalette.TrySample(mean,ThermalVisionState.Mode.WhiteHot,250,400,out colour));
                    if(overRange) Assert.Equal(hottest,colour);
                    Assert.Equal(!overRange,range.Observe(mean));
                    Assert.False(range.Update(0));
                }
                Assert.Equal(low,range.Low); Assert.Equal(high,range.High);
            }
        }
    }
}

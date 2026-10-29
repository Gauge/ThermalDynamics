using System;
using Thermodynamics.Presentation;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Small volume/field fixtures check atomic triangle admission, fair shares and complete fallback surfaces.</summary>
    public class ThermalVisionFrameBudgetTests
    {
        [Fact]
        public void CompleteVolumesAreAdmittedAtomicallyAndKeepTheirExit()
        {
            var field=new ThermalVisionSurfaceField(
                new ThermalVisionRegionPartition.Region(Vector3D.Zero,Vector3D.One,500),1);
            for(int i=0;i<field.Values.Length;i++) field.Values[i]=500;
            field.BuildFaces(20); field.PairedFaceMask=63;
            // Six paired faces still require both entry and exit; a rejected
            // neighbor cannot supply the exit. Caps also consume the same budget.
            int envelope=ThermalVisionFrameBudget.CellTriangles(field,new Vector3D(-1),0);
            Assert.Equal(12,envelope);
            Assert.Equal(19,ThermalVisionFrameBudget.CellTriangles(field,new Vector3D(-1),7));
            var budget=new ThermalVisionFrameBudget(); budget.Reset(11);
            Assert.False(budget.Take(envelope));
            Assert.Equal(11,budget.Remaining);
            budget.Reset(12);
            Assert.True(budget.Take(envelope));
            Assert.False(budget.Take(envelope));
            // An interior camera needs all closing faces even with no entries.
            Assert.Equal(12,ThermalVisionFrameBudget.CellTriangles(field,new Vector3D(.5),0));
        }

        [Fact]
        public void UnusedSharesCarryForwardAndExpensiveCellsCannotStarveOtherGrids()
        {
            var budget=new ThermalVisionFrameBudget(); budget.Reset(12);
            budget.BeginSlice(3);
            Assert.False(budget.Take(5));
            Assert.Equal(12,budget.Remaining);
            budget.BeginSlice(2);
            Assert.True(budget.Take(6));
            budget.BeginSlice(1);
            Assert.True(budget.Take(6));
            Assert.False(budget.Take(1));
            Assert.Equal(0,budget.Remaining);
            Assert.True(budget.Limited);
            budget.Reset(2);
            Assert.False(budget.Limited);
            Assert.Throws<ArgumentException>(()=>budget.Take(0));
        }

        [Theory]
        [InlineData(1,512)]
        [InlineData(2,256)]
        [InlineData(4,128)]
        [InlineData(32,16)]
        [InlineData(1000,16)]
        public void FleetPreparationDoesNotGiveEveryGridTheMaximum(int grids,int ceiling)
        { Assert.Equal(ceiling,ThermalVisionViewPolicy.FleetDetailCeiling(grids)); }

        [Fact]
        public void NativeContextUsesOrderedOpaqueNonThermalBands()
        {
            double prior=0;
            for(int i=0;i<ThermalVisionContext.Layers;i++)
            {
                double distance=ThermalVisionContext.Distance(i,.05,15000);
                Assert.InRange(distance,.05,14999.99);
                Assert.True(distance>prior); prior=distance;
                Vector4 colour=ThermalVisionContext.Colour(i);
                Assert.Equal(1,colour.W);
                Assert.True(colour.Z>colour.Y && colour.Y>colour.X);
            }
            Assert.Throws<ArgumentException>(()=>ThermalVisionContext.Distance(0,.05,double.PositiveInfinity));
        }

        [Fact]
        public void UniformFastPathKeepsBothFadeEndpointsAndNonuniformPriorDetail()
        {
            var bounds=new ThermalVisionRegionPartition.Region(new Vector3D(-2),new Vector3D(2),300);
            var current=new ThermalVisionSurfaceField(bounds,1);
            var previous=new ThermalVisionSurfaceField(bounds,1);
            for(int i=0;i<current.Values.Length;i++) { current.Values[i]=500; previous.Values[i]=300; }
            current.BuildFaces(20,true,previous,20); current.PrepareTransition(previous);
            for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
            {
                var patch=Assert.Single(current.Face(axis,side==1));
                Assert.Equal(new Vector4(400),ThermalVisionSurfaceField.PatchTemperatures(patch,.5f));
            }
            previous.Values[previous.Index(1,2,0)]=900;
            current.BuildFaces(20,true,previous,20);
            Assert.True(current.Face(2,false).Count>1);
            // The budget fallback is explicit spatial coarsening, while its
            // four corners and temporal endpoints retain the measured samples.
            var coarse=current.WholeFace(2,false,previous);
            Assert.Equal(bounds.Min,coarse.A);
            Assert.Equal(new Vector3D(bounds.Max.X,bounds.Max.Y,bounds.Min.Z),coarse.C);
            Assert.Equal(new Vector4(500),coarse.Temperatures);
            Assert.Equal(new Vector4(300),coarse.PreviousTemperatures);
        }
    }
}

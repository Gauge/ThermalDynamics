using Thermodynamics.Presentation;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Three tiny changing lattices compare shared planning scratch with fresh scratch, including
    /// prior-temperature endpoints, patch order, alternate diagonals and sample-only prior fields.
    /// </summary>
    public class ThermalVisionSurfaceScratchTests
    {
        [Fact]
        public void ReusedPlanningScratchPreservesEveryPatchAcrossChangingLattices()
        {
            var scratch=new ThermalVisionSurfaceField.BuildScratch();
            var bounds=new ThermalVisionRegionPartition.Region(Vector3D.Zero,new Vector3D(4),300);
            for(int fixture=0;fixture<3;fixture++)
            {
                var previous=new ThermalVisionSurfaceField(bounds,1);
                var expected=new ThermalVisionSurfaceField(bounds,1);
                var actual=new ThermalVisionSurfaceField(bounds,1);
                for(int z=0;z<=4;z++) for(int y=0;y<=4;y++) for(int x=0;x<=4;x++)
                {
                    int index=actual.Index(x,y,z);
                    float value=fixture==0?(x==1?800:300):fixture==1?(y==1&&z==2?300:800):300;
                    actual.Values[index]=expected.Values[index]=value;
                    previous.Values[index]=fixture==2?(z==1?800:300):value;
                }
                expected.BuildFaces(20,true,previous,20);
                actual.BuildFaces(20,true,previous,20,scratch);
                expected.PrepareTransition(previous); actual.PrepareTransition(previous);
                Assert.True(expected.SavedPatches>0);
                Assert.Equal(expected.SavedPatches,actual.SavedPatches);
                Assert.Equal(expected.Minimum,actual.Minimum); Assert.Equal(expected.Maximum,actual.Maximum);
                for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
                {
                    var fresh=expected.Face(axis,side==1); var reused=actual.Face(axis,side==1);
                    Assert.Equal(fresh.Count,reused.Count);
                    for(int i=0;i<fresh.Count;i++)
                    {
                        Assert.Equal(fresh[i].A,reused[i].A); Assert.Equal(fresh[i].B,reused[i].B);
                        Assert.Equal(fresh[i].C,reused[i].C); Assert.Equal(fresh[i].D,reused[i].D);
                        Assert.Equal(fresh[i].Temperatures,reused[i].Temperatures);
                        Assert.Equal(fresh[i].PreviousTemperatures,reused[i].PreviousTemperatures);
                        Assert.Equal(fresh[i].AlternateDiagonal,reused[i].AlternateDiagonal);
                    }
                }
                Assert.Null(previous.Face(0,false));
            }
        }
    }
}

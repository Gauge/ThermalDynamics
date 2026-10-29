using Thermodynamics.Presentation;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Whole-grid solid-box occlusion preserves openings, partial views and moving targets.</summary>
    public class ThermalVisionOcclusionTests
    {

        private static readonly BoundingBoxD Wall=new BoundingBoxD(new Vector3D(-1,-1,2),new Vector3D(1,1,3));

        [Fact]
        public void ClippedNativeFrontCannotOccludeThroughItsUnclippedInset()
        {
            var full=new BoundingBoxD(new Vector3D(-1),new Vector3D(1));
            var inset=new BoundingBoxD(new Vector3D(-.8),new Vector3D(.8));
            var eye=new Vector3D(-1.01,0,0);
            var target=new BoundingBoxD(new Vector3D(2,-.1,-.1),new Vector3D(2.2,.1,.1));
            Assert.True(ThermalVisionOcclusion.HiddenLocal(eye,target,inset));
            Assert.True(ThermalVisionOcclusion.InFrontOfNearPlane(inset,eye,Vector3D.UnitX,.05));
            Assert.False(ThermalVisionOcclusion.InFrontOfNearPlane(full,eye,Vector3D.UnitX,.05));
            Assert.False(ThermalVisionOcclusion.InFrontOfNearPlane(full,new Vector3D(-.9,0,0),Vector3D.UnitX,.05));
        }

        [Fact]
        public void CachedCandidateUsesCurrentCameraPlaneAfterRigidMotion()
        {
            var full=new BoundingBoxD(new Vector3D(-1),new Vector3D(1));
            var cache=new ThermalVisionOcclusion.CandidateCache();
            var cameraCell=new Vector3I(-1,0,0);
            cache.Begin(20,1,cameraCell); cache.Positions.Add(Vector3I.Zero);
            var grid=MatrixD.CreateRotationY(.37)*MatrixD.CreateTranslation(1e6,-2e6,3e6);
            var inverse=MatrixD.Invert(grid);
            var worldForward=Vector3D.TransformNormal(Vector3D.UnitX,grid);
            var worldUp=Vector3D.TransformNormal(Vector3D.UnitY,grid);
            foreach(double x in new[] {-1.01,-1.1})
            {
                Assert.True(cache.CanReuse(21,1,cameraCell));
                var worldEye=Vector3D.Transform(new Vector3D(x,0,0),grid);
                var localCamera=MatrixD.CreateWorld(worldEye,worldForward,worldUp)*inverse;
                double closest=double.PositiveInfinity;
                for(int corner=0;corner<8;corner++)
                {
                    var point=new Vector3D((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    closest=System.Math.Min(closest,Vector3D.Dot(Vector3D.Transform(point,grid)-worldEye,worldForward));
                }
                bool eligible=ThermalVisionOcclusion.InFrontOfNearPlane(full,localCamera.Translation,localCamera.Forward,.05);
                Assert.Equal(closest>.05,eligible);
                Assert.Equal(x<-1.05,eligible);
            }
            Assert.Single(cache.Positions);
        }

        [Fact]
        public void PreparedTargetMatchesOriginalForMovedAndRotatedBlockers()
        {
            var target=new BoundingBoxD(new Vector3D(-1,-1,10),new Vector3D(1,1,12));
            var solids=new[] { Wall,
                new BoundingBoxD(new Vector3D(5,-1,2),new Vector3D(6,1,3)),
                new BoundingBoxD(new Vector3D(-1),new Vector3D(1)),target };
            int hidden=0,visible=0;
            foreach(var eye in new[] {Vector3D.Zero,new Vector3D(8,0,0)})
                foreach(var inverse in new[] {MatrixD.Identity,MatrixD.CreateRotationY(.35),MatrixD.CreateTranslation(-2,0,0)})
            {
                var prepared=new ThermalVisionOcclusion.PreparedTarget(eye,target,inverse);
                foreach(var solid in solids)
                {
                    bool expected=ThermalVisionOcclusion.Hidden(eye,target,inverse,solid);
                    Assert.Equal(expected,prepared.Hidden(solid));
                    if(expected)hidden++;else visible++;
                }
                int transformed=prepared.CornerTransforms;
                foreach(var solid in solids)
                    Assert.Equal(ThermalVisionOcclusion.Hidden(eye,target,inverse,solid),prepared.Hidden(solid));
                Assert.Equal(transformed,prepared.CornerTransforms);
                Assert.InRange(transformed,1,8);
            }
            Assert.True(hidden>0); Assert.True(visible>0);
        }

        [Fact]
        public void PreparedTargetTransformsCornersLazilyAndReusesThemAcrossSolids()
        {
            var target=new BoundingBoxD(new Vector3D(-1,-1,10),new Vector3D(1,1,12));
            var prepared=new ThermalVisionOcclusion.PreparedTarget(Vector3D.Zero,target,MatrixD.Identity);
            Assert.False(prepared.Hidden(new BoundingBoxD(new Vector3D(5,-1,2),new Vector3D(6,1,3))));
            Assert.Equal(1,prepared.CornerTransforms);
            for(int i=0;i<32;i++) Assert.True(prepared.Hidden(Wall));
            Assert.Equal(8,prepared.CornerTransforms);
        }

        [Fact]
        public void CandidateCacheInvalidatesCellTopologyAgeAndFrameReset()
        {
            var cache=new ThermalVisionOcclusion.CandidateCache();
            Assert.False(cache.CanReuse(20,1,Vector3I.Zero));
            cache.Begin(20,1,Vector3I.Zero); cache.Positions.Add(Vector3I.UnitX);
            Assert.True(cache.CanReuse(20,1,Vector3I.Zero));
            Assert.True(cache.CanReuse(34,1,Vector3I.Zero));
            Assert.False(cache.CanReuse(35,1,Vector3I.Zero));
            Assert.False(cache.CanReuse(19,1,Vector3I.Zero));
            Assert.False(cache.CanReuse(20,2,Vector3I.Zero));
            Assert.False(cache.CanReuse(20,1,Vector3I.UnitX));
            cache.Begin(35,2,Vector3I.UnitX);
            Assert.Empty(cache.Positions);
            Assert.True(cache.CanReuse(35,2,Vector3I.UnitX));
        }

        [Fact]

        public void EntireShipBehindSolidCubeIsHiddenButPartialShipIsNot()
        {
            Assert.True(Hidden(new Vector3D(-2,-2,10),new Vector3D(2,2,12)));
            Assert.False(Hidden(new Vector3D(-2,-2,10),new Vector3D(6,2,12)));
            Assert.False(Hidden(new Vector3D(-.1,-.1,1),new Vector3D(.1,.1,1.5)));
            Assert.False(Hidden(new Vector3D(-.1,-.1,2.5),new Vector3D(.1,.1,12)));
            Assert.False(Hidden(new Vector3D(-.1,-.1,-12),new Vector3D(.1,.1,-10)));
        }

        [Fact]

        public void WindowBetweenSeparateSolidCubesRemainsVisible()
        {

            var ship=new BoundingBoxD(new Vector3D(-5,-1,10),new Vector3D(5,1,12));

            var left=new BoundingBoxD(new Vector3D(-3,-2,2),new Vector3D(-.2,2,3));

            var right=new BoundingBoxD(new Vector3D(.2,-2,2),new Vector3D(3,2,3));
            Assert.False(ThermalVisionOcclusion.Hidden(Vector3D.Zero,ship,MatrixD.Identity,left));
            Assert.False(ThermalVisionOcclusion.Hidden(Vector3D.Zero,ship,MatrixD.Identity,right));
        }

        [Fact]

        public void MovingCameraOrBlockerRevealsPreviouslyHiddenShip()
        {

            var ship=new BoundingBoxD(new Vector3D(-1,-1,10),new Vector3D(1,1,12));
            Assert.True(ThermalVisionOcclusion.Hidden(Vector3D.Zero,ship,MatrixD.Identity,Wall));
            Assert.False(ThermalVisionOcclusion.Hidden(new Vector3D(8,0,0),ship,MatrixD.Identity,Wall));
            Assert.False(ThermalVisionOcclusion.Hidden(Vector3D.Zero,ship,MatrixD.CreateTranslation(-8,0,0),Wall));
            Assert.False(ThermalVisionOcclusion.Hidden(new Vector3D(0,0,2.5),ship,MatrixD.Identity,Wall));
        }

        [Fact]

        public void HiddenDecisionImpliesDenseIndependentRaysHitBlocker()
        {
            var random=new System.Random(436);
            int hidden=0, visible=0;
            for(int i=0;i<300;i++)
            {

                var centre=new Vector3D(random.NextDouble()*12-6,random.NextDouble()*8-4,8+random.NextDouble()*20);

                var target=new BoundingBoxD(centre-new Vector3D(.5),centre+new Vector3D(.5));
                var transform=MatrixD.CreateRotationY(random.NextDouble()*.7-.35);
                if(!ThermalVisionOcclusion.Hidden(Vector3D.Zero,target,MatrixD.Invert(transform),Wall)) { visible++; continue; }
                hidden++;
                for(int x=0;x<=4;x++) for(int y=0;y<=4;y++) for(int z=0;z<=4;z++)
                {

                    var point=target.Min+new Vector3D(x,y,z)*.25;
                    point=Vector3D.Transform(point,MatrixD.Invert(transform));
                    double length=point.Length();

                    var ray=new RayD(Vector3D.Zero,point/length);
                    double? distance=ray.Intersects(Wall);
                    Assert.True(distance.HasValue && distance.Value>0 && distance.Value<length);
                }
            }
            Assert.True(hidden>10); Assert.True(visible>10);
        }


        private static bool Hidden(Vector3D min,Vector3D max)
        { return ThermalVisionOcclusion.Hidden(Vector3D.Zero,new BoundingBoxD(min,max),MatrixD.Identity,Wall); }
    }
}

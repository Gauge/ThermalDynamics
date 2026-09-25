using Thermodynamics.Presentation;
using System;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Apparent planetary size, tangent geometry and sky projection without voxel meshes.</summary>
    public class ThermalVisionCelestialTests
    {
        [Fact]

        public void PreparedRingsMatchIndependentSphereSamplesAcrossScaleAndOrientation()
        {

            var random=new Random(7331);
            for(int body=0;body<100;body++)
            {
                double radius=10+random.NextDouble()*100000;
                var direction=Vector3D.Normalize(new Vector3D(random.NextDouble()-.5,random.NextDouble()-.5,random.NextDouble()-.5));
                var centre=direction*radius*(1.00001+random.NextDouble()*100);
                var disc=new ThermalVisionCelestial.Disc(centre,radius);
                for(int r=0;r<=12;r++)
                {
                    var ring=disc.PrepareRing(r/12d);
                    for(int s=0;s<96;s++)
                    {
                        double angle=s*Math.PI/48;
                        Vector3D expectedRay,expectedNormal,ray,normal;
                        Assert.True(ThermalVisionCelestial.Sample(centre,radius,r/12d,angle,out expectedRay,out expectedNormal));
                        ring.Sample(Math.Cos(angle),Math.Sin(angle),out ray,out normal);
                        Assert.InRange(Vector3D.Distance(ray,expectedRay),0,1e-12);
                        Assert.InRange(Vector3D.Distance(normal,expectedNormal),0,1e-9);
                        Assert.InRange(Math.Abs(normal.Length()-1),0,1e-12);
                    }
                }
            }
        }

        [Fact]

        public void LimbUsesAngularRadiusAndNormalIsTangent()
        {

            var centre=new Vector3D(0,0,-1000000);
            for(int i=0;i<64;i++)
            {
                Vector3D ray,normal;
                Assert.True(ThermalVisionCelestial.Sample(centre,60000,1,i*Math.PI/32,out ray,out normal));
                Assert.InRange(Math.Abs(Vector3D.Dot(ray,normal)),0,1e-6);
                Assert.InRange(Math.Abs(Math.Acos(-ray.Z)-Math.Asin(.06)),0,1e-10);
            }
        }
        [Fact]

        public void SkyProjectionPreservesDirectionWhileRemainingInsideCameraFarPlane()
        {
            var camera=MatrixD.CreateFromYawPitchRoll(.3,.2,.1);

            camera.Translation=new Vector3D(10000,20000,30000);
            var ray=Vector3D.Normalize(camera.Forward+camera.Right*.3+camera.Up*.2);
            Vector3D point;
            Assert.True(ThermalVisionCelestial.Project(ray,camera,14900,out point));
            Assert.InRange(Math.Abs(Vector3D.Dot(point-camera.Translation,camera.Forward)-14900),0,1e-8);
            Assert.InRange(Vector3D.Distance(Vector3D.Normalize(point-camera.Translation),ray),0,1e-10);
            Assert.False(ThermalVisionCelestial.Project(-camera.Forward,camera,14900,out point));
        }
        [Fact]

        public void FarPlanetsRemainVisibleWithoutAnyVoxelOrFarPlaneQuery()
        {
            var projection=MatrixD.CreatePerspectiveFieldOfView(1,1.8,.1,15000);
            Assert.True(ThermalVisionCelestial.InViewport(new Vector3D(0,0,-1000000),60000,MatrixD.Identity,projection));
            Assert.False(ThermalVisionCelestial.InViewport(new Vector3D(0,0,1000000),60000,MatrixD.Identity,projection));
            Assert.False(ThermalVisionCelestial.InViewport(new Vector3D(1000000,0,-10000),60000,MatrixD.Identity,projection));
            Vector3D ray,normal;
            Assert.False(ThermalVisionCelestial.Sample(new Vector3D(0,0,-100),200,0,0,out ray,out normal));
        }
    }
}

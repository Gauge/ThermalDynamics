using Thermodynamics.Presentation;
using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Checks HUD pixel anchoring under camera motion, lens shifts, FOV and DPI changes.</summary>
    public class ThermalVisionHudProjectionTests
    {
        [Theory]
        [InlineData(1920,1080,1,0.8)]
        [InlineData(2578,1440,1.333333,1.2)]
        [InlineData(3440,1440,1.333333,1.5)]

        public void LegendCornersStayAtTheSamePixelsDuringCameraMotion(int width,int height,double dpi,double fov)
        {

            var viewport=new Vector2(width,height);
            var projection=MatrixD.CreatePerspectiveFieldOfView(fov,(double)width/height,.1,10000);
            projection.M31=.08; projection.M32=-.04;
            for(int frame=0;frame<40;frame++)
            {
                var world=MatrixD.CreateFromYawPitchRoll(frame*.13,frame*.04,frame*.03);

                world.Translation=new Vector3D(100000+frame*13,-20000+frame*7,3000-frame*9);
                var plane=ThermalVisionHudProjection.Create(world,projection,viewport,.101,dpi);
                foreach(var offset in new[] { new Vector2(0,0),new Vector2(420,112) })
                {

                    var hud=new Vector3D(width/(2*dpi)-24-offset.X,height/(2*dpi)-38-offset.Y,0);
                    var position=Vector3D.Transform(hud,plane);
                    var clip=Vector4D.Transform(new Vector4D(position,1),MatrixD.Invert(world)*projection);
                    double x=(clip.X/clip.W+1)*width/2;
                    double y=(1-clip.Y/clip.W)*height/2;
                    Assert.InRange(System.Math.Abs(x-(width-(24+offset.X)*dpi)),0,.002);
                    Assert.InRange(System.Math.Abs(y-(38+offset.Y)*dpi),0,.002);
                }
            }
        }
    }
}

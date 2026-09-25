using Thermodynamics.Presentation;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;
using VRageRender;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private static bool smoothFleet;
        private static int regionCapTriangles;
        private static Dictionary<Region,ThermalVisionSurfaceField> smoothCorners = new Dictionary<Region,ThermalVisionSurfaceField>();

        private static Vector2 FleetUv(float kelvin)
        {
            float t = MathHelper.Clamp((kelvin-lowKelvin)/System.Math.Max(1,highKelvin-lowKelvin),0,1);
            return new Vector2((.5f+255*t)/256,.5f);
        }
        private static void SmoothPatch(ThermalVisionSurfaceField.Patch patch,Vector3D offset,Vector3 normal,bool reverse,BoundingFrustumD frustum)
        {
            if(frustum!=null && frustum.Contains(ThermalVisionGeometry.PatchBounds(patch.A,patch.C))==ContainmentType.Disjoint)
            { regionCulledPatches++; return; }
            if(countUniformPatches && ThermalVisionSurfaceField.HasUniformEndpoints(patch))regionUniformPatches++;
            Vector4 t=ThermalVisionSurfaceField.PatchTemperatures(patch,previousSurface==null?1:surfaceBlend);
            Vector2 ua=FleetUv(t.X), ub=FleetUv(t.Y), uc=FleetUv(t.Z), ud=FleetUv(t.W);
            Vector3D a=Vector3D.Transform(patch.A,regionRenderMatrix), b=Vector3D.Transform(patch.B,regionRenderMatrix),
                c=Vector3D.Transform(patch.C,regionRenderMatrix), d=Vector3D.Transform(patch.D,regionRenderMatrix);
            a+=offset; b+=offset; c+=offset; d+=offset;
            if(patch.AlternateDiagonal)
            {
                SubmitGradientTriangle(a,b,d,normal,ua,ub,ud,reverse);
                SubmitGradientTriangle(b,c,d,normal,ub,uc,ud,reverse);
            }
            else
            {
                SubmitGradientTriangle(a,b,c,normal,ua,ub,uc,reverse);
                SubmitGradientTriangle(a,c,d,normal,ua,uc,ud,reverse);
            }
        }

        private static void SubmitGradientTriangle(Vector3D a,Vector3D b,Vector3D c,Vector3 normal,
            Vector2 ua,Vector2 ub,Vector2 uc,bool reverse)
        {
            MyTransparentGeometry.AddTriangleBillboard(a,reverse?c:b,reverse?b:c,normal,normal,normal,
                ua,reverse?uc:ub,reverse?ub:uc,
                State.Current==ThermalVisionState.Mode.Cividis?GradientColour:GradientGrey,
                0,(a+b+c)/3,Vector4.One,MyBillboard.BlendTypeEnum.PostPP);
            regionBillboards++; drawn++; examined++;
        }

        private static void SmoothTriangle(Region region, Vector3D a, Vector3D b, Vector3D c, Vector3D eye, bool bias)
        {
            ThermalVisionSurfaceField corners;
            if (!smoothCorners.TryGetValue(region,out corners)) return;
            ThermalVisionSurfaceField previous=null;
            if(previousSurface!=null && surfaceBlend<1) previousSurface.TryGetValue(region,out previous);
            capTriangles.Clear();
            corners.AppendCapTriangles(a,b,c,previous,surfaceBlend,20,capTriangles);
            for(int i=0;i<capTriangles.Count;i++)
            {
                var triangle=capTriangles[i];
                DrawCapTriangle(triangle.A,triangle.B,triangle.C,triangle.Temperatures,eye,bias);
            }
        }
        private static readonly List<ThermalVisionSurfaceField.TemperatureTriangle> capTriangles=
            new List<ThermalVisionSurfaceField.TemperatureTriangle>(64);
        private static void DrawCapTriangle(Vector3D a,Vector3D b,Vector3D c,Vector3 temperatures,Vector3D eye,bool bias)
        {
            Vector2 ua=FleetUv(temperatures.X),ub=FleetUv(temperatures.Y),uc=FleetUv(temperatures.Z);
            a=Vector3D.Transform(a,regionRenderMatrix); b=Vector3D.Transform(b,regionRenderMatrix); c=Vector3D.Transform(c,regionRenderMatrix);
            eye=Vector3D.Transform(eye,regionRenderMatrix);
            Vector3D normal=Vector3D.Cross(b-a,c-a);
            if(normal.LengthSquared()<1e-20)return;
            if(Vector3D.Dot(normal,eye-a)<0){var p=b;b=c;c=p;var uv=ub;ub=uc;uc=uv;normal=-normal;}
            normal.Normalize();
            Vector3D offset=bias?MyAPIGateway.Session.Camera.WorldMatrix.Backward*.001:Vector3D.Zero;
            SubmitGradientTriangle(a+offset,b+offset,c+offset,(Vector3)normal,ua,ub,uc,false);
            regionCapTriangles++;
        }
    }
}

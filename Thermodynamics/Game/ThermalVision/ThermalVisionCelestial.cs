using Thermodynamics.Presentation;
using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game;
using VRageMath;
using VRageRender;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private static readonly List<PlanetManager.Planet> skyPlanets=new List<PlanetManager.Planet>();
        private struct SkyVertex { public Vector3D Position; public Vector2 Uv; public bool Valid; }
        private static readonly SkyVertex[,] skyVertices=new SkyVertex[13,97];
        private static readonly Vector2D[][] skyAngles=BuildSkyAngles();

        private static Vector2D[][] BuildSkyAngles()
        {
            var angles=new Vector2D[97][];
            for(int count=16;count<=96;count++)
            {
                angles[count]=new Vector2D[count+1];
                for(int i=0;i<count;i++)
                {
                    double angle=2*Math.PI*i/count;
                    angles[count][i]=new Vector2D(Math.Cos(angle),Math.Sin(angle));
                }
                angles[count][count]=angles[count][0];
            }
            return angles;
        }

        private static void DrawThermalSky()
        {
            int start=regionBillboards;
            var camera=MyAPIGateway.Session.Camera;
            var world=camera.WorldMatrix;
            Vector3D sun=Vector3D.Normalize(MyVisualScriptLogicProvider.GetSunDirection());
            double depth=ThermalVisionViewPolicy.BackdropDistance(BlockFleetViewDistance())*.999;
            DrawSkyDisc(sun*1e9,Math.Sqrt(1-.99875*.99875)*1e9,sun,Vector3D.Up,null,true,depth);
            PlanetManager.CopyPlanets(skyPlanets);
            for(int i=1;i<skyPlanets.Count;i++)
            {
                var planet=skyPlanets[i];
                double distance=Vector3D.DistanceSquared(planet.Position,world.Translation);
                int j=i-1;
                while(j>=0 && Vector3D.DistanceSquared(skyPlanets[j].Position,world.Translation)<distance)
                { skyPlanets[j+1]=skyPlanets[j]; j--; }
                skyPlanets[j+1]=planet;
            }
            foreach(var planet in skyPlanets)
            {
                if(planet.Entity==null || planet.Entity.MarkedForClose) continue;
                Vector3D centre=planet.Entity.PositionComp.WorldMatrixRef.Translation-world.Translation;
                double radius=planet.Entity.AverageRadius;
                if(centre.Length()<=radius) continue;
                DrawSkyDisc(centre,radius,sun,planet.Entity.WorldMatrix.Up,planet.Definition(),false,depth);
            }
            if(frames%120==0) RecordEvent("thermal sky: registered-planets="+skyPlanets.Count
                +" triangles="+(regionBillboards-start)+" depth-m="+depth.ToString("F1")
                +" planets=climate-estimate sun=saturated exposure=excluded",false);
        }
        private static void DrawSkyDisc(Vector3D centre,double radius,Vector3D sun,Vector3D axis,
            PlanetDefinition definition,bool solar,double depth)
        {
            var camera=MyAPIGateway.Session.Camera;
            if(!ThermalVisionCelestial.InViewport(centre,radius,camera.WorldMatrix,camera.ProjectionMatrix)) return;
            var climate=definition==null?null:ThermalBlockCatalog.ToPlanetProperties(definition);
            var disc=new ThermalVisionCelestial.Disc(centre,radius);
            double angle=disc.AngularRadius;
            double pixels=angle*camera.ViewportSize.Y*Math.Abs(camera.ProjectionMatrix.M22);
            int sectors=Math.Max(16,Math.Min(96,(int)Math.Ceiling(pixels/4)));
            int rings=solar?1:Math.Max(2,Math.Min(12,sectors/8));
            for(int r=0;r<=rings;r++)
            {
                var ring=disc.PrepareRing((double)r/rings);
                for(int s=0;s<=sectors;s++)
                {
                    Vector3D ray,normal,position;
                    var azimuth=skyAngles[sectors][s];
                    ring.Sample(azimuth.X,azimuth.Y,out ray,out normal);
                    bool valid=ThermalVisionCelestial.Project(ray,camera.WorldMatrix,depth,out position);
                    float temperature=solar?highKelvin:ClimateModel.Target(climate,
                        (float)Vector3D.Dot(normal,axis),(float)Vector3D.Dot(normal,sun),0);
                    skyVertices[r,s]=new SkyVertex { Valid=valid,Position=position,Uv=FleetUv(temperature) };
                }
            }
            for(int r=0;r<rings;r++) for(int s=0;s<sectors;s++)
            {
                SkyTriangle(skyVertices[r,s],skyVertices[r+1,s],skyVertices[r+1,s+1],solar||climate!=null);
                if(r>0) SkyTriangle(skyVertices[r,s],skyVertices[r+1,s+1],skyVertices[r,s+1],solar||climate!=null);
            }
        }
        private static void SkyTriangle(SkyVertex a,SkyVertex b,SkyVertex c,bool known)
        {
            if(!a.Valid || !b.Valid || !c.Valid) return;
            Vector3 normal=(Vector3)MyAPIGateway.Session.Camera.WorldMatrix.Backward;
            MyTransparentGeometry.AddTriangleBillboard(a.Position,b.Position,c.Position,normal,normal,normal,
                a.Uv,b.Uv,c.Uv,known?(State.Current==ThermalVisionState.Mode.Cividis?GradientColour:GradientGrey):CompositeSurfaceMaterial,
                0,(a.Position+b.Position+c.Position)/3,known?Vector4.One:RegionNeutral,MyBillboard.BlendTypeEnum.PostPP);
            regionBillboards++; drawn++; examined++;
        }
    }
}

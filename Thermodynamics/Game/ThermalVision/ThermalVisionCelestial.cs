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
        private static readonly ThermalVisionFrameBudget skyDrawBudget=new ThermalVisionFrameBudget();
        private static readonly List<PlanetManager.Planet> skyPlanets=new List<PlanetManager.Planet>();
        private struct SkyVertex { public Vector3D Position; public Vector2 Uv; public bool Valid; }
        private struct SkyView
        {
            public MatrixD World,Projection;
            public Vector3 Normal;
            public double ViewportHeight;
        }
        private static readonly SkyVertex[,] skyVertices=new SkyVertex[13,97];
        private static readonly Vector2D[][] skyAngles=BuildSkyAngles();

        private static Vector2D[][] BuildSkyAngles()
        {
            var angles=new Vector2D[97][];
            for(int count=3;count<=96;count++)
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
            skyDrawBudget.Reset(ThermalVisionFrameBudget.SkyTriangles);
            var camera=MyAPIGateway.Session.Camera;
            var world=camera.WorldMatrix;
            // One frame's camera state serves all disc vertices and triangles.
            var view=new SkyView { World=world,Projection=camera.ProjectionMatrix,
                Normal=(Vector3)world.Backward,ViewportHeight=camera.ViewportSize.Y };
            Vector3D sun=Vector3D.Normalize(MyVisualScriptLogicProvider.GetSunDirection());
            double depth=ThermalVisionViewPolicy.BackdropDistance(BlockFleetViewDistance())*.999;
            PlanetManager.CopyPlanets(skyPlanets);
            skyDrawBudget.BeginSlice(skyPlanets.Count+1);
            DrawSkyDisc(sun*1e9,Math.Sqrt(1-.99875*.99875)*1e9,sun,Vector3D.Up,null,true,depth,ref view);
            for(int i=1;i<skyPlanets.Count;i++)
            {
                var planet=skyPlanets[i];
                double distance=Vector3D.DistanceSquared(planet.Position,world.Translation);
                int j=i-1;
                while(j>=0 && Vector3D.DistanceSquared(skyPlanets[j].Position,world.Translation)<distance)
                { skyPlanets[j+1]=skyPlanets[j]; j--; }
                skyPlanets[j+1]=planet;
            }
            int remaining=skyPlanets.Count;
            foreach(var planet in skyPlanets)
            {
                skyDrawBudget.BeginSlice(remaining--);
                if(planet.Entity==null || planet.Entity.MarkedForClose) continue;
                Vector3D centre=planet.Entity.PositionComp.WorldMatrixRef.Translation-world.Translation;
                double radius=planet.Entity.AverageRadius;
                if(centre.Length()<=radius) continue;
                DrawSkyDisc(centre,radius,sun,planet.Entity.WorldMatrix.Up,planet,false,depth,ref view);
            }
            if(Telemetry.Enabled && frames%120==0) RecordEvent("thermal sky: registered-planets="+skyPlanets.Count
                +" triangles="+(regionBillboards-start)+" depth-m="+depth.ToString("F1")
                +" planets=climate-estimate sun=saturated exposure=excluded"
                +" triangle-cap="+ThermalVisionFrameBudget.SkyTriangles+" limited="+skyDrawBudget.Limited,false);
        }

        private static PlanetThermalProperties SkyClimate(PlanetManager.Planet planet)
        {
            var definition=planet.Definition();
            if(definition==null) return null;
            var world=Settings.Instance;
            var key=new ThermalVisionClimateCache<PlanetThermalProperties>.Key {
                Definition=definition,Supplied=(int)definition.Supplied,
                DefinitionValues=new Vector3(definition.NightTemperature,definition.DayTemperature,definition.PoleTemperatureDrop),
                HasWorld=world!=null,
                WorldValues=world==null?Vector3.Zero:new Vector3(world.PlanetNightTemperature,world.PlanetDayTemperature,world.PlanetPoleTemperatureDrop)
            };
            var cache=planet.SkyClimate;
            if(cache==null) planet.SkyClimate=cache=new ThermalVisionClimateCache<PlanetThermalProperties>();
            PlanetThermalProperties climate;
            if(cache.TryGet(key,out climate)) return climate;
            // ClimateModel.Target reads only night/day/pole values. Resolve through
            // the existing adapter on a miss to preserve supplied/default/override
            // and clamp semantics, including in-place definition/settings edits.
            climate=ThermalBlockCatalog.ToPlanetProperties(definition);
            cache.Store(key,climate);
            return climate;
        }

        private static void DrawSkyDisc(Vector3D centre,double radius,Vector3D sun,Vector3D axis,
            PlanetManager.Planet planet,bool solar,double depth,ref SkyView view)
        {
            if(!ThermalVisionCelestial.InViewport(centre,radius,view.World,view.Projection)) return;
            var climate=planet==null?null:SkyClimate(planet);
            var disc=new ThermalVisionCelestial.Disc(centre,radius);
            double angle=disc.AngularRadius;
            double pixels=angle*view.ViewportHeight*Math.Abs(view.Projection.M22);
            int sectors=Math.Max(16,Math.Min(96,(int)Math.Ceiling(pixels/4)));
            int rings=solar?1:Math.Max(2,Math.Min(12,sectors/8));
            // Adapt whole discs to the remaining share before sampling. This
            // preserves coverage instead of truncating triangles halfway through.
            if(skyDrawBudget.SliceRemaining<3) { skyDrawBudget.Take(3); return; }
            if((2*rings-1)*sectors>skyDrawBudget.SliceRemaining) skyDrawBudget.ReportLimited();
            while(rings>1 && (2*rings-1)*sectors>skyDrawBudget.SliceRemaining) rings--;
            sectors=Math.Max(3,Math.Min(sectors,skyDrawBudget.SliceRemaining/(2*rings-1)));
            for(int r=0;r<=rings;r++)
            {
                var ring=disc.PrepareRing((double)r/rings);
                // At the centre, both azimuth basis vectors are zero. The seam
                // repeats the first azimuth exactly; neither needs resampling.
                int samples=r==0?1:sectors;
                for(int s=0;s<samples;s++)
                {
                    Vector3D ray,normal,position;
                    var azimuth=skyAngles[sectors][s];
                    ring.Sample(azimuth.X,azimuth.Y,out ray,out normal);
                    bool valid=ThermalVisionCelestial.Project(ray,view.World,depth,out position);
                    float temperature=solar?highKelvin:ClimateModel.Target(climate,
                        (float)Vector3D.Dot(normal,axis),(float)Vector3D.Dot(normal,sun),0);
                    skyVertices[r,s]=new SkyVertex { Valid=valid,Position=position,Uv=FleetUv(temperature) };
                }
                if(r==0) for(int s=1;s<sectors;s++) skyVertices[0,s]=skyVertices[0,0];
                skyVertices[r,sectors]=skyVertices[r,0];
            }
            for(int r=0;r<rings;r++) for(int s=0;s<sectors;s++)
            {
                SkyTriangle(skyVertices[r,s],skyVertices[r+1,s],skyVertices[r+1,s+1],solar||climate!=null,view.Normal);
                if(r>0) SkyTriangle(skyVertices[r,s],skyVertices[r+1,s+1],skyVertices[r,s+1],solar||climate!=null,view.Normal);
            }
        }
        private static void SkyTriangle(SkyVertex a,SkyVertex b,SkyVertex c,bool known,Vector3 normal)
        {
            if(!a.Valid || !b.Valid || !c.Valid || !skyDrawBudget.Take(1)) return;
            MyTransparentGeometry.AddTriangleBillboard(a.Position,b.Position,c.Position,normal,normal,normal,
                a.Uv,b.Uv,c.Uv,known?(State.Current==ThermalVisionState.Mode.Cividis?GradientColour:GradientGrey):CompositeSurfaceMaterial,
                uint.MaxValue,(a.Position+b.Position+c.Position)/3,known?Vector4.One:ThermalVisionContext.Colour(0),MyBillboard.BlendTypeEnum.PostPP);
            regionBillboards++; drawn++; examined++;
        }
    }
}

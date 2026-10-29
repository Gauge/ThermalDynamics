using Thermodynamics.Presentation;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics
{
    public static partial class ThermalVisionProbe
    {
        private struct SolidOccluder
        {
            public ThermalGrid Owner;
            public MatrixD Inverse;
            public BoundingBoxD Bounds;
        }
        private static readonly List<SolidOccluder> visionOccluders=new List<SolidOccluder>();

        private static int occluderQueries,occluderCacheHits,occluderTests;

        private static bool SolidCell(VRage.Game.ModAPI.IMyCubeGrid grid,Vector3I position,out BoundingBoxD bounds)
        {
            bounds=new BoundingBoxD();
            var block=grid.GetCubeBlock(position);
            if(block==null || !block.IsFullIntegrity || block.BuildLevelRatio<1 || block.HasDeformation) return false;
            if(block.BlockDefinition.Context==null || !block.BlockDefinition.Context.IsBaseGame) return false;
            string subtype=block.BlockDefinition.Id.SubtypeName;
            if(subtype!="LargeBlockArmorBlock" && subtype!="LargeHeavyBlockArmorBlock"
                && subtype!="SmallBlockArmorBlock" && subtype!="SmallHeavyBlockArmorBlock") return false;
            Vector3D mid=(Vector3D)position*grid.GridSize;
            Vector3D half=new Vector3D(grid.GridSize*.4);
            bounds=new BoundingBoxD(mid-half,mid+half);
            return true;
        }

        private static int CullHiddenFleet(Vector3D eye)
        {
            visionOccluders.Clear(); occluderQueries=occluderCacheHits=occluderTests=0;
            var camera=Sandbox.ModAPI.MyAPIGateway.Session.Camera;
            double near=camera.NearPlaneDistance*1.05;
            int candidateGrids=0;
            foreach(var view in visibleViews)
            {
                if(candidateGrids++>=ThermalVisionOcclusion.MaxCandidateGrids) break;
                VRage.Game.ModAPI.IMyCubeGrid grid=view.Grid.Grid;
                if(view.Distance>grid.GridSize*3) continue;
                var centre=grid.WorldToGridInteger(eye);
                var cache=view.Occluders;
                if(cache==null) view.Occluders=cache=new ThermalVisionOcclusion.CandidateCache();
                bool reuse=cache.CanReuse(frames,view.Grid.TopologyRevision,centre);
                if(reuse) occluderCacheHits++;
                if(reuse && cache.Positions.Count==0) continue;
                MatrixD inverse=MatrixD.Invert(grid.WorldMatrix);
                MatrixD localCamera=camera.WorldMatrix*inverse;
                if(reuse)
                {
                    for(int i=0;i<cache.Positions.Count && occluderQueries<500 && visionOccluders.Count<32;i++)
                    {
                        // Re-read removal, construction, integrity and deformation
                        // every frame; cached positions alone never occlude anything.
                        occluderQueries++; BoundingBoxD bounds;
                        if(SolidCell(grid,cache.Positions[i],out bounds)
                            && UnclippedOccluder(cache.Positions[i],grid.GridSize,localCamera,near))
                            visionOccluders.Add(new SolidOccluder { Owner=view.Grid,Inverse=inverse,Bounds=bounds });
                    }
                }
                else
                {
                    cache.Begin(frames,view.Grid.TopologyRevision,centre);
                    for(int x=-2;x<=2 && occluderQueries<500 && visionOccluders.Count<32 && cache.Positions.Count<32;x++)
                        for(int y=-2;y<=2 && occluderQueries<500 && visionOccluders.Count<32 && cache.Positions.Count<32;y++)
                            for(int z=-2;z<=2 && occluderQueries<500 && visionOccluders.Count<32 && cache.Positions.Count<32;z++)
                    {
                        occluderQueries++; var position=centre+new Vector3I(x,y,z); BoundingBoxD bounds;
                        if(!SolidCell(grid,position,out bounds)) continue;
                        // Cache candidates, including clipped cubes: camera movement
                        // must reconsider their eligibility even within the same cell.
                        cache.Positions.Add(position);
                        if(!UnclippedOccluder(position,grid.GridSize,localCamera,near)) continue;
                        visionOccluders.Add(new SolidOccluder { Owner=view.Grid,Inverse=inverse,Bounds=bounds });
                    }
                }
                if(occluderQueries>=500 || visionOccluders.Count>=32) break;
            }
            int kept=0, hidden=0;
            for(int i=0;i<visibleViews.Count;i++)
            {
                var view=visibleViews[i]; bool blocked=false;
                if(occluderTests>=ThermalVisionOcclusion.MaxFleetTests) { visibleViews[kept++]=view; continue; }
                ThermalGrid lastOwner=null;
                var target=new ThermalVisionOcclusion.PreparedTarget();
                // Candidates are contiguous by owner, so each inverse transform
                // and target corner can be reused across that owner's solid cubes.
                foreach(var occluder in visionOccluders)
                {
                    if(occluder.Owner==view.Grid) continue;
                    if(occluderTests>=ThermalVisionOcclusion.MaxFleetTests) break;
                    occluderTests++;
                    if(occluder.Owner!=lastOwner)
                    {
                        lastOwner=occluder.Owner;
                        target=new ThermalVisionOcclusion.PreparedTarget(eye,view.WorldBounds,occluder.Inverse);
                    }
                    if(target.Hidden(occluder.Bounds)) { blocked=true; break; }
                }
                if(blocked) hidden++;
                else visibleViews[kept++]=view;
            }
            if(kept<visibleViews.Count) visibleViews.RemoveRange(kept,visibleViews.Count-kept);
            return hidden;
        }

        private static bool UnclippedOccluder(Vector3I position,float gridSize,MatrixD localCamera,double near)
        {
            Vector3D centre=(Vector3D)position*gridSize;
            Vector3D half=new Vector3D(gridSize*.5);
            return ThermalVisionOcclusion.InFrontOfNearPlane(new BoundingBoxD(centre-half,centre+half),
                localCamera.Translation,localCamera.Forward,near);
        }
    }
}

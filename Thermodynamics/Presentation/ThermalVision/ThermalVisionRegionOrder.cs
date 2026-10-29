using System;
using System.Collections.Generic;
using VRageMath;
using Region = Thermodynamics.Presentation.ThermalVisionRegionPartition.Region;

namespace Thermodynamics.Presentation
{
    public sealed class ThermalVisionRegionOrder
    {
        private sealed class Node
        {
            public int Axis;
            public double Plane;
            public Node Low, High;
            public Region Value;
            public BoundingBoxD Bounds;
        }
        private Node root;
        public int LeafCount { get; private set; }
        public int NodeCount { get; private set; }


        public static bool TryBuild(ThermalVisionRegionPartition field, int leafLimit, out ThermalVisionRegionOrder order)
        {
            if (field == null || leafLimit < 1 || leafLimit > 65536) throw new ArgumentException("Invalid region order budget");
            order = null;
            if (field.Count > leafLimit) return false;

            var working = new ThermalVisionRegionOrder();

            var regions = new List<Region>(field.Count);
            for (int i = 0; i < field.Count; i++) regions.Add(field[i]);
            if (!working.Build(regions, leafLimit, 0, out working.root)) return false;
            order = working;
            return true;
        }


        public static bool TryBuild(ThermalVisionRegionScan scan, int leafLimit, out ThermalVisionRegionOrder order)
        {
            if (scan == null || leafLimit < 1 || leafLimit > 65536) throw new ArgumentException("Invalid scan order budget");
            order = null;
            if (scan.Count > leafLimit) return false;

            var regions = new List<Region>(scan.Count);
            for (int i = 0; i < scan.Count; i++) regions.Add(scan[i]);

            var working = new ThermalVisionRegionOrder();
            if (!working.Build(regions, leafLimit, 0, out working.root)) return false;
            order = working;
            return true;
        }


        public static bool TryBuild(IList<Region> disjointRegions, int leafLimit, out ThermalVisionRegionOrder order)
        {
            if (disjointRegions == null || leafLimit < 1 || leafLimit > 65536)
                throw new ArgumentException("Invalid block order budget");
            order = null;
            if (disjointRegions.Count > leafLimit) return false;

            var working = new ThermalVisionRegionOrder();
            if (!working.Build(new List<Region>(disjointRegions), leafLimit, 0, out working.root)) return false;
            order = working;
            return true;
        }


        private bool Build(List<Region> regions, int limit, int depth, out Node node)
        {
            node = null;
            if (regions.Count == 0) return true;
            if (depth > 64 || NodeCount >= limit * 2 - 1) return false;
            NodeCount++;
            if (regions.Count == 1)
            {
                if (++LeafCount > limit) return false;

                node = new Node { Value = regions[0], Bounds=new BoundingBoxD(regions[0].Min,regions[0].Max) };
                return true;
            }

            Vector3D lo = Centre(regions[0]), hi = lo;
            foreach (Region region in regions)
            { lo = Vector3D.Min(lo, Centre(region)); hi = Vector3D.Max(hi, Centre(region)); }
            Vector3D spread = hi - lo;
            int axis = spread.X >= spread.Y && spread.X >= spread.Z ? 0 : spread.Y >= spread.Z ? 1 : 2;

            double target = Coordinate(lo, axis) * .5 + Coordinate(hi, axis) * .5;
            double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
            foreach (Region region in regions)
            {
                minimum = Math.Min(minimum, Coordinate(region.Min, axis));
                maximum = Math.Max(maximum, Coordinate(region.Max, axis));
            }
            double plane = double.NaN, best = double.PositiveInfinity;
            foreach (Region region in regions)
                for (int side = 0; side < 2; side++)
                {

                    double candidate = Coordinate(side == 0 ? region.Min : region.Max, axis);
                    double distance = Math.Abs(candidate - target);
                    if (candidate > minimum && candidate < maximum && distance < best)
                    { plane = candidate; best = distance; }
                }
            if (double.IsNaN(plane)) return false;

            var low = new List<Region>();

            var high = new List<Region>();
            foreach (Region region in regions)
            {
                if (Coordinate(region.Max, axis) <= plane) low.Add(region);
                else if (Coordinate(region.Min, axis) >= plane) high.Add(region);
                else
                {
                    Region a = region, b = region;

                    a.Max = Replace(a.Max, axis, plane);

                    b.Min = Replace(b.Min, axis, plane);
                    low.Add(a); high.Add(b);
                }
                if (low.Count + high.Count > limit) return false;
            }
            Vector3D boundsMin=regions[0].Min,boundsMax=regions[0].Max;
            foreach(var region in regions) { boundsMin=Vector3D.Min(boundsMin,region.Min); boundsMax=Vector3D.Max(boundsMax,region.Max); }

            node = new Node { Axis = axis, Plane = plane, Bounds=new BoundingBoxD(boundsMin,boundsMax) };

            return Build(low, limit, depth + 1, out node.Low)
                && Build(high, limit, depth + 1, out node.High);
        }


        public void WriteNearToFar(Vector3D eye, List<Region> result)
        {
            if (result == null || double.IsNaN(eye.X + eye.Y + eye.Z) || double.IsInfinity(eye.X + eye.Y + eye.Z))
                throw new ArgumentException("Finite eye and output list required");
            result.Clear();
            Visit(root, eye, result);
        }


        public void WriteVisibleNearToFar(Vector3D eye,List<Region> result,BoundingFrustumD frustum)
        {
            if(result==null || frustum==null) throw new ArgumentException("Output and local frustum required");

            int hidden; WriteVisibleNearToFar(eye,result,frustum,null,out hidden);
        }

        public void WriteVisibleNearToFar(Vector3D eye,List<Region> result,BoundingFrustumD frustum,
            IList<BoundingBoxD> blockers,out int hiddenSubtrees)
        {
            if(result==null || frustum==null)throw new ArgumentException("Output and local frustum required");
            // Preserve the original unlimited traversal for existing callers.
            int remainingOcclusionTests=-1;
            hiddenSubtrees=0;result.Clear();VisitVisible(root,eye,result,frustum,false,blockers,ref remainingOcclusionTests,ref hiddenSubtrees);
        }

        /// <summary>Shares an occlusion-test budget across trees; exhaustion keeps all frustum-visible geometry.</summary>
        public void WriteVisibleNearToFar(Vector3D eye,List<Region> result,BoundingFrustumD frustum,
            IList<BoundingBoxD> blockers,ref int remainingOcclusionTests,out int hiddenSubtrees)
        {
            if(result==null || frustum==null)throw new ArgumentException("Output and local frustum required");
            if(remainingOcclusionTests<0) remainingOcclusionTests=0;
            hiddenSubtrees=0;result.Clear();VisitVisible(root,eye,result,frustum,false,blockers,ref remainingOcclusionTests,ref hiddenSubtrees);
        }

        private static void VisitVisible(Node node,Vector3D eye,List<Region> result,BoundingFrustumD frustum,bool inside,
            IList<BoundingBoxD> blockers,ref int remainingOcclusionTests,ref int hiddenSubtrees)
        {
            if(node==null) return;
            if(!inside)
            {
                var relation=frustum.Contains(node.Bounds);
                if(relation==ContainmentType.Disjoint) return;
                inside=relation==ContainmentType.Contains;
            }
            if(blockers!=null)for(int i=0;i<blockers.Count && remainingOcclusionTests!=0;i++)
            {
                if(remainingOcclusionTests>0) remainingOcclusionTests--;
                if(ThermalVisionOcclusion.HiddenLocal(eye,node.Bounds,blockers[i]))
                { hiddenSubtrees++; return; }
            }
            if(node.Low==null && node.High==null) { result.Add(node.Value); return; }
            bool lowFirst=Coordinate(eye,node.Axis)<=node.Plane;
            VisitVisible(lowFirst?node.Low:node.High,eye,result,frustum,inside,blockers,ref remainingOcclusionTests,ref hiddenSubtrees);
            VisitVisible(lowFirst?node.High:node.Low,eye,result,frustum,inside,blockers,ref remainingOcclusionTests,ref hiddenSubtrees);
        }


        public bool TryFind(Vector3D point,out Region region)
        { return Find(root,point,out region); }

        private static bool Find(Node node,Vector3D point,out Region region)
        {
            region=default(Region);
            if(node==null) return false;
            if(node.Low==null && node.High==null)
            {
                var r=node.Value;
                if(point.X<r.Min.X || point.Y<r.Min.Y || point.Z<r.Min.Z
                    || point.X>r.Max.X || point.Y>r.Max.Y || point.Z>r.Max.Z) return false;
                region=r; return true;
            }
            double coordinate=Coordinate(point,node.Axis);
            if(coordinate<node.Plane) return Find(node.Low,point,out region);
            if(coordinate>node.Plane) return Find(node.High,point,out region);
            bool lowFirst=0<=node.Plane;

            return Find(lowFirst?node.Low:node.High,point,out region)
                || Find(lowFirst?node.High:node.Low,point,out region);
        }


        private static void Visit(Node node, Vector3D eye, List<Region> result)
        {
            if (node == null) return;
            if (node.Low == null && node.High == null) { result.Add(node.Value); return; }

            bool lowFirst = Coordinate(eye, node.Axis) <= node.Plane;
            Visit(lowFirst ? node.Low : node.High, eye, result);
            Visit(lowFirst ? node.High : node.Low, eye, result);
        }

        private static Vector3D Centre(Region region) { return region.Min * .5 + region.Max * .5; }

        private static double Coordinate(Vector3D v, int axis) { return axis == 0 ? v.X : axis == 1 ? v.Y : v.Z; }

        private static Vector3D Replace(Vector3D v, int axis, double value)
        { if (axis == 0) v.X = value; else if (axis == 1) v.Y = value; else v.Z = value; return v; }
    }
}

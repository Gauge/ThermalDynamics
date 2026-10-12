using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Presentation
{
    public sealed class ThermalVisionRegionPartition
    {
        // Regions key the per-frame surface-field dictionaries. Without these members every
        // lookup fell back to ValueType's reflection equality: ~400 ns and ~560 bytes of boxing each.
        public struct Region : IEquatable<Region>
        {
            public Vector3D Min, Max;
            public float Kelvin;

            public Region(Vector3D min, Vector3D max, float kelvin)
            { Min = min; Max = max; Kelvin = kelvin; }

            public bool Equals(Region other)
            { return Min.Equals(other.Min) && Max.Equals(other.Max) && Kelvin.Equals(other.Kelvin); }

            public override bool Equals(object obj) { return obj is Region && Equals((Region)obj); }

            public override int GetHashCode() { return Min.GetHashCode() ^ (Max.GetHashCode() * 397); }
        }

        private readonly int capacity;

        private List<Region> regions = new List<Region>();
        public int Count { get { return regions.Count; } }
        public Region this[int index] { get { return regions[index]; } }


        public ThermalVisionRegionPartition(int capacity)
        {
            if (capacity < 1) throw new ArgumentException("Positive region capacity required");
            this.capacity = capacity;
        }


        public bool TryAdd(Region input)
        {
            if (!Finite(input.Min) || !Finite(input.Max) || !Valid(input.Min, input.Max)
                || float.IsNaN(input.Kelvin) || float.IsInfinity(input.Kelvin) || input.Kelvin < 0)
                throw new ArgumentException("Finite positive-volume region and nonnegative temperature required");

            var output = new List<Region>();
            var pending = new List<Region> { input };
            foreach (Region old in regions)
            {
                if (old.Kelvin >= input.Kelvin)
                {

                    var remaining = new List<Region>();
                    foreach (Region piece in pending)
                        if (!Subtract(piece, old, remaining)) return false;
                    pending = remaining;
                    if (!Append(output, old)) return false;
                }
                else if (!Subtract(old, input, output)) return false;
            }
            foreach (Region piece in pending) if (!Append(output, piece)) return false;
            regions = output;
            return true;
        }


        public bool TryOverwrite(Region input)
        {
            if (!Finite(input.Min) || !Finite(input.Max) || !Valid(input.Min, input.Max)
                || float.IsNaN(input.Kelvin) || float.IsInfinity(input.Kelvin) || input.Kelvin < 0)
                throw new ArgumentException("Finite positive-volume region and nonnegative temperature required");

            var output = new List<Region>();
            foreach (Region old in regions)
                if (!Subtract(old, input, output)) return false;
            if (!Append(output, input)) return false;
            regions = output;
            return true;
        }


        public static Vector3D[] NearCap(Region region, MatrixD world, MatrixD projection, double near)
        {
            return NearCap(region,world,projection,near,new List<Vector3D>(16),new List<Vector3D>(16)).ToArray();
        }


        public static List<Vector3D> NearCap(Region region, MatrixD world, MatrixD projection, double near,
            List<Vector3D> polygon, List<Vector3D> scratch)
        {
            if(polygon==null || scratch==null || ReferenceEquals(polygon,scratch))
                throw new ArgumentException("Two distinct clipping buffers required");
            polygon.Clear(); scratch.Clear();
            Vector3D centre;
            float width, height;
            ThermalVisionDepthLayers.Plane(near, projection, world, out centre, out width, out height);
            polygon.Add(centre - world.Right * width - world.Up * height);
            polygon.Add(centre + world.Right * width - world.Up * height);
            polygon.Add(centre + world.Right * width + world.Up * height);
            polygon.Add(centre - world.Right * width + world.Up * height);
            for (int axis = 0; axis < 3; axis++) for (int side = 0; side < 2; side++)
            {
                if (polygon.Count == 0) return polygon;

                double plane = Coordinate(side == 0 ? region.Min : region.Max, axis);
                scratch.Clear();
                Vector3D previous = polygon[polygon.Count - 1];
                double previousDistance = (Coordinate(previous, axis) - plane) * (side == 0 ? 1 : -1);
                for(int i=0;i<polygon.Count;i++)
                {
                    Vector3D current=polygon[i];
                    double distance = (Coordinate(current, axis) - plane) * (side == 0 ? 1 : -1);
                    if ((distance >= 0) != (previousDistance >= 0))
                        scratch.Add(previous + (current - previous) * (previousDistance / (previousDistance - distance)));
                    if (distance >= 0) scratch.Add(current);
                    previous = current; previousDistance = distance;
                }
                var swap=polygon; polygon=scratch; scratch=swap;
            }
            return polygon;
        }


        private static double Coordinate(Vector3D value, int axis)
        { return axis == 0 ? value.X : axis == 1 ? value.Y : value.Z; }


        public static bool TryFocus(ThermalVisionRegionScan coarse, ThermalVisionRegionScan fine,
            Vector3D min, Vector3D max, int capacity, out ThermalVisionRegionPartition field)
        {
            field = null;
            if (coarse == null || fine == null || coarse.Running || fine.Running || coarse.Failure != null || fine.Failure != null
                || !Finite(min) || !Finite(max) || !Valid(min, max)) return false;

            var result = new ThermalVisionRegionPartition(capacity);

            var cut = new Region(min, max, 0);
            for (int i = 0; i < coarse.Count; i++)
                if (!result.Subtract(coarse[i], cut, result.regions)) return false;
            for (int i = 0; i < fine.Count; i++)
            {
                Region item = fine[i];
                if (item.Min.X < min.X || item.Min.Y < min.Y || item.Min.Z < min.Z
                    || item.Max.X > max.X || item.Max.Y > max.Y || item.Max.Z > max.Z
                    || !result.Append(result.regions, item)) return false;
            }
            field = result;
            return true;
        }


        public static bool TryReplace(ThermalVisionRegionPartition coarse, ThermalVisionRegionScan fine,
            Vector3D min, Vector3D max, int capacity, out ThermalVisionRegionPartition field)
        {
            field = null;
            if (coarse == null || fine == null || fine.Running || fine.Failure != null || fine.Generation == 0
                || !Finite(min) || !Finite(max) || !Valid(min, max)) return false;

            var result = new ThermalVisionRegionPartition(capacity);

            var cut = new Region(min, max, 0);
            for (int i = 0; i < coarse.Count; i++)
                if (!result.Subtract(coarse[i], cut, result.regions)) return false;
            for (int i = 0; i < fine.Count; i++)
            {
                Region item = fine[i];
                if (!result.Slab(result.regions, Vector3D.Max(min, item.Min), Vector3D.Min(max, item.Max), item.Kelvin)) return false;
            }
            field = result;
            return true;
        }


        public static ThermalVisionRegionPartition FromScan(ThermalVisionRegionScan scan, int capacity)
        {
            if (scan == null || scan.Running || scan.Failure != null || scan.Generation == 0 || scan.Count > capacity) return null;

            var result = new ThermalVisionRegionPartition(capacity);
            for (int i = 0; i < scan.Count; i++) result.regions.Add(scan[i]);
            return result;
        }


        private bool Append(List<Region> target, Region item)
        {
            if (target.Count >= capacity) return false;
            target.Add(item);
            return true;
        }


        private bool Slab(List<Region> target, Vector3D min, Vector3D max, float kelvin)
        { return !Valid(min, max) || Append(target, new Region(min, max, kelvin)); }


        private bool Subtract(Region source, Region cut, List<Region> target)
        {
            Vector3D lo = Vector3D.Max(source.Min, cut.Min), hi = Vector3D.Min(source.Max, cut.Max);
            if (!Valid(lo, hi)) return Append(target, source);
            return Slab(target, source.Min, new Vector3D(lo.X, source.Max.Y, source.Max.Z), source.Kelvin)

                && Slab(target, new Vector3D(hi.X, source.Min.Y, source.Min.Z), source.Max, source.Kelvin)

                && Slab(target, new Vector3D(lo.X, source.Min.Y, source.Min.Z), new Vector3D(hi.X, lo.Y, source.Max.Z), source.Kelvin)

                && Slab(target, new Vector3D(lo.X, hi.Y, source.Min.Z), new Vector3D(hi.X, source.Max.Y, source.Max.Z), source.Kelvin)

                && Slab(target, new Vector3D(lo.X, lo.Y, source.Min.Z), new Vector3D(hi.X, hi.Y, lo.Z), source.Kelvin)

                && Slab(target, new Vector3D(lo.X, lo.Y, hi.Z), new Vector3D(hi.X, hi.Y, source.Max.Z), source.Kelvin);
        }


        private static bool Valid(Vector3D min, Vector3D max)
        { return min.X < max.X && min.Y < max.Y && min.Z < max.Z; }

        private static bool Finite(Vector3D v)
        { return !double.IsNaN(v.X + v.Y + v.Z) && !double.IsInfinity(v.X + v.Y + v.Z); }
    }
}

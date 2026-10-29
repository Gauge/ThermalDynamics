using System;
using VRageMath;

namespace Thermodynamics
{
    public static class HeatSourceMath
    {
        public const double NearFieldMetres = 1.0;

        public static bool ValidPosition(Vector3D position)
        {
            return !double.IsNaN(position.X) && !double.IsInfinity(position.X)
                && !double.IsNaN(position.Y) && !double.IsInfinity(position.Y)
                && !double.IsNaN(position.Z) && !double.IsInfinity(position.Z);
        }

        public static float Irradiance(Vector3D source, float watts, float range, Vector3D at)
        {
            if (!(watts > 0f) || float.IsInfinity(watts)
                || !(range > 0f) || float.IsInfinity(range)) return 0f;

            double distanceSquared = (source - at).LengthSquared();
            if (double.IsNaN(distanceSquared) || double.IsInfinity(distanceSquared)) return 0f;
            if (distanceSquared > range * (double)range) return 0f;

            if (distanceSquared < NearFieldMetres) distanceSquared = NearFieldMetres;

            return (float)(watts / (4.0 * Math.PI * distanceSquared));
        }

        public static Vector3 Direction(Vector3D source, Vector3D at, ref MatrixD worldToLocal)
        {
            Vector3D delta = source - at;
            double distanceSquared = delta.LengthSquared();
            if (!(distanceSquared > 0.0) || double.IsInfinity(distanceSquared)) return Vector3.Zero;

            return (Vector3)Vector3D.TransformNormal(delta / Math.Sqrt(distanceSquared), worldToLocal);
        }
    }
}

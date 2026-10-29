using System;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics
{
    /// <summary>Compare cell-to-cell shadow transforms without mistaking grid-size scale for rotation.</summary>
    internal static class ThermalOccluderTransform
    {
        internal static bool Changed(SunShadowMap.Occluder previous, int previousVersion,
            SunShadowMap.Occluder current)
        {
            if (previous.Id != current.Id || !ReferenceEquals(previous.Model, current.Model)
                || current.Model == null || previousVersion != current.Model.Version) return true;
            return Changed(ref previous.ToOccluder, ref current.ToOccluder);
        }

        internal static bool Changed(ref MatrixD previous, ref MatrixD current)
        {
            // Translation is measured in occluder cells, as it was before this comparison
            // accounted for the receiver-size / occluder-size scale in the basis vectors.
            if (!(Vector3D.DistanceSquared(previous.Translation, current.Translation) <= 1d)) return true;
            if (!Valid(previous.Determinant()) || !Valid(current.Determinant())) return true;
            return DirectionOrScaleChanged(previous.Forward, current.Forward)
                || DirectionOrScaleChanged(previous.Up, current.Up);
        }

        private static bool Valid(double determinant)
        {
            return determinant != 0d && !double.IsNaN(determinant) && !double.IsInfinity(determinant);
        }

        private static bool DirectionOrScaleChanged(Vector3D previous, Vector3D current)
        {
            double previousSquared = previous.LengthSquared();
            double currentSquared = current.LengthSquared();
            if (!(previousSquared > 0d) || double.IsInfinity(previousSquared)
                || !(currentSquared > 0d) || double.IsInfinity(currentSquared)) return true;

            // These transforms have uniform scale. A real change alters ray coordinates
            // even with unchanged directions; ignore only rotation's double roundoff.
            if (Math.Abs(previousSquared - currentSquared)
                > 1e-10d * Math.Max(previousSquared, currentSquared)) return true;

            double cosine = Vector3D.Dot(previous, current)
                / (Math.Sqrt(previousSquared) * Math.Sqrt(currentSquared));
            return !(cosine >= 0.9994d);
        }
    }
}

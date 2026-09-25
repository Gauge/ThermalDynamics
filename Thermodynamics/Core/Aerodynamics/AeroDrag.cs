using System;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// The subsonic drag model's coefficients: a forebody pressure <c>P0 + P2·cos²φ</c> that can go
    /// negative on a rounded shoulder, and base suction for flow that stayed attached against flow that
    /// left a bluff front and has not reattached. Fitted to White's Table 7.3; see aerodynamics.md, Round 4.
    /// </summary>
    public struct AeroCoefficients
    {
        /// <summary>The part of a surface's pressure coefficient that does not depend on its inclination; negative is shoulder suction.</summary>
        public float Offset;

        /// <summary>The part that grows with cos²φ, the square of how squarely the surface faces the flow.</summary>
        public float Stagnation;

        /// <summary>Base suction where the flow reaches the base attached, or reattached along the sides.</summary>
        public float BaseAttached;

        /// <summary>Base suction behind a bluff front whose flow has separated and not reattached.</summary>
        public float BaseSeparated;

        /// <summary>The fineness at which flow that left a bluff front has reattached along the sides.</summary>
        public float ReattachLength;
    }

    /// <summary>Drag of an <see cref="AeroView"/>: forebody pressure, base suction and skin friction.</summary>
    public static class AeroDrag
    {
        /// <summary>Below this angle to the flow a leeward surface keeps its flow attached.</summary>
        public const float AttachedDegrees = 12f;

        /// <summary>Above this angle to the flow a leeward surface has shed its flow into the wake.</summary>
        public const float SeparatedDegrees = 22f;

        private static readonly float AttachedSine = (float)Math.Sin(AttachedDegrees * Math.PI / 180d);
        private static readonly float SeparatedSine = (float)Math.Sin(SeparatedDegrees * Math.PI / 180d);

        /// <summary>The hull's length along the flow over the square root of its silhouette.</summary>
        public static float Fineness(AeroView view)
        {
            return view.Silhouette > 0f ? view.Length / (float)Math.Sqrt(view.Silhouette) : 0f;
        }

        /// <summary>
        /// Base suction coefficient for a front of bluffness <paramref name="bluff"/> — one for a flat
        /// front — at a fineness.
        /// </summary>
        public static float BaseSuction(AeroCoefficients c, float bluff, float fineness)
        {
            float length = c.ReattachLength > 1e-4f ? c.ReattachLength : 1e-4f;
            float ratio = fineness / length;
            float r2 = ratio * ratio;
            float r4 = r2 * r2;
            float unattached = 1f / (1f + (r4 * r4));
            return c.BaseAttached + (bluff * (c.BaseSeparated - c.BaseAttached) * unattached);
        }

        /// <summary>
        /// Turbulent flat-plate skin friction, Prandtl's 0.074 Re^-0.2, for a Reynolds number on the
        /// hull's length.
        /// </summary>
        public static float SkinFriction(float reynolds)
        {
            if (!(reynolds > 1f)) return 0f;
            return 0.074f * (float)Math.Pow(reynolds, -0.2);
        }

        /// <summary>
        /// Drag area C_D·A in m², and the point the drag acts through in grid-local metres. The
        /// point is the centre of each contribution weighted by its share of the drag.
        /// </summary>
        public static float DragArea(AeroView view, AeroCoefficients c, float friction, out Vector3 centre)
        {
            centre = Vector3.Zero;
            if (view == null || view.Silhouette <= 0f) return 0f;

            float total = 0f;
            float facing = 0f;
            Vector3 moment = Vector3.Zero;

            for (int i = 0; i < view.Front.Count; i++)
            {
                AeroElement e = view.Front[i];
                float squared = e.Incidence * e.Incidence;
                float share = (c.Offset + (c.Stagnation * squared)) * e.Area;
                facing += squared * e.Area;
                total += share;
                moment += e.Centre * share;
            }

            float suction = BaseSuction(c, facing / view.Silhouette, Fineness(view));
            for (int i = 0; i < view.Base.Count; i++)
            {
                AeroElement e = view.Base[i];
                float share = suction * e.Area * Separated(e.Incidence);
                total += share;
                moment += e.Centre * share;
            }

            float skin = friction * view.Wetted;
            total += skin;
            moment += view.WettedCentre * skin;

            if (total > 0f) centre = moment / total;
            return total;
        }

        /// <summary>Drag coefficient on the silhouette.</summary>
        public static float Coefficient(AeroView view, AeroCoefficients c, float friction)
        {
            Vector3 centre;
            float area = DragArea(view, c, friction, out centre);
            return view != null && view.Silhouette > 0f ? area / view.Silhouette : 0f;
        }

        /// <summary>
        /// How much of a leeward surface sits in separated flow, from its incidence: the sine of the
        /// angle its surface meets the flow at. Zero at <see cref="AttachedDegrees"/> and below, one at
        /// <see cref="SeparatedDegrees"/> and above; see aerodynamics.md, Round 2.
        /// </summary>
        public static float Separated(float incidence)
        {
            if (incidence <= AttachedSine) return 0f;
            if (incidence >= SeparatedSine) return 1f;
            float t = (incidence - AttachedSine) / (SeparatedSine - AttachedSine);
            return t * t * (3f - (2f * t));
        }
    }
}

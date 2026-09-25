using System;
using VRageMath;

namespace Thermodynamics.Core
{
    /// <summary>
    /// The subsonic drag model's coefficients: forebody pressure and its falloff with a surface's
    /// inclination, and base suction against the hull's fineness. Fitted to White's Table 7.3 cones
    /// and flat-faced cylinders; see aerodynamics.md, Step 1's criteria.
    /// </summary>
    public struct AeroCoefficients
    {
        /// <summary>Pressure coefficient on a surface square to the flow.</summary>
        public float Forebody;

        /// <summary>Exponent on the incidence a surface's pressure falls off with.</summary>
        public float Falloff;

        /// <summary>Base suction on a hull with no length behind its front face.</summary>
        public float BaseNear;

        /// <summary>Base suction on a hull long enough for the flow to reattach along its sides.</summary>
        public float BaseFar;

        /// <summary>The fineness over which base suction falls from near to far.</summary>
        public float BaseLength;
    }

    /// <summary>Drag of an <see cref="AeroView"/>: forebody pressure, base suction and skin friction.</summary>
    public static class AeroDrag
    {
        /// <summary>The hull's length along the flow over the square root of its silhouette.</summary>
        public static float Fineness(AeroView view)
        {
            return view.Silhouette > 0f ? view.Length / (float)Math.Sqrt(view.Silhouette) : 0f;
        }

        /// <summary>Base suction coefficient at a fineness.</summary>
        public static float BaseSuction(AeroCoefficients c, float fineness)
        {
            float scale = c.BaseLength > 1e-4f ? c.BaseLength : 1e-4f;
            return c.BaseFar + ((c.BaseNear - c.BaseFar) * (float)Math.Exp(-fineness / scale));
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

            float falloff = c.Falloff;
            float total = 0f;
            Vector3 moment = Vector3.Zero;

            for (int i = 0; i < view.Front.Count; i++)
            {
                AeroElement e = view.Front[i];
                float share = c.Forebody * e.Area * Weight(e.Incidence, falloff);
                total += share;
                moment += e.Centre * share;
            }

            float suction = BaseSuction(c, Fineness(view));
            for (int i = 0; i < view.Base.Count; i++)
            {
                AeroElement e = view.Base[i];
                float share = suction * e.Area * Weight(e.Incidence, falloff);
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

        private static float Weight(float incidence, float falloff)
        {
            if (incidence <= 0f) return 0f;
            return incidence >= 1f ? 1f : (float)Math.Pow(incidence, falloff);
        }
    }
}

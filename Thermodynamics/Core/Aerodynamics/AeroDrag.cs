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

        /// <summary>
        /// Round 6's in-sample fit to every sourced body, at radius 24, through the surface-read normal.
        /// Out of sample it reaches 15 of 22 bodies within 25 %, reads rounded noses too draggy, and holds
        /// within 10 % from radius 4 to 24; see aerodynamics.md, Round 6.
        /// </summary>
        public static AeroCoefficients Fitted
        {
            get
            {
                return new AeroCoefficients
                {
                    Offset = -0.08793f,
                    Stagnation = 0.56124f,
                    BaseAttached = 0.35951f,
                    BaseSeparated = 0.56396f,
                    ReattachLength = 1.21170f,
                };
            }
        }
    }

    /// <summary>The sums of one view the drag model reads, and their moments in grid-local metres.</summary>
    public struct AeroSummary
    {
        /// <summary>Projected windward area, m².</summary>
        public float Silhouette;

        /// <summary>First moment of <see cref="Silhouette"/>.</summary>
        public Vector3 SilhouetteMoment;

        /// <summary>Projected windward area weighted by cos²φ, m².</summary>
        public float Facing;

        /// <summary>First moment of <see cref="Facing"/>.</summary>
        public Vector3 FacingMoment;

        /// <summary>Projected leeward area in separated flow, m².</summary>
        public float Base;

        /// <summary>First moment of <see cref="Base"/>.</summary>
        public Vector3 BaseMoment;

        /// <summary>Wetted skin, m².</summary>
        public float Wetted;

        /// <summary>First moment of <see cref="Wetted"/>.</summary>
        public Vector3 WettedMoment;

        /// <summary>Length along the flow over the square root of the silhouette.</summary>
        public float Fineness;
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
        /// The sums the drag model reads from a view, so that a drag is a handful of multiplies: what a
        /// profile stores for each wind direction (aerodynamics.md, AR19).
        /// </summary>
        public static AeroSummary Summarise(AeroView view)
        {
            AeroSummary summary = new AeroSummary();
            if (view == null || view.Silhouette <= 0f) return summary;

            for (int i = 0; i < view.Front.Count; i++)
            {
                AddFront(ref summary, view.Front[i]);
            }

            for (int i = 0; i < view.Base.Count; i++)
            {
                AddBase(ref summary, view.Base[i]);
            }

            summary.Wetted = view.Wetted;
            summary.WettedMoment = view.WettedCentre * view.Wetted;
            summary.Fineness = Fineness(view);
            return summary;
        }

        internal static void AddFront(ref AeroSummary summary, AeroElement e)
        {
            float squared = e.Incidence * e.Incidence * e.Area;
            summary.Silhouette += e.Area;
            summary.SilhouetteMoment += e.Centre * e.Area;
            summary.Facing += squared;
            summary.FacingMoment += e.Centre * squared;
        }

        internal static void AddBase(ref AeroSummary summary, AeroElement e)
        {
            float separated = e.Area * Separated(e.Incidence);
            summary.Base += separated;
            summary.BaseMoment += e.Centre * separated;
        }

        /// <summary>
        /// Drag area C_D·A in m², and the point the drag acts through in grid-local metres: the centre
        /// of each contribution weighted by its share of the drag.
        /// </summary>
        public static float DragArea(AeroSummary s, AeroCoefficients c, float friction, out Vector3 centre)
        {
            centre = Vector3.Zero;
            if (s.Silhouette <= 0f) return 0f;

            float suction = BaseSuction(c, s.Facing / s.Silhouette, s.Fineness);

            float total = (c.Offset * s.Silhouette) + (c.Stagnation * s.Facing)
                + (suction * s.Base) + (friction * s.Wetted);
            Vector3 moment = (s.SilhouetteMoment * c.Offset) + (s.FacingMoment * c.Stagnation)
                + (s.BaseMoment * suction) + (s.WettedMoment * friction);

            if (total > 0f) centre = moment / total;
            return total;
        }

        /// <summary>Drag area of a view; see <see cref="DragArea(AeroSummary, AeroCoefficients, float, out Vector3)"/>.</summary>
        public static float DragArea(AeroView view, AeroCoefficients c, float friction, out Vector3 centre)
        {
            return DragArea(Summarise(view), c, friction, out centre);
        }

        /// <summary>Drag coefficient on the silhouette.</summary>
        public static float Coefficient(AeroSummary summary, AeroCoefficients c, float friction)
        {
            Vector3 centre;
            float area = DragArea(summary, c, friction, out centre);
            return summary.Silhouette > 0f ? area / summary.Silhouette : 0f;
        }

        /// <summary>Drag coefficient of a view on its silhouette.</summary>
        public static float Coefficient(AeroView view, AeroCoefficients c, float friction)
        {
            return Coefficient(Summarise(view), c, friction);
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

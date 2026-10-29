using System;
using VRageMath;

namespace Thermodynamics.Presentation
{
    // Non-quantitative, neutral depth bands preserve opaque native geometry for
    // characters, terrain, asteroids and grids without temperature data. They are
    // deliberately outside the thermal palette and never contribute to exposure.
    public static class ThermalVisionContext
    {
        public const int Layers = 4;

        public static double Distance(int layer, double near, double far)
        {
            if(layer<0 || layer>=Layers || near<=0 || far<=near
                || double.IsNaN(near+far) || double.IsInfinity(near+far))
                throw new ArgumentException("Invalid context depth range");
            return near*Math.Pow(far/near,(double)layer/Layers);
        }

        public static Vector4 Colour(int layer)
        {
            if(layer<0 || layer>=Layers) throw new ArgumentException("Invalid context layer");
            // Blue-grey context is separate from both white-hot and cividis heat.
            float brightness=1f-.65f*layer/(Layers-1);
            return new Vector4(.12f*brightness,.17f*brightness,.2f*brightness,1);
        }
    }
}

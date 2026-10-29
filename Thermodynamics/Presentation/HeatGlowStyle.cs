using VRageMath;

namespace Thermodynamics.Presentation
{
    public static class HeatGlowStyle
    {
        public const double DrawRange = 2000.0;
        public const int MaxQuads = 4000;
        public const int MaxLights = 32;
        public const float HaloScale = 1.12f;
        public const float SurfaceIntensity = 8f;


        public static Vector4 LinearEmission(Vector3 srgb, float glow)
        {
            float brightness = !(glow > 0f) ? 0f : glow > 1f ? 1f : glow;
            return new Vector4(srgb.ToLinearRGB() * (brightness * SurfaceIntensity), brightness);
        }


        public static Vector4 BillboardColour(Vector4 linearEmission, float visibility)
        {
            float fade = !(visibility > 0f) ? 0f : visibility > 1f ? 1f : visibility;
            return (linearEmission * fade).ToSRGB();
        }


        public static float StandOff(float gridSize)
        {
            return gridSize * 0.008f;
        }


        public static double CullRadius(Vector3 halfExtents, float gridSize)
        {
            return halfExtents.Length() * HaloScale + StandOff(gridSize);
        }


        public static float RangeFade(double distance, double range)
        {
            if (!(distance >= 0.0) || !(range > 0.0) || distance >= range) return 0f;
            return Smooth((float)((range - distance) / (range * 0.2)));
        }


        public static float FacingFade(double cosine)
        {
            return Smooth((float)(cosine / 0.25));
        }


        private static float Smooth(float value)
        {
            if (!(value > 0f)) return 0f;
            if (value >= 1f) return 1f;
            return value * value * (3f - 2f * value);
        }
    }
}

using System;

namespace Thermodynamics.Core
{
    public struct DragProfile
    {
        private readonly float f0, f1, f2, f3, f4, f5;

        public readonly bool IsSet;


        private DragProfile(float a, float b, float c, float d, float e, float f)
        {
            f0 = a; f1 = b; f2 = c; f3 = d; f4 = e; f5 = f;
            IsSet = true;
        }


        public static DragProfile Of(float a, float b, float c, float d, float e, float f)
        {
            return new DragProfile(Clamp(a), Clamp(b), Clamp(c), Clamp(d), Clamp(e), Clamp(f));
        }

        /// <summary>Compare exported face multipliers, including an unset profile's unit weights.</summary>
        public bool HasSameWeights(DragProfile other)
        {
            for (int face = 0; face < 6; face++)
            {
                if (this[face] != other[face]) return false;
            }
            return true;
        }

        public float this[int face]
        {
            get
            {
                if (!IsSet) return 1f;

                switch (face)
                {
                    case 0: return f0;
                    case 1: return f1;
                    case 2: return f2;
                    case 3: return f3;
                    case 4: return f4;
                    default: return f5;
                }
            }
        }


        private static float Clamp(float value)
        {
            if (!(value >= 0f)) return 1f;
            return value > 1f ? 1f : value;
        }
    }
}

using System;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The original mod's formulas, reproduced verbatim so tests can demonstrate exactly how the
    /// rewritten model differs. Nothing in the shipping code calls any of this.
    /// </summary>
    public static class LegacyFormulas
    {

        public static int LargestFace(Vector3I vector)
        {
            int s1 = 1;
            int s2 = 1;
            for (int i = 0; i < 3; i++)
            {
                if (vector[i] >= s1)
                {
                    s2 = s1;
                    s1 = vector[i];
                }
            }
            return s1 * s2;
        }


        public static Vector3 TemperatureColor(float temp, float max, float low, float high)
        {
            float t = Math.Max(0, Math.Min(max, temp));

            float h = 240f / 360f;
            float s = 1;
            float v = 0.5f;

            if (t < low)
            {
                v = (1.5f * (t / low)) - 1;
            }

            else if (t < high)
            {
                h = (240f - ((t - low) / (high - low) * 240f)) / 360f;
            }
            else
            {
                h = 0;
                s = 1 - (2 * ((t - high) / (max - high)));
            }

            return new Vector3(h, s, v);
        }

        /// <summary>A cell's per-neighbour conduction coefficient, as the original computed it.</summary>

        /// <summary>A cell's per-neighbour conduction coefficient, as the original computed it.</summary>
        public sealed class Cell
        {
            public float Conductivity;
            public float SpecificHeat;
            public float Mass;
            public float GridSize;
            public float ExposedSurfaceMultiplier = 1f;
            public Vector3I Extents = Vector3I.One;
            public float Temperature;

            public float Area
            {
                get { return GridSize * GridSize * ExposedSurfaceMultiplier; }
            }


            public float C(float timeScaleRatio)
            {
                return (1f / (SpecificHeat * Mass * GridSize)) * timeScaleRatio;
            }


            public float K()
            {
                return Conductivity * (SpecificHeat * Mass * GridSize)
                    / (5f * Area * LargestFace(Extents));
            }
        }


        public static float ConductionDelta(Cell self, Cell neighbour, int touchingSurfaces, float timeScaleRatio)
        {
            float area = Math.Min(self.Area, neighbour.Area);
            float kA = self.K() * area * touchingSurfaces;
            return self.C(timeScaleRatio) * kA * (neighbour.Temperature - self.Temperature);
        }
    }
}

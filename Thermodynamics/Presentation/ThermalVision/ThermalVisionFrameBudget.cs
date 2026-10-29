using System;
using VRageMath;

namespace Thermodynamics.Presentation
{
    // A budget counts submitted triangles, including the two triangles in a quad.
    // Native-depth context is independent, so exhausting detail never removes silhouettes.
    public sealed class ThermalVisionFrameBudget
    {
        public const int GridTriangles = 16384;
        public const int SkyTriangles = 2048;
        public int Remaining { get; private set; }
        public int SliceRemaining { get; private set; }
        public bool Limited { get; private set; }

        public void Reset(int triangles)
        {
            if(triangles<0) throw new ArgumentException("Nonnegative triangle budget required");
            Remaining=SliceRemaining=triangles;
            Limited=false;
        }

        // Reserve an equal share for each remaining visible grid. Unused capacity
        // carries forward; no allocations, sorting or per-patch timer calls are needed.
        public void BeginSlice(int remainingObjects)
        {
            if(remainingObjects<1) throw new ArgumentException("Positive object count required");
            SliceRemaining=Remaining/remainingObjects;
        }

        public bool Take(int triangles)
        {
            if(triangles<1) throw new ArgumentException("Positive triangle count required");
            if(triangles>SliceRemaining || triangles>Remaining) { Limited=true; return false; }
            SliceRemaining-=triangles; Remaining-=triangles;
            return true;
        }

        // Whole shapes can reduce detail before submitting any triangles. Keep
        // that visible to the HUD even when every reduced triangle fits.
        public void ReportLimited() { Limited=true; }

        public static int CellTriangles(ThermalVisionSurfaceField field,Vector3D eye,int capTriangles)
        {
            int triangles=capTriangles;
            for(int axis=0;axis<3;axis++) for(int side=0;side<2;side++)
            {
                double camera=axis==0?eye.X:axis==1?eye.Y:eye.Z;
                Vector3D bound=side==1?field.Bounds.Max:field.Bounds.Min;
                double plane=axis==0?bound.X:axis==1?bound.Y:bound.Z;
                bool entering=(camera-plane)*(side==1?1:-1)>0;
                // Paired exits must remain: a neighbor may not be admitted.
                triangles+=entering?field.Face(axis,side==1).Count*2:2;
            }
            return triangles;
        }

    }
}

using System.Collections.Generic;
using VRageMath;
namespace Thermodynamics.Core
{
    /// <summary>Detached geometry contract. All returned arrays are caller-owned copies.</summary>
    public static class GridGeometrySnapshot
    {
        public static Dictionary<string,object> Copy(GridModel model,long revision)
        {
            int count = model.BlockCount;
            int cellCount = 0;
            for (int i = 0; i < count; i++) cellCount = checked(cellCount + model.Blocks[i].CellCount);
            var min = new Vector3I[count]; var max = new Vector3I[count]; var mass = new float[count];
            var starts = new int[count+1]; var weights = new float[count*6]; var cells = new Vector3I[cellCount];
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                BlockInstance b = model.Blocks[i]; min[i] = b.Min; max[i] = b.MaxExclusive; mass[i] = b.Mass;
                starts[i] = written;
                System.Array.Copy(b.Cells, 0, cells, written, b.CellCount);
                written += b.CellCount;
                for (int f = 0; f < 6; f++) weights[i*6+f] = b.Drag[f];
            }
            starts[count] = written;
            return new Dictionary<string, object>(9) {
                { "Schema", 1 }, { "Revision", revision }, { "GridSize", model.GridSize },
                { "Min", min }, { "MaxExclusive", max }, { "Mass", mass }, { "Cells", cells },
                { "CellStarts", starts }, { "FaceWeights", weights }
            };
        }
    }
}

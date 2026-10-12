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
            var min = new Vector3I[count]; var max = new Vector3I[count]; var mass = new float[count];
            var starts = new int[count+1]; var weights = new float[count*6]; var cells = new List<Vector3I>();
            for (int i = 0; i < count; i++)
            {
                BlockInstance b = model.Blocks[i]; min[i] = b.Min; max[i] = b.MaxExclusive; mass[i] = b.Mass;
                starts[i] = cells.Count; cells.AddRange(b.Cells);
                for (int f = 0; f < 6; f++) weights[i*6+f] = b.Drag[f];
            }
            starts[count] = cells.Count;
            return new Dictionary<string, object> {
                { "Schema", 1 }, { "Revision", revision }, { "GridSize", model.GridSize },
                { "Min", min }, { "MaxExclusive", max }, { "Mass", mass }, { "Cells", cells.ToArray() },
                { "CellStarts", starts }, { "FaceWeights", weights }
            };
        }
    }
}

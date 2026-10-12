using System.Runtime.CompilerServices;
using Thermodynamics.Core;
namespace Aerodynamics.Core
{
    public partial class AeroGrid
    {
        private static readonly ConditionalWeakTable<GridModel, FixtureGrid> fixtures = new ConditionalWeakTable<GridModel, FixtureGrid>();
        public static implicit operator AeroGrid(GridModel grid)
        {
            if (grid == null) return null;
            FixtureGrid result = fixtures.GetValue(grid, g => new FixtureGrid(g));
            result.Refresh(); return result;
        }
        private sealed class FixtureGrid : AeroGrid
        {
            private readonly GridModel source;
            public FixtureGrid(GridModel source) { this.source = source; GridSize = source.GridSize; }
            public override int Version { get { return source.Version; } set { } }
            private bool populated;
            private int stamp;
            public void Refresh()
            {
                int next = source.Version;
                unchecked { for(int i=0;i<source.BlockCount;i++) { BlockInstance b=source.Blocks[i]; next = next * 31 + b.Mass.GetHashCode(); for(int f=0;f<6;f++)next=next*31+b.Drag[f].GetHashCode(); } }
                if(populated && next==stamp)return;
                populated=true;stamp=next;
                Blocks.Clear(); occupied.Clear(); Min = VRageMath.Vector3I.MaxValue; Max = VRageMath.Vector3I.MinValue;
                foreach (BlockInstance b in source.Blocks)
                {
                    var copy = new AeroBlock { Min = b.Min, MaxExclusive = b.MaxExclusive, Mass = b.Mass, Cells = b.Cells };
                    for (int f = 0; f < 6; f++) copy.Drag[f] = b.Drag[f];
                    Add(copy);
                }
            }
        }
    }
}

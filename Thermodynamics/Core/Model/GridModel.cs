using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public class GridModel : IBlockAdjacency
    {
        public readonly float GridSize;

        private Dictionary<long, BlockInstance> blocksByCell = new Dictionary<long, BlockInstance>();


        private readonly List<BlockInstance> blocks = new List<BlockInstance>();


        private int coolantBlocks;
        private int heatPumpBlocks;


        private readonly List<BlockInstance> stateDependent = new List<BlockInstance>();

        private Vector3I min = Vector3I.MaxValue;
        private Vector3I max = Vector3I.MinValue;
        private bool boundsDirty;


        private readonly CellBitset occupied = new CellBitset();
        private int occupancyVersion = -1;
        private int version;


        public GridModel(float gridSize)
        {
            if (gridSize <= 0f) throw new ArgumentException("gridSize must be positive", "gridSize");
            GridSize = gridSize;
        }

        public float CellFaceArea
        {
            get { return GridSize * GridSize; }
        }

        public int BlockCount
        {
            get { return blocks.Count; }
        }

        public IList<BlockInstance> Blocks
        {
            get { return blocks; }
        }

        public IList<BlockInstance> StateDependentBlocks
        {
            get { return stateDependent; }
        }

        public Vector3I Min
        {

            get { RebuildBoundsIfNeeded(); return min; }
        }

        public Vector3I Max
        {

            get { RebuildBoundsIfNeeded(); return max; }
        }


        public BlockInstance At(Vector3I cell)
        {
            BlockInstance block;
            return blocksByCell.TryGetValue(GridMath.Key(cell), out block) ? block : null;
        }

        public int Version
        {
            get { return version; }
        }


        public CellBitset Occupancy()
        {
            if (occupancyVersion == version) return occupied;

            occupied.Reset(Min - Vector3I.One, Max + new Vector3I(2, 2, 2));

            for (int i = 0; i < blocks.Count; i++)
            {
                Vector3I[] cells = blocks[i].Cells;
                for (int c = 0; c < cells.Length; c++) occupied.Add(cells[c]);
            }

            occupancyVersion = version;
            return occupied;
        }

        internal bool PreferSparseCellStorage
        {
            get
            {
                if (blocks.Count == 0) return false;
                Vector3I lower = Min, upper = Max;
                long volume = ((long)upper.X - lower.X + 3)
                    * ((long)upper.Y - lower.Y + 3) * ((long)upper.Z - lower.Z + 3);
                return volume > 65536 && blocksByCell.Count <= volume / 16;
            }
        }

        // The dense prefilter is optional: sparse queries already have an exact cell dictionary.
        internal CellBitset OccupancyForQueries()
        {
            return PreferSparseCellStorage ? null : Occupancy();
        }


        public void EnsureCellCapacity(int cells)
        {
            if (cells <= 0 || blocksByCell.Count > 0) return;

            blocksByCell = new Dictionary<long, BlockInstance>(cells);
            if (blocks.Capacity < cells) blocks.Capacity = cells;
        }


        public BlockInstance Add(BlockInstance block)
        {
            if (block == null) throw new ArgumentNullException("block");

            version++;

            Vector3I[] cells = block.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (blocksByCell.ContainsKey(GridMath.Key(cells[i])))
                {
                    throw new InvalidOperationException(
                        "Cell " + cells[i] + " is already occupied by " + blocksByCell[GridMath.Key(cells[i])].Name);
                }
            }

            for (int i = 0; i < cells.Length; i++)
            {
                blocksByCell[GridMath.Key(cells[i])] = block;
                Grow(cells[i]);
            }

            block.GridSlot = blocks.Count;
            blocks.Add(block);
            if (block.HasStateDependentSealing) stateDependent.Add(block);
            if (block.Model.Coolant != null) coolantBlocks++;
            if (block.Model.HeatPump != null) heatPumpBlocks++;
            return block;
        }


        public BlockInstance Add(BlockModel model, Vector3I min, BlockOrientation orientation)
        {
            return Add(new BlockInstance(model, min, orientation));
        }


        public BlockInstance Add(BlockModel model, Vector3I min)
        {
            return Add(new BlockInstance(model, min, BlockOrientation.Identity));
        }


        public bool Remove(BlockInstance block)
        {
            if (block == null || !Holds(block)) return false;

            version++;

            Vector3I[] cells = block.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                BlockInstance occupant;
                long key = GridMath.Key(cells[i]);
                if (blocksByCell.TryGetValue(key, out occupant) && occupant == block)
                {
                    blocksByCell.Remove(key);
                }
            }

            RemoveSlot(block);
            if (block.HasStateDependentSealing) stateDependent.Remove(block);
            if (block.Model.Coolant != null) coolantBlocks--;
            if (block.Model.HeatPump != null) heatPumpBlocks--;
            boundsDirty = true;
            return true;
        }


        private void RemoveSlot(BlockInstance block)
        {
            if (!Holds(block))
            {
                int found = blocks.IndexOf(block);
                if (found < 0) return;
                block.GridSlot = found;
            }

            int slot = block.GridSlot;
            block.GridSlot = -1;

            int last = blocks.Count - 1;
            if (slot != last)
            {
                BlockInstance moved = blocks[last];
                blocks[slot] = moved;
                moved.GridSlot = slot;
            }

            blocks.RemoveAt(last);
        }

        public int CoolantBlockCount
        {
            get { return coolantBlocks; }
        }

        public int HeatPumpBlockCount
        {
            get { return heatPumpBlocks; }
        }


        public BlockInstance GetAtCell(Vector3I cell)
        {
            return GetAtKey(GridMath.Key(cell));
        }


        public BlockInstance GetAtKey(long key)
        {
            BlockInstance block;
            return blocksByCell.TryGetValue(key, out block) ? block : null;
        }


        public BlockInstance GetByKey(long key)
        {
            BlockInstance block;
            if (!blocksByCell.TryGetValue(key, out block)) return null;
            return block.Key == key ? block : null;
        }


        private bool Holds(BlockInstance block)
        {
            int slot = block.GridSlot;
            return slot >= 0 && slot < blocks.Count && ReferenceEquals(blocks[slot], block);
        }


        public bool IsOccupied(Vector3I cell)
        {
            return blocksByCell.ContainsKey(GridMath.Key(cell));
        }


        public List<BlockInstance> Neighbours(BlockInstance block)
        {

            List<BlockInstance> result = new List<BlockInstance>();
            GetNeighbours(block, result);
            return result;
        }


        public void GetNeighbours(BlockInstance block, List<BlockInstance> results)
        {
            GetNeighbours(block, results, null);
        }


        public void GetNeighbours(BlockInstance block, List<BlockInstance> results, List<int> faces)
        {
            GetNeighbours(block, results, faces, null);
        }


        public void GetNeighbours(BlockInstance block, List<BlockInstance> results, List<int> faces,
            CellBitset occupied)
        {
            if (block == null || results == null) return;

            if (block.CellCount == 1)
            {
                Vector3I cell = block.Min;
                long key = GridMath.Key(cell);
                long slot = occupied == null ? -1L : occupied.IndexOf(cell);

                for (int face = 0; face < Face.Count; face++)
                {
                    if (occupied != null && !occupied.ContainsIndex(slot + occupied.IndexStep(face)))
                    {
                        continue;
                    }


                    BlockInstance other = GetAtKey(key + GridMath.KeyByFace[face]);
                    if (other == null || other == block) continue;
                    results.Add(other);
                    if (faces != null) faces.Add(face);
                }
                return;
            }

            GetNeighboursWalkingTheBoundary(block, results, faces);
        }



        public void GetNeighboursWalkingTheBoundary(BlockInstance block, List<BlockInstance> results)
        {
            GetNeighboursWalkingTheBoundary(block, results, null);
        }


        public void GetNeighboursWalkingTheBoundary(BlockInstance block, List<BlockInstance> results, List<int> faces)
        {
            if (block == null || results == null) return;

            Vector3I min = block.Min;
            Vector3I maxExclusive = block.MaxExclusive;

            for (int face = 0; face < Face.Count; face++)
            {
                BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);
                int axis = span.Axis;
                bool positive = span.Positive;
                int slab = span.Slab;
                int u = span.U;
                int v = span.V;

                for (int a = BoxGeometry.Component(min, u); a < BoxGeometry.Component(maxExclusive, u); a++)
                {
                    for (int b = BoxGeometry.Component(min, v); b < BoxGeometry.Component(maxExclusive, v); b++)
                    {
                        Vector3I cell = BoxGeometry.WithComponent(Vector3I.Zero, axis, slab);
                        cell = BoxGeometry.WithComponent(cell, u, a);
                        cell = BoxGeometry.WithComponent(cell, v, b);


                        BlockInstance other = GetAtKey(GridMath.Key(cell) + GridMath.KeyByFace[face]);
                        if (other == null || other == block) continue;
                        if (results.Contains(other)) continue;

                        results.Add(other);
                        if (faces != null) faces.Add(face);
                    }
                }
            }
        }


        public int SharedFaceCount(BlockInstance a, BlockInstance b)
        {
            if (a == null || b == null || a == b) return 0;

            int count = 0;
            Vector3I[] cells = a.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                for (int f = 0; f < Face.Count; f++)
                {
                    if (GetAtCell(cells[i] + Face.Offsets[f]) == b) count++;
                }
            }
            return count;
        }


        private void Grow(Vector3I cell)
        {
            if (boundsDirty) return;
            min = Vector3I.Min(min, cell);
            max = Vector3I.Max(max, cell);
        }


        private void RebuildBoundsIfNeeded()
        {
            if (!boundsDirty) return;

            min = Vector3I.MaxValue;
            max = Vector3I.MinValue;
            foreach (KeyValuePair<long, BlockInstance> entry in blocksByCell)
            {
                Vector3I cell = GridMath.FromKey(entry.Key);
                min = Vector3I.Min(min, cell);
                max = Vector3I.Max(max, cell);
            }
            boundsDirty = false;
        }
    }
}

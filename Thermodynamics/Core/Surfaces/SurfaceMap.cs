using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public class SurfaceMap
    {
        private readonly Dictionary<long, long> cells = new Dictionary<long, long>();


        private static long KeyOf(Vector3I cell)
        {
            return GridMath.Key(cell);
        }

        private long[] rebuildKeys = new long[0];
        private long[] rebuildSelves = new long[0];

        private const int StructuralShift = 32;
        private const long LiveMask = 0xFFFFFFFFL;


        private static long Pack(int live, int structural)
        {
            return (live & LiveMask) | ((long)structural << StructuralShift);
        }


        private static int Live(long packed)
        {
            return (int)(packed & LiveMask);
        }


        private static int Structural(long packed)
        {
            return (int)(packed >> StructuralShift);
        }

        public int CellCount
        {
            get { return cells.Count; }
        }

        public IEnumerable<Vector3I> Cells
        {
            get
            {
                foreach (long key in cells.Keys) yield return GridMath.FromKey(key);
            }
        }


        public int GetState(Vector3I cell)
        {
            long packed;
            return cells.TryGetValue(KeyOf(cell), out packed) ? Live(packed) : 0;
        }


        public bool HasCell(Vector3I cell)
        {
            return cells.ContainsKey(KeyOf(cell));
        }


        public void AddBlock(BlockInstance block)
        {
            if (block == null) return;

            Vector3I[] blockCells = block.Cells;
            int[] self = block.SelfSurfaces;

            int[] structural = block.StructuralSurfaces;

            for (int i = 0; i < blockCells.Length; i++)
            {
                cells[KeyOf(blockCells[i])] = Pack(
                    CellSurface.SelfOnly(self[i]),
                    CellSurface.SelfOnly(structural == null ? self[i] : structural[i]));
            }

            for (int i = 0; i < blockCells.Length; i++)
            {
                RefreshCell(blockCells[i]);
                RefreshNeighboursOf(blockCells[i]);
            }
        }


        public void RemoveBlock(BlockInstance block)
        {
            if (block == null) return;

            Vector3I[] blockCells = block.Cells;
            for (int i = 0; i < blockCells.Length; i++)
            {
                cells.Remove(KeyOf(blockCells[i]));
            }

            for (int i = 0; i < blockCells.Length; i++)
            {
                RefreshNeighboursOf(blockCells[i]);
            }
        }


        public void Rebuild(GridModel grid)
        {
            cells.Clear();
            if (grid == null) return;

            IList<BlockInstance> blocks = grid.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                BlockInstance block = blocks[b];
                Vector3I[] blockCells = block.Cells;
                int[] self = block.SelfSurfaces;
                int[] structural = block.StructuralSurfaces;
                for (int i = 0; i < blockCells.Length; i++)
                {
                    cells[KeyOf(blockCells[i])] = Pack(
                        CellSurface.SelfOnly(self[i]),
                        CellSurface.SelfOnly(structural == null ? self[i] : structural[i]));
                }
            }

            int count = cells.Count;
            if (rebuildKeys.Length < count)
            {
                rebuildKeys = new long[count];
                rebuildSelves = new long[count];
            }

            long[] keys = rebuildKeys;
            long[] selves = rebuildSelves;
            int at = 0;
            foreach (KeyValuePair<long, long> entry in cells)
            {
                keys[at] = entry.Key;
                selves[at] = entry.Value;
                at++;
            }

            long[] byFace = GridMath.KeyByFace;
            for (int i = 0; i < count; i++)
            {
                long key = keys[i];
                int live = CellSurface.SelfOnly(Live(selves[i]));
                int structural = CellSurface.SelfOnly(Structural(selves[i]));

                for (int face = 0; face < Face.Count; face++)
                {
                    long neighbour;
                    if (!cells.TryGetValue(key + byFace[face], out neighbour)) continue;

                    live |= CellSurface.NeighbourContribution(Live(neighbour), face);
                    structural |= CellSurface.NeighbourContribution(Structural(neighbour), face);
                }


                cells[key] = Pack(live, structural);
            }
        }


        public void RefreshCell(Vector3I cell)
        {
            long packed;

            long key = KeyOf(cell);
            if (!cells.TryGetValue(key, out packed)) return;

            int live = CellSurface.SelfOnly(Live(packed));
            int structural = CellSurface.SelfOnly(Structural(packed));

            long[] byFace = GridMath.KeyByFace;
            for (int face = 0; face < Face.Count; face++)
            {
                long neighbour;
                if (cells.TryGetValue(key + byFace[face], out neighbour))
                {
                    live |= CellSurface.NeighbourContribution(Live(neighbour), face);
                    structural |= CellSurface.NeighbourContribution(Structural(neighbour), face);
                }
            }


            cells[key] = Pack(live, structural);
        }


        private void RefreshNeighboursOf(Vector3I cell)
        {
            for (int face = 0; face < Face.Count; face++)
            {
                RefreshCell(cell + Face.Offsets[face]);
            }
        }


        public bool IsFaceSealed(Vector3I cell, int face)
        {

            int state = GetState(cell);
            if (CellSurface.SelfAirtight(state, face)) return true;


            int neighbourState = GetState(cell + Face.Offsets[face]);
            return CellSurface.SelfAirtight(neighbourState, Face.Opposite(face));
        }


        public bool IsFullySealed(Vector3I cell)
        {
            return CellSurface.IsFullySealed(GetState(cell));
        }


        public int GetStructuralState(Vector3I cell)
        {
            long packed;
            return cells.TryGetValue(KeyOf(cell), out packed) ? Structural(packed) : 0;
        }


        public bool IsFaceSealedStructurally(Vector3I cell, int face)
        {

            int state = GetStructuralState(cell);
            if (CellSurface.SelfAirtight(state, face)) return true;


            int neighbourState = GetStructuralState(cell + Face.Offsets[face]);
            return CellSurface.SelfAirtight(neighbourState, Face.Opposite(face));
        }


        public bool IsFullySealedStructurally(Vector3I cell)
        {
            return CellSurface.IsFullySealed(GetStructuralState(cell));
        }


        public void CopyStructuralSealing(Vector3I min, Vector3I maxExclusive, byte[] sealing)
        {
            CopyStructuralSealing(min, maxExclusive, sealing, null);
        }


        internal void CopyStructuralSealing(Vector3I min, Vector3I maxExclusive, byte[] sealing,
            List<int> written)
        {
            if (sealing == null) return;

            int sizeX = maxExclusive.X - min.X;
            int sizeY = maxExclusive.Y - min.Y;
            int sizeZ = maxExclusive.Z - min.Z;
            if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) return;

            foreach (KeyValuePair<long, long> entry in cells)
            {
                Vector3I at = GridMath.FromKey(entry.Key);
                int x = at.X - min.X;
                int y = at.Y - min.Y;
                int z = at.Z - min.Z;
                if (x < 0 || x >= sizeX || y < 0 || y >= sizeY || z < 0 || z >= sizeZ) continue;

                long index = (((long)z * sizeY) + y) * sizeX + x;
                if (index >= sealing.Length) continue;

                byte value = (byte)(Structural(entry.Value) & CellSurface.SelfAirtightMask);
                sealing[index] = value;
                if (written != null && value != 0) written.Add((int)index);
            }
        }


        public void GetExposedFaces(BlockInstance block, RoomMap rooms, int[] resultsByFace)
        {
            if (resultsByFace == null || resultsByFace.Length < Face.Count)
            {
                throw new ArgumentException("resultsByFace must have at least six entries");
            }

            Array.Clear(resultsByFace, 0, Face.Count);
            if (block == null) return;

            if (block.CellCount == 1)
            {
                Vector3I cell = block.Min;

                int state = GetState(cell);
                for (int face = 0; face < Face.Count; face++)
                {
                    if (CellSurface.NeighbourAirtight(state, face)) continue;
                    if (rooms != null && !rooms.IsExternal(cell + Face.Offsets[face])) continue;
                    resultsByFace[face] = 1;
                }
                return;
            }

            GetExposedFacesWalkingTheBoundary(block, rooms, resultsByFace);
        }


        public void GetExposedFacesWalkingTheBoundary(BlockInstance block, RoomMap rooms, int[] resultsByFace)
        {
            if (block == null || resultsByFace == null) return;

            Vector3I min = block.Min;
            Vector3I maxExclusive = block.MaxExclusive;

            for (int face = 0; face < Face.Count; face++)
            {
                BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);
                Vector3I offset = span.Offset;
                int axis = span.Axis;
                bool positive = span.Positive;
                int slab = span.Slab;
                int u = span.U;
                int v = span.V;

                int count = 0;

                for (int a = BoxGeometry.Component(min, u); a < BoxGeometry.Component(maxExclusive, u); a++)
                {
                    for (int b = BoxGeometry.Component(min, v); b < BoxGeometry.Component(maxExclusive, v); b++)
                    {
                        Vector3I cell = BoxGeometry.WithComponent(Vector3I.Zero, axis, slab);
                        cell = BoxGeometry.WithComponent(cell, u, a);
                        cell = BoxGeometry.WithComponent(cell, v, b);


                        int state = GetState(cell);
                        Vector3I neighbour = cell + offset;

                        if (CellSurface.NeighbourAirtight(state, face)) continue;

                        if (rooms != null && !rooms.IsExternal(neighbour)) continue;

                        count++;
                    }
                }

                resultsByFace[face] = count;
            }
        }


        public void GetRoomContacts(BlockInstance block, RoomMap rooms, List<RoomContact> results)
        {
            if (results == null || block == null || rooms == null) return;

            Vector3I min = block.Min;
            Vector3I maxExclusive = block.MaxExclusive;

            for (int face = 0; face < Face.Count; face++)
            {
                BoxGeometry.FaceSpan span = BoxGeometry.Span(min, maxExclusive, face);
                Vector3I offset = span.Offset;
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

                        Vector3I neighbour = cell + offset;

                        if (HasCell(neighbour)) continue;

                        int room = rooms.RoomIndexOf(neighbour);
                        if (room < 0 || rooms.IsVented(room)) continue;

                        Accumulate(results, room);
                    }
                }
            }
        }


        private static void Accumulate(List<RoomContact> results, int room)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].RoomIndex != room) continue;

                results[i] = new RoomContact(room, results[i].Faces + 1);
                return;
            }
            results.Add(new RoomContact(room, 1));
        }


        public int[] GetExposedFaces(BlockInstance block, RoomMap rooms)
        {
            int[] result = new int[Face.Count];
            GetExposedFaces(block, rooms, result);
            return result;
        }


        public void Clear()
        {
            cells.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// `SurfaceMap` holds its live and structural layers in one dictionary, packed, and is held
    /// against the two-dictionary map it replaced — kept verbatim below — over every cell and every
    /// neighbouring cell of a census hull and of a shell with a door, after a rebuild and after
    /// blocks are added and removed one at a time (`D8`).
    ///
    /// <para>
    /// The map answers four questions — a cell's live state, its structural state, whether a face
    /// seals in either layer — and every reader of it (the flood fill, exposure, room contacts) is
    /// downstream of those four, so agreement on them is agreement everywhere. The reference is
    /// the old code and not a re-derivation, because the old code is the only oracle for *did this
    /// change anything* (`P4`).
    /// </para>
    /// </summary>
    public class SurfaceMapPackingTests
    {
    public class TwoDictionarySurfaceMap
        {
        private readonly Dictionary<Vector3I, int> states = new Dictionary<Vector3I, int>(Vector3I.Comparer);

        private readonly Dictionary<Vector3I, int> structure = new Dictionary<Vector3I, int>(Vector3I.Comparer);

        public int CellCount
        {
            get { return states.Count; }
        }

        public IEnumerable<Vector3I> Cells
        {
            get { return states.Keys; }
        }


        public int GetState(Vector3I cell)
        {
            int state;
            return states.TryGetValue(cell, out state) ? state : 0;
        }


        public bool HasCell(Vector3I cell)
        {
            return states.ContainsKey(cell);
        }


        public void AddBlock(BlockInstance block)
        {
            if (block == null) return;

            Vector3I[] cells = block.Cells;
            int[] self = block.SelfSurfaces;

            int[] structural = block.StructuralSurfaces;

            for (int i = 0; i < cells.Length; i++)
            {
                states[cells[i]] = CellSurface.SelfOnly(self[i]);
                structure[cells[i]] = CellSurface.SelfOnly(structural == null ? self[i] : structural[i]);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                RefreshCell(cells[i]);
                RefreshNeighboursOf(cells[i]);
            }
        }


        public void RemoveBlock(BlockInstance block)
        {
            if (block == null) return;

            Vector3I[] cells = block.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                states.Remove(cells[i]);
                structure.Remove(cells[i]);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                RefreshNeighboursOf(cells[i]);
            }
        }


        public void Rebuild(GridModel grid)
        {
            states.Clear();
            structure.Clear();
            if (grid == null) return;

            IList<BlockInstance> blocks = grid.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                BlockInstance block = blocks[b];
                Vector3I[] cells = block.Cells;
                int[] self = block.SelfSurfaces;
                int[] structural = block.StructuralSurfaces;
                for (int i = 0; i < cells.Length; i++)
                {
                    states[cells[i]] = CellSurface.SelfOnly(self[i]);
                    structure[cells[i]] = CellSurface.SelfOnly(structural == null ? self[i] : structural[i]);
                }
            }


            List<Vector3I> keys = new List<Vector3I>(states.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                RefreshCell(keys[i]);
            }
        }


        public void RefreshCell(Vector3I cell)
        {
            Refresh(states, cell);
            Refresh(structure, cell);
        }


        private static void Refresh(Dictionary<Vector3I, int> layer, Vector3I cell)
        {
            int state;
            if (!layer.TryGetValue(cell, out state)) return;

            state = CellSurface.SelfOnly(state);
            for (int face = 0; face < Face.Count; face++)
            {
                int neighbourState;
                if (layer.TryGetValue(cell + Face.Offsets[face], out neighbourState))
                {
                    state |= CellSurface.NeighbourContribution(neighbourState, face);
                }
            }
            layer[cell] = state;
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
            int state;
            return structure.TryGetValue(cell, out state) ? state : 0;
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


        public void GetExposedFaces(BlockInstance block, RoomMap rooms, int[] resultsByFace)
        {
            if (resultsByFace == null || resultsByFace.Length < Face.Count)
            {
                throw new ArgumentException("resultsByFace must have at least six entries");
            }

            Array.Clear(resultsByFace, 0, Face.Count);
            if (block == null) return;

            Vector3I min = block.Min;
            Vector3I maxExclusive = block.MaxExclusive;

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I offset = Face.Offsets[face];
                int axis = Face.Axis(face);
                bool positive = BoxGeometry.Component(offset, axis) > 0;

                int slab = positive
                    ? BoxGeometry.Component(maxExclusive, axis) - 1
                    : BoxGeometry.Component(min, axis);

                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

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
                Vector3I offset = Face.Offsets[face];
                int axis = Face.Axis(face);
                bool positive = BoxGeometry.Component(offset, axis) > 0;

                int slab = positive
                    ? BoxGeometry.Component(maxExclusive, axis) - 1
                    : BoxGeometry.Component(min, axis);

                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

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
            states.Clear();
            structure.Clear();
        }
    }


        private static GridBuilder Shell()
        {
            return RoomFixtures.DooredShell();
        }


        private static GridBuilder Census()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            return builder;
        }


        private static void AssertSame(TwoDictionarySurfaceMap expected, SurfaceMap actual, bool layersDiffer, string what)
        {
            Assert.True(expected.CellCount > 0, what + ": the reference map is empty, so agreement proves nothing");
            Assert.Equal(expected.CellCount, actual.CellCount);

            int probed = 0;
            int nonTrivial = 0;
            foreach (Vector3I cell in expected.Cells)
            {
                for (int face = -1; face < Face.Count; face++)
                {
                    Vector3I at = face < 0 ? cell : cell + Face.Offsets[face];
                    int live = expected.GetState(at);
                    int structural = expected.GetStructuralState(at);
                    Assert.True(live == actual.GetState(at), what + ": live state at " + at + " is " + actual.GetState(at) + " packed and " + live + " by two dictionaries");
                    Assert.True(structural == actual.GetStructuralState(at), what + ": structural state at " + at + " differs");
                    Assert.Equal(expected.HasCell(at), actual.HasCell(at));
                    for (int f = 0; f < Face.Count; f++)
                    {
                        Assert.Equal(expected.IsFaceSealed(at, f), actual.IsFaceSealed(at, f));
                        Assert.Equal(expected.IsFaceSealedStructurally(at, f), actual.IsFaceSealedStructurally(at, f));
                    }
                    Assert.Equal(expected.IsFullySealed(at), actual.IsFullySealed(at));
                    Assert.Equal(expected.IsFullySealedStructurally(at), actual.IsFullySealedStructurally(at));
                    probed++;
                    if (live != structural) nonTrivial++;
                }
            }

            Assert.True(probed > 100, what + ": only " + probed + " cells probed");
            if (layersDiffer)
            {
                Assert.True(nonTrivial > 0, what + ": no cell's two layers differ, so the open door made no difference and the packing's high half is untested");
            }
        }

        [Theory]
        [InlineData("shell")]
        [InlineData("census")]

        public void ARebuiltMapAnswersEveryQuestionAsTheTwoDictionariesDid(string which)
        {

            GridBuilder builder = which == "shell" ? Shell() : Census();

            TwoDictionarySurfaceMap expected = new TwoDictionarySurfaceMap();

            SurfaceMap actual = new SurfaceMap();
            expected.Rebuild(builder.Grid);
            actual.Rebuild(builder.Grid);
            AssertSame(expected, actual, false, which + ", rebuilt");
        }

        [Theory]
        [InlineData("shell")]
        [InlineData("census")]

        public void AMapBuiltAndThenEditedBlockByBlockAnswersAsTheTwoDictionariesDid(string which)
        {

            GridBuilder builder = which == "shell" ? Shell() : Census();

            TwoDictionarySurfaceMap expected = new TwoDictionarySurfaceMap();

            SurfaceMap actual = new SurfaceMap();

            IList<BlockInstance> placed = builder.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                expected.AddBlock(placed[i]);
                actual.AddBlock(placed[i]);
            }
            AssertSame(expected, actual, false, which + ", added one at a time");

            for (int i = 0; i < placed.Count; i += 7)
            {
                expected.RemoveBlock(placed[i]);
                actual.RemoveBlock(placed[i]);
            }
            AssertSame(expected, actual, false, which + ", with blocks removed");

            for (int i = 0; i < placed.Count; i++)
            {
                if (!placed[i].HasStateDependentSealing) continue;
                placed[i].IsSealedByDoorState = false;
                placed[i].RefreshSurfaces();
                expected.RemoveBlock(placed[i]); expected.AddBlock(placed[i]);
                actual.RemoveBlock(placed[i]); actual.AddBlock(placed[i]);
            }
            AssertSame(expected, actual, which == "shell", which + ", with doors open");
        }
    }
}

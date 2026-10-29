using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public class RoomMapper
    {
        private enum Phase
        {
            Idle,
            External,
            Interior,
            Done
        }

        private readonly SurfaceMap surfaces;

        private Vector3I[] frontier = new Vector3I[64];
        private int frontierHead;
        private int frontierCount;


        private void FrontierClear()
        {
            frontierHead = 0;
            frontierCount = 0;
        }


        private void FrontierEnqueue(Vector3I cell)
        {
            if (frontierCount == frontier.Length)
            {
                Vector3I[] grown = new Vector3I[frontier.Length * 2];
                for (int i = 0; i < frontierCount; i++)
                {
                    grown[i] = frontier[(frontierHead + i) % frontier.Length];
                }
                frontier = grown;
                frontierHead = 0;
            }

            int at = frontierHead + frontierCount;
            if (at >= frontier.Length) at -= frontier.Length;
            frontier[at] = cell;
            frontierCount++;
        }


        private Vector3I FrontierDequeue()
        {
            Vector3I cell = frontier[frontierHead];
            frontierHead++;
            if (frontierHead == frontier.Length) frontierHead = 0;
            frontierCount--;
            return cell;
        }

        private readonly PagedCellBitset visited = new PagedCellBitset();
        private readonly RoomSealingSnapshot pagedSealing = new RoomSealingSnapshot();
        private bool pagedStorage;
        internal int DenseCellLimit = 65536;

        private byte[] sealing = new byte[0];
        private readonly List<int> sealingWritten = new List<int>();
        private bool sparseSealing;
        private int previousSealingCells;
        private int sealingSizeX;
        private int sealingSizeY;
        private int sealingSizeZ;

        public bool SnapshotSealing = true;

        private bool snapshotLive;

        private readonly long[] faceDelta = new long[Face.Count];


        private readonly HashSet<long> doorCells = new HashSet<long>();

        private RoomMap working;
        private RoomMap published = RoomMap.AllExternal;

        private RoomMap supersededPrevious;

        private RoomMap spare;

        private Phase phase = Phase.Idle;
        private bool restartRequested;

        private Vector3I searchMin;
        private Vector3I searchMaxExclusive;
        private Vector3I scanCursor;

        private long scanIndex;

        private Vector3I pendingMin;
        private Vector3I pendingMaxExclusive;
        private bool hasPendingBounds;

        private GridModel pendingGrid;

        public event Action Completed;


        public SimulationWork Work = new SimulationWork();


        public RoomMapper(SurfaceMap surfaces)
        {
            if (surfaces == null) throw new ArgumentNullException("surfaces");
            this.surfaces = surfaces;
        }

        public RoomMap Map
        {
            get { return published; }
        }

        public bool IsRunning
        {
            get { return phase == Phase.External || phase == Phase.Interior; }
        }

        public bool HasWorkPending
        {
            get { return IsRunning || restartRequested; }
        }

        public int CompletedPasses { get; private set; }


        public bool Knows(BlockInstance block)
        {
            if (block == null || !block.HasStateDependentSealing) return false;
            if (CompletedPasses == 0) return false;

            IList<RoomPortal> portals = published.Portals;
            for (int i = 0; i < portals.Count; i++)
            {
                if (portals[i].Block == block) return true;
            }

            return knownDoors.Contains(block);
        }


        private readonly HashSet<BlockInstance> knownDoors = new HashSet<BlockInstance>();

        public int PendingCells
        {
            get { return frontierCount; }
        }


        public void RequestRestart(Vector3I gridMin, Vector3I gridMax)
        {
            pendingMin = gridMin - Vector3I.One;

            pendingMaxExclusive = gridMax + new Vector3I(2, 2, 2);
            hasPendingBounds = true;
            restartRequested = true;
        }


        public void RequestRestart(GridModel grid)
        {
            pendingGrid = grid;

            if (grid == null || grid.BlockCount == 0)
            {
                pendingMin = Vector3I.Zero;
                pendingMaxExclusive = Vector3I.Zero;
                hasPendingBounds = true;
                restartRequested = true;
                return;
            }
            RequestRestart(grid.Min, grid.Max);
        }


        public bool RunToCompletion(int safetyLimit = 0)
        {

            long limit = safetyLimit > 0 ? safetyLimit : SafetyLimitFromBounds();

            long spent = 0;
            while (HasWorkPending && spent < limit)
            {
                spent += 4096;
                Step(4096);
            }

            return !HasWorkPending;
        }


        private long SafetyLimitFromBounds()
        {
            Vector3I min = hasPendingBounds ? pendingMin : searchMin;
            Vector3I max = hasPendingBounds ? pendingMaxExclusive : searchMaxExclusive;

            long x = max.X - min.X;
            long y = max.Y - min.Y;
            long z = max.Z - min.Z;

            if (x <= 0 || y <= 0 || z <= 0) return 4096;

            long volume = x * y * z;
            return (volume * 4) + 4096;
        }


        public bool Step(int cellBudget)
        {
            if (cellBudget <= 0) return false;

            if (restartRequested)
            {
                BeginPass();
            }

            bool completed = false;
            int spent = 0;

            while (spent < cellBudget)
            {
                if (phase == Phase.External)
                {
                    if (frontierCount == 0)
                    {
                        phase = Phase.Interior;
                        BeginInteriorScan();
                        continue;
                    }
                    if (RunWalkLive)
                    {

                        int cells = StepExternalRun(cellBudget - spent);
                        spent += cells;
                        Work.RoomCellsVisited += cells;
                        continue;
                    }

                    StepExternal();
                    spent++;
                    Work.RoomCellsVisited++;
                }

                else if (phase == Phase.Interior)
                {
                    if (frontierCount > 0)
                    {
                        if (RunWalkLive)
                        {

                            int cells = StepInteriorRun(cellBudget - spent);
                            spent += cells;
                            Work.RoomCellsVisited += cells;
                            continue;
                        }

                        StepInterior();
                        spent++;
                        Work.RoomCellsVisited++;
                        continue;
                    }


                    ScanResult result = AdvanceScanToNextUnvisited(ref spent, cellBudget);
                    if (result == ScanResult.Exhausted)
                    {
                        Publish();
                        completed = true;
                        break;
                    }
                    if (result == ScanResult.BudgetSpent) break;
                }
                else
                {
                    break;
                }
            }

            return completed;
        }


        private void BeginPass()
        {
            Work.RoomPassesBegun++;
            restartRequested = false;

            if (hasPendingBounds)
            {
                searchMin = pendingMin;
                searchMaxExclusive = pendingMaxExclusive;
                hasPendingBounds = false;
            }

            if (spare != null)
            {
                working = spare;
                spare = null;
                working.Reset();
            }
            else
            {

                working = new RoomMap();
            }

            long volume = (long)(searchMaxExclusive.X - searchMin.X)
                * (searchMaxExclusive.Y - searchMin.Y) * (searchMaxExclusive.Z - searchMin.Z);
            pagedStorage = volume > DenseCellLimit && surfaces.CellCount <= volume / 16;
            working.SetSearchBounds(searchMin, searchMaxExclusive, pagedStorage);

            if (published != null) working.HintRoomCells(published.RoomCellCount);
            FrontierClear();
            visited.Reset(searchMin, searchMaxExclusive, pagedStorage);
            CollectDoorCells();
            TakeSealingSnapshot();

            if (searchMaxExclusive.X <= searchMin.X ||
                searchMaxExclusive.Y <= searchMin.Y ||
                searchMaxExclusive.Z <= searchMin.Z)
            {
                phase = Phase.Interior;
                BeginInteriorScan();
                return;
            }

            Vector3I seed = searchMin;

            if (!RunWalkLive)
            {
                visited.Add(seed);
                working.AddExternal(seed);
            }

            FrontierEnqueue(seed);
            phase = Phase.External;
        }

        private bool RunWalkLive
        {
            get { return SpanFlood && snapshotLive; }
        }


        private void TakeSealingSnapshot()
        {
            snapshotLive = SnapshotSealing;
            if (!snapshotLive) return;

            sealingSizeX = Math.Max(0, searchMaxExclusive.X - searchMin.X);
            sealingSizeY = Math.Max(0, searchMaxExclusive.Y - searchMin.Y);
            sealingSizeZ = Math.Max(0, searchMaxExclusive.Z - searchMin.Z);

            long cells = (long)sealingSizeX * sealingSizeY * sealingSizeZ;
            if (cells > int.MaxValue && !pagedStorage)
            {
                snapshotLive = false;
                return;
            }

            if (pagedStorage)
            {
                pagedSealing.Reset();
                surfaces.CopyStructuralSealing(searchMin, searchMaxExclusive, pagedSealing);
            }
            else
            {
                if (sealing.Length < cells) sealing = new byte[cells];
                else if (sparseSealing)
                {
                    for (int i = 0; i < sealingWritten.Count; i++) sealing[sealingWritten[i]] = 0;
                }
                else
                {
                    // Clear the previous range, not the new bounds: a later growth must not
                    // expose old entries after a dense-to-sparse shrink or an origin change.
                    Array.Clear(sealing, 0, previousSealingCells);
                }
                sealingWritten.Clear();
                previousSealingCells = (int)cells;

                // Index tracking is worthwhile for sparse extents, but costs four bytes per
                // written cell. Dense hulls retain the bulk clear and allocate no index entries.
                sparseSealing = cells >= 1024 && surfaces.CellCount <= cells / 16;
                surfaces.CopyStructuralSealing(searchMin, searchMaxExclusive, sealing,
                    sparseSealing ? sealingWritten : null);

            }

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I offset = Face.Offsets[face];
                faceDelta[face] = offset.X + ((long)offset.Y * sealingSizeX)
                    + ((long)offset.Z * sealingSizeX * sealingSizeY);
            }
        }

        private byte SealingAt(long index)
        {
            return pagedStorage ? pagedSealing.Read(index) : sealing[index];
        }

        public bool SpanFlood = true;


        private static readonly int MinusX = FaceAlong(-1, 0, 0);

        private static readonly int PlusX = FaceAlong(1, 0, 0);

        private static readonly int[] LateralFaces = BuildLateralFaces();


        private static int FaceAlong(int x, int y, int z)
        {
            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I offset = Face.Offsets[face];
                if (offset.X == x && offset.Y == y && offset.Z == z) return face;
            }
            return -1;
        }


        private static int[] BuildLateralFaces()
        {
            int[] faces = new int[Face.Count - 2];
            int at = 0;
            for (int face = 0; face < Face.Count; face++)
            {
                if (Face.Offsets[face].X != 0) continue;
                faces[at++] = face;
            }
            return faces;
        }


        private int StepExternalRun(int most)
        {

            Vector3I seed = FrontierDequeue();


            long index = SealingIndex(seed);
            if (index < 0 || visited.ContainsIndex(index)) return 1;

            if (most < 1) most = 1;
            int taken = 1;

            int low = seed.X;
            long lowIndex = index;
            while (taken < most && ReachesLow(low, lowIndex))
            {
                low--;
                lowIndex--;
                taken++;
            }

            if (taken >= most && ReachesLow(low, lowIndex))
            {
                FrontierEnqueue(new Vector3I(low - 1, seed.Y, seed.Z));
            }

            int high = seed.X;
            long highIndex = index;
            while (taken < most && ReachesHigh(high, highIndex))
            {
                high++;
                highIndex++;
                taken++;
            }

            if (taken >= most && ReachesHigh(high, highIndex))
            {
                FrontierEnqueue(new Vector3I(high + 1, seed.Y, seed.Z));
            }

            for (long i = lowIndex; i <= highIndex; i++) visited.AddIndex(i);

            int length = (int)(highIndex - lowIndex + 1);
            working.AddExternalRun(length);

            for (int f = 0; f < LateralFaces.Length; f++)
            {
                int face = LateralFaces[f];
                Vector3I offset = Face.Offsets[face];

                int y = seed.Y + offset.Y;
                if (y < searchMin.Y || y >= searchMaxExclusive.Y) continue;

                int z = seed.Z + offset.Z;
                if (z < searchMin.Z || z >= searchMaxExclusive.Z) continue;

                long delta = faceDelta[face];
                int opposite = Face.Opposite(face);
                bool inStretch = false;

                for (long i = lowIndex; i <= highIndex; i++)
                {
                    long neighbour = i + delta;

                    if ((SealingAt(i) & (1 << face)) != 0
                        || visited.ContainsIndex(neighbour)
                        || (SealingAt(neighbour) & (1 << opposite)) != 0)
                    {
                        inStretch = false;
                        continue;
                    }

                    if (inStretch) continue;

                    inStretch = true;
                    FrontierEnqueue(new Vector3I(low + (int)(i - lowIndex), y, z));
                }
            }

            return length;
        }


        private int StepInteriorRun(int most)
        {

            Vector3I seed = FrontierDequeue();


            long index = SealingIndex(seed);
            if (index < 0 || visited.ContainsIndex(index)) return 1;

            if (most < 1) most = 1;
            int taken = 1;

            int low = seed.X;
            long lowIndex = index;
            while (taken < most && ReachesLow(low, lowIndex))
            {
                taken++;
                if (TakeIfStructure(lowIndex - 1, new Vector3I(low - 1, seed.Y, seed.Z))) break;
                low--;
                lowIndex--;
            }

            if (taken >= most && ReachesLow(low, lowIndex))
            {
                FrontierEnqueue(new Vector3I(low - 1, seed.Y, seed.Z));
            }

            int high = seed.X;
            long highIndex = index;
            while (taken < most && ReachesHigh(high, highIndex))
            {
                taken++;
                if (TakeIfStructure(highIndex + 1, new Vector3I(high + 1, seed.Y, seed.Z))) break;
                high++;
                highIndex++;
            }

            if (taken >= most && ReachesHigh(high, highIndex))
            {
                FrontierEnqueue(new Vector3I(high + 1, seed.Y, seed.Z));
            }

            for (long i = lowIndex; i <= highIndex; i++)
            {
                visited.AddIndex(i);
                working.AddToRoom(currentRoom, new Vector3I(low + (int)(i - lowIndex), seed.Y, seed.Z));
            }

            for (int f = 0; f < LateralFaces.Length; f++)
            {
                int face = LateralFaces[f];
                Vector3I offset = Face.Offsets[face];

                int y = seed.Y + offset.Y;
                if (y < searchMin.Y || y >= searchMaxExclusive.Y) continue;

                int z = seed.Z + offset.Z;
                if (z < searchMin.Z || z >= searchMaxExclusive.Z) continue;

                long delta = faceDelta[face];
                int opposite = Face.Opposite(face);
                bool inStretch = false;

                for (long i = lowIndex; i <= highIndex; i++)
                {
                    long neighbour = i + delta;

                    if ((SealingAt(i) & (1 << face)) != 0
                        || visited.ContainsIndex(neighbour)
                        || (SealingAt(neighbour) & (1 << opposite)) != 0)
                    {
                        inStretch = false;
                        continue;
                    }

                    int x = low + (int)(i - lowIndex);

                    Vector3I cell = new Vector3I(x, y, z);

                    if (TakeIfStructure(neighbour, cell))
                    {
                        inStretch = false;
                        continue;
                    }

                    if (inStretch) continue;

                    inStretch = true;
                    FrontierEnqueue(cell);
                }
            }

            return taken;
        }


        private bool TakeIfStructure(long index, Vector3I cell)
        {
            if (!IsStructureAt(index, cell)) return false;

            visited.AddIndex(index);
            working.AddSolid(cell);
            return true;
        }


        private bool ReachesLow(int x, long index)
        {
            return x > searchMin.X
                && (SealingAt(index) & (1 << MinusX)) == 0
                && !visited.ContainsIndex(index - 1)
                && (SealingAt(index - 1) & (1 << PlusX)) == 0;
        }


        private bool ReachesHigh(int x, long index)
        {
            return x < searchMaxExclusive.X - 1
                && (SealingAt(index) & (1 << PlusX)) == 0
                && !visited.ContainsIndex(index + 1)
                && (SealingAt(index + 1) & (1 << MinusX)) == 0;
        }


        private bool Reaches(long index, int sealingHere, Vector3I neighbour, int face, out long neighbourIndex)
        {
            neighbourIndex = -1;

            if ((sealingHere & (1 << face)) != 0) return false;
            if (!GridMath.Contains(searchMin, searchMaxExclusive, neighbour)) return false;

            neighbourIndex = index + faceDelta[face];
            if (visited.ContainsIndex(neighbourIndex)) return false;

            return (SealingAt(neighbourIndex) & (1 << Face.Opposite(face))) == 0;
        }


        private long SealingIndex(Vector3I cell)
        {
            int x = cell.X - searchMin.X;
            if (x < 0 || x >= sealingSizeX) return -1;

            int y = cell.Y - searchMin.Y;
            if (y < 0 || y >= sealingSizeY) return -1;

            int z = cell.Z - searchMin.Z;
            if (z < 0 || z >= sealingSizeZ) return -1;

            return (((long)z * sealingSizeY) + y) * sealingSizeX + x;
        }


        private bool IsFaceSealed(Vector3I cell, int face, Vector3I neighbour)
        {
            if (!snapshotLive) return surfaces.IsFaceSealedStructurally(cell, face);


            long here = SealingIndex(cell);
            if (here >= 0 && (SealingAt(here) & (1 << face)) != 0) return true;


            long there = SealingIndex(neighbour);
            return there >= 0 && (SealingAt(there) & (1 << Face.Opposite(face))) != 0;
        }


        private bool IsFullySealed(Vector3I cell)
        {
            if (!snapshotLive) return surfaces.IsFullySealedStructurally(cell);


            long index = SealingIndex(cell);
            return index >= 0 && (SealingAt(index) & CellSurface.SelfAirtightMask) == CellSurface.SelfAirtightMask;
        }


        private void StepExternal()
        {

            Vector3I cell = FrontierDequeue();

            if (snapshotLive)
            {

                long index = SealingIndex(cell);
                int sealingHere = SealingAt(index);

                for (int face = 0; face < Face.Count; face++)
                {
                    Vector3I neighbour = cell + Face.Offsets[face];
                    long neighbourIndex;
                    if (!Reaches(index, sealingHere, neighbour, face, out neighbourIndex)) continue;

                    visited.AddIndex(neighbourIndex);
                    working.AddExternal(neighbour);
                    FrontierEnqueue(neighbour);
                }
                return;
            }

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I neighbour = cell + Face.Offsets[face];

                if (!GridMath.Contains(searchMin, searchMaxExclusive, neighbour)) continue;
                if (visited.Contains(neighbour)) continue;
                if (IsFaceSealed(cell, face, neighbour)) continue;

                visited.Add(neighbour);
                working.AddExternal(neighbour);
                FrontierEnqueue(neighbour);
            }
        }

        private int currentRoom = -1;


        private void StepInterior()
        {

            Vector3I cell = FrontierDequeue();

            if (snapshotLive)
            {

                long index = SealingIndex(cell);
                int sealingHere = SealingAt(index);

                for (int face = 0; face < Face.Count; face++)
                {
                    Vector3I neighbour = cell + Face.Offsets[face];
                    long neighbourIndex;
                    if (!Reaches(index, sealingHere, neighbour, face, out neighbourIndex)) continue;

                    visited.AddIndex(neighbourIndex);
                    if (IsStructureAt(neighbourIndex, neighbour))
                    {
                        working.AddSolid(neighbour);
                    }
                    else
                    {
                        working.AddToRoom(currentRoom, neighbour);
                        FrontierEnqueue(neighbour);
                    }
                }
                return;
            }

            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I neighbour = cell + Face.Offsets[face];

                if (!GridMath.Contains(searchMin, searchMaxExclusive, neighbour)) continue;
                if (visited.Contains(neighbour)) continue;
                if (IsFaceSealed(cell, face, neighbour)) continue;

                visited.Add(neighbour);
                if (IsStructure(neighbour))
                {
                    working.AddSolid(neighbour);
                }
                else
                {
                    working.AddToRoom(currentRoom, neighbour);
                    FrontierEnqueue(neighbour);
                }
            }
        }

        private enum ScanResult
        {
            Found,

            Exhausted,

            BudgetSpent
        }


        private ScanResult AdvanceScanToNextUnvisited(ref int spent, int budget)
        {
            while (true)
            {
                if (scanCursor.Z >= searchMaxExclusive.Z) return ScanResult.Exhausted;
                if (spent >= budget) return ScanResult.BudgetSpent;

                if (snapshotLive)
                {
                    long window = ((scanIndex >> 6) + (budget - spent)) << 6;
                    if (window > sealingCells) window = sealingCells;

                    int examined;
                    long next = visited.NextClearIndex(scanIndex, window, out examined);
                    spent += examined;
                    Work.RoomCellsVisited += examined;

                    if (next >= window && window < sealingCells)
                    {
                        MoveScanTo(next);
                        return ScanResult.BudgetSpent;
                    }

                    if (next > scanIndex) MoveScanTo(next);
                    if (scanCursor.Z >= searchMaxExclusive.Z) return ScanResult.Exhausted;
                }

                Vector3I cell = scanCursor;
                long index = scanIndex;
                AdvanceCursor();

                if (snapshotLive)
                {
                    if (IsStructureAt(index, cell))
                    {
                        visited.AddIndex(index);
                        working.AddSolid(cell);
                        continue;
                    }

                    if (!RunWalkLive) visited.AddIndex(index);
                }
                else
                {
                    spent++;
                    Work.RoomCellsVisited++;

                    if (visited.Contains(cell)) continue;
                    visited.Add(cell);

                    if (IsStructure(cell))
                    {
                        working.AddSolid(cell);
                        continue;
                    }
                }

                currentRoom = working.BeginRoom();
                if (!RunWalkLive) working.AddToRoom(currentRoom, cell);
                FrontierEnqueue(cell);
                return ScanResult.Found;
            }
        }


        private bool IsStructure(Vector3I cell)
        {
            if (!IsFullySealed(cell)) return false;
            return !IsDoorCell(cell);
        }


        private bool IsStructureAt(long index, Vector3I cell)
        {
            if (index < 0 || (SealingAt(index) & CellSurface.SelfAirtightMask) != CellSurface.SelfAirtightMask)
            {
                return false;
            }

            return !IsDoorCell(cell);
        }


        private bool IsDoorCell(Vector3I cell)
        {
            return doorCells.Count > 0 && doorCells.Contains(GridMath.Key(cell));
        }


        private void CollectDoorCells()
        {
            doorCells.Clear();
            passDoors.Clear();
            if (pendingGrid == null) return;

            IList<BlockInstance> doors = pendingGrid.StateDependentBlocks;
            for (int d = 0; d < doors.Count; d++)
            {
                passDoors.Add(doors[d]);

                Vector3I[] cells = doors[d].Cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    doorCells.Add(GridMath.Key(cells[i]));
                }
            }
        }


        private readonly HashSet<BlockInstance> passDoors = new HashSet<BlockInstance>();


        private void BeginInteriorScan()
        {
            scanCursor = searchMin;
            scanIndex = 0;
        }

        private long sealingCells
        {

            get { return (long)sealingSizeX * sealingSizeY * sealingSizeZ; }
        }


        private void MoveScanTo(long index)
        {
            scanIndex = index;
            if (index >= sealingCells)
            {

                scanCursor = new Vector3I(searchMin.X, searchMin.Y, searchMaxExclusive.Z);
                return;
            }

            long plane = (long)sealingSizeX * sealingSizeY;
            int z = (int)(index / plane);
            long rest = index - (z * plane);
            int y = (int)(rest / sealingSizeX);
            int x = (int)(rest - ((long)y * sealingSizeX));

            scanCursor = new Vector3I(searchMin.X + x, searchMin.Y + y, searchMin.Z + z);
        }


        private void AdvanceCursor()
        {
            scanIndex++;

            scanCursor.X++;
            if (scanCursor.X < searchMaxExclusive.X) return;

            scanCursor.X = searchMin.X;
            scanCursor.Y++;
            if (scanCursor.Y < searchMaxExclusive.Y) return;

            scanCursor.Y = searchMin.Y;
            scanCursor.Z++;
        }


        private void Publish()
        {
            Work.RoomPassesCompleted++;
            working.DropEmptyRooms();

            FindPortals(working);
            working.RefreshVenting();

            knownDoors.Clear();
            foreach (BlockInstance door in passDoors)
            {
                knownDoors.Add(door);
            }

            RoomMap superseded = published;
            published = working;
            working = null;

            spare = supersededPrevious;
            supersededPrevious = superseded == RoomMap.AllExternal ? null : superseded;
            phase = Phase.Done;
            CompletedPasses++;

            Action handler = Completed;
            if (handler != null) handler();
        }


        private void FindPortals(RoomMap map)
        {
            if (pendingGrid == null) return;

            IList<BlockInstance> doors = pendingGrid.StateDependentBlocks;
            for (int d = 0; d < doors.Count; d++)
            {
                BlockInstance door = doors[d];
                Vector3I[] cells = door.Cells;

                for (int face = 0; face < Face.Count; face++)
                {
                    if (!door.IsPortalFace(face)) continue;

                    Vector3I offset = Face.Offsets[face];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        Vector3I outside = cells[i] + offset;

                        if (pendingGrid.GetAtCell(outside) == door) continue;


                        int inner = RegionAt(map, cells[i]);

                        int outer = RegionAt(map, outside);
                        if (inner == outer) continue;

                        if (inner == SolidRegion || outer == SolidRegion) continue;

                        map.AddPortal(new RoomPortal(door, face, inner, outer));
                    }
                }
            }
        }


        private static int RegionAt(RoomMap map, Vector3I cell)
        {
            if (map.IsSolid(cell)) return SolidRegion;
            return map.RegionOf(cell);
        }

        private const int SolidRegion = -2;
    }
}

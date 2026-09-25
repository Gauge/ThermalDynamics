using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The room mapper reads a per-pass snapshot of the structural sealing instead of the surface
    /// map's dictionaries, and the map it publishes is held identical to the one the dictionaries
    /// produce — cell for cell, room for room, portal for portal (`D8`).
    ///
    /// <para>
    /// A map is not a temperature, so "close" has no meaning: one cell classified differently is a
    /// different room, a different exposure, a different ship. The comparison runs on a census
    /// hull with compartments, on a shell with a door in it — the case with a portal — and on the
    /// air the pressurised hull then holds, stepped both ways to the same temperatures. Every
    /// fixture asserts it found rooms, because two empty maps agree about nothing (`E8`).
    /// </para>
    /// </summary>
    public class RoomMapSnapshotTests
    {

        private static ThermalSimulation Shell(bool snapshot)
        {
            GridBuilder builder = RoomFixtures.DooredShell();
            BlockModel armour = Catalog.LightArmor();

            for (int x = 6; x <= 8; x++)
            for (int y = 0; y <= 2; y++)
            for (int z = 0; z <= 2; z++)
            {
                if (x == 7 && y == 1 && z == 1) continue;
                builder.Place(armour, new Vector3I(x, y, z));
            }


            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            simulation.Rooms.SnapshotSealing = snapshot;

            simulation.Rooms.SpanFlood = false;
            for (int i = 0; i < builder.Placed.Count; i++) simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            simulation.RebuildAll();
            return simulation;
        }


        private static ThermalSimulation Census(bool snapshot)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 8000));

            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            simulation.Rooms.SnapshotSealing = snapshot;

            simulation.Rooms.SpanFlood = false;
            for (int i = 0; i < builder.Placed.Count; i++) simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            simulation.RebuildAll();
            return simulation;
        }

        [Theory]
        [InlineData(3, 4, 5)]
        [InlineData(1, 7, 2)]
        [InlineData(9, 1, 1)]

        public void WalkingABoxInScanOrderAdvancesTheIndexByOne(int sizeX, int sizeY, int sizeZ)
        {

            Vector3I min = new Vector3I(-3, 11, -7);

            Vector3I maxExclusive = min + new Vector3I(sizeX, sizeY, sizeZ);


            CellBitset box = new CellBitset();
            box.Reset(min, maxExclusive);

            long expected = 0;
            int wraps = 0;
            for (int z = min.Z; z < maxExclusive.Z; z++)
            for (int y = min.Y; y < maxExclusive.Y; y++)
            for (int x = min.X; x < maxExclusive.X; x++)
            {

                Vector3I cell = new Vector3I(x, y, z);
                Assert.True(expected == box.IndexOf(cell),
                    cell + " is index " + box.IndexOf(cell) + " and the scan is at " + expected);
                if (x == min.X && expected > 0) wraps++;
                expected++;
            }

            Assert.Equal((long)sizeX * sizeY * sizeZ, expected);
            Assert.True(sizeY * sizeZ == 1 || wraps > 0, "the box has no wrap in it, so the wrapping case is untested");
        }

        [Fact]

        public void TheSnapshotScanChargesFarFewerUnitsThanTheCellByCellScan()
        {

            ThermalSimulation byDictionary = Census(false);

            ThermalSimulation bySnapshot = Census(true);

            long walked = byDictionary.Work.RoomCellsVisited;
            long skipped = bySnapshot.Work.RoomCellsVisited;

            Vector3I extents = (byDictionary.Grid.Max - byDictionary.Grid.Min) + new Vector3I(3, 3, 3);
            long volume = (long)extents.X * extents.Y * extents.Z;

            Assert.True(walked > 0 && skipped > 0, "a pass charged nothing");
            Assert.True(walked - skipped > volume / 2,
                "the snapshot scan charged " + skipped + " units against " + walked
                + " cell by cell over a box of " + volume + " cells, so it skipped less than half the box");
        }

        [Fact]

        public void NextClearIndexFindsTheFirstClearBitAtOrAfterAnyOffset()
        {

            CellBitset bits = new CellBitset();
            Vector3I min = Vector3I.Zero;
            bits.Reset(min, new Vector3I(200, 1, 1));

            for (int i = 0; i < 150; i++)
            {
                if (i == 37 || i == 129) continue;
                bits.AddIndex(i);
            }

            int examined;
            Assert.Equal(37L, bits.NextClearIndex(0, 200, out examined));
            Assert.Equal(1, examined);
            Assert.Equal(37L, bits.NextClearIndex(37, 200, out examined));
            Assert.Equal(129L, bits.NextClearIndex(38, 200, out examined));
            Assert.Equal(3, examined);
            Assert.Equal(150L, bits.NextClearIndex(130, 200, out examined));
            Assert.Equal(151L, bits.NextClearIndex(151, 200, out examined));
            Assert.Equal(150L, bits.NextClearIndex(150, 150, out examined));

            for (int i = 150; i < 200; i++) bits.AddIndex(i);
            Assert.Equal(200L, bits.NextClearIndex(130, 200, out examined));
            Assert.Equal(37L, bits.NextClearIndex(0, 200, out examined));
        }

        [Fact]

        public void APassIsUnaffectedByWhatTheLastOneLeftInTheFrontier()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            ThermalSimulation simulation = builder.BuildSimulation(Hulls.Uncapped(), 293.15f);

            RoomMap first = simulation.Rooms.Map;
            Assert.True(first.RoomCount > 0, "the hull mapped no rooms");
            Assert.Equal(0, simulation.Rooms.PendingCells);


            Vector3I pad = new Vector3I(40, 40, 40);
            simulation.Rooms.RequestRestart(simulation.Grid.Min - pad, simulation.Grid.Max + pad);
            Assert.True(simulation.Rooms.RunToCompletion());
            Assert.True(simulation.Rooms.Map.ExternalCellCount > first.ExternalCellCount * 4,
                "the wide pass flooded " + simulation.Rooms.Map.ExternalCellCount
                + " external cells against the ordinary pass's " + first.ExternalCellCount
                + ", so the frontier was never grown");

            simulation.Rooms.RequestRestart(simulation.Grid);
            Assert.True(simulation.Rooms.RunToCompletion());

            RoomMapAssert.SameMap(first, simulation.Rooms.Map, "after a wider pass");
        }

        [Fact]

        public void AShellWithADoorMapsToTheSameRoomsAndPortals()
        {

            ThermalSimulation byDictionary = Shell(false);

            ThermalSimulation bySnapshot = Shell(true);

            Assert.True(byDictionary.Rooms.Map.Portals.Count > 0, "the shell's door made no portal, so the portal half is untested");
            RoomMapAssert.SameMap(byDictionary.Rooms.Map, bySnapshot.Rooms.Map, "shell");
        }

        [Fact]

        public void ACensusHullMapsToTheSameCompartments()
        {

            ThermalSimulation byDictionary = Census(false);

            ThermalSimulation bySnapshot = Census(true);

            RoomMapAssert.SameMap(byDictionary.Rooms.Map, bySnapshot.Rooms.Map, "census hull");
        }

        [Fact]

        public void APressurisedHullStepsToTheSameTemperaturesEitherWay()
        {

            ThermalSimulation byDictionary = Census(false);

            ThermalSimulation bySnapshot = Census(true);

            int pressurised = 0;
            foreach (ThermalSimulation simulation in new[] { byDictionary, bySnapshot })
            {
                RoomMap rooms = simulation.Rooms.Map;
                for (int r = 0; r < rooms.RoomCount; r++)
                {
                    if (simulation.SetRoomPressure(rooms.CellsOf(r)[0], 1f)) pressurised++;
                }
                LoadBenchmarks.SeedSpread(simulation);
                Thermodynamics.Harness.Census.DriveCensus(simulation);
            }
            Assert.True(pressurised > 0, "no room took air, so the air half is untested");

            EnvironmentSample sample = Worlds.Ab.MildAtmosphere();
            byDictionary.StepExact(20, sample);
            bySnapshot.StepExact(20, sample);

            SolverAb.AssertIdentical(
                SolverAb.Temperatures(byDictionary), SolverAb.Temperatures(bySnapshot),
                "pressurised census hull", "by dictionary", "by snapshot");
        }
    }
}

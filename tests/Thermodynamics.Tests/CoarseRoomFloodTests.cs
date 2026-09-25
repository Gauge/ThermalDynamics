using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The supercell flood finds the shipped mapper's partition exactly — external count, room
    /// count, and every room's cell set under a bijection of ids — on the grids whose shapes
    /// exercise its seams: rooms smaller than a supercell, rooms spanning many, structure on a
    /// supercell boundary, a closed door's roomable cell, a partially sealed cell's pocket, and a
    /// dealt hull. A speedup measured on any other partition would be a speedup at finding the
    /// wrong rooms, so this suite is what lets `bench coarserooms` report milliseconds at all.
    /// </summary>
    public class CoarseRoomFloodTests
    {

        private static ThermalSimulation Prepared(GridBuilder builder)
        {

            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            simulation.Surfaces.Rebuild(simulation.Grid);
            simulation.Rooms.RequestRestart(simulation.Grid);
            Assert.True(simulation.Rooms.RunToCompletion(), "the reference pass did not finish");
            return simulation;
        }


        private static void AssertMatches(GridBuilder builder, int edge, string what)
        {

            ThermalSimulation simulation = Prepared(builder);
            RoomMap oracle = simulation.Rooms.Map;
            Assert.True(oracle.RoomCount > 0,
                what + ": the mapper found no rooms, so agreement proves nothing");


            CoarseRoomFlood flood = new CoarseRoomFlood(edge);
            flood.Run(simulation.Grid, simulation.Surfaces);

            string mismatch = CoarseRoomLab.Verify(oracle, flood);
            Assert.True(mismatch == null, what + " at edge " + edge + ": " + mismatch);
        }

        [Fact]

        public void OneRoomShellMatchesAtEveryEdge()
        {
            foreach (int edge in new[] { 2, 3, 4, 7 })
            {
                GridBuilder builder = GridBuilder.Large();
                builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(5, 5, 5));
                AssertMatches(builder, edge, "shell");
            }
        }

        [Fact]

        public void AWallInsideOneSupercellStillSplitsTheRooms()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();
            builder.Shell(armour, new Vector3I(0, 0, 0), new Vector3I(9, 4, 4));
            for (int y = 1; y < 3; y++)
            for (int z = 1; z < 3; z++)
            {
                builder.Place(armour, new Vector3I(4, y, z));
            }
            AssertMatches(builder, 4, "divided shell");
        }

        [Fact]

        public void AnOpenFrameCellJoinsItsRoom()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(5, 5, 5));
            builder.Place(BlockModel.Open("Frame", Vector3I.One, 100f, null), new Vector3I(2, 2, 2));
            AssertMatches(builder, 3, "framed pocket");
        }

        [Fact]

        public void AClosedDoorCellIsARoomNotStructure()
        {
            GridBuilder builder = RoomFixtures.DooredShell();

            AssertMatches(builder, 3, "shell with a door");
        }

        [Fact]

        public void ACensusHullMatchesTheMapper()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 8000));
            AssertMatches(builder, 4, "census hull");
        }

        [Fact]

        public void ARefinedHullMatchesTheMapperAtTheLatticeFactor()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            GridBuilder fine = Se2Refine.Refined(builder, 3);
            AssertMatches(fine, 3, "refined census hull");
        }
    }
}

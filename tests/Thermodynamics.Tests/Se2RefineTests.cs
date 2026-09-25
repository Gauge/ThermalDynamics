using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Refining the lattice moves no block and changes no topology: the same nodes, the same
    /// links, the same rooms — only the cells under them multiply. These are the invariants that
    /// make `bench se2tax` a measurement of the lattice rather than of an accidentally different
    /// ship.
    /// </summary>
    public class Se2RefineTests
    {

        private static ThermalSimulation Prepared(GridBuilder builder)
        {

            ThermalSimulation simulation = new ThermalSimulation(Hulls.Uncapped(), builder.Grid);
            for (int i = 0; i < builder.Placed.Count; i++)
            {
                simulation.Solver.AddBlock(builder.Placed[i], 293.15f);
            }
            simulation.Surfaces.Rebuild(simulation.Grid);
            simulation.Solver.RebuildLinks();
            simulation.Rooms.RequestRestart(simulation.Grid);
            Assert.True(simulation.Rooms.RunToCompletion(), "the room pass did not finish");
            return simulation;
        }

        [Fact]

        public void NodesAndLinksHoldWhileCellsCube()
        {
            GridBuilder source = GridBuilder.Large();
            source.PlaceCensus(LoadShapes.Build("ship", 2000));

            ThermalSimulation coarse = Prepared(source);

            const int factor = 3;
            GridBuilder refined = Se2Refine.Refined(source, factor);

            ThermalSimulation fine = Prepared(refined);

            Assert.Equal(coarse.Solver.Nodes.Count, fine.Solver.Nodes.Count);
            Assert.Equal(coarse.Solver.LinkCount, fine.Solver.LinkCount);

            long coarseCells = 0;
            for (int i = 0; i < source.Placed.Count; i++) coarseCells += source.Placed[i].CellCount;
            long fineCells = 0;
            for (int i = 0; i < refined.Placed.Count; i++) fineCells += refined.Placed[i].CellCount;
            Assert.Equal(coarseCells * factor * factor * factor, fineCells);
        }

        [Fact]

        public void RoomsSurviveRefinementWithTheirVolumesCubed()
        {
            GridBuilder source = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();
            source.Shell(armour, new Vector3I(0, 0, 0), new Vector3I(9, 4, 4));
            for (int y = 1; y < 3; y++)
            for (int z = 1; z < 3; z++)
            {
                source.Place(armour, new Vector3I(4, y, z));
            }


            ThermalSimulation coarse = Prepared(source);
            Assert.True(coarse.Rooms.Map.RoomCount > 0, "no rooms, so invariance proves nothing");

            const int factor = 3;
            GridBuilder refined = Se2Refine.Refined(source, factor);

            ThermalSimulation fine = Prepared(refined);

            RoomMap before = coarse.Rooms.Map;
            RoomMap after = fine.Rooms.Map;
            Assert.Equal(before.RoomCount, after.RoomCount);


            List<int> coarseSizes = new List<int>();

            List<int> fineSizes = new List<int>();
            for (int r = 0; r < before.RoomCount; r++) coarseSizes.Add(before.CellsInRoom(r) * factor * factor * factor);
            for (int r = 0; r < after.RoomCount; r++) fineSizes.Add(after.CellsInRoom(r));
            coarseSizes.Sort();
            fineSizes.Sort();
            Assert.Equal(coarseSizes, fineSizes);
        }

        [Fact]

        public void PartialSealingRefinesWithoutPhantomPockets()
        {
            GridBuilder source = GridBuilder.Large();
            BlockModel armour = Catalog.LightArmor();
            source.Shell(armour, new Vector3I(-1, -1, -1), new Vector3I(5, 5, 5));

            BlockModel vented = BlockModel.Solid("Vented", Vector3I.One, 100f, null);
            int open = CellSurface.WithSelfAirtight(
                CellSurface.SelfAirtightMask | CellSurface.SelfMountMask, 0, false);
            vented.SetLocalSurface(Vector3I.Zero, open);
            source.Place(vented, new Vector3I(2, 2, 2));


            ThermalSimulation coarse = Prepared(source);

            const int factor = 3;
            GridBuilder refined = Se2Refine.Refined(source, factor);

            ThermalSimulation fine = Prepared(refined);

            Assert.Equal(coarse.Rooms.Map.RoomCount, fine.Rooms.Map.RoomCount);
        }
    }
}

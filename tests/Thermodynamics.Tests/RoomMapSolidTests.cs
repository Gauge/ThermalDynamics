using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The published map's solid set is a bitset over the search box, and it is checked against
    /// something that is not the map (`E7`): a cell is solid exactly when the surface map says it
    /// seals structurally on all six faces and it belongs to no door — which is the mapper's own
    /// definition, applied here cell by cell over the whole box rather than trusted through the map.
    ///
    /// <para>
    /// Every cell of the box is asked, inside and out, so a bit stored at the wrong index, a box
    /// sized a cell short, or a set that was never reset between passes would each show up as a
    /// cell answering differently from its definition. Each fixture asserts it found structure,
    /// since an empty set agrees with anything (`E8`).
    /// </para>
    /// </summary>
    public class RoomMapSolidTests
    {

        private static ThermalSimulation Build(string which)
        {
            GridBuilder builder = GridBuilder.Large();
            if (which == "census")
            {
                builder.PlaceCensus(LoadShapes.Build("ship", 4000));
            }
            else
            {
                RoomFixtures.AddDooredShell(builder);
            }
            return builder.BuildSimulation(Hulls.Uncapped(), 293.15f);
        }

        [Theory]
        [InlineData("shell")]
        [InlineData("census")]

        public void ACellIsSolidExactlyWhenTheSurfaceMapSealsItOnEveryFaceAndItIsNoDoor(string which)
        {

            ThermalSimulation simulation = Build(which);
            RoomMap map = simulation.Rooms.Map;
            SurfaceMap surfaces = simulation.Surfaces;


            HashSet<Vector3I> doorCells = new HashSet<Vector3I>(Vector3I.Comparer);
            foreach (BlockInstance door in simulation.Grid.StateDependentBlocks)
            {
                foreach (Vector3I cell in door.Cells) doorCells.Add(cell);
            }


            Vector3I min = simulation.Grid.Min - new Vector3I(2, 2, 2);

            Vector3I max = simulation.Grid.Max + new Vector3I(2, 2, 2);

            int solid = 0;
            int asked = 0;
            for (int x = min.X; x <= max.X; x++)
            for (int y = min.Y; y <= max.Y; y++)
            for (int z = min.Z; z <= max.Z; z++)
            {

                Vector3I cell = new Vector3I(x, y, z);
                bool expected = surfaces.IsFullySealedStructurally(cell) && !doorCells.Contains(cell);
                Assert.True(expected == map.IsSolid(cell),
                    which + ": " + cell + " is " + (map.IsSolid(cell) ? "solid" : "not solid")
                    + " in the map and " + (expected ? "sealed on every face" : "not") + " in the surface map");
                asked++;
                if (expected) solid++;
            }

            Assert.True(solid > 0, which + ": no cell is structure, so the set is untested");
            Assert.True(asked > solid, which + ": every cell asked was structure, so the outside is untested");
            Assert.Equal(solid, map.SolidCellCount);
        }

        [Fact]

        public void ASecondPassStartsFromAnEmptySet()
        {

            ThermalSimulation simulation = Build("shell");
            int before = simulation.Rooms.Map.SolidCellCount;

            BlockInstance last = simulation.Grid.Blocks[0];
            simulation.RemoveBlock(last);
            simulation.RebuildAll();

            Assert.True(before > 0);
            Assert.Equal(before - 1, simulation.Rooms.Map.SolidCellCount);
            Assert.False(simulation.Rooms.Map.IsSolid(last.Cells[0]));
        }
    }
}

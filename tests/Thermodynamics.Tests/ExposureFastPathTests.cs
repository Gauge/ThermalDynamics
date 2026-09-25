using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// `SurfaceMap.GetExposedFaces` answers a one-cell block from one cell state and six face
    /// tests, and is held to the boundary-walking count it short-cuts, face for face, over every
    /// block of a census hull with its rooms mapped and of a grid mixing unit blocks with bars and a
    /// cube (`D8`). Exposure is what every radiating and convecting term multiplies by, so a face
    /// counted differently is a different temperature on every step after.
    /// </summary>
    public class ExposureFastPathTests
    {

        private static void AssertSame(ThermalSimulation simulation, string what)
        {
            SurfaceMap surfaces = simulation.Surfaces;
            RoomMap rooms = simulation.Rooms.Map;
            int[] fast = new int[Face.Count];
            int[] walked = new int[Face.Count];
            int exposedFaces = 0;
            int oneCell = 0;

            for (int b = 0; b < simulation.Grid.Blocks.Count; b++)
            {
                BlockInstance block = simulation.Grid.Blocks[b];
                surfaces.GetExposedFaces(block, rooms, fast);
                System.Array.Clear(walked, 0, Face.Count);
                surfaces.GetExposedFacesWalkingTheBoundary(block, rooms, walked);

                for (int f = 0; f < Face.Count; f++)
                {
                    Assert.True(fast[f] == walked[f],
                        what + ": " + block + " face " + Face.Name(f) + " counts " + fast[f]
                        + " by the short path and " + walked[f] + " by the walk");
                    exposedFaces += fast[f];
                }
                if (block.CellCount == 1) oneCell++;
            }

            Assert.True(exposedFaces > 0, what + ": no face is exposed, so nothing was compared");
            Assert.True(oneCell > 0, what + ": no one-cell block, so the short path never ran");
            Assert.True(rooms.RoomCount > 0 || what != "census hull", what + ": no rooms, so the interior half of the question was never asked");
        }

        [Fact]

        public void ACensusHullCountsTheSameExposedFacesByBothPaths()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 2000));
            AssertSame(builder.BuildSimulation(Hulls.Uncapped(), 293.15f), "census hull");
        }

        [Fact]

        public void AMixedGridCountsTheSameExposedFacesByBothPaths()
        {
            GridBuilder builder = GridBuilder.Large();
            BlockModel unit = Catalog.LightArmor();
            builder.Place(Catalog.LightArmorCube(3), new Vector3I(0, 0, 0));
            builder.Place(Catalog.LightArmorBar(3), new Vector3I(3, 0, 0));
            for (int x = -1; x <= 6; x++)
            for (int z = -1; z <= 3; z++)
            {
                if (builder.Grid.IsOccupied(new Vector3I(x, 3, z))) continue;
                builder.Place(unit, new Vector3I(x, 3, z));
            }
            for (int y = 0; y < 3; y++) builder.Place(unit, new Vector3I(-1, y, 1));
            AssertSame(builder.BuildSimulation(Hulls.Uncapped(), 293.15f), "mixed grid");
        }
    }
}

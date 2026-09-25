using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The room mapper is well covered for grids built in one shot through
    /// <see cref="ThermalSimulation.RebuildAll"/>. A player welding a room is a different path
    /// entirely: one <see cref="ThermalSimulation.AddBlock"/> per block, with
    /// <see cref="ThermalSimulation.Update"/> pumping the budgeted mapper in between. These
    /// tests walk that path.
    /// </summary>
    public class IncrementalRoomTests
    {

        private static List<Vector3I> ShellCells(Vector3I min, Vector3I maxExclusive)
        {

            List<Vector3I> cells = new List<Vector3I>();
            for (int z = min.Z; z < maxExclusive.Z; z++)
            {
                for (int y = min.Y; y < maxExclusive.Y; y++)
                {
                    for (int x = min.X; x < maxExclusive.X; x++)
                    {
                        bool onSurface =
                            x == min.X || x == maxExclusive.X - 1 ||
                            y == min.Y || y == maxExclusive.Y - 1 ||
                            z == min.Z || z == maxExclusive.Z - 1;
                        if (onSurface) cells.Add(new Vector3I(x, y, z));
                    }
                }
            }
            return cells;
        }


        private static void SettleMapper(ThermalSimulation simulation, int maxFrames = 600)
        {
            for (int i = 0; i < maxFrames && (simulation.Rooms.HasWorkPending || i == 0); i++)
            {
                simulation.Update(1f / 60f, Worlds.Shadow());
            }
        }

        [Fact]

        public void WeldingAShellOneBlockAtATimeStillFindsTheRoom()
        {

            GridModel grid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings(), grid);

            BlockModel armour = Catalog.LightArmor();

            List<Vector3I> cells = ShellCells(new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            for (int i = 0; i < cells.Count; i++)
            {
                simulation.AddBlock(new BlockInstance(armour, cells[i], BlockOrientation.Identity));

                simulation.Update(ThermalGridTick, Worlds.Shadow());
            }

            SettleMapper(simulation);

            Assert.Equal(1, simulation.Rooms.Map.RoomCount);
            Assert.False(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
        }

        private const float ThermalGridTick = 10f / 60f;

        [Fact]

        public void EveryBlockCellIsSolidNotExternalAfterAnIncrementalBuild()
        {

            GridModel grid = new GridModel(Catalog.LargeGridSize);

            ThermalSimulation simulation = new ThermalSimulation(new ThermalSettings(), grid);

            BlockModel armour = Catalog.LightArmor();

            List<Vector3I> cells = ShellCells(new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            for (int i = 0; i < cells.Count; i++)
            {
                simulation.AddBlock(new BlockInstance(armour, cells[i], BlockOrientation.Identity));
                simulation.Update(ThermalGridTick, Worlds.Shadow());
            }
            SettleMapper(simulation);

            RoomMap map = simulation.Rooms.Map;
            for (int i = 0; i < cells.Count; i++)
            {
                Assert.True(map.IsSolid(cells[i]), "cell " + cells[i] + " should be structure, not open space");
            }
        }

        [Fact]

        public void AClosedDoorInTheWallKeepsTheRoomSealedAndAnOpenOneDoesNot()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            builder.Grid.Remove(plug);
            builder.Place(Catalog.SlideDoor(), new Vector3I(0, 0, -1));
            BlockInstance door = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            Assert.Equal(1, simulation.Rooms.Map.RoomCount);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.True(simulation.Rooms.Map.IsVented(0));
            Assert.True(simulation.Rooms.Map.IsExternal(Vector3I.Zero));

            door.IsSealedByDoorState = true;
            simulation.RefreshBlockSealing(door);

            Assert.Equal(1, simulation.Rooms.Map.RoomCount);
            Assert.False(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
        }

        [Fact]

        public void TheClassificationOfEveryCellInTheSearchBoxAddsUp()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            RoomMap map = simulation.Rooms.Map;

            Assert.Equal(98, map.ExternalCellCount);
            Assert.Equal(26, map.SolidCellCount);
            Assert.Equal(1, map.RoomCount);
            Assert.Equal(1, map.CellsInRoom(0));
        }
    }
}

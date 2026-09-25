using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A door seals differently in each of its two states, and not by the same rule on every
    /// face. The sides it is bolted in by hold whatever it is doing; only the way through opens.
    /// One flag cannot express that, which is what these pin down.
    /// </summary>
    public class DoorSealingTests
    {

        private static BlockInstance Door(bool sealed_)
        {

            BlockInstance door = new BlockInstance(Catalog.SlideDoor(), Vector3I.Zero, BlockOrientation.Identity);
            door.IsSealedByDoorState = sealed_;
            door.RefreshSurfaces();
            return door;
        }

        [Fact]

        public void AClosedDoorSealsTheWayThroughAndAnOpenOneDoesNot()
        {
            Assert.Equal(1f, Door(true).SealFraction(Face.Forward));
            Assert.Equal(0f, Door(false).SealFraction(Face.Forward));
        }

        [Fact]

        public void OpeningADoorLeavesTheSidesItIsBoltedInBySealed()
        {

            BlockInstance open = Door(false);

            Assert.Equal(1f, open.SealFraction(Face.Up));
            Assert.Equal(1f, open.SealFraction(Face.Down));
            Assert.Equal(1f, open.SealFraction(Face.Left));
            Assert.Equal(1f, open.SealFraction(Face.Right));
        }

        [Fact]

        public void TheSurfaceBitsAgreeWithTheFaceFractions()
        {

            BlockInstance open = Door(false);
            int state = open.SelfSurfaces[0];

            Assert.False(CellSurface.SelfAirtight(state, Face.Forward));
            Assert.True(CellSurface.SelfAirtight(state, Face.Up));
            Assert.True(CellSurface.SelfMount(state, Face.Forward));
        }

        [Fact]

        public void ABlockWithNoOpenStateIsNotADoorAndIgnoresTheFlag()
        {

            BlockInstance block = new BlockInstance(Catalog.LightArmor(), Vector3I.Zero, BlockOrientation.Identity);

            Assert.False(block.HasStateDependentSealing);

            block.IsSealedByDoorState = false;
            block.RefreshSurfaces();

            for (int face = 0; face < Face.Count; face++)
            {
                Assert.Equal(1f, block.SealFraction(face));
                Assert.True(CellSurface.SelfAirtight(block.SelfSurfaces[0], face));
                Assert.False(block.IsPortalFace(face));
            }
        }

        [Fact]

        public void TheDoorCellJoinsTheRoomAndIsNotReportedAsALeak()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            builder.Grid.Remove(plug);
            builder.Place(Catalog.SlideDoor(), new Vector3I(0, 0, -1));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            RoomMap map = simulation.Rooms.Map;

            Assert.Equal(1, map.RoomCount);
            Assert.Equal(0, map.RoomIndexOf(Vector3I.Zero));
            Assert.Equal(0, map.RoomIndexOf(new Vector3I(0, 0, -1)));

            RoomAudit audit = simulation.AuditRooms();
            Assert.False(audit.HasLeak);
            Assert.Equal(0, audit.OpenBlockCells);
            Assert.Equal(25, audit.SolidCells);
            Assert.Equal(2, audit.RoomCells);
        }

        [Fact]

        public void OpeningTheDoorPutsTheRoomOutdoorsAndClosingItRestoresTheRoom()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            builder.Grid.Remove(plug);
            builder.Place(Catalog.SlideDoor(), new Vector3I(0, 0, -1));
            BlockInstance door = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            int room = simulation.Rooms.Map.RoomIndexOf(Vector3I.Zero);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.Equal(1, simulation.Rooms.Map.RoomCount);
            Assert.Equal(room, simulation.Rooms.Map.RoomIndexOf(Vector3I.Zero));
            Assert.True(simulation.Rooms.Map.IsVented(room));
            Assert.True(simulation.Rooms.Map.IsExternal(Vector3I.Zero));

            door.IsSealedByDoorState = true;
            simulation.RefreshBlockSealing(door);

            Assert.Equal(1, simulation.Rooms.Map.RoomCount);
            Assert.False(simulation.Rooms.Map.IsVented(room));
            Assert.False(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
        }
    }
}

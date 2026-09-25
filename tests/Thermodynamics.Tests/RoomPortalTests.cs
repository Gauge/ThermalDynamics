using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Rooms are built from structure, and doors are portals through it. A door cycling changes
    /// which rooms reach open air; it does not change where the rooms are.
    ///
    /// The property that has to hold above all others is that the consumer cannot tell the
    /// difference: <see cref="SurfaceMap.GetExposedFaces"/> asks one question per face — is the
    /// cell beyond it outdoors — and it must get the same answer it would from a map rebuilt
    /// from scratch with the doors in their current state.
    /// </summary>
    public class RoomPortalTests
    {


        private static ThermalSimulation ShellWithDoor(BlockModel doorModel, out BlockInstance door)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            builder.Grid.Remove(plug);
            builder.Placed.Remove(plug);
            builder.Place(doorModel, new Vector3I(0, 0, -1));
            door = builder.Last;

            return builder.BuildSimulation(new ThermalSettings());
        }


        private static Dictionary<Vector3I, bool> ExternalMap(ThermalSimulation simulation, int reach = 4)
        {
            Dictionary<Vector3I, bool> result = new Dictionary<Vector3I, bool>(Vector3I.Comparer);
            RoomMap map = simulation.Rooms.Map;

            for (int x = -reach; x <= reach; x++)
            {
                for (int y = -reach; y <= reach; y++)
                {
                    for (int z = -reach; z <= reach; z++)
                    {

                        Vector3I cell = new Vector3I(x, y, z);
                        result[cell] = map.IsExternal(cell);
                    }
                }
            }
            return result;
        }


        private static Dictionary<Vector3I, int> Exposure(ThermalSimulation simulation)
        {
            Dictionary<Vector3I, int> result = new Dictionary<Vector3I, int>(Vector3I.Comparer);
            IList<BlockInstance> blocks = simulation.Grid.Blocks;

            for (int i = 0; i < blocks.Count; i++)
            {
                int[] faces = simulation.Surfaces.GetExposedFaces(blocks[i], simulation.Rooms.Map);
                int total = 0;
                for (int f = 0; f < faces.Length; f++) total += faces[f];
                result[blocks[i].Position] = total;
            }
            return result;
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]

        public void PortalsAgreeWithAFullRemapForEitherDoorState(bool open)
        {
            BlockInstance viaPortal;

            ThermalSimulation portalSim = ShellWithDoor(Catalog.SlideDoor(), out viaPortal);

            viaPortal.IsSealedByDoorState = !open;
            portalSim.RefreshBlockSealing(viaPortal);

            BlockInstance viaRemap;

            ThermalSimulation remapSim = ShellWithDoor(Catalog.SlideDoor(), out viaRemap);
            viaRemap.IsSealedByDoorState = !open;
            viaRemap.RefreshSurfaces();
            remapSim.RebuildAll();

            Assert.Equal(ExternalMap(remapSim), ExternalMap(portalSim));
            Assert.Equal(Exposure(remapSim), Exposure(portalSim));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]

        public void TheSameHoldsForADoorThatSealsOnEveryFace(bool open)
        {
            BlockInstance viaPortal;

            ThermalSimulation portalSim = ShellWithDoor(Catalog.AirtightDoor(), out viaPortal);

            viaPortal.IsSealedByDoorState = !open;
            portalSim.RefreshBlockSealing(viaPortal);

            BlockInstance viaRemap;

            ThermalSimulation remapSim = ShellWithDoor(Catalog.AirtightDoor(), out viaRemap);
            viaRemap.IsSealedByDoorState = !open;
            viaRemap.RefreshSurfaces();
            remapSim.RebuildAll();

            Assert.Equal(ExternalMap(remapSim), ExternalMap(portalSim));
            Assert.Equal(Exposure(remapSim), Exposure(portalSim));
        }


        [Fact]

        public void CyclingADoorCostsNoMappingPass()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);

            int passes = simulation.Rooms.CompletedPasses;

            for (int i = 0; i < 50; i++)
            {
                door.IsSealedByDoorState = (i % 2 == 0);
                simulation.RefreshBlockSealing(door);
                Assert.False(simulation.Rooms.HasWorkPending, "a door cycle must not request a remap");
            }

            Assert.Equal(passes, simulation.Rooms.CompletedPasses);
        }

        [Fact]

        public void CyclingADoorManyTimesDoesNotDrift()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);


            Dictionary<Vector3I, bool> shut = ExternalMap(simulation);

            Dictionary<Vector3I, int> shutExposure = Exposure(simulation);

            for (int i = 0; i < 50; i++)
            {
                door.IsSealedByDoorState = false;
                simulation.RefreshBlockSealing(door);
                door.IsSealedByDoorState = true;
                simulation.RefreshBlockSealing(door);
            }

            Assert.Equal(shut, ExternalMap(simulation));
            Assert.Equal(shutExposure, Exposure(simulation));
        }


        [Fact]

        public void AWallDoorIsFoundAsOnePortalOntoOpenAir()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);
            RoomMap map = simulation.Rooms.Map;

            Assert.Single(map.Portals);

            RoomPortal portal = map.Portals[0];
            Assert.Same(door, portal.Block);
            Assert.True(portal.JoinsDistinctRegions);
            Assert.False(portal.IsOpen);

            Assert.True(portal.RegionA == RoomMap.ExternalRegion || portal.RegionB == RoomMap.ExternalRegion);
        }

        [Fact]

        public void ADoorSealedOnEveryFaceIsARegionRatherThanStructure()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.AirtightDoor(), out door);
            RoomMap map = simulation.Rooms.Map;


            Vector3I cell = new Vector3I(0, 0, -1);
            Assert.False(map.IsSolid(cell));
            Assert.True(map.RoomIndexOf(cell) >= 0);

            Assert.False(map.IsExternal(Vector3I.Zero));

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.True(map.IsExternal(Vector3I.Zero));
            Assert.True(map.IsExternal(cell));
        }


        [Fact]

        public void VentingIsTransitiveThroughAChainOfDoors()
        {
            GridBuilder builder = GridBuilder.Large();

            builder.Shell(Catalog.LightArmor(), new Vector3I(-2, -1, -1), new Vector3I(3, 2, 2));
            builder.Place(Catalog.LightArmor(), new Vector3I(0, 0, 0));

            BlockInstance outerPlug = builder.Grid.GetAtCell(new Vector3I(-2, 0, 0));
            builder.Grid.Remove(outerPlug);
            builder.Placed.Remove(outerPlug);
            builder.Place(Catalog.AirtightDoor(), new Vector3I(-2, 0, 0));
            BlockInstance outer = builder.Last;

            BlockInstance dividerPlug = builder.Grid.GetAtCell(new Vector3I(0, 0, 0));
            builder.Grid.Remove(dividerPlug);
            builder.Placed.Remove(dividerPlug);
            builder.Place(Catalog.AirtightDoor(), new Vector3I(0, 0, 0));
            BlockInstance inner = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            RoomMap map = simulation.Rooms.Map;


            Vector3I westChamber = new Vector3I(-1, 0, 0);

            Vector3I eastChamber = new Vector3I(1, 0, 0);

            Assert.False(map.IsExternal(westChamber));
            Assert.False(map.IsExternal(eastChamber));

            inner.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(inner);

            Assert.False(map.IsExternal(westChamber));
            Assert.False(map.IsExternal(eastChamber));

            outer.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(outer);

            Assert.True(map.IsExternal(westChamber));
            Assert.True(map.IsExternal(eastChamber));

            inner.IsSealedByDoorState = true;
            simulation.RefreshBlockSealing(inner);

            Assert.True(map.IsExternal(westChamber));
            Assert.False(map.IsExternal(eastChamber));
        }

        [Fact]

        public void ADoorBetweenTwoSealedRoomsVentsNeither()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-2, -1, -1), new Vector3I(3, 2, 2));
            builder.Place(Catalog.AirtightDoor(), new Vector3I(0, 0, 0));
            BlockInstance door = builder.Last;

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            RoomMap map = simulation.Rooms.Map;

            Assert.Equal(3, map.RoomCount);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.False(map.IsExternal(new Vector3I(-1, 0, 0)));
            Assert.False(map.IsExternal(new Vector3I(1, 0, 0)));
            Assert.Equal(0, map.RoomCount - map.AirtightRoomCount);
        }


        [Fact]

        public void ADoorTheMapHasNotSeenForcesARemap()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            BlockInstance plug = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            simulation.RemoveBlock(plug);


            BlockInstance door = new BlockInstance(Catalog.SlideDoor(), new Vector3I(0, 0, -1), BlockOrientation.Identity);
            simulation.AddBlock(door);

            Assert.False(simulation.Rooms.Knows(door));

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.True(simulation.Rooms.HasWorkPending);

            simulation.Rooms.RunToCompletion();
            Assert.True(simulation.Rooms.Knows(door));
            Assert.True(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
        }

        [Fact]

        public void RemovingADoorDropsItsPortalOnTheNextPass()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);

            Assert.Single(simulation.Rooms.Map.Portals);
            Assert.Single(simulation.Grid.StateDependentBlocks);

            simulation.RemoveBlock(door);
            simulation.RebuildAll();

            Assert.Empty(simulation.Rooms.Map.Portals);
            Assert.Empty(simulation.Grid.StateDependentBlocks);

            Assert.True(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
        }

        [Fact]

        public void APassRunWhileADoorIsOpenPublishesItVented()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            simulation.MarkTopologyDirty();
            simulation.Update(1f / 60f, Worlds.Shadow());
            simulation.Rooms.RunToCompletion();

            Assert.True(simulation.Rooms.Map.IsExternal(Vector3I.Zero));
            Assert.Equal(1, simulation.Rooms.Map.RoomCount);
            Assert.Equal(0, simulation.Rooms.Map.AirtightRoomCount);
        }


        [Fact]

        public void RoomIdentitySurvivesADoorCycle()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);

            RoomMap before = simulation.Rooms.Map;
            int room = before.RoomIndexOf(Vector3I.Zero);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.Same(before, simulation.Rooms.Map);
            Assert.Equal(room, simulation.Rooms.Map.RoomIndexOf(Vector3I.Zero));
        }

        [Fact]

        public void ChangedRoomsNamesOnlyWhatActuallyChanged()
        {
            BlockInstance door;

            ThermalSimulation simulation = ShellWithDoor(Catalog.SlideDoor(), out door);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.Single(simulation.Rooms.Map.ChangedRooms);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            Assert.Empty(simulation.Rooms.Map.ChangedRooms);
        }
    }
}

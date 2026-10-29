using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Tiny restore boundaries: healthy records survive nonfinite neighbours, current records match
    /// exact block anchors, and legacy reference cells still restore rotated multicell blocks.
    /// </summary>
    public class PersistenceRestoreTests
    {
        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        public void NonfiniteRecordsDoNotReplaceHealthyStoredState(float invalid)
        {
            var position = Vector3I.Zero;
            string data = ThermalStorageCodec.Encode(
                new[] { new StoredTemperature(position, 450f), new StoredTemperature(position, invalid) },
                new[] { new StoredLoop(1, invalid) },
                new[] { new StoredRoom(Vector3I.UnitX, invalid) },
                new[] { new StoredHeldCoolant(position, invalid) },
                new[] { new StoredLoopFill(1, invalid) });
            var blocks = new List<StoredTemperature>();
            var loops = new List<StoredLoop>();
            var rooms = new List<StoredRoom>();
            var held = new List<StoredHeldCoolant>();
            var fills = new List<StoredLoopFill>();
            Assert.True(ThermalStorageCodec.TryDecode(data, blocks, loops, rooms, held, fills));
            Assert.Single(blocks);
            Assert.Equal(450f, blocks[0].Temperature);
            Assert.Empty(loops);
            Assert.Empty(rooms);
            Assert.Empty(held);
            Assert.Empty(fills);

            var builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), position);
            var simulation = builder.BuildSimulation(new ThermalSettings(), 420f);
            Assert.Equal(1, simulation.Load(data));
            Assert.Equal(450f, simulation.Solver.Nodes[0].Temperature);
            Assert.Equal(0f, simulation.Solver.Nodes[0].HeldCoolantCapacity);

            // Exercise the public room restore entry point independently of the codec.
            var map = new RoomMap();
            map.SetSearchBounds(Vector3I.Zero, new Vector3I(2, 1, 1));
            map.AddToRoom(map.BeginRoom(), Vector3I.UnitX);
            simulation.Solver.RebuildRoomAir(map);
            var air = Assert.Single(simulation.RoomAir);
            air.Temperature = 430f;
            Assert.False(air.Initialised);
            Assert.Equal(0, simulation.Solver.RestoreRoomAir(new[] { new StoredRoom(air.Anchor, invalid) }));
            Assert.Equal(430f, air.Temperature);
            Assert.False(air.Initialised);
            Assert.Equal(1, simulation.Solver.RestoreRoomAir(new[]
            {
                new StoredRoom(air.Anchor, 410f), new StoredRoom(air.Anchor, invalid)
            }));
            Assert.Equal(410f, air.Temperature);
            Assert.True(air.Initialised);
        }

        [Fact]
        public void CurrentAnchorsAreExactWhileLegacyReferenceCellsRemainSupported()
        {
            var min = new Vector3I(-3, 2, 1);
            var reference = min + Vector3I.UnitX;
            var orientation = new BlockOrientation(Base6Directions.Direction.Backward, Base6Directions.Direction.Up);
            var builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmorBar(2), min, orientation);
            var simulation = builder.BuildSimulation(new ThermalSettings(), 420f);
            var node = Assert.Single(simulation.Solver.Nodes);
            Assert.Equal(reference, node.Block.LocalToGrid(Vector3I.Zero));
            Assert.Equal(min, node.Block.Position);

            string stale = ThermalStorageCodec.Encode(
                new[] { new StoredTemperature(reference, 500f) }, null, null,
                new[] { new StoredHeldCoolant(reference, 1000f) });
            Assert.Equal(0, simulation.Load(stale));
            Assert.Equal(420f, node.Temperature);
            Assert.Equal(0f, node.HeldCoolantCapacity);

            // The original game's legacy writer saved SlimBlock.Position, not Min.
            string legacy = ThermalStorageCodec.EncodeLegacyBlocks(
                new[] { new StoredTemperature(reference, 500f) });
            Assert.Equal(1, simulation.Load(legacy));
            Assert.Equal(500f, node.Temperature);

            string current = ThermalStorageCodec.Encode(
                new[] { new StoredTemperature(min, -10f) }, null, null,
                new[] { new StoredHeldCoolant(min, 1000f) });
            Assert.Equal(1, simulation.Load(current));
            Assert.Equal(ThermalConstants.MinimumTemperature, node.Temperature);
            Assert.Equal(1000f, node.HeldCoolantCapacity);
        }
    }
}

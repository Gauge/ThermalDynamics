using Thermodynamics.Core;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The one definition of "the same map": cell for cell, room for room, portal for portal,
    /// venting included. A map is not a temperature, so "close" has no meaning — one cell
    /// classified differently is a different room, a different exposure, a different ship.
    ///
    /// <para>
    /// Shared by <see cref="RoomMapSnapshotTests"/> (the snapshot path against the dictionary
    /// path) and <see cref="RoomMapRecyclingTests"/> (a recycled map against a fresh one),
    /// because two drifted copies of a map comparison would each pass on what the other
    /// catches, silently (`P5`).
    /// </para>
    /// </summary>
    internal static class RoomMapAssert
    {

        internal static void SameMap(RoomMap expected, RoomMap actual, string what)
        {
            Assert.True(expected.RoomCount > 0, what + ": the reference map found no rooms, so agreement proves nothing");
            Assert.Equal(expected.RoomCount, actual.RoomCount);
            Assert.Equal(expected.SolidCellCount, actual.SolidCellCount);
            Assert.Equal(expected.ExternalCellCount, actual.ExternalCellCount);
            Assert.Equal(expected.RoomCellCount, actual.RoomCellCount);

            for (int r = 0; r < expected.RoomCount; r++)
            {
                RoomMap.RoomCells a = expected.CellsOf(r);
                RoomMap.RoomCells b = actual.CellsOf(r);
                Assert.True(a.Count == b.Count, what + ": room " + r + " has " + b.Count + " cells against the reference's " + a.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.True(a[i] == b[i], what + ": room " + r + " cell " + i + " is " + b[i] + " against the reference's " + a[i]);
                }
                Assert.Equal(expected.IsVented(r), actual.IsVented(r));
            }

            Assert.Equal(expected.Portals.Count, actual.Portals.Count);
            for (int p = 0; p < expected.Portals.Count; p++)
            {
                Assert.Equal(expected.Portals[p].Block.Model.Name, actual.Portals[p].Block.Model.Name);
                Assert.Equal(expected.Portals[p].Block.Min, actual.Portals[p].Block.Min);
                Assert.Equal(expected.Portals[p].Face, actual.Portals[p].Face);
                Assert.Equal(expected.Portals[p].RegionA, actual.Portals[p].RegionA);
                Assert.Equal(expected.Portals[p].RegionB, actual.Portals[p].RegionB);
            }
        }
    }
}

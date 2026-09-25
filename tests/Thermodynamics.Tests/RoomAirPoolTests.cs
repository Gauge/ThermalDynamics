using System;
using System.Text;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Room air nodes are pooled across rebuilds, and a rebuild is the same rebuild for it.**
    ///
    /// <para>
    /// A rebuild allocated a node and a link list per room every time — 2,573 KB on a
    /// 126,731-block hull, mostly the links — and it runs on every completed room pass. Reusing the
    /// node keeps the list's capacity, which is where the bytes were.
    /// </para>
    ///
    /// <para>
    /// **The bug this could cause is silent and physical, not a crash**, which is why the tests are
    /// paired rather than a smoke check. The carry-over map used to hold the *nodes* a rebuild was
    /// about to reuse, so a room could have read a temperature belonging to whichever room reused
    /// its node first; it holds copies now. And `Initialised` was the one field written on only one
    /// of the two branches, so a pooled node would have arrived at a fresh room already claiming to
    /// hold a meaningful temperature.
    /// </para>
    /// </summary>
    public class RoomAirPoolTests
    {

        private static ThermalSimulation Hull(int side)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableRoomAir = true;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(side, side, side));
            for (int x = 1; x < side - 1; x++)
                for (int y = 1; y < side - 1; y++)
                    for (int z = 1; z < side - 1; z++)
                        builder.Remove(new Vector3I(x, y, z));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static string State(ThermalSimulation simulation)
        {

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < simulation.RoomAir.Count; i++)
            {
                RoomAirNode air = simulation.RoomAir[i];
                text.Append(air.RoomIndex).Append(' ').Append(air.Anchor).Append(' ')
                    .Append(air.CellCount).Append(' ')
                    .Append(air.Volume.ToString("R")).Append(' ')
                    .Append(air.Temperature.ToString("R")).Append(' ')
                    .Append(air.Pressure.ToString("R")).Append(' ')
                    .Append(air.Initialised).Append(' ')
                    .Append(air.ThermalMass.ToString("R")).Append(' ')
                    .Append(air.Links.Count).Append('\n');
            }

            return text.ToString();
        }


        private static void Rebuild(ThermalSimulation simulation)
        {
            simulation.Rooms.RequestRestart(simulation.Grid);
            simulation.Rooms.RunToCompletion();
            simulation.Solver.RebuildRoomAir(simulation.Rooms.Map);
        }

        [Fact]

        public void RepeatedRebuildsAgreeWithASingleOne()
        {

            ThermalSimulation reused = Hull(6);
            Rebuild(reused);

            string first = State(reused);

            Assert.False(string.IsNullOrEmpty(first), "the hull carried no room air, so this compared nothing");

            for (int i = 0; i < 4; i++) Rebuild(reused);


            ThermalSimulation fresh = Hull(6);
            Rebuild(fresh);

            Assert.Equal(first, State(reused));
            Assert.Equal(State(fresh), State(reused));
        }

        [Fact]

        public void AFilledRoomCarriesItsTemperatureAndPressureOver()
        {

            ThermalSimulation simulation = Hull(6);
            Rebuild(simulation);

            RoomMap map = simulation.Rooms.Map;
            int filled = 0;
            for (int r = 0; r < map.RoomCount; r++)
            {
                if (map.CellsInRoom(r) == 0) continue;
                if (simulation.SetRoomPressure(map.CellsOf(r)[0], 1f)) filled++;
            }

            Assert.True(filled > 0, "no room took air, so the carry-over is untested");

            for (int i = 0; i < simulation.RoomAir.Count; i++) simulation.RoomAir[i].Temperature = 321.5f;

            Rebuild(simulation);

            int carried = 0;
            for (int i = 0; i < simulation.RoomAir.Count; i++)
            {
                RoomAirNode air = simulation.RoomAir[i];
                if (air.Pressure <= 0f) continue;

                carried++;
                Assert.Equal(321.5f, air.Temperature, 3);
                Assert.True(air.Initialised, "a room that carried its air is not marked initialised");
            }

            Assert.True(carried > 0, "no room carried its pressure over");
        }

        [Fact]

        public void ARoomWithNoCarryOverIsNotMarkedInitialised()
        {

            ThermalSimulation simulation = Hull(6);
            Rebuild(simulation);

            RoomMap map = simulation.Rooms.Map;
            for (int r = 0; r < map.RoomCount; r++)
            {
                if (map.CellsInRoom(r) == 0) continue;
                simulation.SetRoomPressure(map.CellsOf(r)[0], 1f);
            }

            Vector3I anchor = Vector3I.Zero;
            bool any = false;
            for (int i = 0; i < simulation.RoomAir.Count; i++)
            {
                RoomAirNode air = simulation.RoomAir[i];
                air.Initialised = true;
                air.Temperature = 400f;
                if (!any) { anchor = air.Anchor; any = true; }
            }

            Assert.True(any, "the hull carried no room air, so this tests nothing");

            simulation.AddBlock(

                new BlockInstance(Catalog.HeavyArmor(), anchor, BlockOrientation.Identity), 293.15f);

            Rebuild(simulation);

            bool checkedOne = false;
            for (int i = 0; i < simulation.RoomAir.Count; i++)
            {
                RoomAirNode air = simulation.RoomAir[i];
                if (air.Anchor == anchor) continue;

                checkedOne = true;
                Assert.False(air.Initialised,
                    "a room with no carry-over came back initialised, so a pooled node kept a"
                        + " predecessor's flag");
                Assert.NotEqual(400f, air.Temperature);
            }

            Assert.True(checkedOne, "no room moved its anchor, so the branch was never taken");
        }

        [Fact]

        public void ARepeatedRebuildAllocatesLess()
        {

            ThermalSimulation simulation = Hull(10);
            Rebuild(simulation);
            Rebuild(simulation);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetAllocatedBytesForCurrentThread();
            simulation.Solver.RebuildRoomAir(simulation.Rooms.Map);
            long after = GC.GetAllocatedBytesForCurrentThread() - before;


            ThermalSimulation cold = Hull(10);
            Rebuild(cold);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long coldBefore = GC.GetAllocatedBytesForCurrentThread();

            ThermalSimulation second = Hull(10);
            Rebuild(second);
            long coldCost = GC.GetAllocatedBytesForCurrentThread() - coldBefore;

            Assert.True(after < coldCost,
                "a pooled rebuild allocated " + after + " B, no better than a cold one at " + coldCost);
        }
    }
}

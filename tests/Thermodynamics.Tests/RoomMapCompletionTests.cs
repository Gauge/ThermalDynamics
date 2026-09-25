using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A load-time room mapping pass finishes, and says so when it does not.
    ///
    /// <para>
    /// `RunToCompletion` carried a flat safety limit of twenty million cell visits. The flood walks
    /// the grid's bounding volume, which passes twenty million at about seven hundred thousand
    /// blocks — and passing it returned quietly, having published nothing, leaving
    /// `RoomMap.AllExternal` in place. A hull that size finished its load with no rooms at all and
    /// every interior block classified as facing open space: 95 % of blocks exposed against 34 % on
    /// the same shape one rung smaller, all of them radiating and convecting to the sky, and no
    /// compartment holding air.
    /// </para>
    ///
    /// <para>
    /// The limit is now derived from the volume it is about to walk, which cannot bind on a pass
    /// that is going to finish, and the call reports whether it completed. These tests pin both,
    /// and they reach the failure through a bounding volume rather than through a block count —
    /// the volume is what the limit counts, and a grid of eight blocks can have as much of it as a
    /// grid of eight hundred thousand.
    /// </para>
    /// </summary>
    public class RoomMapCompletionTests
    {

        private static ThermalSimulation SparseAcrossAHugeVolume(int span)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), 0, 0, 0);
            builder.Place(Catalog.LightArmor(), span, 0, 0);
            builder.Place(Catalog.LightArmor(), 0, span, 0);
            builder.Place(Catalog.LightArmor(), 0, 0, span);
            builder.Place(Catalog.LightArmor(), span, span, span);


            ThermalSettings settings = new ThermalSettings();
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            return builder.BuildSimulation(settings, 293.15f);
        }

        [Fact]

        public void APassOverAVolumeTheOldLimitCouldNotWalkStillCompletes()
        {

            ThermalSimulation simulation = SparseAcrossAHugeVolume(280);

            simulation.Rooms.RequestRestart(simulation.Grid);
            bool completed = simulation.Rooms.RunToCompletion();

            Assert.True(completed, "the pass gave up before publishing");

            Assert.True(simulation.Rooms.Map.ExternalCellCount > 20000000,
                "the map published " + simulation.Rooms.Map.ExternalCellCount
                + " external cells, which is not a walk of a 21.9 million cell volume");
            Assert.Equal(5, simulation.Rooms.Map.SolidCellCount);
        }

        [Fact]

        public void APassThatGivesUpSaysSoAndPublishesNothing()
        {

            ThermalSimulation simulation = SparseAcrossAHugeVolume(80);


            SurfaceMap surfaces = new SurfaceMap();
            surfaces.Rebuild(simulation.Grid);


            RoomMapper mapper = new RoomMapper(surfaces);
            mapper.RequestRestart(simulation.Grid);

            bool completed = mapper.RunToCompletion(safetyLimit: 4096);

            Assert.False(completed, "a pass over half a million cells finished inside 4,096 visits");
            Assert.True(mapper.Map.IsEmpty,
                "an unfinished pass published a map, so the grid would be running on a partial one");
            Assert.True(mapper.HasWorkPending, "the pass reported nothing left to do");
        }

        [Fact]

        public void AnUnfinishedPassLeavesTheMapItHadInPlace()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 4000));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            int rooms = simulation.Rooms.Map.RoomCount;
            Assert.True(rooms > 0);

            simulation.Rooms.RequestRestart(simulation.Grid);
            Assert.False(simulation.Rooms.RunToCompletion(safetyLimit: 4096));

            Assert.Equal(rooms, simulation.Rooms.Map.RoomCount);
        }

        [Fact]

        public void AnOrdinaryHullMapsAtLoadAndReportsIt()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", 8000));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            Assert.True(simulation.Rooms.Map.RoomCount > 0,
                "a census hull with bulkheads mapped no compartments at all");

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int exposed = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].TotalExposedFaces > 0) exposed++;
            }

            Assert.True(exposed < nodes.Count * 9 / 10,
                exposed + " of " + nodes.Count + " blocks are exposed, which is what an unmapped"
                + " grid looks like rather than a hull with compartments in it");
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// <see cref="ThermalSimulation.RefreshBlock"/> is the path for a block whose geometry or
    /// mounting changed under an existing node — the block is not placed and not removed, so
    /// neither of the two incremental paths applies to it.
    ///
    /// It used to do neither: it refreshed the surface bits, dirtied the topology and left every
    /// conduction link touching the block carrying a conductance derived from mounting the block
    /// no longer had. Contact area is the product of both ends' mount fractions, so those links
    /// were wrong rather than merely stale, and nothing recomputed them. Meanwhile it charged a
    /// full room remap for the privilege.
    ///
    /// These pin both halves: the graph it leaves behind is the graph a full rebuild would build,
    /// and the flood fill runs again only when what the block seals actually changed.
    /// </summary>
    public class BlockRefreshTests
    {

        private static List<string> Graph(ThermalSolver solver)
        {

            List<string> rows = new List<string>();

            foreach (ThermalLink link in solver.Links)
            {
                string a = solver.Nodes[link.NodeA].Block.Min.ToString();
                string b = solver.Nodes[link.NodeB].Block.Min.ToString();
                string low = string.CompareOrdinal(a, b) <= 0 ? a : b;
                string high = string.CompareOrdinal(a, b) <= 0 ? b : a;

                rows.Add(low + "|" + high + "|" + link.Conductance.ToString("R")
                    + "|" + link.ContactFaces);
            }

            rows.Sort(string.CompareOrdinal);
            return rows;
        }


        private static List<string> Degrees(ThermalSolver solver)
        {

            List<string> rows = new List<string>();
            for (int i = 0; i < solver.Nodes.Count; i++)
            {
                ThermalNode node = solver.Nodes[i];
                rows.Add(node.Block.Min + "|" + node.LinkCount);
            }
            rows.Sort(string.CompareOrdinal);
            return rows;
        }


        private static ThermalSimulation Ship()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), new Vector3I(-2, -1, -1), new Vector3I(3, 2, 2));
            builder.Place(Catalog.Reactor(), new Vector3I(0, 2, 0));
            builder.Place(Catalog.LightArmorBar(3), new Vector3I(-1, -2, 0));
            builder.Place(Catalog.HeavyArmor(), new Vector3I(3, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void RefreshingABlockLeavesTheGraphAFullRebuildWouldHaveBuilt()
        {

            ThermalSimulation simulation = Ship();
            ThermalSolver solver = simulation.Solver;


            List<string> before = Graph(solver);

            List<string> degreesBefore = Degrees(solver);

            BlockInstance block = simulation.Grid.GetAtCell(Vector3I.Zero);
            simulation.RefreshBlock(block);
            solver.BuildLinksIfNeeded();

            Assert.Equal(before, Graph(solver));
            Assert.Equal(degreesBefore, Degrees(solver));

            solver.RebuildLinks();
            Assert.Equal(before, Graph(solver));
        }


        private static BlockModel BoltedTopAndBottom()
        {
            BlockModel model = BlockModel.Solid(
                "Bracket", Vector3I.One, 400f, Catalog.LightArmor().Thermal);

            int state = CellSurface.SelfAirtightMask;
            state = CellSurface.WithSelfMount(state, Face.Up, true);
            state = CellSurface.WithSelfMount(state, Face.Down, true);
            model.SetLocalSurface(Vector3I.Zero, state);
            return model;
        }

        [Fact]

        public void ReorientingABlockRebuildsTheJointsItsMountsDecide()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(BoltedTopAndBottom(), Vector3I.Zero);
            BlockInstance bracket = builder.Last;
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            simulation.RebuildAll();
            ThermalSolver solver = simulation.Solver;

            Assert.Equal(0, solver.GetNode(bracket).LinkCount);
            Assert.Empty(Graph(solver));


            bracket.Orientation = new BlockOrientation(
                Base6Directions.Direction.Forward, Base6Directions.Direction.Right);

            simulation.RefreshBlock(bracket);
            solver.BuildLinksIfNeeded();

            Assert.Equal(1, solver.GetNode(bracket).LinkCount);

            List<string> repaired = Graph(solver);
            Assert.Single(repaired);

            solver.RebuildLinks();
            Assert.Equal(repaired, Graph(solver));
        }

        [Fact]

        public void ReorientingABlockAwayFromItsNeighbourDropsTheJoint()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(BoltedTopAndBottom(), Vector3I.Zero,

                new BlockOrientation(Base6Directions.Direction.Forward, Base6Directions.Direction.Right));
            BlockInstance bracket = builder.Last;
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            simulation.RebuildAll();
            ThermalSolver solver = simulation.Solver;

            Assert.Equal(1, solver.GetNode(bracket).LinkCount);

            bracket.Orientation = BlockOrientation.Identity;
            simulation.RefreshBlock(bracket);
            solver.BuildLinksIfNeeded();

            Assert.Equal(0, solver.GetNode(bracket).LinkCount);
            Assert.Empty(Graph(solver));

            solver.RebuildLinks();
            Assert.Empty(Graph(solver));
        }

        [Fact]

        public void RefreshingEveryBlockInTurnLeavesTheGraphIntact()
        {

            ThermalSimulation simulation = Ship();
            ThermalSolver solver = simulation.Solver;


            List<string> before = Graph(solver);

            IList<BlockInstance> blocks = simulation.Grid.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                simulation.RefreshBlock(blocks[i]);
                solver.BuildLinksIfNeeded();
            }

            Assert.Equal(before, Graph(solver));
        }

        [Fact]

        public void RefreshingABlockCostsItsOwnDegreeRatherThanTheGrid()
        {

            ThermalSimulation simulation = Ship();
            ThermalSolver solver = simulation.Solver;

            BlockInstance block = simulation.Grid.GetAtCell(Vector3I.Zero);
            int degree = solver.GetNode(block).LinkCount;
            Assert.True(degree > 0);

            solver.Work.LinksRemoved = 0;
            solver.Work.TopologyNodeVisits = 0;

            simulation.RefreshBlock(block);
            solver.BuildLinksIfNeeded();

            Assert.Equal(degree, solver.Work.LinksRemoved);
            Assert.Equal(1, solver.Work.TopologyNodeVisits);
            Assert.True(solver.Nodes.Count > 10);
        }

        [Fact]

        public void AMountingChangeDoesNotAskForARemap()
        {

            ThermalSimulation simulation = Ship();
            long passesBefore = simulation.Rooms.Work.RoomPassesBegun;

            BlockInstance block = simulation.Grid.GetAtCell(Vector3I.Zero);
            simulation.RefreshBlock(block);
            simulation.Update(1f / 60f, EnvironmentSample.Vacuum(Vector3.Up));

            Assert.Equal(passesBefore, simulation.Rooms.Work.RoomPassesBegun);
        }

        [Fact]

        public void ADoorOpeningIsResolvedThroughItsPortalRatherThanARemap()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            BlockInstance wall = builder.Grid.GetAtCell(new Vector3I(0, 0, -1));
            builder.Grid.Remove(wall);
            builder.Place(Catalog.AirtightDoor(), new Vector3I(0, 0, -1));
            BlockInstance door = builder.Last;
            door.IsSealedByDoorState = true;
            door.RefreshSurfaces();

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            simulation.RebuildAll();

            long passesBefore = simulation.Rooms.Work.RoomPassesBegun;

            door.IsSealedByDoorState = false;
            simulation.RefreshBlock(door);
            simulation.Update(1f / 60f, EnvironmentSample.Vacuum(Vector3.Up));

            Assert.Equal(passesBefore, simulation.Rooms.Work.RoomPassesBegun);

            Assert.True(simulation.Rooms.Map.RoomCount >= 0);
        }

        [Fact]

        public void ADoorTheMapperHasNeverSeenAsksForARemap()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            simulation.RebuildAll();

            long passesBefore = simulation.Rooms.Work.RoomPassesBegun;

            BlockInstance wall = simulation.Grid.GetAtCell(new Vector3I(0, 0, -1));
            simulation.RemoveBlock(wall);

            BlockInstance door = new BlockInstance(
                Catalog.AirtightDoor(), new Vector3I(0, 0, -1), BlockOrientation.Identity);
            door.IsSealedByDoorState = true;
            simulation.AddBlock(door);

            door.IsSealedByDoorState = false;
            simulation.RefreshBlock(door);
            simulation.Update(1f / 60f, EnvironmentSample.Vacuum(Vector3.Up));

            Assert.True(simulation.Rooms.Work.RoomPassesBegun > passesBefore);
        }

        [Fact]

        public void RefreshingABlockRecountsItsOwnExposedFaces()
        {

            ThermalSimulation simulation = Ship();

            BlockInstance block = simulation.Grid.GetAtCell(new Vector3I(3, 0, 0));
            ThermalNode node = simulation.Solver.GetNode(block);

            int[] expected = new int[Face.Count];
            for (int f = 0; f < Face.Count; f++)
            {
                expected[f] = node.GetExposedFaces(f);
                node.SetExposedFaces(f, 0);
            }
            node.RefreshExposure();
            Assert.Equal(0f, node.ExposedArea);

            simulation.RefreshBlock(block);

            for (int f = 0; f < Face.Count; f++)
            {
                Assert.Equal(expected[f], node.GetExposedFaces(f));
            }
            Assert.True(node.ExposedArea > 0f);
        }

        [Fact]

        public void RefreshingABlockKeepsItsTemperature()
        {

            ThermalSimulation simulation = Ship();

            BlockInstance block = simulation.Grid.GetAtCell(Vector3I.Zero);
            ThermalNode node = simulation.Solver.GetNode(block);
            node.Temperature = 777f;

            simulation.RefreshBlock(block);
            simulation.Solver.BuildLinksIfNeeded();

            Assert.Same(node, simulation.Solver.GetNode(block));
            Assert.Equal(777f, node.Temperature);
        }

        [Fact]

        public void RefreshingAnUnknownBlockDoesNothing()
        {

            ThermalSimulation simulation = Ship();

            List<string> before = Graph(simulation.Solver);


            BlockInstance stranger = new BlockInstance(
                Catalog.LightArmor(), new Vector3I(40, 40, 40), BlockOrientation.Identity);

            simulation.RefreshBlock(null);
            simulation.RefreshBlock(stranger);
            simulation.Solver.BuildLinksIfNeeded();

            Assert.Equal(before, Graph(simulation.Solver));
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Two properties the game adapter depends on and cannot check for itself.
    ///
    /// A host holds its own map from game block to simulation node. These pin down what that map
    /// has to be keyed on, and what the host is guaranteed to be told about damage — both of
    /// which had adapter bugs that only showed up on blocks larger than one cell, or on ticks
    /// that ran more than one step.
    /// </summary>
    public class MultiCellAndDamageTests
    {

        private static BlockThermalProperties Fragile()
        {
            BlockThermalProperties t = Catalog.DefaultThermal();
            t.CriticalTemperature = 400f;
            t.OverheatDamagePerKelvin = 1f;
            return t;
        }

        [Fact]

        public void ABlockIsIdentifiedByItsMinimumCell()
        {

            GridModel grid = new GridModel(2.5f);
            BlockModel model = BlockModel.Solid("BigBlock", new Vector3I(3, 3, 3), 5000f, Catalog.DefaultThermal());


            Vector3I min = new Vector3I(4, 0, 0);
            BlockInstance block = grid.Add(model, min);

            Assert.Equal(min, block.Position);
            Assert.Equal(GridMath.Key(min), block.Key);

            Assert.Same(block, grid.GetAtCell(min));
            Assert.Same(block, grid.GetAtCell(min + new Vector3I(1, 1, 1)));
            Assert.Same(block, grid.GetAtCell(min + new Vector3I(2, 2, 2)));
        }

        [Fact]

        public void AnOverheatEventNamesTheBlockByItsMinimumCell()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            BlockModel big = BlockModel.Solid("BigBlock", new Vector3I(3, 3, 3), 5000f, Fragile());
            builder.Place(big, new Vector3I(2, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 900f);
            simulation.RebuildAll();
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Single(simulation.Overheats);
            Assert.Equal(new Vector3I(2, 0, 0), simulation.Overheats[0].Block.Position);
        }

        [Fact]

        public void OverheatsFromEveryStepOfAnUpdateReachTheHost()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Fragile", Vector3I.One, 500f, Fragile()), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 900f);
            simulation.RebuildAll();

            simulation.StepExact(5, Worlds.Shadow());

            Assert.Equal(5, simulation.Overheats.Count);
        }

        [Fact]

        public void EachUpdateReportsOnlyItsOwnOverheats()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableEnvironment = false;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Fragile", Vector3I.One, 500f, Fragile()), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 900f);
            simulation.RebuildAll();

            simulation.StepExact(3, Worlds.Shadow());
            Assert.Equal(3, simulation.Overheats.Count);

            simulation.StepExact(2, Worlds.Shadow());
            Assert.Equal(2, simulation.Overheats.Count);
        }

        [Fact]

        public void ACoolBlockProducesNoOverheats()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();
            simulation.StepExact(10, Worlds.Shadow());

            Assert.Empty(simulation.Overheats);
        }

        [Fact]

        public void RefreshingSealingLeavesTheConductionGraphAlone()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            int links = simulation.Solver.Links.Count;
            float conductance = simulation.Solver.Links[0].Conductance;

            BlockInstance door = builder.Placed[0];
            door.IsSealedByDoorState = false;
            simulation.RefreshBlockSealing(door);

            simulation.Update(1f / 6f, Worlds.Shadow());

            Assert.Equal(links, simulation.Solver.Links.Count);
            Assert.Equal(conductance, simulation.Solver.Links[0].Conductance, 4);
        }

        [Fact]

        public void RefreshingSealingStillRemapsTheRooms()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.RebuildAll();

            Assert.False(simulation.Rooms.HasWorkPending);

            simulation.RefreshBlockSealing(builder.Placed[0]);

            Assert.True(simulation.Rooms.HasWorkPending);
        }
    }
}

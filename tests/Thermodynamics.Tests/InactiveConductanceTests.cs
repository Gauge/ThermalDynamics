using System.Collections.Generic;
using System.Reflection;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Two-block checks that inactive exchange paths cannot demand substeps, inflate heat capacity,
    /// or suppress independent source heating. Toggles also cover incremental link edits and the
    /// removal of an already-applied mass floor, without a large hull or long simulation.
    /// </summary>
    public class InactiveConductanceTests
    {
        private static ThermalSimulation Pair(bool adjacent, int floor = 0)
        {
            var settings = new ThermalSettings
            {
                EnableConduction = false, EnableCoolantLoops = false, EnableRoomAir = false,
                EnableEnvironment = false, EnableSolarHeat = false, EnableWasteHeat = false,
                EnableDamage = false, HeatTimeScale = 1f, MaxSubstepsPerBlock = floor,
            };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            Add(simulation, 0);
            Add(simulation, adjacent ? 1 : 3);
            simulation.RebuildAll();
            return simulation;
        }

        private static BlockInstance Add(ThermalSimulation simulation, int x)
        {
            var thermal = new BlockThermalProperties
                { Conductivity = 100000f, SpecificHeat = 1f, SolarAbsorptivity = .5f };
            var block = new BlockInstance(BlockModel.Solid("stiff", Vector3I.One, 1f, thermal),
                new Vector3I(x, 0, 0), BlockOrientation.Identity);
            simulation.AddBlock(block, 300f);
            return block;
        }

        [Fact]
        public void SwitchingConductionSurvivesIncrementalPlacementAndRemoval()
        {
            var simulation = Pair(true);
            var solver = simulation.Solver;
            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            Assert.Equal(0f, solver.NodeConductanceTotal(0));

            simulation.Settings.EnableConduction = true;
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            Assert.True(solver.NodeConductanceTotal(0) > 0f);

            simulation.Settings.EnableConduction = false;
            BlockInstance third = Add(simulation, 2);
            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            simulation.RemoveBlock(simulation.Grid.Blocks[0]);
            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            for (int i = 0; i < solver.Nodes.Count; i++) Assert.Equal(0f, solver.NodeConductanceTotal(i));

            simulation.Settings.EnableConduction = true;
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            simulation.RemoveBlock(third);
            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            Add(simulation, 2);
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            Assert.Single(solver.Links);
            for (int i = 0; i < solver.Nodes.Count; i++)
                Assert.Equal(solver.Links[0].Conductance, solver.NodeConductanceTotal(i));
        }

        [Fact]
        public void InactiveNeighboursCannotFloorOrSlowIndependentSourceHeating()
        {
            var joined = Pair(true, 1);
            var separate = Pair(false, 1);
            joined.Settings.EnableConduction = true;
            joined.Solver.RequiredSubsteps(.25f);
            Assert.Equal(2, joined.Solver.FlooredNodes);
            joined.Settings.EnableConduction = false;

            var environment = new EnvironmentState
            {
                HeatSources = new[] { new HeatSourceState(Vector3.Up, 16f) },
                HeatSourceCount = 1,
            };
            joined.Solver.Step(.25f, environment);
            separate.Solver.Step(.25f, environment);
            Assert.Equal(1, joined.Solver.LastSubsteps);
            Assert.Equal(0, joined.Solver.FlooredNodes);
            Assert.False(joined.Solver.ConductionClampLive);
            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(312.5f, joined.Solver.Nodes[i].Temperature, 4);
                Assert.Equal(separate.Solver.Nodes[i].Temperature, joined.Solver.Nodes[i].Temperature, 4);
            }
        }

        [Fact]
        public void TurningTheFloorOffRestoresRealCapacityWithoutDirtyNodes()
        {
            var simulation = Pair(true, 1);
            var solver = simulation.Solver;
            simulation.Settings.EnableConduction = true;
            solver.RequiredSubsteps(.25f);
            Assert.Equal(2, solver.FlooredNodes);
            simulation.Settings.MaxSubstepsPerBlock = 0;
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            Assert.Equal(0, solver.FlooredNodes);
            var effective = (float[])typeof(ThermalSolver).GetField("nodeThermalMass",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(solver);
            for (int i = 0; i < solver.Nodes.Count; i++)
                Assert.Equal(solver.Nodes[i].ThermalMass, effective[i]);
        }

        [Fact]
        public void DisablingBlockConductionPreservesOnlyEnabledCoolantAndRoomCoupling()
        {
            var simulation = Pair(true);
            var solver = simulation.Solver;
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 300f);
            loop.Pipes.Add(simulation.Grid.Blocks[0]);
            solver.SetLoops(new List<CoolantLoop> { loop });
            loop.Links.Clear();
            loop.Links.Add(new LoopLink(0, 50000f, 0));
            var room = new RoomAirNode { Volume = 1f, Pressure = 1f, Temperature = 300f };
            room.RefreshThermalMass();
            room.Links.Add(new RoomLink(1, 70000f));
            solver.RoomAir.Add(room);

            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            simulation.Settings.EnableCoolantLoops = true;
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            Assert.Equal(50000f, solver.NodeConductanceTotal(0));
            Assert.Equal(0f, solver.NodeConductanceTotal(1));

            simulation.Settings.EnableCoolantLoops = false;
            simulation.Settings.EnableRoomAir = true;
            Assert.True(solver.RequiredSubsteps(.25f) > 1f);
            Assert.Equal(0f, solver.NodeConductanceTotal(0));
            Assert.Equal(70000f, solver.NodeConductanceTotal(1));

            simulation.Settings.EnableRoomAir = false;
            Assert.Equal(1f, solver.RequiredSubsteps(.25f));
            Assert.Equal(0f, solver.NodeConductanceTotal(1));
        }
    }
}

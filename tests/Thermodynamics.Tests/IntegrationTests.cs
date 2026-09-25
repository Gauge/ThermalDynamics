using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The simulation driven the way a host drives it: build, edit, step, save, load.
    ///
    /// <para>
    /// Everything else in this project tests one mechanism against a fixture built for it. This tests
    /// that the mechanisms compose — that a block added at runtime joins the graph, that removing one
    /// opens its neighbours to space, and that a save round-trips every temperature and is taken
    /// without disturbing the run it was taken from.
    /// </para>
    /// </summary>
    public class SimulationIntegrationTests
    {
        [Fact]

        public void ABuiltSimulationHasNodesLinksAndExposure()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            Assert.Equal(27, simulation.Solver.Nodes.Count);
            Assert.Equal(54, simulation.Solver.Links.Count);

            ThermalNode centre = simulation.Solver.GetNodeAt(new Vector3I(1, 1, 1));
            ThermalNode corner = simulation.Solver.GetNodeAt(Vector3I.Zero);

            Assert.Equal(0, centre.TotalExposedFaces);
            Assert.Equal(3, corner.TotalExposedFaces);
        }

        [Fact]

        public void BlocksMarkedIgnoreThermalsNeverBecomeNodes()
        {
            BlockThermalProperties ignored = Catalog.DefaultThermal();
            ignored.ExcludeFromSimulation = true;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(BlockModel.Solid("Decoration", Vector3I.One, 10f, ignored), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            Assert.Single(simulation.Solver.Nodes);
            Assert.Empty(simulation.Solver.Links);
        }

        [Fact]

        public void AddingABlockAtRuntimeJoinsTheConductionGraph()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            Assert.Empty(simulation.Solver.Links);

            simulation.AddBlock(new BlockInstance(Catalog.LightArmor(), new Vector3I(1, 0, 0), BlockOrientation.Identity));
            simulation.Update(1f, Worlds.Shadow());

            Assert.Equal(2, simulation.Solver.Nodes.Count);
            Assert.Single(simulation.Solver.Links);
        }

        [Fact]

        public void RemovingABlockLeavesTheRestConsistent()
        {
            GridBuilder builder = GridBuilder.Large();
            for (int x = 0; x < 3; x++) builder.Place(Catalog.LightArmor(), new Vector3I(x, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            Assert.Equal(2, simulation.Solver.Links.Count);

            simulation.RemoveBlock(builder.Placed[1]);
            simulation.RebuildAll();

            Assert.Equal(2, simulation.Solver.Nodes.Count);
            Assert.Empty(simulation.Solver.Links);

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                Assert.Equal(i, simulation.Solver.Nodes[i].Index);
            }
        }

        [Fact]

        public void RemovingABlockOpensUpItsNeighboursToSpace()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Shell(Catalog.LightArmor(), new Vector3I(-1, -1, -1), new Vector3I(2, 2, 2));
            builder.Place(Catalog.Reactor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());
            ThermalNode reactor = simulation.Solver.GetNode(builder.Last);
            Assert.Equal(0, reactor.TotalExposedFaces);

            simulation.RemoveBlock(builder.Grid.GetAtCell(new Vector3I(1, 0, 0)));
            simulation.RebuildAll();

            Assert.True(reactor.TotalExposedFaces > 0);
        }

        [Fact]

        public void UpdateDrivesTheSchedulerAndTheRoomMapper()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(4, 4, 4));

            ThermalSettings settings = new ThermalSettings { Frequency = 4 };
            settings.Derive();

            ThermalSimulation simulation = builder.BuildSimulation(settings);
            long before = simulation.Solver.StepCount;

            for (int frame = 0; frame < 60; frame++)
            {
                simulation.Update(1f / 60f, Worlds.Shadow());
            }

            Assert.Equal(before + 4, simulation.Solver.StepCount);
            Assert.False(simulation.Rooms.HasWorkPending);
        }

        [Fact]

        public void SaveAndLoadRestoresEveryTemperature()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 1, 3));

            Dictionary<int, Vector3I> sinks = new Dictionary<int, Vector3I>();
            PipeFitter.BuildRing(builder, PipeFitter.RectangleXZ(new Vector3I(0, 2, 0), 3, 3), -1, sinks);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 300f);

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                simulation.Solver.Nodes[i].Temperature = 300f + (i * 7.25f);
            }
            simulation.Solver.Loops[0].Temperature = 512.75f;

            string saved = simulation.Save();

            float[] expected = new float[simulation.Solver.Nodes.Count];
            for (int i = 0; i < expected.Length; i++) expected[i] = simulation.Solver.Nodes[i].Temperature;

            simulation.Solver.SetAllTemperatures(1f);
            int restored = simulation.Load(saved);

            Assert.Equal(expected.Length, restored);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], simulation.Solver.Nodes[i].Temperature, 3);
            }
            Assert.Equal(512.75f, simulation.Solver.Loops[0].Temperature, 3);
        }

        [Fact]

        public void LoadingIgnoresBlocksThatAreNoLongerThere()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            builder.Place(Catalog.LightArmor(), new Vector3I(1, 0, 0));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 300f);
            simulation.Solver.Nodes[0].Temperature = 600f;
            simulation.Solver.Nodes[1].Temperature = 700f;

            string saved = simulation.Save();

            simulation.RemoveBlock(builder.Placed[1]);
            simulation.RebuildAll();
            simulation.Solver.SetAllTemperatures(1f);

            int restored = simulation.Load(saved);

            Assert.Equal(1, restored);
            Assert.Equal(600f, simulation.Solver.Nodes[0].Temperature, 3);
        }

        [Fact]

        public void SavingDoesNotDisturbTheRunningSimulation()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.456f);
            float before = simulation.Solver.Nodes[0].Temperature;

            simulation.Save();

            Assert.Equal(before, simulation.Solver.Nodes[0].Temperature, 6);
        }

        [Fact]

        public void AGridWithNoEnvironmentReachesAUniformTemperature()
        {
            ThermalSettings settings = Fixture.ConductionOnly();

            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            simulation.Solver.GetNodeAt(Vector3I.Zero).Temperature = 900f;

            float expected = simulation.Solver.TotalEnergy;
            simulation.StepExact(4000, Worlds.Shadow());

            ThermalNode hottest = simulation.Solver.HottestNode();
            float coldest = float.MaxValue;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                coldest = Math.Min(coldest, simulation.Solver.Nodes[i].Temperature);
            }

            Assert.Equal(hottest.Temperature, coldest, 1);
            Assert.Equal(1f, simulation.Solver.TotalEnergy / expected, 3);
        }

        [Fact]

        public void ReplayingTheSameScenarioGivesIdenticalResults()
        {

            float first = RunSmallScenario();

            float second = RunSmallScenario();

            Assert.Equal(first, second, 6);
        }


        private static float RunSmallScenario()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
            builder.Place(Catalog.Reactor(), new Vector3I(3, 0, 0)).Producing(5e6f);

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();

            for (int i = 0; i < 200; i++)
            {
                simulation.StepExact(1, Worlds.PlanetSurface(0.7f, (i / 200f)));
            }

            return simulation.Solver.HottestNode().Temperature;
        }
    }

    /// <summary>
    /// Every scenario runs, finishes and produces finite numbers.
    ///
    /// <para>
    /// The scenarios are this repository's worked examples and its documentation quotes their figures,
    /// so a scenario that silently stopped exercising what it names would take a page of prose down
    /// with it. ScenarioClaimTests checks the conclusions; this checks they were reached at all.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class ScenarioTests
    {
        [Theory]
        [InlineData("vacuum-soak")]
        [InlineData("reactor")]
        [InlineData("atmosphere")]
        [InlineData("daynight")]
        [InlineData("reentry")]
        [InlineData("coolant")]
        [InlineData("sealed-room")]
        [InlineData("meltdown")]
        [InlineData("radiator")]
        [InlineData("airlock")]
        [InlineData("coolant-failure")]
        [InlineData("welding")]
        [InlineData("stiff")]
        [InlineData("units")]

        public void EveryScenarioRunsAndProducesFiniteResults(string name)
        {
            ScenarioResult result = Scenarios.Run(name);

            Assert.NotNull(result.Summary);
            Assert.NotNull(result.Runner);
            Assert.NotEmpty(result.Runner.Samples);

            foreach (Sample sample in result.Runner.Samples)
            {
                Assert.False(float.IsNaN(sample.HottestTemperature));
                Assert.False(float.IsInfinity(sample.HottestTemperature));
                Assert.True(sample.HottestTemperature >= 0f);
            }
        }

        [Fact]

        public void TheCoolantScenarioActuallyFormsALoop()
        {
            ScenarioResult result = Scenarios.Run("coolant");
            Assert.Single(result.Runner.Simulation.Solver.Loops);
            Assert.DoesNotContain("NO CLOSED LOOP", result.Summary);
        }

        [Fact]

        public void TheSealedRoomScenarioKeepsItsInteriorSealed()
        {
            ScenarioResult result = Scenarios.Run("sealed-room");
            Assert.Contains("Its exposed faces: 0", result.Summary);
        }

        [Fact]

        public void ReentryHeatsTheLeadingFace()
        {
            ScenarioResult result = Scenarios.Run("reentry");

            float start = result.Runner.Samples[0].Tracked["nose"];
            float end = result.Runner.Final.Tracked["nose"];

            Assert.True(end > start + 2f, "the nose should heat up: " + start + " -> " + end);
        }

        [Fact]

        public void DayNightProducesAnOscillation()
        {
            ScenarioResult result = Scenarios.Run("daynight");

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (Sample sample in result.Runner.Samples)
            {
                float value = sample.Tracked["plate"];
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            Assert.True(max - min > 50f, "expected a day/night swing, got " + (max - min) + " K");
        }

        [Fact]

        public void CsvOutputHasOneRowPerSamplePlusAHeader()
        {
            ScenarioResult result = Scenarios.Run("vacuum-soak");
            string[] lines = result.Csv.Trim().Split('\n');

            Assert.Equal(result.Runner.Samples.Count + 1, lines.Length);
            Assert.StartsWith("time_s,", lines[0]);
        }
    }
}

using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>One-block numeric boundaries keep inactive radiation finite and cap demand before integer conversion.</summary>
    public class ThermalNumericBoundaryTests
    {
        private static ThermalSimulation NonRadiatingBlock(bool clamp)
        {
            var settings = new ThermalSettings
            {
                EnableEnvironment = true, EnableRadiation = true, EnableConvection = false,
                EnableConduction = false, EnableSolarHeat = false, EnableHeatSources = false,
                EnableWasteHeat = false, EnableDamage = false, EnableCoolantLoops = false,
                EnableRoomAir = false, EnableHeatPumps = false, ClampEnvironmentOvershoot = clamp,
                HeatTimeScale = 1f, MaxSubsteps = 3
            };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var model = BlockModel.Solid("zero-emissivity", Vector3I.One, 10f,
                new BlockThermalProperties { SpecificHeat = 1000f, Emissivity = 0f });
            simulation.AddBlock(new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            simulation.Solver.CollectDiagnostics = true;
            return simulation;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AcceptedHighTemperatureWithZeroEmissivityHasNoRadiationOrNonfiniteDiagnostics(bool clamp)
        {
            var simulation = NonRadiatingBlock(clamp);
            var solver = simulation.Solver;
            var node = Assert.Single(solver.Nodes);
            float accepted;
            Assert.True(ThermalApiInputs.TryTemperature(1e10f, out accepted));
            Assert.True(float.IsInfinity((accepted * accepted) * (accepted * accepted)));
            Assert.True(node.TotalExposedFaces > 0);
            Assert.Equal(0f, node.RadiationCoefficient);
            node.Temperature = accepted;
            var environment = EnvironmentSolver.Solve(simulation.Settings, simulation.Planet,
                EnvironmentSample.DarkVacuum());

            solver.Step(.1f, environment);

            Assert.Equal(1, solver.LastSubsteps);
            Assert.Equal(accepted, node.Temperature);
            Assert.Equal(0f, node.LastDeltaTemperature);
            Assert.Equal(0f, node.LastRadiationWatts);
            Assert.Equal(0f, node.LastConvectionWatts);
            Assert.Equal(0f, solver.LastEnvironmentWatts);
        }

        [Fact]
        public void DirectTemperatureImportsIgnoreNonfiniteRecordsAndPreserveFiniteFloorAndQuantum()
        {
            var simulation = NonRadiatingBlock(false);
            var node = Assert.Single(simulation.Solver.Nodes);
            var position = node.Block.Position;
            var invalid = new[]
            {
                new StoredTemperature(position, float.NaN),
                new StoredTemperature(position, float.PositiveInfinity),
                new StoredTemperature(position, float.NegativeInfinity)
            };
            Assert.Equal(0, simulation.ImportHotTail(invalid));
            Assert.Equal(300f, node.Temperature);

            var mixed = new[]
            {
                invalid[0], new StoredTemperature(position, 500f), invalid[1], invalid[2],
                new StoredTemperature(position, 500f + HotTailCodec.TemperatureStep * .5f)
            };
            Assert.Equal(1, simulation.ImportHotTail(mixed));
            Assert.Equal(500f, node.Temperature);
            Assert.Equal(1, simulation.ImportHotTail(new[] { new StoredTemperature(position, -10f) }));
            Assert.Equal(ThermalConstants.MinimumTemperature, node.Temperature);
        }

        [Fact]
        public void ExtremeDemandIsCappedBeforeTheProductionStepUsesAnIntegerCount()
        {
            var simulation = NonRadiatingBlock(true);
            var solver = simulation.Solver;
            var environment = EnvironmentSolver.Solve(simulation.Settings, simulation.Planet,
                EnvironmentSample.DarkVacuum());
            foreach (float demand in new[] { float.MaxValue, float.PositiveInfinity })
            {
                solver.PrepareForSteps();
                Assert.True(solver.BeginStep(.1f, environment, demand));
                Assert.Equal(3, solver.LastSubsteps);
                Assert.True(solver.LastStepWasClamped);
                Assert.True(solver.StepWorkRemaining > 0);
                Assert.True(solver.AdvanceStep(long.MaxValue));
                Assert.Equal(300f, solver.Nodes[0].Temperature);
            }
        }
    }
}

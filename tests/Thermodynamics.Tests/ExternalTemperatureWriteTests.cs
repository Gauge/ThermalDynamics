using Thermodynamics.Core;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>A one-block cooling step must preserve the temperature floor after an interleaved API write.</summary>
    public class ExternalTemperatureWriteTests
    {
        [Fact]
        public void PendingRadiativeCoolingCannotPublishBelowAnExternalWritesTemperatureFloor()
        {
            var settings = new ThermalSettings
            {
                EnableEnvironment = true, EnableRadiation = true, EnableConvection = false,
                EnableConduction = false, EnableSolarHeat = false, EnableHeatSources = false,
                EnableWasteHeat = false, EnableDamage = false, EnableCoolantLoops = false,
                EnableRoomAir = false, EnableHeatPumps = false, HeatTimeScale = 1f, MaxSubsteps = 1
            };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var model = BlockModel.Solid("radiating", Vector3I.One, 10f,
                new BlockThermalProperties { SpecificHeat = 1000f, Emissivity = 1f });
            simulation.AddBlock(new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity), 300f);
            simulation.RebuildAll();
            var solver = simulation.Solver;
            solver.CollectDiagnostics = true;
            var node = Assert.Single(solver.Nodes);
            var environment = EnvironmentSolver.Solve(settings, simulation.Planet, EnvironmentSample.DarkVacuum());

            Assert.True(solver.BeginStep(.1f, environment));
            Assert.False(solver.AdvanceStep(1));
            Assert.True(node.LastRadiationWatts < 0f);
            Assert.Equal(300f, node.Temperature);

            float accepted;
            Assert.True(ThermalApiInputs.TryTemperature(-1f, out accepted));
            node.Temperature = accepted; // Same accepted write as SetBlockTemperature, between slices.
            Assert.True(solver.AdvanceStep(long.MaxValue));
            Assert.True(node.LastDeltaTemperature < 0f);
            Assert.Equal(ThermalConstants.MinimumTemperature, node.Temperature);
        }
    }
}

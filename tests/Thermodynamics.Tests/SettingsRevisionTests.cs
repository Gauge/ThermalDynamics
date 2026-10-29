using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// One block, one parcel and one air node hold revision cancellation, consistent heat-clock
    /// rescaling and the host's skipped-sample boundary. No large room map or long run is needed.
    /// </summary>
    public class SettingsRevisionTests
    {
        [Fact]
        public void RevisionsCancelBeforeRescalingAndWaitForAFreshSample()
        {
            var settings = new ThermalSettings { EnableEnvironment = true, EnableRadiation = false,
                EnableConvection = false, EnableConduction = false, EnableSolarHeat = false,
                EnableWasteHeat = false, EnableDamage = false, HeatTimeScale = 1f,
                MaxSubsteps = 2, MaxSubstepsPerBlock = 0, MaxElementVisitsPerStep = 0 };
            settings.Derive();
            var simulation = new ThermalSimulation(settings, new GridModel(2.5f));
            var model = BlockModel.Solid("clock", Vector3I.One, 1f,
                new BlockThermalProperties { SpecificHeat = 100f, Conductivity = 0f, Emissivity = 0f });
            var block = new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity);
            ThermalNode node = simulation.AddBlock(block, 400f);
            simulation.RebuildAll();
            // This ordinary refresh previously set appliedOnce in the wrong method, making
            // later revisions behave differently from those on a never-refreshed grid.
            simulation.RefreshBlock(block);
            settings.Derive();
            simulation.Update(0f, EnvironmentSample.DarkVacuum());
            var solver = simulation.Solver;
            var loop = new CoolantLoop(LoopThermalProperties.Default(), 400f);
            loop.Pipes.Add(block);
            loop.RefreshSignature();
            solver.SetLoops(new List<CoolantLoop> { loop });
            loop.FillFraction = .5f;
            loop.Links.Clear();
            var air = new RoomAirNode { Temperature = 400f, Volume = 1f, Pressure = 1f,
                AirDensity = settings.RoomAirDensity, HeatTimeScale = settings.HeatTimeScale };
            solver.RoomAir.Add(air);
            solver.PrepareForSteps();
            float nodeMass = node.ThermalMass;
            float loopMass = loop.ThermalMass;
            float airMass = air.ThermalMass;

            var oldEnvironment = EnvironmentState.Vacuum(300f);
            oldEnvironment.SunDirectionLocal = Vector3.UnitY;
            oldEnvironment.HeatSources = new[] { new HeatSourceState(Vector3.Up, 16f) };
            oldEnvironment.HeatSourceCount = 1;
            Assert.True(solver.BeginStep(.5f, oldEnvironment, 2f));
            Assert.False(solver.AdvanceStep(1));
            Assert.False(simulation.NeedsEnvironmentSample);
            settings.EnableConduction = true;
            settings.EnableRadiation = true;
            settings.EnableHeatSources = false;
            settings.HeatTimeScale = 2f;
            settings.Derive();
            simulation.Update(settings.StepSeconds, default(EnvironmentSample), false);

            Assert.False(solver.StepInFlight);
            Assert.True(simulation.NeedsEnvironmentSample);
            Assert.Equal(0L, solver.StepCount);
            Assert.Equal(0L, simulation.StepsCompleted);
            Assert.Equal(400f, node.Temperature);
            Assert.Equal(400f, loop.Temperature);
            Assert.Equal(400f, air.Temperature);
            Assert.Same(loop, Assert.Single(solver.Loops));
            Assert.Same(air, Assert.Single(solver.RoomAir));
            Assert.Equal(2f, node.HeatTimeScale);
            Assert.Equal(2f, loop.HeatTimeScale);
            Assert.Equal(2f, air.HeatTimeScale);
            Assert.Equal(nodeMass / 2f, node.ThermalMass);
            Assert.Equal(loopMass / 2f, loop.ThermalMass);
            Assert.Equal(airMass / 2f, air.ThermalMass);
            Assert.Equal(Vector3.UnitY, solver.Environment.SunDirectionLocal);

            var fresh = EnvironmentSample.DarkVacuum();
            fresh.SunDirectionLocal = Vector3.UnitZ;
            fresh.RelativeWindDirectionLocal = Vector3.UnitX;
            fresh.RelativeWindSpeed = 29f;
            simulation.Update(.0001f, fresh, true);
            Assert.True(solver.StepInFlight);
            Assert.Equal(Vector3.UnitZ, solver.Environment.SunDirectionLocal);
            Assert.Equal(Vector3.UnitX, solver.Environment.WindDirectionLocal);
            Assert.Equal(29f, solver.Environment.WindSpeed);
            Assert.Equal(0, solver.Environment.HeatSourceCount);
            // Missing a new sample must still allow work on the captured environment.
            simulation.Update(settings.StepSeconds, default(EnvironmentSample), false);
            Assert.False(solver.StepInFlight);
            Assert.Equal(1L, solver.StepCount);
            Assert.Equal(1L, simulation.StepsCompleted);
            Assert.Equal(400f, node.Temperature);
            Assert.Equal(Vector3.UnitZ, solver.Environment.SunDirectionLocal);

            // Topology can cancel after PrepareTick skipped sampling too. The same gate
            // must defer that replacement, and the original two-argument API stays valid.
            Assert.True(solver.BeginStep(.5f, solver.Environment, 2f));
            Assert.False(solver.AdvanceStep(1));
            simulation.MarkLayoutDirty();
            simulation.Update(settings.StepSeconds, default(EnvironmentSample), false);
            Assert.False(solver.StepInFlight);
            Assert.Equal(1L, solver.StepCount);
            Assert.Equal(1L, simulation.StepsCompleted);
            Assert.True(simulation.NeedsEnvironmentSample);
            fresh.SunDirectionLocal = Vector3.UnitX;
            simulation.Update(settings.StepSeconds, fresh);
            Assert.Equal(2L, solver.StepCount);
            Assert.Equal(2L, simulation.StepsCompleted);
            Assert.Equal(Vector3.UnitX, solver.Environment.SunDirectionLocal);

            // A full rebuild must not mark the revision applied while leaving old masses.
            float beforeRebuildMass = node.ThermalMass;
            settings.HeatTimeScale = 4f;
            settings.Derive();
            simulation.RebuildAll();
            Assert.Equal(4f, node.HeatTimeScale);
            Assert.Equal(beforeRebuildMass / 2f, node.ThermalMass);
        }
    }
}

using Thermodynamics.Fixtures;
using Aerodynamics.Core;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Physical leading/trailing-face controls: flow travels downstream, while exposed-face rays look upstream.</summary>
    public class AirflowDirectionTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void HeatingHitsTheUpstreamBlockForFlightAndWindInAnyGridFrame(bool ambientWind, bool rotated)
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                EnableEnvironment = false, EnableSolarHeat = false, EnableConduction = false,
                EnableDamage = false, EnableRecoveryHeating = false
            };
            settings.Derive();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), Vector3I.UnitX);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.Solver.CollectDiagnostics = true;
            Matrix orientation = rotated ? Matrix.CreateRotationY(0.7f) : Matrix.Identity;
            Vector3 heading = Vector3.TransformNormal(Vector3.Right, orientation);
            EnvironmentSample sample = Worlds.Flight(1f, 100f);
            sample.GridVelocity = ambientWind ? Vector3.Zero : heading * 100f;
            sample.ComposeRelativeWind(ambientWind ? heading : Vector3.Zero, ambientWind ? 100f : 0f, Matrix.Transpose(orientation));
            simulation.StepExact(1, sample);
            int upstream = ambientWind ? 0 : 1;
            float heated = 0f, lee = 0f;
            foreach (ThermalNode node in simulation.Solver.Nodes)
            {
                if (node.Block.Min.X == upstream) heated += node.LastFrictionWatts;
                else lee += node.LastFrictionWatts;
            }
            Assert.True(heated > 100f, "Air must strike the upstream exposed face.");
            Assert.InRange(lee, 0f, 0.01f);
            AeroProfile profile = AeroProfile.Build(simulation.Grid, 26);
            Vector3 air = ambientWind ? heading * 100f : -heading * 100f;
            AeroWrench force = AeroForces.Evaluate(profile, orientation, air, Vector3.Zero, 1.225f, profile.Mass, 1f / 60f);
            Assert.True(Vector3.Dot(force.Force, air) > 0f, "Drag follows the relative air velocity.");
            if (!ambientWind) Assert.True(Vector3.Dot(force.Force, sample.GridVelocity) < 0f, "Drag must remove translational energy in still air.");
        }

        [Fact]
        public void ShieldingLooksTowardTheApproachingAir()
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                EnableEnvironment = false, EnableSolarHeat = false, EnableConduction = false,
                EnableDamage = false, EnableRecoveryHeating = false, EnableWindwardShielding = true
            };
            settings.Derive();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);
            builder.Place(Catalog.HeavyArmor(), new Vector3I(3, 0, 0));
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.Solver.CollectDiagnostics = true;
            EnvironmentSample sample = Worlds.Flight(1f, 100f);
            sample.GridVelocity = Vector3.Right * 100f;
            sample.ComposeRelativeWind(Vector3.Zero, 0f, Matrix.Identity);
            simulation.StepExact(12, sample);
            foreach (ThermalNode node in simulation.Solver.Nodes)
            {
                if (node.Block.Min.X == 3) Assert.True(node.LastFrictionWatts > 100f);
                else Assert.Equal(0f, node.LastFrictionWatts);
            }
        }
    }
}

using Thermodynamics.Fixtures;
using Aerodynamics.Core;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Independent geometry, symmetry, energy and flight-frame controls for the coupled lifting model.</summary>
    [Trait("scope", "integration")]
    public class AeroLiftTests
    {
        private static AeroLiftingSurface Plate()
        {
            return new AeroLiftingSurface { Area = 20f, Normal = Vector3.Up, Centre = Vector3.Zero, Size = new Vector3(10f, 0f, 2f) };
        }
        private static Vector3 Air(float degrees, float speed = 50f)
        {
            double a = degrees * Math.PI / 180d;
            return new Vector3(0f, (float)Math.Sin(a), (float)Math.Cos(a)) * speed;
        }
        [Fact]
        public void ConnectedPlateGeometryHasExactAreaAndSpanAndCubeHasNoThinRegion()
        {
            AeroProfile plate = AeroProfile.Build(AeroCalibrationLab.Box(8, 1, 4), 26);
            Assert.Equal(1, plate.LiftingSurfaceCount);
            AeroLiftingSurface surface = plate.LiftingSurface(0);
            Assert.Equal(200f, surface.Area);
            Assert.Equal(20f, surface.Span(Vector3.Backward));
            Assert.Equal(0, AeroProfile.Build(AeroCalibrationLab.Box(4, 4, 4), 26).LiftingSurfaceCount);
        }
        [Theory]
        [InlineData(0f)]
        [InlineData(90f)]
        [InlineData(-90f)]
        public void AlignedAndBroadsidePlatesHaveNoLift(float angle)
        {
            AeroStallState state = new AeroStallState();
            Vector3 lift, drag;
            AeroLift.Evaluate(Plate(), Air(angle), 1.225f, float.PositiveInfinity, 10f, ref state, out lift, out drag);
            Assert.InRange(lift.Length(), 0f, 0.01f);
            if (angle != 0f) Assert.InRange(drag.Length(), 35831f, 35832f); // .5 * 1.225 * 50² * 20 * 1.17
        }
        [Fact]
        public void LiftIsTransverseOddInIncidenceAndDragIsDissipative()
        {
            foreach (float angle in new[] { 2f, 10f, 30f, 60f, 85f })
            {
                AeroStallState a = new AeroStallState(), b = new AeroStallState();
                Vector3 lift, drag, reverseLift, reverseDrag;
                AeroLift.Evaluate(Plate(), Air(angle), 1f, float.PositiveInfinity, 10f, ref a, out lift, out drag);
                AeroLift.Evaluate(Plate(), Air(-angle), 1f, float.PositiveInfinity, 10f, ref b, out reverseLift, out reverseDrag);
                Assert.InRange(Math.Abs(Vector3.Dot(lift, Vector3.Normalize(Air(angle)))), 0f, 0.01f);
                Assert.True(Vector3.Dot(drag, Air(angle)) >= 0f);
                Assert.Equal(lift.Length(), reverseLift.Length(), 2);
                Assert.Equal(drag.Length(), reverseDrag.Length(), 2);
                Assert.Equal(lift.Y, -reverseLift.Y, 2);
            }
        }
        [Fact]
        public void AttachedForcesScaleWithDensityAndSpeedSquared()
        {
            AeroStallState state = new AeroStallState();
            Vector3 lift, drag, fasterLift, fasterDrag;
            AeroLift.Evaluate(Plate(), Air(5), 1f, float.PositiveInfinity, 1f, ref state, out lift, out drag);
            AeroLift.Evaluate(Plate(), Air(5, 100), 0.5f, float.PositiveInfinity, 1f, ref state, out fasterLift, out fasterDrag);
            Assert.InRange((fasterLift - 2f * lift).Length(), 0f, 0.01f);
            Assert.InRange((fasterDrag - 2f * drag).Length(), 0f, 0.01f);
        }
        [Fact]
        public void GroundEffectReducesInducedDragAndRaisesAttachedLift()
        {
            Assert.Equal(0f, AeroLift.GroundFactor(0, 10));
            Assert.Equal(1f, AeroLift.GroundFactor(float.PositiveInfinity, 10));
            AeroStallState state = new AeroStallState();
            Vector3 highLift, highDrag, lowLift, lowDrag;
            AeroLift.Evaluate(Plate(), Air(5), 1f, float.PositiveInfinity, 1f, ref state, out highLift, out highDrag);
            AeroLift.Evaluate(Plate(), Air(5), 1f, 0.1f, 1f, ref state, out lowLift, out lowDrag);
            Assert.True(lowLift.Length() > highLift.Length());
            Assert.True(lowDrag.Length() < highDrag.Length());
        }
        [Fact]
        public void DescendingIncidenceRetainsSeparationUntilRecovery()
        {
            AeroStallState up = new AeroStallState(), down = new AeroStallState();
            Vector3 lift, drag;
            AeroLift.Evaluate(Plate(), Air(30), 1f, 1000f, 10f, ref down, out lift, out drag);
            AeroLift.Evaluate(Plate(), Air(14), 1f, 1000f, 10f, ref up, out lift, out drag);
            Assert.False(up.Separated);
            AeroLift.Evaluate(Plate(), Air(14), 1f, 1000f, 10f, ref down, out lift, out drag);
            Assert.True(down.Separated);
            AeroLift.Evaluate(Plate(), Air(2), 1f, 1000f, 10f, ref down, out lift, out drag);
            Assert.False(down.Separated);
        }
        [Fact]
        public void CoupledFlightIsCovariantAndDisableRestoresDragControl()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(8, 1, 4), 26);
            AeroFlightModel flight = new AeroFlightModel(profile);
            Vector3 air = Air(5);
            AeroWrench expected = AeroForces.Evaluate(profile, Matrix.Identity, air, Vector3.Zero, 1.225f, profile.Mass, 1f / 60f);
            AeroWrench disabled = flight.Evaluate(Matrix.Identity, air, Vector3.Zero, 1.225f, profile.Mass, 1f / 60f, false, false, 100f, Vector3.Up, 288f);
            Assert.Equal(expected.Force, disabled.Force);
            AeroWrench one = flight.Evaluate(Matrix.Identity, air, Vector3.Zero, 1.225f, profile.Mass, 1f / 60f, true, false, 100f, Vector3.Up, 288f);
            Matrix rotation = Matrix.CreateRotationZ(0.4f);
            AeroWrench two = flight.Evaluate(rotation, Vector3.TransformNormal(air, rotation), Vector3.Zero, 1.225f, profile.Mass, 1f / 60f, true, false, 100f, Vector3.Up, 288f);
            Assert.True(one.Lift.Length() > 100f);
            Assert.InRange((two.Force - Vector3.TransformNormal(one.Force, rotation)).Length(), 0f, 0.2f);
            Assert.InRange((two.Restoring - Vector3.TransformNormal(one.Restoring, rotation)).Length(), 0f, 2f);
        }
        [Fact]
        public void RecoveryHeatingCannotHeatAnAlreadyHotSurfaceOrOvershoot()
        {
            float limit = 288f + AeroHeating.RecoveryRise(100f);
            Assert.InRange(limit, 292.42f, 292.44f);
            Assert.Equal(0f, AeroHeating.BoundedWatts(1e6f, 288f, 100f, 300f, 100f, 1f));
            float watts = AeroHeating.BoundedWatts(1e6f, 288f, 100f, 280f, 100f, 10f);
            Assert.InRange(280f + watts * 10f / 100f, 280f, limit);
        }
        [Fact]
        public void ASpinningPlateLosesRotationalEnergyWithLiftEnabled()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(8, 1, 4), 26);
            AeroFlightModel flight = new AeroFlightModel(profile);
            Vector3 spin = new Vector3(0f, 0f, 0.2f);
            AeroWrench result = flight.Evaluate(Matrix.Identity, Vector3.Zero, spin, 1.225f, profile.Mass,
                1f / 60f, true, false, 100f, Vector3.Up, 288f);
            Assert.True(Vector3.Dot(result.Restoring + result.Damping, spin) < 0f);
        }

        [Fact]
        public void AuthoredMeshHasFiniteReverseFlowAndAUsefulGlideRatio()
        {
            GridModel grid = new GridModel(2.5f);
            BlockInstance block = grid.Add(new BlockModel { Mass = 100f }, Vector3I.Zero);
            AeroGrid detached = grid;
            detached.Blocks[0].Lift = new AeroLiftingSurface { Normal = Vector3.Up, SpanAxis = Vector3.Right, Area = 20f, SpanMetres = 10f, ChordMetres = 2f };
            AeroProfile profile = AeroProfile.Build(detached, 26);
            Assert.Equal(1, profile.LiftingSurfaceCount);
            AeroFlightModel flight = new AeroFlightModel(profile);
            foreach (float sign in new[] { -1f, 1f })
            {
                AeroWrench result = flight.Evaluate(Matrix.Identity, Air(5) * sign, Vector3.Zero, 1.225f, 100f,
                    1f / 60f, true, false, 100f, Vector3.Up, 288f);
                Assert.True(result.Force.IsValid());
                Assert.InRange(result.Lift.Length() / result.Drag.Length(), 3f, 30f);
                Assert.True(Vector3.Dot(result.Drag, Air(5) * sign) > 0f);
            }
        }

        [Fact]
        public void AnAuthoredBroadsidePlatePaysItsPressureDragOnce()
        {
            GridModel grid = new GridModel(2.5f);
            BlockInstance block = grid.Add(new BlockModel { Mass = 1000f }, Vector3I.Zero);
            AeroGrid detached = grid;
            detached.Blocks[0].Lift = new AeroLiftingSurface { Normal = Vector3.Up, SpanAxis = Vector3.Right, Area = 20f, SpanMetres = 10f, ChordMetres = 2f };
            AeroProfile profile = AeroProfile.Build(detached, 26);
            AeroWrench result = new AeroFlightModel(profile).Evaluate(Matrix.Identity, Vector3.Up * 50f, Vector3.Zero,
                1.225f, 1000f, 1f / 60f, true, false, 100f, Vector3.Up, 288f);
            // Normal pressure coefficient 1.17 plus small skin term; a duplicate broad-face force misses this band.
            Assert.InRange(result.Drag.Length() / (0.5f * 1.225f * 2500f * 20f), 1.17f, 1.19f);
        }

        [Fact]
        public void CharacterFallConvergesToTheIndependentQuadraticTerminalSpeed()
        {
            const float mass = 120f, density = 1.225f, gravity = 9.81f;
            float expected = (float)Math.Sqrt(2f * mass * gravity / (density * 0.7f));
            float speed = 0f;
            for (int i = 0; i < 12000; i++)
            {
                Vector3 force = AeroCharacter.Force(Vector3.Up * speed, density, mass, 1f / 120f);
                speed += (gravity - force.Y / mass) / 120f;
            }
            Assert.InRange(speed, 50f, 60f);
            Assert.InRange(Math.Abs(speed - expected), 0f, 0.02f);
        }

        [Fact]
        public void SettledFlightEvaluationAllocatesNothing()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(8, 1, 4), 26);
            AeroFlightModel flight = new AeroFlightModel(profile);
            flight.Evaluate(Matrix.Identity, Air(5), Vector3.Zero, 1f, profile.Mass, 1f / 60f, true, true, 2f, Vector3.Up, 288f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
                flight.Evaluate(Matrix.Identity, Air(5), Vector3.Zero, 1f, profile.Mass, 1f / 60f, true, true, 2f, Vector3.Up, 288f);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void SeparationRelaxationIsInvariantUnderTimestepSubdivision()
        {
            AeroStallState a = new AeroStallState(), b = new AeroStallState();
            Vector3 lift, drag;
            for (int i = 0; i < 60; i++) AeroLift.Evaluate(Plate(), Air(30), 1f, 1000f, 1f / 60f, ref a, out lift, out drag);
            for (int i = 0; i < 120; i++) AeroLift.Evaluate(Plate(), Air(30), 1f, 1000f, 1f / 120f, ref b, out lift, out drag);
            Assert.InRange(Math.Abs(a.Separation - b.Separation), 0f, 1e-5f);
        }

        [Fact]
        public void RecoveryDefaultReachesTheSolverAndPublishesTheActualHeat()
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                EnableEnvironment = false, EnableSolarHeat = false, EnableConduction = false,
                EnableWasteHeat = false, EnableHeatSources = false, EnableDamage = false, FrictionScale = 100f
            };
            settings.Derive();
            EnvironmentSample sample = Worlds.Flight(1f, 100f);
            PlanetThermalProperties planet = PlanetThermalProperties.Default();
            EnvironmentState environment = EnvironmentSolver.Solve(settings, planet, sample);
            float recovery = environment.AmbientTemperature + AeroHeating.RecoveryRise(environment.WindSpeed);
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, environment.AmbientTemperature);
            simulation.Planet = planet;
            simulation.Solver.CollectDiagnostics = true;
            simulation.StepExact(1, sample);
            ThermalNode node = simulation.Solver.Nodes[0];
            Assert.InRange(node.Temperature, environment.AmbientTemperature, recovery + 0.001f);
            Assert.True(simulation.FrictionWatts > 0f);
            Assert.Equal(node.LastFrictionWatts, simulation.FrictionWatts, 2);
            node.Temperature = recovery + 20f;
            simulation.StepExact(1, sample);
            Assert.Equal(0f, simulation.FrictionWatts);
            Assert.Equal(recovery + 20f, node.Temperature, 3);
        }

        [Fact]
        public void GameplayHeatingIsIndependentOfTheLegacyPotentialScale()
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings
            {
                EnableEnvironment = false, EnableSolarHeat = false, EnableConduction = false,
                EnableWasteHeat = false, EnableHeatSources = false, EnableDamage = false,
                FrictionScale = 0f, EnableReentryHeating = true, ReentryStartSpeed = 200f, ReentryHeatingScale = 0.001f
            };
            settings.Derive();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            simulation.StepExact(1, Worlds.Flight(1f, 300f));
            Assert.True(simulation.FrictionWatts > 0f);
            settings.EnableReentryHeating = false;
            settings.Derive();
            simulation.StepExact(1, Worlds.Flight(1f, 300f));
            Assert.Equal(0f, simulation.FrictionWatts);
        }

        [Fact]
        public void DisconnectedWingsDoNotAcquireAnInventedSpanAcrossTheirGap()
        {
            GridModel grid = new GridModel(1f);
            for (int side = 0; side < 2; side++)
                for (int x = 0; x < 4; x++)
                    for (int z = 0; z < 2; z++)
                        grid.Add(new BlockModel(), new Vector3I(x + side * 20, 0, z));
            AeroProfile profile = AeroProfile.Build(grid, 26);
            Assert.Equal(2, profile.LiftingSurfaceCount);
            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(8f, profile.LiftingSurface(i).Area);
                Assert.Equal(4f, profile.LiftingSurface(i).Span(Vector3.Backward));
            }
        }

        [Fact]
        public void ASlenderBodyHasLowAspectLiftingRegionsWithoutPretendingItIsAThinWing()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(3, 3, 12), 26);
            Assert.Equal(2, profile.LiftingSurfaceCount);
            for (int i = 0; i < 2; i++)
            {
                AeroLiftingSurface surface = profile.LiftingSurface(i);
                float span = surface.Span(Vector3.Backward);
                Assert.InRange(span * span / surface.Area, 0.24f, 0.26f);
            }
        }

    }
}

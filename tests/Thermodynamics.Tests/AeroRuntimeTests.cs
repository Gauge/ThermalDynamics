using Aerodynamics.Core;
using System;
using System.IO;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Profile lifetime, bounded preparation, force-coordinate covariance and numerical units at the engine boundary.</summary>
    [Trait("scope", "integration")]
    public class AeroRuntimeTests
    {
        [Theory]
        [InlineData(26)]
        [InlineData(98)]
        public void RuntimeReplacesGeometryResolutionAndMassWithoutDroppingTheActiveProfile(int directions)
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Vector3 centre = new Vector3(2.5f);
            Finish(runtime, grid, directions, 0, centre);
            AeroProfile first = runtime.Profile;
            Assert.Equal(directions, first.Directions);
            Assert.True(runtime.Prepare(grid, true, directions, 0, centre, 0, 0));
            Assert.Same(first, runtime.Profile);
            Assert.Equal(0, runtime.BuildWork);
            Assert.Equal(0, runtime.CentreWork);
            grid.Add(new BlockModel { Mass = 10f }, new Vector3I(4, 0, 0));
            Assert.True(runtime.Prepare(grid, true, directions, 0, centre, 1, 1));
            Assert.True(runtime.Rebuilding);
            Assert.Same(first, runtime.Profile);
            Assert.Equal(1, runtime.BuildWork);
            Finish(runtime, grid, directions, 0, centre);
            Assert.NotSame(first, runtime.Profile);
            Assert.True(runtime.Prepare(grid, true, directions, 1, centre, 0, 0));
            Assert.True(runtime.Rebuilding);
            Assert.Equal(0, runtime.ProfileRevision);
            Finish(runtime, grid, directions, 1, centre);
            int other = directions == 26 ? 98 : 26;
            Assert.True(runtime.Prepare(grid, true, other, 1, centre, 0, 0));
            Assert.True(runtime.Rebuilding);
            Assert.Equal(directions, runtime.Profile.Directions);
            Finish(runtime, grid, other, 1, centre);
            Assert.Equal(other, runtime.Profile.Directions);
        }

        [Fact]
        public void OffClearsStateAndRepeatedDisabledFramesAllocateNothing()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Finish(runtime, grid, 26, 0, Vector3.Zero);
            runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 100, 100);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) runtime.Prepare(grid, false, 26, 0, Vector3.Zero, 100, 100);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.Null(runtime.Profile);
            Assert.False(runtime.Ready);
            Assert.Equal(0, runtime.BuildWork);
            Assert.Equal(0, runtime.CentreWork);
            Finish(runtime, grid, 26, 0, Vector3.Zero);
            Assert.True(runtime.Ready);
            var active = runtime.Profile;
            Assert.True(runtime.Prepare(null, true, 26, 0, Vector3.Zero, 100, 100));
            Assert.Same(active, runtime.Profile);
            Assert.True(runtime.Rebuilding);
            runtime.Reset();
            Assert.Null(runtime.Profile);
        }

        [Fact]
        public void CentreChangesKeepTheActiveCacheUntilTheirOwnBudgetFinishes()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Vector3 original = new Vector3(2.5f);
            Finish(runtime, grid, 26, 0, original);
            Assert.True(runtime.Prepare(grid, true, 26, 0, original + Vector3.One, 0, 7));
            Assert.True(runtime.Ready);
            Assert.True(runtime.Rebuilding);
            Assert.Equal(original, runtime.Profile.CentreOfMass);
            Assert.Equal(7, runtime.CentreWork);
            Assert.True(runtime.Prepare(grid, true, 26, 0, original + Vector3.One, 0, 47));
            Assert.Equal(original + Vector3.One, runtime.Profile.CentreOfMass);
        }

        [Fact]
        public void SubMillimetreCentreNoiseCannotStarvePreparation()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Vector3 original = new Vector3(2.5f);
            Finish(runtime, grid, 98, 0, original);
            Vector3 anchor = original + Vector3.One + new Vector3(0.00001f, 0f, 0f);
            Vector3 target = original;
            bool ready = false;
            int faces = 0;
            for (int i = 0; i < 8; i++)
            {
                target = original + Vector3.One + new Vector3(i % 2 == 0 ? 0.00001f : -0.00001f, 0f, 0f);
                ready = runtime.Prepare(grid, true, 98, 0, target, 0, 7);
                faces += runtime.CentreWork;
            }
            Assert.True(ready);
            Assert.Equal(54, faces);
            Assert.Equal(target, runtime.Profile.CentreOfMass);
            LegacyAeroProfile legacy = LegacyAeroProfile.Build(grid, 98);
            legacy.SetCentreOfMass(anchor);
            legacy.Damping(Vector3.One, 1f);
            legacy.SetCentreOfMass(target);
            Assert.Equal(legacy.Damping(Vector3.One, 1f), runtime.Profile.Damping(Vector3.One, 1f));
        }

        [Fact]
        public void SettledPreparationAndWorldEvaluationAllocateNothing()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Vector3 centre = new Vector3(2.5f);
            Finish(runtime, grid, 98, 0, centre);
            AeroForces.Evaluate(runtime.Profile, Matrix.Identity, Vector3.Backward * 50f, Vector3.One,
                1.225f, runtime.Profile.Mass, 1f / 60f);
            float sum = 0f;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                runtime.Prepare(grid, true, 98, 0, centre, 4096, 256);
                sum += AeroForces.Evaluate(runtime.Profile, Matrix.Identity, Vector3.Backward * 50f, Vector3.One,
                    1.225f, runtime.Profile.Mass, 1f / 60f).Force.Z;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.True(sum > 0f);
        }

        [Fact]
        public void WorldForcesRotateWithTheHullAndDensityEntersLinearly()
        {
            AeroProfile profile = AeroProfile.Build(AeroFlightLab.Shuttlecock(), 98);
            Vector3 air = new Vector3(-12, 3, -50), spin = new Vector3(0.1f, -0.2f, 0.3f);
            AeroWrench original = AeroForces.Evaluate(profile, Matrix.Identity, air, spin, 1.225f, profile.Mass, 1f / 60f);
            Matrix rotate = Matrix.Identity;
            rotate.M11 = 0; rotate.M12 = 1; rotate.M21 = -1; rotate.M22 = 0;
            AeroWrench rotated = AeroForces.Evaluate(profile, rotate, Turn(air), Turn(spin), 1.225f, profile.Mass, 1f / 60f);
            Assert.Equal(Turn(original.Force), rotated.Force);
            Assert.Equal(Turn(original.Restoring), rotated.Restoring);
            Assert.Equal(Turn(original.Damping), rotated.Damping);
            Assert.True(original.Force.LengthSquared() > 0f && original.Restoring.LengthSquared() > 0f
                && original.Damping.LengthSquared() > 0f);
            Assert.False(original.Guarded);
            AeroWrench half = AeroForces.Evaluate(profile, Matrix.Identity, air, spin, 1.225f / 2f, profile.Mass, 1f / 60f);
            Assert.Equal(original.Force / 2f, half.Force);
            Assert.Equal(original.Restoring / 2f, half.Restoring);
            Assert.Equal(original.Damping / 2f, half.Damping);
        }

        [Fact]
        public void ImpulseGuardUsesSecondsAndVacuumProducesNoWrench()
        {
            AeroProfile profile = AeroProfile.Build(AeroCalibrationLab.Box(3, 3, 3), 26);
            float seconds = 1f / 60f, mass = 0.001f, speed = 20f;
            AeroWrench force = AeroForces.Evaluate(profile, Matrix.Identity, Vector3.UnitX * speed,
                Vector3.Zero, 1.225f, mass, seconds);
            Assert.True(force.Guarded);
            Assert.Equal(mass * speed, force.Force.Length() * seconds, 6);
            AeroWrench vacuum = AeroForces.Evaluate(profile, Matrix.Identity, Vector3.UnitX * speed,
                Vector3.One, 0f, mass, seconds);
            Assert.Equal(Vector3.Zero, vacuum.Force);
            Assert.Equal(Vector3.Zero, vacuum.Restoring);
            Assert.Equal(Vector3.Zero, vacuum.Damping);
            Assert.Throws<ArgumentException>(() => AeroForces.Evaluate(profile, Matrix.Identity, Vector3.Zero,
                Vector3.Zero, float.NaN, mass, seconds));
        }

        [Theory]
        [InlineData(60)]
        [InlineData(120)]
        public void WorldAdapterPreservesTheIndependentBodyFrameFlightControl(int frequency)
        {
            AeroProfile profile = AeroProfile.Build(AeroFlightLab.Shuttlecock(), 98);
            var direct = AeroFlightLab.Fly(profile, Vector3.Backward, 50f, 1.225f, 20f, 60f, 1f / frequency);
            var world = AeroFlightLab.Fly(profile, Vector3.Backward, 50f, 1.225f, 20f, 60f, 1f / frequency, true);
            Assert.InRange(Math.Abs(world.FinalError - direct.FinalError), 0f, 0.1f);
            Assert.InRange(Math.Abs(world.LastOutsideFive - direct.LastOutsideFive), 0f, 0.1f);
            Assert.True(world.LastOutsideFive < 30f);
            Assert.True(world.WorstLateError < 5f);
            Assert.Equal(0, world.GuardBound);
            Assert.Equal(frequency * 60, world.Steps);
        }

        [Fact]
        public void RegisteredFaceOverridesReachForceAndDampingAndCanBeCleared()
        {
            GridModel grid = AeroCalibrationLab.Box(3, 3, 3);
            AeroRuntime runtime = new AeroRuntime();
            Vector3 centre = new Vector3(2.5f);
            Finish(runtime, grid, 98, 0, centre);
            AeroWrench original = AeroForces.Evaluate(runtime.Profile, Matrix.Identity, new Vector3(0, 0, 50),
                Vector3.One, 1.225f, runtime.Profile.Mass, 1f / 60f);
            foreach (BlockInstance block in grid.Blocks) block.Drag = DragProfile.Of(0, 0, 0, 0, 0, 0);
            Finish(runtime, grid, 98, 1, centre);
            AeroWrench removed = AeroForces.Evaluate(runtime.Profile, Matrix.Identity, new Vector3(0, 0, 50),
                Vector3.One, 1.225f, runtime.Profile.Mass, 1f / 60f);
            Assert.True(original.Force.LengthSquared() > 0f && original.Damping.LengthSquared() > 0f);
            Assert.Equal(Vector3.Zero, removed.Force);
            Assert.Equal(Vector3.Zero, removed.Damping);
            foreach (BlockInstance block in grid.Blocks) block.Drag = DragProfile.Of(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
            Finish(runtime, grid, 98, 2, centre);
            AeroWrench half = AeroForces.Evaluate(runtime.Profile, Matrix.Identity, new Vector3(0, 0, 50),
                Vector3.One, 1.225f, runtime.Profile.Mass, 1f / 60f);
            Assert.Equal(original.Damping / 2f, half.Damping);
            Assert.InRange(half.Force.Length(), 1f, original.Force.Length());
            foreach (BlockInstance block in grid.Blocks) block.Drag = default(DragProfile);
            Finish(runtime, grid, 98, 3, centre);
            AeroWrench cleared = AeroForces.Evaluate(runtime.Profile, Matrix.Identity, new Vector3(0, 0, 50),
                Vector3.One, 1.225f, runtime.Profile.Mass, 1f / 60f);
            Assert.Equal(original.Force, cleared.Force);
            Assert.Equal(original.Restoring, cleared.Restoring);
            Assert.Equal(original.Damping, cleared.Damping);
        }

        [Fact]
        public void TheGameTickReachesPhysicsBeforeTheThermalEarlyReturn()
        {
            string root = ShippedBlocks.RepoRoot();
            string thermal = File.ReadAllText(Path.Combine(root,"Thermodynamics/Game/ThermalGridSimulation.cs"));
            Assert.DoesNotContain("TickAerodynamics",thermal);
            string adapter=File.ReadAllText(Path.Combine(root,"../Aerodynamics/Source/FlightGrid.cs"));
            Assert.DoesNotContain("ThermalGrid",adapter);
            Assert.DoesNotContain("Simulation.Solver",adapter);
            Assert.Contains("api.Geometry(Grid)",adapter);
            Assert.Contains("Runtime.Flight.Evaluate",adapter);
            Assert.Contains("APPLY_WORLD_FORCE,Wrench.Force,null,null",System.Text.RegularExpressions.Regex.Replace(adapter, @"\s+", ""));
            Assert.Contains("APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE,null,null,torque",System.Text.RegularExpressions.Regex.Replace(adapter, @"\s+", ""));

        }

        private static Vector3 Turn(Vector3 v) { return new Vector3(-v.Y, v.X, v.Z); }

        private static void Finish(AeroRuntime runtime, GridModel grid, int directions, long revision, Vector3 centre)
        {
            int calls = 0;
            while (!runtime.Prepare(grid, true, directions, revision, centre, 127, 7) || runtime.Rebuilding)
            {
                Assert.InRange(runtime.BuildWork, 0, 127);
                Assert.InRange(runtime.CentreWork, 0, 7);
                Assert.True(++calls < 10000);
            }
            Assert.True(runtime.Ready);
            Assert.True(runtime.Profile.Mass > 0f);
        }
    }
}

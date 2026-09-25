using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Regressions written from an in-game block-count stress test: twenty 44,632 cell capital
    /// ships plus sixty projections, run to a world close.
    ///
    /// Each test here pins one thing that report got wrong or nearly got wrong. The report itself
    /// is the specification — where a number is quoted in a comment, it came out of that run.
    /// </summary>
    [Trait("speed", "slow")]
    public class StressFindingsTests
    {

        [Fact]

        public void AFreshSolverReportsEmptySpaceAndNotAbsoluteZero()
        {

            ThermalSettings settings = new ThermalSettings().Derive();
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.HeavyArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);

            Assert.Equal(settings.VacuumTemperature, simulation.Solver.Environment.AmbientTemperature, 3);
            Assert.True(simulation.Solver.Environment.AmbientTemperature > 0f,
                "a grid that has not stepped yet still sits in empty space, not at 0 K");
        }

        [Fact]

        public void SubstepEstimateBeforeTheFirstStepIsUsable()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Fill(Catalog.HeavyArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 800f);

            float required = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);

            Assert.False(float.IsNaN(required));
            Assert.True(required >= 0f, "substep estimate went negative on a fresh solver");

            Assert.False(float.IsInfinity(required),
                "a fresh solver has to answer with a number a scheduler can act on");

            float duringStep = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(duringStep, required, 3);
            Assert.Equal(
                required <= 1f ? 1 : (int)Math.Ceiling(required),
                simulation.Solver.LastSubsteps);
        }

        [Fact]

        public void TheFirstRecordedSampleHasAPhysicalAmbient()
        {
            ScenarioResult result = Scenarios.Run("vacuum-soak");

            Assert.True(result.Runner.Samples[0].AmbientTemperature > 0f,
                "sample zero reported " + result.Runner.Samples[0].AmbientTemperature + " K");
        }


        [Fact]

        public void BuildingSurfacesConcurrentlyMatchesTheSingleThreadedAnswer()
        {

            Vector3I size = new Vector3I(1, 1, 2);

            SealTest seals = delegate (Vector3I cell, int face) { return face != Face.Up; };

            int[] expected = BlockSurfaceBuilder.BuildSurfaces(size, false, seals, Mounts());

            const int workers = 8;
            const int perWorker = 200;
            int[][] results = new int[workers][];

            Parallel.For(0, workers, w =>
            {
                int[] last = null;
                for (int i = 0; i < perWorker; i++)
                {
                    last = BlockSurfaceBuilder.BuildSurfaces(size, false, seals, Mounts());
                }
                results[w] = last;
            });

            for (int w = 0; w < workers; w++)
            {
                Assert.Equal(expected, results[w]);
            }
        }

        [Fact]

        public void BuildingSurfacesDoesNotMutateOrRetainTheCallersMounts()
        {

            List<MountRect> mounts = Mounts();
            int before = mounts.Count;


            SealTest seals = delegate (Vector3I cell, int face) { return true; };
            int[] first = BlockSurfaceBuilder.BuildSurfaces(Vector3I.One, true, seals, mounts);

            Assert.Equal(before, mounts.Count);

            int[] copy = (int[])first.Clone();
            mounts.Clear();

            Assert.Equal(copy, first);
        }


        private static List<MountRect> Mounts()
        {

            List<MountRect> mounts = new List<MountRect>();
            for (int face = 0; face < Face.Count; face++)
            {
                Vector3I normal = Face.Offsets[face];

                MountRect rect = new MountRect(normal, Vector3.Zero, Vector3.One);
                rect.Enabled = true;
                mounts.Add(rect);
            }
            return mounts;
        }


        [Fact]

        public void ACapitalShipCostsFarMoreToMapOnceThanToStep()
        {
            ScenarioResult result = Scenarios.Run("capital");

            Assert.Contains("cells", result.Summary);
            Assert.Contains("sealed rooms", result.Summary);

            ThermalSimulation simulation = result.Runner.Simulation;

            Assert.True(simulation.Solver.Nodes.Count > 30000,
                "the capital scenario has to stay at stress-test scale: "
                + simulation.Solver.Nodes.Count + " cells");
            Assert.True(simulation.Rooms.Map.RoomCount > 1, "bulkheads should seal compartments");
            Assert.False(simulation.Rooms.HasWorkPending, "the map should be finished before stepping");
        }

        [Fact]

        public void FleetCostScalesWithTheNumberOfGrids()
        {
            ScenarioResult result = Scenarios.Run("fleet");

            Assert.Contains("20 ships", result.Summary);

            IList<Sample> samples = result.Runner.Samples;
            for (int i = 1; i < samples.Count; i++)
            {
                Assert.True(samples[i].Substeps >= 1);
                Assert.False(float.IsNaN(samples[i].HottestTemperature));
            }
        }

        [Fact]

        public void ABuriedHeatSourceHasNoExposedFaceAndStillShedsItsHeat()
        {
            ScenarioResult result = Scenarios.Run("interior");
            ThermalSimulation simulation = result.Runner.Simulation;

            ThermalNode buried = simulation.Solver.GetNodeAt(Vector3I.Zero);
            ThermalNode skin = simulation.Solver.GetNodeAt(new Vector3I(0, 4, 0));

            Assert.Equal(0, buried.TotalExposedFaces);
            Assert.True(skin.TotalExposedFaces > 0, "the skin block should see space");

            IList<Sample> samples = result.Runner.Samples;
            float peak = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Tracked["buried"] > peak) peak = samples[i].Tracked["buried"];
            }

            Assert.True(peak < 400f,
                "a 10 kW waste load in armour must not run away: peaked at " + peak + " K");

            float gap = Math.Abs(result.Runner.Final.Tracked["buried"] - result.Runner.Final.Tracked["hull"]);
            Assert.True(gap < 5f, "buried block and hull should equilibrate, gap was " + gap + " K");
        }
    }
}

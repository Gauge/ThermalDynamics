using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Temperature thresholds another mod can register, and the edge semantics they promise.
    ///
    /// <para>
    /// The whole difficulty is not reporting twice. A block sitting exactly on a threshold, a block
    /// crossing it inside a multi-step update, and a block crossing the wrong way each have a case
    /// here, because a threshold that fires repeatedly is worse than one that never fires: the caller
    /// acts on it.
    /// </para>
    /// </summary>
    public class ThresholdTests
    {

        private static ThermalSimulation OneBlock(float initial, ThermalSettings settings = null)
        {

            ThermalSettings effective = settings ?? new ThermalSettings();
            effective.EnableEnvironment = false;
            effective.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero);
            return builder.BuildSimulation(effective, initial);
        }

        [Fact]

        public void NothingRegisteredReportsNothing()
        {

            ThermalSimulation simulation = OneBlock(300f);
            simulation.Solver.Nodes[0].Block.PowerProducedWatts = 15e6f;
            simulation.Solver.RefreshHeatGeneration();

            simulation.StepExact(20, Worlds.Shadow());

            Assert.Empty(simulation.Crossings);
        }

        [Fact]

        public void ARisingBlockReportsOnceAsItPasses()
        {

            ThermalSimulation simulation = OneBlock(300f);
            int id = simulation.Thresholds.Add(400f, ThresholdDirection.Rising);

            simulation.Solver.Nodes[0].Block.PowerProducedWatts = 15e6f;
            simulation.Solver.RefreshHeatGeneration();

            int seen = 0;
            for (int i = 0; i < 200; i++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                for (int c = 0; c < simulation.Crossings.Count; c++)
                {
                    Assert.Equal(id, simulation.Crossings[c].ThresholdId);
                    Assert.True(simulation.Crossings[c].Rising);
                    Assert.Equal(400f, simulation.Crossings[c].Threshold, 3);
                    seen++;
                }
            }

            Assert.True(simulation.Solver.Nodes[0].Temperature > 400f);
            Assert.Equal(1, seen);
        }

        [Fact]

        public void ARisingThresholdIgnoresABlockCoolingThroughIt()
        {

            ThermalSimulation simulation = OneBlock(500f);
            simulation.Thresholds.Add(400f, ThresholdDirection.Rising);

            simulation.Settings.EnableEnvironment = true;
            simulation.Settings.Derive();

            int seen = 0;
            for (int i = 0; i < LabClock.Steps(400); i++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                seen += simulation.Crossings.Count;
            }

            Assert.True(simulation.Solver.Nodes[0].Temperature < 400f);
            Assert.Equal(0, seen);
        }

        [Fact]

        public void AFallingThresholdCatchesIt()
        {

            ThermalSimulation simulation = OneBlock(500f);
            simulation.Thresholds.Add(400f, ThresholdDirection.Falling);
            simulation.Settings.EnableEnvironment = true;
            simulation.Settings.Derive();

            int seen = 0;
            bool rising = true;
            for (int i = 0; i < LabClock.Steps(400); i++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                for (int c = 0; c < simulation.Crossings.Count; c++)
                {
                    rising = simulation.Crossings[c].Rising;
                    seen++;
                }
            }

            Assert.Equal(1, seen);
            Assert.False(rising);
        }

        [Fact]

        public void BothCatchesEitherDirection()
        {

            ThermalThresholds thresholds = new ThermalThresholds();
            int id = thresholds.Add(400f, ThresholdDirection.Both);


            List<ThresholdCrossing> results = new List<ThresholdCrossing>();
            thresholds.Collect(null, 390f, 410f, results);
            thresholds.Collect(null, 410f, 390f, results);

            Assert.Equal(2, results.Count);
            Assert.True(results[0].Rising);
            Assert.False(results[1].Rising);
            Assert.Equal(id, results[0].ThresholdId);
        }

        [Fact]

        public void SittingOnAThresholdDoesNotReportTwice()
        {

            ThermalThresholds thresholds = new ThermalThresholds();
            thresholds.Add(400f, ThresholdDirection.Both);


            List<ThresholdCrossing> results = new List<ThresholdCrossing>();

            thresholds.Collect(null, 399f, 400f, results);
            thresholds.Collect(null, 400f, 401f, results);

            Assert.Single(results);
        }

        [Fact]

        public void CrossingsSurviveAMultiStepUpdate()
        {

            ThermalSimulation simulation = OneBlock(300f);
            simulation.Thresholds.Add(310f, ThresholdDirection.Rising);
            simulation.Thresholds.Add(320f, ThresholdDirection.Rising);

            simulation.Solver.Nodes[0].Block.PowerProducedWatts = 15e6f;
            simulation.Solver.RefreshHeatGeneration();

            simulation.StepExact(400, Worlds.Shadow());

            Assert.Equal(2, simulation.Crossings.Count);
        }

        [Fact]

        public void RemovingAThresholdStopsIt()
        {

            ThermalSimulation simulation = OneBlock(300f);
            int id = simulation.Thresholds.Add(310f, ThresholdDirection.Rising);
            Assert.True(simulation.Thresholds.Remove(id));

            simulation.Solver.Nodes[0].Block.PowerProducedWatts = 15e6f;
            simulation.Solver.RefreshHeatGeneration();
            simulation.StepExact(400, Worlds.Shadow());

            Assert.Empty(simulation.Crossings);
        }

        [Fact]

        public void CrossingCarriesTheBlockThatCrossed()
        {

            ThermalSimulation simulation = OneBlock(300f);
            simulation.Thresholds.Add(310f, ThresholdDirection.Rising);

            BlockInstance block = simulation.Solver.Nodes[0].Block;
            block.PowerProducedWatts = 15e6f;
            simulation.Solver.RefreshHeatGeneration();

            simulation.StepExact(400, Worlds.Shadow());

            Assert.Single(simulation.Crossings);
            Assert.Same(block, simulation.Crossings[0].Block);
        }
    }
}

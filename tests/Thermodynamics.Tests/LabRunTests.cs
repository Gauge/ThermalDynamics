using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The two ways the lab spreads its work, and the invariant that makes one of them usable.
    ///
    /// Running the battery concurrently is only sound if it produces the same matrix. That is not
    /// obvious: every simulation built from one ship shares that ship's <c>BlockInstance</c>
    /// objects, and the load is written onto them, so a naive fan-out silently has two scenarios
    /// overwriting each other's watts — which does not throw, and does not look wrong in a report.
    /// It just answers a different question.
    /// </summary>
    [Collection("alone")]
    public class LabRunTests
    {
        [Theory]
        [InlineData(LabMode.Linear)]
        [InlineData(LabMode.Parallel)]

        public void ResultsKeepTheOrderOfTheirInputs(LabMode mode)
        {

            List<int> items = new List<int>();
            for (int i = 0; i < 200; i++) items.Add(i);

            List<string> results = LabRun.Map(items, i => i.ToString(), mode);

            Assert.Equal(items.Count, results.Count);
            for (int i = 0; i < items.Count; i++) Assert.Equal(i.ToString(), results[i]);
        }

        [Theory]
        [InlineData(LabMode.Linear)]
        [InlineData(LabMode.Parallel)]

        public void OneItemThrowingDoesNotLoseTheRest(LabMode mode)
        {

            List<int> items = new List<int>();
            for (int i = 0; i < 50; i++) items.Add(i);

            List<string> results = LabRun.Map(items, i =>
            {
                if (i % 10 == 0) throw new System.InvalidOperationException("this ship is unbuildable");
                return i.ToString();
            }, mode);

            Assert.Equal(45, results.Count);
            Assert.DoesNotContain("10", results);
            Assert.Contains("11", results);
        }

        [Fact]

        public void ParallelUsesMoreThanOneWorkerButLeavesTheMachineACore()
        {
            Assert.True(LabRun.Workers >= 1);
            Assert.True(LabRun.Workers < System.Environment.ProcessorCount
                || System.Environment.ProcessorCount == 1);
        }

        [Fact]

        public void ParallelAndLinearProduceTheSameMatrix()
        {
            if (CorpusFixture.Files().Count == 0) return;


            List<Battery.Scenario> scenarios = new List<Battery.Scenario>();
            foreach (Battery.Scenario scenario in Battery.All())
            {
                if (scenario.Name == "idle" || scenario.Name == "burn-forward") scenarios.Add(scenario);
            }

            List<Blueprints.Ship> ships = CorpusFixture.Spread(Sample);
            if (ships.Count == 0) return;

            List<ScenarioOutcome> parallel = BatteryLab.Run(ships, scenarios, null, LabMode.Parallel);
            List<ScenarioOutcome> linear = BatteryLab.Run(ships, scenarios, null, LabMode.Linear);

            int expected = ships.Count * scenarios.Count;
            Assert.Equal(expected, linear.Count);
            Assert.Equal(expected, parallel.Count);

            for (int i = 0; i < linear.Count; i++)
            {
                Assert.Equal(linear[i].Ship, parallel[i].Ship);
                Assert.Equal(linear[i].Scenario, parallel[i].Scenario);
                Assert.Equal(linear[i].PeakKelvin, parallel[i].PeakKelvin, 3);
                Assert.Equal(linear[i].MeanKelvin, parallel[i].MeanKelvin, 3);
                Assert.Equal(linear[i].BlocksOverCritical, parallel[i].BlocksOverCritical);
                Assert.Equal(linear[i].HottestBlock, parallel[i].HottestBlock);
            }
        }

        private const int Sample = 60;
    }
}

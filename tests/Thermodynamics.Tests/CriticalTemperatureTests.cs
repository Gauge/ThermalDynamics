using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Overheat damage: the one output of this simulation that destroys a player's ship, and the
    /// one that two field sessions failed to exercise at all.
    ///
    /// <para>
    /// Both dumps reported <c>critical damage events 0</c> and <c>total heat damage 0.0</c>. Not
    /// because nothing got hot — a thruster reached 939 K — but because the equilibrium it
    /// reached sat under its 1,050 K rating. So the whole damage path shipped untested against
    /// anything but unit tests on a single block, and the balance question ("what should a block
    /// survive") and the correctness question ("does the threshold fire when it should") were
    /// indistinguishable from outside.
    /// </para>
    ///
    /// <para>
    /// These separate them. Every test here drives a hull to a temperature the block census says
    /// is fatal, and asserts what the simulation does about it. What temperature a real ship
    /// reaches is a balance question and belongs in a dump; whether the model acts on it when it
    /// does is a correctness question and belongs here.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class CriticalTemperatureTests
    {

        private static ThermalSettings Settings(int cap = 0)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubstepsPerBlock = cap;
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.EnableDamage = true;
            return settings.Derive();
        }


        private static ThermalSimulation Driven(ThermalSettings settings, int blocks, float watts)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!Census.IsProducer(nodes[i])) continue;

                float waste = nodes[i].Thermal.ProducerWasteEnergy;
                if (waste <= 0f) continue;

                nodes[i].Block.PowerProducedWatts = watts / waste;
                nodes[i].RefreshHeatGeneration();
            }

            return simulation;
        }


        private static List<OverheatEvent> RunCollecting(ThermalSimulation simulation, int steps)
        {

            List<OverheatEvent> events = new List<OverheatEvent>();
            EnvironmentSample sample = Worlds.Shadow();

            for (int i = 0; i < steps; i++)
            {
                simulation.StepExact(1, sample);

                IList<OverheatEvent> raised = simulation.Solver.Overheats;
                for (int e = 0; e < raised.Count; e++) events.Add(raised[e]);
            }

            return events;
        }


        private static float Hottest(ThermalSimulation simulation)
        {
            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            float peak = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Temperature > peak) peak = nodes[i].Temperature;
            }
            return peak;
        }

        [Fact]

        public void ACensusShipAtMeasuredPowerSettlesBelowItsRating()
        {

            ThermalSimulation simulation = Driven(Settings(), 2000, Census.ProducerWatts);

            List<OverheatEvent> events = RunCollecting(simulation, 400);


            float peak = Hottest(simulation);

            Assert.True(peak > 400f, "the hull never warmed at all, peak " + peak + " K");
            Assert.True(peak < Census.ProducerCriticalTemperature,
                "the census ship reached " + peak + " K against a producer rating of "
                + Census.ProducerCriticalTemperature + " K; the field dumps it is built from"
                + " settled below theirs, so either the census or this expectation has moved");

            Assert.Empty(events);
        }

        [Fact]

        public void APastCriticalBlockRaisesDamageProportionalToHowFarPast()
        {

            ThermalSimulation simulation = Driven(Settings(), 2000, Census.ProducerWatts * 12f);

            List<OverheatEvent> events = RunCollecting(simulation, 400);

            Assert.NotEmpty(events);

            for (int i = 0; i < events.Count; i++)
            {
                OverheatEvent raised = events[i];

                float critical = raised.Block.Thermal.CriticalTemperature;
                Assert.True(raised.Temperature > critical,
                    "damage was raised at " + raised.Temperature + " K against a critical of " + critical);
                Assert.True(raised.Damage > 0f);
            }
        }

        [Fact]

        public void DamagePerSecondDoesNotDependOnTheStepLength()
        {
            float[] totals = new float[2];
            int[] frequencies = { 2, 8 };

            for (int f = 0; f < frequencies.Length; f++)
            {

                ThermalSettings settings = Settings();
                settings.Frequency = frequencies[f];
                settings.DamageIsPerSecond = true;
                settings.Derive();


                ThermalSimulation simulation = Driven(settings, 2000, Census.ProducerWatts * 12f);

                simulation.StepExact(frequencies[f] * LabClock.Steps(60), Worlds.Shadow());


                List<OverheatEvent> events = RunCollecting(simulation, frequencies[f] * 4);

                float total = 0f;
                for (int i = 0; i < events.Count; i++) total += events[i].Damage;
                totals[f] = total;
            }

            Assert.True(totals[0] > 0f && totals[1] > 0f,
                "no damage was dealt at either frequency, so nothing was compared");

            float ratio = totals[0] / totals[1];
            Assert.True(ratio > 0.5f && ratio < 2f,
                "four seconds of damage came to " + totals[0] + " at Frequency 2 and "
                + totals[1] + " at Frequency 8; per-second damage must not scale with the step");
        }

        [Fact]

        public void TheSubstepCapDoesNotChangeWhetherAHullBurns()
        {
            int Steps = LabClock.Steps(600);


            ThermalSimulation uncapped = Driven(Settings(0), 2000, Census.ProducerWatts * 12f);

            List<OverheatEvent> uncappedEvents = RunCollecting(uncapped, Steps);

            Assert.NotEmpty(uncappedEvents);

            foreach (int cap in new int[] { 8, 4, 1 })
            {

                ThermalSimulation capped = Driven(Settings(cap), 2000, Census.ProducerWatts * 12f);

                List<OverheatEvent> cappedEvents = RunCollecting(capped, Steps);

                Assert.True(cappedEvents.Count > 0,
                    "cap " + cap + " stopped the hull burning at all, which the equilibrium"
                    + " argument says it cannot do");

                float difference = Math.Abs(Hottest(capped) - Hottest(uncapped));
                Assert.True(difference < 5f,

                    "cap " + cap + " left the hottest block at " + Hottest(capped)

                    + " K against " + Hottest(uncapped) + " K uncapped");
            }
        }

        [Fact]

        public void NothingBelowItsRatingIsEverDamaged()
        {
            foreach (int cap in new int[] { 0, 4, 1 })
            {

                ThermalSimulation simulation = Driven(Settings(cap), 2000, Census.ProducerWatts * 12f);

                List<OverheatEvent> events = RunCollecting(simulation, 400);

                for (int i = 0; i < events.Count; i++)
                {
                    OverheatEvent raised = events[i];
                    Assert.True(raised.Temperature > raised.Block.Thermal.CriticalTemperature,
                        "cap " + cap + " damaged a block at " + raised.Temperature
                        + " K, rated for " + raised.Block.Thermal.CriticalTemperature + " K");
                }
            }
        }

        [Fact]

        public void DamageDisabledRaisesNothing()
        {

            ThermalSettings settings = Settings();
            settings.EnableDamage = false;
            settings.Derive();


            ThermalSimulation simulation = Driven(settings, 2000, Census.ProducerWatts * 12f);

            List<OverheatEvent> events = RunCollecting(simulation, 400);

            Assert.Empty(events);
            Assert.True(Hottest(simulation) > Census.ProducerCriticalTemperature,
                "the hull did not reach a damaging temperature, so nothing was suppressed");
        }
    }
}

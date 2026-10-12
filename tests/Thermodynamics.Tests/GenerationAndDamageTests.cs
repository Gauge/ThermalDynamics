using Thermodynamics.Fixtures;
using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Power crossing a block turned into watts of heat.
    ///
    /// <para>
    /// The producer and consumer fractions are separate dials on separate flows, and thrust counts as
    /// consumption — which is what makes hydrogen thrusters the hottest thing on most ships. The last
    /// case is the one that matters for balance: generation is watts, so it must not change when the
    /// step rate does.
    /// </para>
    /// </summary>
    public class HeatGenerationTests
    {

        private static HistoricalThermalSettings Isolated()
        {
            HistoricalThermalSettings settings = new HistoricalThermalSettings { Frequency = 4 };
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            return settings.Derive();
        }

        [Fact]

        public void GeneratedPowerBecomesHeatAtTheDeclaredFraction()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(10f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            ThermalNode node = simulation.Solver.Nodes[0];

            float waste = 10f * ThermalConstants.MegawattsToWatts * node.Thermal.ProducerWasteEnergy;
            Assert.True(waste > 0f, "the reactor wastes nothing, so this judges nothing");
            Assert.Equal(waste, node.HeatGenerationWatts, 0);

            float before = node.Temperature;
            simulation.StepExact(4, Worlds.Shadow());

            float expected = before + (waste / node.ThermalMass);
            Assert.Equal(expected, node.Temperature, 1);
        }

        [Fact]

        public void ConsumedPowerAndThrustBothCount()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Thruster(), Vector3I.Zero)
                   .Consuming(1e6f)
                   .Thrusting(3e6f);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            ThermalNode node = simulation.Solver.Nodes[0];

            Assert.Equal(1e6f, node.HeatGenerationWatts, 0);
        }

        [Fact]

        public void ProducerAndConsumerFractionsAreIndependent()
        {
            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.ProducerWasteEnergy = 0.1f;
            thermal.ConsumerWasteEnergy = 0.5f;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Mixed", Vector3I.One, 1000f, thermal), Vector3I.Zero)
                   .Producing(1e6f)
                   .Consuming(1e6f);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            Assert.Equal(0.6e6f, simulation.Solver.Nodes[0].HeatGenerationWatts, 0);
        }

        [Fact]

        public void ChangingPowerTakesEffectAfterARefresh()
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(Isolated(), 300f);
            ThermalNode node = simulation.Solver.Nodes[0];
            Assert.Equal(0f, node.HeatGenerationWatts, 3);

            node.Block.PowerProducedWatts = 4e6f;
            node.RefreshHeatGeneration();

            Assert.Equal(4e6f * node.Thermal.ProducerWasteEnergy, node.HeatGenerationWatts, 0);
        }

        [Fact]

        public void HeatGenerationIsIndependentOfStepRate()
        {

            float atFour = TemperatureAfterOneSecond(4);

            float atSixteen = TemperatureAfterOneSecond(16);

            Assert.Equal(atFour, atSixteen, 2);
        }


        private static float TemperatureAfterOneSecond(int frequency)
        {

            HistoricalThermalSettings settings = Isolated();
            settings.Frequency = frequency;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.Reactor(), Vector3I.Zero).Producing(10f * ThermalConstants.MegawattsToWatts);

            ThermalSimulation simulation = builder.BuildSimulation(settings, 300f);
            simulation.StepExact(frequency, Worlds.Shadow());
            return simulation.Solver.Nodes[0].Temperature;
        }
    }

    /// <summary>
    /// What happens above a block's critical temperature.
    ///
    /// <para>
    /// The rate has to be per second rather than per step, or a server running at a different
    /// frequency destroys ships at a different speed. The pre-fix per-step behaviour is kept behind a
    /// setting and pinned as still scaling with frequency, so the difference between the two stays a
    /// measurement rather than a memory.
    /// </para>
    /// </summary>
    public class DamageTests
    {

        private static ThermalSimulation Overheated(HistoricalThermalSettings settings, float temperature)
        {
            GridBuilder builder = GridBuilder.Large();
            builder.Place(Catalog.LightArmor(), Vector3I.Zero);

            ThermalSimulation simulation = builder.BuildSimulation(settings, temperature);
            return simulation;
        }


        private static HistoricalThermalSettings NoTransfer(int frequency)
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = true;
            settings.Frequency = frequency;
            return settings.Derive();
        }

        [Fact]

        public void BelowCriticalNothingIsDamaged()
        {

            ThermalSimulation simulation = Overheated(NoTransfer(4), 500f);
            simulation.StepExact(4, Worlds.Shadow());

            Assert.Empty(simulation.Overheats);
        }

        [Fact]

        public void AboveCriticalTheBlockIsReported()
        {

            ThermalSimulation simulation = Overheated(NoTransfer(4), 1000f);
            simulation.StepExact(1, Worlds.Shadow());

            Assert.Single(simulation.Overheats);
            Assert.Equal(1000f, simulation.Overheats[0].Temperature, 1);
            Assert.True(simulation.Overheats[0].Damage > 0f);
        }

        [Fact]

        public void DamagePerSecondDoesNotDependOnStepRate()
        {

            float atFour = TotalDamageOverOneSecond(NoTransfer(4));

            float atSixteen = TotalDamageOverOneSecond(NoTransfer(16));

            Assert.Equal(100f, atFour, 1);
            Assert.Equal(100f, atSixteen, 1);
        }

        [Fact]

        public void TheOldPerStepBehaviourIsStillAvailableAndScalesWithFrequency()
        {

            HistoricalThermalSettings four = NoTransfer(4);
            four.DamageIsPerSecond = false;


            HistoricalThermalSettings sixteen = NoTransfer(16);
            sixteen.DamageIsPerSecond = false;


            float atFour = TotalDamageOverOneSecond(four);

            float atSixteen = TotalDamageOverOneSecond(sixteen);

            Assert.Equal(400f, atFour, 1);
            Assert.Equal(1600f, atSixteen, 1);
        }


        private static float TotalDamageOverOneSecond(HistoricalThermalSettings settings)
        {

            ThermalSimulation simulation = Overheated(settings, 1000f);

            float total = 0f;
            for (int i = 0; i < settings.Frequency; i++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                for (int d = 0; d < simulation.Overheats.Count; d++)
                {
                    total += simulation.Overheats[d].Damage;
                }
            }
            return total;
        }

        [Fact]

        public void DamageScalesWithOvershootAndScaler()
        {
            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.CriticalTemperature = 500f;
            thermal.OverheatDamagePerKelvin = 2f;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Fragile", Vector3I.One, 500f, thermal), Vector3I.Zero);


            HistoricalThermalSettings settings = NoTransfer(1);
            ThermalSimulation simulation = builder.BuildSimulation(settings, 600f);

            simulation.StepExact(1, Worlds.Shadow());

            Assert.Equal(200f, simulation.Overheats[0].Damage, 1);
        }

        [Fact]

        public void DisablingDamageSilencesTheEvents()
        {

            HistoricalThermalSettings settings = NoTransfer(4);
            settings.EnableDamage = false;


            ThermalSimulation simulation = Overheated(settings, 2000f);
            simulation.StepExact(4, Worlds.Shadow());

            Assert.Empty(simulation.Overheats);
        }
    }
}

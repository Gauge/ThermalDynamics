using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// How long a block has after it crosses its critical temperature — the second half of the
    /// event the balance survey had only measured the first half of.
    ///
    /// <para>
    /// The solver damages an overheating block by <c>(T - critical) x OverheatDamagePerKelvin</c>
    /// per simulated second, so at the crossing the damage rate is exactly zero and the block is
    /// losing nothing. What it costs a player is when its hit points run out, and
    /// <see cref="BlockHeatIndex.SecondsFromCriticalToLoss"/> is the closed answer to that.
    /// This class is what stops that answer being its own oracle (`E7`): the closed form against
    /// arithmetic where arithmetic has an answer, and against the shipped solver where it does not.
    /// See balance.md, How long a block has after it crosses.
    /// </para>
    /// </summary>
    public class TimeToLossTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper output;


        public TimeToLossTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void WithNothingRadiatingTheLossTimeIsTheClosedForm()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;
            const float Critical = 600f;
            const float DamagePerKelvin = 2f;
            const float Integrity = 30000f;

            float expected = (float)Math.Sqrt(
                2d * Capacity * Integrity / (DamagePerKelvin * (double)Watts));

            float measured = BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, 0f, Critical, DamagePerKelvin, Integrity);

            Assert.Equal(expected, measured, 1);

            Assert.Equal(expected * (float)Math.Sqrt(2d), BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts / 2f, 0f, Critical, DamagePerKelvin, Integrity), 1);
            Assert.Equal(expected * (float)Math.Sqrt(2d), BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, 0f, Critical, DamagePerKelvin, Integrity * 2f), 1);
        }

        [Fact]

        public void ABlockThatSettlesJustAboveCriticalGrindsDownAtAConstantRate()
        {
            const float Capacity = 200f;
            const float Watts = 250f;
            const float Critical = 500f;
            const float DamagePerKelvin = 1f;
            const float Integrity = 20000f;


            float ambient4 = Pow4(BlockHeatIndex.AmbientKelvin);
            float coefficient = Watts / (Pow4(Critical + 10f) - ambient4);

            float measured = BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, coefficient, Critical, DamagePerKelvin, Integrity);

            float marched = MarchToLoss(Capacity, Watts, coefficient, Critical, DamagePerKelvin,
                Integrity);

            Assert.False(float.IsInfinity(measured), "a block ten kelvin over does die, eventually");
            Assert.Equal(marched, measured, marched * 0.005f);

            Assert.True(measured > 1500f, "the tail is most of the life, got " + measured);
        }


        private static float MarchToLoss(float capacity, float watts, float coefficient,
            float critical, float damagePerKelvin, float integrity)
        {
            const double Step = 0.001d;

            double ambient4 = Pow4(BlockHeatIndex.AmbientKelvin);
            double temperature = critical;
            double damage = 0d;

            for (int i = 0; i < 20000000; i++)
            {
                damage += damagePerKelvin * (temperature - critical) * Step;
                if (damage >= integrity) return (float)(i * Step);

                double squared = temperature * temperature;
                double net = watts - (coefficient * ((squared * squared) - ambient4));
                temperature += net * Step / capacity;
            }

            return float.PositiveInfinity;
        }

        [Fact]

        public void ABlockThatNeverCrossesIsNeverDestroyed()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;
            const float Critical = 900f;


            float ambient4 = Pow4(BlockHeatIndex.AmbientKelvin);
            float coefficient = Watts / (Pow4(Critical - 100f) - ambient4);

            Assert.True(float.IsPositiveInfinity(BlockHeatIndex.SecondsToReach(
                Capacity, Watts, coefficient, Critical)));
            Assert.True(float.IsPositiveInfinity(BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, coefficient, Critical, 1f, 20000f)));
        }

        [Fact]

        public void TheClosedFormAgreesWithTheSolverOnASingleBlock()
        {
            const float Mass = 30000f;
            const float SpecificHeat = 450f;
            const float Critical = 700f;
            const float DamagePerKelvin = 2f;
            const float Integrity = 30000f;
            const float Watts = 400000f;

            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.SpecificHeat = SpecificHeat;
            thermal.CriticalTemperature = Critical;
            thermal.OverheatDamagePerKelvin = DamagePerKelvin;
            thermal.ProducerWasteEnergy = 1f;
            thermal.ExposedSurfaceMultiplier = 1f;


            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = true;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.Frequency = 60;

            settings.VacuumTemperature = BlockHeatIndex.AmbientKelvin;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Oracle", Vector3I.One, Mass, thermal), Vector3I.Zero)
                   .Producing(Watts);

            ThermalSimulation simulation =
                builder.BuildSimulation(settings, BlockHeatIndex.AmbientKelvin);

            float area = 6f * Catalog.LargeGridSize * Catalog.LargeGridSize;
            float capacity = Mass * SpecificHeat / settings.HeatTimeScale;
            float coefficient = thermal.Emissivity * ThermalConstants.StefanBoltzmann * area;

            float closedCritical = BlockHeatIndex.SecondsToReach(capacity,
                Watts * thermal.ProducerWasteEnergy, coefficient, Critical);
            float closedLoss = BlockHeatIndex.SecondsFromCriticalToLoss(capacity,
                Watts * thermal.ProducerWasteEnergy, coefficient, Critical, DamagePerKelvin,
                Integrity);

            Assert.False(float.IsInfinity(closedLoss),
                "the rig has to destroy the block or this test asserts nothing");

            float step = settings.StepSeconds;
            float elapsed = 0f;
            float damage = 0f;
            float measuredCritical = -1f;
            float measuredLoss = -1f;

            for (int i = 0; i < 200000 && measuredLoss < 0f; i++)
            {
                simulation.StepExact(1, Worlds.Shadow());
                elapsed += step;

                IList<OverheatEvent> events = simulation.Overheats;
                for (int e = 0; e < events.Count; e++)
                {
                    if (measuredCritical < 0f) measuredCritical = elapsed;
                    damage += events[e].Damage;
                    if (damage >= Integrity && measuredLoss < 0f) measuredLoss = elapsed;
                }
            }

            Assert.True(measuredLoss > 0f, "the solver never destroyed the block");

            Assert.Equal(closedCritical, measuredCritical, closedCritical * 0.01f);
            Assert.Equal(closedLoss, measuredLoss - measuredCritical, closedLoss * 0.01f);
        }


        [Fact]

        public void DestructionBeyondTheHorizonReadsAsNever()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;
            float critical = BlockHeatIndex.AmbientKelvin + 100f;

            float equilibrium = critical + 1f;

            float ambient4 = Pow4(BlockHeatIndex.AmbientKelvin);
            float coefficient = Watts / (Pow4(equilibrium) - ambient4);

            Assert.True(float.IsPositiveInfinity(BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, coefficient, critical, 1f, 1e6f)),
                "a block an hour of overheating cannot finish is not a loss");
        }

        [Fact]

        public void AnUnpricedBlockIsUnavailableRatherThanIndestructible()
        {
            Assert.Equal(0f, BlockHeatIndex.SecondsFromCriticalToLoss(
                5000f, 250f, 0f, BlockHeatIndex.AmbientKelvin + 100f, 1f, 0f));
        }

        [Fact]

        public void NoShippedBlockIsLostBeforeItCrosses()
        {
            if (!GameBlocks.IsInstalled) return;

            List<BlockHeatIndex.Reading> readings = BlockHeatIndex.All();
            Assert.True(readings.Count > 0, "the install yielded no readings");

            int crossing = 0;

            List<string> violations = new List<string>();

            foreach (BlockHeatIndex.Reading reading in readings)
            {
                if (float.IsInfinity(reading.SecondsToCritical)) continue;
                crossing++;

                if (reading.SecondsCriticalToLoss <= 0f)
                {
                    violations.Add(reading.Subtype + " is destroyed at the moment it crosses");
                }
            }

            Assert.True(crossing > 0, "no shipped block crosses, so nothing was judged");
            Assert.True(violations.Count == 0, string.Join("\n  ", violations));
        }


        [Fact]

        public void TheSpanGoesAsTheInverseSquareRootOfTheDamageDial()
        {
            const float Capacity = 5000f;
            const float Watts = 250f;
            const float Critical = 600f;
            const float Integrity = 30000f;

            float baseline = BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, 0f, Critical, 2f, Integrity);

            Assert.Equal(baseline * (float)Math.Sqrt(2d), BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, 0f, Critical, 1f, Integrity), 1);
            Assert.Equal(baseline / (float)Math.Sqrt(2d), BlockHeatIndex.SecondsFromCriticalToLoss(
                Capacity, Watts, 0f, Critical, 4f, Integrity), 1);
        }

        [Fact]

        public void TheAuthoredDamageRuleWouldPutTheWholeEventInsideAboutTenSeconds()
        {
            if (!GameBlocks.IsInstalled) return;

            const float Authored = 8f;


            List<float> shipped = new List<float>();

            List<float> authored = new List<float>();

            foreach (BlockHeatIndex.Reading reading in BlockHeatIndex.All())
            {
                if (float.IsInfinity(reading.SecondsToCritical)) continue;
                if (float.IsInfinity(reading.SecondsCriticalToLoss)) continue;

                float harsher = BlockHeatIndex.SecondsFromCriticalToLoss(
                    reading.HeatCapacity, reading.Watts, reading.RadiativeCoefficient,
                    reading.CriticalKelvin, reading.DamagePerKelvin * Authored, reading.Integrity);

                if (float.IsInfinity(harsher)) continue;

                shipped.Add(reading.SecondsCriticalToLoss);
                authored.Add(harsher);
            }

            Assert.True(shipped.Count > 20, "too few shipped blocks crossed to say anything");


            float shippedMedian = Median(shipped);

            float authoredMedian = Median(authored);

            output.WriteLine(string.Format("{0,-22}{1,9}{2,9}{3,9}", "damage per kelvin", "p10", "p50", "p90"));
            output.WriteLine(string.Format("{0,-22}{1,9:n1}{2,9:n1}{3,9:n1}", "as shipped (x1)",
                Percentile(shipped, 10), shippedMedian, Percentile(shipped, 90)));
            output.WriteLine(string.Format("{0,-22}{1,9:n1}{2,9:n1}{3,9:n1}", "as authored (x8)",
                Percentile(authored, 10), authoredMedian, Percentile(authored, 90)));
            output.WriteLine(shipped.Count + " shipped block types cross and are destroyed");

            Assert.True(shippedMedian > 20f,
                "the shipped rule gives the median block " + shippedMedian.ToString("n1") + " s");
            Assert.True(authoredMedian < 15f,
                "the authored rule gives the median block " + authoredMedian.ToString("n1") + " s");
            Assert.True(authoredMedian < shippedMedian / 3f,
                "the authored rule gives the median block " + authoredMedian.ToString("n1")
                + " s against the shipped rule's " + shippedMedian.ToString("n1")
                + " s, so restoring it would cost less than the third it costs now");
        }


        private static float Percentile(List<float> values, int percent)
        {

            List<float> ordered = new List<float>(values);
            ordered.Sort();
            int index = (ordered.Count - 1) * percent / 100;
            return ordered[index];
        }


        private static float Median(List<float> values)
        {

            List<float> ordered = new List<float>(values);
            ordered.Sort();
            return ordered[ordered.Count / 2];
        }


        private static float Pow4(float value)
        {
            float square = value * value;
            return square * square;
        }
    }
}

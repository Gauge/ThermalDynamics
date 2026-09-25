using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A coolant loop costs power to run, and it is very much cheaper than a heat pump.
    ///
    /// <para>
    /// `CoolantPump.MaxPowerWatts` was declared and assigned nowhere, so a loop was free once built
    /// while a heat pump paid for every watt it lifted. The asymmetry is right — a circulator is
    /// not a refrigerator — but nothing had ever said so, and the mod's own intent is that *a
    /// cooling system costs power and makes heat doing it*.
    /// </para>
    ///
    /// <para>
    /// The rating is derived rather than chosen: a large-grid ring moves 200 kg/s of coolant, and
    /// pushing that against two bar of head at seventy per cent efficiency is `ṁ ΔP / (ρ η)` =
    /// 57 kW. What these check is the *shape* — that it is charged at all, linear in speed, off
    /// when the block is, and small against what the ring carries.
    /// </para>
    /// </summary>
    public class PumpPowerTests
    {
        [Fact]

        public void APumpAsksForPowerInProportionToItsSpeed()
        {
            CoolantPump pump = new CoolantPump { MaxPowerWatts = 50000f };

            Assert.Equal(50000f, pump.DemandWatts, 1);

            pump.Speed = 0.5f;
            Assert.Equal(25000f, pump.DemandWatts, 1);

            pump.Speed = 0f;
            Assert.Equal(0f, pump.DemandWatts, 1);
        }

        [Fact]

        public void AStoppedPumpAsksForNothing()
        {
            CoolantPump pump = new CoolantPump { MaxPowerWatts = 50000f, Enabled = false };

            Assert.Equal(0f, pump.DemandWatts, 1);
            Assert.Equal(0f, pump.Contribution, 3);
        }

        [Fact]

        public void AnUnderSuppliedPumpCirculatesSlowerRatherThanStopping()
        {
            CoolantPump pump = new CoolantPump { MaxPowerWatts = 50000f };

            Assert.Equal(1f, pump.Contribution, 3);

            pump.PowerAvailable = 0.4f;
            Assert.Equal(0.4f, pump.Contribution, 3);

            pump.PowerAvailable = 0f;
            Assert.Equal(0f, pump.Contribution, 3);
        }

        [Fact]

        public void ACirculatorIsFarCheaperPerWattMovedThanAHeatPump()
        {

            LoopThermalProperties loop = new LoopThermalProperties();

            const float ParcelMetres = 2.5f;
            float parcelsPerSecond = loop.FlowRateFor(ParcelMetres) / ParcelMetres;
            float wattsPerKelvin =
                parcelsPerSecond * loop.MassPerPipe(ParcelMetres) * loop.SpecificHeat;

            Assert.True(wattsPerKelvin > 100000f,
                "a ring should carry hundreds of kilowatts per kelvin: " + wattsPerKelvin);

            float pumpShare = 50000f / wattsPerKelvin;
            Assert.True(pumpShare < 0.2f,
                "a circulator costing " + (pumpShare * 100f).ToString("n1")
                + "% of one kelvin's worth of transport is not a circulator");

            HeatPumpShape shape = HeatPumpShape.Centred(Vector3I.Forward, Vector3I.One, 60000f, 20000f);
            float lift = shape.RatedWatts / shape.MaxPowerWatts;

            Assert.True(lift < 1f / pumpShare,
                "the heat pump should be the expensive way to move heat: it lifts " + lift
                + " W per watt against the loop's " + (1f / pumpShare));
        }
    }
}

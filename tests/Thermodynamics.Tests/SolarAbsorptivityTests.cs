using System;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Emissivity and solar absorptivity are two numbers.
    ///
    /// <para>
    /// One number used to do both jobs: `ThermalNode.RadiationCoefficient` is
    /// `Emissivity x sigma x area` for the heat a block gives up, and the same emissivity scaled
    /// the sun and every point source coming in. A good radiator was therefore forced to be a good
    /// absorber, which is the one combination real spacecraft radiators exist to avoid — a
    /// selective surface is high in the thermal infrared and low in the visible. Backlog `B27`.
    /// </para>
    ///
    /// <para>
    /// The compatibility constraint is the point of most of this class: **an authored file that
    /// says nothing about absorptivity must behave exactly as it did**, and 674 authored values plus
    /// every third-party definition say nothing about it.
    /// </para>
    /// </summary>
    public class SolarAbsorptivityTests
    {
        [Fact]

        public void UndeclaredAbsorptivityFollowsTheEmissivity()
        {
            BlockThermalProperties properties = new BlockThermalProperties { Emissivity = 0.42f };

            Assert.Equal(0.42f, properties.EffectiveSolarAbsorptivity, 5);

            properties.Clamp();
            Assert.Equal(0.42f, properties.EffectiveSolarAbsorptivity, 5);

            properties.Emissivity = 0.9f;
            Assert.Equal(0.9f, properties.EffectiveSolarAbsorptivity, 5);
        }

        [Fact]

        public void ADeclaredAbsorptivityIsUsedAndBoundedWithoutTouchingTheEmissivity()
        {
            BlockThermalProperties properties = new BlockThermalProperties
            {
                Emissivity = 0.9f,
                SolarAbsorptivity = 0.1f,
            };

            properties.Clamp();
            Assert.Equal(0.9f, properties.Emissivity, 5);
            Assert.Equal(0.1f, properties.EffectiveSolarAbsorptivity, 5);

            BlockThermalProperties mirror = new BlockThermalProperties
            {
                Emissivity = 0.9f,
                SolarAbsorptivity = 0f,
            };
            mirror.Clamp();
            Assert.Equal(0f, mirror.EffectiveSolarAbsorptivity, 5);

            BlockThermalProperties impossible = new BlockThermalProperties { SolarAbsorptivity = 3f };
            Assert.Contains(impossible.Validate(),
                problem => problem.Contains("SolarAbsorptivity"));
            impossible.Clamp();
            Assert.Equal(1f, impossible.EffectiveSolarAbsorptivity, 5);
        }

        [Fact]

        public void ASelectiveSurfaceSettlesCoolerInSunlightThanABlackOne()
        {
            Assert.True(Settled(0.9f, 0.1f) < Settled(0.9f, 0.9f) - 5f,
                "a low-absorptivity surface must settle clearly cooler in the same sunlight");

            float previous = 0f;
            for (float absorptivity = 0.1f; absorptivity <= 0.9f; absorptivity += 0.2f)
            {

                float settled = Settled(0.9f, absorptivity);
                Assert.True(settled > previous, "more absorptive must never be cooler");
                previous = settled;
            }
        }

        [Fact]

        public void RaisingTheEmissivityAloneCools()
        {
            Assert.True(Settled(0.9f, 0.3f) < Settled(0.3f, 0.3f) - 5f,
                "a better emitter at the same absorptivity must settle cooler");
        }

        [Fact]

        public void ABlockThatDeclaresNothingIsUnchanged()
        {
            Assert.Equal(Settled(0.35f, -1f), Settled(0.35f, 0.35f), 4);
        }


        private static float Settled(float emissivity, float absorptivity)
        {
            BlockThermalProperties thermal = Catalog.DefaultThermal();
            thermal.Emissivity = emissivity;
            thermal.SolarAbsorptivity = absorptivity;
            thermal.Clamp();


            ThermalSettings settings = new ThermalSettings();
            settings.EnableDamage = false;
            settings.EnableFriction = false;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(BlockModel.Solid("Panel", Vector3I.One, 500f, thermal), Vector3I.Zero);

            ThermalSimulation simulation =
                builder.BuildSimulation(settings.Derive(), settings.VacuumTemperature);

            EnvironmentSample sun = Worlds.Space(Vector3.Up);
            for (int i = 0; i < 4000; i++) simulation.StepExact(1, sun);

            return simulation.Solver.Nodes[0].Temperature;
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a block is made of, turned into what a block is thermally.
    ///
    /// Before this existed, twenty-eight block types out of a hundred and one had hand-written
    /// thermal properties and everything else in the game — and every block of every other mod —
    /// fell through to a single entry describing mild steel. A window, a battery, a medical bay and
    /// a plushie were the same object. The game already publishes what every block is built from,
    /// so none of that had to be true.
    /// </summary>
    public class BlockDerivationTests
    {

        private static List<BlockComponent> Of(params BlockComponent[] components)
        {
            return new List<BlockComponent>(components);
        }


        private static BlockComponent Part(string component, int count, float massEach)
        {
            return new BlockComponent(component, count, massEach);
        }

        [Fact]

        public void SpecificHeatIsTheMassWeightedMeanExactly()
        {
            BlockThermalProperties p = BlockThermalDerivation.Material(Of(
                Part("SteelPlate", 1, 100f),
                Part("BulletproofGlass", 1, 100f)));

            Assert.Equal((466f + 840f) / 2f, p.SpecificHeat, 1);
        }

        [Fact]

        public void AMassiveComponentDominatesALightOne()
        {
            BlockThermalProperties p = BlockThermalDerivation.Material(Of(
                Part("SteelPlate", 99, 20f),
                Part("Computer", 1, 0.2f)));

            Assert.Equal(BlockMaterials.Steel.SpecificHeat, p.SpecificHeat, 0);
            Assert.Equal(BlockMaterials.Steel.Conductivity, p.Conductivity, 0);
        }

        [Fact]

        public void AWindowIsGlassAndAnArmourBlockIsSteel()
        {
            BlockThermalProperties window = BlockThermalDerivation.Material(Of(
                Part("BulletproofGlass", 10, 15f),
                Part("Construction", 4, 8f)));

            BlockThermalProperties armour = BlockThermalDerivation.Material(Of(
                Part("SteelPlate", 25, 20f)));

            Assert.True(window.Conductivity < armour.Conductivity / 4f,
                "window conductivity " + window.Conductivity + " is not far below steel's " + armour.Conductivity);
            Assert.True(window.Emissivity > armour.Emissivity * 3f,
                "window emissivity " + window.Emissivity + " does not beat steel's " + armour.Emissivity);
        }

        [Fact]

        public void EmissivityComesFromTheHeaviestComponentRatherThanTheBlend()
        {
            BlockThermalProperties p = BlockThermalDerivation.Material(Of(
                Part("BulletproofGlass", 10, 15f),
                Part("SteelPlate", 1, 20f)));

            Assert.Equal(BlockMaterials.Get("BulletproofGlass").Emissivity, p.Emissivity, 3);
        }

        [Fact]

        public void CriticalTemperatureSeparatesABatteryFromAReactor()
        {
            BlockThermalProperties battery = BlockThermalDerivation.Material(Of(
                Part("PowerCell", 80, 25f),
                Part("SteelPlate", 80, 20f)));

            BlockThermalProperties reactor = BlockThermalDerivation.Material(Of(
                Part("Reactor", 100, 25f),
                Part("SteelPlate", 50, 20f)));

            Assert.True(battery.CriticalTemperature < reactor.CriticalTemperature - 200f,
                "battery " + battery.CriticalTemperature + " K against reactor " + reactor.CriticalTemperature + " K");
        }

        [Fact]

        public void APlushieIsFabricRatherThanSteel()
        {
            BlockThermalProperties p = BlockThermalDerivation.Material(Of(
                Part("EngineerPlushie", 1, 1f)));

            Assert.True(p.Conductivity < 1f, "a plushie conducts at " + p.Conductivity);
            Assert.True(p.SpecificHeat > 1000f, "a plushie holds " + p.SpecificHeat + " J/(kg K)");
        }

        [Fact]

        public void ABlockWithNoComponentsIsSteel()
        {
            foreach (List<BlockComponent> components in new List<BlockComponent>[]
            {
                null,

                new List<BlockComponent>(),
                Of(Part("SteelPlate", 0, 20f)),
            })
            {
                BlockThermalProperties p = BlockThermalDerivation.Material(components);

                Assert.Equal(BlockMaterials.Steel.Conductivity, p.Conductivity, 2);
                Assert.Equal(BlockMaterials.Steel.SpecificHeat, p.SpecificHeat, 2);
                Assert.Equal(BlockMaterials.Steel.ServiceLimit, p.CriticalTemperature, 2);
            }
        }

        [Fact]

        public void AnUnknownComponentIsTreatedAsSteel()
        {
            BlockThermalProperties p = BlockThermalDerivation.Material(Of(
                Part("SomeOtherModsExoticAlloy", 10, 20f)));

            Assert.Equal(BlockMaterials.Steel.SpecificHeat, p.SpecificHeat, 2);
        }

        [Fact]

        public void FunctionComesFromTheTypeAndNotFromTheComponents()
        {

            List<BlockComponent> same = Of(Part("SteelPlate", 10, 20f));

            BlockThermalProperties thruster = ShippedBlocks.DeriveWithFunction(same, "Thrust");
            BlockThermalProperties girder = ShippedBlocks.DeriveWithFunction(same, "CubeBlock");

            Assert.Equal(thruster.SpecificHeat, girder.SpecificHeat, 3);
            Assert.True(thruster.ConsumerWasteEnergy > girder.ConsumerWasteEnergy);
            Assert.True(thruster.ExposedSurfaceMultiplier > girder.ExposedSurfaceMultiplier);
        }

        [Theory]
        [InlineData("Reactor")]
        [InlineData("HydrogenEngine")]
        [InlineData("BatteryBlock")]

        public void EveryProducerTypeConvertsSomeOfItsOutput(string typeId)
        {
            Assert.True(ShippedBlocks.FunctionOf(typeId).ProducerWasteEnergy > 0f,
                typeId + " produces power and makes no heat doing it");
        }

        [Fact]

        public void ACombustionEngineRunsHotterThanAReactorPerWatt()
        {
            Assert.True(
                ShippedBlocks.FunctionOf("HydrogenEngine").ProducerWasteEnergy >
                ShippedBlocks.FunctionOf("Reactor").ProducerWasteEnergy);
        }

        [Fact]

        public void NoFunctionCreatesEnergyFromNothing()
        {
            foreach (string typeId in ShippedBlocks.FunctionTypes())
            {
                ShippedBlocks.Function f = ShippedBlocks.FunctionOf(typeId);

                Assert.True(f.ProducerWasteEnergy <= 1f, typeId + " producer fraction " + f.ProducerWasteEnergy);
                Assert.True(f.ConsumerWasteEnergy <= 1f, typeId + " consumer fraction " + f.ConsumerWasteEnergy);
                Assert.True(f.ExposedSurfaceMultiplier > 0f, typeId + " has no surface");
                Assert.True(f.OverheatDamagePerKelvin >= 0f, typeId + " has a negative damage rate");
            }
        }

        [Fact]

        public void EveryMaterialProducesAValidBlock()
        {
            foreach (string component in BlockMaterials.Names)
            {
                BlockThermalProperties p = ShippedBlocks.DeriveWithFunction(
                    Of(Part(component, 10, 20f)), "CubeBlock");

                Assert.Empty(p.Validate());
                Assert.True(p.SpecificHeat > 0f, component + " has no heat capacity");
                Assert.True(p.CriticalTemperature > 0f, component + " is critical at placement");
            }
        }

        [Fact]

        public void EveryInventedMaterialSitsInsideTheRangeOfTheRealOnes()
        {
            CheckInsideRealRange(delegate (BlockMaterial m) { return m.Conductivity; }, "conductivity");
            CheckInsideRealRange(delegate (BlockMaterial m) { return m.SpecificHeat; }, "specific heat");
            CheckInsideRealRange(delegate (BlockMaterial m) { return m.Emissivity; }, "emissivity");
            CheckInsideRealRange(delegate (BlockMaterial m) { return m.ServiceLimit; }, "service limit");
        }


        private static void CheckInsideRealRange(System.Func<BlockMaterial, float> property, string name)
        {
            float lowest, highest;
            BlockMaterials.RealRange(property, out lowest, out highest);

            foreach (string component in BlockMaterials.Names)
            {
                BlockMaterial material = BlockMaterials.Get(component);
                if (!material.Invented) continue;


                float value = property(material);
                Assert.True(value >= lowest && value <= highest,
                    component + " invents a " + name + " of " + value
                    + ", outside the real range " + lowest + " to " + highest);
            }
        }
    }
}

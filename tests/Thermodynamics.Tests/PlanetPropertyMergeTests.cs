using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a planet definition that says less than everything does to the climate.
    ///
    /// A field dump taken on an earthlike world read 2.7 K of ambient at 0.93 air density, no
    /// convection at all, and no solar decay — the vacuum figures, in breathable air — with 232,000
    /// points of heat damage behind them. The planet's thermal group had not reached the mod, and
    /// every value it did not supply arrived as zero and was written over the model's own default.
    /// A definition now overrides only what it carried.
    /// </summary>
    public class PlanetPropertyMergeTests
    {

        private static PlanetThermalProperties Read()
        {

            PlanetThermalProperties read = new PlanetThermalProperties();
            read.DayTemperature = 400f;
            read.NightTemperature = 300f;
            read.ConvectionCoefficient = 12f;
            read.SolarDecay = 0.8f;
            read.CoreTemperature = 1234f;
            return read;
        }

        [Fact]

        public void ADefinitionThatSuppliedNothingLeavesTheModelsOwnClimate()
        {

            PlanetThermalProperties defaults = new PlanetThermalProperties();

            PlanetThermalProperties merged = PlanetProperties.Merge(

                defaults, new PlanetThermalProperties(), PlanetField.None);

            Assert.Equal(defaults.DayTemperature, merged.DayTemperature);
            Assert.Equal(defaults.NightTemperature, merged.NightTemperature);
            Assert.Equal(defaults.ConvectionCoefficient, merged.ConvectionCoefficient);
            Assert.False(PlanetProperties.IsVacuum(merged));
        }

        [Fact]

        public void ABlankDefinitionCannotTurnBreathableAirIntoSpace()
        {

            PlanetThermalProperties blank = new PlanetThermalProperties();
            blank.DayTemperature = 0f;
            blank.NightTemperature = 0f;
            blank.UndergroundTemperature = 0f;
            blank.ConvectionCoefficient = 0f;

            Assert.True(PlanetProperties.IsVacuum(blank));

            PlanetThermalProperties merged = PlanetProperties.Merge(

                new PlanetThermalProperties(), blank, PlanetField.None);

            Assert.False(PlanetProperties.IsVacuum(merged));
            Assert.True(merged.ConvectionCoefficient > 0f);
        }

        [Fact]

        public void OnlyTheFieldsNamedAreTaken()
        {

            PlanetThermalProperties defaults = new PlanetThermalProperties();

            PlanetThermalProperties merged = PlanetProperties.Merge(

                defaults, Read(), PlanetField.DayTemperature | PlanetField.ConvectionCoefficient);

            Assert.Equal(400f, merged.DayTemperature);
            Assert.Equal(12f, merged.ConvectionCoefficient);

            Assert.Equal(defaults.NightTemperature, merged.NightTemperature);
            Assert.Equal(defaults.CoreTemperature, merged.CoreTemperature);
            Assert.Equal(defaults.SolarDecay, merged.SolarDecay);
        }

        [Fact]

        public void AFullDefinitionIsTakenWhole()
        {

            PlanetThermalProperties read = Read();

            PlanetThermalProperties merged = PlanetProperties.Merge(

                new PlanetThermalProperties(), read, PlanetField.All);

            Assert.Equal(read.DayTemperature, merged.DayTemperature);
            Assert.Equal(read.NightTemperature, merged.NightTemperature);
            Assert.Equal(read.ConvectionCoefficient, merged.ConvectionCoefficient);
            Assert.Equal(read.SolarDecay, merged.SolarDecay);
            Assert.Equal(read.CoreTemperature, merged.CoreTemperature);
        }

        [Fact]

        public void ADefinitionMaySupplyZeroDeliberately()
        {

            PlanetThermalProperties read = new PlanetThermalProperties();
            read.ConvectionCoefficient = 0f;
            read.PoleTemperatureDrop = 0f;

            PlanetThermalProperties merged = PlanetProperties.Merge(

                new PlanetThermalProperties(), read,
                PlanetField.ConvectionCoefficient | PlanetField.PoleTemperatureDrop);

            Assert.Equal(0f, merged.ConvectionCoefficient);
            Assert.Equal(0f, merged.PoleTemperatureDrop);
        }

        [Fact]

        public void NeitherArgumentIsModified()
        {

            PlanetThermalProperties defaults = new PlanetThermalProperties();
            float day = defaults.DayTemperature;


            PlanetThermalProperties read = Read();
            PlanetProperties.Merge(defaults, read, PlanetField.All);

            Assert.Equal(day, defaults.DayTemperature);
            Assert.Equal(400f, read.DayTemperature);
        }

        [Fact]

        public void TheResultIsClampedToWhatTheSolverCanUse()
        {

            PlanetThermalProperties read = new PlanetThermalProperties();
            read.DayTemperature = -50f;
            read.SolarDecay = 4f;

            PlanetThermalProperties merged = PlanetProperties.Merge(

                new PlanetThermalProperties(), read,
                PlanetField.DayTemperature | PlanetField.SolarDecay);

            Assert.Equal(0f, merged.DayTemperature);
            Assert.Equal(1f, merged.SolarDecay);
        }

        [Fact]

        public void AMissingBaselineIsTheModelsOwnDefaults()
        {
            PlanetThermalProperties merged = PlanetProperties.Merge(null, null, PlanetField.All);

            Assert.False(PlanetProperties.IsVacuum(merged));
            Assert.Equal(new PlanetThermalProperties().DayTemperature, merged.DayTemperature);
        }
    }
}

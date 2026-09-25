using System.Reflection;
using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The model's own settings type, whose defaults have to survive being loaded from a file
    /// written before a setting existed.
    ///
    /// The game-side config class cannot be reached from here — it needs the game's assemblies —
    /// but it has the same shape and the same trap, and the trap is worth stating once: a config
    /// file has no element for a setting added after it was written, the XML reader leaves the
    /// field at whatever the type starts as, and a world silently runs with every new feature off.
    /// Defaults belong on the fields, where a reader that finds nothing leaves them alone.
    /// </summary>
    public class SettingsDefaultsTests
    {
        [Fact]

        public void AFreshSettingsObjectIsAlreadyUsable()
        {

            ThermalSettings settings = new ThermalSettings();

            Assert.True(settings.EnableConduction);
            Assert.True(settings.EnableRadiation);
            Assert.True(settings.EnableSolarHeat);
            Assert.True(settings.SolarSelfShadowing);
            Assert.True(settings.Frequency > 0);
            Assert.True(settings.SimulationSpeed > 0f);
            Assert.True(settings.HeatTimeScale > 0f);
            Assert.True(settings.SolarEnergy > 0f);
        }

        [Fact]

        public void EveryNumericSettingStartsAtSomethingUsable()
        {

            ThermalSettings settings = new ThermalSettings();

            foreach (FieldInfo field in typeof(ThermalSettings).GetFields(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float) && field.FieldType != typeof(int)) continue;

                if (field.Name != "Frequency"
                    && field.Name != "SimulationSpeed"
                    && field.Name != "HeatTimeScale"
                    && field.Name != "SolarEnergy"
                    && field.Name != "RoomAirDensity"
                    && field.Name != "RoomConvectionCoefficient") continue;


                double value = field.FieldType == typeof(float)
                    ? (float)field.GetValue(settings)
                    : (int)field.GetValue(settings);

                Assert.True(value > 0d, field.Name + " should not start at zero");
            }
        }
    }
}

using Thermodynamics.Fixtures;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public static class Isolation
    {

        public static HistoricalThermalSettings DeadWorld()
        {

            HistoricalThermalSettings settings = new HistoricalThermalSettings();
            settings.EnableEnvironment = false;
            settings.EnableSolarHeat = false;
            settings.EnableFriction = false;
            settings.EnableDamage = false;
            return settings.Derive();
        }
    }
}

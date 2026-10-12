using Thermodynamics.Fixtures;
using Thermodynamics.Core;

namespace Thermodynamics.Harness
{
    public static class LabClock
    {
        public const float TunedAt = 225f;

        public static float Shipped
        {
            get
            {

                float clock = new HistoricalThermalSettings().HeatTimeScale;
                return clock > 0f ? clock : 1f;
            }
        }

        public static float Stretch
        {
            get { return TunedAt / Shipped; }
        }


        public static float Seconds(float seconds)
        {
            return seconds * Stretch;
        }


        public static int Steps(int steps)
        {
            int stretched = (int)(steps * Stretch);
            return stretched < 1 ? 1 : stretched;
        }
    }
}

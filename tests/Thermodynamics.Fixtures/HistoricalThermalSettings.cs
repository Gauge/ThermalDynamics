using Aerodynamics.Core;
using Thermodynamics.Core;

namespace Thermodynamics.Fixtures
{
    /// <summary>Explicit heating provider for historical thermal rigs; never compiled into production Core.</summary>
    public sealed class HistoricalThermalSettings : ThermalSettings
    {
        public bool EnableFriction = true;
        public bool EnableRecoveryHeating = true;
        public bool EnableReentryHeating = false;
        public float ReentryStartSpeed = 300f;
        public float ReentryHeatingScale = 0.001f;
        public float FrictionAtSpeedsAbove = 0f;
        public float FrictionScale = 0.001f;

        public HistoricalThermalSettings()
        {
            SurfaceHeatingGate = speed => EnableFriction && speed > FrictionAtSpeedsAbove;
            SurfaceHeating = (area, density, speed, ambient, temperature, capacity, seconds) =>
            {
                float potential = FrictionScale * density * speed * speed * speed * area;
                float heat = EnableRecoveryHeating
                    ? AeroHeating.BoundedWatts(potential, ambient, speed, temperature, capacity, seconds)
                    : potential;
                float excess = System.Math.Max(0f, speed - ReentryStartSpeed);
                return heat + (EnableReentryHeating
                    ? ReentryHeatingScale * density * excess * excess * excess * area : 0f);
            };
        }

        public new HistoricalThermalSettings Derive()
        {
            if (!(ReentryStartSpeed >= 0f) || float.IsInfinity(ReentryStartSpeed)) ReentryStartSpeed = 300f;
            if (!(ReentryHeatingScale >= 0f) || float.IsInfinity(ReentryHeatingScale)) ReentryHeatingScale = 0f;
            if (FrictionAtSpeedsAbove < 0f) FrictionAtSpeedsAbove = 0f;
            if (FrictionScale < 0f) FrictionScale = 0f;
            base.Derive();
            return this;
        }
    }
}

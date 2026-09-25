using System;
using RelativeTopSpeed;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Thermodynamics
{
    /// <summary>
    /// The read-only link to RelativeTopSpeed, which owns a ship's top speed when it is installed.
    /// Absent is the normal case and answers false; see aerodynamics.md, AR15.
    /// </summary>
    public static class RelativeTopSpeedLink
    {
        private static RtsApi api;

        /// <summary>Asks for RelativeTopSpeed's API; call once while the session loads.</summary>
        public static void Load()
        {
            if (api != null) return;
            api = new RtsApi();
            api.Load();
        }

        /// <summary>Releases the API; call while the session unloads.</summary>
        public static void Unload()
        {
            if (api == null) return;
            RtsApi leaving = api;
            api = null;
            leaving.Unload();
        }

        /// <summary>Whether RelativeTopSpeed is installed and has answered.</summary>
        public static bool IsReady
        {
            get { return api != null && api.IsReady; }
        }

        /// <summary>
        /// RelativeTopSpeed's cruise speed and speed ceiling for a grid, in m/s; false where it is not
        /// installed, does not govern this grid, or failed.
        /// </summary>
        public static bool TryLimits(IMyCubeGrid grid, out float cruise, out float ceiling)
        {
            cruise = 0f;
            ceiling = 0f;
            if (grid == null || !IsReady) return false;

            try
            {
                if (!api.IsGoverned(grid)) return false;
                cruise = api.GetEffectiveCruiseSpeed(grid);
                ceiling = api.GetEffectiveSpeedCeiling(grid);
                return true;
            }
            catch (Exception e)
            {
                Telemetry.Exception("RelativeTopSpeedLink.TryLimits", e);
                return false;
            }
        }

        /// <summary>
        /// The world's speed limit in m/s: RelativeTopSpeed's where it is installed, and the engine's own
        /// cap for the grid's size otherwise.
        /// </summary>
        public static float WorldSpeedLimit(bool largeGrid)
        {
            if (IsReady)
            {
                try
                {
                    return api.GetWorldSpeedLimit();
                }
                catch (Exception e)
                {
                    Telemetry.Exception("RelativeTopSpeedLink.WorldSpeedLimit", e);
                }
            }

            MyDefinitionManager definitions = MyDefinitionManager.Static;
            if (definitions == null || definitions.EnvironmentDefinition == null) return 0f;
            return largeGrid
                ? definitions.EnvironmentDefinition.LargeShipMaxSpeed
                : definitions.EnvironmentDefinition.SmallShipMaxSpeed;
        }
    }
}

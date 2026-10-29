using System;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private static readonly Guid StorageGuid = new Guid("f7cd64ae-9cd8-41f3-8e5d-3db992619343");

        private void Save()
        {
            // Native serialization can precede UpdateOnceBeforeFrame; preserve stored heat until
            // the existing blocks and their saved temperatures have actually been loaded.
            if (!Started || Entity.Storage == null) return;

            try
            {
                if (Telemetry.Enabled && Stats != null) Stats.SaveTime.Begin();

                string data = Simulation.Save();

                MyModStorageComponentBase storage = Entity.Storage;
                if (storage.ContainsKey(StorageGuid))
                {
                    storage[StorageGuid] = data;
                }
                else
                {
                    storage.Add(StorageGuid, data);
                }

                if (Telemetry.Enabled && Stats != null)
                {
                    Stats.Saves++;
                    Stats.SaveBytes += data.Length;
                    Stats.SaveTime.End();
                }
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.Save", e);
                MyLog.Default.Error("[" + Settings.Name + "] save failed: " + e);
            }
        }

        private void Load()
        {
            if (disabled || Simulation == null || Entity.Storage == null) return;

            try
            {
                if (!Entity.Storage.ContainsKey(StorageGuid)) return;

                if (Telemetry.Enabled && Stats != null)
                {
                    Stats.Loads++;
                    Stats.LoadTime.Begin();
                }

                string data = Entity.Storage[StorageGuid];
                int restored = Simulation.Load(data);

                if (Telemetry.Enabled && Stats != null)
                {
                    Stats.LoadBytes += data.Length;
                    Stats.BlocksRestored += restored;
                    Stats.RoomsRestored += Simulation.RoomsRestored;
                    Stats.LoadTime.End();
                }
            }
            catch (Exception e)
            {
                Telemetry.Exception("ThermalGrid.Load", e);
            }
        }
    }
}

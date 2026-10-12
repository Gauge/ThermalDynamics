using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace Thermodynamics
{
    public partial class Settings
    {
        public static Settings EnsureLoaded()
        {
            if (Instance != null) return Instance;

            Instance = CanReadWorldStorage() ? Load() : GetDefaults();
            Instance.Apply();
            return Instance;
        }

        private static bool CanReadWorldStorage()
        {
            try
            {
                return MyAPIGateway.Utilities != null
                    && MyAPIGateway.Session != null
                    && MyAPIGateway.Session.IsServer;
            }
            catch
            {
                return false;
            }
        }

        public static Settings Load()
        {
            Settings settings = GetDefaults();
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    Settings loaded = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);

                    if (text.IndexOf("MaxLinkVisitsPerStep", StringComparison.Ordinal) >= 0)
                    {
                        MyLog.Default.Info("[" + Name + "] MaxLinkVisitsPerStep is now"
                            + " MaxElementVisitsPerStep and counts nodes as well as links;"
                            + " the old value was not carried over. Default "
                            + settings.MaxElementVisitsPerStep + " is in force.");
                    }

                    if (loaded.Version != CurrentVersion)
                    {
                        MyLog.Default.Info("[" + Name + "] config version " + loaded.Version
                            + " replaced with " + CurrentVersion);
                        Save(settings);
                    }
                    else
                    {
                        settings = loaded;
                    }
                }
                else
                {
                    Save(settings);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] failed to load configuration, using defaults\n" + e);
            }

            settings.Clamp();
            return settings;
        }

        public static bool SavePending;

        public static void FlushPending()
        {
            if (!SavePending || Instance == null) return;

            try
            {
                if (MyAPIGateway.Session == null || !MyAPIGateway.Session.IsServer) return;

                SavePending = false;
                Save(Instance);
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] deferred save failed\n" + e);
            }
        }

        public static void Save(Settings settings)
        {
            try
            {
                TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
            }
            catch (Exception e)
            {
                MyLog.Default.Info("[" + Name + "] failed to save settings\n" + e);
            }
        }
    }
}

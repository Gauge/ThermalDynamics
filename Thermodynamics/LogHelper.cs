using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Utils;

namespace ThermalDynamics
{
    /// <summary>
    /// Cloned from https://github.com/ArthurGamerHD/Adk/blob/master/Adk.Utils/LogHelper.cs
    /// </summary>
    public static class LogHelper
    {
        static readonly HashSet<string> LoggedOnce = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string Prefix = GetPrefix();

        private static string GetPrefix() => MyAPIGateway.Utilities.GamePaths.ModScopeName.Split('_').Last();

        public static readonly HashSet<MyLogSeverity> Severity = Enum.GetValues(typeof(MyLogSeverity)).Cast<MyLogSeverity>().ToHashSet();

        static LogHelper()
        {
#if !DEBUG
            Severity.Remove(MyLogSeverity.Debug);
#endif
        }
        
        /// <summary>
        /// Drops repeated log messages with the same key, only logs the first one.
        /// </summary>
        /// <param name="key">Log Key</param>
        /// <param name="message">Log Message</param>
        public static void LogOnce(string key, string message)
        {
            if (!LoggedOnce.Add(key))
                return;

            LogInfo(message);
        }
        
        /// <summary>
        /// Logs a message with the Info severity level, prefixed with the mod's name.
        /// </summary>
        /// <param name="message">Log Message</param>
        public static void LogInfo(string message) => Log(MyLogSeverity.Info, message);
        
        /// <summary>
        /// Logs a formatted message with the Info severity level, prefixed with the mod's name.
        /// </summary>
        /// <param name="message">Log Message</param>
        /// <param name="args">Format Arguments</param>
        public static void LogInfo(string message, params object[] args) => Log(MyLogSeverity.Info, message, args);
        
        /// <summary>
        /// Logs a message with the specified severity level, prefixed with the mod's name.
        /// </summary>
        /// <param name="severity">Log Severity</param>
        /// <param name="message">Log Message</param>
        public static void Log(MyLogSeverity severity, string message)
        {
            if(Severity.Contains(severity))
                MyLog.Default.Log(severity, $"[{Prefix}] " + message.Replace("{", "{{").Replace("}", "}}"));
        }

        /// <summary>
        /// Logs a formatted message with the specified severity level, prefixed with the mod's name.
        /// </summary>
        /// <param name="severity">Log Severity</param>
        /// <param name="message">Log Message</param>
        /// <param name="args">Format Arguments</param>
        public static void Log(MyLogSeverity severity, string message, params object[] args)
        {
            if(Severity.Contains(severity))
                MyLog.Default.Log(severity, $"[{Prefix}] " + message, args);
        }
    }
}
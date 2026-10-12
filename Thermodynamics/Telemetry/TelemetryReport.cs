using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thermodynamics.Core;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static partial class TelemetryReport
    {
        private const int DetailedGridLimit = 25;


        public static void Write(string reason)
        {
            string stamp = Telemetry.StartedUtc.ToString("yyyyMMdd_HHmmss");


            string report = BuildReport(reason);

            MyLog.Default.Info("[" + Settings.Name + "] [Telemetry] " + BuildLogSummary(reason));


            bool wrote = TryWrite("Thermodynamics_Telemetry_" + stamp + ".log", report);
            TryWrite("Thermodynamics_BlockTypes_" + stamp + ".csv", BuildBlockTypeCsv());
            TryWrite("Thermodynamics_Grids_" + stamp + ".csv", BuildGridCsv());
            TryWrite("Thermodynamics_Surfaces_" + stamp + ".csv", BuildSurfaceCsv());
            TryWrite("Thermodynamics_Rooms_" + stamp + ".csv", BuildRoomCsv());
            TryWrite("Thermodynamics_Environment_" + stamp + ".csv", BuildEnvironmentCsv());


            if (PlanetProbes.Rows.Count > 0)
            {
                TryWrite("Thermodynamics_PlanetProbes_" + stamp + ".csv", PlanetProbes.Csv());
            }

            if (!wrote)
            {
                MyLog.Default.Info("[" + Settings.Name + "] [Telemetry] world storage unavailable, full report follows\n" + report);
            }
        }


        private static bool TryWrite(string filename, string content)
        {
            try
            {
                TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(filename, typeof(TelemetryReport));
                writer.Write(content);
                writer.Flush();
                writer.Close();

                MyLog.Default.Info("[" + Settings.Name + "] [Telemetry] wrote " + filename
                    + " (" + content.Length.ToString("n0") + " chars)");
                return true;
            }
            catch (Exception e)
            {
                MyLog.Default.Warning("[" + Settings.Name + "] [Telemetry] could not write " + filename + ": " + e.Message);
                return false;
            }
        }


        private static string BuildLogSummary(string reason)
        {
            long liveGrids = 0;
            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                if (!Telemetry.Grids[i].IsClosed) liveGrids++;
            }

            return "session ended (" + reason + ") after " + Telemetry.SessionSeconds.ToString("n1")

                + "s: " + Telemetry.GridsSeen + " grids (" + liveGrids + " alive at close), "
                + Telemetry.BlockTypes.Count + " block types, "
                + Telemetry.CellUpdatesObserved.ToString("n0") + " cell updates, "
                + Telemetry.Anomalies.Count + " anomaly kinds";
        }



        private static string BuildReport(string reason)
        {

            StringBuilder sb = new StringBuilder(64 * 1024);

            WriteHeader(sb, reason);
            WriteWorld(sb);
            WriteSettings(sb);
            WriteSessionTotals(sb);
            WriteAnomalies(sb);
            AppendClimate(sb);
            WritePerformance(sb);
            WriteOverlay(sb);
            Telemetry.Vision.Write(sb);
            WriteSubsteps(sb);
            WriteGridTable(sb);
            WriteGridDetails(sb);
            WriteBlockTypes(sb);
            WriteConsistency(sb);

            sb.Append("\n--- end of report ---\n");
            return sb.ToString();
        }


        private static void AppendCoolantAndPumps(StringBuilder sb, GridTelemetry g)
        {
            bool loops = g.LoopTemperature.Count > 0;
            bool pumps = g.HeatPumps.Count > 0 && g.HeatPumps.Max > 0;
            if (!loops && !pumps) return;

            sb.Append("\n    coolant and heat pumps\n");

            if (loops)
            {
                Field(sb, "  loop temperature K", g.LoopTemperature.Format("n1"));
                Field(sb, "  drawn from blocks W", g.LoopWattsAbsorbed.Format("n0"));
                Field(sb, "  shed into blocks W", g.LoopWattsRejected.Format("n0"));

                Field(sb, "  net into the fluid W",
                    (g.LoopWattsAbsorbed.Mean - g.LoopWattsRejected.Mean).ToString("n0"));
                Field(sb, "  pipes per loop", g.LoopPipes.Format("n0"));
                Field(sb, "  links per loop", g.LoopLinks.Format("n0"));

                if (g.LoopWattsAbsorbed.Max <= 0.0)
                {
                    sb.Append("      !! every loop on this grid carried nothing: check that a sink\n");
                    sb.Append("         face is against something hotter than the coolant\n");
                }
            }
            else
            {
                Field(sb, "  loops", "none");
            }

            if (!pumps) return;

            Field(sb, "  heat pumps", g.HeatPumps.Format("n0"));
            Field(sb, "  of which running", g.HeatPumpsRunning.Format("n0"));

            if (g.HeatPumpUnconnectedSamples > 0)
                Field(sb, "  idle: nothing on a face", g.HeatPumpUnconnectedSamples.ToString("n0") + " samples");
            if (g.HeatPumpDisabledSamples > 0)
                Field(sb, "  idle: switched off", g.HeatPumpDisabledSamples.ToString("n0") + " samples");
            if (g.HeatPumpStarvedSamples > 0)
                Field(sb, "  idle: no power", g.HeatPumpStarvedSamples.ToString("n0") + " samples");

            if (g.HeatPumpLiftWatts.Count == 0)
            {
                sb.Append("      !! no heat pump on this grid ran at all\n");
                return;
            }

            Field(sb, "  lifting W", g.HeatPumpLiftWatts.Format("n0"));
            Field(sb, "  drawing W", g.HeatPumpPowerWatts.Format("n0"));
            Field(sb, "  rejecting W", g.HeatPumpRejectedWatts.Format("n0"));
            Field(sb, "  coefficient", g.HeatPumpCoefficient.Format("n2"));

            long limited = g.HeatPumpLimitedByRating + g.HeatPumpLimitedByCarnotOrHeat;
            if (limited > 0)
            {
                Field(sb, "  bound by its rating",
                    Percent(g.HeatPumpLimitedByRating, limited) + " of running samples");
                Field(sb, "  bound by Carnot or heat",
                    Percent(g.HeatPumpLimitedByCarnotOrHeat, limited) + " of running samples");
            }

            if (g.HeatPumpCoefficient.Count > 0 && g.HeatPumpCoefficient.Mean < 1.0)
            {
                sb.Append("      note: mean coefficient below 1 — these pumps spend more energy than\n");
                sb.Append("            they move, so the gap they lift across is very wide\n");
            }
        }


        private static string Percent(long part, long whole)
        {
            if (whole <= 0) return "-";
            return (100.0 * part / whole).ToString("n1") + " %";
        }


        private static void Section(StringBuilder sb, string title)
        {
            sb.Append('\n').Append(title).Append('\n');
            sb.Append(new string('=', title.Length)).Append('\n');
        }


        private static void Field(StringBuilder sb, string name, object value)
        {
            sb.Append("  ").Append(name.PadRight(30)).Append(name.Length >= 30 ? " " : "").Append(value).Append('\n');
        }


        private static void WriteRoomMapping(StringBuilder sb, GridTelemetry g)
        {
            sb.Append("\n    room mapping (min / mean / max)\n");

            if (g.MapperCompletions == 0)
            {
                Field(sb, "  mapped", "never (no pass completed)");
                return;
            }

            Field(sb, "  sealed rooms", g.RoomCount.Format("n0"));
            Field(sb, "  exterior cells", g.ExternalCells.Format("n0"));
            Field(sb, "  structure cells", g.SolidCells.Format("n0"));
            Field(sb, "  room cells", g.RoomCells.Format("n0"));
            Field(sb, "  cells outdoors w/ block", g.OpenBlockCells.Format("n0"));
            Field(sb, "  sealed struct. read open", g.LeakedCells.Format("n0"));

            if (!g.HasAudit) return;

            Core.RoomAudit audit = g.LastAudit;
            Field(sb, "  last pass: rooms", DescribeRooms(audit));
            Field(sb, "  last pass: search box", audit.SearchVolume.ToString("n0") + " cells");
            Field(sb, "  last pass: block cells", audit.BlockCells.ToString("n0"));
            Field(sb, "  last pass: unsealed faces", audit.UnsealedBlockFaces.ToString("n0")
                + " of " + (audit.BlockCells * Core.Face.Count).ToString("n0"));
            Field(sb, "  blocks sealing nothing", audit.BlocksSealingNothing.ToString("n0"));

            if (audit.Examples != null && audit.Examples.Count > 0)
            {
                sb.Append("\n    block cells the map left outdoors\n");
                for (int i = 0; i < audit.Examples.Count; i++)
                {
                    sb.Append("      ").Append(audit.Examples[i]).Append('\n');
                }
            }

            WriteRooms(sb, g);
        }


        private static void WriteRooms(StringBuilder sb, GridTelemetry g)
        {
            if (!g.RoomScanRan && g.Rooms.Count == 0) return;

            int mapped = 0;
            int lost = 0;
            int lostWithVent = 0;
            int dry = 0;

            for (int i = 0; i < g.Rooms.Count; i++)
            {
                if (g.Rooms[i].Kind == "lost")
                {
                    lost++;
                    if (g.Rooms[i].VentPressurised) lostWithVent++;
                }
                else
                {
                    mapped++;
                    if (g.Rooms[i].Disagreement) dry++;
                }
            }

            sb.Append("\n    compartments\n");
            Field(sb, "  found by this model", mapped.ToString("n0"));
            Field(sb, "  held only by the game", lost.ToString("n0")
                + (lostWithVent > 0 ? "  (" + lostWithVent + " with a vent reporting pressurised)" : ""));

            Field(sb, "  found, dry, air in game", dry.ToString("n0")
                + (dry > 0 ? "  <-- the game has air in these and this model runs none" : ""));

            if (g.RoomScanTruncated)
            {
                Field(sb, "  scan", "TRUNCATED at the cell limit - the list below is partial");
            }

            if (g.Rooms.Count == 0)
            {
                Field(sb, "  rooms", "none");
                return;
            }

            sb.Append("\n      kind   idx  cells      volume  press   air kg      K links  seal  gameO2  vent  O2   leaks\n");

            for (int i = 0; i < g.Rooms.Count && i < MaxRoomsReported; i++)
            {
                RoomRow row = g.Rooms[i];

                sb.Append("      ");
                sb.Append(row.Kind.PadRight(7));
                sb.Append(row.Index.ToString().PadLeft(3));
                sb.Append(row.CellCount.ToString("n0").PadLeft(7));
                sb.Append(row.Volume.ToString("n1").PadLeft(12));
                sb.Append(row.Pressure.ToString("n2").PadLeft(7));
                sb.Append(row.AirMass.ToString("n1").PadLeft(9));
                sb.Append(row.TemperatureKelvin.ToString("n1").PadLeft(7));
                sb.Append(row.LinkCount.ToString("n0").PadLeft(6));
                sb.Append((row.GameAirtight ? "  yes" : "   no").PadLeft(6));
                sb.Append((row.GameOxygen < 0f ? "   -" : row.GameOxygen.ToString("n2")).PadLeft(8));
                sb.Append((row.VentPressurised ? "  yes" : (string.IsNullOrEmpty(row.Vents) ? "    -" : "   no")).PadLeft(6));
                sb.Append((row.OxygenLevel < 0f ? "   -" : row.OxygenLevel.ToString("n2")).PadLeft(6));
                sb.Append(row.LeakCount.ToString("n0").PadLeft(7));
                if (row.Disagreement) sb.Append("  <-- the game has air here and this model does not");
                sb.Append('\n');

                if (!string.IsNullOrEmpty(row.Vents))
                {
                    sb.Append("               vents: ").Append(row.Vents).Append('\n');
                }

                if (!string.IsNullOrEmpty(row.LeakingBlocks))
                {
                    sb.Append("               leaking through: ").Append(row.LeakingBlocks).Append('\n');
                }
            }

            if (g.Rooms.Count > MaxRoomsReported)
            {
                Field(sb, "  not listed",
                    (g.Rooms.Count - MaxRoomsReported).ToString("n0") + " more, see the rooms CSV");
            }
        }

        private const int MaxRoomsReported = 60;


        private static string DescribeRooms(Core.RoomAudit audit)
        {
            if (audit.RoomCount == 0) return "none";
            if (audit.RoomSizes == null || audit.RoomSizes.Count == 0)
            {
                return audit.RoomCount + " (" + audit.RoomCells + " cells)";
            }


            StringBuilder sb = new StringBuilder();
            sb.Append(audit.RoomCount).Append(audit.RoomCount == 1 ? " room, cells: " : " rooms, cells: ");
            for (int i = 0; i < audit.RoomSizes.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(audit.RoomSizes[i]);
            }
            if (audit.RoomSizes.Count < audit.RoomCount) sb.Append(", ...");
            return sb.ToString();
        }


        private static string FaceFractions(float[] byFace)
        {
            if (byFace == null) return "-";


            StringBuilder sb = new StringBuilder();
            for (int face = 0; face < Core.Face.Count && face < byFace.Length; face++)
            {
                if (face > 0) sb.Append(' ');
                sb.Append(Core.Face.Name(face).Substring(0, 1)).Append(':').Append(byFace[face].ToString("n2"));
            }
            return sb.ToString();
        }


        private static void WriteHeader(StringBuilder sb, string reason)
        {
            sb.Append("Thermodynamics telemetry report\n");
            sb.Append("===============================\n");

            Field(sb, "reason", reason);
            Field(sb, "started (utc)", Telemetry.StartedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            Field(sb, "ended (utc)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            Field(sb, "real duration", Telemetry.SessionSeconds.ToString("n1") + " s ("
                + (Telemetry.SessionSeconds / 60.0).ToString("n1") + " min)");
            Field(sb, "in-game start", Telemetry.GameStartDate.ToString("yyyy-MM-dd HH:mm:ss"));
            Field(sb, "in-game end", Telemetry.GameEndDate.ToString("yyyy-MM-dd HH:mm:ss"));
            Field(sb, "in-game elapsed", (Telemetry.GameEndDate - Telemetry.GameStartDate).ToString());
            Field(sb, "world", Telemetry.WorldName);
            Field(sb, "world path", Telemetry.SessionPath);
            Field(sb, "online mode", Telemetry.OnlineMode);
            Field(sb, "max players", Telemetry.MaxPlayers);
            Field(sb, "server", Telemetry.IsServer);
            Field(sb, "dedicated", Telemetry.IsDedicated);
            Field(sb, "multiplayer", Telemetry.IsMultiplayer);
            Field(sb, "sample stride", "1 in " + Telemetry.SampleStride + " cell updates");
        }


        private static void WriteWorld(StringBuilder sb)
        {
            Section(sb, "World");

            Field(sb, "game version", Telemetry.GameVersion);
            Field(sb, "mods loaded", Telemetry.Mods.Count);
            for (int i = 0; i < Telemetry.Mods.Count; i++)
            {
                Field(sb, "  " + (i + 1).ToString("n0"), Telemetry.Mods[i]);
            }


            List<string> conflicts = WorldConflicts();
            for (int i = 0; i < conflicts.Count; i++)
            {
                sb.Append("      !! ").Append(conflicts[i]).Append('\n');
            }

            if (Telemetry.WorldSettingsRows.Count == 0)
            {
                sb.Append("  (the world's settings were not readable)\n");
                return;
            }

            sb.Append("\n  world settings\n");
            for (int i = 0; i < Telemetry.WorldSettingsRows.Count; i++)
            {
                KeyValuePair<string, string> row = Telemetry.WorldSettingsRows[i];
                Field(sb, "  " + row.Key, row.Value.Length == 0 ? "-" : row.Value);
            }
        }


        private static List<string> WorldConflicts()
        {
            Settings s = Telemetry.SettingsSnapshot;

            WorldSettings.ModFeatures features = new WorldSettings.ModFeatures();
            features.Damage = s == null || s.EnableDamage;
            features.RoomAir = s == null || s.EnableRoomAir;
            features.Persistence = true;

            return WorldSettings.Conflicts(Telemetry.WorldSettingsRows, features);
        }


        private static void WriteSettings(StringBuilder sb)
        {
            Section(sb, "Settings in force");

            Settings s = Telemetry.SettingsSnapshot;
            if (s == null)
            {
                sb.Append("  (settings were never captured)\n");
                return;
            }

            Field(sb, "Version", s.Version);
            Field(sb, "StepsPerSecond", s.StepsPerSecond);

            List<string> names = Settings.Names();
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                float value = s.GetValue(name);

                if (Settings.IsFlag(name)) Field(sb, name, value != 0f);

                else Field(sb, name, value);
            }
        }


        private static void WriteSessionTotals(StringBuilder sb)
        {
            Section(sb, "Session totals");

            long added = 0, removed = 0, splits = 0, merges = 0, doorChanges = 0;
            long mapperPasses = 0, surfaceRecalcs = 0, saves = 0, loads = 0;
            long saveBytes = 0, loadBytes = 0, damageEvents = 0, simFrames = 0;
            long clampedSteps = 0, nodeUpdates = 0;
            int gridsWithLoops = 0, gridsWithWorkingLoops = 0;
            int gridsWithHeatPumps = 0, gridsWithWorkingPumps = 0;
            double totalDamage = 0;
            long liveGrids = 0;
            int peakCells = 0;

            for (int i = 0; i < Telemetry.Grids.Count; i++)
            {
                GridTelemetry g = Telemetry.Grids[i];
                added += g.BlocksAdded;
                removed += g.BlocksRemoved;
                splits += g.Splits;
                merges += g.Merges;
                doorChanges += g.DoorStateChanges;
                mapperPasses += g.MapperCompletions;
                surfaceRecalcs += g.SurfaceRecalcs;
                saves += g.Saves;
                loads += g.Loads;
                saveBytes += g.SaveBytes;
                loadBytes += g.LoadBytes;
                damageEvents += g.DamageEvents;
                totalDamage += g.TotalDamage;
                simFrames += g.SimulationSteps;
                if (g.CoolantLoops.Count > 0 && g.CoolantLoops.Max > 0) gridsWithLoops++;
                if (g.LoopWattsAbsorbed.Count > 0 && g.LoopWattsAbsorbed.Max > 0) gridsWithWorkingLoops++;
                if (g.HeatPumps.Count > 0 && g.HeatPumps.Max > 0) gridsWithHeatPumps++;
                if (g.HeatPumpLiftWatts.Count > 0 && g.HeatPumpLiftWatts.Max > 0) gridsWithWorkingPumps++;
                clampedSteps += g.ClampedSteps;
                nodeUpdates += g.NodeUpdates;
                if (!g.IsClosed) liveGrids++;
                if (g.PeakCellCount > peakCells) peakCells = g.PeakCellCount;
            }

            Field(sb, "frames observed", Telemetry.FramesObserved.ToString("n0"));
            Field(sb, "grid simulation steps", simFrames.ToString("n0"));
            Field(sb, "block updates", nodeUpdates.ToString("n0"));
            Field(sb, "block updates / real second", Telemetry.SessionSeconds <= 0
                ? "-"
                : (nodeUpdates / Telemetry.SessionSeconds).ToString("n0"));
            Field(sb, "blocks observed (sampled)", Telemetry.CellUpdatesObserved.ToString("n0"));
            Field(sb, "steps clamped by substep cap", clampedSteps.ToString("n0"));
            Field(sb, "grids seen", Telemetry.GridsSeen);
            Field(sb, "grids alive at close", liveGrids);
            Field(sb, "largest grid (cells)", peakCells.ToString("n0"));
            Field(sb, "block types seen", Telemetry.BlockTypes.Count);
            Field(sb, "block models built", ThermalBlockCatalog.ModelCount
                + (ThermalBlockCatalog.MountFallbacks > 0
                    ? " (" + ThermalBlockCatalog.MountFallbacks + " with no usable mount points)"
                    : ""));
            Field(sb, "cross-grid bridges", ThermalBridges.Count);
            Field(sb, "blocks added", added.ToString("n0"));
            Field(sb, "blocks removed", removed.ToString("n0"));
            Field(sb, "grid splits", splits);
            Field(sb, "grid merges", merges);
            Field(sb, "door state changes", doorChanges.ToString("n0"));
            Field(sb, "surface refreshes", surfaceRecalcs.ToString("n0"));
            Field(sb, "room mapper passes", mapperPasses.ToString("n0"));
            Field(sb, "grids with a coolant loop", gridsWithLoops
                + (gridsWithLoops > 0 ? " (" + gridsWithWorkingLoops + " carrying heat)" : ""));
            Field(sb, "grids with a heat pump", gridsWithHeatPumps
                + (gridsWithHeatPumps > 0 ? " (" + gridsWithWorkingPumps + " running)" : ""));
            Field(sb, "critical damage events", damageEvents.ToString("n0"));
            Field(sb, "total heat damage", totalDamage.ToString("n1"));
            Field(sb, "saves / bytes", saves + " / " + saveBytes.ToString("n0"));
            Field(sb, "loads / bytes", loads + " / " + loadBytes.ToString("n0"));

            if (Telemetry.GridRecordsDropped > 0)
                Field(sb, "grid records dropped", Telemetry.GridRecordsDropped + " (cap " + Telemetry.MaxGridRecords + ")");
            if (Telemetry.BlockTypeRecordsDropped > 0)
                Field(sb, "block type lookups dropped", Telemetry.BlockTypeRecordsDropped);
            if (Telemetry.AnomalyKindsDropped > 0)
                Field(sb, "anomaly kinds dropped", Telemetry.AnomalyKindsDropped);
        }


        private static void WriteAnomalies(StringBuilder sb)
        {
            Section(sb, "Anomalies");

            if (Telemetry.Anomalies.Count == 0)
            {
                sb.Append("  none recorded\n");
                return;
            }


            List<AnomalyRecord> records = new List<AnomalyRecord>(Telemetry.Anomalies.Values);

            records.Sort(delegate (AnomalyRecord a, AnomalyRecord b)
            {
                if (a.IsFault != b.IsFault) return a.IsFault ? -1 : 1;
                return b.Count.CompareTo(a.Count);
            });

            for (int i = 0; i < records.Count; i++)
            {
                AnomalyRecord r = records[i];
                sb.Append(r.IsFault ? "  !! " : "  ").Append(r.Kind).Append(" x").Append(r.Count.ToString("n0")).Append('\n');
                sb.Append("      first at ").Append(r.FirstSeconds.ToString("n1")).Append("s: ").Append(r.FirstExample).Append('\n');
                if (r.Count > 1)
                {
                    sb.Append("      last  at ").Append(r.LastSeconds.ToString("n1")).Append("s: ").Append(r.LastExample).Append('\n');
                }
            }
        }

        private static void WriteGridDetails(StringBuilder sb)
        {

            List<GridTelemetry> grids = SortedGrids();
            int limit = Math.Min(grids.Count, DetailedGridLimit);

            if (limit == 0) return;

            Section(sb, "Grid detail (" + limit + " largest of " + grids.Count + ")");

            for (int i = 0; i < limit; i++)
            {
                GridTelemetry g = grids[i];

                sb.Append("\n  ").Append(g.Name).Append("  [").Append(g.EntityId).Append("]\n");
                sb.Append("  ").Append(new string('-', 60)).Append('\n');

                Field(sb, "grid size", g.GridSize + " (" + g.GridSizeMeters.ToString("n2") + " m)");
                Field(sb, "static", g.IsStatic);
                Field(sb, "lifetime", g.LifetimeSeconds.ToString("n1") + " s"
                    + (g.IsClosed ? " (closed at " + g.ClosedAtSeconds.ToString("n1") + "s)" : " (still alive)"));

                sb.Append("\n    structure (min / mean / max)\n");
                Field(sb, "  thermal cells", g.CellCount.Format("n0"));
                Field(sb, "  grid blocks", g.BlockCount.Format("n0"));
                Field(sb, "  conduction links", g.NeighborLinks.Format("n0"));
                Field(sb, "  sealed rooms", g.RoomCount.Format("n0"));
                Field(sb, "  surface entries", g.SurfaceEntries.Format("n0"));
                Field(sb, "  coolant loops", g.CoolantLoops.Format("n0"));
                Field(sb, "  RecentlyRemoved size", g.RecentlyRemovedSize.Format("n0"));
                Field(sb, "  mapper queue", g.MapperQueueDepth.Format("n0"));

                WriteRoomMapping(sb, g);

                sb.Append("\n    simulation\n");
                Field(sb, "  simulation steps", g.SimulationSteps.ToString("n0"));
                Field(sb, "  node updates", g.NodeUpdates.ToString("n0"));
                Field(sb, "  nodes sampled", g.SampledNodes.ToString("n0"));
                Field(sb, "  nodes per step", g.NodesPerStep.Format("n0"));
                Field(sb, "  solver substeps", g.Substeps.Format("n2"));
                Field(sb, "  substeps required", g.RequiredSubsteps.Format("n2"));
                Field(sb, "  steps clamped", g.ClampedSteps.ToString("n0"));
                Field(sb, "  blocks raised by cap", g.FlooredNodes.Count == 0
                    ? "-"
                    : g.FlooredNodes.Format("n0"));
                Field(sb, "  simulation rate", g.SimulationRate.Count == 0
                    ? "-"
                    : (100.0 * g.SimulationRate.Mean).ToString("n1") + " % ("
                        + g.SimulatedSecondsSkipped.ToString("n1") + " s not advanced)");
                Field(sb, "  hottest block T", g.HottestBlockTemperature.Format("n1"));
                Field(sb, "  peak temperature", FormatPeak(g.PeakTemperature) + "  " + g.PeakTemperatureBlock);
                Field(sb, "  critical blocks", g.CriticalBlocks.Format("n0"));
                Field(sb, "  damage events", g.DamageEvents.ToString("n0"));
                Field(sb, "  total damage", g.TotalDamage.ToString("n1"));

                WriteGridSubsteps(sb, g);

                AppendCoolantAndPumps(sb, g);

                sb.Append("\n    environment\n");
                Field(sb, "  planets visited", g.PlanetList);
                Field(sb, "  ambient K", g.AmbientTemperature.Format("n1"));
                Field(sb, "  air density", g.AirDensity.Format("n4"));
                Field(sb, "  atmosphere factor", g.AtmosphereFactor.Format("n4"));
                Field(sb, "  wind speed m/s", g.WindSpeed.Format("n2"));
                Field(sb, "  convection coeff", g.ConvectionCoefficient.Format("n3"));
                Field(sb, "  vented W", g.VentedWatts.Format("n0"));
                Field(sb, "  made W", g.HeatGainWatts.Format("n0"));
                Field(sb, "  effective solar W", g.EffectiveSolarEnergy.Format("n1"));
                Field(sb, "  occluded share", g.OccludedShare.Format("n3"));
                Field(sb, "  grid speed m/s", g.Speed.Format("n2"));
                Field(sb, "  sun occluded", (100.0 * g.OccludedFraction).ToString("n1") + " % of "
                    + g.EnvironmentSamples.ToString("n0") + " samples");
                Field(sb, "  in atmosphere", (100.0 * g.AtmosphereFraction).ToString("n1") + " %");

                sb.Append("\n    events\n");
                Field(sb, "  blocks added / removed", g.BlocksAdded.ToString("n0") + " / " + g.BlocksRemoved.ToString("n0"));
                Field(sb, "  blocks ignored", g.BlocksIgnored.ToString("n0"));
                Field(sb, "  foreign block events", g.ForeignBlockEvents.ToString("n0"));
                Field(sb, "  splits / merges", g.Splits + " / " + g.Merges);
                Field(sb, "  door state changes", g.DoorStateChanges.ToString("n0"));
                Field(sb, "  surface refreshes", g.SurfaceRecalcs.ToString("n0"));
                Field(sb, "  room pressure sweeps", g.RoomPressureSweeps.ToString("n0")
                    + " (" + g.RoomPressureRoomVisits.ToString("n0") + " compartments, "
                    + g.RoomPressureGameQueries.ToString("n0") + " game calls)");
                Field(sb, "  of which read vents", g.RoomPressureVentScans.ToString("n0")
                    + " (" + g.RoomPressureVentsWalked.ToString("n0") + " vents walked)");
                Field(sb, "  mapper passes completed", g.MapperCompletions.ToString("n0"));
                Field(sb, "  blocks restored on load", g.BlocksRestored.ToString("n0"));
                Field(sb, "  rooms restored on load", g.RoomsRestored.ToString("n0"));
                Field(sb, "  saves / loads", g.Saves + " / " + g.Loads);
                Field(sb, "  save / load bytes", g.SaveBytes.ToString("n0") + " / " + g.LoadBytes.ToString("n0"));

                sb.Append("\n    cost\n");
                TimingStat.WriteHeader(sb, "  path");
                g.SimulationTime.WriteRow(sb);
                g.Profiler.Topology.WriteRow(sb);
                g.Profiler.RoomMapping.WriteRow(sb);
                g.Profiler.Exposure.WriteRow(sb);
                g.Profiler.RoomPressure.WriteRow(sb);
                g.Profiler.Solver.WriteRow(sb);
                g.SolarTime.WriteRow(sb);
                g.SaveTime.WriteRow(sb);
                g.LoadTime.WriteRow(sb);

                Field(sb, "  ticks", g.SimulationTime.Calls.ToString("n0")
                    + (g.FirstTickFrame < 0
                        ? ""
                        : " over frames " + g.FirstTickFrame.ToString("n0")
                            + " .. " + g.LastTickFrame.ToString("n0")));

                sb.Append("\n    final temperature distribution\n");
                g.FinalTemperatures.Write(sb, "      ", "K");
            }
        }


        private static void WriteBlockTypes(StringBuilder sb)
        {
            Section(sb, "Block types (" + Telemetry.BlockTypes.Count + ")");

            if (Telemetry.BlockTypes.Count == 0)
            {
                sb.Append("  none\n");
                return;
            }


            List<BlockTypeTelemetry> types = SortedBlockTypes();

            sb.Append("  ")
              .Append("subtype".PadRight(40))
              .Append("placed".PadLeft(8))
              .Append("live".PadLeft(7))
              .Append("updates".PadLeft(12))
              .Append("mean T".PadLeft(10))
              .Append("max T".PadLeft(10))
              .Append("peak T".PadLeft(10))
              .Append("crit".PadLeft(9))
              .Append("damage".PadLeft(11))
              .Append('\n');

            for (int i = 0; i < types.Count; i++)
            {
                BlockTypeTelemetry t = types[i];
                sb.Append("  ")
                  .Append(Truncate(t.Name, 39).PadRight(40))
                  .Append(t.Placed.ToString("n0").PadLeft(8))
                  .Append(t.Live.ToString("n0").PadLeft(7))
                  .Append(t.TotalUpdates.ToString("n0").PadLeft(12))
                  .Append(t.Temperature.Mean.ToString("n1").PadLeft(10))
                  .Append(t.Temperature.SafeMax.ToString("n1").PadLeft(10))
                  .Append(FormatPeak(t.PeakTemperature).PadLeft(10))
                  .Append(t.CriticalUpdates.ToString("n0").PadLeft(9))
                  .Append(t.TotalDamage.ToString("n1").PadLeft(11))
                  .Append('\n');
            }

            Section(sb, "Block type detail");

            for (int i = 0; i < types.Count; i++)
            {
                BlockTypeTelemetry t = types[i];

                sb.Append("\n  ").Append(t.Name).Append("   (").Append(t.DefinitionId.TypeId).Append(")\n");
                sb.Append("  ").Append(new string('-', 60)).Append('\n');

                if (t.Definition != null)
                {
                    Core.BlockThermalProperties d = t.Definition;
                    Field(sb, "  size", t.Size.ToString());
                    Field(sb, "  Conductivity", d.Conductivity);
                    Field(sb, "  SpecificHeat", d.SpecificHeat);
                    Field(sb, "  Emissivity", d.Emissivity);
                    Field(sb, "  ExposedSurfaceMultiplier", d.ExposedSurfaceMultiplier);
                    Field(sb, "  ProducerWasteEnergy", d.ProducerWasteEnergy);
                    Field(sb, "  ConsumerWasteEnergy", d.ConsumerWasteEnergy);
                    Field(sb, "  CriticalTemperature", d.CriticalTemperature);
                    Field(sb, "  OverheatDamagePerKelvin", d.OverheatDamagePerKelvin);
                }

                if (t.HasSurfaceProfile)
                {
                    Field(sb, "  seals faces", t.FullySealingFaces + " of 6   " + FaceFractions(t.SealFractionByFace));
                    Field(sb, "  mounts faces", t.MountingFaces + " of 6   " + FaceFractions(t.MountFractionByFace));
                    Field(sb, "  observed with seal off", t.UnsealedByDoorState.ToString("n0") + " (open door)");
                }

                Field(sb, "  placed / removed / live", t.Placed + " / " + t.Removed + " / " + t.Live
                    + " (peak " + t.PeakLive + ")");
                Field(sb, "  updates (all / sampled)", t.TotalUpdates.ToString("n0") + " / " + t.SampledUpdates.ToString("n0"));
                Field(sb, "  mass kg", t.Mass.Format("n0"));
                Field(sb, "  thermal mass J/K", t.ThermalMass.Format("n0"));
                Field(sb, "  exposed surfaces", t.ExposedSurfaces.Format("n2"));
                Field(sb, "  exposed area m2", t.ExposedSurfaceArea.Format("n2"));
                Field(sb, "  temperature K", t.Temperature.Format("n1"));
                Field(sb, "  peak temperature K", FormatPeak(t.PeakTemperature) + " on grid " + t.PeakTemperatureGrid);
                Field(sb, "  dT per step", t.DeltaTemperature.Format("n5"));
                Field(sb, "  conduction W", t.ConductionWatts.Format("n2"));
                Field(sb, "  radiation W", t.RadiationWatts.Format("n2"));
                Field(sb, "  convection W", t.ConvectionWatts.Format("n2"));
                Field(sb, "  solar W", t.SolarWatts.Format("n2"));
                Field(sb, "  friction W", t.FrictionWatts.Format("n2"));
                Field(sb, "  heat generation W", t.HeatGeneration.Format("n2"));
                Field(sb, "  power produced W", t.EnergyProduction.Format("n0"));
                Field(sb, "  power consumed W", t.EnergyConsumption.Format("n0"));
                Field(sb, "  thrust consumed W", t.ThrustConsumption.Format("n0"));
                Field(sb, "  substeps demanded", t.SubstepDemand.Count == 0
                    ? "-"
                    : t.SubstepDemand.Format("n2"));
                if (t.PeakSubstepDemand > 0f)
                {
                    Field(sb, "  worst demand", t.PeakSubstepDemand.ToString("n2")
                        + " substeps, " + t.PeakSubstepDemandPosition
                        + " on grid " + t.PeakSubstepDemandGrid);
                }
                Field(sb, "  critical updates", t.CriticalUpdates.ToString("n0"));
                Field(sb, "  heat damage dealt", t.TotalDamage.ToString("n1"));

                sb.Append("\n    sampled temperature distribution\n");
                t.SampledTemperatures.Write(sb, "      ", "K");
                sb.Append("    final temperature distribution\n");
                t.FinalTemperatures.Write(sb, "      ", "K");
            }
        }


        private static List<BlockTypeTelemetry> SortedBlockTypes()
        {

            List<BlockTypeTelemetry> types = new List<BlockTypeTelemetry>(Telemetry.BlockTypes.Values);
            types.Sort(delegate (BlockTypeTelemetry a, BlockTypeTelemetry b)
            {
                int byUpdates = b.TotalUpdates.CompareTo(a.TotalUpdates);
                return byUpdates != 0 ? byUpdates : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
            return types;
        }



        private static string Truncate(string value, int length)
        {
            return TelemetryFormat.Truncate(value, length);
        }


        private static string FormatPeak(float value)
        {
            return TelemetryFormat.Peak(value);
        }
    }
}

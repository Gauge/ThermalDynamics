using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// What a corpus pass **learned**, written down: the invariants are pass or fail, and every figure
    /// the run measured to answer them used to die with the assertion. Written down once, the same
    /// pass names the specimens that answer the question in minutes afterwards.
    ///
    /// <para>
    /// Off unless <c>THERMAL_CORPUS_DATA</c> names a directory. One file per kind of row, appended a
    /// batch at a time, because thirty workers opening a file per ship is its own bottleneck.
    /// See balance.md, The datasets.
    /// </para>
    /// </summary>
    internal static class CorpusRecord
    {

        private static readonly object Gate = new object();

        internal static readonly HashSet<string> Started = new HashSet<string>(StringComparer.Ordinal);


        public static string Directory()
        {
            string path = Environment.GetEnvironmentVariable("THERMAL_CORPUS_DATA");
            if (string.IsNullOrEmpty(path)) return null;

            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Root(), path));
        }

        public static bool On
        {

            get { return Directory() != null; }
        }

        private static readonly string[] Definitions = { "Cubes.xml", "Loops.xml", "Planets.xml" };


        public static void Provenance(string walk)
        {

            string directory = Directory();
            if (directory == null) return;

            lock (Gate)
            {
                if (!Started.Add("provenance:" + walk)) return;
            }


            StringBuilder text = new StringBuilder();
            text.Append("walk ").Append(walk)
                .Append(" started ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .AppendLine();
            text.Append("commit ").AppendLine(Commit());
            foreach (string definition in Definitions)
            {
                text.Append(definition).Append(' ')
                    .AppendLine(HashOf(Path.Combine(ShippedBlocks.DataRoot(), definition)));
            }

            try
            {
                System.IO.Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "provenance.txt"), text.ToString());
            }
            catch (IOException)
            {
            }
        }


        private static string Root()
        {
            return ShippedBlocks.RepoRoot();
        }


        private static string Commit()
        {
            try
            {

                string git = GitDirectory();
                if (git == null) return "unknown";


                string common = CommonDirectory(git);
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();

                string hash;
                if (head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    string reference = head.Substring(4).Trim();
                    string relative = reference.Replace('/', Path.DirectorySeparatorChar);

                    string path = Path.Combine(git, relative);
                    if (!File.Exists(path)) path = Path.Combine(common, relative);

                    hash = File.Exists(path)
                        ? File.ReadAllText(path).Trim()

                        : Packed(common, reference);
                }
                else
                {
                    hash = head;
                }

                return string.IsNullOrEmpty(hash) ? "unknown" : hash;
            }
            catch (IOException)
            {
                return "unknown";
            }
        }


        private static string GitDirectory()
        {

            string root = Root();
            string git = Path.Combine(root, ".git");

            if (System.IO.Directory.Exists(git)) return git;
            if (!File.Exists(git)) return null;

            string text = File.ReadAllText(git).Trim();
            if (!text.StartsWith("gitdir:", StringComparison.Ordinal)) return null;

            string named = text.Substring("gitdir:".Length).Trim();
            if (named.Length == 0) return null;

            string resolved = Path.IsPathRooted(named) ? named : Path.Combine(root, named);
            return System.IO.Directory.Exists(resolved) ? resolved : null;
        }


        private static string CommonDirectory(string git)
        {
            string marker = Path.Combine(git, "commondir");
            if (!File.Exists(marker)) return git;

            string named = File.ReadAllText(marker).Trim();
            if (named.Length == 0) return git;

            string resolved = Path.IsPathRooted(named) ? named : Path.Combine(git, named);
            return System.IO.Directory.Exists(resolved) ? resolved : git;
        }


        private static string Packed(string git, string reference)
        {
            string path = Path.Combine(git, "packed-refs");
            if (!File.Exists(path)) return null;

            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.EndsWith(" " + reference, StringComparison.Ordinal)) continue;

                return line.Substring(0, line.IndexOf(' '));
            }

            return null;
        }


        private static string HashOf(string path)
        {
            try
            {
                if (!File.Exists(path)) return "missing";

                using (System.Security.Cryptography.SHA256 sha =
                    System.Security.Cryptography.SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream))
                        .Replace("-", "").ToLowerInvariant().Substring(0, 16);
                }
            }
            catch (IOException)
            {
                return "unreadable";
            }
        }


        public static void Write(string name, string header, List<string> rows)
        {

            string directory = Directory();
            if (directory == null || rows.Count == 0) return;

            try
            {
                lock (Gate)
                {
                    System.IO.Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, name + ".csv");


                    StringBuilder text = new StringBuilder();
                    if (Started.Add(name) && !File.Exists(path)) text.AppendLine(header);
                    foreach (string row in rows) text.AppendLine(row);

                    File.AppendAllText(path, text.ToString());
                }
            }
            catch
            {
            }
        }


        public static string Text(string value)
        {
            return Thermodynamics.Harness.CsvLine.Text(value);
        }


        public static string Num(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }


        public const string OutcomeHeader =
            "walk,ship,workshop_id,scenario,blocks,grids,joints,peak_k,mean_k,median_k,p95_k,min_k,"
            + "gradient_k,hotspot_k,hotspot_blocks,over_critical,over_share,margin_k,"
            + "seconds_to_settle,seconds_to_critical,peak_rate_k_per_s,thermal_mass_j_per_k,"
            + "bulk_drift_w,made_w,vented_w,radiation_w,convection_w,solar_w,friction_w,"
            + "generation_w,substeps_demanded,substeps_granted,hottest_block,seconds_to_first_loss,"
            + "links,substep_cost,run_seconds,cap,floored";


        public static string Row(string walk, ScenarioOutcome o)
        {

            StringBuilder row = new StringBuilder();
            row.Append(Text(walk)).Append(',');
            row.Append(Text(o.Ship)).Append(',');
            row.Append(o.WorkshopId.ToString(CultureInfo.InvariantCulture)).Append(',');
            row.Append(Text(o.Scenario)).Append(',');
            row.Append(o.Blocks).Append(',');
            row.Append(o.Grids).Append(',');
            row.Append(o.Joints).Append(',');
            row.Append(Num(o.PeakKelvin)).Append(',');
            row.Append(Num(o.MeanKelvin)).Append(',');
            row.Append(Num(o.MedianKelvin)).Append(',');
            row.Append(Num(o.P95Kelvin)).Append(',');
            row.Append(Num(o.MinKelvin)).Append(',');
            row.Append(Num(o.GradientKelvin)).Append(',');
            row.Append(Num(o.HotSpotKelvin)).Append(',');
            row.Append(o.HotSpotBlocks).Append(',');
            row.Append(o.BlocksOverCritical).Append(',');
            row.Append(Num(o.OverCriticalShare)).Append(',');
            row.Append(Num(o.MarginKelvin)).Append(',');
            row.Append(Num(o.SecondsToSettle)).Append(',');
            row.Append(Num(o.SecondsToCritical)).Append(',');
            row.Append(Num(o.PeakRateKelvinPerSecond)).Append(',');
            row.Append(Num(o.ThermalMass)).Append(',');
            row.Append(Num(o.BulkDriftWatts)).Append(',');
            row.Append(Num(o.MadeWatts)).Append(',');
            row.Append(Num(o.VentedWatts)).Append(',');
            row.Append(Num(o.RadiationWatts)).Append(',');
            row.Append(Num(o.ConvectionWatts)).Append(',');
            row.Append(Num(o.SolarWatts)).Append(',');
            row.Append(Num(o.FrictionWatts)).Append(',');
            row.Append(Num(o.GenerationWatts)).Append(',');
            row.Append(Num(o.SubstepsDemanded)).Append(',');
            row.Append(o.SubstepsGranted).Append(',');
            row.Append(Text(o.HottestBlock)).Append(',');
            row.Append(Num(o.SecondsToFirstLoss)).Append(',');
            row.Append(o.Links).Append(',');
            row.Append(o.SubstepCost).Append(',');
            row.Append(Num(o.RunSeconds)).Append(',');
            row.Append(o.SubstepsPerBlockCap).Append(',');
            row.Append(o.FlooredNodes);
            return row.ToString();
        }


        public static void Outcomes(string walk, List<ScenarioOutcome> outcomes)
        {
            if (!On) return;


            List<string> rows = new List<string>(outcomes.Count);
            foreach (ScenarioOutcome outcome in outcomes) rows.Add(Row(walk, outcome));

            Write("outcomes", OutcomeHeader, rows);
        }


        public const string ShipHeader =
            "ship,workshop_id,path,large,blocks,nodes,grids,joints,rooms,sealed_blocks,"
            + "stepped,accounted";


        public static string ShipRow(Blueprints.Ship ship, int nodes, int joints, int rooms,
            long sealedBlocks, bool stepped, bool accounted)
        {

            StringBuilder row = new StringBuilder();
            row.Append(Text(ship.Name)).Append(',');
            row.Append(ship.WorkshopId.ToString(CultureInfo.InvariantCulture)).Append(',');
            row.Append(Text(ship.Path)).Append(',');
            row.Append(ship.Large ? 1 : 0).Append(',');
            row.Append(ship.Blocks).Append(',');
            row.Append(nodes).Append(',');
            row.Append(ship.Grids.Count).Append(',');
            row.Append(joints).Append(',');
            row.Append(rooms).Append(',');
            row.Append(sealedBlocks).Append(',');
            row.Append(stepped ? 1 : 0).Append(',');
            row.Append(accounted ? 1 : 0);
            return row.ToString();
        }
    }
}

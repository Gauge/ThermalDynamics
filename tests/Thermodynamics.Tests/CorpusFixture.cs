using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The blueprint corpus, read once and shared by every test that wants it. **Opt-in**, behind
    /// <c>THERMAL_CORPUS_TESTS</c>, because it reads gigabytes that live outside the repository and
    /// takes minutes.
    ///
    /// <code>
    ///     THERMAL_CORPUS_TESTS=1 dotnet test --filter LabInvariantTests
    /// </code>
    /// </summary>
    internal static class CorpusFixture
    {

        private static readonly object Gate = new object();
        private static List<string> cached;


        public static List<string> Files()
        {
            lock (Gate)
            {
                if (cached != null) return cached;


                cached = Find();
                return cached;
            }
        }


        public static bool OptedIn(string configured)
        {
            if (string.IsNullOrEmpty(configured)) return false;

            string value = configured.Trim().ToLowerInvariant();
            return value != "0" && value != "no" && value != "off" && value != "false";
        }


        public static bool OptedIn()
        {
            return OptedIn(Environment.GetEnvironmentVariable("THERMAL_CORPUS_TESTS"));
        }


        private static List<string> Find()
        {
            if (!OptedIn()) return new List<string>();

            if (!GameBlocks.IsInstalled) return new List<string>();

            string root = Blueprints.DefaultPath();
            if (root == null) return new List<string>();

            List<string> files = Blueprints.Files(root);

            files.Sort(delegate (string a, string b)
            {
                return Length(b).CompareTo(Length(a));
            });


            long ceiling = Ceiling();

            List<string> oversized = new List<string>();

            for (int i = files.Count - 1; i >= 0; i--)
            {
                if (Length(files[i]) <= ceiling) continue;

                oversized.Add(files[i]);
                files.RemoveAt(i);
            }

            excluded = oversized;
            if (oversized.Count > 0)
            {
                Note("corpus excluded " + oversized.Count + " oversized blueprints");
                foreach (string file in oversized)
                {
                    Note("excluded " + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file))
                        + " at " + (Length(file) / (1024L * 1024L)) + " MB");
                }
            }

            Assert.True(files.Count > 0,
                "the corpus at " + root + " holds no blueprint files.");

            return files;
        }


        private static string DonePath(string label)
        {
            string directory = CorpusRecord.Directory();
            if (directory == null || string.IsNullOrEmpty(label)) return null;

            return System.IO.Path.Combine(directory, "done-" + label + ".txt");
        }


        private static HashSet<string> Done(string label)
        {

            HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);


            string path = DonePath(label);
            if (path == null || !System.IO.File.Exists(path)) return done;

            try
            {
                foreach (string line in System.IO.File.ReadAllLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0) done.Add(trimmed);
                }
            }
            catch (System.IO.IOException)
            {
            }

            return done;
        }


        public static List<string> Remaining(IList<string> corpus, ICollection<string> done)
        {

            List<string> remaining = new List<string>(corpus.Count);

            for (int i = 0; i < corpus.Count; i++)
            {
                if (done == null || !done.Contains(corpus[i])) remaining.Add(corpus[i]);
            }

            return remaining;
        }


        private static void MarkDone(string label, string blueprint)
        {

            string path = DonePath(label);
            if (path == null) return;

            try
            {
                System.IO.File.AppendAllText(path, blueprint + System.Environment.NewLine);
            }
            catch (System.IO.IOException)
            {
            }
        }


        public static List<string> Sample(string label = null)
        {

            List<string> corpus = Files();


            corpus = Only(corpus);


            HashSet<string> done = Done(label);
            if (done.Count > 0)
            {

                List<string> remaining = Remaining(corpus, done);
                Note(label + ": resuming, " + done.Count + " blueprints already finished and "
                    + remaining.Count + " to go");
                corpus = remaining;
            }

            int cap;
            string configured = Environment.GetEnvironmentVariable("THERMAL_CORPUS_SHIPS");
            if (string.IsNullOrEmpty(configured) || !int.TryParse(configured, out cap) || cap <= 0)
            {
                return corpus;
            }

            if (cap >= corpus.Count) return corpus;


            List<string> sample = new List<string>();
            int stride = corpus.Count / cap;
            if (stride < 1) stride = 1;

            for (int i = 0; i < corpus.Count && sample.Count < cap; i += stride)
            {
                sample.Add(corpus[i]);
            }

            return sample;
        }


        public static List<string> Only(List<string> corpus)
        {
            string path = Environment.GetEnvironmentVariable("THERMAL_CORPUS_ONLY");
            if (string.IsNullOrEmpty(path)) return corpus;

            Assert.True(System.IO.File.Exists(path),
                "THERMAL_CORPUS_ONLY names " + path + ", which does not exist");


            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

                wanted.Add(trimmed);
            }

            Assert.True(wanted.Count > 0, "THERMAL_CORPUS_ONLY at " + path + " names no blueprints");


            List<string> chosen = new List<string>();

            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < corpus.Count; i++)
            {
                if (!wanted.Contains(corpus[i])) continue;

                chosen.Add(corpus[i]);
                found.Add(corpus[i]);
            }

            Assert.True(found.Count == wanted.Count,
                "THERMAL_CORPUS_ONLY names " + wanted.Count + " blueprints and the corpus holds "
                + found.Count + " of them, so this list was built against a different corpus");

            Note("walking " + chosen.Count + " blueprints named by " + path);
            return chosen;
        }


        public static List<Blueprints.Ship> Spread(int count)
        {

            List<string> corpus = Files();

            List<Blueprints.Ship> ships = new List<Blueprints.Ship>();
            if (corpus.Count == 0 || count <= 0) return ships;

            GameBlocks.BySubtype();

            int stride = corpus.Count / count;
            if (stride < 1) stride = 1;


            List<string> paths = new List<string>();
            for (int i = 0; i < corpus.Count && paths.Count < count; i += stride) paths.Add(corpus[i]);

            foreach (List<Blueprints.Ship> read in LabRun.Map(paths, Blueprints.Read, LabMode.Parallel))
            {
                foreach (Blueprints.Ship ship in read)
                {
                    if (!ship.IsVanilla) continue;
                    if (ship.Blocks < CorpusLab.MinimumBlocks) continue;
                    ships.Add(ship);
                }
            }

            return ships;
        }

        private static void Report(string label, int batch, int batches, int ships, int files)
        {
            string path = Environment.GetEnvironmentVariable("THERMAL_CORPUS_PROGRESS");
            if (string.IsNullOrEmpty(path)) return;

            string line = DateTime.Now.ToString("HH:mm:ss") + " " + label + " batch " + batch
                + "/" + batches + " ships " + ships + " files " + files + Environment.NewLine;

            try
            {
                lock (Gate)
                {
                    System.IO.File.AppendAllText(path, line);
                }
            }
            catch
            {
            }
        }


        private static List<string> excluded = new List<string>();


        public static List<string> Excluded()
        {
            Files();
            return excluded;
        }


        private static long Ceiling()
        {
            int megabytes;
            string configured = Environment.GetEnvironmentVariable("THERMAL_CORPUS_MAX_MB");
            if (string.IsNullOrEmpty(configured) || !int.TryParse(configured, out megabytes))
            {
                megabytes = 256;
            }

            return megabytes <= 0 ? long.MaxValue : megabytes * 1024L * 1024L;
        }


        private static void Note(string line)
        {
            string path = Environment.GetEnvironmentVariable("THERMAL_CORPUS_PROGRESS");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                lock (Gate)
                {
                    System.IO.File.AppendAllText(path,
                        DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private const long HeavyFileBytes = 48L * 1024L * 1024L;

        private const int ReportEvery = 10;


        public static List<T> Sweep<T>(string label, Func<Blueprints.Ship, T> work) where T : class
        {

            List<string> paths = Sample(label);

            List<T> results = new List<T>();
            if (paths.Count == 0) return results;


            GameBlocks.BySubtype();

            CorpusRecord.Provenance(label);


            object collect = new object();
            int files = 0;
            int ships = 0;

            System.Threading.Tasks.ParallelOptions options =
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = LabRun.Workers };

            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, paths.Count, 1),
                options,
                range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {

                        List<T> mine = Visit(paths[i], work);

                        lock (collect)
                        {
                            results.AddRange(mine);
                            MarkDone(label, paths[i]);
                            files++;
                            ships += mine.Count;

                            if (files % ReportEvery == 0 || files == paths.Count)
                            {
                                Report(label, files, paths.Count, ships, files);
                            }
                        }
                    }
                });

            Report(label, files, paths.Count, ships, files);
            return results;
        }


        private static List<T> Visit<T>(string path, Func<Blueprints.Ship, T> work) where T : class
        {

            List<T> results = new List<T>();


            bool heavy = Length(path) > HeavyFileBytes;
            if (heavy) ParseSlots.Wait();

            List<Blueprints.Ship> ships;
            try
            {
                ships = Blueprints.Read(path);
            }
            catch
            {
                return results;
            }
            finally
            {
                if (heavy) ParseSlots.Release();
            }

            foreach (Blueprints.Ship ship in ships)
            {
                if (!ship.IsVanilla) continue;
                if (ship.Blocks < CorpusLab.MinimumBlocks) continue;

                try
                {

                    T result = work(ship);
                    if (result != null) results.Add(result);
                }
                catch
                {
                }
            }

            return results;
        }


        private static long Length(string path)
        {
            try
            {
                return new System.IO.FileInfo(path).Length;
            }
            catch
            {
                return 0L;
            }
        }

        private static readonly System.Threading.SemaphoreSlim ParseSlots =
            new System.Threading.SemaphoreSlim(4, 4);
    }
}

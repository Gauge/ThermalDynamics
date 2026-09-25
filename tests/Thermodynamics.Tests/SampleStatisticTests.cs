using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The lab that asks which summary of a stage's repeats is reproducible.**
    ///
    /// <para>
    /// `SampleStatisticLab` decides a rule the whole performance page rests on, so its arithmetic is
    /// checked against hand-computed answers rather than against itself (`E7`), and the two ways it
    /// can quietly compare the wrong thing — a stage present in some runs and not others, a run file
    /// that parsed to nothing — are checked to be loud (`P2`).
    /// </para>
    /// </summary>
    public class SampleStatisticTests
    {
        [Fact]

        public void AQuantileIsAReadingThatWasActuallyTaken()
        {
            double[] sorted = new double[101];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = i;

            Assert.Equal(0d, SampleStatisticLab.Quantile(sorted, 0d));
            Assert.Equal(1d, SampleStatisticLab.Quantile(sorted, 0.01d));
            Assert.Equal(5d, SampleStatisticLab.Quantile(sorted, 0.05d));
            Assert.Equal(10d, SampleStatisticLab.Quantile(sorted, 0.10d));
            Assert.Equal(25d, SampleStatisticLab.Quantile(sorted, 0.25d));
            Assert.Equal(50d, SampleStatisticLab.Quantile(sorted, 0.50d));

            foreach (double q in SampleStatisticLab.Quantiles)
            {
                Assert.Contains(SampleStatisticLab.Quantile(sorted, q), sorted);
            }

            double[] ten = { 1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d, 10d };
            Assert.Equal(1d, SampleStatisticLab.Quantile(ten, 0.05d));
            Assert.Equal(1d, SampleStatisticLab.Quantile(ten, 0.10d));
            Assert.Equal(3d, SampleStatisticLab.Quantile(ten, 0.25d));

            Assert.Equal(0d, SampleStatisticLab.Quantile(new double[0], 0.5d));
        }

        [Fact]

        public void TheSpreadIsBetweenRunsForOneStatistic()
        {
            List<SampleStatisticLab.Series> series = new List<SampleStatisticLab.Series>
            {
                Series("run1", "links", new double[] { 1d, 10d, 10d, 10d, 12d }),
                Series("run2", "links", new double[] { 3d, 11d, 11d, 11d, 12d }),
            };

            List<string> dropped;
            List<SampleStatisticLab.Row> rows = SampleStatisticLab.Compare(series, out dropped);

            Assert.Empty(dropped);


            SampleStatisticLab.Row min = Find(rows, "links", 0d);
            Assert.Equal(2, min.Runs);
            Assert.Equal(1d, min.Lowest);
            Assert.Equal(3d, min.Highest);
            Assert.Equal(200d, min.SpreadPercent, 6);


            SampleStatisticLab.Row median = Find(rows, "links", 0.50d);
            Assert.Equal(10d, median.Lowest);
            Assert.Equal(11d, median.Highest);
            Assert.Equal(10d, median.SpreadPercent, 6);
        }

        [Fact]

        public void AStageMissingFromARunIsDroppedAndSaidSo()
        {
            List<SampleStatisticLab.Series> series = new List<SampleStatisticLab.Series>
            {
                Series("run1", "links", new double[] { 1d, 2d }),
                Series("run2", "links", new double[] { 1d, 2d }),
                Series("run1", "rooms", new double[] { 5d, 6d }),
            };

            List<string> dropped;
            List<SampleStatisticLab.Row> rows = SampleStatisticLab.Compare(series, out dropped);

            Assert.Single(dropped);
            Assert.Contains("rooms", dropped[0]);
            Assert.Contains("1 of 2", dropped[0]);

            foreach (SampleStatisticLab.Row row in rows) Assert.Equal("links", row.Stage);
        }

        [Fact]

        public void ARunThatParsedToNothingIsLoud()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "stage,blocks,repeat,ms\n");
                Assert.Throws<InvalidOperationException>(() => SampleStatisticLab.Read(path, "run1"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]

        public void TheStageLabsSamplesFileIsWhatThisLabReads()
        {
            int repeats = StageLab.Repeats;
            int confirming = StageLab.ConfirmingRepeats;

            StageLab.Repeats = 3;
            StageLab.ConfirmingRepeats = 2;
            try
            {
                List<StageLab.Row> stageRows = StageLab.Run("ship", 2000, new[] { "surfaces", "links" });

                string path = Path.Combine(Path.GetTempPath(),
                    "samples-" + Guid.NewGuid().ToString("n") + ".csv");
                File.WriteAllText(path, StageLab.SamplesCsv(stageRows));
                try
                {
                    List<SampleStatisticLab.Series> read = SampleStatisticLab.Read(path, "run1");

                    Assert.Equal(stageRows.Count, read.Count);
                    for (int i = 0; i < stageRows.Count; i++)
                    {
                        Assert.Equal(stageRows[i].Stage, read[i].Stage);
                        Assert.Equal(stageRows[i].Repeats, read[i].Samples.Count);
                        Assert.Equal(stageRows[i].Samples.Count, read[i].Samples.Count);

                        double[] sorted = read[i].Samples.ToArray();
                        Array.Sort(sorted);
                        Assert.Equal(stageRows[i].BestMs, sorted[0], 9);
                    }
                }
                finally
                {
                    File.Delete(path);
                }
            }
            finally
            {
                StageLab.Repeats = repeats;
                StageLab.ConfirmingRepeats = confirming;
            }
        }

        [Fact]

        public void RunsAreOneWindowOnlyIfTheyWereTakenInsideOne()
        {
            List<SampleStatisticLab.Series> inside = new List<SampleStatisticLab.Series>
            {
                Stamped("run1", "2026-08-27T10:00:00Z"),
                Stamped("run2", "2026-08-27T10:59:00Z"),
            };

            SampleStatisticLab.WindowSpan span = SampleStatisticLab.Window(inside);
            Assert.True(span.IsOneWindow);
            Assert.Equal(0, span.Unstamped);
            Assert.Equal("2026-08-27T10:00:00Z", span.Earliest);
            Assert.Equal("2026-08-27T10:59:00Z", span.Latest);
            Assert.Equal(59d, span.Elapsed.TotalMinutes);


            inside[1] = Stamped("run2", "2026-08-27T11:00:00Z");
            Assert.True(SampleStatisticLab.Window(inside).IsOneWindow);


            inside[1] = Stamped("run2", "2026-08-27T11:01:00Z");
            Assert.False(SampleStatisticLab.Window(inside).IsOneWindow);

            inside.Reverse();
            Assert.False(SampleStatisticLab.Window(inside).IsOneWindow);
            Assert.Equal("2026-08-27T10:00:00Z", SampleStatisticLab.Window(inside).Earliest);
        }

        [Fact]

        public void ARunWithNoReadableStampIsNotOneWindowWithAnything()
        {
            List<SampleStatisticLab.Series> runs = new List<SampleStatisticLab.Series>
            {
                Stamped("run1", "2026-08-27T10:00:00Z"),
                Stamped("run2", string.Empty),
            };

            SampleStatisticLab.WindowSpan span = SampleStatisticLab.Window(runs);
            Assert.False(span.IsOneWindow);
            Assert.Equal(1, span.Unstamped);


            runs[1] = Stamped("run2", "not a time");
            Assert.False(SampleStatisticLab.Window(runs).IsOneWindow);
            Assert.Equal(1, SampleStatisticLab.Window(runs).Unstamped);

            List<SampleStatisticLab.Series> oneRun = new List<SampleStatisticLab.Series>
            {
                Stamped("run1", string.Empty, "place"),
                Stamped("run1", string.Empty, "rooms"),
            };
            Assert.Equal(1, SampleStatisticLab.Window(oneRun).Unstamped);
        }

        [Fact]

        public void TheStampIsWrittenToTheArtefactAndReadBackFromIt()
        {
            StageLab.Row row = new StageLab.Row();
            row.Stage = "exposure";
            row.Blocks = 10;
            row.Samples.AddRange(new[] { 1d, 2d, 3d });

            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, StageLab.SamplesCsv(new List<StageLab.Row> { row }));

                Assert.Contains("taken_utc", File.ReadAllLines(path)[0]);

                List<SampleStatisticLab.Series> read = SampleStatisticLab.Read(path, "run1");
                Assert.Single(read);
                Assert.Equal(StageLab.TakenUtc, read[0].TakenUtc);
                Assert.Equal(3, read[0].Samples.Count);
                Assert.Equal(0, SampleStatisticLab.Window(read).Unstamped);
            }
            finally
            {
                File.Delete(path);
            }
        }


        private static SampleStatisticLab.Series Stamped(string run, string takenUtc, string stage = "exposure")
        {
            SampleStatisticLab.Series series = new SampleStatisticLab.Series();
            series.Run = run;
            series.Stage = stage;
            series.TakenUtc = takenUtc;
            return series;
        }


        private static SampleStatisticLab.Series Series(string run, string stage, double[] samples)
        {
            SampleStatisticLab.Series series = new SampleStatisticLab.Series();
            series.Run = run;
            series.Stage = stage;
            series.Samples.AddRange(samples);
            return series;
        }


        private static SampleStatisticLab.Row Find(IList<SampleStatisticLab.Row> rows, string stage, double quantile)
        {
            foreach (SampleStatisticLab.Row row in rows)
            {
                if (row.Stage == stage && row.Quantile == quantile) return row;
            }

            throw new InvalidOperationException("no row for " + stage + " at " + quantile);
        }
    }
}

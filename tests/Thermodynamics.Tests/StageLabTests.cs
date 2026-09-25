using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The two classes that turn the stage lab's dials down run one at a time.** `StageLab.Repeats`
    /// and its convergence settings are static, and a suite that runs eight ways in parallel had two
    /// classes assigning them at once — which showed up the moment a second dial was added, as a
    /// stage that stopped before its best was confirmed. Serialised rather than made instance state,
    /// because a lab that a caller configures is what every other bench command here expects.
    /// </summary>
    [CollectionDefinition("the stage lab's dials", DisableParallelization = true)]
    public class StageLabDials
    {
    }

    /// <summary>
    /// `bench stages` is the instrument a performance pass judges a stage on, so this holds the
    /// two properties that make its reading a reading: every stage it names produces a figure on a
    /// hull that exercised it, and a stage's work counter is identical across repeats — which the
    /// lab enforces by refusing, and this checks by asking twice.
    ///
    /// <para>
    /// And a third, since pass 8: **a stage does not stop until its fastest reading has been
    /// reproduced.** Best-of-fifteen was not converged — three runs of the same binary read the
    /// surface rebuild at 7.4, 9.6 and 6.5 ms — so a stage now repeats until five readings land
    /// within two per cent of its best. `EveryStageConfirmsItsBest` is what says that happened
    /// rather than that the cap was reached.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    [Collection("the stage lab's dials")]
    public class StageLabTests
    {
        [Fact]

        public void EveryStageReportsWorkAndATimeOnAHullThatExercisedIt()
        {
            int repeats = StageLab.Repeats;
            StageLab.Repeats = 3;
            try
            {

                List<string> all = new List<string>(StageLab.Stages);
                all.AddRange(StageLab.ExtraStages);

                List<StageLab.Row> rows = StageLab.Run("ship", 2000, all);
                Assert.Equal(all.Count, rows.Count);

                for (int i = 0; i < rows.Count; i++)
                {
                    StageLab.Row row = rows[i];
                    Assert.True(row.Work > 0, row.Stage + " reports no work, so it timed nothing");
                    Assert.True(row.BestMs > 0d && row.BestMs <= row.WorstMs, row.Stage + " has no usable time");
                    Assert.True(row.Blocks > 1000, row.Stage + " ran on " + row.Blocks + " blocks");
                }


                StageLab.Row dirty = Find(rows, "syncdirty");

                StageLab.Row clean = Find(rows, "syncclean");
                Assert.Equal(dirty.Work, clean.Work);
                Assert.True(dirty.BestMs > clean.BestMs,
                    "mirroring every node took " + dirty.BestMs.ToString("n4")
                    + " ms and mirroring none took " + clean.BestMs.ToString("n4")
                    + ", so this pair prices nothing");
            }
            finally
            {
                StageLab.Repeats = repeats;
            }
        }

        [Fact]

        public void ARowSurvivesBeingWrittenAndReadBack()
        {
            int repeats = StageLab.Repeats;
            StageLab.Repeats = 3;
            try
            {
                List<StageLab.Row> written = StageLab.Run("ship", 2000,
                    new List<string> { "place", "rooms" });

                string path = Path.Combine(Path.GetTempPath(),
                    "stagelab-" + Guid.NewGuid().ToString("n") + ".csv");
                try
                {
                    File.WriteAllText(path, StageLab.Csv(written));
                    List<StageLab.Row> read = StageLab.ReadCsv(path);

                    Assert.Equal(written.Count, read.Count);
                    for (int i = 0; i < written.Count; i++)
                    {
                        Assert.Equal(written[i].Stage, read[i].Stage);
                        Assert.Equal(written[i].Blocks, read[i].Blocks);
                        Assert.Equal(written[i].BestMs, read[i].BestMs);
                        Assert.Equal(written[i].MedianMs, read[i].MedianMs);
                        Assert.Equal(written[i].WorstMs, read[i].WorstMs);
                        Assert.Equal(written[i].Repeats, read[i].Repeats);
                        Assert.Equal(written[i].ConfirmedBest, read[i].ConfirmedBest);
                        Assert.Equal(written[i].Stop, read[i].Stop);
                        Assert.Equal(written[i].Work, read[i].Work);
                        Assert.Equal(written[i].WorkUnit, read[i].WorkUnit);
                        Assert.Equal(written[i].AllocatedBytes, read[i].AllocatedBytes);
                        Assert.Equal(written[i].FastModeShare, read[i].FastModeShare);
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
            }
        }

        [Fact]

        public void AnArtefactMissingAColumnIsRefusedRatherThanMisread()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "stagelab-old-" + Guid.NewGuid().ToString("n") + ".csv");
            try
            {
                File.WriteAllText(path,
                    "stage,blocks,best_ms,worst_ms,spread_percent,work,work_unit,ns_per_unit,allocated_bytes"
                    + Environment.NewLine
                    + "rooms,126731,13.6,69.4,409,1651592,cells visited,8.3,4882720"
                    + Environment.NewLine);

                InvalidOperationException failure =
                    Assert.Throws<InvalidOperationException>(() => StageLab.ReadCsv(path));
                Assert.Contains("median_ms", failure.Message);

                File.WriteAllText(path, "stage,blocks,best_ms" + Environment.NewLine);
                Assert.Throws<InvalidOperationException>(() => StageLab.ReadCsv(path));
            }
            finally
            {
                File.Delete(path);
            }
        }


        private static StageLab.Row Find(IList<StageLab.Row> rows, string stage)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Stage == stage) return rows[i];
            }

            throw new System.InvalidOperationException("no row for " + stage);
        }

        [Fact]

        public void EveryRowCarriesItsMedianAndItsShape()
        {
            int repeats = StageLab.Repeats;
            int confirming = StageLab.ConfirmingRepeats;

            StageLab.Repeats = 5;
            StageLab.ConfirmingRepeats = 2;
            try
            {
                List<StageLab.Row> rows = StageLab.Run("ship", 2000, StageLab.Stages);

                foreach (StageLab.Row row in rows)
                {
                    Assert.Contains(row.MedianMs, row.Samples);
                    Assert.True(row.MedianMs >= row.BestMs,
                        row.Stage + " has a median of " + row.MedianMs + " below its best of "
                        + row.BestMs + ", so one of the two is not of these repeats");
                    Assert.True(row.MedianMs <= row.WorstMs,
                        row.Stage + " has a median of " + row.MedianMs + " above its worst of "
                        + row.WorstMs);

                    Assert.True(row.FastModeShare > 0d && row.FastModeShare <= 1d,
                        row.Stage + " reports a best/median of " + row.FastModeShare);
                }

                string table = StageLab.Table(rows);
                string header = StageLab.Csv(rows).Split('\n')[0];

                Assert.Contains("median ms", table);
                Assert.Contains("best/med", table);
                Assert.Contains("median_ms", header);
                Assert.Contains("fast_mode_share", header);
            }
            finally
            {
                StageLab.Repeats = repeats;
                StageLab.ConfirmingRepeats = confirming;
            }
        }

        [Fact]

        public void AStepPhaseRowCarriesItsRepeatsAndItsMedian()
        {
            int repeats = StageLab.Repeats;

            StageLab.Repeats = 3;
            try
            {
                List<StageLab.Row> rows = StageLab.StepPhases("ship", 2000);

                Assert.NotEmpty(rows);
                foreach (StageLab.Row row in rows)
                {
                    Assert.Equal(StageLab.Row.Fixed, row.Stop);
                    Assert.Equal(3, row.Repeats);
                    Assert.Equal(3, row.Samples.Count);
                    Assert.True(row.MedianMs > 0d,
                        row.Stage + " reports no median, which would read as a best that is"
                        + " infinitely far below its own bulk");
                }
            }
            finally
            {
                StageLab.Repeats = repeats;
            }
        }


        private static StageLab.Row Contrived(string stage, string stop)
        {
            StageLab.Row row = new StageLab.Row();
            row.Stage = stage;
            row.Stop = stop;
            row.BestMs = 1d;
            row.WorstMs = 2d;
            row.Work = 1;
            row.WorkUnit = "things";
            return row;
        }

        [Fact]

        public void TheTableAndTheCsvSayWhyEachStageStopped()
        {
            int repeats = StageLab.Repeats;
            int confirming = StageLab.ConfirmingRepeats;

            StageLab.Repeats = 3;
            StageLab.ConfirmingRepeats = 2;
            try
            {
                List<StageLab.Row> rows = StageLab.Run("ship", 2000, StageLab.Stages);

                string table = StageLab.Table(rows);
                string csv = StageLab.Csv(rows);

                Assert.Contains("stopped", table);
                Assert.Contains("repeats", table);
                Assert.Contains("stopped", csv.Split('\n')[0]);
                Assert.Contains("repeats", csv.Split('\n')[0]);
                Assert.Contains("confirmed_best", csv.Split('\n')[0]);

                Assert.Contains("taken_utc", csv.Split('\n')[0]);
                Assert.Contains("host", csv.Split('\n')[0]);
                Assert.Contains(StageLab.TakenUtc, csv);
                Assert.Contains(StageLab.Host, csv);

                for (int i = 0; i < rows.Count; i++)
                {
                    StageLab.Row row = rows[i];

                    Assert.True(row.Stop == StageLab.Row.Confirmed || row.Stop == StageLab.Row.Capped,
                        row.Stage + " stopped for \"" + row.Stop + "\", which is not one of the words"
                        + " the lab defines, so a reader cannot tell what ended it");

                    Assert.Contains(row.Stage, table);
                    Assert.Contains(row.Stage + ",", csv);
                }


                StageLab.Row capped = Contrived("contrived-capped", StageLab.Row.Capped);

                StageLab.Row confirmed = Contrived("contrived-confirmed", StageLab.Row.Confirmed);

                List<StageLab.Row> mixed = new List<StageLab.Row> { confirmed, capped };
                string mixedTable = StageLab.Table(mixed);
                string mixedCsv = StageLab.Csv(mixed);

                Assert.Contains(StageLab.Row.Capped, mixedTable);
                Assert.Contains(StageLab.Row.Confirmed, mixedTable);
                Assert.Contains("," + StageLab.Row.Capped + ",", mixedCsv);
                Assert.Contains("," + StageLab.Row.Confirmed + ",", mixedCsv);
            }
            finally
            {
                StageLab.Repeats = repeats;
                StageLab.ConfirmingRepeats = confirming;
            }
        }

        [Fact]

        public void EveryStageStopsForAReasonItCanName()
        {
            int repeats = StageLab.Repeats;
            int confirming = StageLab.ConfirmingRepeats;

            StageLab.Repeats = 3;
            StageLab.ConfirmingRepeats = 2;
            try
            {
                List<StageLab.Row> rows = StageLab.Run("ship", 2000, StageLab.Stages);

                int confirmed = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    StageLab.Row row = rows[i];

                    Assert.True(row.Repeats >= StageLab.Repeats,
                        row.Stage + " stopped after " + row.Repeats + " repeats, below the floor of "
                        + StageLab.Repeats);

                    Assert.True(row.ConfirmedBest >= StageLab.ConfirmingRepeats
                        || row.Repeats >= StageLab.MaxRepeats,
                        row.Stage + " stopped after " + row.Repeats + " repeats with its best"
                        + " confirmed " + row.ConfirmedBest + " times — neither a confirmation nor"
                        + " the cap, so the rule that ended it is not the rule as written");

                    Assert.True(row.ConfirmedBest >= 1,
                        row.Stage + " never counted its own best as confirming itself");

                    if (row.ConfirmedBest >= StageLab.ConfirmingRepeats) confirmed++;
                }

                Assert.True(confirmed > 0,
                    "not one stage of " + rows.Count + " confirmed its best, so either the counting"
                    + " is broken or the cap is being reached every time");
            }
            finally
            {
                StageLab.Repeats = repeats;
                StageLab.ConfirmingRepeats = confirming;
            }
        }

        [Fact]

        public void EveryStageReportsWhatItAllocatedAndTheSteppingPathAllocatesNothing()
        {
            int repeats = StageLab.Repeats;
            StageLab.Repeats = 3;
            try
            {
                List<StageLab.Row> rows = StageLab.Run("ship", 2000, StageLab.Stages);
                StageLab.Row place = null;
                StageLab.Row rooms = null;
                StageLab.Row solver = null;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Stage == "place") place = rows[i];
                    if (rows[i].Stage == "rooms") rooms = rows[i];
                    if (rows[i].Stage == "solver") solver = rows[i];
                }

                Assert.NotNull(place);
                Assert.NotNull(rooms);
                Assert.NotNull(solver);
                Assert.True(place.AllocatedBytes > 100 * 1024,
                    "place allocated " + place.AllocatedBytes + " bytes, which is too little to be building a block each");
                Assert.True(rooms.AllocatedBytes < 64 * 1024,
                    "a warm room pass allocated " + rooms.AllocatedBytes + " bytes; the row samples the first recycled"
                    + " pass, so this figure is `D20` coming undone or the sample landing on a slot-building repeat");
                Assert.True(solver.AllocatedBytes < 4 * 1024,

                    "a settled step allocated " + solver.AllocatedBytes + " bytes; nothing allocates on the stepping path (`C4`)");
            }
            finally
            {
                StageLab.Repeats = repeats;
            }
        }

        [Fact]

        public void AStagesWorkIsTheSameFigureEveryTimeItIsAsked()
        {
            int repeats = StageLab.Repeats;
            StageLab.Repeats = 2;
            try
            {
                List<StageLab.Row> first = StageLab.Run("ship", 2000, StageLab.Stages);
                List<StageLab.Row> second = StageLab.Run("ship", 2000, StageLab.Stages);
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.Equal(first[i].Work, second[i].Work);
                    Assert.Equal(first[i].WorkUnit, second[i].WorkUnit);
                }
            }
            finally
            {
                StageLab.Repeats = repeats;
            }
        }
    }
}

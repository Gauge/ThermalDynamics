using System;
using System.Text;
using System.Threading;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The accumulators behind every number in the telemetry report.
    ///
    /// They matter more than their size suggests: a session runs for hours and nothing is kept
    /// but these running totals, so an error here is not recoverable after the fact.
    /// </summary>
    public class RunningStatTests
    {
        [Fact]

        public void AnEmptyStatReportsNothingRatherThanSentinels()
        {

            RunningStat stat = new RunningStat();

            Assert.Equal(0, stat.Count);
            Assert.Equal(0, stat.Mean);
            Assert.Equal(0, stat.StdDev);

            Assert.Equal(0f, stat.SafeMin);
            Assert.Equal(0f, stat.SafeMax);
            Assert.Equal("-", stat.Format("n3"));
        }

        [Fact]

        public void MeanMinMaxAndCountTrackTheSamples()
        {

            RunningStat stat = new RunningStat();
            foreach (float value in new[] { 4f, 1f, 9f, 6f })
            {
                stat.Add(value);
            }

            Assert.Equal(4, stat.Count);
            Assert.Equal(1f, stat.SafeMin);
            Assert.Equal(9f, stat.SafeMax);
            Assert.Equal(5.0, stat.Mean, 9);
            Assert.Equal(6f, stat.Last);
        }

        [Fact]

        public void StandardDeviationMatchesTheDirectCalculation()
        {
            float[] values = { 2f, 4f, 4f, 4f, 5f, 5f, 7f, 9f };


            RunningStat stat = new RunningStat();
            foreach (float value in values) stat.Add(value);

            Assert.Equal(2.0, stat.StdDev, 6);
        }

        [Fact]

        public void StandardDeviationIsZeroForOneSampleAndForConstantInput()
        {

            RunningStat single = new RunningStat();
            single.Add(42f);
            Assert.Equal(0, single.StdDev);


            RunningStat constant = new RunningStat();
            for (int i = 0; i < 100; i++) constant.Add(7f);

            Assert.Equal(0, constant.StdDev);
        }

        [Fact]

        public void NonFiniteSamplesAreRefusedRatherThanPoisoningEveryDerivedNumber()
        {

            RunningStat stat = new RunningStat();
            stat.Add(10f);
            stat.Add(float.NaN);
            stat.Add(float.PositiveInfinity);
            stat.Add(float.NegativeInfinity);
            stat.Add(20f);

            Assert.Equal(2, stat.Count);
            Assert.Equal(15.0, stat.Mean, 9);
            Assert.Equal(10f, stat.SafeMin);
            Assert.Equal(20f, stat.SafeMax);
            Assert.False(double.IsNaN(stat.StdDev));
        }

        [Fact]

        public void NegativeSamplesAreKept()
        {

            RunningStat stat = new RunningStat();
            stat.Add(-5f);
            stat.Add(-15f);

            Assert.Equal(2, stat.Count);
            Assert.Equal(-15f, stat.SafeMin);
            Assert.Equal(-5f, stat.SafeMax);
            Assert.Equal(-10.0, stat.Mean, 9);
        }

        [Fact]

        public void MergingTwoStatsMatchesFeedingBothSetsToOne()
        {
            float[] first = { 1f, 2f, 3f };
            float[] second = { 10f, 20f };


            RunningStat a = new RunningStat();
            foreach (float value in first) a.Add(value);


            RunningStat b = new RunningStat();
            foreach (float value in second) b.Add(value);

            a.Merge(b);


            RunningStat combined = new RunningStat();
            foreach (float value in first) combined.Add(value);
            foreach (float value in second) combined.Add(value);

            Assert.Equal(combined.Count, a.Count);
            Assert.Equal(combined.Mean, a.Mean, 9);
            Assert.Equal(combined.StdDev, a.StdDev, 9);
            Assert.Equal(combined.SafeMin, a.SafeMin);
            Assert.Equal(combined.SafeMax, a.SafeMax);
        }

        [Fact]

        public void MergingAnEmptyOrNullStatChangesNothing()
        {

            RunningStat stat = new RunningStat();
            stat.Add(5f);

            stat.Merge(new RunningStat());
            stat.Merge(null);

            Assert.Equal(1, stat.Count);
            Assert.Equal(5f, stat.SafeMin);
            Assert.Equal(5f, stat.SafeMax);
        }

        [Fact]

        public void FormatCarriesTheWholeShapeOfTheDistribution()
        {

            RunningStat stat = new RunningStat();
            stat.Add(1f);
            stat.Add(3f);

            string formatted = stat.Format("n1");

            Assert.Contains("1.0", formatted);
            Assert.Contains("3.0", formatted);
            Assert.Contains("2.0", formatted);
            Assert.Contains("n 2", formatted);
        }
    }

    /// <summary>
    /// The bucketing behind every distribution in the telemetry report.
    ///
    /// <para>
    /// A histogram is read as evidence, so what it does with the values that are not ordinary matters
    /// more than what it does with the ones that are: NaN is dropped rather than bucketed, infinity
    /// goes to the overflow bucket, and merging refuses two histograms whose edges differ rather than
    /// silently adding unlike things.
    /// </para>
    /// </summary>
    public class HistogramTests
    {

        private static Histogram TenTwentyThirty()
        {
            return new Histogram(new float[] { 10f, 20f, 30f });
        }

        [Fact]

        public void EdgesAreExclusiveUpperBounds()
        {

            Histogram histogram = TenTwentyThirty();

            histogram.Add(9.999f);
            histogram.Add(10f);
            histogram.Add(19.999f);
            histogram.Add(20f);
            histogram.Add(29.999f);

            Assert.Equal(1, histogram.Counts[0]);
            Assert.Equal(2, histogram.Counts[1]);
            Assert.Equal(2, histogram.Counts[2]);
            Assert.Equal(0, histogram.Counts[3]);
        }

        [Fact]

        public void ThereIsAnOverflowBucketAboveTheLastEdge()
        {

            Histogram histogram = TenTwentyThirty();

            Assert.Equal(4, histogram.Counts.Length);

            histogram.Add(30f);
            histogram.Add(1e9f);

            Assert.Equal(2, histogram.Counts[3]);
            Assert.Equal(2, histogram.Total);
        }

        [Fact]

        public void EverythingBelowTheFirstEdgeLandsInBucketZeroIncludingNegatives()
        {

            Histogram histogram = TenTwentyThirty();

            histogram.Add(0f);
            histogram.Add(-273f);

            Assert.Equal(2, histogram.Counts[0]);
        }

        [Fact]

        public void NotANumberIsDroppedRatherThanBucketed()
        {

            Histogram histogram = TenTwentyThirty();

            histogram.Add(float.NaN);

            Assert.Equal(0, histogram.Total);
        }

        [Fact]

        public void InfinityLandsInTheOverflowBucket()
        {

            Histogram histogram = TenTwentyThirty();

            histogram.Add(float.PositiveInfinity);

            Assert.Equal(1, histogram.Counts[3]);
        }

        [Fact]

        public void TotalIsTheSumOfEveryBucket()
        {

            Histogram histogram = TenTwentyThirty();
            for (int i = 0; i < 40; i++) histogram.Add(i);

            Assert.Equal(40, histogram.Total);
        }

        [Fact]

        public void MergingAddsBucketwise()
        {

            Histogram a = TenTwentyThirty();

            Histogram b = TenTwentyThirty();

            a.Add(5f);
            b.Add(5f);
            b.Add(25f);

            a.Merge(b);

            Assert.Equal(2, a.Counts[0]);
            Assert.Equal(1, a.Counts[2]);
            Assert.Equal(3, a.Total);
        }

        [Fact]

        public void MergingRefusesAHistogramWithDifferentEdges()
        {

            Histogram a = TenTwentyThirty();
            a.Add(5f);


            Histogram other = new Histogram(new float[] { 1f, 2f });
            other.Add(0.5f);

            a.Merge(other);
            a.Merge(null);

            Assert.Equal(1, a.Total);
        }

        [Fact]

        public void ClearingEmptiesEveryBucket()
        {

            Histogram histogram = TenTwentyThirty();
            for (int i = 0; i < 40; i++) histogram.Add(i);

            histogram.Clear();

            Assert.Equal(0, histogram.Total);
            histogram.Add(15f);
            Assert.Equal(1, histogram.Total);
        }

        [Fact]

        public void AnEmptyHistogramSaysSoInsteadOfWritingNothing()
        {

            StringBuilder sb = new StringBuilder();
            TenTwentyThirty().Write(sb, "  ", "K");

            Assert.Contains("no samples", sb.ToString());
        }

        [Fact]

        public void WrittenPercentagesAreOfTheTotalAndEmptyBucketsAreSkipped()
        {

            Histogram histogram = TenTwentyThirty();
            histogram.Add(5f);
            histogram.Add(5f);
            histogram.Add(5f);
            histogram.Add(25f);


            StringBuilder sb = new StringBuilder();
            histogram.Write(sb, "", "K");
            string text = sb.ToString();

            Assert.Contains("75.00%", text);
            Assert.Contains("25.00%", text);

            Assert.DoesNotContain("10K - 20K", text);
            Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [Fact]

        public void TheShippedTemperatureEdgesAreAscendingAndStraddleRoomTemperature()
        {
            float[] edges = Histogram.TemperatureEdges();

            for (int i = 1; i < edges.Length; i++)
            {
                Assert.True(edges[i] > edges[i - 1], "edge " + i + " is not above its predecessor");
            }

            Assert.True(edges[0] <= 2.8f, "the lowest bucket must isolate the vacuum temperature");


            Histogram histogram = new Histogram(edges);
            histogram.Add(293.15f);
            histogram.Add(2.7f);

            Assert.Equal(1, histogram.Counts[0]);
            Assert.Equal(2, histogram.Total);
        }

        [Fact]

        public void TheShippedMillisecondEdgesAreAscending()
        {
            float[] edges = Histogram.MillisecondEdges();

            for (int i = 1; i < edges.Length; i++)
            {
                Assert.True(edges[i] > edges[i - 1], "edge " + i + " is not above its predecessor");
            }
        }
    }

    /// <summary>
    /// The accumulator every stage timing in the report is built from.
    ///
    /// <para>
    /// EachIntervalIsMeasuredFromScratchRatherThanAccumulatingTheStopwatch is the case with a fault
    /// behind it: a stopwatch restarted rather than reset reports every interval as the time since the
    /// session began.
    /// </para>
    /// </summary>
    public class TimingStatTests
    {
        [Fact]

        public void RecordingAccumulatesCallsTotalMeanAndWorst()
        {

            TimingStat stat = new TimingStat("path");
            stat.Record(1.0);
            stat.Record(3.0);
            stat.Record(2.0);

            Assert.Equal(3, stat.Calls);
            Assert.Equal(6.0, stat.TotalMilliseconds, 9);
            Assert.Equal(2.0, stat.MeanMilliseconds, 9);
            Assert.Equal(3.0, stat.MaxMilliseconds, 9);
        }

        [Fact]

        public void AnUnusedStatReportsZeroRatherThanDividingByZero()
        {

            TimingStat stat = new TimingStat("path");

            Assert.Equal(0, stat.Calls);
            Assert.Equal(0, stat.MeanMilliseconds);
        }

        [Fact]

        public void BeginAndEndMeasureAnElapsedInterval()
        {

            TimingStat stat = new TimingStat("path");

            stat.Begin();
            Thread.Sleep(20);
            stat.End();

            Assert.Equal(1, stat.Calls);
            Assert.True(stat.TotalMilliseconds >= 10,
                "a 20 ms interval measured as " + stat.TotalMilliseconds + " ms");
        }

        [Fact]

        public void EachIntervalIsMeasuredFromScratchRatherThanAccumulatingTheStopwatch()
        {

            TimingStat stat = new TimingStat("path");

            stat.Begin();
            Thread.Sleep(30);
            stat.End();

            double first = stat.TotalMilliseconds;

            stat.Begin();
            stat.End();

            double second = stat.TotalMilliseconds - first;

            Assert.Equal(2, stat.Calls);
            Assert.True(second < first / 2,
                "the second interval was " + second + " ms against a first of " + first
                + " ms, so the stopwatch was not reset");
        }

        [Fact]

        public void MergingCombinesCallsTotalsAndWorstCase()
        {

            TimingStat a = new TimingStat("a");
            a.Record(1.0);


            TimingStat b = new TimingStat("b");
            b.Record(5.0);
            b.Record(2.0);

            a.Merge(b);
            a.Merge(null);
            a.Merge(new TimingStat("empty"));

            Assert.Equal(3, a.Calls);
            Assert.Equal(8.0, a.TotalMilliseconds, 9);
            Assert.Equal(5.0, a.MaxMilliseconds, 9);
        }

        [Fact]

        public void MergingManyStatsEqualsTheArithmeticSumOfTheirParts()
        {

            TimingStat merged = new TimingStat("merged");

            long calls = 0;
            double total = 0;

            for (int i = 1; i <= 200; i++)
            {

                TimingStat part = new TimingStat("part " + i);
                for (int c = 0; c < i; c++)
                {
                    double sample = 0.001 * i * (c + 1);
                    part.Record(sample);
                    calls++;
                    total += sample;
                }

                merged.Merge(part);
            }

            Assert.Equal(calls, merged.Calls);
            Assert.Equal(total, merged.TotalMilliseconds, 6);
        }

        [Fact]

        public void MergingCarriesTheDistributionAcrossToo()
        {

            TimingStat a = new TimingStat("a");

            TimingStat b = new TimingStat("b");
            b.Record(0.02);

            a.Merge(b);


            StringBuilder sb = new StringBuilder();
            a.WriteDistribution(sb, "");

            Assert.DoesNotContain("no samples", sb.ToString());
        }

        [Fact]

        public void RowAndHeaderShareTheSameColumnWidths()
        {

            StringBuilder header = new StringBuilder();
            TimingStat.WriteHeader(header, "path");


            TimingStat stat = new TimingStat("path");
            stat.Record(1.0);


            StringBuilder row = new StringBuilder();
            stat.WriteRow(row);

            Assert.Equal(header.Length, row.Length);
        }
    }
}

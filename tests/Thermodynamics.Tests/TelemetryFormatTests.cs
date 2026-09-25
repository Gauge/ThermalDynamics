using System.Globalization;
using System.Text;
using System.Threading;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Formatting of the report and the CSVs.
    ///
    /// The culture tests are the point of this file. Space Engineers runs on whatever locale the
    /// player has, and a German or French client formats 1.5 as "1,5" — which in a comma
    /// separated file is not a decimal point but a new column. A CSV that parses on one machine
    /// and silently shifts every column on another is worse than no CSV at all.
    /// </summary>
    public class TelemetryFormatTests
    {

        private static void InGermanLocale(System.Action body)
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {

                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                body();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]

        public void NumbersUseAPointRegardlessOfTheClientLocale()
        {
            InGermanLocale(() =>
            {
                Assert.Equal("1.5", TelemetryFormat.Number(1.5));
                Assert.Equal("-0.25", TelemetryFormat.Number(-0.25));
                Assert.Equal("1234.5", TelemetryFormat.Number(1234.5));
            });
        }

        [Fact]

        public void NumbersCarryNoThousandsSeparator()
        {
            Assert.Equal("1234567", TelemetryFormat.Number(1234567));
            Assert.DoesNotContain(",", TelemetryFormat.Number(9876543.21));
        }

        [Fact]

        public void NonFiniteNumbersBecomeBlankRatherThanText()
        {
            Assert.Equal("", TelemetryFormat.Number(double.NaN));
            Assert.Equal("", TelemetryFormat.Number(double.PositiveInfinity));
            Assert.Equal("", TelemetryFormat.Number(double.NegativeInfinity));
        }

        [Fact]

        public void WholeNumbersDoNotGrowATrailingDecimalPart()
        {
            Assert.Equal("0", TelemetryFormat.Number(0));
            Assert.Equal("42", TelemetryFormat.Number(42.0));
        }

        [Fact]

        public void IntegersAreInvariantToo()
        {
            InGermanLocale(() =>
            {
                Assert.Equal("1234567", TelemetryFormat.Integer(1234567));
                Assert.Equal("-9", TelemetryFormat.Integer(-9));
            });
        }

        [Fact]

        public void QuotingWrapsAValueAndDoublesAnyEmbeddedQuote()
        {
            Assert.Equal("\"Rusty Miner\"", TelemetryFormat.Quote("Rusty Miner"));
            Assert.Equal("\"a,b\"", TelemetryFormat.Quote("a,b"));
            Assert.Equal("\"say \"\"hi\"\"\"", TelemetryFormat.Quote("say \"hi\""));
        }

        [Fact]

        public void QuotingAnEmptyOrNullValueProducesAnEmptyField()
        {
            Assert.Equal("", TelemetryFormat.Quote(null));
            Assert.Equal("", TelemetryFormat.Quote(""));
        }

        [Fact]

        public void TruncationNeverExceedsTheColumnWidth()
        {
            for (int width = 1; width <= 12; width++)
            {
                string result = TelemetryFormat.Truncate("Gauge_LG_CoolantPipe_Straight", width);
                Assert.True(result.Length <= width,
                    "width " + width + " produced " + result.Length + " characters");
            }
        }

        [Fact]

        public void TruncationMarksTheClipSoAShortenedNameIsNeverMistakenForAWholeOne()
        {
            Assert.Equal("Gaug~", TelemetryFormat.Truncate("Gauge_LG_CoolantPump", 5));
        }

        [Fact]

        public void AValueThatFitsIsLeftExactlyAsItIs()
        {
            Assert.Equal("Reactor", TelemetryFormat.Truncate("Reactor", 7));
            Assert.Equal("Reactor", TelemetryFormat.Truncate("Reactor", 40));
        }

        [Fact]

        public void AnAbsentValueTruncatesToADash()
        {
            Assert.Equal("-", TelemetryFormat.Truncate(null, 10));
            Assert.Equal("-", TelemetryFormat.Truncate("", 10));
        }

        [Fact]

        public void APeakThatWasNeverSetReadsAsAbsentRatherThanAsTheFloatFloor()
        {
            Assert.Equal("-", TelemetryFormat.Peak(float.MinValue));
        }

        [Fact]

        public void APeakThatWasSetIsPrinted()
        {
            Assert.Contains("1", TelemetryFormat.Peak(1234.5f));
            Assert.Equal("0.0", TelemetryFormat.Peak(0f));
        }

        [Fact]

        public void BucketLabelsCoverTheWholeLineWithoutAGap()
        {
            float[] edges = { 10f, 20f, 30f };

            Assert.Equal("< 10K", TelemetryFormat.BucketLabel(edges, 0, "K"));
            Assert.Equal("10K - 20K", TelemetryFormat.BucketLabel(edges, 1, "K"));
            Assert.Equal("20K - 30K", TelemetryFormat.BucketLabel(edges, 2, "K"));
            Assert.Equal(">= 30K", TelemetryFormat.BucketLabel(edges, 3, "K"));
        }

        [Fact]

        public void BucketLabelsKeepAFractionalEdgeReadable()
        {
            float[] edges = { 2.8f, 50f };

            Assert.Equal("< 2.8K", TelemetryFormat.BucketLabel(edges, 0, "K"));
            Assert.Equal("2.8K - 50K", TelemetryFormat.BucketLabel(edges, 1, "K"));
        }

        [Fact]

        public void BucketLabelsAreInvariantToo()
        {
            InGermanLocale(() =>
            {
                Assert.Equal("< 2.8K", TelemetryFormat.BucketLabel(new float[] { 2.8f }, 0, "K"));
            });
        }

        [Fact]

        public void ABucketLabelWithoutEdgesDoesNotThrow()
        {
            Assert.Equal("all", TelemetryFormat.BucketLabel(null, 0, "K"));
            Assert.Equal("all", TelemetryFormat.BucketLabel(new float[0], 0, "K"));
        }

        [Fact]

        public void CsvFieldsAreCommaSeparatedAndTheLastEndsTheLine()
        {

            StringBuilder sb = new StringBuilder();

            TelemetryFormat.AppendCsv(sb, "Rusty Miner");
            TelemetryFormat.AppendCsv(sb, 12L);
            TelemetryFormat.AppendCsv(sb, 1.5);
            TelemetryFormat.AppendCsvLast(sb, double.NaN);

            Assert.Equal("\"Rusty Miner\",12,1.5,\n", sb.ToString());
        }

        [Fact]

        public void ARowHasAsManyFieldsAsItsHeaderEvenWhenValuesAreMissing()
        {

            StringBuilder sb = new StringBuilder();

            TelemetryFormat.AppendCsv(sb, (string)null);
            TelemetryFormat.AppendCsv(sb, double.NaN);
            TelemetryFormat.AppendCsvLast(sb, 0.0);

            Assert.Equal(3, sb.ToString().TrimEnd('\n').Split(',').Length);
        }

        [Fact]

        public void WhatTheShippedWriterQuotesTheHarnessReaderParsesBack()
        {
            string name = "The \"Iron\" Maiden, Mk II";
            string line = TelemetryFormat.Quote(name) + "," + TelemetryFormat.Quote("plain")
                + "," + TelemetryFormat.Quote("");

            var fields = Thermodynamics.Harness.CsvLine.Split(line);

            Assert.Equal(3, fields.Count);
            Assert.Equal(name, fields[0]);
            Assert.Equal("plain", fields[1]);
            Assert.Equal("", fields[2]);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The population constants the harness reasons from have a dataset behind them, and it is
    /// checked.**
    ///
    /// <para>
    /// `Census.Corpus` states what the corpus does — substep demand in vacuum and in air, waste per
    /// block, ship count — and every one of those figures was transcribed by hand out of a walk's
    /// output. `E5` says a figure comes from the dataset it is about, and `F14` records what
    /// happens otherwise: a page said *twenty-four sealed blocks* where the dataset said **1,184
    /// across 331 ships**, a sample figure standing as a population figure with only its share
    /// right. Nothing was checking these.
    /// </para>
    ///
    /// <para>
    /// The committed summary is the source. It is kilobytes and versioned, so a constant and the
    /// walk behind it can be compared without the gigabytes the walk produced — which is the whole
    /// reason `verdict.py --csv` exists.
    /// </para>
    /// </summary>
    public class CorpusConstantTests
    {
        private const string Summary = "summary-survey-2026-08-28.csv";


        private static Dictionary<string, double> Figures(string name)
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), "tools", "corpus", name);
            Assert.True(File.Exists(path), "no committed summary at " + path);

            Dictionary<string, double> figures = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (string line in File.ReadAllLines(path))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 2) continue;

                double value;
                if (double.TryParse(parts[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value))
                {
                    figures[parts[0]] = value;
                }
            }

            Assert.True(figures.Count > 20,
                "only " + figures.Count + " figures were read from " + name
                + ", so this test is not reading it");

            return figures;
        }

        [Fact]

        public void TheVacuumConstantsMatchTheCommittedSurvey()
        {

            Dictionary<string, double> figures = Figures(Summary);

            Within(0.01, Census.Corpus.VacuumP50, figures["G6 idle demand p50"], "vacuum p50");
            Within(0.01, Census.Corpus.VacuumP90, figures["G6 idle demand p90"], "vacuum p90");
        }


        private static void Within(double share, double constant, double measured, string what)
        {
            double drift = Math.Abs(measured - constant) / Math.Max(1e-9, Math.Abs(constant));

            Assert.True(drift <= share,
                what + ": the constant says " + constant.ToString("n3", CultureInfo.InvariantCulture)
                + " and the committed survey says "
                + measured.ToString("n3", CultureInfo.InvariantCulture) + ", which is "
                + (drift * 100d).ToString("n2", CultureInfo.InvariantCulture) + " % apart");
        }

        [Fact]

        public void TheShipCountNamesAWalkRatherThanThePopulation()
        {

            Dictionary<string, double> figures = Figures(Summary);

            Assert.Equal(8144d, figures["dataset population"]);

            Assert.True(figures["dataset ships"] > Census.Corpus.Ships,
                "the survey reached " + figures["dataset ships"] + " ships against the constant's "
                + Census.Corpus.Ships + ", so the constant is no longer the larger of the two and"
                + " the comment explaining the gap is out of date");

            Assert.True(figures["dataset ships"] - Census.Corpus.Ships < 100d,
                "the two walks are " + (figures["dataset ships"] - Census.Corpus.Ships)
                + " ships apart, which is too many to be the reader cap and the modded hulls the"
                + " constant names");
        }

        [Fact]

        public void TheAirConstantsAreStillAwaitingTheirWalk()
        {
            Assert.True(Census.Corpus.AirP50 > Census.Corpus.VacuumP50,
                "air is not stiffer than vacuum in the constants, which is the one thing every"
                + " measurement of the pair has agreed on");

            Assert.True(Census.Corpus.AirP90 > Census.Corpus.VacuumP90);
            Assert.True(Census.Corpus.AirMax >= Census.Corpus.AirP90);
            Assert.True(Census.Corpus.AirP10 < Census.Corpus.AirP50);
        }
    }
}

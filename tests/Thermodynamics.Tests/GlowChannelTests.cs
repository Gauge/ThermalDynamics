using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **How much of the glow's colour channel a player can actually see**, which is what
    /// backlog.md `B34` needed before the intent page could say who the
    /// two-channel design is for.
    /// </summary>
    public class GlowChannelTests
    {
        private readonly ITestOutputHelper output;


        public GlowChannelTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]

        public void TheColourConversionAgreesWithKnownValues()
        {
            double[] white = GlowChannelLab.Lab(new VRageMath.Vector3(1f, 1f, 1f));
            Assert.Equal(100d, white[0], 2);
            Assert.Equal(0d, white[1], 2);
            Assert.Equal(0d, white[2], 2);

            double[] black = GlowChannelLab.Lab(new VRageMath.Vector3(0f, 0f, 0f));
            Assert.Equal(0d, black[0], 2);

            double[] red = GlowChannelLab.Lab(new VRageMath.Vector3(1f, 0f, 0f));
            Assert.Equal(53.24d, red[0], 1);
            Assert.Equal(80.09d, red[1], 1);
            Assert.Equal(67.20d, red[2], 1);

            Assert.Equal(0d, GlowChannelLab.DeltaE(
                new VRageMath.Vector3(1f, 0f, 0f), new VRageMath.Vector3(1f, 0f, 0f)), 6);
        }

        [Fact]

        public void NoBlockMovesMoreThanAFewDeltaEAcrossItsWholeGlowBand()
        {
            if (!GameBlocks.IsInstalled) return;

            List<GlowChannelLab.Band> bands = GlowChannelLab.Bands();
            Assert.NotEmpty(bands);

            int visible = 0;
            int pinned = 0;

            foreach (GlowChannelLab.Band band in bands)
            {
                if (band.Visible) visible++;
                if (band.BelowDraper) pinned++;
            }

            output.WriteLine("{0} block types, {1} under the Draper point, {2} whose colour moves "
                + "a visible amount over their own band", bands.Count, pinned, visible);

            for (int i = 0; i < bands.Count && i < 5; i++)
            {
                output.WriteLine("most colour: {0} rated {1:n0} K, band {2:n0}-{1:n0}, dE {3:n2}",
                    bands[i].TypeId, bands[i].CriticalKelvin, bands[i].StartKelvin,
                    bands[i].DeltaE);
            }

            float atKelvin;
            double best = GlowChannelLab.BestBand(out atKelvin);
            output.WriteLine("the best hundred kelvin anywhere in the table: dE {0:n2} from {1:n0} K",
                best, atKelvin);

            Assert.InRange(pinned / (double)bands.Count, 0.1d, 0.45d);

            Assert.True(best < 12d,
                "the best hundred-kelvin band in the table carries dE " + best.ToString("n1")
                + ", which is not the flat channel this claim rests on");
        }

        [Fact]

        public void TheChannelIsBestInTheMiddleOfTheTableAndEvenThereItIsSmall()
        {
            double low = GlowChannelLab.DeltaE(
                Incandescence.Colour(800f), Incandescence.Colour(900f));

            double high = GlowChannelLab.DeltaE(
                Incandescence.Colour(2900f), Incandescence.Colour(3000f));

            float atKelvin;
            double best = GlowChannelLab.BestBand(out atKelvin);

            output.WriteLine("800-900 K: dE {0:n2}; 2,900-3,000 K: dE {1:n2}; best dE {2:n2} "
                + "from {3:n0} K", low, high, best, atKelvin);

            Assert.True(best > low && best > high,
                "the best band in the table is at one of its ends, so the shape described above "
                + "is wrong");

            Assert.InRange(atKelvin, 900f, 2000f);

            Assert.True(best < 3d * GlowChannelLab.JustNoticeable,
                "the best band carries dE " + best.ToString("n1") + ", which is more than three "
                + "just-noticeable differences and is not the flat channel this claim rests on");
        }

        [Fact]

        public void BetweenTheCoolestAndHottestRatedBlocksTheColourIsPlainlyDifferent()
        {
            if (!GameBlocks.IsInstalled) return;

            float coolest;
            float hottest;
            double across = GlowChannelLab.AcrossBlocks(out coolest, out hottest);

            output.WriteLine("{0:n0} K against {1:n0} K: dE {2:n2}", coolest, hottest, across);

            Assert.True(across > 4d * GlowChannelLab.JustNoticeable,
                "the coolest and hottest rated blocks in the game differ by dE "
                + across.ToString("n1") + " at their own ratings, so the colour channel says "
                + "nothing between blocks either and the design carries one channel outright");
        }
    }
}

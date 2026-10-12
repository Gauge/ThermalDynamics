using System.Text;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Frame pacing is the dump's answer to "was that hitch ours": the gap between draw calls, and
    /// this mod's simulation and draw time inside it. These pin the attribution and the period.
    /// </summary>
    public class FramePacingTests
    {
        [Fact]
        public void HitchesAreGapsAtTheThresholdAndCarryTheTimeSpentHereInsideThem()
        {
            var pacing = new FramePacing();

            pacing.AddSimulation(0.2);
            pacing.Record(1.0, 16.7, 0.1);

            pacing.AddSimulation(0.3);
            pacing.AddSimulation(0.4);
            pacing.Record(2.0, FramePacing.HitchMilliseconds, 0.5);

            Assert.Equal(2, pacing.Frames);
            Assert.Equal(1, pacing.Hitches);
            FramePacing.Hitch hitch = Assert.Single(pacing.Worst);
            Assert.Equal(0.7, hitch.SimulationMilliseconds, 9);
            Assert.Equal(0.5, hitch.DrawMilliseconds, 9);
            Assert.Equal(1.2, pacing.HitchOursMilliseconds, 9);
        }

        [Fact]
        public void SimulationIsChargedOnlyToTheGapItRanIn()
        {
            var pacing = new FramePacing();

            pacing.AddSimulation(5.0);
            pacing.Record(1.0, 16.7, 0.0);
            pacing.Record(2.0, 80.0, 0.0);

            Assert.Equal(0.0, Assert.Single(pacing.Worst).SimulationMilliseconds);
        }

        [Fact]
        public void WorstGapsAreKeptLongestFirstAndBounded()
        {
            var pacing = new FramePacing();
            for (int i = 0; i < FramePacing.WorstKept + 5; i++) pacing.Record(i, 60.0 + i, 0.0);

            Assert.Equal(FramePacing.WorstKept, pacing.Worst.Count);
            Assert.Equal(60.0 + FramePacing.WorstKept + 4, pacing.Worst[0].GapMilliseconds);
            for (int i = 1; i < pacing.Worst.Count; i++)
                Assert.True(pacing.Worst[i - 1].GapMilliseconds >= pacing.Worst[i].GapMilliseconds);
        }

        [Fact]
        public void APeriodicHitchReportsItsPeriod()
        {
            var pacing = new FramePacing();
            for (int i = 0; i < 20; i++)
            {
                for (int f = 0; f < 59; f++) pacing.Record(i * 2.5 + f * 0.0167, 16.7, 0.0);
                pacing.Record(i * 2.5 + 1.0, 120.0, 0.0);
            }

            Assert.Equal(20, pacing.Hitches);
            Assert.Equal(2.5, pacing.MedianSpacingSeconds(), 6);

            var sb = new StringBuilder();
            pacing.Write(sb);
            string text = sb.ToString();
            Assert.Contains("median spacing                2.50 s", text);
            Assert.Contains("ours inside hitches", text);
        }

        [Fact]
        public void NonFiniteGapsAreIgnored()
        {
            var pacing = new FramePacing();
            pacing.Record(0.0, double.NaN, 0.0);
            pacing.Record(0.0, double.PositiveInfinity, 0.0);
            pacing.Record(0.0, -1.0, 0.0);

            Assert.Equal(0, pacing.Frames);
        }
    }
}

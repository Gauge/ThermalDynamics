using Thermodynamics;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The per-frame cost tracker, which is what a report says when someone asks why the game
    /// stutters.
    ///
    /// Worth testing on its own because everything it does is bookkeeping that only shows up
    /// after a session — a frame closed at the wrong moment, a worst-frame list that keeps the
    /// most recent rather than the worst, or a hitch threshold that lets the list fill with
    /// ordinary frames all produce a report that looks plausible and says nothing.
    /// </summary>
    public class FrameCostTests
    {
        [Fact]

        public void AFrameWithNoWorkIsNotRecorded()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.EndFrame(1, 0.1);
            tracker.EndFrame(2, 0.2);

            Assert.Equal(0, tracker.FramesWithWork);
            Assert.Equal(0, tracker.Frame.Calls);
            Assert.Empty(tracker.Worst);
        }

        [Fact]

        public void AFrameCostsWhatEveryGridOnItCostTogether()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            for (int i = 0; i < 20; i++)
            {
                tracker.AddGrid("ship " + i, 2d, 40000);
            }
            tracker.EndFrame(1, 1d);

            Assert.Equal(1, tracker.FramesWithWork);
            Assert.Equal(40d, tracker.Frame.MaxMilliseconds, 6);
            Assert.Equal(1, tracker.FramesOverBudget);

            Assert.Single(tracker.Worst);
            Assert.Equal(20, tracker.Worst[0].Grids);
            Assert.Equal(2d, tracker.Worst[0].WorstGridMs, 6);
        }

        [Fact]

        public void TheWorstGridOnAFrameIsNamed()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("tug", 1d, 200);
            tracker.AddGrid("station", 30d, 300000);
            tracker.AddGrid("fighter", 2d, 900);
            tracker.EndFrame(7, 3d);

            FrameSample sample = tracker.Worst[0];
            Assert.Equal("station", sample.WorstGrid);
            Assert.Equal(300000, sample.WorstGridBlocks);
            Assert.Equal(33d, sample.TotalMs, 6);
            Assert.Equal(3, sample.Grids);
        }

        [Fact]

        public void StagesAndWorkCountsAreCarriedWithTheFrame()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("station", 30d, 300000);
            tracker.AddStage(0, 12d);
            tracker.AddStage(1, 8d);
            tracker.AddStage(2, 4d);
            tracker.AddStage(3, 6d);
            tracker.AddWork(300000, 4096, 1500000);
            tracker.EndFrame(9, 4d);

            FrameSample sample = tracker.Worst[0];
            Assert.Equal(12d, sample.TopologyMs, 6);
            Assert.Equal(8d, sample.RoomMappingMs, 6);
            Assert.Equal(4d, sample.ExposureMs, 6);
            Assert.Equal(6d, sample.SolverMs, 6);
            Assert.Equal(300000, sample.TopologyNodeVisits);
            Assert.Equal(1500000, sample.RoomCellsVisited);
        }

        [Fact]

        public void TheStagesAroundAStepReachTheFrameToo()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("station", 30d, 300000);
            tracker.AddStage(3, 6d);
            tracker.AddSample(9d);
            tracker.AddAfterStep(11d);
            tracker.EndFrame(1, 1d);

            FrameSample sample = tracker.Worst[0];
            Assert.Equal(9d, sample.SampleMs, 6);
            Assert.Equal(11d, sample.AfterStepMs, 6);
            Assert.Equal(4d, sample.UnattributedMs, 6);
            Assert.Contains("after step 11.00", sample.Describe());
        }

        [Fact]

        public void WhatNoStageClaimedIsTheRemainderAndCanGoNegative()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("station", 20d, 300000);
            tracker.AddStage(0, 5d);
            tracker.AddStage(1, 5d);
            tracker.AddStage(2, 5d);
            tracker.AddStage(3, 5d);
            tracker.EndFrame(1, 1d);

            Assert.Equal(0d, tracker.Worst[0].UnattributedMs, 6);


            FrameCostTracker overlapping = new FrameCostTracker();
            overlapping.AddGrid("station", 20d, 300000);
            overlapping.AddStage(3, 15d);
            overlapping.AddAfterStep(15d);
            overlapping.EndFrame(1, 1d);

            Assert.Equal(-10d, overlapping.Worst[0].UnattributedMs, 6);
        }

        [Fact]

        public void EndingAFrameClearsWhatItAccumulated()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("station", 30d, 300000);
            tracker.AddStage(0, 12d);
            tracker.AddSample(3d);
            tracker.AddAfterStep(4d);
            tracker.AddWork(300000, 0, 0);
            tracker.EndFrame(1, 1d);

            tracker.AddGrid("tug", 1d, 200);
            tracker.EndFrame(2, 2d);

            Assert.Equal(2, tracker.Frame.Calls);
            Assert.Equal(1d, tracker.Frame.LastMilliseconds, 6);

            Assert.Single(tracker.Worst);
            Assert.Equal(1, tracker.Worst[0].Frame);
            Assert.Equal(3d, tracker.Worst[0].SampleMs, 6);
        }

        [Fact]

        public void TheListKeepsTheWorstFramesAndNotTheLatest()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            tracker.AddGrid("station", 500d, 300000);
            tracker.EndFrame(1, 1d);

            for (int i = 0; i < FrameCostTracker.Keep * 3; i++)
            {
                tracker.AddGrid("ship", 10d, 4000);
                tracker.EndFrame(100 + i, 2d + i);
            }

            Assert.Equal(FrameCostTracker.Keep, tracker.Worst.Count);
            Assert.Equal(1, tracker.Worst[0].Frame);
            Assert.Equal(500d, tracker.Worst[0].TotalMs, 6);

            for (int i = 1; i < tracker.Worst.Count; i++)
            {
                Assert.True(tracker.Worst[i - 1].TotalMs >= tracker.Worst[i].TotalMs);
            }
        }

        [Fact]

        public void OrdinaryFramesDoNotFillTheHitchList()
        {

            FrameCostTracker tracker = new FrameCostTracker();

            for (int i = 0; i < 500; i++)
            {
                tracker.AddGrid("ship", 0.4d, 4000);
                tracker.EndFrame(i, i * 0.016);
            }

            Assert.Equal(500, tracker.FramesWithWork);
            Assert.Equal(0, tracker.FramesOverBudget);
            Assert.Empty(tracker.Worst);
        }

        [Fact]

        public void SpikeRatioSeparatesUniformlySlowFromOccasionallyEnormous()
        {

            FrameCostTracker uniform = new FrameCostTracker();
            for (int i = 0; i < 100; i++)
            {
                uniform.AddGrid("ship", 10d, 4000);
                uniform.EndFrame(i, i);
            }


            FrameCostTracker spiky = new FrameCostTracker();
            for (int i = 0; i < 99; i++)
            {
                spiky.AddGrid("ship", 0.1d, 4000);
                spiky.EndFrame(i, i);
            }
            spiky.AddGrid("station", 1000d, 300000);
            spiky.EndFrame(99, 99);

            Assert.True(uniform.SpikeRatio < 1.1d,
                "a uniformly expensive session should not read as spiky: " + uniform.SpikeRatio);
            Assert.True(spiky.SpikeRatio > 50d,
                "one enormous frame in a hundred should read as spiky: " + spiky.SpikeRatio);
        }
    }
}

using Thermodynamics.Presentation;
using System.Text;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Probe report aggregation, bounded retention, missing samples and repeatable snapshots.</summary>
    public class ThermalVisionTelemetryTests
    {
        [Fact]

        public void ColdWarmMissingAndPartialResultsRemainDistinct()
        {

            var stats = new ThermalVisionTelemetry();
            stats.Frame("Cividis | reactor | submitted", 500, 10, 20, false, true, 4, 9, 1);
            stats.Frame("Cividis | reactor | submitted", 600, 12, 22, false, false, 1);
            stats.Frame("WhiteHot | armour | no-fat-block-mesh", 300, 0, 0, false, false, .5f);
            stats.Frame("WhiteHot | door | partial", 350, 8, 24, true, false, 2);
            stats.Frame("no target", float.NaN, 0, 0, false, false, .1f);
            stats.Suppress();
            stats.Failure();

            var sb = new StringBuilder();
            stats.Write(sb);
            string report = sb.ToString();
            Assert.Equal(5, stats.Frames);
            Assert.Equal(1, stats.ColdMilliseconds.Count);
            Assert.Equal(4, stats.WarmMilliseconds.Count);
            Assert.Contains("no-fat-block-mesh: frames=1 zero-submitted=1", report);
            Assert.Contains("partial: frames=1 zero-submitted=0 partial=1", report);
            Assert.Contains("K min/mean/max: -", report);
            Assert.Contains("menu-suppressed=1 render-errors=1", report);
            Assert.Contains("backfaces-total=9 degenerate-total=1", report);
            Assert.Contains("do NOT prove visibility", report);
        }

        [Fact]

        public void OverflowIsCountedAndNewestEventsSurvive()
        {

            var stats = new ThermalVisionTelemetry();
            for (int i = 0; i < ThermalVisionTelemetry.RowLimit + 3; i++)
                stats.Frame("block-" + i, 300, 1, 2, false, false, 1);
            for (int i = 0; i < ThermalVisionTelemetry.EventLimit + 2; i++)
                stats.Event(i, "event-" + i + "END");

            var sb = new StringBuilder();
            stats.Write(sb);
            Assert.Equal(3, stats.RowsOverflowed);
            Assert.Equal(2, stats.EventsDropped);
            Assert.Contains("OTHER (row cap; merged frames=3): frames=3", sb.ToString());
            Assert.DoesNotContain("event-0END", sb.ToString());
            Assert.Contains("event-65END", sb.ToString());
        }

        [Fact]

        public void TargetChurnCannotEvictNotesAndImportantHistoryIsBounded()
        {

            var stats = new ThermalVisionTelemetry();
            stats.Event(0, "tester note: slope flickers", true);
            stats.Event(1, "automatic off: ineligible viewpoint", true);
            for (int i = 0; i < 500; i++) stats.Event(i, "target changed");

            var sb = new StringBuilder();
            stats.Write(sb);
            Assert.Contains("tester note: slope flickers", sb.ToString());
            Assert.Contains("automatic off: ineligible viewpoint", sb.ToString());
            Assert.Equal(0, stats.ImportantEventsDropped);
            for (int i = 0; i < ThermalVisionTelemetry.EventLimit; i++) stats.Event(i, "mode changed", true);
            Assert.Equal(2, stats.ImportantEventsDropped);
        }

        [Fact]

        public void SceneReportDistinguishesCandidatesFromSubmissionsAndLimits()
        {

            var stats = new ThermalVisionTelemetry();
            stats.SceneFrame(400, 100, 20, 15, 5, true, 200, 700, true, 1.1f, 2.2f);
            stats.SceneFrame(0, 100, 100, 90, 10, false, 200, 700, false, .01f, 2.1f);

            var sb = new StringBuilder();
            stats.Write(sb);
            Assert.Contains("Scene frames=2 discovery/draw-limit frames=1", sb.ToString());
            Assert.Contains("submitted blocks:", sb.ToString());
            Assert.Contains("Discovery refreshes=1", sb.ToString());
            Assert.Equal(2, stats.SceneDiscoveryMilliseconds.Count);
            Assert.Equal(2, stats.SceneDrawingMilliseconds.Count);
            Assert.Contains("hidden blocks may affect AUTO", sb.ToString());
            Assert.Contains("Low K:", sb.ToString());
        }

        [Fact]

        public void SceneLimitsCanOverlapWithoutHidingWhichBudgetWasReached()
        {

            var stats = new ThermalVisionTelemetry();
            stats.SceneFrame(200, 150, 120, 110, 10, true, 200, 700);
            stats.SceneBudget(ThermalVisionSceneLimit.Discovery | ThermalVisionSceneLimit.Time
                | ThermalVisionSceneLimit.ArmourQuota, 100, 10);
            stats.SceneFrame(0, 150, 120, 110, 10, true, 200, 700);
            stats.SceneBudget(ThermalVisionSceneLimit.ModelBuild | ThermalVisionSceneLimit.Triangles, 90, 20);

            var sb = new StringBuilder();
            stats.Write(sb);
            Assert.Contains("discovery=1 blocks=0 triangles=1 time=1 model-build=1 armour-quota=1", sb.ToString());
            Assert.Contains("Armour submitted blocks:", sb.ToString());
            Assert.Contains("entity submitted blocks:", sb.ToString());
        }

        [Fact]

        public void ProgressiveBuildReportIncludesPendingAndCompletionFrames()
        {

            var stats = new ThermalVisionTelemetry();
            stats.SceneFrame(100, 80, 60, 50, 10, true, 200, 700);
            stats.SceneBuild(8192, true, .9f);
            stats.SceneBuild(8192, true, .8f);
            stats.SceneBuild(512, false, .1f);
            stats.SceneCulling(972, 51840);

            var sb = new StringBuilder();
            stats.Write(sb);
            Assert.Contains("Progressive build pending frames=2", sb.ToString());
            Assert.Contains("source triangles/frame:", sb.ToString());
            Assert.Contains("build CPU ms:", sb.ToString());
            Assert.Contains("triangles rejected by batch:", sb.ToString());
        }

        [Fact]

        public void NotesCannotInjectReportLinesAndSnapshotsDoNotDrainHistory()
        {

            var stats = new ThermalVisionTelemetry();
            stats.Event(2, "door\r\nlooks good" + new string('x', 1000));

            var first = new StringBuilder();

            var second = new StringBuilder();
            stats.Write(first);
            stats.Write(second);
            Assert.Equal(first.ToString(), second.ToString());
            Assert.Contains("door  looks good", first.ToString());
            Assert.DoesNotContain(new string('x', 321), first.ToString());
            Assert.Equal(0, new ThermalVisionTelemetry().Frames);
        }
    }
}

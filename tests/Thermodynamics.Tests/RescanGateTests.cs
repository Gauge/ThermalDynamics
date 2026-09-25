using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The gate on a periodic diagnostic scan.
    ///
    /// It exists because the lost-room scan re-derived the same answer for every grid in a fleet
    /// every thirty seconds — a game call per external cell, on 292 grids, all inside one frame's
    /// after-step. Two properties matter and they pull against each other: a scan must never be
    /// skipped when its subject has moved, and must never repeat when it has not.
    /// </summary>
    public class RescanGateTests
    {
        private const int Interval = 240;


        private static int Run(RescanGate gate, int version, int steps)
        {
            int scans = 0;
            for (int i = 0; i < steps; i++)
            {
                if (gate.Due(version, 1)) scans++;
            }

            return scans;
        }

        [Fact]

        public void TheFirstCheckScans()
        {

            RescanGate gate = new RescanGate(Interval);

            Assert.True(gate.Due(0, 1));
            Assert.True(gate.HasScanned);
        }

        [Fact]

        public void NothingScansAgainUntilTheIntervalHasPassed()
        {

            RescanGate gate = new RescanGate(Interval);
            gate.Due(0, 1);

            Assert.Equal(0, Run(gate, 1, Interval - 1));
            Assert.True(gate.Due(1, 1));
        }

        [Fact]

        public void AnUnchangedSubjectIsNeverScannedTwice()
        {

            RescanGate gate = new RescanGate(Interval);

            Assert.Equal(1, Run(gate, 7, Interval * 20));
            Assert.True(gate.Skipped > 0);
        }

        [Fact]

        public void AChangedSubjectIsScannedAtTheNextInterval()
        {

            RescanGate gate = new RescanGate(Interval);
            Run(gate, 7, Interval * 4);

            Assert.Equal(1, Run(gate, 8, Interval + 1));
        }

        [Fact]

        public void ASubjectChangingConstantlyIsStillHeldToTheCadence()
        {

            RescanGate gate = new RescanGate(Interval);

            int scans = 0;
            for (int i = 0; i < Interval * 10; i++)
            {
                if (gate.Due(i, 1)) scans++;
            }

            Assert.InRange(scans, 9, 11);
        }

        [Fact]

        public void GoingIdleMakesTheNextReaderWaitForNothing()
        {

            RescanGate gate = new RescanGate(Interval);
            Run(gate, 3, Interval * 2);

            gate.Idle();

            Assert.False(gate.HasScanned);
            Assert.True(gate.Due(3, 1));
        }

        [Fact]

        public void AForcedScanSatisfiesTheCadence()
        {

            RescanGate gate = new RescanGate(Interval);
            gate.Mark(12);

            Assert.Equal(0, Run(gate, 12, Interval * 3));
            Assert.True(gate.HasScanned);
        }

        [Fact]

        public void AMultiStepTickAdvancesByAllOfIt()
        {

            RescanGate gate = new RescanGate(Interval);
            gate.Due(0, 1);

            Assert.Equal(0, gate.Due(1, Interval - 4) ? 1 : 0);
            Assert.True(gate.Due(1, 8));
        }

        [Fact]

        public void AnIntervalOfZeroScansEveryChange()
        {

            RescanGate gate = new RescanGate(0);

            Assert.True(gate.Due(1, 1));
            Assert.False(gate.Due(1, 1));
            Assert.True(gate.Due(2, 1));
        }
    }
}

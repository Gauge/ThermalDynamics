using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A client that joined with the wrong temperatures converges on the server's, and this is what
    /// keeps the figures that decision rests on from moving quietly.
    ///
    /// <para>
    /// Block temperatures are not replicated: a client re-simulates from the same inputs, and a
    /// client joining mid-session starts from whatever the world was last saved at. The open
    /// question was how far it may drift before it has to be corrected (backlog.md
    /// `B4`), and the answer is that the model is dissipative — the disagreement decays on its own,
    /// so what matters is how fast, and what the readout says while it lasts.
    /// </para>
    ///
    /// <para>
    /// The runs here are small and short because the *claims* are about shape rather than about a
    /// particular hull: convergence happens, air is faster than vacuum, and a run against itself
    /// agrees exactly. The figures a decision is quoted from come from `-- drift`, at sizes a test
    /// suite has no business running.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class ClientDriftTests
    {
        [Fact]

        public void ARunAgainstItselfDisagreesAboutNothing()
        {
            ClientDriftLab.Run run = ClientDriftLab.Measure("shadow", 0f, 30f, 400);

            Assert.NotEmpty(run.Samples);
            Assert.Equal(0f, run.JoinKelvin, 5);

            foreach (ClientDriftLab.Sample sample in run.Samples)
            {
                Assert.Equal(0f, sample.MaxKelvin, 5);
                Assert.Equal(0, sample.DisagreeOnCritical);
            }

            Assert.Equal(0f, run.SecondsMisreadingCritical, 5);
        }

        [Fact]

        public void AStaleClientStartsWrong()
        {
            ClientDriftLab.Run run = ClientDriftLab.Measure("shadow", 60f, 30f, 400);

            Assert.True(run.JoinKelvin > 5f,
                "a client sixty seconds behind should disagree by more than five kelvin, got "
                + run.JoinKelvin);
        }

        [Fact]

        public void TheDisagreementDecaysOnItsOwn()
        {
            ClientDriftLab.Run run = ClientDriftLab.Measure("shadow", 60f, 300f, 400);

            Assert.NotEmpty(run.Samples);

            float last = run.Samples[run.Samples.Count - 1].MaxKelvin;
            Assert.True(last < run.JoinKelvin * 0.5f,
                "five minutes in, the disagreement should be well under half what it started at: "
                + run.JoinKelvin + " K to " + last + " K");

            for (int i = 1; i < run.Samples.Count; i++)
            {
                Assert.True(run.Samples[i].MaxKelvin <= run.Samples[i - 1].MaxKelvin + 0.001f,
                    "the disagreement grew between " + run.Samples[i - 1].Seconds + " s and "
                    + run.Samples[i].Seconds + " s");
            }
        }

        [Fact]

        public void AirConvergesFasterThanVacuum()
        {
            ClientDriftLab.Run vacuum = ClientDriftLab.Measure("shadow", 60f, 120f, 400);
            ClientDriftLab.Run air = ClientDriftLab.Measure("planet", 60f, 120f, 400);

            float vacuumEnd = vacuum.Samples[vacuum.Samples.Count - 1].MaxKelvin;
            float airEnd = air.Samples[air.Samples.Count - 1].MaxKelvin;

            Assert.True(airEnd < vacuumEnd,
                "air should have converged further than vacuum in the same time: "
                + airEnd + " K against " + vacuumEnd + " K");
        }


        [Fact]

        public void AClientThatKeepsLosingTimeHoldsAStandingError()
        {
            ClientDriftLab.Run steady = ClientDriftLab.Measure("shadow", 0f, 240f, 400, null,
                ClientDriftLab.Correction.None, ClientDriftLab.Machine.KeepsUp);

            ClientDriftLab.Run hitching = ClientDriftLab.Measure("shadow", 0f, 240f, 400, null,
                ClientDriftLab.Correction.None,
                new ClientDriftLab.Machine { HitchEverySeconds = 10f, HitchLosesSeconds = 5f });

            Assert.Equal(0f, steady.Samples[steady.Samples.Count - 1].MaxKelvin, 4);

            Assert.True(hitching.Hitches > 0, "the rig took no hitches");
            Assert.True(hitching.SecondsLostToHitches > 0f, "the client lost no time");

            float last = hitching.Samples[hitching.Samples.Count - 1].MaxKelvin;
            Assert.True(last > 0.5f,
                "a client still losing time should still be wrong at the end of the run, got "
                + last + " K");
        }


        [Fact]

        public void CorrectingTheBandLeavesTheReadoutWrongForLessTime()
        {
            ClientDriftLab.Run alone = ClientDriftLab.Measure("shadow", 60f, 300f, 2000, null,
                ClientDriftLab.Correction.None);

            ClientDriftLab.Run corrected = ClientDriftLab.Measure("shadow", 60f, 300f, 2000, null,
                new ClientDriftLab.Correction { IntervalSeconds = 5f });

            Assert.True(alone.SecondsShowingSafe > 0f,
                "the rig needs an uncorrected client that misreads critical; it did not");

            Assert.True(corrected.SecondsShowingSafe < alone.SecondsShowingSafe,
                "the correction should shorten the time the client shows safe: "
                + corrected.SecondsShowingSafe + " s against " + alone.SecondsShowingSafe + " s");
        }

        [Fact]

        public void ALongerIntervalLeavesTheReadoutWrongForLonger()
        {
            ClientDriftLab.Run tight = ClientDriftLab.Measure("shadow", 60f, 300f, 2000, null,
                new ClientDriftLab.Correction { IntervalSeconds = 5f });

            ClientDriftLab.Run loose = ClientDriftLab.Measure("shadow", 60f, 300f, 2000, null,
                new ClientDriftLab.Correction { IntervalSeconds = 60f });

            Assert.True(loose.SecondsShowingSafe > tight.SecondsShowingSafe,
                "a minute between updates should be worse than five seconds: "
                + loose.SecondsShowingSafe + " s against " + tight.SecondsShowingSafe + " s");

            Assert.True(loose.Updates < tight.Updates);
            Assert.True(loose.Bytes < tight.Bytes);
        }

        [Fact]

        public void TheBytesChargedAreTheBytesTheCodecPacks()
        {
            ClientDriftLab.Run run = ClientDriftLab.Measure("shadow", 60f, 60f, 2000, null,
                new ClientDriftLab.Correction { IntervalSeconds = 5f });

            Assert.True(run.Updates > 0);
            Assert.True(run.Bytes >= run.Updates * (long)HotTailCodec.HeaderSize);
            Assert.True(run.Bytes
                <= run.Updates * (long)HotTailCodec.SizeOf(run.PeakBlocksSent));
        }

        [Fact]

        public void TheCorrectionSwitchedOffSendsNothing()
        {
            ClientDriftLab.Run run = ClientDriftLab.Measure("shadow", 60f, 60f, 400, null,
                ClientDriftLab.Correction.None);

            Assert.Equal(0, run.Updates);
            Assert.Equal(0L, run.Bytes);
            Assert.Equal(0f, run.BytesPerSecond, 5);
        }
    }
}

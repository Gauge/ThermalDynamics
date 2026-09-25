using System.Collections.Generic;
using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// That a validator's finding reaches somebody.
    ///
    /// <para>
    /// `BlockThermalProperties.Validate` and `ThermalSettings.Validate` were correct and tested for
    /// as long as they existed, and were called from nowhere the game runs — the whole of
    /// backlog.md `A19`, and the defect class `D2` exists to catch: built,
    /// documented, reached by nothing. Tests that call a validator directly cannot see that, which
    /// is why these test the collector instead: what it says, how often it says it, and that a
    /// finding survives having nowhere to write it.
    /// </para>
    ///
    /// <para>
    /// The game side of the wiring — the two call sites and the log — is a game type and cannot run
    /// here; `NoDocCommentDescribesSomethingThatIsNotThere` and the build are what hold it.
    /// </para>
    /// </summary>
    [Collection("ThermalValidation")]
    public class ValidationReportingTests
    {

        private static List<string> Capture()
        {
            ThermalValidation.Reset();

            List<string> written = new List<string>();
            ThermalValidation.Writer = written.Add;
            return written;
        }

        [Fact]

        public void AnUnphysicalBlockPropertyIsNamedWithTheBlockThatCarriesIt()
        {

            List<string> written = Capture();
            try
            {
                ThermalValidation.Check("SomeModsReactor", new BlockThermalProperties
                {
                    Emissivity = 2f,
                    ProducerWasteEnergy = 3f,
                });

                Assert.Equal(2, written.Count);
                foreach (string line in written)
                {
                    Assert.StartsWith("SomeModsReactor: ", line);
                }

                Assert.Equal(written, ThermalValidation.Problems);
            }
            finally
            {
                ThermalValidation.Writer = null;
                ThermalValidation.Reset();
            }
        }

        [Fact]

        public void TheSameProblemIsSaidOnceHoweverOftenItIsFound()
        {

            List<string> written = Capture();
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    ThermalValidation.Check("Repeater",
                        new BlockThermalProperties { SpecificHeat = 0f });
                }

                Assert.Single(written);
                Assert.Single(ThermalValidation.Problems);

                ThermalValidation.Check("Another", new BlockThermalProperties { SpecificHeat = 0f });
                Assert.Equal(2, written.Count);
            }
            finally
            {
                ThermalValidation.Writer = null;
                ThermalValidation.Reset();
            }
        }

        [Fact]

        public void NothingIsSaidAboutValuesThatAreFine()
        {

            List<string> written = Capture();
            try
            {
                ThermalValidation.Check("Fine", new BlockThermalProperties());
                ThermalValidation.Check(new ThermalSettings().Derive());

                Assert.Empty(written);
                Assert.Equal(0, ThermalValidation.Count);
            }
            finally
            {
                ThermalValidation.Writer = null;
                ThermalValidation.Reset();
            }
        }

        [Fact]

        public void ASettingsFaultIsReportedAfterItHasBeenDerived()
        {

            List<string> written = Capture();
            try
            {

                ThermalSettings settings = new ThermalSettings();
                settings.Frequency = 2;
                settings.HeatTimeScale = 20000f;
                settings.Derive();

                ThermalValidation.Check(settings);

                Assert.NotEmpty(written);
                Assert.All(written, line => Assert.StartsWith("settings: ", line));
                Assert.Contains(written, line => line.Contains("HeatTimeScale / Frequency"));
            }
            finally
            {
                ThermalValidation.Writer = null;
                ThermalValidation.Reset();
            }
        }

        [Fact]

        public void AProblemFoundBeforeAnybodyIsListeningIsStillRecorded()
        {
            ThermalValidation.Reset();
            ThermalValidation.Writer = null;
            try
            {
                ThermalValidation.Check("Early", new BlockThermalProperties { Emissivity = 4f });
                Assert.Equal(1, ThermalValidation.Count);
            }
            finally
            {
                ThermalValidation.Reset();
            }
        }

        [Fact]

        public void AWriterThatThrowsIsNotTheReasonAWorldFailsToLoad()
        {
            ThermalValidation.Reset();
            ThermalValidation.Writer = delegate { throw new System.InvalidOperationException(); };
            try
            {
                ThermalValidation.Check("Loud", new BlockThermalProperties { Emissivity = 4f });
                Assert.Equal(1, ThermalValidation.Count);
            }
            finally
            {
                ThermalValidation.Writer = null;
                ThermalValidation.Reset();
            }
        }
    }
}

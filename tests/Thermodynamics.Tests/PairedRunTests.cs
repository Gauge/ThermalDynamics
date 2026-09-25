using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The stopping rule must not be part of the difference.**
    ///
    /// <para>
    /// `Battery.Run` stops when the hottest block moves less than `SettleWithin` — a quarter of a
    /// kelvin — over a sixty-second chunk, which is what makes a corpus walk affordable: most runs
    /// are flat long before their clock runs out, and the sampled trial for `CorpusCapWalk` had 232
    /// of 320 runs stopping at 120 s of an 1,800 s scenario.
    /// </para>
    ///
    /// <para>
    /// That is fine for a figure read one run at a time and wrong for a *paired* one. Two
    /// configurations of the same ship stop at different instants, and the difference `C3` is about
    /// is a hundredth of a kelvin — two orders of magnitude under the tolerance that decides where
    /// each run stopped. A pair compared that way measures the stopping rule (`M1`, `P6`). So the
    /// control arm runs first and hands its own elapsed clock to the other through
    /// <see cref="Battery.RunForSeconds"/>, and these tests are what say that machinery works: that
    /// the clock is honoured, that the outcome records where it stopped, and that a pair given the
    /// same settings lands on the same numbers.
    /// </para>
    ///
    /// <para>
    /// Synthetic blueprints, in the tradition of `BlueprintTests`: the end of the chain is the walk
    /// itself, and what needs a test here is the harness the walk depends on.
    /// </para>
    /// </summary>
    public class PairedRunTests
    {

        private static string WriteBlueprint(string blocks)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "thermal-pair-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(path);

            string file = Path.Combine(path, "bp.sbc");
            File.WriteAllText(file,
                "<?xml version=\"1.0\"?>\n" +
                "<Definitions xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "  <ShipBlueprints>\n" +
                "    <ShipBlueprint>\n" +
                "      <Id Type=\"MyObjectBuilder_ShipBlueprintDefinition\" Subtype=\"PairShip\" />\n" +
                "      <CubeGrids>\n" +
                "        <CubeGrid>\n" +
                "          <DisplayName>Pair Ship</DisplayName>\n" +
                "          <GridSizeEnum>Large</GridSizeEnum>\n" +
                "          <CubeBlocks>\n" + blocks + "\n          </CubeBlocks>\n" +
                "        </CubeGrid>\n" +
                "      </CubeGrids>\n" +
                "    </ShipBlueprint>\n" +
                "  </ShipBlueprints>\n" +
                "</Definitions>\n");
            return file;
        }


        private static string Block(string subtype, int x, int y, int z)
        {
            return "<MyObjectBuilder_CubeBlock xsi:type=\"MyObjectBuilder_CubeBlock\">"
                + "<SubtypeName>" + subtype + "</SubtypeName>"
                + "<Min x=\"" + x + "\" y=\"" + y + "\" z=\"" + z + "\" />"
                + "</MyObjectBuilder_CubeBlock>";
        }


        private static Blueprints.Ship Ship()
        {
            System.Text.StringBuilder blocks = new System.Text.StringBuilder();
            const int side = 4;

            for (int x = 0; x < side; x++)
            {
                for (int y = 0; y < side; y++)
                {
                    for (int z = 0; z < side; z++)
                    {
                        blocks.Append(Block("LargeBlockArmorBlock", x, y, z));
                    }
                }
            }

            blocks.Append(Block("LargeBlockSmallGenerator", side, 0, 0));

            return Blueprints.Read(WriteBlueprint(blocks.ToString()))[0];
        }


        private static Battery.Scenario Scenario(string name)
        {
            foreach (Battery.Scenario scenario in Battery.All())
            {
                if (scenario.Name == name) return scenario;
            }

            throw new InvalidOperationException("no battery scenario named " + name);
        }

        [Fact]

        public void ASettleStoppedRunRecordsWhereItStoppedAndItIsNotTheClock()
        {
            if (!GameBlocks.IsInstalled) return;


            Battery.Scenario scenario = Scenario("surface-hot-noon");
            ScenarioOutcome outcome = Battery.Run(Ship(), scenario);

            Assert.True(outcome.RunSeconds > 0f, "the run advanced no simulated time at all");
            Assert.True(outcome.RunSeconds <= scenario.Seconds,
                "a run cannot advance past its own scenario's clock");
            Assert.True(outcome.RunSeconds < scenario.Seconds,
                "this hull settled at its clock, so it cannot show that a settle-stopped run stops "
                + "anywhere else and the pairing machinery has nothing to demonstrate on it");
        }

        [Fact]

        public void AFixedClockRunStopsWhereItWasTold()
        {
            if (!GameBlocks.IsInstalled) return;


            Battery.Scenario scenario = Scenario("vacuum-shadow");

            ScenarioOutcome control = Battery.Run(Ship(), scenario);
            ScenarioOutcome paired = Battery.RunForSeconds(Ship(), scenario, control.RunSeconds);

            Assert.Equal(control.RunSeconds, paired.RunSeconds, 3);

            float longer = control.RunSeconds + (2f * Battery.Chunk);
            ScenarioOutcome held = Battery.RunForSeconds(Ship(), scenario, longer);

            Assert.Equal(longer, held.RunSeconds, 3);
            Assert.True(held.RunSeconds > control.RunSeconds,
                "the fixed-clock run stopped where the settle-stopped one did, so the clock was "
                + "ignored");
        }

        [Fact]

        public void TwoArmsThatDifferInNothingAgreeExactly()
        {
            if (!GameBlocks.IsInstalled) return;


            Battery.Scenario scenario = Scenario("vacuum-shadow");

            ScenarioOutcome control = Battery.Run(Ship(), scenario);
            ScenarioOutcome first = Battery.RunForSeconds(Ship(), scenario, control.RunSeconds);
            ScenarioOutcome second = Battery.RunForSeconds(Ship(), scenario, control.RunSeconds);

            Assert.Equal(first.PeakKelvin, second.PeakKelvin);
            Assert.Equal(first.SubstepsDemanded, second.SubstepsDemanded);
            Assert.Equal(first.SubstepCost, second.SubstepCost);
            Assert.Equal(first.Links, second.Links);
        }

        [Fact]

        public void ACapMakesTheDemandTheSmallerOfItselfAndTheCap()
        {
            if (!GameBlocks.IsInstalled) return;


            Battery.Scenario scenario = Scenario("vacuum-shadow");

            ScenarioOutcome uncapped = Battery.Run(Ship(), scenario, Settings(0));
            Assert.True(uncapped.SubstepsDemanded > 1f,
                "this hull demands " + uncapped.SubstepsDemanded.ToString("n2") + " substeps, so no "
                + "cap above one can bind on it and the identity is untestable here");

            int cap = (int)Math.Floor(uncapped.SubstepsDemanded / 2f);
            if (cap < 1) cap = 1;

            ScenarioOutcome capped =
                Battery.RunForSeconds(Ship(), scenario, uncapped.RunSeconds, Settings(cap));

            Assert.Equal(Math.Min(uncapped.SubstepsDemanded, cap), capped.SubstepsDemanded, 2);

            Assert.Equal(0, uncapped.FlooredNodes);
            Assert.True(capped.FlooredNodes > 0,
                "the cap moved the demand and floored no node, which cannot both be true");

            ScenarioOutcome loose = Battery.RunForSeconds(Ship(), scenario, uncapped.RunSeconds,
                Settings((int)Math.Ceiling(uncapped.SubstepsDemanded) + 8));

            Assert.Equal(uncapped.SubstepsDemanded, loose.SubstepsDemanded, 3);
        }


        private static ThermalSettings Settings(int cap)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubstepsPerBlock = cap;
            return settings;
        }

        [Fact]

        public void TheRecordedRowSaysWhichArmItIs()
        {

            List<string> header = new List<string>(CorpusRecord.OutcomeHeader.Split(','));
            Assert.Contains("cap", header);
            Assert.Contains("run_seconds", header);
            Assert.Contains("floored", header);

            ScenarioOutcome outcome = new ScenarioOutcome
            {
                Ship = "hull",
                Scenario = "vacuum-shadow",
                SubstepsPerBlockCap = 6,
                RunSeconds = 120f,
                FlooredNodes = 37,
            };

            string[] cells = CorpusRecord.Row("cap", outcome).Split(',');

            Assert.Equal(header.Count, cells.Length);
            Assert.Equal("6", cells[header.IndexOf("cap")]);
            Assert.Equal("120", cells[header.IndexOf("run_seconds")]);
            Assert.Equal("37", cells[header.IndexOf("floored")]);
        }
    }
}

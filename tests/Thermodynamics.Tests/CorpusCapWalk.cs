using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every ship in the corpus, in air, **twice**: with the per-block substep cap off as it ships,
    /// and with `MaxSubstepsPerBlock 6`.
    ///
    /// <para>
    /// **Why it exists.** backlog.md `C3` asks whether that cap should be
    /// a default and is undecided only because what it costs has been measured on *one hull* —
    /// 0.028 K on the worst-placed block of a driven census hull. `G6`'s cost half fails in air
    /// (`F11`), and the cap is the one lever that lowers a step's work rather than moving it into
    /// the frame. They are the same question, and this is the measurement. What the walk is
    /// expected to say was written into
    /// balance-lab.md
    /// with its falsifiers before any of this ran (`E1`, `E11`).
    /// </para>
    ///
    /// <para>
    /// **The one thing the design has to get right: both arms run to the same simulated clock.**
    /// `Battery.Run` stops when the hottest block moves less than 0.25 K in a sixty-second chunk,
    /// so two configurations of one ship stop at different instants — and the difference this walk
    /// is measuring is expected to be a hundredth of a kelvin, two orders of magnitude under that
    /// tolerance. Comparing two settle-stopped runs would have measured the stopping rule (`M1`,
    /// `P6`). So the uncapped arm runs first, and its elapsed clock is handed to the capped one
    /// through <see cref="Battery.RunForSeconds"/>.
    /// </para>
    ///
    /// <para>
    /// **What it judges, and what it only records.** The population figures are scored downstream
    /// by `verdict.py` and `cap.py` against markers already written down; what this walk asserts is
    /// the four things only the run itself can check — that the air scenarios carried convection,
    /// that the cap held something back, that a capped run is never *stiffer* than its own control,
    /// and that the two arms really did run the same clock. The identity the projection rested on —
    /// capped demand being `min(uncapped demand, cap)` — is asserted here too, because it is the
    /// assumption under the predicted benefit and a walk that does not test its own premise has
    /// tested nothing (`P4`).
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class CorpusCapWalk
    {
        private const string Label = "cap";

        private const int Cap = 6;

        private const float Slack = 0.01f;


        private static List<Battery.Scenario> Scenarios()
        {
            return ScenarioIndex.Resolve(PairLab.AirScenarios, "the cap walk");
        }

        private const string Anchor = "vacuum-shadow";


        private static ThermalSettings Arm(int cap)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxSubstepsPerBlock = cap;
            return settings;
        }

        private class Walked
        {
            public string Ship;
            public bool Convected;
            public bool Floored;

            public readonly List<ScenarioOutcome> Outcomes = new List<ScenarioOutcome>();

            public readonly List<string> Violations = new List<string>();
        }

        [Fact]

        public void EveryShipInTheCorpusIsMeasuredWithTheCapAndWithoutIt()
        {
            if (CorpusFixture.Files().Count == 0) return;


            List<Battery.Scenario> scenarios = Scenarios();

            List<Walked> results = CorpusFixture.Sweep(Label,
                delegate (Blueprints.Ship ship) { return Walk(ship, scenarios); });

            Assert.True(results.Count > 0, "the corpus yielded no ships to walk under the cap");


            List<string> violations = new List<string>();
            int convected = 0;
            int floored = 0;

            foreach (Walked ship in results)
            {
                if (ship.Convected) convected++;
                if (ship.Floored) floored++;
                violations.AddRange(ship.Violations);
            }

            Assert.True(convected > 0,
                "no ship of " + results.Count + " recorded any convection, so this walk measured "
                + "vacuum under three atmospheric names");

            Assert.True(floored > 0,
                "the cap held nothing back on any of " + results.Count + " ships, so the capped arm "
                + "is the control run twice and every delta this dataset carries is noise");

            Assert.True(violations.Count == 0,
                violations.Count + " violations across " + results.Count + " ships:\n  "
                + string.Join("\n  ", violations.ToArray()));
        }


        private static Walked Walk(Blueprints.Ship ship, List<Battery.Scenario> scenarios)
        {
            Walked walked = new Walked { Ship = ship.Name };

            Dictionary<string, ScenarioOutcome> control =
                new Dictionary<string, ScenarioOutcome>(StringComparer.Ordinal);

            foreach (Battery.Scenario scenario in scenarios)
            {
                ScenarioOutcome uncapped = Battery.Run(ship, scenario, Arm(0));
                uncapped.SubstepsPerBlockCap = 0;

                ScenarioOutcome capped =
                    Battery.RunForSeconds(ship, scenario, uncapped.RunSeconds, Arm(Cap));
                capped.SubstepsPerBlockCap = Cap;

                control[scenario.Name] = uncapped;
                walked.Outcomes.Add(uncapped);
                walked.Outcomes.Add(capped);

                Judge(walked, scenario.Name, uncapped, capped);
            }

            CorpusRecord.Outcomes(Label, walked.Outcomes);

            ScenarioOutcome anchor = control[Anchor];

            if (anchor.Blocks != ship.Blocks)
            {
                walked.Violations.Add("accounting: " + ship.Name + " read " + ship.Blocks
                    + " blocks and built " + anchor.Blocks + " nodes");
                return walked;
            }

            foreach (KeyValuePair<string, ScenarioOutcome> entry in control)
            {
                if (entry.Key == Anchor) continue;

                if (entry.Value.ConvectionWatts != 0f) walked.Convected = true;

                if (entry.Value.SubstepsDemanded + Slack < anchor.SubstepsDemanded)
                {
                    walked.Violations.Add("air-softer: " + ship.Name + " / " + entry.Key
                        + " demands " + entry.Value.SubstepsDemanded.ToString("n2")
                        + " substeps against " + anchor.SubstepsDemanded.ToString("n2")
                        + " in vacuum");
                }
            }

            return walked;
        }


        private static void Judge(Walked walked, string scenario,
            ScenarioOutcome uncapped, ScenarioOutcome capped)
        {
            string where = walked.Ship + " / " + scenario;

            if (Math.Abs(capped.RunSeconds - uncapped.RunSeconds) > 1f)
            {
                walked.Violations.Add("clock: " + where + " ran " + uncapped.RunSeconds.ToString("n0")
                    + " s uncapped and " + capped.RunSeconds.ToString("n0") + " s capped");
            }

            if (capped.SubstepsDemanded > Cap + Slack)
            {
                walked.Violations.Add("over-cap: " + where + " demands "
                    + capped.SubstepsDemanded.ToString("n2") + " substeps under a cap of " + Cap);
            }

            float expected = Math.Min(uncapped.SubstepsDemanded, Cap);
            float tolerance = Math.Max(Slack, expected * 0.01f);

            if (Math.Abs(capped.SubstepsDemanded - expected) > tolerance)
            {
                walked.Violations.Add("identity: " + where + " demands "
                    + capped.SubstepsDemanded.ToString("n3") + " capped against min("
                    + uncapped.SubstepsDemanded.ToString("n3") + ", " + Cap + ")");
            }

            if (capped.FlooredNodes > 0) walked.Floored = true;

            if (uncapped.FlooredNodes != 0)
            {
                walked.Violations.Add("control-capped: " + where + " floored "
                    + uncapped.FlooredNodes + " nodes with the cap off");
            }

            if (float.IsNaN(capped.PeakKelvin) || float.IsInfinity(capped.PeakKelvin))
            {
                walked.Violations.Add("not-a-number: " + where + " peaked at " + capped.PeakKelvin
                    + " under the cap");
            }
        }
    }
}

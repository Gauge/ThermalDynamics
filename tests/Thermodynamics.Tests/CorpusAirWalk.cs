using System;
using System.Collections.Generic;
using System.Globalization;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// One pass over every ship in the corpus in **air**, which is the environment both halves of
    /// `G6` are decided in and the only one the survey has never run.
    ///
    /// <para>
    /// **Why it exists.** `CorpusSurvey`'s five scenarios are all vacuum. `G6`'s *demand* half read
    /// as passing for months on that dataset — corpus p99 6.02 against 64 granted — and failed the
    /// moment air was measured on 49 hulls, which is what `C19` and then `C24` came out of. Its
    /// *cost* half is in exactly the same position now: scored at p99 883,675 element visits in
    /// vacuum, and projected through the panel's own air-to-vacuum ratio it lands near four million
    /// against the four million the allowance grants (`C27`). A projection of a projection is
    /// standing where a measurement belongs, and this is the measurement.
    /// backlog.md `F11`.
    /// </para>
    ///
    /// <para>
    /// **Why a separate walk rather than three more scenarios on the survey.** Every figure quoted
    /// from the survey was taken on a run of five scenarios; adding to it makes each of those rows
    /// a different run and nothing quoted from the old one is comparable with the new (`M1`, `P6`).
    /// A walk of its own has its own resume record and its own data directory, is read by the same
    /// `verdict.py`, and can be run and re-run without touching what the survey means.
    /// </para>
    ///
    /// <para>
    /// **What it judges, and what it only records.** The population criteria are scored downstream
    /// by `verdict.py` against the markers written in balance-lab.md,
    /// which is where they belong and where they were written before any of this data existed
    /// (`E1`, `E11`). What this walk asserts is the two things only a run in air can check — that
    /// the convection path actually carried watts, and that no ship is *cheaper* in air than in
    /// vacuum — plus the accounting every walk owes its own dataset.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class CorpusAirWalk
    {

        private static List<Battery.Scenario> Scenarios()
        {
            List<Battery.Scenario> chosen = ScenarioIndex.Resolve(PairLab.AirScenarios, "the air walk");
            for (int i = 0; i < chosen.Count; i++) chosen[i] = Ceiling(chosen[i]);
            return chosen;
        }


        private static Battery.Scenario Ceiling(Battery.Scenario scenario)
        {
            string set = Environment.GetEnvironmentVariable("THERMAL_SCENARIO_CEILING");
            float seconds;
            if (string.IsNullOrEmpty(set)
                || !float.TryParse(set, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out seconds)
                || seconds <= 0f
                || seconds >= scenario.Seconds)
            {
                return scenario;
            }

            return new Battery.Scenario
            {
                Name = scenario.Name,
                Question = scenario.Question,
                Environment = scenario.Environment,
                Load = scenario.Load,
                Then = scenario.Then,
                ThenAfterSeconds = scenario.ThenAfterSeconds,
                Seconds = seconds,
            };
        }

        private const string Label = "air";

        private const string Anchor = "vacuum-shadow";

        private const float Slack = 0.01f;

        private class Walked
        {
            public string Ship;
            public bool Convected;

            public readonly List<ScenarioOutcome> Outcomes = new List<ScenarioOutcome>();

            public readonly List<string> Violations = new List<string>();
        }

        [Fact]

        public void EveryShipInTheCorpusIsMeasuredInAir()
        {
            if (CorpusFixture.Files().Count == 0) return;


            List<Battery.Scenario> scenarios = Scenarios();

            List<Walked> results = CorpusFixture.Sweep(Label,
                delegate (Blueprints.Ship ship) { return Walk(ship, scenarios); });

            Assert.True(results.Count > 0, "the corpus yielded no ships to walk in air");


            List<string> violations = new List<string>();
            int convected = 0;

            foreach (Walked ship in results)
            {
                if (ship.Convected) convected++;
                violations.AddRange(ship.Violations);
            }

            Assert.True(convected > 0,
                "no ship of " + results.Count + " recorded any convection, so this walk measured "
                + "vacuum under three atmospheric names");

            Assert.True(violations.Count == 0,
                violations.Count + " violations across " + results.Count + " ships:\n  "
                + string.Join("\n  ", violations.ToArray()));
        }


        private static Walked Walk(Blueprints.Ship ship, List<Battery.Scenario> scenarios)
        {
            Walked walked = new Walked { Ship = ship.Name };

            Dictionary<string, ScenarioOutcome> byName =
                new Dictionary<string, ScenarioOutcome>(StringComparer.Ordinal);

            foreach (Battery.Scenario scenario in scenarios)
            {
                ScenarioOutcome outcome = Battery.Run(ship, scenario);
                byName[scenario.Name] = outcome;
                walked.Outcomes.Add(outcome);
            }

            CorpusRecord.Outcomes(Label, walked.Outcomes);


            ScenarioOutcome anchor = byName[Anchor];

            if (anchor.Blocks != ship.Blocks)
            {
                walked.Violations.Add("accounting: " + ship.Name + " read " + ship.Blocks
                    + " blocks and built " + anchor.Blocks + " nodes");
                return walked;
            }

            foreach (ScenarioOutcome outcome in walked.Outcomes)
            {
                if (outcome.Scenario == Anchor) continue;

                if (outcome.ConvectionWatts != 0f) walked.Convected = true;

                if (outcome.SubstepsDemanded + Slack < anchor.SubstepsDemanded)
                {
                    walked.Violations.Add("air-softer: " + ship.Name + " / " + outcome.Scenario
                        + " demands " + outcome.SubstepsDemanded.ToString("n2")
                        + " substeps against " + anchor.SubstepsDemanded.ToString("n2")
                        + " in vacuum");
                }

                if (float.IsNaN(outcome.PeakKelvin) || float.IsInfinity(outcome.PeakKelvin))
                {
                    walked.Violations.Add("not-a-number: " + ship.Name + " / " + outcome.Scenario
                        + " peaked at " + outcome.PeakKelvin);
                }
            }

            return walked;
        }
    }
}

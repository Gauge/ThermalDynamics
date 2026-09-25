using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every ship in the corpus in air **twice**: with the hull shape term off as it ships, and with
    /// `EnableShapeDrag` on.
    ///
    /// <para>
    /// **Why it exists.** backlog.md `K22` built the shape term and measured what it is worth on two
    /// toy hulls — a settled four-cell brick runs 9.96 K cooler at reentry air and a stair-stepped
    /// wedge 11.36 K. Both are larger than the 7.4 K windward shielding is worth, which is already
    /// enough to keep *that* switch off, so the term cannot become a default on a hull's evidence.
    /// This is the population.
    /// </para>
    ///
    /// <para>
    /// **Both arms run to the same simulated clock**, for the reason the cap walk states: a run
    /// stops when the hottest block settles, so two configurations of one ship stop at different
    /// instants and a comparison of two settle-stopped runs measures the stopping rule (`M1`,
    /// `P6`). The unshaped arm runs first and hands its elapsed clock to the shaped one.
    /// </para>
    ///
    /// <para>
    /// **The arm is carried in the scenario name rather than in a new column**, because the outcome
    /// schema is read by every corpus tool and a walk that only this one reads should not widen it.
    /// A shaped row is `reentry-shaped`; its control is `reentry` in the same file for the same ship.
    /// </para>
    ///
    /// <para>
    /// **`vacuum-shadow` is the control the change cannot reach.** There is no air in it, so the
    /// friction term is dead and the shape factor multiplies nothing; if it moves, the term is
    /// reaching something it has no business touching and the reentry delta is not what it says.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class CorpusShapeWalk
    {
        private const string Label = "shape";

        private const string Measured = "reentry";
        private const string Control = "vacuum-shadow";

        private const string ShapedSuffix = "-shaped";

        private const float Slack = 0.01f;


        private static ThermalSettings Arm(bool shape)
        {

            ThermalSettings settings = new ThermalSettings();
            settings.EnableShapeDrag = shape;
            return settings;
        }


        private static List<Battery.Scenario> Scenarios()
        {
            Dictionary<string, Battery.Scenario> byName =
                new Dictionary<string, Battery.Scenario>(StringComparer.Ordinal);
            foreach (Battery.Scenario scenario in Battery.All()) byName[scenario.Name] = scenario;


            List<Battery.Scenario> chosen = new List<Battery.Scenario>();
            foreach (string name in new[] { Control, Measured })
            {
                Assert.True(byName.ContainsKey(name),
                    "the shape walk asks for scenario '" + name + "' and the battery has no such case");
                chosen.Add(byName[name]);
            }

            return chosen;
        }

        private class Walked
        {
            public string Ship;
            public bool Moved;

            public readonly List<ScenarioOutcome> Outcomes = new List<ScenarioOutcome>();

            public readonly List<string> Violations = new List<string>();
        }

        [Fact]

        public void EveryShipInTheCorpusIsMeasuredWithTheShapeTermAndWithoutIt()
        {
            if (CorpusFixture.Files().Count == 0) return;


            List<Battery.Scenario> scenarios = Scenarios();

            List<Walked> results = CorpusFixture.Sweep(Label,
                delegate (Blueprints.Ship ship) { return Walk(ship, scenarios); });

            Assert.True(results.Count > 0, "the corpus yielded no ships to walk under the shape term");


            List<string> violations = new List<string>();
            int moved = 0;

            foreach (Walked ship in results)
            {
                if (ship.Moved) moved++;
                violations.AddRange(ship.Violations);
            }

            Assert.True(moved > 0,
                "the shape term moved no temperature on any of " + results.Count + " ships, so the "
                + "shaped arm is the control run twice and this dataset says nothing");

            Assert.True(violations.Count == 0,
                violations.Count + " violations across " + results.Count + " ships:\n  "
                + string.Join("\n  ", violations.ToArray()));
        }


        private static Walked Walk(Blueprints.Ship ship, List<Battery.Scenario> scenarios)
        {
            Walked walked = new Walked { Ship = ship.Name };

            foreach (Battery.Scenario scenario in scenarios)
            {
                ScenarioOutcome plain = Battery.Run(ship, scenario, Arm(false));

                ScenarioOutcome shaped =
                    Battery.RunForSeconds(ship, scenario, plain.RunSeconds, Arm(true));
                shaped.Scenario = scenario.Name + ShapedSuffix;

                walked.Outcomes.Add(plain);
                walked.Outcomes.Add(shaped);

                Judge(walked, scenario.Name, plain, shaped);
            }

            CorpusRecord.Outcomes(Label, walked.Outcomes);
            return walked;
        }


        private static void Judge(Walked walked, string scenario,
            ScenarioOutcome plain, ScenarioOutcome shaped)
        {
            float delta = shaped.PeakKelvin - plain.PeakKelvin;

            if (scenario == Control)
            {
                if (Math.Abs(delta) > Slack)
                {
                    walked.Violations.Add("control moved: " + walked.Ship + " / " + scenario
                        + " read " + plain.PeakKelvin + " K plain and " + shaped.PeakKelvin
                        + " K shaped, and there is no air in it to carry the difference");
                }

                return;
            }

            if (delta > Slack)
            {
                walked.Violations.Add("shaped hotter: " + walked.Ship + " / " + scenario
                    + " read " + plain.PeakKelvin + " K plain and " + shaped.PeakKelvin
                    + " K shaped, and the shape factor cannot exceed one");
            }

            if (Math.Abs(delta) > Slack) walked.Moved = true;
        }
    }
}

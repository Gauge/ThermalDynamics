using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Sunlight never cools a real hull, proven the transient-proof way: **cold start, equal
    /// clocks, all six directions.**
    ///
    /// <para>
    /// The survey's version of this claim compares two adaptively-stopped runs that begin at
    /// 293 K — above both equilibria — so both hulls are cooling and the comparison depends on
    /// when each run happened to stop. Twenty per cent of real ships "violated" it that way, every
    /// one an artifact of unequal stop times. Started at the vacuum floor instead, both runs warm:
    /// the shadowed hull from its own waste heat, the sunlit one from waste heat plus the sun, so
    /// the sunlit hull is at least as warm at *every* instant — provided the clocks are equal,
    /// which is why this runs a fixed duration with no early exit.
    /// </para>
    ///
    /// <para>
    /// Six directions rather than the survey's one, because a panel of two hundred ships can
    /// afford what ten thousand cannot, and the spread across directions is itself balance data:
    /// how much a real hull's heat budget depends on which way it faces the sun.
    /// </para>
    /// </summary>
    public class SunlightPanelWalk
    {
        private const int Panel = 200;

        private const float ColdStart = 2.7f;

        private const float Clock = 1800f;

        private static readonly Vector3[] Directions =
        {
            Vector3.Forward, Vector3.Backward,
            Vector3.Left, Vector3.Right,
            Vector3.Up, Vector3.Down,
        };

        [Fact]

        public void SunlightNeverCoolsFromAnyDirection()
        {
            List<Blueprints.Ship> ships = CorpusFixture.Spread(Panel);
            if (ships.Count == 0) return;

            List<List<string>> reports = LabRun.Map(ships, Compare, LabMode.Parallel);


            List<string> violations = new List<string>();
            foreach (List<string> report in reports) violations.AddRange(report);

            Assert.True(ships.Count > 0);
            Assert.Empty(violations);
        }


        private static List<string> Compare(Blueprints.Ship ship)
        {

            List<string> violations = new List<string>();


            ScenarioOutcome shadow = Run(ship, null);
            List<ScenarioOutcome> record = new List<ScenarioOutcome> { shadow };

            foreach (Vector3 direction in Directions)
            {

                ScenarioOutcome sunlit = Run(ship, direction);
                record.Add(sunlit);

                if (sunlit.MeanKelvin < shadow.MeanKelvin - 0.5f)
                {
                    violations.Add("sunlight-panel: " + ship.Name + " is "
                        + (shadow.MeanKelvin - sunlit.MeanKelvin).ToString("n1")

                        + " K cooler lit from " + Name(direction) + " than in shadow, on equal "
                        + "clocks from a cold start — the solar path cooled a hull");
                }
            }

            CorpusRecord.Outcomes("sunlight-panel", record);
            return violations;
        }


        private static ScenarioOutcome Run(Blueprints.Ship ship, Vector3? sunDirection)
        {
            ShipAssembly assembly = ship.Build(null, ColdStart);

            AssemblyRunner runner = new AssemblyRunner(assembly);

            runner.Environment = sunDirection == null
                ? (System.Func<float, EnvironmentSample>)(t => Worlds.Shadow())
                : t => Worlds.Space(sunDirection.Value);

            runner.Run(Clock);

            ScenarioOutcome outcome = ScenarioOutcome.Read(assembly, ship.Name,

                sunDirection == null ? "cold-shadow" : "cold-sunlit-" + Name(sunDirection.Value));
            outcome.WorkshopId = ship.WorkshopId;
            return outcome;
        }


        private static string Name(Vector3 direction)
        {
            if (direction == Vector3.Forward) return "forward";
            if (direction == Vector3.Backward) return "backward";
            if (direction == Vector3.Left) return "left";
            if (direction == Vector3.Right) return "right";
            if (direction == Vector3.Up) return "up";
            return "down";
        }
    }
}

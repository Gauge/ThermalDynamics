using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// One pass over every ship in the corpus: built once, run through each distinct scenario once,
    /// with every invariant evaluated against the shared outcomes and every violation reported by
    /// name (`M8`). Five stepped runs a ship where separate walks cost twelve, most of the difference
    /// being scenarios that are the same simulation under two names.
    ///
    /// <para>
    /// **<c>all-peak</c> is deliberately absent**: it puts 96 % of ships over critical, and a melting
    /// grid never settles, so it starves the balance invariant it would be run for. Every invariant
    /// here asserts a non-zero judged count (`E8`).
    /// See balance-lab.md, and balance.md, The population.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class CorpusSurvey
    {

        private static List<Battery.Scenario> Scenarios()
        {
            Dictionary<string, Battery.Scenario> byName = new Dictionary<string, Battery.Scenario>();
            foreach (Battery.Scenario scenario in Battery.All()) byName[scenario.Name] = scenario;

            return new List<Battery.Scenario>
            {
                byName["idle"],
                byName["vacuum-sunlit"],
                byName["full-electrical"],
                byName["burn-forward"],
                byName["recovery"],
            };
        }

        private class Surveyed
        {
            public string Ship;
            public long SealedBlocks;
            public long Blocks;
            public bool Joints;
            public bool Rooms;
            public bool SmallGrid;
            public bool Stepped;

            public readonly List<ScenarioOutcome> Outcomes = new List<ScenarioOutcome>();

            public readonly List<string> Violations = new List<string>();
        }

        [Fact]

        public void EveryShipInTheCorpusHoldsEveryInvariant()
        {
            if (CorpusFixture.Files().Count == 0) return;


            List<Battery.Scenario> scenarios = Scenarios();

            List<Surveyed> results = CorpusFixture.Sweep("survey",
                delegate (Blueprints.Ship ship) { return Survey(ship, scenarios); });

            Assert.True(results.Count > 0, "the corpus yielded no surveyable ships");



            List<string> violations = new List<string>();
            long blocks = 0;
            long sealedBlocks = 0;
            int sealedShips = 0;
            int withJoints = 0;
            int withRooms = 0;
            int smallGrid = 0;
            int stepped = 0;
            int warmedBySun = 0;
            int judgedBalance = 0;

            foreach (Surveyed ship in results)
            {
                blocks += ship.Blocks;
                sealedBlocks += ship.SealedBlocks;
                if (ship.SealedBlocks > 0) sealedShips++;
                if (ship.Joints) withJoints++;
                if (ship.Rooms) withRooms++;
                if (ship.SmallGrid) smallGrid++;
                if (ship.Stepped) stepped++;

                foreach (string violation in ship.Violations)
                {
                    if (violation == WarmedMarker) warmedBySun++;
                    else if (violation == JudgedMarker) judgedBalance++;
                    else violations.Add(violation);
                }
            }

            if (blocks > 0)
            {
                double share = sealedBlocks / (double)blocks;
                if (share >= 0.001d)
                {
                    violations.Add("sealed-blocks: " + sealedBlocks + " of " + blocks
                        + " blocks across " + sealedShips
                        + " ships are thermally sealed — the parser may be dropping mounts again");
                }
            }

            if (withJoints == 0) violations.Add("population: no ship resolved a mechanical joint");
            if (withRooms == 0) violations.Add("population: no ship mapped a sealed room");
            if (smallGrid == 0) violations.Add("population: no small-grid ship was read");
            if (stepped == 0) violations.Add("population: no ship changed temperature when stepped");

            if (warmedBySun == 0) violations.Add("population: the sun delivered watts to not one ship");

            if (judgedBalance == 0)
            {
                violations.Add("population: no outcome reached equilibrium, so made-versus-vented "
                    + "was never checked");
            }

            Assert.True(violations.Count == 0,
                violations.Count + " invariant violations across " + results.Count + " ships:\n  "
                + string.Join("\n  ", violations.GetRange(0, Math.Min(violations.Count, 40))));
        }

        private const string WarmedMarker = "warmed";
        private const string JudgedMarker = "judged";

        private const float Agreement = 0.05f;

        private const float BulkFlat = 0.01f;


        private static Surveyed Survey(Blueprints.Ship ship, List<Battery.Scenario> scenarios)
        {
            Surveyed surveyed = new Surveyed { Ship = ship.Name, SmallGrid = !ship.Large };


            ShipAssembly assembly = ship.Build();
            surveyed.Blocks = assembly.NodeCount;
            surveyed.Joints = assembly.Bridges.Count > 0;
            surveyed.Rooms = assembly.RoomCount > 0;

            if (assembly.NodeCount != ship.Blocks)
            {
                surveyed.Violations.Add("accounting: " + ship.Name + " read " + ship.Blocks
                    + " blocks and built " + assembly.NodeCount + " nodes");
                return surveyed;
            }

            for (int g = 0; g < assembly.Simulations.Count; g++)
            {
                ThermalSolver solver = assembly.Simulations[g].Solver;
                if (solver.Nodes.Count < 2) continue;

                System.Collections.Generic.HashSet<int> touchingAir =
                    HotSpotLab.NodesTouchingAir(solver);

                for (int i = 0; i < solver.Nodes.Count; i++)
                {
                    if (HotSpotLab.IsSealed(solver, i, touchingAir)) surveyed.SealedBlocks++;
                }
            }


            AssemblyRunner probe = new AssemblyRunner(assembly);
            probe.Environment = t => Worlds.Shadow();
            float before = assembly.Hottest() == null ? 0f : assembly.Hottest().Temperature;
            probe.Run(60f);
            surveyed.Stepped = probe.Hottest[probe.Hottest.Count - 1] != before;


            Dictionary<string, ScenarioOutcome> byName = new Dictionary<string, ScenarioOutcome>();
            foreach (Battery.Scenario scenario in scenarios)
            {
                ScenarioOutcome outcome = Battery.Run(ship, scenario);
                byName[scenario.Name] = outcome;
                surveyed.Outcomes.Add(outcome);
            }

            CorpusRecord.Outcomes("survey", surveyed.Outcomes);

            if (CorpusRecord.On)
            {

                List<string> row = new List<string>();
                row.Add(CorpusRecord.ShipRow(ship, assembly.NodeCount, assembly.Bridges.Count,
                    assembly.RoomCount, surveyed.SealedBlocks, surveyed.Stepped, true));
                CorpusRecord.Write("ships", CorpusRecord.ShipHeader, row);
            }


            ScenarioOutcome idle = byName["idle"];
            ScenarioOutcome sunlit = byName["vacuum-sunlit"];
            ScenarioOutcome loaded = byName["full-electrical"];
            ScenarioOutcome burn = byName["burn-forward"];
            ScenarioOutcome recovery = byName["recovery"];

            if (idle.BlocksOverCritical > 0)
            {
                surveyed.Violations.Add("idle-safe: " + ship.Name + " has "
                    + idle.BlocksOverCritical + " blocks over critical while parked");
            }

            if (sunlit.SolarWatts > 0f) surveyed.Violations.Add(WarmedMarker);

            if (loaded.PeakKelvin < idle.PeakKelvin - 1f)
            {
                surveyed.Violations.Add("load-ladder: " + ship.Name + " peaks at "
                    + loaded.PeakKelvin.ToString("n0") + " K loaded, below "
                    + idle.PeakKelvin.ToString("n0") + " K idle");
            }

            if (surveyed.SealedBlocks == 0)
            {
                foreach (ScenarioOutcome outcome in new[] { loaded, burn })
                {
                    if (outcome.SecondsToSettle < 0f) continue;
                    if (outcome.MadeWatts <= 1f) continue;
                    if (outcome.BlocksOverCritical > 0) continue;
                    if (float.IsNaN(outcome.BulkDriftWatts)) continue;
                    if (Math.Abs(outcome.BulkDriftWatts) > BulkFlat * outcome.MadeWatts) continue;

                    surveyed.Violations.Add(JudgedMarker);

                    float difference = Math.Abs(outcome.MadeWatts - outcome.VentedWatts);
                    if (difference / outcome.MadeWatts > Agreement)
                    {
                        surveyed.Violations.Add("balance: " + ship.Name + " / " + outcome.Scenario
                            + " made " + outcome.MadeWatts.ToString("n0") + " W, vented "
                            + outcome.VentedWatts.ToString("n0") + " W");
                    }
                }
            }

            if (recovery.BlocksOverCritical > 0
                && !float.IsNaN(recovery.BulkDriftWatts)
                && recovery.BulkDriftWatts >= 0f)
            {
                surveyed.Violations.Add("recovery: " + ship.Name + " has "
                    + recovery.BlocksOverCritical + " blocks over critical and is still gaining "
                    + recovery.BulkDriftWatts.ToString("n0") + " W after the load stopped");
            }

            return surveyed;
        }
    }
}

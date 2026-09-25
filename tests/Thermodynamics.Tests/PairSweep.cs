using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Conduction and the clock, moved together**, which is the one thing every sweep so far has
    /// not done and the whole of `G8`'s open question.
    ///
    /// <para>
    /// The projected route to the significance window — conductivity ×4 with `HeatTimeScale` ≈ 15,
    /// for a ~200 s crossing at ~0.7 substeps — was arrived at by multiplying two single-dial
    /// curves together. Substep demand goes as conductivity × clock, so the projection is at least
    /// dimensionally sound; whether a *crossing time* composes the same way is an extrapolation
    /// across an interaction nothing has run. This runs the grid.
    /// </para>
    ///
    /// <code>
    ///     THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=out/pairs \
    ///         dotnet test --filter "FullyQualifiedName~PairSweep"
    /// </code>
    ///
    /// <para>
    /// **On the retest set, not the standing panel, and that is a choice with a cost.** `G8` is a
    /// criterion about the event *a player meets*, and the retest set is the forty hulls chosen to
    /// be the ones people fly; the panel is chosen for spread, so its median is the median of a
    /// deliberately extreme sample. The cost is comparability: every published knob curve was taken
    /// on the panel, so **the edges of this grid are the same dials on a different population and
    /// are not the published rows re-derived**. What is compared here is the interior against the
    /// edges *within this dataset*, which is what the interaction question needs and which is
    /// `M1`-clean because every cell shares a population, a clock rule and a stop criterion.
    /// </para>
    ///
    /// <para>
    /// The panel would have been the other choice and was priced: 656,516 blocks against the retest
    /// set's 177,822, which is eleven hours against three for the same sixteen cells. Point
    /// <c>THERMAL_PANEL</c> at <c>tools/corpus/panel.csv</c> to run it there instead — the walk
    /// reads whatever file it is given and names the count it resolved.
    /// </para>
    ///
    /// <para>
    /// **The cells run in sequence**: a material override is a process-wide static that empties the
    /// shared model cache, so two cells in flight would build ships out of each other's world. The
    /// parallelism is inside a cell, across the panel.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class PairSweep
    {
        public const string Header =
            "cell,conductivity,clock,waste,shipped,ceiling_s,scenario,ship,workshop_id,large,blocks,"
            + "peak_k,mean_k,p95_k,hotspot_k,over_critical,over_share,"
            + "seconds_to_settle,seconds_to_critical,seconds_to_first_loss,"
            + "made_w,vented_w,substeps_demanded,hottest_block";

        [Fact]

        public void EveryPairOfConductionAndClockGetsAMeasuredCell()
        {
            List<Blueprints.Ship> ships;
            Dictionary<string, Battery.Scenario> scenarios;
            if (!Prologue(out ships, out scenarios)) return;

            Sweep(Pairs, PairLab.All(), PairLab.Scenarios, ships, scenarios);
        }

        [Fact]

        public void EveryCandidateCellIsPricedInAir()
        {
            List<Blueprints.Ship> ships;
            Dictionary<string, Battery.Scenario> scenarios;
            if (!Prologue(out ships, out scenarios)) return;

            Sweep(Air, PairLab.Decision(), PairLab.AirScenarios, ships, scenarios);
        }

        [Fact]

        public void EveryLoadAndClockPairGetsAMeasuredCell()
        {
            List<Blueprints.Ship> ships;
            Dictionary<string, Battery.Scenario> scenarios;
            if (!Prologue(out ships, out scenarios)) return;

            Sweep(LoadDial, PairLab.Load(), PairLab.LoadScenarios, ships, scenarios);
        }


        private static bool Prologue(
            out List<Blueprints.Ship> ships, out Dictionary<string, Battery.Scenario> scenarios)
        {
            ships = null;
            scenarios = null;
            if (CorpusFixture.Files().Count == 0) return false;

            List<ShipSet.Entry> set = ShipSet.Read("THERMAL_PANEL", "tools/corpus/typical.csv");
            Assert.True(set.Count > 0,
                "the corpus is opted in but no ship set was read. Build one with "
                + "tools/corpus/typical.py, or point THERMAL_PANEL at another.");

            ships = ShipSet.Load(set, Progress);
            Assert.True(ships.Count > 0, "the set named ships but none of them could be read");

            scenarios = new Dictionary<string, Battery.Scenario>(StringComparer.Ordinal);
            foreach (Battery.Scenario scenario in Battery.All()) scenarios[scenario.Name] = scenario;
            return true;
        }


        private static void Sweep(Pass pass, List<PairLab.Cell> cells, string[] scenarioNames,
            List<Blueprints.Ship> ships, Dictionary<string, Battery.Scenario> scenarios)
        {
            foreach (string name in scenarioNames)
            {
                Assert.True(scenarios.ContainsKey(name),
                    pass.Dataset + " asks for scenario '" + name + "' and the battery has no such case");
            }

            int controls = 0;
            foreach (PairLab.Cell cell in cells)
            {
                if (cell.IsShipped) controls++;
            }

            Assert.Equal(1, controls);
            Assert.True(cells[0].IsShipped, "the control must run first");

            foreach (PairLab.Cell cell in cells) Run(pass, cell, scenarioNames, ships, scenarios);

            Assert.True(pass.RowsWritten > 0 || pass.Record.Done().Count >= cells.Count * ships.Count,
                "the grid produced no rows and had nothing recorded as already finished");
        }


        private static void Run(Pass pass, PairLab.Cell cell, string[] scenarioNames,
            List<Blueprints.Ship> ships, Dictionary<string, Battery.Scenario> scenarios)
        {

            object gate = new object();
            int written = 0;
            int skipped = 0;

            HashSet<string> done = pass.Record.Done();

            try
            {
                Blueprints.MaterialOverride = cell.Material();

                System.Threading.Tasks.ParallelOptions options =
                    new System.Threading.Tasks.ParallelOptions
                    { MaxDegreeOfParallelism = LabRun.Workers };

                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, ships.Count, 1),
                    options,
                    range =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            Blueprints.Ship source = ships[i];

                            string mark = cell.Name + "|" + source.Name + "|" + source.WorkshopId;
                            if (done.Contains(mark))
                            {
                                lock (gate) skipped++;
                                continue;
                            }


                            List<string> mine = new List<string>();

                            Blueprints.Ship ship = source.Reload();

                            for (int s = 0; s < scenarioNames.Length; s++)
                            {
                                Battery.Scenario scenario;
                                if (!scenarios.TryGetValue(scenarioNames[s], out scenario)) continue;

                                Battery.Scenario stretched = PairLab.Stretch(scenario, cell);

                                try
                                {
                                    ScenarioOutcome outcome =
                                        Battery.Run(ship, stretched, cell.Settings());
                                    mine.Add(Row(cell, stretched, ship, outcome));
                                }
                                catch
                                {
                                }
                            }

                            lock (gate)
                            {
                                if (CorpusRecord.On && mine.Count > 0)
                                {
                                    CorpusRecord.Write(pass.Dataset, Header, mine);
                                }

                                pass.Record.Mark(mark);
                                written += mine.Count;
                                pass.RowsWritten += mine.Count;
                            }
                        }
                    });
            }
            finally
            {
                Blueprints.MaterialOverride = null;
            }

            ShipSet.Progress(pass.Dataset,
                cell.Name + " (conductivity ×" + cell.Conductivity + ", clock " + cell.Clock
                + ") wrote " + written + " rows, resumed past " + skipped + " ships");
        }


        private static string Row(PairLab.Cell cell, Battery.Scenario scenario,
            Blueprints.Ship ship, ScenarioOutcome o)
        {

            StringBuilder row = new StringBuilder();
            row.Append(CorpusRecord.Text(cell.Name)).Append(',');
            row.Append(CorpusRecord.Num(cell.Conductivity)).Append(',');
            row.Append(CorpusRecord.Num(cell.Clock)).Append(',');
            row.Append(CorpusRecord.Num(cell.Waste)).Append(',');
            row.Append(cell.IsShipped ? 1 : 0).Append(',');
            row.Append(CorpusRecord.Num(scenario.Seconds)).Append(',');
            row.Append(CorpusRecord.Text(o.Scenario)).Append(',');
            row.Append(CorpusRecord.Text(ship.Name)).Append(',');
            row.Append(ship.WorkshopId.ToString(CultureInfo.InvariantCulture)).Append(',');
            row.Append(ship.Large ? 1 : 0).Append(',');
            row.Append(ship.Blocks).Append(',');
            row.Append(CorpusRecord.Num(o.PeakKelvin)).Append(',');
            row.Append(CorpusRecord.Num(o.MeanKelvin)).Append(',');
            row.Append(CorpusRecord.Num(o.P95Kelvin)).Append(',');
            row.Append(CorpusRecord.Num(o.HotSpotKelvin)).Append(',');
            row.Append(o.BlocksOverCritical).Append(',');
            row.Append(CorpusRecord.Num(o.OverCriticalShare)).Append(',');
            row.Append(CorpusRecord.Num(o.SecondsToSettle)).Append(',');
            row.Append(CorpusRecord.Num(o.SecondsToCritical)).Append(',');
            row.Append(CorpusRecord.Num(o.SecondsToFirstLoss)).Append(',');
            row.Append(CorpusRecord.Num(o.MadeWatts)).Append(',');
            row.Append(CorpusRecord.Num(o.VentedWatts)).Append(',');
            row.Append(CorpusRecord.Num(o.SubstepsDemanded)).Append(',');
            row.Append(CorpusRecord.Text(o.HottestBlock));
            return row.ToString();
        }


        private class Pass
        {
            public string Dataset;
            public ShipSet.Resume Record;
            public int RowsWritten;
        }

        private static readonly Pass Pairs =
            new Pass { Dataset = "pairs", Record = new ShipSet.Resume("pairs") };

        private static readonly Pass Air =
            new Pass { Dataset = "air", Record = new ShipSet.Resume("air") };

        private static readonly Pass LoadDial =
            new Pass { Dataset = "load", Record = new ShipSet.Resume("load") };


        private static void Progress(string line)
        {
            ShipSet.Progress("pairs", line);
        }
    }
}

using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The corpus scores a step's cost in the unit the allowance is spent in, and the link half
    /// of that unit was missing.**
    ///
    /// <para>
    /// `G6`'s cost half compares a step's element visits against `MaxElementVisitsPerStep`. The
    /// mod's own statement of that cost is <see cref="ThermalSimulation.SubstepCost"/>, which is
    /// `links + 4 x nodes`: a substep runs a conduction pass that is per *link* and an environment
    /// pass that is per *node*, and benchmarks.md measured the weight
    /// between them rather than assuming it.
    /// </para>
    ///
    /// <para>
    /// **The corpus had no link column, and `verdict.py` was handed `joints` in its place.** A
    /// joint is a rotor or a piston *between two grids* and there are none on most blueprints; a
    /// link is a face two blocks share and there are one to three per block. So every step-work
    /// figure this repository has published is the node half alone, and the true cost is larger by
    /// whatever the hull's link-to-node ratio is. These tests pin all three claims — that the two
    /// counts are different, that the recorded cost is the mod's own arithmetic rather than a copy
    /// of it, and that the per-grid reading is per grid — because the defect is silent in exactly
    /// the way `P5` describes: two definitions of *links*, and the wrong one is a valid number.
    /// </para>
    ///
    /// <para>
    /// This is the same class of error as the 2.125-against-4 correction of 2026-08-24, one level
    /// down: that one had the right count in the wrong currency, this one has the wrong count.
    /// </para>
    /// </summary>
    public class StepWorkUnitTests
    {
        private const int Blocks = 2000;


        private static WorstCases.Built Hull()
        {
            return WorstCases.Hull("ship", Blocks);
        }

        [Fact]

        public void TheRecordedSubstepCostIsTheOneTheAllowanceIsDividedBy()
        {

            WorstCases.Built built = Hull();
            ThermalSimulation simulation = built.Simulation;

            Assert.Equal(simulation.Solver.LinkCount
                + (ThermalSettings.NodeCostInLinks * simulation.Solver.Nodes.Count),
                simulation.SubstepCost);
        }

        [Fact]

        public void ScoringAStepWithTheJointCountUnderstatesItByTheLinkHalf()
        {

            WorstCases.Built built = Hull();


            ShipAssembly assembly = new ShipAssembly();
            assembly.Simulations.Add(built.Simulation);

            Assert.Empty(assembly.Bridges);
            Assert.True(assembly.LinkCount > 0, "a census hull of touching blocks has no links");

            long honest = assembly.WorstGridSubstepCost;
            long asScored = ThermalSettings.NodeCostInLinks * assembly.NodeCount;

            Assert.True(honest > asScored,
                "the link term is nought, so nothing was being left out and this test is not"
                + " measuring what it says it is");

            double links = assembly.LinkCount / (double)assembly.NodeCount;
            Assert.InRange(links, 0.5, 3.5);

            double understated = honest / (double)asScored;
            Assert.InRange(understated, 1.1, 1.9);
        }

        [Fact]

        public void TheCostIsTheWorstGridRatherThanTheSumOfThem()
        {

            WorstCases.Built one = Hull();

            WorstCases.Built two = Hull();


            ShipAssembly assembly = new ShipAssembly();
            assembly.Simulations.Add(one.Simulation);
            assembly.Simulations.Add(two.Simulation);

            Assert.Equal(one.Simulation.SubstepCost, assembly.WorstGridSubstepCost);
            Assert.Equal(2 * one.Simulation.Solver.LinkCount, assembly.LinkCount);
        }

        [Fact]

        public void AnOutcomeCarriesTheLinkCountAndTheCostItImplies()
        {

            WorstCases.Built built = Hull();


            ShipAssembly assembly = new ShipAssembly();
            assembly.Simulations.Add(built.Simulation);

            ScenarioOutcome outcome = ScenarioOutcome.Read(assembly, "hull", "bench");

            Assert.Equal(assembly.LinkCount, outcome.Links);
            Assert.Equal(assembly.WorstGridSubstepCost, outcome.SubstepCost);


            List<string> header = new List<string>(CorpusRecord.OutcomeHeader.Split(','));
            Assert.Contains("links", header);
            Assert.Contains("substep_cost", header);

            string[] cells = CorpusRecord.Row("bench", outcome).Split(',');
            Assert.Equal(header.Count, cells.Length);
            Assert.Equal(outcome.Links.ToString(), cells[header.IndexOf("links")]);
            Assert.Equal(outcome.SubstepCost.ToString(), cells[header.IndexOf("substep_cost")]);
        }
    }
}

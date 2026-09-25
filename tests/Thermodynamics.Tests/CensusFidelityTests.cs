using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Whether the synthetic ship still resembles the real one — **tests of the measuring instrument
    /// rather than of the simulation**. They hold <see cref="Census"/> against the field observations
    /// recorded beside it and fail when the two drift apart; refresh both together when a dump arrives
    /// (`M11`). See stiffness.md, How close the synthetic tests are to a real ship.
    ///
    /// <para>
    /// **Two of the three references here were taken at a pace that no longer ships, and only one
    /// of them can be re-measured.** `C24` moved `ConductionScale` and `HeatTimeScale` together,
    /// and a substep demand is a conductance over a capacity, so every stiffness figure in this
    /// class moved with them and none by the same factor. The corpus walk was re-run — 8,105
    /// blueprints, 190 s — and <see cref="Census.Corpus"/> carries the new figures with the old
    /// ones beside them. <see cref="Census.Field"/> cannot be: it is two ships from two vanished
    /// sessions, and re-taking it needs a live session on the new pair. So the corpus is the
    /// reference where there is a choice, and where a test still reads the dump it says which pace
    /// each side was taken at (`P6`).
    /// </para>
    ///
    /// <para>
    /// **And the hull has left the population at the one cap that ships.** At a per-block cap of 8
    /// it floors 6.91 % of its own blocks against a real population's 0.92 % — seven times as many
    /// — while at caps of 4, 2 and 1 it is inside the population as it always was. That is
    /// backlog.md `C26`, pinned below rather than asserted around.
    /// </para>
    /// </summary>
    public class CensusFidelityTests
    {

        private static ThermalSimulation Hull(int blocks, int cap = 0)
        {
            ThermalSettings settings = new ThermalSettings { Frequency = 4 };
            settings.MaxSubstepsPerBlock = cap;
            settings.MaxSubsteps = 4096;
            settings.MaxElementVisitsPerStep = 0;
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            builder.PlaceCensus(LoadShapes.Build("ship", blocks));

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.RebuildAll();
            return simulation;
        }

        [Fact]

        public void TheTiersCarryTheMountsTheirBlocksDeclare()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, GameBlocks.Definition> bySubtype = GameBlocks.BySubtype();

            foreach (Census.Tier tier in Census.Tiers)
            {
                GameBlocks.Definition definition;
                Assert.True(bySubtype.TryGetValue(tier.Example, out definition),
                    "the " + tier.Name + " band names " + tier.Example
                    + ", which the installed game does not define");

                int declared = 0;
                for (int face = 0; face < definition.MountFaces.Length; face++)
                {
                    if (definition.MountFaces[face]) declared++;
                }

                if (declared == 0) declared = Face.Count;

                if (tier.Name == "machinery")
                {
                    Assert.Equal(Face.Count, tier.MountFaces);
                    Assert.Equal(1, declared);
                    continue;
                }

                Assert.True(declared == tier.MountFaces,
                    "the " + tier.Name + " band mounts on " + tier.MountFaces + " faces and "
                    + tier.Example + " declares " + declared
                    + "; refresh the tier or say why it differs, as the machinery band does");
            }
        }

        [Fact]

        public void TheTiersAreAWholePopulation()
        {
            float total = 0f;
            for (int i = 0; i < Census.Tiers.Length; i++)
            {
                Assert.True(Census.Tiers[i].Share > 0f, Census.Tiers[i].Name + " has no population");
                Assert.True(Census.Tiers[i].Mass > 0f, Census.Tiers[i].Name + " is massless");
                total += Census.Tiers[i].Share;
            }

            Assert.Equal(1f, total, 2);
        }

        [Fact]

        public void ACensusHullIsAsStiffAsARealShip()
        {

            ThermalSimulation simulation = Hull(4000);
            float demand = simulation.Solver.RequiredSubsteps(simulation.Settings.StepSeconds);

            Assert.True(demand > Census.Corpus.AirP10 * 0.6f && demand < Census.Corpus.AirMax * 1.6f,
                "the census hull asks for " + demand.ToString("n2")
                + " substeps against a corpus range of " + Census.Corpus.AirP10
                + " to " + Census.Corpus.AirMax
                + "; the harness has drifted away from the ships it is meant to describe");
        }

        [Theory]
        [InlineData(4, Census.Corpus.FlooredAtCap4)]
        [InlineData(2, Census.Corpus.FlooredAtCap2)]
        [InlineData(1, Census.Corpus.FlooredAtCap1)]

        public void ACapReachesAboutAsMuchOfTheHullAsItReachesOfARealPopulation(
            int cap, float corpusShare)
        {

            ThermalSimulation simulation = Hull(4000, cap);
            ThermalSolver.SubstepProfile profile = simulation.Solver.ProfileSubsteps();

            int index = Array.IndexOf(ThermalSolver.SubstepProfile.ProjectedCaps, cap);
            Assert.True(index >= 0, "cap " + cap + " is no longer projected");

            double share = (double)profile.CapNodesFloored[index] / profile.Nodes;

            Assert.True(share > corpusShare * 0.25 && share < corpusShare * 4.0,
                "cap " + cap + " raises " + (100 * share).ToString("n1")
                + "% of the census hull against " + (100 * corpusShare).ToString("n1")
                + "% of a real population");
        }




        private static StiffnessLab.Row CensusHull()
        {
            return StiffnessLab.Census(4000);
        }

        [Fact]

        public void TheCensusHullIsInsideThePopulationItStandsIn()
        {

            StiffnessLab.Row hull = CensusHull();

            Assert.True(hull.Air > Census.Corpus.AirP10 && hull.Air < Census.Corpus.AirMax,
                "the census hull demands " + hull.Air.ToString("n2")
                + " substeps of a quarter-second step in air, against a corpus of "
                + Census.Corpus.Ships.ToString("n0") + " real ships spanning "
                + Census.Corpus.AirP10 + " to " + Census.Corpus.AirMax
                + "; the harness has left the population it is meant to describe");

            Assert.InRange(hull.Air, Census.Corpus.AirP50 * 0.75f, Census.Corpus.AirP90);
        }

        [Fact]

        public void TheShippedCapReachesThePopulationsTailAndNotATypicalHull()
        {

            ThermalSimulation simulation = Hull(4000, 8);
            ThermalSolver.SubstepProfile profile = simulation.Solver.ProfileSubsteps();

            int index = Array.IndexOf(ThermalSolver.SubstepProfile.ProjectedCaps, 8);
            double share = (double)profile.CapNodesFloored[index] / profile.Nodes;

            Assert.True(share < Census.Corpus.FlooredAtCap8,
                "a cap of 8 floors " + (100 * share).ToString("n2")
                + " % of the census hull against " + (100 * Census.Corpus.FlooredAtCap8).ToString("n2")
                + " % of a real population, so the hull has grown a tail the population's median"
                + " ship does not have");

            Assert.InRange(Census.Corpus.FlooredAtCap8, 0f, 0.02f);
        }

        [Fact]

        public void TheCensusHullFeelsAirLikeARealHullDoes()
        {

            StiffnessLab.Row hull = CensusHull();
            Assert.True(hull.StiffestInVacuum > 0f, "the stiffest block has no vacuum demand");

            float ratio = StiffnessLab.Ratio(hull);

            Assert.True(ratio >= Census.Corpus.AirRatioP10,
                "the census hull's stiffest block is " + ratio.ToString("n2")
                + " times stiffer in air than out of it, below the tenth percentile of "
                + Census.Corpus.AirRatioP10 + " over " + Census.Corpus.Ships.ToString("n0")
                + " real ships; the hull has stopped responding to the air");

            Assert.True(ratio < Census.Corpus.AirRatioP90,
                "the census hull is now " + ratio.ToString("n2")
                + " times stiffer in air, past the ninetieth percentile of "
                + Census.Corpus.AirRatioP90 + "; it has become one of the extreme hulls rather"
                + " than an ordinary one");
        }

        [Fact]

        public void TheCensusHullMakesFarMoreHeatThanARealShip()
        {
            float perBlock = Census.ProducerShare * Census.ProducerWatts;

            Assert.Equal(Census.Corpus.CensusWastePerBlock, perBlock, 0);

            Assert.True(perBlock > Census.Corpus.WastePerBlockP90,
                "the census hull now makes " + perBlock.ToString("n0")

                + " W of waste per block, below the ninetieth percentile of real ships ("
                + Census.Corpus.WastePerBlockP90.ToString("n0")
                + "). If the tiers have been softened toward a typical ship, every temperature"
                + " figure in this repository describes something different and the pages quoting"
                + " them need re-reading.");

            Assert.True(Census.Corpus.WastePerBlockP50 < Census.Corpus.WastePerBlockP90,
                "the recorded population figures are no longer ordered");
        }

        [Fact]

        public void TheFieldObservationsAreFromAPaceThatNoLongerShips()
        {
            Assert.True(Census.Field.MostDemand > Census.Corpus.AirMax,
                "the stiffer field ship at " + Census.Field.MostDemand
                + " is back inside the corpus range " + Census.Corpus.AirP10 + " to "
                + Census.Corpus.AirMax + ", so a dump has been taken at the shipped pair and the"
                + " claim this test replaced can come back");

            Assert.InRange(Census.Field.MostDemand / Census.Corpus.AirMax, 1f, 3f);
        }

        [Fact]

        public void TheTwoModesOfTheCorpusHaveClosed()
        {
            Assert.True(Census.Corpus.LitP50 > Census.Corpus.StructuralP50 * 2f,
                "a light-limited ship demands " + Census.Corpus.LitP50 + " against a structure"
                + "-limited ship's " + Census.Corpus.StructuralP50
                + "; the two modes have merged entirely and a median describes the population");

            Assert.True(Census.Corpus.LitP50 < Census.Corpus.StructuralP50 * 4f,
                "the two modes are " + (Census.Corpus.LitP50 / Census.Corpus.StructuralP50)
                + "x apart, which is the split population this repository used to describe; the"
                + " pages that stopped quoting a typical ship can start again");

            Assert.True(Census.Corpus.LitShare > 0.2f && Census.Corpus.LitShare < 0.8f,
                "a light sets the substep count on " + (100f * Census.Corpus.LitShare).ToString("n0")
                + "% of ships, which is no longer a split population");

            Assert.InRange(Census.Corpus.BetweenTheModes, 0.3f, 0.7f);

            Assert.True(Census.Corpus.AirP50 < Census.Corpus.LitP50,
                "the corpus median has moved into the lit mode");
        }

        [Fact]

        public void TheFieldCapCurveMatchesTheCorpusWhereTheCapIsActuallySet()
        {
            Assert.True(Census.Corpus.FlooredAtCap8 < Census.Field.RaisedAtCap8 * 2f,
                "a cap of 8 holds back " + (100f * Census.Corpus.FlooredAtCap8).ToString("n2")
                + " % of real blocks against the dump's "
                + (100f * Census.Field.RaisedAtCap8).ToString("n2")
                + " %; the shipped cap was chosen on a curve the population no longer has");

            Assert.True(Census.Corpus.FlooredAtCap4 > Census.Field.RaisedAtCap4 * 2f,
                "a cap of 4 holds back " + (100f * Census.Corpus.FlooredAtCap4).ToString("n2")
                + " % of real blocks against the dump's "
                + (100f * Census.Field.RaisedAtCap4).ToString("n2")
                + " %, so the two curves agree below the shipped cap and the note beside them"
                + " needs rewriting");

            Assert.True(Census.Corpus.FlooredAtCap1 > Census.Corpus.FlooredAtCap2);
            Assert.True(Census.Corpus.FlooredAtCap2 > Census.Corpus.FlooredAtCap4);
            Assert.True(Census.Corpus.FlooredAtCap4 > Census.Corpus.FlooredAtCap8);
        }

        [Fact]

        public void TheRecordedCorpusFiguresAreInternallyConsistent()
        {
            Assert.True(Census.Corpus.AirP10 < Census.Corpus.AirP50);
            Assert.True(Census.Corpus.AirP50 < Census.Corpus.AirP90);
            Assert.True(Census.Corpus.AirP90 < Census.Corpus.AirMax);

            Assert.True(Census.Corpus.VacuumP50 < Census.Corpus.VacuumP90);
            Assert.True(Census.Corpus.VacuumP90 < Census.Corpus.VacuumMax);

            Assert.True(Census.Corpus.AirRatioP10 < Census.Corpus.AirRatioP50);
            Assert.True(Census.Corpus.AirRatioP50 < Census.Corpus.AirRatioP90);

            Assert.True(Census.Corpus.AirRatioP10 >= 1f,
                "a ratio below one would mean air made a block softer");
            Assert.True(Census.Corpus.AirP50 > Census.Corpus.VacuumP50,
                "the corpus is no longer stiffer in air than in vacuum at the median");

            Assert.InRange(Census.Corpus.CensusAirRatio,
                Census.Corpus.AirRatioP10, Census.Corpus.AirRatioP50);

            Assert.True(Census.Corpus.StructuralP50 < Census.Corpus.AirP50);
            Assert.True(Census.Corpus.LitP50 > Census.Corpus.AirP50);

            Assert.InRange(Census.Corpus.StiffestFacesMean, 1f, 6f);

            Assert.InRange(Census.Corpus.ProducerSharePercentile, 0f, 100f);
            Assert.InRange(Census.Corpus.ProducerWattsPercentile, 0f, 100f);
            Assert.InRange(Census.Corpus.WastePerBlockPercentile, 0f, 100f);
            Assert.True(Census.Corpus.WastePerBlockPercentile > Census.Corpus.ProducerSharePercentile);
            Assert.True(Census.Corpus.WastePerBlockPercentile > Census.Corpus.ProducerWattsPercentile);

            Assert.InRange(Census.Corpus.FlooredAtCap8, 0f, 1f);
            Assert.InRange(Census.Corpus.FlooredAtCap1, 0f, 1f);

            Assert.True(Census.Corpus.Ships > 1000,
                "only " + Census.Corpus.Ships + " ships are recorded, which is not a population");
        }

        [Fact]

        public void EvenASmallHullCarriesSomeOfTheTail()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();

            for (int i = 0; i < 200; i++)
            {
                string name = Census.Tiers[Census.TierAt(i)].Name;
                counts[name] = counts.ContainsKey(name) ? counts[name] + 1 : 1;
            }

            Assert.True(counts.ContainsKey("light fitting"),
                "two hundred blocks contained no light fitting, so a small hull would measure a"
                + " ship with no tail and no substep problem");
        }

        [Fact]

        public void ProducersAreTheMeasuredShareAndRatedHotterThanTheStructure()
        {

            ThermalSimulation simulation = Hull(4000);
            int producers = Census.DriveCensus(simulation);

            double share = (double)producers / simulation.Solver.Nodes.Count;
            Assert.True(share > Census.ProducerShare * 0.5 && share < Census.ProducerShare * 1.5,
                producers + " producers of " + simulation.Solver.Nodes.Count
                + " blocks, against a measured share of " + Census.ProducerShare);

            for (int i = 0; i < Census.Tiers.Length; i++)
            {
                Assert.True(Census.ProducerCriticalTemperature >= Census.Tiers[i].CriticalTemperature,
                    "the " + Census.Tiers[i].Name + " tier is rated hotter than the blocks that"
                    + " make the heat, which would make structure the thing that burns first");
            }
        }

        [Fact]

        public void TheProducerPlacementRuleAndTheProducerIdentityPickTheSameBlocks()
        {

            ThermalSimulation simulation = Hull(2000);
            IList<ThermalNode> nodes = simulation.Solver.Nodes;

            int disagreements = 0;
            int producers = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                bool byPlacement = Census.ProducesHeatAt(i);
                bool byIdentity = Census.IsProducer(nodes[i]);

                if (byIdentity) producers++;
                if (byPlacement != byIdentity) disagreements++;
            }

            Assert.True(producers > 0, "the hull has no producer at all, so this judges nothing");
            Assert.Equal(0, disagreements);
        }

        [Fact]

        public void ACensusShipRunsHotWithoutBurning()
        {

            ThermalSimulation simulation = Hull(2000);
            Census.DriveCensus(simulation);

            EnvironmentSample sample = Worlds.Shadow();
            simulation.StepExact(400, sample);

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            float peak = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Temperature > peak) peak = nodes[i].Temperature;
            }

            Assert.True(peak > 400f, "the census ship barely warmed, peak " + peak.ToString("n0")
                + " K, against " + Census.Field.HottestObserved + " K a field ship reached");
            Assert.True(peak < Census.Field.HottestRating,
                "the census ship reached " + peak.ToString("n0")
                + " K, past the " + Census.Field.HottestRating
                + " K rating that two field sessions never crossed");
        }
    }
}

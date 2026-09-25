using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The invariants that make the lab's output trustworthy without anyone reading it.
    ///
    /// <para>
    /// Three faults have been found in this harness so far — a gyro's torque read as thrust, a
    /// block with no declared mount points built with no mounts at all, and a battery counted as
    /// charging and discharging at once. Every one of them was a *definition read wrongly*, every
    /// one produced a plausible-looking report, and **not one was caught by a test**. They were
    /// caught by looking at a number and disbelieving it.
    /// </para>
    ///
    /// <para>
    /// That is not a method that scales to ten thousand ships. These are the standing versions of
    /// the disbelief: physical statements that must hold whatever the ship, so a harness that
    /// starts lying fails here rather than in a report someone has to read carefully.
    /// </para>
    ///
    /// <para>
    /// **What is left here is what nothing else does.** `CorpusSurvey` absorbed the walks that used
    /// to stand alone — sealed blocks are rare, a settled grid sheds what it makes, more load is
    /// never cooler — and `SunlightPanelWalk` took the sunlight claim, because all four want the
    /// same build of the same ship and doing them in one pass costs one pass. The bodies stayed
    /// behind as uncalled helpers for a while, which is the worst of both: a claim that reads as
    /// checked and runs nowhere. They are gone; the claims are in the two classes named above.
    /// </para>
    /// </summary>
    [Collection("alone")]
    public class LabInvariantTests
    {

        private static List<string> Corpus()
        {
            return CorpusFixture.Files();
        }


        [Fact]

        public void NoDefinitionClaimsAPreposterousAmountOfPower()
        {
            if (!GameBlocks.IsInstalled) return;

            const float Ceiling = 1e9f;

            List<string> wrong = new List<string>();

            foreach (GameBlocks.Definition definition in GameBlocks.All())
            {
                if (definition.PowerOutputWatts > Ceiling)
                    wrong.Add(definition + " delivers " + definition.PowerOutputWatts + " W");

                if (definition.PowerDrawWatts > Ceiling)
                    wrong.Add(definition + " draws " + definition.PowerDrawWatts + " W");
            }

            Assert.Empty(wrong);
        }

        [Fact]

        public void EveryDefinitionHasASizeAndAMass()
        {
            if (!GameBlocks.IsInstalled) return;


            List<string> wrong = new List<string>();

            foreach (GameBlocks.Definition definition in GameBlocks.All())
            {
                if (definition.Size.X < 1 || definition.Size.Y < 1 || definition.Size.Z < 1)
                    wrong.Add(definition + " is " + definition.Size + " cells");

                if (definition.Components.Count > 0 && definition.Mass <= 0f)
                    wrong.Add(definition + " is built from components and weighs nothing");
            }

            Assert.Empty(wrong);
        }

        internal static void EveryBlockInABlueprintIsEitherPlacedOrCountedAsUnknown()
        {
            if (!CorpusFixture.OptedIn()) return;
            if (!GameBlocks.IsInstalled) return;

            string root = Blueprints.DefaultPath();
            if (root == null) return;

            List<string> wrong = LabRun.MapMany(Blueprints.Files(root), Account, LabMode.Parallel);

            Assert.Empty(wrong);
        }


        private static List<string> Account(string file)
        {

            List<string> wrong = new List<string>();

            List<Blueprints.Ship> ships = Blueprints.Read(file);
            if (ships.Count == 0) return wrong;

            int accounted = 0;
            foreach (Blueprints.Ship ship in ships) accounted += ship.Blocks + ship.UnknownBlocks;


            int inFile = CountBlocks(file);
            if (inFile != accounted)
            {
                wrong.Add(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file))
                    + ": " + inFile + " blocks in the file, " + accounted + " accounted for");
            }

            return wrong;
        }


        private static int CountBlocks(string file)
        {
            System.Xml.Linq.XDocument document;
            try
            {
                document = System.Xml.Linq.XDocument.Load(file);
            }
            catch
            {
                return -1;
            }

            int count = 0;
            foreach (System.Xml.Linq.XElement grid in document.Descendants("CubeGrid"))
            {
                System.Xml.Linq.XElement blocks = grid.Element("CubeBlocks");
                if (blocks == null) continue;

                foreach (System.Xml.Linq.XElement block in blocks.Elements()) count++;
            }
            return count;
        }

        [Fact]

        public void ABlockWithNoNeighboursIsFullyExposed()
        {
            if (!GameBlocks.IsInstalled) return;

            GameBlocks.Definition armour;
            if (!GameBlocks.BySubtype().TryGetValue("LargeBlockArmorBlock", out armour)) return;

            GridBuilder builder = GridBuilder.Large();
            builder.Place(Blueprints.Model(armour), Vector3I.Zero);

            builder.Place(Blueprints.Model(armour), new Vector3I(0, 0, 12));

            ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings());

            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                ThermalNode node = simulation.Solver.Nodes[i];

                Assert.True(node.TotalExposedFaces == Face.Count,
                    "a block with no neighbour reports " + node.TotalExposedFaces
                    + " exposed faces of " + Face.Count);
            }
        }

        internal static void RunningTheSameThingTwiceGivesTheSameAnswer()
        {

            List<string> corpus = Corpus();
            if (corpus.Count == 0) return;


            List<string> drifted = new List<string>();
            int ships = 0;

            List<List<string>> reports = CorpusFixture.Sweep("determinism", Compare);
            foreach (List<string> report in reports)
            {
                ships++;
                drifted.AddRange(report);
            }

            Assert.True(ships > 0);
            Assert.Empty(drifted);
        }


        internal static List<string> Compare(Blueprints.Ship ship)
        {
            Dictionary<string, Battery.Scenario> byName = new Dictionary<string, Battery.Scenario>();
            foreach (Battery.Scenario candidate in Battery.All()) byName[candidate.Name] = candidate;

            Battery.Scenario scenario = byName["full-electrical"];
            Battery.Scenario between = byName["burn-forward"];


            List<string> drifted = new List<string>();

            ScenarioOutcome first = Battery.Run(ship, scenario);
            Battery.Run(ship, between);
            ScenarioOutcome third = Battery.Run(ship, scenario);

            ScenarioOutcome clean = Battery.Run(ship.Reload(), scenario);

            if (Differs(first, third)) drifted.Add(ship.Name + ": changed after another scenario ran");
            if (Differs(first, clean)) drifted.Add(ship.Name + ": differs from a freshly read copy");

            return drifted;
        }


        private static bool Differs(ScenarioOutcome a, ScenarioOutcome b)
        {
            if (a.HottestBlock != b.HottestBlock) return true;
            if (System.Math.Abs(a.PeakKelvin - b.PeakKelvin) > 0.001f) return true;
            return System.Math.Abs(a.MeanKelvin - b.MeanKelvin) > 0.001f;
        }
    }
}

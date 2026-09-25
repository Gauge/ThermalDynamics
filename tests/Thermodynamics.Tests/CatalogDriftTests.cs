using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// That the scenario catalogue *is* the blocks it stands in for, and stays that way.
    ///
    /// <para>
    /// `Catalog` used to open by saying its masses were approximations and its thermal properties
    /// "mirror Data/Cubes.xml". Nothing had compared them: four of the six stand-ins were out by
    /// more than five per cent, the large thruster by **4.32x**, the battery by 3.70x because it
    /// carried the *small-grid* battery's mass under a large-grid name, and `ReactorThermal` said a
    /// quarter of a reactor's output becomes heat where the shipped definition says a hundredth.
    /// Every scenario in this repository is built out of these, so every scenario temperature was
    /// quoted off them. backlog.md `C4`.
    /// </para>
    ///
    /// <para>
    /// **The stand-ins are now derived from `Vanilla` and the shipped derivation rather than typed**,
    /// so the disagreement is structurally gone rather than corrected once. These hold it that way:
    /// a hand-typed mass reappearing is what this fails on.
    /// </para>
    /// </summary>
    public class CatalogDriftTests
    {
        private readonly ITestOutputHelper output;


        public CatalogDriftTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private class Stand
        {
            public string Catalogue;
            public float CatalogueMass;
            public string Vanilla;
        }


        private static List<Stand> Stands()
        {
            return new List<Stand>
            {

                new Stand { Catalogue = "LightArmorBlock", CatalogueMass = Mass(Catalog.LightArmor()), Vanilla = "LargeBlockArmorBlock" },

                new Stand { Catalogue = "HeavyArmorBlock", CatalogueMass = Mass(Catalog.HeavyArmor()), Vanilla = "LargeHeavyBlockArmorBlock" },

                new Stand { Catalogue = "SmallReactor", CatalogueMass = Mass(Catalog.Reactor()), Vanilla = "LargeBlockSmallGenerator" },

                new Stand { Catalogue = "LargeReactor", CatalogueMass = Mass(Catalog.LargeReactor()), Vanilla = "LargeBlockLargeGenerator" },

                new Stand { Catalogue = "Battery", CatalogueMass = Mass(Catalog.Battery()), Vanilla = "LargeBlockBatteryBlock" },

                new Stand { Catalogue = "LargeThruster", CatalogueMass = Mass(Catalog.Thruster()), Vanilla = "LargeBlockLargeThrust" },
            };
        }


        private static float Mass(BlockModel model)
        {
            return model.Mass;
        }

        [Fact]

        public void EveryStandInWeighsWhatItStandsInFor()
        {

            List<string> drifted = new List<string>();
            float worst = 1f;

            foreach (Stand stand in Stands())
            {
                Vanilla.Block real = Vanilla.Find(stand.Vanilla);
                Assert.True(real != null, "no vanilla reference for " + stand.Vanilla);

                float ratio = real.Mass <= 0f ? 1f : stand.CatalogueMass / real.Mass;
                float apart = ratio > 1f ? ratio : 1f / ratio;
                if (apart > worst) worst = apart;

                if (apart > 1.05f)
                {
                    drifted.Add(string.Format("{0,-18}{1,10:n0} kg against {2}'s {3:n0} — {4:n2}x",
                        stand.Catalogue, stand.CatalogueMass, stand.Vanilla, real.Mass, apart));
                }
            }

            foreach (string line in drifted) output.WriteLine(line);

            Assert.True(drifted.Count == 0,
                "a stand-in has stopped being the block it stands in for:\n  "
                + string.Join("\n  ", drifted.ToArray()));

            Assert.True(worst < 1.001f,
                "the worst stand-in is " + worst.ToString("n4") + "x its block, which is not derived");
        }

        [Fact]

        public void AScenarioReactorWastesWhatAShippedOneWastes()
        {
            BlockThermalProperties catalogue = Catalog.Reactor().Thermal;

            BlockThermalProperties real =
                ShippedBlocks.DeriveWithFunction(new List<BlockComponent>(), "Reactor");

            Assert.NotNull(real);
            output.WriteLine("catalogue ProducerWasteEnergy " + catalogue.ProducerWasteEnergy
                + " against the shipped " + real.ProducerWasteEnergy);

            Assert.Equal(real.ProducerWasteEnergy, catalogue.ProducerWasteEnergy, 4);
            Assert.Equal(0.01f, catalogue.ProducerWasteEnergy, 4);
        }

        [Fact]

        public void ArmourMatchesExactly()
        {
            Assert.Equal(Vanilla.Find("LargeBlockArmorBlock").Mass, Mass(Catalog.LightArmor()), 1);
            Assert.Equal(Vanilla.Find("LargeHeavyBlockArmorBlock").Mass, Mass(Catalog.HeavyArmor()), 1);
        }
    }
}

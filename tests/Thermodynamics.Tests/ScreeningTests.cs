using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The cheap pass of the balance lab: what a ship is, before anything is stepped.
    ///
    /// Every figure here becomes a row in the matrix the balance criteria are decided from, so an
    /// error in one of them is an error in a default. The load model is the part most able to be
    /// wrong while looking right, and the tests are weighted accordingly.
    /// </summary>
    public class ScreeningTests
    {
        [Fact]

        public void ThrustIsTheStrongestDirectionRatherThanTheSumOfAllOfThem()
        {
            if (!GameBlocks.IsInstalled) return;


            ShipProfile balanced = Measure(new string[]
            {
                "LargeBlockArmorBlock:Forward:0",
                "LargeBlockSmallThrust:Forward:2",
                "LargeBlockSmallThrust:Backward:4",
            });

            if (balanced == null) return;

            Assert.True(balanced.ThrustNewtons > 0f, "no thrust was found at all");
            Assert.True(balanced.ThrustNewtonsAllDirections > balanced.ThrustNewtons,
                "a ship with opposed thrusters reported the same figure both ways: "
                + balanced.ThrustNewtons + " against " + balanced.ThrustNewtonsAllDirections);
        }

        [Fact]

        public void OnlyAThrusterCarriesThrust()
        {
            if (!GameBlocks.IsInstalled) return;


            List<string> wrong = new List<string>();

            foreach (GameBlocks.Definition definition in GameBlocks.All())
            {
                if (definition.TypeId != "Thrust" && definition.ThrustNewtons > 0f)
                {
                    wrong.Add(definition.TypeId + "/" + definition.SubtypeId
                        + " reports " + definition.ThrustNewtons + " N of thrust");
                }
            }

            Assert.Empty(wrong);
        }

        [Fact]

        public void AGyroIsRatedByItsDrawAndNotByItsTorque()
        {
            if (!GameBlocks.IsInstalled) return;

            GameBlocks.Definition gyro;
            if (!GameBlocks.BySubtype().TryGetValue("LargeBlockGyro", out gyro)) return;

            Assert.Equal(0f, gyro.ThrustNewtons);
            Assert.True(gyro.PowerDrawWatts > 0f, "a gyro draws nothing at all");
            Assert.True(gyro.PowerDrawWatts < 1e6f,
                "a gyro drawing " + gyro.PowerDrawWatts + " W has picked up its torque again");
        }

        [Fact]

        public void ABlockThatDeclaresNoMountPointsStillConducts()
        {
            if (!GameBlocks.IsInstalled) return;

            GameBlocks.Definition battery;
            if (!GameBlocks.BySubtype().TryGetValue("LargeBlockBatteryBlock", out battery)) return;

            Assert.False(battery.HasDeclaredMounts);


            ShipProfile profile = Measure(new string[]
            {
                "LargeBlockBatteryBlock:Forward:0",
                "LargeBlockBatteryBlock:Forward:1",
            });

            if (profile == null) return;

            Assert.Equal(2, profile.Blocks);
            Assert.True(profile.ExposedArea > 0f,
                "a block with no declared mounts came back with no exposed surface at all");
        }

        [Fact]

        public void AStoreIsNotBothChargingAndDischarging()
        {
            if (!GameBlocks.IsInstalled) return;

            GameBlocks.Definition battery;
            if (!GameBlocks.BySubtype().TryGetValue("LargeBlockBatteryBlock", out battery)) return;

            Assert.True(battery.PowerOutputWatts > 0f && battery.PowerDrawWatts > 0f,
                "the premise has changed: a battery no longer rates both ways");
            Assert.True(ShipLoad.IsStore(battery.TypeId));


            ShipProfile profile = Measure(new string[]
            {
                "LargeBlockBatteryBlock:Forward:0",
                "LargeBlockBatteryBlock:Forward:1",
            });

            if (profile == null) return;

            Assert.Equal(0f, profile.WasteWatts, 1);
        }

        [Fact]

        public void ThermalStressIsWattsOverExposedArea()
        {
            ShipProfile profile = new ShipProfile { WasteWatts = 5000f, ExposedArea = 100f };
            Assert.Equal(50f, profile.ThermalStress, 3);

            Assert.Equal(0f, new ShipProfile { WasteWatts = 5000f, ExposedArea = 0f }.ThermalStress);
        }

        [Fact]

        public void TheEquilibriumEstimateInvertsStefanBoltzmann()
        {
            ShipProfile profile = new ShipProfile { WasteWatts = 8500f, ExposedArea = 1f };
            float kelvin = profile.EquilibriumKelvin(0.15f);

            Assert.InRange(kelvin, 950f, 1050f);
            Assert.Equal(0f, new ShipProfile().EquilibriumKelvin());
        }

        [Fact]

        public void ThePanelAlwaysHoldsTheExtremesOfEveryAxis()
        {

            List<ShipProfile> corpus = Corpus();
            List<Specimens.Scored> panel = Specimens.Select(corpus, 8);

            int largest = 0;
            for (int i = 0; i < corpus.Count; i++)
            {
                if (corpus[i].Blocks > corpus[largest].Blocks) largest = i;
            }

            bool found = false;
            foreach (Specimens.Scored scored in panel)
            {
                if (ReferenceEquals(scored.Ship, corpus[largest])) found = true;
            }

            Assert.True(found, "the largest ship in the corpus is not in the panel");
        }

        [Fact]

        public void ACrowdOfNearDuplicatesDoesNotCrowdOutTheOutlier()
        {

            List<ShipProfile> corpus = new List<ShipProfile>();

            for (int i = 0; i < 20; i++)
            {
                corpus.Add(new ShipProfile
                {
                    Name = "clone " + i,
                    Blocks = 100 + i,
                    Mass = 10000f,
                    ExposedArea = 200f,
                    WasteWatts = 50000f,
                    PeakSubstepDemand = 2f,
                });
            }

            ShipProfile outlier = new ShipProfile
            {
                Name = "outlier",
                Blocks = 9000,
                Mass = 6000000f,
                ExposedArea = 80000f,
                WasteWatts = 400000f,
                PeakSubstepDemand = 40f,
                Large = true,
            };
            corpus.Add(outlier);

            List<Specimens.Scored> panel = Specimens.Select(corpus, 3);

            bool found = false;
            foreach (Specimens.Scored scored in panel)
            {
                if (ReferenceEquals(scored.Ship, outlier)) found = true;
            }

            Assert.True(found, "a panel of three took three clones and left the outlier out");
        }

        [Fact]

        public void NearDuplicatesAreReportedAsRedundantAndTheOutlierIsNot()
        {

            List<ShipProfile> corpus = new List<ShipProfile>();

            for (int i = 0; i < 5; i++)
            {
                corpus.Add(new ShipProfile
                {
                    Name = "clone " + i,
                    Blocks = 100,
                    Mass = 10000f,
                    ExposedArea = 200f,
                    WasteWatts = 50000f,
                    PeakSubstepDemand = 2f,
                });
            }

            ShipProfile outlier = new ShipProfile
            {
                Name = "outlier",
                Blocks = 9000,
                Mass = 6000000f,
                ExposedArea = 80000f,
                WasteWatts = 400000f,
                PeakSubstepDemand = 40f,
            };
            corpus.Add(outlier);

            List<KeyValuePair<ShipProfile, double>> redundant = Specimens.Redundant(corpus, 0.05d);

            Assert.Equal(5, redundant.Count);
            foreach (KeyValuePair<ShipProfile, double> entry in redundant)
            {
                Assert.NotEqual("outlier", entry.Key.Name);
            }
        }

        [Fact]

        public void FidelityImprovesAsThePanelGrows()
        {

            List<ShipProfile> corpus = Corpus();

            double small = Specimens.Fidelity(corpus, Specimens.Select(corpus, 3));
            double large = Specimens.Fidelity(corpus, Specimens.Select(corpus, 10));
            double whole = Specimens.Fidelity(corpus, Specimens.Select(corpus, corpus.Count));

            Assert.True(large <= small + 1e-9d, "a bigger panel served the corpus worse");
            Assert.Equal(0d, whole, 6);
        }


        private static List<ShipProfile> Corpus()
        {

            List<ShipProfile> corpus = new List<ShipProfile>();

            for (int i = 0; i < 24; i++)
            {
                corpus.Add(new ShipProfile
                {
                    Name = "ship " + i,
                    Blocks = 20 + (i * i * 15),
                    Mass = 1000f + (i * 250000f),
                    ExposedArea = 30f + (i * 3000f),
                    ExposedFraction = 0.5f + (i % 5) * 0.1f,
                    WasteWatts = i * 25000f,
                    PeakSubstepDemand = 1.2f + (i * 1.7f),
                    Large = (i % 2) == 0,
                });
            }

            return corpus;
        }


        private static ShipProfile Measure(string[] entries)
        {
            Dictionary<string, GameBlocks.Definition> definitions = GameBlocks.BySubtype();

            System.Text.StringBuilder xml = new System.Text.StringBuilder();
            foreach (string entry in entries)
            {
                string[] parts = entry.Split(':');
                if (!definitions.ContainsKey(parts[0])) return null;

                xml.Append("<MyObjectBuilder_CubeBlock xsi:type=\"MyObjectBuilder_CubeBlock\">")
                   .Append("<SubtypeName>").Append(parts[0]).Append("</SubtypeName>")
                   .Append("<Min x=\"").Append(parts[2]).Append("\" y=\"0\" z=\"0\" />")
                   .Append("<BlockOrientation Forward=\"").Append(parts[1]).Append("\" Up=\"Up\" />")
                   .Append("</MyObjectBuilder_CubeBlock>");
            }

            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "thermal-screen-" + System.Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(path);

            string file = System.IO.Path.Combine(path, "bp.sbc");
            System.IO.File.WriteAllText(file,
                "<?xml version=\"1.0\"?>\n" +
                "<Definitions xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<ShipBlueprints><ShipBlueprint>" +
                "<Id Type=\"MyObjectBuilder_ShipBlueprintDefinition\" Subtype=\"T\" />" +
                "<CubeGrids><CubeGrid><DisplayName>T</DisplayName>" +
                "<GridSizeEnum>Large</GridSizeEnum>" +
                "<CubeBlocks>" + xml + "</CubeBlocks>" +
                "</CubeGrid></CubeGrids></ShipBlueprint></ShipBlueprints></Definitions>");

            List<Blueprints.Ship> ships = Blueprints.Read(file);
            return ships.Count == 0 ? null : ShipProfile.Measure(ships[0]);
        }
    }
}

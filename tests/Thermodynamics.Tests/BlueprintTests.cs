using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Reading a real ship out of a blueprint file.
    ///
    /// This is the foundation of the balance lab, and the thing most able to be quietly wrong. A
    /// blueprint that half-parses does not throw: it yields a ship with some of its blocks, which
    /// simulates perfectly well and answers a different question from the one being asked. Every
    /// test here is about the *yield* rather than about the physics.
    ///
    /// Every test here is synthetic, and deliberately: the end of the chain — a real subscribed
    /// blueprint building a simulation that steps — is `CorpusSurvey`, which builds every ship in
    /// the corpus, probes that it steps and counts joints, rooms and small grids in one pass.
    /// </summary>
    public class BlueprintTests
    {

        private static string WriteBlueprint(string blocks, bool large = true)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "thermal-bp-" + System.Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(path);

            string file = Path.Combine(path, "bp.sbc");
            File.WriteAllText(file,
                "<?xml version=\"1.0\"?>\n" +
                "<Definitions xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "  <ShipBlueprints>\n" +
                "    <ShipBlueprint>\n" +
                "      <Id Type=\"MyObjectBuilder_ShipBlueprintDefinition\" Subtype=\"TestShip\" />\n" +
                "      <CubeGrids>\n" +
                "        <CubeGrid>\n" +
                "          <DisplayName>Test Ship</DisplayName>\n" +
                "          <GridSizeEnum>" + (large ? "Large" : "Small") + "</GridSizeEnum>\n" +
                "          <CubeBlocks>\n" + blocks + "\n          </CubeBlocks>\n" +
                "        </CubeGrid>\n" +
                "      </CubeGrids>\n" +
                "    </ShipBlueprint>\n" +
                "  </ShipBlueprints>\n" +
                "</Definitions>\n");
            return file;
        }


        private static string BlueprintText(string blocks)
        {
            return "<?xml version=\"1.0\"?>\n" +
                "<Definitions xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                "  <ShipBlueprints>\n" +
                "    <ShipBlueprint>\n" +
                "      <Id Type=\"MyObjectBuilder_ShipBlueprintDefinition\" Subtype=\"TestShip\" />\n" +
                "      <CubeGrids>\n" +
                "        <CubeGrid>\n" +
                "          <DisplayName>Test Ship</DisplayName>\n" +
                "          <GridSizeEnum>Large</GridSizeEnum>\n" +
                "          <CubeBlocks>\n" + blocks + "\n          </CubeBlocks>\n" +
                "        </CubeGrid>\n" +
                "      </CubeGrids>\n" +
                "    </ShipBlueprint>\n" +
                "  </ShipBlueprints>\n" +
                "</Definitions>\n";
        }


        private static string Block(string subtype, int x, int y, int z, string orientation = null)
        {
            return "<MyObjectBuilder_CubeBlock xsi:type=\"MyObjectBuilder_CubeBlock\">" +
                (subtype == null ? "<SubtypeName />" : "<SubtypeName>" + subtype + "</SubtypeName>") +
                "<Min x=\"" + x + "\" y=\"" + y + "\" z=\"" + z + "\" />" +
                (orientation ?? "") +
                "</MyObjectBuilder_CubeBlock>";
        }

        [Fact]
        public void UpgradeLookupRejectsMissingUnknownAndCompetingIdentities()
        {
            Assert.Null(GameBlocks.UniqueSubtypeUpgrade(null, "AutoCannonTurret"));
            Assert.Null(GameBlocks.UniqueSubtypeUpgrade("NoSuchObjectBuilder", "AutoCannonTurret"));
            Assert.Null(GameBlocks.UniqueSubtypeUpgrade("LargeMissileTurret", "NoSuchSubtype"));
            Assert.Null(GameBlocks.UniqueSubtypeUpgrade("LargeMissileTurret", ""));
            Assert.Null(GameBlocks.UniqueSubtypeUpgrade("CubeBlock", "LargePistonBase"));
            var upgraded = GameBlocks.UniqueSubtypeUpgrade("MyObjectBuilder_LargeMissileTurret", "AutoCannonTurret");
            Assert.NotNull(upgraded);
            Assert.Equal("LargeGatlingTurret", upgraded.TypeId);
            Assert.False(upgraded.Large);
        }

        [Fact]
        public void UpgradeCannotChangeGridSizeOrChooseACompetingDefinition()
        {
            foreach (string xml in new[] { Typed("LargeMissileTurret", "AutoCannonTurret", 1, 0, 0),
                Typed("CubeBlock", "LargePistonBase", 1, 0, 0), Typed("NoSuchObjectBuilder", "AutoCannonTurret", 1, 0, 0) })
            {
                string path = WriteBlueprint(Block("LargeBlockArmorBlock", 0, 0, 0) + xml);
                try
                {
                    var ship = Assert.Single(Blueprints.Read(path));
                    Assert.Equal(1, ship.Blocks);
                    Assert.Equal(1, ship.UnknownBlocks);
                    Assert.Equal(0, ship.UpgradedBlocks);
                }
                finally { Directory.Delete(Path.GetDirectoryName(path), true); }
            }
        }

        [Fact]
        public void LegacyAutocannonTypeLoadsWithTheInstalledReplacementGeometry()
        {
            string legacy = WriteBlueprint(Typed("LargeMissileTurret", "AutoCannonTurret", 2, 3, 4), false);
            string modern = WriteBlueprint(Typed("LargeGatlingTurret", "AutoCannonTurret", 2, 3, 4), false);
            try
            {
                var before = Assert.Single(Blueprints.Read(legacy));
                var after = Assert.Single(Blueprints.Read(modern));
                Assert.True(before.IsVanilla);
                Assert.Equal(0, before.AmbiguousBlocks);
                Assert.Equal(1, before.UpgradedBlocks);
                Assert.Equal(0, after.UpgradedBlocks);
                var a = Assert.Single(before.Grids[0].Builder.Grid.Blocks);
                var b = Assert.Single(after.Grids[0].Builder.Grid.Blocks);
                Assert.Equal(b.Mass, a.Mass);
                Assert.Equal(b.Name, a.Name);
                Assert.Equal(b.Position, a.Position);
                Assert.Equal(b.Model.Size, a.Model.Size);
                Assert.True(a.Mass > 0);
            }
            finally { Directory.Delete(Path.GetDirectoryName(legacy), true); Directory.Delete(Path.GetDirectoryName(modern), true); }
        }

        [Fact]

        public void AShipIsReadWithItsNameAndItsBlocks()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(
                Block("LargeBlockArmorBlock", 0, 0, 0) +
                Block("LargeBlockArmorBlock", 1, 0, 0) +
                Block("LargeHeavyBlockArmorBlock", 2, 0, 0));

            List<Blueprints.Ship> ships = Blueprints.Read(file);

            Assert.Single(ships);

            Assert.Equal("TestShip", ships[0].Name);
            Assert.Equal(3, ships[0].Blocks);
            Assert.Single(ships[0].Grids);
            Assert.True(ships[0].IsVanilla);
        }


        private static string Typed(string typeId, string subtype, int x, int y, int z)
        {
            return "<MyObjectBuilder_CubeBlock xsi:type=\"MyObjectBuilder_" + typeId + "\">" +
                (subtype == null ? "<SubtypeName />" : "<SubtypeName>" + subtype + "</SubtypeName>") +
                "<Min x=\"" + x + "\" y=\"" + y + "\" z=\"" + z + "\" />" +
                "</MyObjectBuilder_CubeBlock>";
        }

        [Fact]

        public void ABlockWithNoSubtypeNameIsItsOwnTypesBaseVariantRatherThanArmour()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(Typed("OxygenGenerator", null, 0, 0, 0));
            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.Equal(1, ship.Blocks);
            Assert.Equal(0, ship.UnknownBlocks);

            BlockInstance placed = ship.Grids[0].Builder.Placed[0];
            Assert.True(placed.Model.Mass > 2000f,
                "a vanilla oxygen generator was built weighing " + placed.Model.Mass
                + " kg; light armour is 500 and the generator is 2,587, so an empty SubtypeName is"
                + " resolving to armour again");
        }

        [Fact]

        public void ASubtypeClaimedByTwoTypesResolvesByTheTypeTheBlueprintStates()
        {
            if (!GameBlocks.IsInstalled) return;


            string extended = WriteBlueprint(Typed("ExtendedPistonBase", "LargePistonBase", 0, 0, 0));

            string plain = WriteBlueprint(Typed("PistonBase", "LargePistonBase", 0, 0, 0));

            BlockModel extendedModel = Blueprints.Read(extended)[0].Grids[0].Builder.Placed[0].Model;
            BlockModel plainModel = Blueprints.Read(plain)[0].Grids[0].Builder.Placed[0].Model;

            Assert.Equal(3, extendedModel.Size.Y);
            Assert.Equal(2, plainModel.Size.Y);
        }

        [Fact]

        public void NoBaseVariantsTypeIdIsAlsoSomeOtherBlocksSubtype()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, GameBlocks.Definition> bySubtype = GameBlocks.BySubtype();

            List<string> collided = new List<string>();

            foreach (GameBlocks.Definition variant in GameBlocks.BaseVariants().Values)
            {
                GameBlocks.Definition other;
                if (bySubtype.TryGetValue(variant.TypeId, out other) && other.SubtypeId.Length > 0)
                {
                    collided.Add(variant.TypeId + " is also the subtype of a " + other.TypeId);
                }
            }

            Assert.True(GameBlocks.BaseVariants().Count > 5,
                "only " + GameBlocks.BaseVariants().Count + " base variants were found, so this"
                + " test is looking in the wrong place and would pass on any collision");

            Assert.True(collided.Count == 0, string.Join("\n  ", collided.ToArray()));
        }

        [Fact]

        public void ABaseVariantOfTheWrongGridSizeIsNotBuilt()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(
                Block("LargeBlockArmorBlock", 0, 0, 0) + Typed("SmallGatlingGun", null, 1, 0, 0));
            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.Equal(1, ship.Blocks);
            Assert.Equal(1, ship.UnknownBlocks);
            Assert.False(ship.IsVanilla);
        }

        [Fact]

        public void ABlockWithNoSubtypeNameIsTheBaseArmourCube()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(Block(null, 0, 0, 0) + Block(null, 1, 0, 0));
            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.Equal(2, ship.Blocks);
            Assert.Equal(0, ship.UnknownBlocks);
        }

        [Fact]

        public void OneModdedBlockDisqualifiesTheShip()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(
                Block("LargeBlockArmorBlock", 0, 0, 0) +
                Block("SomeoneElsesRailgunMkIV", 1, 0, 0));

            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.False(ship.IsVanilla);
            Assert.Equal(1, ship.UnknownBlocks);
            Assert.Contains("SomeoneElsesRailgunMkIV", ship.UnknownSubtypes);
        }

        [Fact]

        public void ALargeGridBlockIsNotAcceptedOnASmallGridShip()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(
                Block("SmallBlockArmorBlock", 0, 0, 0) +
                Block("LargeBlockArmorBlock", 1, 0, 0), false);

            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.Equal(1, ship.Blocks);
            Assert.Equal(1, ship.UnknownBlocks);
            Assert.False(ship.IsVanilla);
        }

        [Fact]

        public void AnOrientedBlockKeepsItsOrientation()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(Block("LargeBlockArmorBlock", 0, 0, 0,
                "<BlockOrientation Forward=\"Right\" Up=\"Up\" />"));

            Blueprints.Ship ship = Blueprints.Read(file)[0];

            Assert.Equal(1, ship.Blocks);
            Assert.Equal(Base6Directions.Direction.Right,
                ship.Grids[0].Builder.Grid.Blocks[0].Orientation.Forward);
        }

        [Fact]

        public void AnUnreadableFileYieldsNoShips()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "thermal-bp-" + System.Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(path);

            string file = Path.Combine(path, "bp.sbc");
            File.WriteAllText(file, "this is not xml <<<");

            Assert.Empty(Blueprints.Read(file));
        }

        [Fact]

        public void ABlueprintWithTwoBlocksInOneCellIsSkippedRatherThanThrown()
        {
            if (!GameBlocks.IsInstalled) return;


            string file = WriteBlueprint(
                Block("LargeBlockArmorBlock", 0, 0, 0) +
                Block("LargeHeavyBlockArmorBlock", 0, 0, 0));

            Assert.Empty(Blueprints.Read(file));
            Assert.True(Blueprints.Unreadable().ContainsKey(file),
                "the file should have been recorded as unreadable");
        }
        [Theory]
        [InlineData("244850/1415550344", 1415550344L)]
        [InlineData("244850/1415550344/T.N.F. Orbital Station 'Charlie'", 1415550344L)]
        [InlineData("244850/413269248/T.N.F. Strike Carrier Class 'Tigershark' Mk.II", 413269248L)]

        public void AWorkshopIdIsReadThroughACollectionFolder(string under, long expected)
        {
            string root = Path.Combine(Path.GetTempPath(),
                "thermal-ws-" + System.Guid.NewGuid().ToString("n"));
            string folder = Path.Combine(root, "content", under.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "bp.sbc");
            File.WriteAllText(path, BlueprintText(Block("LargeBlockArmorBlock", 0, 0, 0)));

            try
            {
                List<Blueprints.Ship> ships = Blueprints.Read(path);
                Assert.NotEmpty(ships);
                Assert.Equal(expected, ships[0].WorkshopId);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]

        public void ABlueprintOutsideTheWorkshopHasNoId()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "thermal-ws-" + System.Guid.NewGuid().ToString("n"), "somewhere", "else");
            Directory.CreateDirectory(root);

            string path = Path.Combine(root, "bp.sbc");
            File.WriteAllText(path, BlueprintText(Block("LargeBlockArmorBlock", 0, 0, 0)));

            try
            {
                List<Blueprints.Ship> ships = Blueprints.Read(path);
                Assert.NotEmpty(ships);
                Assert.Equal(0L, ships[0].WorkshopId);
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(root)), true);
            }
        }

    }
}

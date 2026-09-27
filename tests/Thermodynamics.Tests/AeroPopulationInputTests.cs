using System;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Independent full-power thrust and coordinate controls for the population instrument's inputs.</summary>
    public class AeroPopulationInputTests
    {
        [Theory]
        [InlineData(-1f, 100f)]
        [InlineData(0f, 100f)]
        [InlineData(0.5f, 60f)]
        [InlineData(1f, 20f)]
        [InlineData(2f, 20f)]
        public void IonCurveMatchesHandComputedInterpolation(float influence, float expected)
        {
            GameBlocks.Definition ion = new GameBlocks.Definition
            { ThrustNewtons = 100, EffectivenessAtMaxInfluence = 0.2f };
            Assert.InRange(Math.Abs(ion.EffectiveThrust(influence, true) - expected), 0, 1e-5f);
            Assert.Equal(100, ion.ThrustNewtons);
        }

        [Fact]
        public void AtmosphereDefaultAndEqualEndpointsFollowEngineBranches()
        {
            GameBlocks.Definition atmosphere = new GameBlocks.Definition
            {
                ThrustNewtons = 100, MinPlanetaryInfluence = 0.3f,
                EffectivenessAtMinInfluence = 0, NeedsAtmosphereForInfluence = true
            };
            Assert.Equal(0, atmosphere.EffectiveThrust(1, false));
            Assert.Equal(0, atmosphere.EffectiveThrust(0.3f, true));
            Assert.InRange(atmosphere.EffectiveThrust(0.65f, true), 49.999f, 50.001f);
            Assert.Equal(100, atmosphere.EffectiveThrust(1, true));
            atmosphere.MaxPlanetaryInfluence = atmosphere.MinPlanetaryInfluence;
            Assert.Equal(100, atmosphere.EffectiveThrust(0, true));
            Assert.Equal(0, atmosphere.EffectiveThrust(0, false));
            GameBlocks.Definition neutral = new GameBlocks.Definition { ThrustNewtons = 100 };
            Assert.Equal(100, neutral.EffectiveThrust(0, false));
            Assert.Equal(100, neutral.EffectiveThrust(1, true));
            Assert.Throws<ArgumentException>(() => neutral.EffectiveThrust(float.NaN, true));
        }

        [Theory]
        [InlineData("LargeBlockSmallThrust", 345600f, 69120f, 345600f)]
        [InlineData("LargeBlockSmallAtmosphericThrust", 648000f, 648000f, 0f)]
        [InlineData("LargeBlockSmallHydrogenThrust", 1080000f, 1080000f, 1080000f)]
        public void InstalledThrustersMatchIndependentSbcRatings(string name, float nominal, float sea, float vacuum)
        {
            Assert.True(GameBlocks.IsInstalled, "Installed SBC controls require the game content.");
            GameBlocks.Definition definition = GameBlocks.BySubtype()[name];
            Assert.Equal(nominal, definition.ThrustNewtons);
            Assert.InRange(Math.Abs(definition.EffectiveThrust(1, true) - sea), 0, 0.1f);
            Assert.InRange(Math.Abs(definition.EffectiveThrust(0, false) - vacuum), 0, 0.1f);
        }

        [Fact]
        public void QuaternionAssignmentOrderMatchesTheEnginePoseSetter()
        {
            XElement pose = Pose(MatrixD.Identity);
            pose.Add(XElement.Parse("<Orientation><X>0</X><Y>0.70710678</Y><Z>0</Z><W>0.70710678</W></Orientation>"));
            BlueprintFrame frame = BlueprintFrame.Read(pose);
            Assert.True(frame.Valid, frame.Error);
            Near(Vector3D.Left, frame.World.Forward);
            XElement quaternionOnly = new XElement(pose);
            quaternionOnly.Element("Forward").Remove();
            quaternionOnly.Element("Up").Remove();
            Near(Vector3D.Left, BlueprintFrame.Read(quaternionOnly).World.Forward);
            pose.Add(new XElement("Forward", new XAttribute("x", 0), new XAttribute("y", 0), new XAttribute("z", -1)));
            pose.Add(new XElement("Up", new XAttribute("x", 0), new XAttribute("y", 1), new XAttribute("z", 0)));
            Near(Vector3D.Forward, BlueprintFrame.Read(pose).World.Forward);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("<PositionAndOrientation />")]
        [InlineData("<PositionAndOrientation><Position x='NaN' y='0' z='0'/><Forward x='0' y='0' z='-1'/><Up x='0' y='1' z='0'/></PositionAndOrientation>")]
        [InlineData("<PositionAndOrientation><Position x='0' y='0' z='0'/><Forward x='0' y='0' z='0'/><Up x='0' y='1' z='0'/></PositionAndOrientation>")]
        [InlineData("<PositionAndOrientation><Position x='0' y='0' z='0'/><Forward x='0' y='1' z='0'/><Up x='0' y='1' z='0'/></PositionAndOrientation>")]
        [InlineData("<PositionAndOrientation><Position x='0' y='0' z='0'/><Orientation><X>0</X><Y>0</Y><Z>0</Z><W>0</W></Orientation></PositionAndOrientation>")]
        public void InvalidPosesRemainVisible(string xml)
        {
            BlueprintFrame frame = BlueprintFrame.Read(xml == null ? null : XElement.Parse(xml));
            Assert.False(frame.Valid);
            Assert.False(string.IsNullOrEmpty(frame.Error));
        }

        [Fact]
        public void ThrusterForceOpposesExhaustAndRotatesWithEachSubgrid()
        {
            Blueprints.Ship ship = Ship(MatrixD.Identity);
            var forces = AeroPopulationInputs.Read(ship, 1f, true);
            Assert.Equal(2, forces.Count);
            Near(Vector3D.Backward, forces[0].Direction);
            Near(Vector3D.Right, forces[1].Direction);
            Assert.InRange(forces[0].Effective, 69119.9f, 69120.1f);
            MatrixD common = MatrixD.CreateRotationZ(Math.PI / 2);
            var rotated = AeroPopulationInputs.Read(Ship(common), 1f, true);
            for (int i = 0; i < forces.Count; i++)
            {
                Near(Vector3D.TransformNormal(forces[i].Direction, common), rotated[i].Direction);
                Assert.Equal(forces[i].Effective, rotated[i].Effective);
            }
            ship.Grids[0].Frame = BlueprintFrame.Read(null);
            Assert.Throws<InvalidDataException>(() => AeroPopulationInputs.Read(ship, 1, true));
        }

        [Fact]
        public void BlueprintReaderRetainsBothWorldFramesAndDoublePrecisionPositions()
        {
            string path = Path.Combine(Path.GetTempPath(), "aero-frame-" + Guid.NewGuid().ToString("n") + ".sbc");
            try
            {
                MatrixD translated = MatrixD.CreateWorld(new Vector3D(1e12 + 0.25, 4, 5), Vector3D.Left, Vector3D.Up);
                XElement grid = new XElement("CubeGrid", new XElement("GridSizeEnum", "Large"), Pose(translated),
                    new XElement("CubeBlocks", new XElement("MyObjectBuilder_CubeBlock",
                        new XElement("SubtypeName", "LargeBlockArmorBlock"),
                        new XElement("Min", new XAttribute("x", 0), new XAttribute("y", 0), new XAttribute("z", 0)))));
                XElement other = new XElement(grid);
                other.Element("PositionAndOrientation").ReplaceWith(Pose(MatrixD.Identity));
                new XDocument(new XElement("Definitions", new XElement("ShipBlueprints", new XElement("ShipBlueprint",
                    new XElement("CubeGrids", grid, other))))).Save(path);
                var ships = Blueprints.Read(path);
                Assert.Single(ships);
                Assert.Equal(2, ships[0].Grids.Count);
                Assert.All(ships[0].Grids, part => Assert.True(part.Frame.Valid, part.Frame.Error));
                Blueprints.Grid shifted = Assert.Single(ships[0].Grids, part => part.Frame.World.Translation.X > 1e11);
                Near(Vector3D.Left, shifted.Frame.World.Forward);
                Assert.Equal(translated.Translation, shifted.Frame.World.Translation);
                Blueprints.Grid origin = Assert.Single(ships[0].Grids, part => part.Frame.World.Translation == Vector3D.Zero);
                Near(Vector3D.Forward, origin.Frame.World.Forward);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ExportedThrusterDirectionsRetainTheReferenceFramesDoublePrecision()
        {
            Blueprints.Ship ship = Ship(MatrixD.CreateRotationY(0.123456789) * MatrixD.CreateRotationX(0.321987));
            Vector3D expected = ship.Grids[0].Frame.World.Backward;
            Assert.NotEqual(expected, (Vector3D)(Vector3)expected);
            var thrust = AeroPopulationInputs.Read(ship, 1, true);
            Assert.Equal(expected, thrust[0].Direction);
        }

        [Fact]
        public void JsonExportCarriesForceDirectionsRatingsAndSixReferenceHeadings()
        {
            string path = Path.Combine(Path.GetTempPath(), "aero-json-" + Guid.NewGuid().ToString("n") + ".sbc");
            try
            {
                XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                XElement grid = new XElement("CubeGrid", new XElement("GridSizeEnum", "Large"), Pose(MatrixD.Identity),
                    new XElement("CubeBlocks", new XElement("MyObjectBuilder_CubeBlock",
                        new XAttribute(xsi + "type", "MyObjectBuilder_Thrust"),
                        new XElement("SubtypeName", "LargeBlockSmallThrust"),
                        new XElement("Min", new XAttribute("x", 0), new XAttribute("y", 0), new XAttribute("z", 0)))));
                new XDocument(new XElement("Definitions", new XElement("ShipBlueprints", new XElement("ShipBlueprint",
                    new XElement("CubeGrids", grid))))).Save(path);
                using (JsonDocument document = JsonDocument.Parse(AeroPopulationInputs.Json(path)))
                {
                    JsonElement root = document.RootElement;
                    Assert.Equal(1, root.GetProperty("schema").GetInt32());
                    Assert.Equal("aero-population-inputs", root.GetProperty("kind").GetString());
                    Assert.Equal(1, root.GetProperty("relative_air_density").GetDouble());
                    Assert.True(root.GetProperty("atmosphere").GetBoolean());
                    Assert.Equal(1, root.GetProperty("grids").GetInt32());
                    Assert.Equal(GameBlocks.BySubtype()["LargeBlockSmallThrust"].Mass, root.GetProperty("mass_kg").GetDouble());
                    JsonElement thrusters = root.GetProperty("thrusters");
                    Assert.Equal(1, thrusters.GetArrayLength());
                    Assert.Equal(345600, thrusters[0].GetProperty("nominal_n").GetDouble());
                    Assert.InRange(thrusters[0].GetProperty("effective_n").GetDouble(), 69119.9, 69120.1);
                    JsonElement direction = thrusters[0].GetProperty("direction");
                    Assert.Equal(0, direction[0].GetDouble());
                    Assert.Equal(0, direction[1].GetDouble());
                    Assert.Equal(1, direction[2].GetDouble());
                    JsonElement headings = root.GetProperty("headings");
                    Assert.Equal(6, headings.GetArrayLength());
                    for (int i = 0; i < headings.GetArrayLength(); i++)
                    {
                        if (headings[i].GetProperty("name").GetString() != "forward") continue;
                        Assert.Equal(-1, headings[i].GetProperty("direction")[2].GetDouble());
                        return;
                    }
                    Assert.Fail("The exported reference headings omitted forward.");
                }
            }
            finally { File.Delete(path); }
        }

        private static Blueprints.Ship Ship(MatrixD common)
        {
            var definition = GameBlocks.BySubtype()["LargeBlockSmallThrust"];
            Blueprints.Ship ship = new Blueprints.Ship();
            foreach (MatrixD rotation in new[] { MatrixD.Identity, MatrixD.CreateRotationY(Math.PI / 2) })
                ship.Grids.Add(new Blueprints.Grid
                {
                    Blocks = 1, Large = true, Frame = BlueprintFrame.Read(Pose(rotation * common)),
                    Builder = GridBuilder.Large().Place(Blueprints.Model(definition), Vector3I.Zero)
                });
            return ship;
        }

        private static XElement Pose(MatrixD world)
        {
            return new XElement("PositionAndOrientation", Vector("Position", world.Translation),
                Vector("Forward", world.Forward), Vector("Up", world.Up));
        }

        private static XElement Vector(string name, Vector3D v)
        {
            return new XElement(name, new XAttribute("x", v.X), new XAttribute("y", v.Y), new XAttribute("z", v.Z));
        }

        private static void Near(Vector3D expected, Vector3D actual)
        {
            Assert.InRange(Vector3D.Distance(expected, actual), 0, 1e-5);
        }
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Independent terminal-speed roots and legacy drag on differently oriented grids.</summary>
    public class AeroPopulationDragTests
    {
        [Theory]
        [InlineData(1, "LargeBlockArmorBlock", "excluded_small")]
        [InlineData(25, "LargeBlockArmorBlock", "candidate")]
        [InlineData(25, "NotAnInstalledBlock_AeroTest", "excluded_modded")]
        public void SelectionCountsExplicitExclusions(int count, string subtype, string status)
        {
            string path = Blueprint(count, subtype);
            try
            {
                using (var document = JsonDocument.Parse(AeroPopulationInputs.Selection(path)))
                    Assert.Equal(status, document.RootElement.GetProperty("status").GetString());
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void MissingBlueprintAndForgedThrustProvenanceFailLoudly()
        {
            Assert.Throws<InvalidDataException>(() => AeroPopulationInputs.Selection("missing-aero-fixture.sbc"));
            string path = Blueprint(25, "LargeBlockArmorBlock"), thrust = Path.GetTempFileName();
            try
            {
                File.WriteAllText(thrust, "{\"schema\":1,\"blueprint_sha256\":\"wrong\"}");
                Assert.Throws<InvalidDataException>(() => AeroPopulationDrag.Report(path, thrust));
                using (var input = JsonDocument.Parse(AeroPopulationInputs.Json(path)))
                {
                    var data = input.RootElement;
                    File.WriteAllText(thrust, JsonSerializer.Serialize(new
                    {
                        schema = 1, blueprint_sha256 = data.GetProperty("blueprint_sha256").GetString(),
                        harness_mvid = data.GetProperty("harness_mvid").GetString(), core_mvid = "wrong"
                    }));
                    Assert.Throws<InvalidDataException>(() => AeroPopulationDrag.Report(path, thrust));
                }
            }
            finally { File.Delete(path); File.Delete(thrust); }
        }

        [Fact]
        public void AmbiguousDefinitionsAndMissingFramesAreUnresolved()
        {
            string path = Blueprint(25, "LargeBlockArmorBlock");
            try
            {
                var xml = XDocument.Load(path);
                foreach (var block in xml.Descendants("MyObjectBuilder_CubeBlock")) block.RemoveAttributes();
                xml.Save(path);
                Assert.Throws<InvalidDataException>(() => AeroPopulationInputs.Selection(path));
                foreach (var block in xml.Descendants("MyObjectBuilder_CubeBlock"))
                    block.SetAttributeValue(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "MyObjectBuilder_CubeBlock");
                foreach (var pose in xml.Descendants("PositionAndOrientation")) { pose.Remove(); break; }
                xml.Save(path);
                Assert.Throws<InvalidDataException>(() => AeroPopulationInputs.Selection(path));
            }
            finally { File.Delete(path); }
        }

        private static string Blueprint(int count, string subtype)
        {
            string path = Path.Combine(Path.GetTempPath(), "population-" + Guid.NewGuid().ToString("n") + ".sbc");
            XElement blocks = new XElement("CubeBlocks");
            for (int i = 0; i < count; i++)
                blocks.Add(new XElement("MyObjectBuilder_CubeBlock",
                    new XAttribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "MyObjectBuilder_CubeBlock"),
                    new XElement("SubtypeName", subtype),
                    new XElement("Min", new XAttribute("x", i), new XAttribute("y", 0), new XAttribute("z", 0))));
            XElement pose = XElement.Parse("<PositionAndOrientation><Position x='0' y='0' z='0'/><Forward x='0' y='0' z='-1'/><Up x='0' y='1' z='0'/></PositionAndOrientation>");
            new XDocument(new XElement("Definitions", new XElement("ShipBlueprints", new XElement("ShipBlueprint",
                new XElement("CubeGrids", new XElement("CubeGrid", new XElement("GridSizeEnum", "Large"), pose, blocks)))))).Save(path);
            return path;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0.125)]
        [InlineData(60)]
        [InlineData(100)]
        [InlineData(128)]
        public void QuadraticDragBracketsTheAnalyticRoot(double speed)
        {
            var result = AeroPopulationDrag.Terminal(v => 3 * v * v, 3 * speed * speed);
            Assert.NotNull(result.Upper);
            Assert.True(result.Lower <= speed && result.Upper.Value >= speed);
            Assert.InRange(result.Upper.Value - result.Lower, 0, AeroPopulationDrag.SpeedTolerance);
        }

        [Fact]
        public void BeyondSubsonicBracketIsCensoredAndInvalidForcesFail()
        {
            var result = AeroPopulationDrag.Terminal(v => v * v, 200 * 200);
            Assert.Equal(128, result.Lower);
            Assert.Null(result.Upper);
            Assert.Throws<ArgumentException>(() => AeroPopulationDrag.Terminal(v => v, double.NaN));
            foreach (Func<double, double> bad in new Func<double, double>[] { v => double.NaN, v => -1, v => 200 - v })
                Assert.Throws<InvalidDataException>(() => AeroPopulationDrag.Terminal(bad, 100));
        }

        [Fact]
        public void RotatedMultipleProfilesMatchIndependentLegacyEvaluator()
        {
            GridModel[] grids = { AeroCalibrationLab.Box(4, 3, 2), AeroCalibrationLab.Box(2, 5, 3) };
            MatrixD[] frames = { MatrixD.CreateRotationY(0.321), MatrixD.CreateRotationX(-0.432) };
            AeroProfile[] profiles = { AeroProfile.Build(grids[0], 98), AeroProfile.Build(grids[1], 98) };
            Vector3D heading = Vector3D.Normalize(new Vector3D(1, 2, 3));
            var force = AeroPopulationDrag.Force(profiles, frames, heading);
            foreach (double speed in new double[] { 0, 0.1, 60, 100, 128 })
            {
                double expected = 0;
                for (int i = 0; i < grids.Length; i++)
                {
                    Vector3 local = Vector3.Normalize(new Vector3((float)Vector3D.Dot(heading, frames[i].Right),
                        (float)Vector3D.Dot(heading, frames[i].Up), (float)Vector3D.Dot(heading, frames[i].Backward)));
                    LegacyAeroProfile.Build(grids[i], 98).Evaluate(-local * (float)speed, 1.225f, Vector3.Zero,
                        AeroCoefficients.Fitted, 1.5e-5f, out Vector3 f, out Vector3 t, out Vector3 d, out Vector3 c);
                    expected += f.Length();
                }
                Assert.InRange(Math.Abs(force(speed) - expected), 0, Math.Max(1, expected) * 2e-6);
                if (speed > 0) Assert.True(expected > 0);
            }
            Assert.Throws<ArgumentException>(() => force(129));
            Assert.Throws<ArgumentException>(() => AeroPopulationDrag.Force(profiles, frames, Vector3D.Zero));
        }
    }
}

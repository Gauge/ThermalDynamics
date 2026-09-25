using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The eight worlds `PlanetLab` transcribes still say what the installed game says.
    ///
    /// <para>
    /// `PlanetLab` generates the shipped `Planets.xml` and opens by claiming that every input comes
    /// from `PlanetGeneratorDefinitions.sbc` and that "nothing here is remembered or estimated" —
    /// but the inputs are typed into a table by hand, and until now nothing compared the two. It is
    /// the same gap `TheVanillaReferenceStillMatchesTheInstalledGame` closed for blocks, on a table
    /// that decides the climate of every world in the game (`E7`).
    /// </para>
    ///
    /// <para>
    /// **Reading the file is itself a trap worth recording.** Six of the eight worlds are written as
    /// `&lt;Definition&gt;` and two — Triton and Pertam — as `&lt;PlanetGeneratorDefinition&gt;`, so
    /// a reader that keys on the first name silently sees six worlds and reports success. This test
    /// asserts it found all eight for that reason.
    /// </para>
    /// </summary>
    public class PlanetReferenceTests
    {
        private class Definition
        {
            public float HillMin;
            public float SurfaceGravity;
            public bool HasAtmosphere;
        }


        private static Dictionary<string, Definition> Installed()
        {
            Dictionary<string, Definition> worlds =
                new Dictionary<string, Definition>(StringComparer.Ordinal);

            string content = GameBlocks.ContentPath();
            if (content == null) return worlds;

            string path = Path.Combine(content, "PlanetGeneratorDefinitions.sbc");
            if (!File.Exists(path)) return worlds;

            foreach (XElement element in XDocument.Load(path).Descendants())
            {
                if (element.Name.LocalName != "Definition"
                    && element.Name.LocalName != "PlanetGeneratorDefinition") continue;

                XElement id = element.Element("Id");
                if (id == null) continue;

                string subtype = (string)id.Element("SubtypeId");
                XElement hills = element.Element("HillParams");
                if (subtype == null || hills == null) continue;

                float min;
                if (!float.TryParse((string)hills.Attribute("Min"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out min)) continue;

                float gravity;
                float.TryParse((string)element.Element("SurfaceGravity"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out gravity);

                bool air;
                bool.TryParse((string)element.Element("HasAtmosphere"), out air);

                worlds[subtype] = new Definition
                {
                    HillMin = min,
                    SurfaceGravity = gravity,
                    HasAtmosphere = air,
                };
            }

            return worlds;
        }

        private static readonly Dictionary<string, float> Radius =
            new Dictionary<string, float>(StringComparer.Ordinal)
            {
                { "EarthLike", 60000f }, { "Alien", 60000f }, { "Mars", 60000f },
                { "Pertam", 30000f }, { "Triton", 40000f },
                { "Europa", 9500f }, { "Titan", 9500f }, { "Moon", 9500f },
            };

        [Fact]

        public void EveryTranscribedWorldMatchesTheInstalledDefinition()
        {

            Dictionary<string, Definition> installed = Installed();
            if (installed.Count == 0) return;

            Assert.True(installed.Count >= 8,
                "only " + installed.Count + " planet definitions were read. Six of the eight are"
                + " <Definition> and two are <PlanetGeneratorDefinition>, so a reader that keys on"
                + " one of those names sees a subset and reports success");


            List<string> wrong = new List<string>();
            int checked_ = 0;

            foreach (PlanetLab.World world in PlanetLab.Vanilla())
            {
                Definition definition;
                if (!installed.TryGetValue(world.Subtype, out definition)) continue;

                checked_++;

                if (Math.Abs(world.Engine.SurfaceGravity - definition.SurfaceGravity) > 0.001f)
                {
                    wrong.Add(world.Subtype + ": gravity " + world.Engine.SurfaceGravity
                        + " against the game's " + definition.SurfaceGravity);
                }

                if (world.Engine.HasAtmosphere != definition.HasAtmosphere)
                {
                    wrong.Add(world.Subtype + ": atmosphere " + world.Engine.HasAtmosphere
                        + " against the game's " + definition.HasAtmosphere);
                }

                float radius;
                if (!Radius.TryGetValue(world.Subtype, out radius)) continue;

                float deepest = Math.Abs(definition.HillMin) * radius;
                if (Math.Abs(world.Engine.DeepestGroundMetres - deepest) > 1f)
                {
                    wrong.Add(world.Subtype + ": deepest ground "
                        + world.Engine.DeepestGroundMetres.ToString("n0")
                        + " m against the game's " + deepest.ToString("n0") + " m");
                }
            }

            Assert.True(checked_ >= 8, "only " + checked_ + " worlds were compared");

            wrong.Sort(StringComparer.Ordinal);
            Assert.True(wrong.Count == 0,
                "the transcribed worlds no longer match the installed game:\n  "
                + string.Join("\n  ", wrong.ToArray()));
        }

        [Fact]

        public void TheDeadzoneIsTheWorldsOwnDeepestGround()
        {
            foreach (PlanetLab.World world in PlanetLab.Vanilla())
            {
                if (world.Engine.DeepestGroundMetres <= 0f) continue;

                Assert.Equal(world.Engine.DeepestGroundMetres, world.Shipped.SealevelDeadzone, 1);
            }

            PlanetThermalDerivation.Engine silent = new PlanetThermalDerivation.Engine
            {
                SurfaceTemperatureLevel = 0.5f,
                SurfaceGravity = 1f,
                HasAtmosphere = true,
                AtmosphereDensity = 1f,
                SolarRadiationProtection = 1f,
            };

            Assert.Equal(new PlanetThermalProperties().SealevelDeadzone,
                PlanetThermalDerivation.Derive(silent).SealevelDeadzone, 1);
        }
    }
}

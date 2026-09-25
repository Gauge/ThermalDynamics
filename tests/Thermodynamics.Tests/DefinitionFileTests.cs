using System.Collections.Generic;
using System.IO;
using System.Xml;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The extension files the mod ships, read the way Definition Extensions reads them.
    ///
    /// The game's own loaders are lenient; Draygo's importer deserialises with the strict .NET
    /// serialiser, and a file it rejects is rejected silently in ordinary play — one log line in a
    /// file nobody reads, and every entry in the file quietly absent. The shipped Planets.xml
    /// carried a "--" inside its header comment, which is illegal XML, so no world that ever
    /// loaded the mod received a single per-planet climate. The report's
    /// <c>[from definition: None]</c> line is what finally said so.
    /// </summary>
    public class DefinitionFileTests
    {


        private static List<string> ExtensionFiles()
        {
            string manifest = Path.Combine(ShippedBlocks.DataRoot(), "definitionextensions.txt");
            Assert.True(File.Exists(manifest), "definitionextensions.txt missing");


            List<string> files = new List<string>();
            foreach (string line in File.ReadAllLines(manifest))
            {
                string name = line.Trim();
                if (name.Length > 0) files.Add(Path.Combine(ShippedBlocks.DataRoot(), name));
            }

            Assert.NotEmpty(files);
            return files;
        }

        [Fact]

        public void EveryListedFileExists()
        {
            foreach (string file in ExtensionFiles())
            {
                Assert.True(File.Exists(file), Path.GetFileName(file) + " is listed and missing");
            }
        }

        [Fact]

        public void EveryListedFileIsStrictlyValidXml()
        {
            foreach (string file in ExtensionFiles())
            {

                XmlDocument document = new XmlDocument();

                try
                {
                    document.Load(file);
                }
                catch (XmlException e)
                {
                    Assert.Fail(Path.GetFileName(file) + " is not strict XML and Definition "
                        + "Extensions will silently drop every entry in it: " + e.Message);
                }
            }
        }

        [Fact]

        public void EveryEntryCarriesAnIdAndAGroup()
        {
            foreach (string file in ExtensionFiles())
            {

                XmlDocument document = new XmlDocument();
                document.Load(file);

                XmlNodeList entries = document.SelectNodes("/Definitions/CubeBlocks/Definition");
                Assert.True(entries != null && entries.Count > 0,
                    Path.GetFileName(file) + " has no Definition entries where the importer looks");

                foreach (XmlNode entry in entries)
                {
                    Assert.True(entry.SelectSingleNode("Id/TypeId") != null,
                        Path.GetFileName(file) + " has an entry without an Id");
                    Assert.True(entry.SelectSingleNode("ModExtensions/Group") != null,
                        Path.GetFileName(file) + " has an entry without a ModExtensions group");
                }
            }
        }

        [Fact]

        public void PlanetsCarryTheGeneratorSubtypesTheLookupAsksFor()
        {

            XmlDocument document = new XmlDocument();
            document.Load(Path.Combine(ShippedBlocks.DataRoot(), "Planets.xml"));


            HashSet<string> subtypes = new HashSet<string>();
            foreach (XmlNode id in document.SelectNodes(
                "/Definitions/CubeBlocks/Definition/Id[TypeId='PlanetGeneratorDefinition']/SubtypeId"))
            {
                subtypes.Add(id.InnerText);
            }

            Assert.Contains("DefaultThermodynamics", subtypes);
            foreach (string world in new[] { "EarthLike", "Alien", "Mars", "Pertam", "Triton", "Europa", "Titan", "Moon" })
            {
                Assert.Contains(world, subtypes);
            }
        }

        [Fact]

        public void TheGeneratorEmitsStrictXml()
        {

            XmlDocument document = new XmlDocument();
            document.LoadXml(PlanetLab.Xml());
        }
    }
}

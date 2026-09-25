using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Thermodynamics.Harness;
using Xunit;
using Xunit.Abstractions;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The two things in this repository that cannot be undone by editing them back.**
    ///
    /// <para>
    /// `R5` and `R4` are both *judgement* on the rules page: nothing checked either, and both are
    /// irreversible in a way a compiler cannot see. Regenerating `modinfo.sbmi` publishes the mod as
    /// a **new workshop item** and every subscriber stays on the old one — the repository would look
    /// correct, the build would be green, and the players would be gone. Restructuring `Models/`
    /// costs a re-export of every block model, because a `.mwm` path is written into the `.sbc` that
    /// names it and neither this repository nor the game can regenerate the other side.
    /// </para>
    ///
    /// <para>
    /// **A pin, not a prohibition.** Both are things that may one day be done deliberately; what
    /// they must not be is done *incidentally*, by a tool that rewrites a file or a tidy-up that
    /// moves a folder. Changing the constants below is the deliberate act, and it is one line in a
    /// diff that a reviewer sees.
    /// </para>
    /// </summary>
    public class ShippedIdentityTests
    {
        private readonly ITestOutputHelper output;


        public ShippedIdentityTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private const string WorkshopId = "2985582372";

        private const string SteamIdOwner = "76561198079985653";

        [Fact]

        public void TheWorkshopIdentityIsTheOneTheModIsPublishedUnder()
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), "Workshop", "modinfo.sbmi");
            Assert.True(File.Exists(path), "modinfo.sbmi is gone, which is the whole failure");

            XDocument info = XDocument.Load(path);


            List<string> ids = new List<string>();
            foreach (XElement element in info.Descendants())
            {
                if (element.Name.LocalName != "Id") continue;
                ids.Add(element.Value.Trim());
            }

            output.WriteLine("workshop ids in modinfo.sbmi: " + string.Join(", ", ids));

            Assert.True(ids.Contains(WorkshopId),
                "modinfo.sbmi does not carry workshop id " + WorkshopId + " — it carries "
                + (ids.Count == 0 ? "none at all" : string.Join(", ", ids))
                + ". Publishing from this folder would create a NEW workshop item and every"
                + " subscriber would stay on the old one. If the mod is genuinely being republished"
                + " under a new id, change the constant in this test and say why in the commit.");

            XElement owner = null;
            foreach (XElement element in info.Descendants())
            {
                if (element.Name.LocalName == "SteamIDOwner") owner = element;
            }

            Assert.NotNull(owner);
            Assert.Equal(SteamIdOwner, owner.Value.Trim());
        }

        [Fact]

        public void TheSecondIdentityFileIsStillThere()
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), "Workshop", "metadata.mod");
            Assert.True(File.Exists(path),
                "metadata.mod is gone; the workshop identity is two files and this is the other one");
        }

        [Fact]

        public void TheModelTreeKeepsItsShape()
        {
            string models = Path.Combine(ShippedBlocks.ContentRoot(), "Models");
            Assert.True(Directory.Exists(models), "Models/ is gone");


            List<string> paths = new List<string>();
            foreach (string file in Directory.GetFiles(models, "*.mwm", SearchOption.AllDirectories))
            {
                paths.Add(file.Substring(ShippedBlocks.ContentRoot().Length)
                    .TrimStart('/', '\\').Replace('\\', '/'));
            }

            paths.Sort(StringComparer.Ordinal);

            Assert.True(paths.Count >= 100,
                "only " + paths.Count + " models were found, so this test is looking in the wrong"
                + " place and would pass on an emptied tree");

            string joined = string.Join("\n", paths.ToArray());
            string digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(joined)))
                    .Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }

            output.WriteLine("{0} models, path-set digest {1}", paths.Count, digest);


            List<string> stray = new List<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (!paths[i].StartsWith("Models/", StringComparison.Ordinal)) stray.Add(paths[i]);
            }

            Assert.True(stray.Count == 0,
                "models outside Models/:\n  " + string.Join("\n  ", stray.ToArray()));

            Assert.Equal(ModelPathDigest, digest);
        }

        private const string ModelPathDigest = "fcaac6b833bce623";
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Every MSBuild file in the repository is a file MSBuild can read.
    ///
    /// <para>
    /// **A project file that does not parse does not fail loudly — it leaves the build.** `C11` puts
    /// the mod project in `tests/Thermodynamics.slnx` so the suite's own build compiles every file
    /// the game compiles, and that is the right check for a *rename*. It is not the check for a
    /// malformed project: `dotnet build Thermodynamics.Tests.csproj` and `dotnet test --no-build`
    /// both succeed while the mod project is unreadable, so the only command that says so is the
    /// one command a session may not have run. Pass 9, Iteration 4 wrote a `--` into an XML comment
    /// in the mod project and shipped it; the mod project did not build for three commits and the
    /// suite passed throughout.
    /// </para>
    ///
    /// <para>
    /// This is the same shape as <see cref="OptimisedBuildTests"/>: a build property is not covered
    /// by the thing it configures, so something has to read the file itself. Parsing is all this
    /// asserts — whether the project *means* the right thing is what the solution build is for —
    /// because parsing is the failure that hides.
    /// </para>
    /// </summary>
    public class ProjectFileTests
    {
        [Fact]
        public void SolutionsAndProjectReferencesStayInsideThisRepository()
        {
            string root = ShippedBlocks.RepoRoot();
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (object[] row in MsBuildFiles())
            {
                string path = Path.Combine(root, (string)row[0]);
                var document = new XmlDocument();
                document.Load(path);
                foreach (XmlNode node in document.SelectNodes("//*[local-name()='ProjectReference']/@Include | /*[local-name()='Solution']//*[local-name()='Project']/@Path"))
                {
                    string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), node.Value.Replace('\\', Path.DirectorySeparatorChar)));
                    Assert.True(target.StartsWith(prefix, StringComparison.Ordinal), path + " builds an external project: " + node.Value);
                }
            }
            foreach (Match project in Regex.Matches(File.ReadAllText(Path.Combine(root, "Thermodynamics.sln")), "\"([^\"]+\\.csproj)\""))
            {
                string target = Path.GetFullPath(Path.Combine(root, project.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
                Assert.True(target.StartsWith(prefix, StringComparison.Ordinal), "Root solution builds an external project: " + target);
            }
        }

        public static IEnumerable<object[]> MsBuildFiles()
        {
            string root = ShippedBlocks.RepoRoot();
            string[] patterns = { "*.csproj", "*.props", "*.targets", "*.slnx"};

            foreach (string pattern in patterns)
            {
                foreach (string path in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                    if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
                    yield return new object[] { relative };
                }
            }
        }

        [Theory]
        [MemberData(nameof(MsBuildFiles))]

        public void EveryMsBuildFileParses(string relative)
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), relative);

            XmlDocument document = new XmlDocument();

            try
            {
                document.Load(path);
            }
            catch (XmlException failure)
            {
                Assert.Fail(relative + " is not readable by MSBuild, so every project that"
                    + " references it silently leaves the build: " + failure.Message);
            }

            Assert.NotNull(document.DocumentElement);
        }

        [Fact]

        public void TheModProjectIsAMemberOfTheTestSolution()
        {
            string solution = File.ReadAllText(
                Path.Combine(ShippedBlocks.RepoRoot(), "tests", "Thermodynamics.slnx"));

            Assert.Contains("../Thermodynamics/Thermodynamics.csproj", solution);
            Assert.True(File.Exists(Path.Combine(ShippedBlocks.RepoRoot(),
                "Thermodynamics", "Thermodynamics.csproj")));
        }
    }
}

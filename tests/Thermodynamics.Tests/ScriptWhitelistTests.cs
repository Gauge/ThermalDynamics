using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The mod's source against the game's script whitelist.
    ///
    /// <para>
    /// **A second opinion on the mod build's.** The mod project compiles the mod's sources against
    /// the installed game's own assemblies, and MDK2's analyzer checks them against its own copy of
    /// the whitelist; the game runs its analyzer over its own list, so a type can pass here and be
    /// refused there. That happened before the analyzer ran in the build: `Units.Watts` took an
    /// `IFormatProvider`, the build was green, and the mod would not load. This reads the list
    /// transcribed from the game. See known-issues.md.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class ScriptWhitelistTests
    {
        [Fact]

        public void NoModSourceNamesAFrameworkTypeTheGameRefuses()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, string> prohibited = ScriptWhitelist.ProhibitedNames();
            Assert.True(prohibited.Count > 100,
                "the framework surface came back with " + prohibited.Count + " prohibited names, "
                + "which is too few to be the real one — netstandard.dll was probably not read");

            List<string> sources = ScriptWhitelist.Sources();
            Assert.True(sources.Count > 50,
                "found " + sources.Count + " mod source files, which is not the mod");


            List<ScriptWhitelist.Finding> findings = new List<ScriptWhitelist.Finding>();
            foreach (string path in sources)
            {
                findings.AddRange(ScriptWhitelist.Scan(Short(path), File.ReadAllText(path), prohibited));
            }

            if (findings.Count == 0) return;


            StringBuilder message = new StringBuilder();
            message.Append(findings.Count)
                .Append(" name(s) the game's script whitelist refuses. The mod project builds them "
                    + "because the assemblies have them; a session will not compile the mod at all. ")
                .Append(ScriptWhitelist.TranscribedFrom).AppendLine();

            for (int i = 0; i < findings.Count && i < 40; i++)
            {
                message.AppendLine("  " + findings[i]);
            }

            Assert.Fail(message.ToString());
        }

        [Fact]

        public void TheCheckFindsTheDeclarationThatBrokeASession()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, string> prohibited = ScriptWhitelist.ProhibitedNames();

            Assert.True(prohibited.ContainsKey("IFormatProvider"),
                "IFormatProvider is not in the prohibited set, so the case this file exists for "
                + "would not be caught");

            const string broken =
                "namespace T { public static class U {\n"

                + "  public static string W(float w, IFormatProvider culture) { return null; }\n"

                + "  public static string X(float w, System.IFormatProvider culture) { return null; }\n"
                + "} }";

            List<ScriptWhitelist.Finding> found = ScriptWhitelist.Scan("broken.cs", broken, prohibited);
            Assert.Equal(2, found.Count);
            Assert.All(found, f => Assert.Equal("IFormatProvider", f.Name));

            const string fixedUp =
                "using System.Globalization;\n"
                + "namespace T { public static class U {\n"

                + "  public static string W(float w, CultureInfo culture) { return null; }\n"
                + "} }";

            Assert.Empty(ScriptWhitelist.Scan("fixed.cs", fixedUp, prohibited));
        }

        [Fact]

        public void TheCheckDoesNotReportMembersThatShareAFrameworkTypesName()
        {
            if (!GameBlocks.IsInstalled) return;

            Dictionary<string, string> prohibited = ScriptWhitelist.ProhibitedNames();

            Assert.True(prohibited.ContainsKey("Uri"), "Uri is not a prohibited framework name");
            Assert.True(prohibited.ContainsKey("Lazy"), "Lazy is not a prohibited framework name");

            const string innocent =
                "namespace T { public class B {\n"
                + "  public int Uri;\n"
                + "  public string Lazy { get { return null; } }\n"

                + "  public void Use(B other) { int s = other.Uri; string c = other.Lazy; }\n"
                + "} }";

            Assert.Empty(ScriptWhitelist.Scan("innocent.cs", innocent, prohibited));
        }

        [Fact]

        public void ANameTheGameAlsoDeclaresIsLeftAlone()
        {
            if (!GameBlocks.IsInstalled) return;

            Assert.Contains("Color", ScriptWhitelist.GameTypeNames());
            Assert.False(ScriptWhitelist.ProhibitedNames().ContainsKey("Color"),
                "Color was reported, which would bury every real finding under it");
        }

        [Fact]

        public void TheTranscriptionSaysWhereItCameFrom()
        {
            Assert.Contains("Bin64", ScriptWhitelist.TranscribedFrom);
            Assert.NotEmpty(ScriptWhitelist.AllowedNamespaces);
            Assert.True(ScriptWhitelist.AllowedTypes.Count > 50);

            Assert.DoesNotContain("System", ScriptWhitelist.AllowedNamespaces);
        }


        private static string Short(string path)
        {
            string root = Harness.ShippedBlocks.RepoRoot();
            return path.StartsWith(root, StringComparison.Ordinal)
                ? path.Substring(root.Length).TrimStart('/', '\\')
                : path;
        }
    }

    /// <summary>
    /// No mod source carries a preprocessor directive the game's script compiler refuses.
    ///
    /// <para>
    /// `#pragma warning` compiles in the mod project and fails the world load, so a suppression
    /// that keeps the local build quiet is a mod that does not load (`C2`). Warnings are scoped in
    /// `.editorconfig` instead.
    /// </para>
    /// </summary>
    public class ScriptPreprocessorTests
    {
        [Fact]
        public void NoModSourceUsesAPragma()
        {
            // The game's script compiler refuses '#pragma warning' outright, so a
            // suppression that keeps the mod build quiet stops a session loading it.
            // Scope warnings in .editorconfig instead.
            List<string> sources = ScriptWhitelist.Sources();
            Assert.True(sources.Count > 50,
                "found " + sources.Count + " mod source files, which is not the mod");

            List<string> findings = new List<string>();
            foreach (string path in sources)
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("#pragma", StringComparison.Ordinal))
                        findings.Add("  " + Short(path) + "(" + (i + 1) + "): " + lines[i].Trim());
                }
            }

            Assert.True(findings.Count == 0,
                findings.Count + " '#pragma' line(s); the game will not compile the mod:\n"
                + string.Join("\n", findings));
        }


        private static string Short(string path)
        {
            string root = Harness.ShippedBlocks.RepoRoot();
            return path.StartsWith(root, StringComparison.Ordinal)
                ? path.Substring(root.Length).TrimStart('/', '\\')
                : path;
        }
    }
}

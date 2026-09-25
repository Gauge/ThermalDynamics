using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// No credential is written into the tree.
    ///
    /// <para>
    /// The rule is `R3`, it was absolute from the day the corpus fetcher was written, and until now
    /// nothing enforced it — the *Checked by* field said "nothing scans the tree". This is the
    /// scan. It is worth having because the failure is silent and permanent: a key committed once
    /// is in the history whether or not the next commit removes it, and **this repository is the
    /// published mod folder**, so anything in it ships.
    /// </para>
    ///
    /// <para>
    /// Shaped patterns rather than entropy. A Steam Web API key is thirty-two hex characters, which
    /// is a shape a file can hold innocently — an MD5, a GUID with its dashes taken out — so the
    /// test looks for a key *beside a word that says it is one*, and separately for the shapes that
    /// cannot be anything else: a private key header, a provider-prefixed token, an assigned
    /// password.
    /// </para>
    /// </summary>
    public class CredentialScanTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static IEnumerable<string> Files()
        {
            string[] extensions = { ".cs", ".xml", ".sbc", ".md", ".py", ".sh", ".json", ".txt",
                ".html", ".csv", ".props", ".sln", ".slnx", ".csproj", ".sbmi", ".mod", ".yml" };

            foreach (string file in Directory.GetFiles(RepoRoot(), "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(RepoRoot().Length)
                    .TrimStart('/', '\\').Replace('\\', '/');

                if (relative.StartsWith("out/", StringComparison.Ordinal)) continue;
                if (relative.StartsWith(".git/", StringComparison.Ordinal)) continue;
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                if (relative.Contains("RichHudFramework/")) continue;

                if (relative == "tests/Thermodynamics.Tests/CredentialScanTests.cs") continue;

                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(extensions, extension) < 0) continue;

                yield return file;
            }
        }

        private class Shape
        {
            public string Name;
            public Regex Pattern;
        }


        private static List<Shape> Shapes()
        {
            return new List<Shape>
            {
                new Shape
                {
                    Name = "a private key block",

                    Pattern = new Regex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----"),
                },
                new Shape
                {
                    Name = "a provider-prefixed token",

                    Pattern = new Regex(@"\b(?:gh[pousr]_[A-Za-z0-9]{16,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9-]{10,}|sk-[A-Za-z0-9]{20,})\b"),
                },
                new Shape
                {
                    Name = "a credential assigned a literal",

                    Pattern = new Regex(
                        @"(?i)\b(?:api[_-]?key|secret|password|passwd|token)\b\s*[:=]\s*[""']([A-Za-z0-9+/=_-]{16,})[""']"),
                },
                new Shape
                {
                    Name = "a Steam Web API key",

                    Pattern = new Regex(@"(?i)steam[^\n]{0,40}\b[0-9A-F]{32}\b"),
                },
            };
        }

        [Fact]

        public void NoCredentialShapedLiteralIsInTheTree()
        {

            List<string> found = new List<string>();

            List<Shape> shapes = Shapes();
            int scanned = 0;

            foreach (string file in Files())
            {
                scanned++;
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (Shape shape in shapes)
                {
                    Match match = shape.Pattern.Match(text);
                    if (!match.Success) continue;

                    found.Add(file.Substring(RepoRoot().Length).TrimStart('/', '\\')
                        .Replace('\\', '/') + ": " + shape.Name
                        + " at offset " + match.Index);
                }
            }

            Assert.True(scanned > 100,
                "only " + scanned + " files were scanned, so this test is looking in the wrong"
                + " place and would pass whatever the tree held");

            found.Sort(StringComparer.Ordinal);
            Assert.True(found.Count == 0,
                "credential-shaped literals in the tree:\n  " + string.Join("\n  ", found.ToArray()));
        }

        [Fact]

        public void TheScanFiresOnKeysAndNotOnTheWordKey()
        {

            List<Shape> shapes = Shapes();

            string[] credentials =
            {
                "-----BEGIN RSA PRIVATE KEY-----",
                "ghp_0123456789abcdefghijABCDEFGHIJ",
                "AKIAIOSFODNN7EXAMPLE",
                "api_key = \"0123456789abcdef0123456789abcdef\"",
                "password: 'hunter2ismuchtooshortbutthisisnot'",
                "steam web api key 0123456789ABCDEF0123456789ABCDEF",
            };

            foreach (string credential in credentials)
            {
                bool caught = false;
                foreach (Shape shape in shapes) caught |= shape.Pattern.IsMatch(credential);
                Assert.True(caught, "the scan missed: " + credential.Substring(0, 12) + "...");
            }

            string[] innocent =
            {
                "Environment.GetEnvironmentVariable(\"STEAM_WEB_API_KEY\")",
                "--key <key>, or set STEAM_WEB_API_KEY.",
                "return text.Replace(secret, \"<key>\");",
                "\"?key=\" + Uri.EscapeDataString(key) +",
                "Get one free at https://steamcommunity.com/dev/apikey",
                "the workshop item 1634759283 is a corvette",
            };

            foreach (string line in innocent)
            {
                foreach (Shape shape in shapes)
                {
                    Assert.False(shape.Pattern.IsMatch(line),
                        shape.Name + " fired on legitimate text: " + line);
                }
            }
        }
    }
}

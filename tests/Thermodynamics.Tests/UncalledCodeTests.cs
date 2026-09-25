using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The highest-yield defect class here is a thing that exists, is described accurately, and is
    /// called by nothing** (`D2`), and until now nothing looked for it by machine at all.
    ///
    /// <para>
    /// rules.md said so in as many words: *nothing yet scans the test project for an uncalled
    /// `internal static` invariant, which is how the four above survived*. The compiler will not do
    /// it — an unused private method is not a warning, unlike an unused private field — so a lab
    /// helper written for a measurement that was then taken another way stays in the tree, keeps
    /// compiling, keeps being maintained, and reads to the next person as something the suite
    /// relies on. This found `Scenarios.OpenSides` on its first run.
    /// </para>
    ///
    /// <para>
    /// It covers the shipped code as well, where `D2`'s evidence mostly came from, and finds
    /// nothing there today — which is a result rather than an omission: it says the mod's own
    /// private helpers are all reached. The vendored framework is excluded because `R6` forbids
    /// editing it, and it carries an uncalled `UnpauseClients` that nobody here may delete.
    /// </para>
    ///
    /// <para>
    /// **Two limits, stated rather than discovered.** A name is counted across every identifier in
    /// `Data/` and `tests/`, so an *overload* of a name something else calls is invisible here; and
    /// a helper reached only through a string literal — an xUnit `MemberData("Name")` — would be
    /// reported. Both are deliberate: the second is a reason to write `nameof`, and the first is the
    /// same false negative the rules page already names for a misspelled declaration.
    /// </para>
    /// </summary>
    [Trait("speed", "slow")]
    public class UncalledCodeTests
    {

        private static List<string> SourceFiles()
        {

            List<string> files = new List<string>();
            string root = ShippedBlocks.RepoRoot();

            foreach (string folder in new[] { "Thermodynamics", "tests" })
            {
                foreach (string path in Directory.GetFiles(Path.Combine(root, folder), "*.cs",
                    SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                    if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;
                    files.Add(path);
                }
            }

            return files;
        }


        private static bool IsVendored(string relative)
        {
            return relative.Contains("/RichHudFramework/");
        }

        [Fact]

        public void NoPrivateHelperInTheTreeIsCalledByNothing()
        {
            string root = ShippedBlocks.RepoRoot();

            List<string> files = SourceFiles();

            Dictionary<string, int> mentions = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, SyntaxNode> roots = new Dictionary<string, SyntaxNode>(StringComparer.Ordinal);

            foreach (string path in files)
            {
                SyntaxNode tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
                roots[path] = tree;

                foreach (SyntaxToken token in tree.DescendantTokens())
                {
                    if (!token.IsKind(SyntaxKind.IdentifierToken)) continue;

                    int seen;
                    mentions.TryGetValue(token.ValueText, out seen);
                    mentions[token.ValueText] = seen + 1;
                }
            }


            List<string> uncalled = new List<string>();

            foreach (string path in files)
            {
                string relative = path.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                if (IsVendored(relative)) continue;

                foreach (MethodDeclarationSyntax method in roots[path].DescendantNodes()
                    .OfType<MethodDeclarationSyntax>())
                {
                    if (!IsPrivateOrInternalStatic(method)) continue;

                    string name = method.Identifier.ValueText;
                    if (mentions[name] > 1) continue;

                    uncalled.Add(relative + ":"
                        + (method.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
                        + " " + name);
                }
            }

            Assert.True(uncalled.Count == 0,
                "these are declared and mentioned nowhere else, so nothing calls"

                + " them — delete them, or give them the caller they were written for (`D2`): "
                + string.Join(", ", uncalled.ToArray()));
        }


        private static bool IsPrivateOrInternalStatic(MethodDeclarationSyntax method)
        {
            bool isStatic = false;
            bool isReachableFromOutside = false;
            bool isElsewhere = false;

            foreach (SyntaxToken modifier in method.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.StaticKeyword)) isStatic = true;
                if (modifier.IsKind(SyntaxKind.PublicKeyword)
                    || modifier.IsKind(SyntaxKind.ProtectedKeyword)) isReachableFromOutside = true;
                if (modifier.IsKind(SyntaxKind.PartialKeyword)
                    || modifier.IsKind(SyntaxKind.ExternKeyword)) isElsewhere = true;
            }

            return isStatic && !isReachableFromOutside && !isElsewhere;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The game's script whitelist, and the mod's source read against it.
    ///
    /// <para>
    /// **Compiling is not this check.** The mod project compiles the mod's sources against the
    /// installed game's own assemblies, which catches a member that does not exist and cannot catch
    /// a member the game *refuses*: Space Engineers compiles a mod through a Roslyn analyzer over a
    /// positive whitelist, so a type can resolve on this machine and be prohibited in a session. It
    /// cost one — `Units.Watts` took an `IFormatProvider`, and the only feedback was a red wall of
    /// text on the loading screen. MDK2's analyzer now runs in the mod build as well, from its own
    /// copy of the list; this one is transcribed from the game, so the two can disagree (`P4`).
    /// </para>
    ///
    /// <para>
    /// **The list below is transcribed from the game rather than guessed at**, from
    /// `SpaceEngineers.Game.MySpaceGameDefaultIlChecker`, which builds it in three methods of
    /// `AllowNamespaceOfTypes` / `AllowTypes` / `AllowMembers` calls, and
    /// `VRage.Scripting.MyScriptWhitelist`, which matches against it. The shape that catches people
    /// out is that **`System` is not an allowed namespace** — only the types named one by one are.
    /// Read it again with `ilspycmd -t SpaceEngineers.Game.MySpaceGameDefaultIlChecker` when the
    /// game updates; the transcription date is below.
    /// </para>
    ///
    /// <para>
    /// **This is narrower than the game's rule, deliberately, in the direction that cannot cry
    /// wolf.** It judges the framework surface only — the types a mod reaches through `netstandard`
    /// — because that is where the whitelist is a list of individual types and where a mod author
    /// has no way of knowing. The game half is allowed by whole namespaces, is already exercised by
    /// every session the mod has ever loaded in, and would need the game's own resolution to judge.
    /// What is left is exactly the class of mistake that produced this file.
    /// </para>
    ///
    /// <para>
    /// **Two more names are dropped for the same reason, and both cost sensitivity.** A name the
    /// game also declares — `Color` is `VRageMath.Color` far more often than `System.Drawing.Color`
    /// — and a name the mod declares itself. Without resolving the way a compiler does there is no
    /// telling which was meant, and a check that reported three hundred `Color`s would be switched
    /// off within a week. The cost is that a genuinely prohibited framework type whose simple name
    /// collides with a game type is invisible here; the benefit is that everything this reports is
    /// real.
    /// </para>
    /// </summary>
    public static class ScriptWhitelist
    {
        public const string TranscribedFrom = "Space Engineers, Bin64 of 2026-08-14, read 2026-08-23";

        public static readonly HashSet<string> AllowedNamespaces = new HashSet<string>
        {
            "System.Collections",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Globalization",

            "System.Linq",
            "System.Collections.Concurrent",

            "System.Timers",

            "System.Collections.Immutable",
        };

        public static readonly HashSet<string> AllowedTypes = new HashSet<string>
        {
            "Object", "IDisposable", "String", "StringComparison", "Math", "Enum",
            "Int32", "Int16", "Int64", "UInt32", "UInt16", "UInt64",
            "Double", "Single", "Boolean", "Char", "Byte", "SByte", "Decimal",
            "DateTime", "TimeSpan", "Array",

            "XmlElementAttribute", "XmlAttributeAttribute", "XmlArrayAttribute",
            "XmlArrayItemAttribute", "XmlAnyAttributeAttribute", "XmlAnyElementAttribute",
            "XmlAnyElementAttributes", "XmlArrayItemAttributes", "XmlAttributeEventArgs",
            "XmlAttributeOverrides", "XmlAttributes", "XmlChoiceIdentifierAttribute",
            "XmlElementAttributes", "XmlElementEventArgs", "XmlEnumAttribute",
            "XmlIgnoreAttribute", "XmlIncludeAttribute", "XmlRootAttribute", "XmlTextAttribute",
            "XmlTypeAttribute",

            "RuntimeHelpers", "BinaryReader", "BinaryWriter",
            "NullReferenceException", "ArgumentException", "ArgumentNullException",
            "InvalidOperationException", "FormatException", "Exception", "DivideByZeroException",
            "InvalidCastException", "FileNotFoundException", "NotSupportedException",
            "Nullable", "StringComparer", "IEquatable", "IComparable", "BitConverter",
            "FlagsAttribute", "Path", "Random", "Convert", "StringSplitOptions", "DateTimeKind",
            "MidpointRounding", "EventArgs", "Buffer",
            "INotifyPropertyChanging", "PropertyChangingEventHandler", "PropertyChangingEventArgs",
            "INotifyPropertyChanged", "PropertyChangedEventHandler", "PropertyChangedEventArgs",

            "Action", "Func",

            "Stream", "TextWriter", "TextReader",

            "TraceEventType", "AssemblyProductAttribute", "AssemblyDescriptionAttribute",
            "AssemblyConfigurationAttribute", "AssemblyCompanyAttribute", "AssemblyCultureAttribute",
            "AssemblyVersionAttribute", "AssemblyFileVersionAttribute", "AssemblyCopyrightAttribute",
            "AssemblyTrademarkAttribute", "AssemblyTitleAttribute", "ComVisibleAttribute",
            "DefaultValueAttribute", "SerializableAttribute", "GuidAttribute",
            "StructLayoutAttribute", "LayoutKind", "Guid",

            "Monitor", "AutoResetEvent", "ManualResetEvent", "Interlocked",
            "Stopwatch", "ConditionalAttribute", "Version", "ObsoleteAttribute",

            "MemberInfo", "Type", "ValueType", "Environment", "Delegate",

            "Void", "IntPtr", "UIntPtr",
        };

        public struct Finding
        {
            public string File;
            public int Line;
            public string Name;

            public string Namespaces;


            public override string ToString()
            {
                return File + ":" + Line + "  " + Name + "  (" + Namespaces + ")";
            }
        }


        public static string SourceRoot()
        {
            string root = Harness.ShippedBlocks.RepoRoot();
            if (root == null) return null;

            string candidate = Path.Combine(root, "Thermodynamics");
            return Directory.Exists(candidate) ? candidate : null;
        }


        public static string FrameworkFacade()
        {
            string content = Harness.GameBlocks.ContentPath();
            if (content == null) return null;

            DirectoryInfo install = Directory.GetParent(content);
            if (install != null) install = install.Parent;
            if (install == null) return null;

            string facade = Path.Combine(install.FullName, "Bin64", "netstandard.dll");
            return File.Exists(facade) ? facade : null;
        }


        public static Dictionary<string, string> ProhibitedNames()
        {
            Dictionary<string, string> prohibited = new Dictionary<string, string>();
            Dictionary<string, List<string>> byName = new Dictionary<string, List<string>>();


            string facade = FrameworkFacade();
            if (facade == null) return prohibited;

            using (PEReader reader = new PEReader(File.OpenRead(facade)))
            {
                if (!reader.HasMetadata) return prohibited;
                MetadataReader metadata = reader.GetMetadataReader();

                foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
                {
                    TypeDefinition definition = metadata.GetTypeDefinition(handle);
                    TypeAttributes visibility = definition.Attributes & TypeAttributes.VisibilityMask;
                    if (visibility != TypeAttributes.Public) continue;

                    Record(byName, metadata.GetString(definition.Namespace),
                        metadata.GetString(definition.Name));
                }

                foreach (ExportedTypeHandle handle in metadata.ExportedTypes)
                {
                    ExportedType exported = metadata.GetExportedType(handle);
                    Record(byName, metadata.GetString(exported.Namespace),
                        metadata.GetString(exported.Name));
                }
            }


            HashSet<string> shadowed = GameTypeNames();

            HashSet<string> declared = DeclaredByTheMod();

            foreach (KeyValuePair<string, List<string>> entry in byName)
            {
                if (AllowedTypes.Contains(entry.Key)) continue;
                if (entry.Value.Any(AllowedNamespaces.Contains)) continue;
                if (shadowed.Contains(entry.Key)) continue;
                if (declared.Contains(entry.Key)) continue;

                prohibited[entry.Key] = string.Join(", ", entry.Value.Distinct().OrderBy(x => x));
            }

            return prohibited;
        }


        public static HashSet<string> GameTypeNames()
        {
            if (_gameTypeNames != null) return _gameTypeNames;


            HashSet<string> names = new HashSet<string>();

            string facade = FrameworkFacade();

            if (facade != null)
            {
                string bin = Path.GetDirectoryName(facade);

                foreach (string file in Directory.GetFiles(bin, "*.dll"))
                {
                    string assembly = Path.GetFileName(file);
                    if (!assembly.StartsWith("Sandbox.", StringComparison.Ordinal)
                        && !assembly.StartsWith("VRage", StringComparison.Ordinal)
                        && !assembly.StartsWith("SpaceEngineers", StringComparison.Ordinal)
                        && !assembly.StartsWith("ProtoBuf", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    CollectTypeNames(file, names);
                }
            }

            _gameTypeNames = names;
            return names;
        }

        private static HashSet<string> _gameTypeNames;


        private static void CollectTypeNames(string file, HashSet<string> names)
        {
            try
            {
                using (PEReader reader = new PEReader(File.OpenRead(file)))
                {
                    if (!reader.HasMetadata) return;
                    MetadataReader metadata = reader.GetMetadataReader();

                    foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
                    {
                        TypeDefinition definition = metadata.GetTypeDefinition(handle);
                        string name = metadata.GetString(definition.Name);

                        int generic = name.IndexOf('`');
                        if (generic >= 0) name = name.Substring(0, generic);
                        names.Add(name);
                    }
                }
            }
            catch (BadImageFormatException)
            {
            }
        }


        public static HashSet<string> DeclaredByTheMod()
        {

            HashSet<string> declared = new HashSet<string>();

            foreach (string path in Sources())
            {
                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();

                foreach (SyntaxNode node in root.DescendantNodes())
                {
                    BaseTypeDeclarationSyntax type = node as BaseTypeDeclarationSyntax;
                    if (type != null)
                    {
                        declared.Add(type.Identifier.ValueText);
                        continue;
                    }

                    DelegateDeclarationSyntax handler = node as DelegateDeclarationSyntax;
                    if (handler != null) declared.Add(handler.Identifier.ValueText);
                }
            }

            return declared;
        }


        private static void Record(Dictionary<string, List<string>> byName, string space, string name)
        {
            if (space.Length == 0 || !(space == "System" || space.StartsWith("System."))) return;

            int generic = name.IndexOf('`');
            if (generic >= 0) name = name.Substring(0, generic);

            List<string> spaces;
            if (!byName.TryGetValue(name, out spaces))
            {

                spaces = new List<string>();
                byName[name] = spaces;
            }

            spaces.Add(space);
        }


        public static List<string> Sources()
        {

            string root = SourceRoot();
            if (root == null) return new List<string>();

            return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(x => !IsBuildOutput(x.Substring(root.Length)))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }


        private static bool IsBuildOutput(string relative)
        {
            string path = relative.Replace('\\', '/') + "/";
            return path.Contains("/bin/") || path.Contains("/obj/");
        }


        public static List<Finding> Scan(string path, string text, Dictionary<string, string> prohibited)
        {

            List<Finding> findings = new List<Finding>();

            SyntaxNode root = CSharpSyntaxTree.ParseText(text).GetRoot();

            List<TypeSyntax> types = new List<TypeSyntax>();
            CollectTypes(root, types);

            foreach (TypeSyntax type in types)
            {
                foreach (SimpleNameSyntax name in Names(type))
                {
                    string identifier = name.Identifier.ValueText;

                    string spaces;
                    if (!prohibited.TryGetValue(identifier, out spaces)) continue;

                    if (AllowedTypes.Contains(identifier + "Attribute")) continue;

                    if (name.Parent is QualifiedNameSyntax
                        && ((QualifiedNameSyntax)name.Parent).Right == name
                        && !IsFrameworkQualified((QualifiedNameSyntax)name.Parent))
                    {
                        continue;
                    }


                    Finding finding = new Finding();
                    finding.File = path;
                    finding.Line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    finding.Name = identifier;
                    finding.Namespaces = spaces;
                    findings.Add(finding);
                }
            }

            return findings;
        }


        private static bool IsFrameworkQualified(QualifiedNameSyntax qualified)
        {
            NameSyntax left = qualified.Left;
            while (left is QualifiedNameSyntax) left = ((QualifiedNameSyntax)left).Left;

            SimpleNameSyntax simple = left as SimpleNameSyntax;
            return simple != null && simple.Identifier.ValueText == "System";
        }


        private static void CollectTypes(SyntaxNode node, List<TypeSyntax> types)
        {
            foreach (SyntaxNode child in node.DescendantNodes())
            {
                if (child is ParameterSyntax) Add(((ParameterSyntax)child).Type, types);
                else if (child is VariableDeclarationSyntax) Add(((VariableDeclarationSyntax)child).Type, types);
                else if (child is MethodDeclarationSyntax) Add(((MethodDeclarationSyntax)child).ReturnType, types);
                else if (child is PropertyDeclarationSyntax) Add(((PropertyDeclarationSyntax)child).Type, types);
                else if (child is IndexerDeclarationSyntax) Add(((IndexerDeclarationSyntax)child).Type, types);
                else if (child is DelegateDeclarationSyntax) Add(((DelegateDeclarationSyntax)child).ReturnType, types);
                else if (child is OperatorDeclarationSyntax) Add(((OperatorDeclarationSyntax)child).ReturnType, types);
                else if (child is ConversionOperatorDeclarationSyntax) Add(((ConversionOperatorDeclarationSyntax)child).Type, types);
                else if (child is ObjectCreationExpressionSyntax) Add(((ObjectCreationExpressionSyntax)child).Type, types);
                else if (child is CastExpressionSyntax) Add(((CastExpressionSyntax)child).Type, types);
                else if (child is TypeOfExpressionSyntax) Add(((TypeOfExpressionSyntax)child).Type, types);
                else if (child is DefaultExpressionSyntax) Add(((DefaultExpressionSyntax)child).Type, types);
                else if (child is SizeOfExpressionSyntax) Add(((SizeOfExpressionSyntax)child).Type, types);
                else if (child is CatchDeclarationSyntax) Add(((CatchDeclarationSyntax)child).Type, types);
                else if (child is TypeConstraintSyntax) Add(((TypeConstraintSyntax)child).Type, types);
                else if (child is SimpleBaseTypeSyntax) Add(((SimpleBaseTypeSyntax)child).Type, types);

                else if (child is TypeArgumentListSyntax)
                {
                    foreach (TypeSyntax argument in ((TypeArgumentListSyntax)child).Arguments)
                    {
                        Add(argument, types);
                    }
                }

                else if (child is AttributeSyntax)
                {
                    Add(((AttributeSyntax)child).Name, types);
                }

                else if (child is BinaryExpressionSyntax)
                {
                    BinaryExpressionSyntax binary = (BinaryExpressionSyntax)child;
                    if (binary.IsKind(SyntaxKind.AsExpression) || binary.IsKind(SyntaxKind.IsExpression))
                    {
                        Add(binary.Right as TypeSyntax, types);
                    }
                }
            }
        }


        private static void Add(TypeSyntax type, List<TypeSyntax> types)
        {
            if (type != null) types.Add(type);
        }


        private static IEnumerable<SimpleNameSyntax> Names(TypeSyntax type)
        {

            List<SimpleNameSyntax> names = new List<SimpleNameSyntax>();
            Walk(type, names);
            return names;
        }


        private static void Walk(SyntaxNode node, List<SimpleNameSyntax> names)
        {
            if (node == null) return;

            if (node is PredefinedTypeSyntax) return;

            if (node is SimpleNameSyntax)
            {
                names.Add((SimpleNameSyntax)node);

                GenericNameSyntax generic = node as GenericNameSyntax;
                if (generic != null)
                {
                    foreach (TypeSyntax argument in generic.TypeArgumentList.Arguments)
                    {
                        Walk(argument, names);
                    }
                }

                return;
            }

            foreach (SyntaxNode child in node.ChildNodes()) Walk(child, names);
        }
    }
}

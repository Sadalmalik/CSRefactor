using CSR.Core.Arch;
using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Arch
{
    public class SolutionChecksTests
    {
        private const string Builder = """
            public class Builder
            {
                public bool HasAnchors { get; }

                private bool m_IsReady;

                public bool IsWall;

                public const bool IsDebug = false;

                public void Build()
                {
                    if (HasAnchors && m_IsReady)
                        return;
                }
            }
            """;

        private static readonly ProjectOptions Project = new();

        internal static List<Finding> Analyze(IReadOnlyList<Compilation> compilations, string check,
            Action<CheckSettings>? configure = null)
        {
            var configuration = CheckConfiguration.Create(ArchCatalog.Find(check)!, Severity.Hint, "R1");
            configure?.Invoke(configuration.Settings);
            var options = new CheckOptions();
            options.Checks.Add(configuration);
            return ArchSolutionAnalyzer.Analyze(compilations, options, Project, path => path, CancellationToken.None);
        }

        // Компиляция из пар "путь", "код"
        internal static CSharpCompilation Compile(string name, params string[] files)
            => Compile(name, Array.Empty<MetadataReference>(), files);

        internal static CSharpCompilation Compile(string name, IEnumerable<MetadataReference> extra,
            params string[] files)
        {
            var trees = new List<SyntaxTree>();
            for (int i = 0; i < files.Length; i += 2)
                trees.Add(CSharpSyntaxTree.ParseText(files[i + 1], Project.ParseOptions, files[i]));

            var references = extra.Append(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            return CSharpCompilation.Create(name, trees, references, options);
        }

        private static string User(string name, string condition)
        {
            return $$"""
                public class {{name}}
                {
                    public int Run(Builder builder)
                    {
                        {{condition}}
                    }
                }
                """;
        }

        [Test]
        public void VariantFlag_BranchesInFiles_Found()
        {
            var compilation = Compile("Game",
                "Builder.cs", Builder,
                "Path.cs", User("Path", "if (!builder.HasAnchors) return 0; return 1;"),
                "Agent.cs", User("Agent", "return builder?.HasAnchors == true ? 1 : builder.HasAnchors ? 2 : 0;"),
                "Zone.cs", User("Zone", "while (builder.HasAnchors && builder.IsWall) { } return 0;"),
                "Wall.cs", User("Wall", "if (builder.IsWall || builder.IsWall) return 1; return 0;"));
            var finding = Analyze([compilation], "variant-flag").Single();
            Assert.That(finding.File, Is.EqualTo("Builder.cs"));
            Assert.That(finding.Line, Is.EqualTo(3));
            const string message = "флаг 'Builder.HasAnchors' ветвит код других типов: мест 3 в 3 файлах: "
                + "Agent.cs, Path.cs, Zone.cs";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [Test]
        public void VariantFlag_NotBranchOrOwnType_NoFinding()
        {
            const string use = "public partial class Agent { void Use(bool b) { } }";
            var compilation = Compile("Game",
                "Builder.cs", Builder,
                "Path.cs", User("Path", "var flag = builder.HasAnchors; return flag ? 1 : 0;"),
                "Agent.cs", User("Agent", "Use(builder.HasAnchors); return 0;").Replace("public class", "public partial class"),
                "Use.cs", use,
                "Zone.cs", User("Zone", "return builder.HasAnchors.GetHashCode();"),
                "Debug.cs", User("Debug", "if (Builder.IsDebug) return 1; return 0;"));
            Assert.That(Analyze([compilation], "variant-flag", settings => settings.Set("minFiles", 1)), Is.Empty);
        }

        [Test]
        public void VariantFlag_AcrossProjects_CountedOnce()
        {
            const string branch = "if (builder.IsWall) return 1; return 0;";
            var game = Compile("Game", "Builder.cs", Builder, "Path.cs", User("Path", branch));
            var tools = Compile("Tools", [game.ToMetadataReference()],
                "Tool.cs", User("Tool", branch), "Map.cs", User("Map", branch));

            var finding = Analyze([game, tools], "variant-flag").Single();
            Assert.That(finding.Message, Does.Contain("'Builder.IsWall'").And.Contain("мест 3 в 3 файлах"));
        }

        [Test]
        public void VariantFlag_Options_NamesAndThresholds()
        {
            var compilation = Compile("Game",
                "Builder.cs", Builder,
                "Path.cs", User("Path", "if (builder.IsWall) return 1; return 0;"),
                "Agent.cs", User("Agent", "if (builder.HasAnchors) return 1; return 0;"));
            Assert.That(Analyze([compilation], "variant-flag"), Is.Empty);

            var findings = Analyze([compilation], "variant-flag", settings =>
            {
                settings.Set("minFiles", 1);
                settings.Set("ignoreNames", new[] { "is*" });
            });
            Assert.That(findings.Single().Message, Does.Contain("'Builder.HasAnchors'"));
            var excluded = Analyze([compilation], "variant-flag", settings =>
            {
                settings.Set("minFiles", 1);
                settings.Set("excludePaths", new[] { "Builder.cs" });
            });
            Assert.That(excluded, Is.Empty);
        }

        [Test]
        public void SolutionAnalyzer_FileChecksOnTreesOnce()
        {
            const string source = "public class Sample { [System.Obsolete] public void Old() { } }";
            var game = Compile("Game", "Sample.cs", source);
            var copy = Compile("Copy", "Sample.cs", source);
            var findings = Analyze([game, copy], "obsolete-reason");
            Assert.That(findings.Single().File, Is.EqualTo("Sample.cs"));
        }

        [Test]
        public void FileMode_SolutionCheckSilent()
            => Assert.That(ArchTestCode.Run("variant-flag", Builder), Is.Empty);
    }
}

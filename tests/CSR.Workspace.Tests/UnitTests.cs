using CSR.Reports.Config;
using CSR.Workspace.Loading;
using CSR.Workspace.Rename;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Tests
{
    // Части без загрузки решения: запрос символа, дифф, конфиг, файл решения
    public class UnitTests
    {
#region SymbolQuery

        [Test]
        public void Query_Name()
        {
            var query = SymbolQuery.Parse("Sample.Items.Inventory.Add");

            Assert.That(query.IsPosition, Is.False);
            Assert.That(query.Name, Is.EqualTo("Sample.Items.Inventory.Add"));
            Assert.That(query.LastSegment, Is.EqualTo("Add"));
            Assert.That(query.Parameters, Is.Null);
        }

        [Test]
        public void Query_NameWithParameters()
        {
            var query = SymbolQuery.Parse("global::Sample.Items.Inventory.Add(Item, int)");

            Assert.That(query.Name, Is.EqualTo("Sample.Items.Inventory.Add"));
            Assert.That(query.Parameters, Is.EqualTo(new[] { "Item", "int" }));
            Assert.That(SymbolQuery.Parse("A.B()").Parameters, Is.Empty);
        }

        [Test]
        public void Query_Position()
        {
            var query = SymbolQuery.Parse(@"C:\code\Game\Player.cs:12:17");

            Assert.That(query.IsPosition, Is.True);
            Assert.That(query.File, Is.EqualTo(@"C:\code\Game\Player.cs"));
            Assert.That(query.Line, Is.EqualTo(12));
            Assert.That(query.Column, Is.EqualTo(17));
            Assert.That(SymbolQuery.Parse("Game/Player.cs:3").Column, Is.Null);
        }

        [Test]
        public void Query_Invalid()
        {
            Assert.Throws<SymbolException>(() => SymbolQuery.Parse("Player.cs:x"));
            Assert.Throws<SymbolException>(() => SymbolQuery.Parse("A..B<int>"));
        }

#endregion


#region TextDiff

        [Test]
        public void Diff_ChangeInLine()
        {
            var before = SourceText.From("a\nb\nc\nd\ne\nf\ng\nh\n");
            var change = new TextChange(new TextSpan(8, 1), "X");

            var diff = TextDiff.Build("f.cs", before, before.WithChanges(change), [change]);

            Assert.That(diff, Is.EqualTo("--- a/f.cs\n+++ b/f.cs\n@@ -2,7 +2,7 @@\n b\n c\n d\n-e\n+X\n f\n g\n h\n"));
        }

        [Test]
        public void Diff_InsertedLine()
        {
            var before = SourceText.From("a\nb\nc\n");
            var change = new TextChange(new TextSpan(2, 0), "new\n");

            var diff = TextDiff.Build("f.cs", before, before.WithChanges(change), [change]);

            Assert.That(diff, Is.EqualTo("--- a/f.cs\n+++ b/f.cs\n@@ -1,4 +1,5 @@\n a\n-b\n+new\n+b\n c\n \n"));
        }

        [Test]
        public void Diff_DistantChangesGiveTwoHunks()
        {
            var lines = string.Concat(Enumerable.Range(1, 20).Select(index => $"line{index}\n"));
            var before = SourceText.From(lines);
            var first = new TextChange(new TextSpan(lines.IndexOf("line2\n", StringComparison.Ordinal), 5), "two");
            var start = lines.IndexOf("line18\n", StringComparison.Ordinal);
            var second = new TextChange(new TextSpan(start, 6), "eighteen");

            var diff = TextDiff.Build("f.cs", before, before.WithChanges(first, second), [first, second]);

            Assert.That(diff, Does.Contain("@@ -1,5 +1,5 @@"));
            Assert.That(diff, Does.Contain("@@ -15,7 +15,7 @@"));
            Assert.That(diff, Does.Contain("-line18\n+eighteen\n"));
        }

        [Test]
        public void Diff_NoChanges()
            => Assert.That(TextDiff.Build("f.cs", SourceText.From("a"), SourceText.From("a"), []), Is.Empty);

#endregion


#region RefactorConfig

        [Test]
        public void Config_Parse()
        {
            var options = RefactorConfig.Parse("""
                {
                  // проектные атрибуты
                  "serialization": { "attributes": ["Game.Saved"] },
                  "strings": {
                    "extensions": [".lua"], "roots": ["Assets"], "exclude": ["Cache"],
                    "maxFileSizeKb": 10, "maxMatches": 5, "unityExtensions": [".unity"],
                  },
                }
                """);

            Assert.That(options.SerializationAttributes, Is.EqualTo(new[] { "Game.Saved" }));
            Assert.That(options.StringExtensions, Is.EqualTo(new[] { ".lua" }));
            Assert.That(options.UnityExtensions, Is.EqualTo(new[] { ".unity" }));
            Assert.That(options.StringRoots, Is.EqualTo(new[] { "Assets" }));
            Assert.That(options.StringExclude, Is.EqualTo(new[] { "Cache" }));
            Assert.That(options.MaxFileSizeKb, Is.EqualTo(10));
            Assert.That(options.MaxStringMatches, Is.EqualTo(5));
        }

        [Test]
        public void Config_UnknownKey()
        {
            Assert.Throws<ConfigException>(() => RefactorConfig.Parse("""{ "rename": {} }"""));
            Assert.Throws<ConfigException>(() => RefactorConfig.Parse("""{ "strings": { "depth": 1 } }"""));
            var wrongType = """{ "serialization": { "attributes": [1] } }""";
            Assert.Throws<ConfigException>(() => RefactorConfig.Parse(wrongType));
        }

#endregion


#region SolutionFile

        [Test]
        public void Solution_ProjectsAndShadow()
        {
            using var folder = new TempFolder();
            var solution = folder.Write("App.sln", """
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App.csproj", "{1}"
                EndProject
                Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Folder", "Folder", "{2}"
                EndProject
                """);
            folder.Write("src/App.csproj", "<Project ToolsVersion=\"4.0\">\n</Project>\n");

            var projects = SolutionFile.ReadProjects(solution);
            var shadow = SolutionFile.WriteShadow(solution, Path.Combine(folder.Path, "cache"));

            Assert.That(projects, Is.EqualTo(new[] { Path.Combine(folder.Path, "src", "App.csproj") }));
            Assert.That(SolutionFile.IsLegacyProject(projects[0]), Is.True);
            var absolute = Path.Combine(folder.Path, "src", "App.csproj");
            Assert.That(File.ReadAllText(shadow), Does.Contain($"\"{absolute}\""));
            Assert.That(File.ReadAllText(shadow), Does.Contain("\"Folder\", \"Folder\""));
            Assert.That(SolutionFile.ReadProjects(shadow), Is.EqualTo(projects));
        }

        [Test]
        public void Solution_Slnx()
        {
            using var folder = new TempFolder();
            var solution = folder.Write("App.slnx", "<Solution>\n  <Project Path=\"src/App.csproj\" />\n</Solution>\n");
            folder.Write("src/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n");

            var shadow = SolutionFile.WriteShadow(solution, Path.Combine(folder.Path, "cache"));

            var expected = Path.Combine(folder.Path, "src", "App.csproj");
            Assert.That(SolutionFile.ReadProjects(solution), Is.EqualTo(new[] { expected }));
            Assert.That(SolutionFile.IsLegacyProject(expected), Is.False);
            Assert.That(SolutionFile.ReadProjects(shadow), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void CacheKey_IgnoresCase()
        {
            Assert.That(CachePaths.GetKey(@"C:\Code\App.sln"), Is.EqualTo(CachePaths.GetKey(@"c:\code\app.sln")));
            Assert.That(CachePaths.GetKey(@"C:\Code\App.sln"), Is.Not.EqualTo(CachePaths.GetKey(@"C:\Code\Other.sln")));
        }

#endregion
    }

    public class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csr-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relative, string text)
        {
            var file = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
            return file;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
        }
    }
}

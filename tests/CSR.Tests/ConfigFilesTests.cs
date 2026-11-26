using System.Text.Json;
using CSR.Commands;

namespace CSR.Tests
{
    // Поиск конфигов: явные --config, слой по умолчанию у csr.exe, проектный .csr до корня репозитория
    public class ConfigFilesTests
    {
        private const string Dirty = """
            public class Sample
            {
                private bool Check(object value)
                    => value is null;
            }
            """;

        [Test]
        public void Layers_DefaultThenProject()
        {
            using var folder = CreateRepository();
            var defaults = folder.Write("install/config/style.jsonc", "{}");
            var project = folder.Write("repo/.csr/style.jsonc", "{}");
            var start = Directory.CreateDirectory(Path.Combine(folder.Path, "repo", "src", "Code")).FullName;

            var layers = ConfigFileTools.GetLayers([], "style.jsonc", start, Path.GetDirectoryName(defaults)!);
            Assert.That(layers, Is.EqualTo(new[] { defaults, project }));

            var explicitLayers = ConfigFileTools.GetLayers(
                ["my.jsonc"],
                "style.jsonc",
                start,
                Path.GetDirectoryName(defaults)!);
            Assert.That(explicitLayers, Is.EqualTo(new[] { "my.jsonc" }), "явный --config отменяет поиск");
        }

        [Test]
        public void Single_ProjectOverDefault()
        {
            using var folder = CreateRepository();
            var defaults = folder.Write("install/config/project.jsonc", "{}");
            var directory = Path.GetDirectoryName(defaults)!;
            var start = Directory.CreateDirectory(Path.Combine(folder.Path, "repo", "src")).FullName;
            Assert.That(ConfigFileTools.Find(null, "project.jsonc", start, directory), Is.EqualTo(defaults));

            var project = folder.Write("repo/.csr/project.jsonc", "{}");
            Assert.That(ConfigFileTools.Find(null, "project.jsonc", start, directory), Is.EqualTo(project));
            Assert.That(ConfigFileTools.Find("my.jsonc", "project.jsonc", start, directory), Is.EqualTo("my.jsonc"));
        }

        [Test]
        public void Project_SearchStopsAtRepositoryRoot()
        {
            using var folder = CreateRepository();
            folder.Write(".csr/style.jsonc", "{}");
            var start = Directory.CreateDirectory(Path.Combine(folder.Path, "repo", "src")).FullName;
            var empty = Path.Combine(folder.Path, "none");
            Assert.That(ConfigFileTools.GetLayers([], "style.jsonc", start, empty), Is.Empty, ".csr выше .git не берется");
        }

        [Test]
        public void Style_WithoutConfig_ProjectLayer()
        {
            using var folder = CreateRepository();
            folder.Write(
                "repo/.csr/style.jsonc",
                """{ "checks": { "null-check": { "severity": "error", "prefer": "equality" } } }""");
            var file = folder.Write("repo/src/Sample.cs", Dirty);
            var result = Cli.Run("style", file);

            Assert.That(result.ExitCode, Is.EqualTo(1), result.Error);
            using var document = JsonDocument.Parse(result.Output);
            Assert.That(document.RootElement.GetProperty("findings").GetArrayLength(), Is.EqualTo(1));
        }

        // Временная папка с репозиторием repo (маркер .git) внутри
        private static TempFolder CreateRepository()
        {
            var folder = new TempFolder();
            Directory.CreateDirectory(Path.Combine(folder.Path, "repo", ".git"));
            return folder;
        }
    }
}

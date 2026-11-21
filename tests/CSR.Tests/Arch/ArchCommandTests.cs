using System.Text.Json;

namespace CSR.Tests.Arch
{
    public class ArchCommandTests
    {
        private const string Config = """
            {
                "checks": {
                    "type-branch": { "severity": "warning", "rule": "R1" },
                    "numeric-constant": { "severity": "hint", "rule": "R2" }
                }
            }
            """;

        private const string Source = """
            public class Process
            {
                private const float EnterRadius = 0.5f;

                public float Score(Shape shape)
                    => shape is Circle circle ? circle.Radius : 0;
            }
            """;

        [Test]
        public void Help_ShowsArch()
        {
            var result = Cli.Run("help", "arch");
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("csr arch <файлы | папки>"));
            Assert.That(Cli.Run("help").Output, Does.Contain("arch     архитектурные признаки"));
        }

        [Test]
        public void Json_FindingsWithQuestions_ExitZero()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Process.cs", Source);
            var config = folder.Write("arch.jsonc", Config);
            var result = Cli.Run("arch", file, "--config", config);

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Error, Does.Contain("csr arch: файлов 1, находок 2"));
            using var document = JsonDocument.Parse(result.Output);
            var summary = document.RootElement.GetProperty("summary");
            Assert.That(summary.GetArrayLength(), Is.EqualTo(2));
            Assert.That(summary[0].GetProperty("question").GetString(), Is.Not.Empty);
            var finding = document.RootElement.GetProperty("findings")[1];
            Assert.That(finding.GetProperty("rule").GetString(), Is.EqualTo("R1"));
        }

        [Test]
        public void ErrorSeverity_ExitOne_MarkdownToFile()
        {
            using var folder = new TempFolder();
            folder.Write("Process.cs", Source);
            var config = folder.Write("arch.jsonc", Config);
            var layer = folder.Write("layer.jsonc", """{ "checks": { "type-branch": { "severity": "error" } } }""");
            var output = Path.Combine(folder.Path, "arch.md");
            var result = Cli.Run(
                "arch",
                folder.Path,
                "--config",
                config,
                "--config",
                layer,
                "--format",
                "md",
                "--out",
                output);

            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Output, Is.Empty);
            Assert.That(folder.Read("arch.md"), Does.Contain("`type-branch` R1 (error) - проверка на тип 'Circle'"));
        }

        [Test]
        public void ChangedLines_OnlyChangedFindings()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Process.cs", Source);
            var config = folder.Write("arch.jsonc", Config);
            Cli.InitRepository(folder.Path);
            File.WriteAllText(file, Source.Replace("EnterRadius = 0.5f", "EnterRadius = 0.7f"));

            var result = Cli.Run("arch", folder.Path, "--config", config, "--diff", "HEAD");

            using var document = JsonDocument.Parse(result.Output);
            var findings = document.RootElement.GetProperty("findings");
            Assert.That(findings.GetArrayLength(), Is.EqualTo(1));
            Assert.That(findings[0].GetProperty("check").GetString(), Is.EqualTo("numeric-constant"));
        }

        [TestCase("--format", "html")]
        [TestCase("--scope", "lines")]
        [TestCase("--config", "missing.jsonc")]
        public void InvalidArguments_ExitTwo(string option, string value)
        {
            using var folder = new TempFolder();
            var file = folder.Write("Process.cs", Source);
            var config = folder.Write("arch.jsonc", Config);
            Assert.That(Cli.Run("arch", file, "--config", config, option, value).ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void WithoutConfigOrFiles_ExitTwo()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Process.cs", Source);
            var result = Cli.Run("arch", file);

            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("--config"));
            Assert.That(Cli.Run("arch").ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void StyleCheckInArchConfig_ExitTwo()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Process.cs", Source);
            var config = folder.Write("arch.jsonc", """{ "checks": { "line-length": {} } }""");
            var result = Cli.Run("arch", file, "--config", config);

            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("неизвестная проверка 'line-length'"));
        }
    }
}

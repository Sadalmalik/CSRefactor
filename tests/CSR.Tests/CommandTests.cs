using System.Text.Json;

namespace CSR.Tests
{
    public class ProgramTests
    {
        [TestCase()]
        [TestCase("help")]
        [TestCase("--help")]
        public void Help_ExitZero(params string[] args)
        {
            var result = Cli.Run(args);
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("metrics"));
        }

        [TestCase("metrics")]
        [TestCase("style")]
        public void HelpForCommand(string command)
            => Assert.That(Cli.Run("help", command).Output, Does.StartWith($"csr {command}"));

        [Test]
        public void UnknownCommand_ExitTwo()
        {
            var result = Cli.Run("refactor");
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("не реализована"));
        }
    }

    public class MetricsCommandTests
    {
        private const string Source = """
            namespace Space
            {
                public class Sample
                {
                    public int Branchy(bool a, bool b)
                    {
                        if (a)
                            return 1;
                        if (b)
                            return 2;
                        return 3;
                    }
                }
            }
            """;

        [Test]
        public void Json_ToOutFile()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Source);
            var result = Cli.Run("metrics", file, "--out", Path.Combine(folder.Path, "report.json"));

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Is.Empty);
            Assert.That(result.Error, Does.Contain("функций 1"));
            using var document = JsonDocument.Parse(folder.Read("report.json"));
            Assert.That(document.RootElement.GetProperty("functions").GetInt32(), Is.EqualTo(1));
        }

        [Test]
        public void Markdown_ToStdoutWithConfigAndTop()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Source);
            var config = folder.Write("metrics.jsonc", """{ "function": { "cyclomatic": { "threshold": 1 } } }""");
            var project = folder.Write("project.jsonc", """{ "preprocessorSymbols": [] }""");
            var result = Cli.Run(
                "metrics",
                file,
                "--config",
                config,
                "--project",
                project,
                "--format",
                "md",
                "--top",
                "1");

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("| Функция | Цикломатическая сложность | 3 | 3 | 3 | 3 | 1 | 1 |"));
        }

        [Test]
        public void CoverageAndEdn()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Source);
            var coverage = folder.Write("coverage.xml", $"""
                <CoverageSession><Modules><Module>
                  <Files><File uid="1" fullPath="{file}" /></Files>
                  <Classes><Class><Methods><Method><SequencePoints>
                    <SequencePoint vc="1" sl="7" sc="1" fileid="1" />
                    <SequencePoint vc="0" sl="8" sc="1" fileid="1" />
                  </SequencePoints></Method></Methods></Class></Classes>
                </Module></Modules></CoverageSession>
                """);
            var edn = Path.Combine(folder.Path, "crap.edn");
            var result = Cli.Run("metrics", file, "--coverage", coverage, "--edn", edn, "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("## CRAP"));
            Assert.That(folder.Read("crap.edn"), Does.Contain(
                "{:name \"Branchy\", :namespace \"Space.Sample\", :complexity 3, :coverage 50.0, :crap 4.125}"));
        }

        [TestCase("--format", "xml")]
        [TestCase("--top", "-1")]
        [TestCase("--coverage", "missing.xml")]
        [TestCase("--config", "missing.jsonc")]
        public void InvalidArguments_ExitTwo(string option, string value)
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Source);
            Assert.That(Cli.Run("metrics", file, option, value).ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void NoFiles_ExitTwo()
            => Assert.That(Cli.Run("metrics").ExitCode, Is.EqualTo(2));

        [TestCase("""{ "function": { "nope": {} } }""", "неизвестная метрика")]
        [TestCase("{ broken", "конфиг")]
        [TestCase("<not xml", "покрытие")]
        public void BrokenInputs_ExitTwoWithMessage(string text, string message)
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Source);
            var input = folder.Write("input.txt", text);
            var option = message == "покрытие" ? "--coverage" : "--config";
            var result = Cli.Run("metrics", file, option, input);

            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain(message));
        }
    }

    public class StyleCommandTests
    {
        private const string Config = """
            {
                "checks": {
                    "null-check": { "severity": "error", "rule": "R1", "prefer": "equality" },
                    "comment-ending": { "severity": "warning", "rule": "R2" }
                }
            }
            """;

        private const string Clean = """
            public class Sample
            {
                // Комментарий без точки
                private bool Check(object value)
                    => value == null;
            }
            """;

        private const string Dirty = """
            public class Sample
            {
                // Комментарий с точкой.
                private bool Check(object value)
                    => value is null;
            }
            """;

        [Test]
        public void Clean_ExitZero()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var config = folder.Write("style.jsonc", Config);
            var result = Cli.Run("style", file, "--config", config);

            Assert.That(result.ExitCode, Is.EqualTo(0));
            using var document = JsonDocument.Parse(result.Output);
            Assert.That(document.RootElement.GetProperty("findings").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public void ErrorFinding_ExitOne_Markdown()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);
            var project = folder.Write("project.jsonc", """{ "logger": { "methods": ["Debug.Log"] } }""");
            var result = Cli.Run("style", folder.Path, "--config", config, "--project", project, "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Output, Does.Contain("`null-check` R1 (error)"));
            Assert.That(result.Output, Does.Contain("`comment-ending` R2 (warning)"));
        }

        [Test]
        public void WarningOnly_ExitZero()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);
            var layer = folder.Write("layer.jsonc", """{ "checks": { "null-check": { "enabled": false } } }""");
            var result = Cli.Run("style", file, "--config", config, "--config", layer);

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("comment-ending"));
            Assert.That(result.Output, Does.Not.Contain("null-check"));
        }

        [TestCase("--scope", "lines")]
        [TestCase("--format", "html")]
        public void InvalidArguments_ExitTwo(string option, string value)
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var config = folder.Write("style.jsonc", Config);
            Assert.That(Cli.Run("style", file, "--config", config, option, value).ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void WithoutConfig_ExitTwo()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var result = Cli.Run("style", file);

            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("--config"));
        }

        [Test]
        public void UnknownCheck_ExitTwo()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var config = folder.Write("style.jsonc", """{ "checks": { "nope": {} } }""");
            Assert.That(Cli.Run("style", file, "--config", config).ExitCode, Is.EqualTo(2));
        }

#region changed-lines

        [Test]
        public void ChangedLines_OnlyFindingsOnChangedLines()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            File.WriteAllText(file, Dirty.Replace("private bool Check", "private bool CheckValue"));

            var result = Cli.Run("style", file, "--config", config, "--scope", "changed-lines");

            using var document = JsonDocument.Parse(result.Output);
            var findings = document.RootElement.GetProperty("findings");
            Assert.That(findings.GetArrayLength(), Is.EqualTo(0), "строка 4 изменена, находки - на строках 3 и 5");
            Assert.That(document.RootElement.GetProperty("scope").GetString(), Is.EqualTo("changed-lines"));
        }

        [Test]
        public void ChangedLines_ModifiedLineWithFinding()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            File.WriteAllText(file, Clean.Replace("value == null", "value is null"));

            var result = Cli.Run("style", folder.Path, "--config", config, "--diff", "HEAD");

            Assert.That(result.ExitCode, Is.EqualTo(1));
            using var document = JsonDocument.Parse(result.Output);
            var finding = document.RootElement.GetProperty("findings")[0];
            Assert.That(finding.GetProperty("check").GetString(), Is.EqualTo("null-check"));
            Assert.That(finding.GetProperty("line").GetInt32(), Is.EqualTo(5));
        }

        [Test]
        public void ChangedLines_UntrackedFileIsWhole()
        {
            using var folder = new TempFolder();
            folder.Write("Old.cs", Clean);
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            folder.Write("New.cs", Dirty);

            var result = Cli.Run("style", folder.Path, "--config", config, "--scope", "changed-lines");

            using var document = JsonDocument.Parse(result.Output);
            Assert.That(document.RootElement.GetProperty("files").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("findings").GetArrayLength(), Is.EqualTo(2));
        }

        [Test]
        public void DiffFile_UsesGivenDiff()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            var diff = folder.Write("changes.diff", """
                --- a/Sample.cs
                +++ b/Sample.cs
                @@ -3 +3 @@
                -// old
                +// new
                """);

            var result = Cli.Run("style", file, "--config", config, "--diff", diff);

            using var document = JsonDocument.Parse(result.Output);
            var findings = document.RootElement.GetProperty("findings");
            Assert.That(findings.GetArrayLength(), Is.EqualTo(1));
            Assert.That(findings[0].GetProperty("check").GetString(), Is.EqualTo("comment-ending"));
        }

        [Test]
        public void ChangedLines_OutsideRepository_ExitTwo()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Clean);
            var config = folder.Write("style.jsonc", Config);
            var result = Cli.Run("style", file, "--config", config, "--scope", "changed-lines");

            Assert.That(result.ExitCode, Is.EqualTo(2));
        }

#endregion
    }
}

using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Style;
using CSR.Reports.Arch;
using CSR.Reports.Config;
using CSR.Reports.Style;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Reports.Tests.Arch
{
    public class ArchReportTests
    {
        private const string Config = """
            {
                // метки архитектурного гайда
                "checks": {
                    "type-branch": { "severity": "warning", "rule": "A4" },
                    "obsolete-reason": { "severity": "hint", "rule": "A32" },
                },
            }
            """;

        private const string Source = """
            public class Sample
            {
                [Obsolete]
                private void Method(object value)
                {
                    if (value is Box box)
                        return;
                }
            }
            """;

        private static StyleReport Analyze(StyleOptions options)
        {
            var project = new ProjectOptions();
            var tree = CSharpSyntaxTree.ParseText(Source, project.ParseOptions, "Sample.cs");
            var findings = StyleAnalyzer.Analyze(tree, options, project);
            return new StyleReport { FileCount = 1, Scope = "file", Findings = findings };
        }

        [Test]
        public void Config_ArchChecksByName()
        {
            var options = ArchConfig.Parse(Config);
            var names = options.Checks.Select(check => check.Check.Name);
            Assert.That(names, Is.EqualTo(new[] { "type-branch", "obsolete-reason" }));
            Assert.That(options.Checks[0].Rule, Is.EqualTo("A4"));
        }

        [TestCase("""{ "checks": { "line-length": {} } }""", "неизвестная проверка 'line-length'")]
        [TestCase("""{ "checks": { "type-branch": { "nope": 1 } } }""", "type-branch: неизвестный параметр 'nope'")]
        [TestCase("""{ "checks": { "type-branch": { "paths": "x" } } }""", "type-branch.paths: ожидается массив строк")]
        public void Config_Invalid_Throws(string json, string message)
        {
            var exception = Assert.Throws<ConfigException>(() => ArchConfig.Parse(json));
            Assert.That(exception!.Message, Is.EqualTo(message));
        }

        [Test]
        public void Config_StyleCatalogUnchanged()
        {
            Assert.Throws<ConfigException>(() => StyleConfig.Parse("""{ "checks": { "type-branch": {} } }"""));
            Assert.That(StyleConfig.Parse("""{ "checks": { "tuple-type": {} } }""").Checks, Has.Count.EqualTo(1));
        }

        [Test]
        public void Config_Layers_DisableAndParameters()
        {
            var options = ArchConfig.Parse(Config);
            const string layer = """
                { "checks": { "obsolete-reason": { "enabled": false }, "type-branch": { "ignoreTypes": ["Box"] } } }
                """;
            ArchConfig.Apply(layer, options);
            Assert.That(Analyze(options).Findings, Is.Empty);
        }

        [Test]
        public void Json_SummaryWithQuestion()
        {
            var json = ArchJsonWriter.Write(Analyze(ArchConfig.Parse(Config)));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var summary = root.GetProperty("summary");
            Assert.That(summary.GetArrayLength(), Is.EqualTo(2));
            Assert.That(summary[0].GetProperty("check").GetString(), Is.EqualTo("type-branch"));
            Assert.That(summary[0].GetProperty("question").GetString(), Does.StartWith("Может ли тип"));

            var finding = root.GetProperty("findings")[1];
            Assert.That(finding.GetProperty("rule").GetString(), Is.EqualTo("A4"));
            Assert.That(finding.GetProperty("line").GetInt32(), Is.EqualTo(6));
            Assert.That(finding.TryGetProperty("replacement", out _), Is.False);
        }

        [Test]
        public void Markdown_TableWithQuestions()
        {
            var text = ArchMarkdownWriter.Write(Analyze(ArchConfig.Parse(Config)));
            Assert.That(text, Does.StartWith("# Архитектура"));
            Assert.That(text, Does.Contain("| `type-branch` | A4 | warning | 1 | Может ли тип"));
            Assert.That(text, Does.Contain("- 6: `type-branch` A4 (warning) - проверка на тип 'Box'"));
            Assert.That(text, Does.Contain("- 3: `obsolete-reason` A32 (hint) - [Obsolete] без причины: Method"));
        }
    }
}

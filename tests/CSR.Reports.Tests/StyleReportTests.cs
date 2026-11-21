using System.Text.Json;
using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Reports.Checks;
using CSR.Reports.Config;
using CSR.Reports.Style;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Reports.Tests
{
    public class StyleReportTests
    {
        private const string Config = """
            {
                // личные метки
                "checks": {
                    "line-length": { "severity": "error", "rule": "F20", "max": 30 },
                    "null-check": { "severity": "warning", "rule": "I12", "prefer": "equality" },
                    "xml-doc": { "severity": "error" },
                },
            }
            """;

        private const string Source = """
            public class Sample
            {
                private void Method(object value)
                {
                    if (value is null)
                        return;
                }
            }
            """;

        private static List<Finding> Analyze(CheckOptions options)
        {
            var project = new ProjectOptions();
            var tree = CSharpSyntaxTree.ParseText(Source, project.ParseOptions, "Sample.cs");
            return SyntaxAnalyzer.Analyze(tree, options, project);
        }

        [Test]
        public void Config_EnablesChecksWithLabelsAndParameters()
        {
            var options = StyleConfig.Parse(Config);
            Assert.That(options.Checks.Select(check => check.Check.Name),
                Is.EqualTo(new[] { "line-length", "null-check", "xml-doc" }));

            var findings = Analyze(options);
            Assert.That(findings.Select(finding => $"{finding.Line} {finding.Check} {finding.Rule}"), Is.EqualTo(new[]
            {
                "3 line-length F20",
                "5 null-check I12",
            }));
        }

        [Test]
        public void Config_SecondLayerOverridesAndDisables()
        {
            var options = StyleConfig.Parse(Config);
            StyleConfig.Apply("""{ "checks": { "line-length": { "max": 120 }, "null-check": { "enabled": false } } }""",
                options);

            Assert.That(Analyze(options), Is.Empty);
            Assert.That(options.Checks[0].Rule, Is.EqualTo("F20"));
        }

        [TestCase("""{ "checks": { "no-such-check": {} } }""")]
        [TestCase("""{ "checks": { "line-length": { "maximum": 10 } } }""")]
        [TestCase("""{ "checks": { "null-check": { "prefer": "never" } } }""")]
        [TestCase("""{ "checks": { "line-length": { "severity": "fatal" } } }""")]
        [TestCase("""{ "rules": {} }""")]
        public void Config_Invalid_Throws(string json)
            => Assert.Throws<ConfigException>(() => StyleConfig.Parse(json));

        [Test]
        public void Project_LoggerMasks()
        {
            var project = ProjectConfig.Parse("""{ "logger": { "methods": ["Debug.Log", "*.Logger.Error"] } }""");
            Assert.That(project.IsLoggerMethod("Debug.Log"), Is.True);
            Assert.That(project.IsLoggerMethod("Combat.Logger.Error"), Is.True);
            Assert.That(project.IsLoggerMethod("Debug.LogError"), Is.False);
            Assert.That(project.PreprocessorSymbols, Is.EqualTo(new[] { "UNITY_EDITOR" }));
        }

        [Test]
        public void Diff_ParsesAddedLines()
        {
            const string diff = """
                diff --git a/src/A.cs b/src/A.cs
                --- a/src/A.cs
                +++ b/src/A.cs
                @@ -10,0 +11,2 @@ class A
                +line
                +line
                @@ -20 +22 @@
                -old
                +new
                @@ -30,3 +33,0 @@
                diff --git a/src/Old.cs b/src/Old.cs
                --- a/src/Old.cs
                +++ /dev/null
                """;
            var files = UnifiedDiff.Parse(diff);
            Assert.That(files.Keys, Is.EqualTo(new[] { "src/A.cs" }));

            var lines = files["src/A.cs"];
            Assert.That(Changed(lines, 11), Is.True);
            Assert.That(Changed(lines, 12), Is.True);
            Assert.That(Changed(lines, 13), Is.False);
            Assert.That(Changed(lines, 22), Is.True);
            Assert.That(Changed(lines, 33), Is.False);
        }

        [Test]
        public void Json_FindingsAndSummary()
        {
            var findings = Analyze(StyleConfig.Parse(Config));
            var report = new CheckReport { FileCount = 1, Scope = "file", Findings = findings };
            using var document = JsonDocument.Parse(StyleJsonWriter.Write(report));
            var root = document.RootElement;

            Assert.That(root.GetProperty("summary")[0].GetProperty("check").GetString(), Is.EqualTo("line-length"));
            var nullCheck = root.GetProperty("findings")[1];
            Assert.That(nullCheck.GetProperty("rule").GetString(), Is.EqualTo("I12"));
            Assert.That(nullCheck.GetProperty("severity").GetString(), Is.EqualTo("warning"));
            Assert.That(nullCheck.GetProperty("replacement")[0].GetString(), Is.EqualTo("        if (value == null)"));
            Assert.That(report.HasErrors, Is.True);
        }

        [Test]
        public void Markdown_ByFileAndLine()
        {
            var findings = Analyze(StyleConfig.Parse(Config));
            var report = new CheckReport { FileCount = 1, Scope = "file", Findings = findings };
            var markdown = StyleMarkdownWriter.Write(report);

            Assert.That(markdown, Does.Contain("Файлов: 1, находок: 2, режим: file."));
            Assert.That(markdown, Does.Contain("## `Sample.cs`"));
            Assert.That(markdown, Does.Contain("- 5: `null-check` I12 (warning)"));
        }

        [Test]
        public void ChangedLines_ContainsLine()
        {
            var lines = new ChangedLines();
            lines.Add(3, 2);
            Assert.That(new[] { 2, 3, 4, 5 }.Select(lines.Contains), Is.EqualTo(new[] { false, true, true, false }));
            Assert.That(new ChangedLines { IsWholeFile = true }.Contains(100), Is.True);
        }

        private static bool Changed(ChangedLines lines, int line)
        {
            var finding = new Finding
            {
                Check = "x",
                Severity = Severity.Hint,
                Message = "",
                File = "",
                Line = line,
                EndLine = line,
                Column = 1,
            };

            return lines.Intersects(finding);
        }
    }
}

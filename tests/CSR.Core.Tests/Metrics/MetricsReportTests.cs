using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class MetricsReportTests
    {
        private const string Source = """
            class Sample
            {
                void Simple() { }

                void Branchy(bool a, bool b)
                {
                    if (a) return;
                    if (b) return;
                    if (a && b) return;
                }

                public static Sample operator |(Sample x, Sample y) => x;
            }
            """;

        private static MetricSummary Find(MetricsReport report, string id)
            => report.Metrics.Single(summary => summary.Metric.Id == id);

        [Test]
        public void Percentile_TakesFloorIndex()
        {
            int[] values = Enumerable.Range(1, 10).ToArray();
            Assert.That(MetricsReport.Percentile(values, 0.5), Is.EqualTo(6));
            Assert.That(MetricsReport.Percentile(values, 0.9), Is.EqualTo(10));
            Assert.That(MetricsReport.Percentile(values, 0.99), Is.EqualTo(10));
            Assert.That(MetricsReport.Percentile(Array.Empty<int>(), 0.5), Is.EqualTo(0));
        }

        [Test]
        public void Build_SummaryThresholdAndTop()
        {
            var options = MetricsOptions.FromJson("""
                {
                    // комментарий
                    "top": 2,
                    "function": { "cyclomatic": { "threshold": 1 } },
                }
                """);
            MetricsReport report = MetricsReport.Build([TestCode.Measure(Source)], options);
            MetricSummary cyclomatic = Find(report, "function.cyclomatic");

            Assert.That(cyclomatic.Count, Is.EqualTo(3));
            Assert.That(cyclomatic.Max, Is.EqualTo(5));
            Assert.That(cyclomatic.Threshold, Is.EqualTo(1));
            Assert.That(cyclomatic.AboveThreshold, Is.EqualTo(1));
            Assert.That(cyclomatic.Top.Select(item => item.Name),
                Is.EqualTo(new[] { "Sample.Branchy", "Sample.Simple" }));
        }

        [Test]
        public void Options_DisabledMetricAndNullThreshold()
        {
            var options = MetricsOptions.FromJson("""
                { "function": { "parameters": { "enabled": false }, "nesting": { "threshold": null } } }
                """);
            MetricsReport report = MetricsReport.Build([TestCode.Measure(Source)], options);

            Assert.That(report.Metrics.Any(summary => summary.Metric.Id == "function.parameters"), Is.False);
            Assert.That(Find(report, "function.nesting").Threshold, Is.Null);
            Assert.That(Find(report, "file.lines").Threshold, Is.EqualTo(400));
        }

        [TestCase("""{ "function": { "unknown": {} } }""")]
        [TestCase("""{ "functions": {} }""")]
        [TestCase("""{ "function": { "nesting": { "max": 3 } } }""")]
        [TestCase("""{ "top": "ten" }""")]
        public void Options_InvalidConfig_Throws(string json)
            => Assert.Throws<ConfigException>(() => MetricsOptions.FromJson(json));

        [Test]
        public void Project_PreprocessorSymbolsAffectParsing()
        {
            const string source = """
                class Sample
                {
                #if UNITY_EDITOR
                    void EditorOnly() { }
                #endif
                }
                """;
            ProjectOptions withEditor = new();
            ProjectOptions withoutEditor = ProjectOptions.FromJson("""{ "preprocessorSymbols": [] }""");

            FileMetrics editor = MetricsCalculator.Calculate(source, "a.cs", withEditor.ParseOptions);
            FileMetrics player = MetricsCalculator.Calculate(source, "a.cs", withoutEditor.ParseOptions);
            Assert.That(editor.Functions, Has.Count.EqualTo(1));
            Assert.That(player.Functions, Is.Empty);
        }

        [Test]
        public void Json_ContainsSummaryTopAndItems()
        {
            MetricsReport report = MetricsReport.Build([TestCode.Measure(Source)], new MetricsOptions());
            using JsonDocument document = JsonDocument.Parse(MetricsJsonWriter.Write(report, includeItems: true));
            JsonElement root = document.RootElement;

            Assert.That(root.GetProperty("files").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("functions").GetInt32(), Is.EqualTo(3));

            JsonElement cyclomatic = root.GetProperty("metrics").EnumerateArray()
                .Single(metric => metric.GetProperty("id").GetString() == "function.cyclomatic");
            JsonElement first = cyclomatic.GetProperty("top")[0];
            Assert.That(first.GetProperty("name").GetString(), Is.EqualTo("Sample.Branchy"));
            Assert.That(first.GetProperty("value").GetInt32(), Is.EqualTo(5));
            Assert.That(first.GetProperty("line").GetInt32(), Is.EqualTo(5));

            JsonElement file = root.GetProperty("items")[0];
            Assert.That(file.GetProperty("file").GetString(), Is.EqualTo(TestCode.FilePath));
            Assert.That(file.GetProperty("functions").GetArrayLength(), Is.EqualTo(3));
            Assert.That(file.GetProperty("types")[0].GetProperty("members").GetProperty("methods").GetInt32(),
                Is.EqualTo(2));
        }

        [Test]
        public void Json_WithoutItems_HasNoItems()
        {
            MetricsReport report = MetricsReport.Build([TestCode.Measure(Source)], new MetricsOptions());
            using JsonDocument document = JsonDocument.Parse(MetricsJsonWriter.Write(report, includeItems: false));
            Assert.That(document.RootElement.TryGetProperty("items", out _), Is.False);
        }

        [Test]
        public void Markdown_SummaryAndEscapedTop()
        {
            MetricsReport report = MetricsReport.Build([TestCode.Measure(Source)], new MetricsOptions());
            string markdown = MetricsMarkdownWriter.Write(report);

            Assert.That(markdown, Does.Contain("Файлов: 1, типов: 1, функций: 3."));
            Assert.That(markdown, Does.Contain("| Функция | Цикломатическая сложность | 1 | 5 | 5 | 5 | 10 | 0 |"));
            Assert.That(markdown, Does.Contain("`Sample.operator \\|`"));
        }
    }
}

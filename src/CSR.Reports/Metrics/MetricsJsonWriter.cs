using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Metrics;

namespace CSR.Reports.Metrics
{
    // Отчет для агентов: сводка, топ по каждой метрике и, по запросу, все элементы по файлам
    public static class MetricsJsonWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Write(MetricsReport report, bool includeItems)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("files", report.Files.Count);
                writer.WriteNumber("types", report.TypeCount);
                writer.WriteNumber("functions", report.FunctionCount);

                writer.WriteStartArray("metrics");
                foreach (var summary in report.Metrics)
                    WriteSummary(writer, summary);
                writer.WriteEndArray();

                if (report.CrapTop.Count > 0)
                {
                    writer.WriteStartArray("crap");
                    foreach (var function in report.CrapTop)
                    {
                        writer.WriteStartObject();
                        writer.WriteNumber("value", function.Crap!.Value);
                        writer.WriteNumber("coverage", function.Coverage!.Value);
                        writer.WriteNumber("complexity", function.Values[MetricKeys.Cyclomatic]);
                        WriteLocation(writer, function);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                if (includeItems)
                {
                    writer.WriteStartArray("items");
                    foreach (var file in report.Files)
                        WriteFile(writer, file);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteSummary(Utf8JsonWriter writer, MetricSummary summary)
        {
            var metric = summary.Metric;
            writer.WriteStartObject();
            writer.WriteString("id", metric.Id);
            writer.WriteString("title", metric.Title);
            writer.WriteNumber("count", summary.Count);
            writer.WriteNumber("p50", summary.P50);
            writer.WriteNumber("p90", summary.P90);
            writer.WriteNumber("p99", summary.P99);
            writer.WriteNumber("max", summary.Max);
            if (summary.Threshold == null)
                writer.WriteNull("threshold");
            else
                writer.WriteNumber("threshold", summary.Threshold.Value);
            writer.WriteNumber("aboveThreshold", summary.AboveThreshold);

            writer.WriteStartArray("top");
            foreach (var item in summary.Top)
            {
                writer.WriteStartObject();
                writer.WriteNumber("value", item.Values[metric.Key]);
                WriteLocation(writer, item);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteFile(Utf8JsonWriter writer, FileMetrics file)
        {
            writer.WriteStartObject();
            writer.WriteString("file", file.File.File);
            WriteValues(writer, "values", file.File.Values);

            writer.WriteStartArray("types");
            foreach (var type in file.Types)
                WriteItem(writer, type);
            writer.WriteEndArray();

            writer.WriteStartArray("functions");
            foreach (var function in file.Functions)
                WriteItem(writer, function);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        private static void WriteItem(Utf8JsonWriter writer, MetricItem item)
        {
            writer.WriteStartObject();
            WriteLocation(writer, item);
            WriteValues(writer, "values", item.Values);
            if (item.Coverage != null)
                writer.WriteNumber("coverage", item.Coverage.Value);
            if (item.Crap != null)
                writer.WriteNumber("crap", item.Crap.Value);
            if (item.Members != null)
                WriteValues(writer, "members", item.Members);
            writer.WriteEndObject();
        }

        private static void WriteLocation(Utf8JsonWriter writer, MetricItem item)
        {
            if (item.Level == MetricLevel.File)
            {
                writer.WriteString("file", item.File);
                return;
            }

            writer.WriteString("name", item.Name);
            writer.WriteString("kind", item.Kind);
            writer.WriteString("namespace", item.Namespace);
            writer.WriteString("file", item.File);
            writer.WriteNumber("line", item.Line);
            writer.WriteNumber("endLine", item.EndLine);
        }

        private static void WriteValues(Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, int> values)
        {
            writer.WriteStartObject(name);
            foreach (var pair in values)
                writer.WriteNumber(pair.Key, pair.Value);
            writer.WriteEndObject();
        }
    }
}

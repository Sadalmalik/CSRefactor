using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Checks;
using CSR.Reports.Checks;

namespace CSR.Reports.Style
{
    // Отчет для агентов: находки и сводка по проверкам
    public static class StyleJsonWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Write(CheckReport report)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("files", report.FileCount);
                writer.WriteString("scope", report.Scope);

                writer.WriteStartArray("summary");
                foreach (var group in report.Summarize())
                {
                    writer.WriteStartObject();
                    writer.WriteString("check", group.Check);
                    WriteNullable(writer, "rule", group.Rule);
                    writer.WriteString("severity", Finding.GetSeverityKey(group.Severity));
                    writer.WriteNumber("count", group.Count);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                writer.WriteStartArray("findings");
                foreach (var finding in report.Findings)
                    WriteFinding(writer, finding);
                writer.WriteEndArray();

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteFinding(Utf8JsonWriter writer, Finding finding)
        {
            writer.WriteStartObject();
            writer.WriteString("file", finding.File);
            writer.WriteNumber("line", finding.Line);
            writer.WriteNumber("endLine", finding.EndLine);
            writer.WriteNumber("column", finding.Column);
            writer.WriteString("check", finding.Check);
            WriteNullable(writer, "rule", finding.Rule);
            writer.WriteString("severity", Finding.GetSeverityKey(finding.Severity));
            writer.WriteString("message", finding.Message);
            if (finding.Replacement != null)
            {
                writer.WriteStartArray("replacement");
                foreach (var line in finding.Replacement)
                    writer.WriteStringValue(line);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
        {
            if (value == null)
                writer.WriteNull(name);
            else
                writer.WriteString(name, value);
        }
    }

    // Отчет для человека: сводка, затем находки по файлам и строкам
    public static class StyleMarkdownWriter
    {
        public static string Write(CheckReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Стиль");
            builder.AppendLine();
            builder.AppendLine($"Файлов: {report.FileCount}, находок: {report.Findings.Count}, режим: {report.Scope}.");

            var summary = report.Summarize();
            if (summary.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("| Проверка | Правило | Уровень | Находок |");
                builder.AppendLine("|---|---|---|---:|");
                foreach (var group in summary)
                {
                    var severity = Finding.GetSeverityKey(group.Severity);
                    builder.AppendLine($"| `{group.Check}` | {group.Rule ?? ""} | {severity} | {group.Count} |");
                }
            }

            foreach (var file in report.Findings.GroupBy(finding => finding.File))
            {
                builder.AppendLine();
                builder.AppendLine($"## `{file.Key}`");
                builder.AppendLine();
                foreach (var finding in file)
                    WriteFinding(builder, finding);
            }

            return builder.ToString();
        }

        private static void WriteFinding(StringBuilder builder, Finding finding)
        {
            var lines = finding.EndLine == finding.Line ? $"{finding.Line}" : $"{finding.Line}-{finding.EndLine}";
            var rule = finding.Rule == null ? "" : $" {finding.Rule}";
            var severity = Finding.GetSeverityKey(finding.Severity);
            builder.AppendLine($"- {lines}: `{finding.Check}`{rule} ({severity}) - {finding.Message}");
            if (finding.Replacement == null)
                return;

            if (finding.Replacement.Count == 0)
            {
                builder.AppendLine("  - замена: удалить строки");
                return;
            }

            builder.AppendLine("  ```csharp");
            foreach (var line in finding.Replacement)
                builder.AppendLine("  " + line);
            builder.AppendLine("  ```");
        }
    }
}

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Arch;
using CSR.Core.Style;
using CSR.Reports.Style;

namespace CSR.Reports.Arch
{
    // Отчет arch: находки-решения. В сводке у признака - вопрос, который надо решить; в находке - что сработало
    public static class ArchJsonWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Write(StyleReport report)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("files", report.FileCount);
                writer.WriteString("scope", report.Scope);

                writer.WriteStartArray("summary");
                foreach (var group in StyleJsonWriter.Summarize(report))
                {
                    writer.WriteStartObject();
                    writer.WriteString("check", group.Check);
                    WriteNullable(writer, "rule", group.Rule);
                    writer.WriteString("severity", Finding.GetSeverityKey(group.Severity));
                    writer.WriteNumber("count", group.Count);
                    WriteNullable(writer, "question", ArchCatalog.GetQuestion(group.Check));
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

    // Отчет arch для человека: сводка с вопросами, затем находки по файлам и строкам
    public static class ArchMarkdownWriter
    {
        public static string Write(StyleReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Архитектура");
            builder.AppendLine();
            builder.AppendLine($"Файлов: {report.FileCount}, находок: {report.Findings.Count}, режим: {report.Scope}.");

            var summary = StyleJsonWriter.Summarize(report);
            if (summary.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("| Признак | Правило | Уровень | Находок | Что решить |");
                builder.AppendLine("|---|---|---|---:|---|");
                foreach (var group in summary)
                {
                    var severity = Finding.GetSeverityKey(group.Severity);
                    var question = ArchCatalog.GetQuestion(group.Check) ?? "";
                    builder.AppendLine(
                        $"| `{group.Check}` | {group.Rule ?? ""} | {severity} | {group.Count} | {question} |");
                }
            }

            foreach (var file in report.Findings.GroupBy(finding => finding.File))
            {
                builder.AppendLine();
                builder.AppendLine($"## `{file.Key}`");
                builder.AppendLine();
                foreach (var finding in file)
                {
                    var rule = finding.Rule == null ? "" : $" {finding.Rule}";
                    var severity = Finding.GetSeverityKey(finding.Severity);
                    builder.AppendLine($"- {finding.Line}: `{finding.Check}`{rule} ({severity}) - {finding.Message}");
                }
            }

            return builder.ToString();
        }
    }
}

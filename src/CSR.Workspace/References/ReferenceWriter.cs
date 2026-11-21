using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CSR.Workspace.References
{
    // Отчет refs: JSON для агента или строки "файл:строка:колонка: код" (как у grep)
    public static class ReferenceWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string WriteJson(ReferenceReport report)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteString("query", report.Query);
                writer.WriteNumber("references", report.ReferenceCount);
                writer.WriteNumber("files", report.FileCount);
                writer.WriteStartArray("definitions");
                foreach (var definition in report.Definitions)
                    WriteDefinition(writer, definition);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        public static string WriteText(ReferenceReport report)
        {
            var text = new StringBuilder();
            foreach (var definition in report.Definitions)
            {
                var source = definition.Metadata ? ", метаданные" : "";
                var count = definition.References.Count;
                text.Append($"# {definition.Symbol} ({definition.Kind}{source}): ссылок {count}\n");
                foreach (var declaration in definition.Declarations)
                    text.Append($"{FormatLocation(declaration)}: объявление: {declaration.Text}\n");

                foreach (var place in definition.References)
                    text.Append($"{FormatLocation(place)}: {place.Text}{GetMarks(place)}\n");
            }

            return text.ToString();
        }

        private static void WriteDefinition(Utf8JsonWriter writer, ReferencedDefinition definition)
        {
            writer.WriteStartObject();
            writer.WriteString("symbol", definition.Symbol);
            writer.WriteString("kind", definition.Kind);
            if (definition.Metadata)
                writer.WriteBoolean("metadata", true);

            writer.WriteStartArray("declarations");
            foreach (var declaration in definition.Declarations)
            {
                writer.WriteStartObject();
                WriteLocation(writer, declaration);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("references");
            foreach (var place in definition.References)
                WritePlace(writer, place);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WritePlace(Utf8JsonWriter writer, ReferencePlace place)
        {
            writer.WriteStartObject();
            WriteLocation(writer, place);
            if (place.Container != null)
                writer.WriteString("container", place.Container);

            if (place.Generated)
                writer.WriteBoolean("generated", true);

            if (place.Implicit)
                writer.WriteBoolean("implicit", true);

            if (place.Candidate)
                writer.WriteBoolean("candidate", true);

            writer.WriteEndObject();
        }

        private static void WriteLocation(Utf8JsonWriter writer, CodeLocation location)
        {
            writer.WriteString("file", location.File);
            writer.WriteNumber("line", location.Line);
            writer.WriteNumber("column", location.Column);
            writer.WriteString("text", location.Text);
        }

        private static string FormatLocation(CodeLocation location)
            => $"{location.File}:{location.Line}:{location.Column}";

        private static string GetMarks(ReferencePlace place)
        {
            var marks = new List<string>();
            if (place.Generated)
                marks.Add("генерированный");

            if (place.Implicit)
                marks.Add("неявная");

            if (place.Candidate)
                marks.Add("кандидат");

            return marks.Count == 0 ? "" : $"  [{string.Join(", ", marks)}]";
        }
    }
}

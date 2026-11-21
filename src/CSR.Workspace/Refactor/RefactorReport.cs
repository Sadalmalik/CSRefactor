using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Workspace.References;
using CSR.Workspace.Rename;

namespace CSR.Workspace.Refactor
{
    public static class RefactorReport
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string WriteJson(RefactorPlan plan, RefactorStatus status)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteString("status", status.ToString().ToLowerInvariant());
                writer.WriteString("command", plan.Command);
                writer.WriteString("subject", plan.Subject);
                writer.WriteString("target", plan.Target);
                if (plan.Refusal != null)
                    writer.WriteString("refusal", plan.Refusal);

                writer.WriteStartArray("files");
                foreach (var change in plan.Changes)
                {
                    writer.WriteStartObject();
                    writer.WriteString("file", change.File);
                    writer.WriteString("change", change.Kind.ToString().ToLowerInvariant());
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteString("diff", plan.Diff);
                writer.WriteStartArray("notes");
                foreach (var note in plan.Notes)
                    writer.WriteStringValue(note);

                writer.WriteEndArray();
                WriteCompile(writer, plan.Compile);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        // Сводка: в режиме диффа идет в stderr, stdout - только дифф
        public static string WriteSummary(RefactorPlan plan, RefactorStatus status)
        {
            var text = new StringBuilder();
            text.Append($"csr {plan.Command}: {plan.Subject} -> {plan.Target}: {GetStatusText(plan, status)}\n");
            foreach (var note in plan.Notes)
                text.Append($"  замечание: {note}\n");

            if (plan.Compile is not { } compile)
                return text.ToString();

            var seconds = compile.Time.TotalSeconds;
            text.Append($"  компиляция: проектов {compile.Projects}, ошибок до {compile.ErrorsBefore}");
            text.Append($", после {compile.ErrorsAfter}, новых {compile.NewErrors.Count} ({seconds:F1} с)\n");
            foreach (var error in compile.NewErrors)
                text.Append($"    {error.File}:{error.Line}:{error.Column}: {error.Text}\n");

            return text.ToString();
        }

        private static string GetStatusText(RefactorPlan plan, RefactorStatus status)
        {
            return status switch
            {
                RefactorStatus.Refused => $"отказ: {plan.Refusal}",
                RefactorStatus.Blocked => "не записано: новые ошибки компиляции (--force - записать все равно)",
                RefactorStatus.Applied => $"записано файлов {plan.Changes.Count}",
                _ => $"файлов {plan.Changes.Count}, без записи (--apply - записать)",
            };
        }

        private static void WriteCompile(Utf8JsonWriter writer, CompileResult? compile)
        {
            if (compile == null)
                return;

            writer.WriteStartObject("compile");
            writer.WriteNumber("projects", compile.Projects);
            writer.WriteNumber("errorsBefore", compile.ErrorsBefore);
            writer.WriteNumber("errorsAfter", compile.ErrorsAfter);
            writer.WriteNumber("seconds", Math.Round(compile.Time.TotalSeconds, 1));
            writer.WriteStartArray("newErrors");
            foreach (var error in compile.NewErrors)
                WriteLocation(writer, error);

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteLocation(Utf8JsonWriter writer, CodeLocation location)
        {
            writer.WriteStartObject();
            writer.WriteString("file", location.File);
            writer.WriteNumber("line", location.Line);
            writer.WriteNumber("column", location.Column);
            writer.WriteString("text", location.Text);
            writer.WriteEndObject();
        }
    }
}

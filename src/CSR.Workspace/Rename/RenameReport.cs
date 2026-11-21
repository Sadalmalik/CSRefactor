using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Workspace.References;

namespace CSR.Workspace.Rename
{
    // Итог rename: preview - дифф без записи; applied - записано; refused - отказ (сериализация);
    // blocked - --apply не выполнен (новые ошибки компиляции или строковые совпадения без --force)
    public enum RenameStatus
    {
        Preview,
        Applied,
        Refused,
        Blocked,
    }

    public static class RenameReport
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string WriteJson(RenamePlan plan, RenameStatus status)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteString("status", status.ToString().ToLowerInvariant());
                writer.WriteString("symbol", plan.Symbol);
                writer.WriteString("kind", plan.Kind);
                writer.WriteString("oldName", plan.OldName);
                writer.WriteString("newName", plan.NewName);
                WriteSerialized(writer, plan);
                writer.WriteStartArray("files");
                foreach (var change in plan.Changes)
                    writer.WriteStringValue(change.File);
                writer.WriteEndArray();
                writer.WriteString("diff", plan.Diff);
                WriteStrings(writer, plan.Strings);
                writer.WriteStartArray("notes");
                foreach (var note in plan.Notes)
                    writer.WriteStringValue(note);
                writer.WriteEndArray();
                WriteCompile(writer, plan.Compile);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        // Сводка для человека и агента: в режиме диффа идет в stderr, stdout - только дифф
        public static string WriteSummary(RenamePlan plan, RenameStatus status)
        {
            var text = new StringBuilder();
            text.Append($"csr rename: {plan.Symbol} -> {plan.NewName}: {GetStatusText(plan, status)}\n");
            foreach (var name in plan.Serialized)
            {
                var migration = name.CanMigrate ? GetMigrationText(name, plan.OldName) : name.MigrationProblem;
                text.Append($"  сериализация: {name.Reason}; {migration}\n");
            }

            if (plan.Strings is { Total: > 0 } strings)
            {
                text.Append($"  проверить вручную: строковых совпадений {strings.Total}");
                text.Append(strings.Total > strings.Matches.Count ? $", показано {strings.Matches.Count}\n" : "\n");
                foreach (var match in strings.Matches)
                    text.Append($"    {match.File}:{match.Line}:{match.Column}: {match.Text}\n");
            }

            if (plan.Strings is { SkippedFiles: > 0 })
                text.Append($"  не просмотрено файлов больше лимита: {plan.Strings.SkippedFiles}\n");

            foreach (var note in plan.Notes)
                text.Append($"  замечание: {note}\n");

            if (plan.Compile is { } compile)
            {
                var seconds = compile.Time.TotalSeconds;
                text.Append($"  компиляция: проектов {compile.Projects}, ошибок до {compile.ErrorsBefore}");
                text.Append($", после {compile.ErrorsAfter}, новых {compile.NewErrors.Count} ({seconds:F1} с)\n");
                foreach (var error in compile.NewErrors)
                    text.Append($"    {error.File}:{error.Line}:{error.Column}: {error.Text}\n");
            }

            return text.ToString();
        }

        private static string GetStatusText(RenamePlan plan, RenameStatus status)
        {
            return status switch
            {
                RenameStatus.Refused => plan.Migrated
                    ? "отказ: --migrate не переносит это имя"
                    : "отказ: имя сериализуется; --migrate сохранит старое имя для данных",
                RenameStatus.Blocked => plan.HasNewErrors
                    ? "не записано: новые ошибки компиляции (--force - записать все равно)"
                    : "не записано: есть строковые совпадения (проверить и повторить с --force)",
                RenameStatus.Applied => $"записано файлов {plan.Changes.Count}",
                _ => $"файлов {plan.Changes.Count}, без записи (--apply - записать)",
            };
        }

        private static string GetMigrationText(SerializedName name, string oldName)
        {
            return name.Kind switch
            {
                SerializedKind.UnityField when name.BackingField
                    => $"--migrate: [field: FormerlySerializedAs(\"<{oldName}>k__BackingField\")]",
                SerializedKind.UnityField => $"--migrate: [FormerlySerializedAs(\"{oldName}\")]",
                SerializedKind.Json => $"--migrate: [JsonProperty(\"{oldName}\")]",
                SerializedKind.DataMember => $"--migrate: [DataMember(Name = \"{oldName}\")]",
                _ => "",
            };
        }

        private static void WriteSerialized(Utf8JsonWriter writer, RenamePlan plan)
        {
            writer.WriteBoolean("migrate", plan.Migrated);
            writer.WriteStartArray("serialized");
            foreach (var name in plan.Serialized)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", name.Kind.ToString());
                writer.WriteString("reason", name.Reason);
                writer.WriteBoolean("canMigrate", name.CanMigrate);
                if (name.MigrationProblem != null)
                    writer.WriteString("problem", name.MigrationProblem);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteStrings(Utf8JsonWriter writer, StringScan? strings)
        {
            if (strings == null)
                return;

            writer.WriteStartObject("manual");
            writer.WriteNumber("total", strings.Total);
            writer.WriteNumber("scannedFiles", strings.ScannedFiles);
            writer.WriteNumber("skippedFiles", strings.SkippedFiles);
            writer.WriteStartArray("matches");
            foreach (var match in strings.Matches)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", match.Kind);
                WriteLocation(writer, match);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
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
            {
                writer.WriteStartObject();
                WriteLocation(writer, error);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteLocation(Utf8JsonWriter writer, CodeLocation location)
        {
            writer.WriteString("file", location.File);
            writer.WriteNumber("line", location.Line);
            writer.WriteNumber("column", location.Column);
            writer.WriteString("text", location.Text);
        }
    }
}

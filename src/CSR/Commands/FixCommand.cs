using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Fix;
using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Reports.Config;
using CSR.Reports.Diff;
using CSR.Reports.Checks;
using CSR.Reports.Style;

namespace CSR.Commands
{
    // csr fix: замены находок csr style применяются к файлам. По умолчанию - дифф на stdout без записи;
    // запись - --apply
    public static class FixCommand
    {
        public const string Usage = """
            csr fix <файлы | папки>... [--config <style.jsonc>]... [опции]

              --config, --project, --diff, --scope   как у csr style
              --checks <a,b,...>             только эти проверки
              --hints                        и замены уровня hint (по умолчанию - error и warning:
                                             coalesce-assignment и commented-code требуют суждения)
              --apply                        записать файлы (без него ничего не пишется)
              --format diff | json           diff (по умолчанию): дифф в stdout, сводка в stderr;
                                             json: исправлено, дифф и оставшиеся находки (как у csr style)
              --out <файл>                   записать дифф или json в файл вместо stdout

            Замены применяются проходами до устойчивого текста: за проход - непересекающиеся по строкам,
            затем файл разбирается заново. Оставшиеся находки - то, что правит агент.
            Коды возврата: 0 - после исправлений нет находок уровня error, 1 - есть, 2 - ошибка запуска
            """;

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly FileCommand Command = new()
        {
            Name = "fix",
            Options = ["--checks"],
            Flags = ["--apply", "--hints"],
            Formats = ["diff", "json"],
            HasScope = true,
            Analyze = Analyze,
        };

        public static int Run(IEnumerable<string> args)
            => FileCommands.Run(Command, args);

        private static FileResult Analyze(FileRun run)
        {
            var options = run.Configs.LoadChecks("style.jsonc", StyleConfig.Apply);
            var fix = CreateFixOptions(run.CommandLine);
            var results = Fix(run.Files, options, run.Project, fix, run.Changed);
            var apply = run.CommandLine.HasFlag("--apply");
            if (apply)
                Write(results);

            var remaining = results.SelectMany(result => result.Fix.Remaining).ToList();
            var report = new CheckReport { FileCount = run.Files.Count, Scope = run.Scope, Findings = remaining };
            var patch = string.Concat(results.Select(result => result.Diff));
            return new FileResult
            {
                Text = run.Format == "json" ? WriteJson(results, patch, report, apply) : patch,
                Summary = GetSummary(results, report, apply),
                Details = GetDetails(results),
                ExitCode = report.HasErrors ? 1 : 0,
            };
        }

        private static List<FileFix> Fix(
            List<string> files,
            CheckOptions options,
            ProjectOptions project,
            FixOptions fix,
            Dictionary<string, ChangedLines>? changed)
        {
            var results = new ConcurrentBag<FileFix>();
            Parallel.ForEach(files, file =>
            {
                var source = File.ReadAllText(file);
                var display = SourceFiles.GetDisplayPath(file);
                var lines = changed?[file];
                var result = SyntaxFixer.Fix(source, display, options, project, fix, lines);
                var diff = LineDiff.Build(display.Replace('\\', '/'), source, result.Text);
                results.Add(new FileFix(file, result, diff, result.Text != source));
            });

            return results.OrderBy(result => result.Path, StringComparer.Ordinal).ToList();
        }

        // Запись только измененных файлов, с прежней кодировкой и BOM
        private static void Write(List<FileFix> results)
        {
            foreach (var result in results.Where(item => item.IsChanged))
                File.WriteAllText(result.Path, result.Fix.Text, DetectEncoding(result.Path));
        }

        private static string WriteJson(List<FileFix> results, string patch, CheckReport report, bool apply)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, JsonOptions))
            {
                writer.WriteStartObject();
                writer.WriteBoolean("applied", apply);
                writer.WriteStartArray("fixed");
                foreach (var group in GroupApplied(results))
                {
                    writer.WriteStartObject();
                    writer.WriteString("check", group.Check);
                    if (group.Rule != null)
                        writer.WriteString("rule", group.Rule);

                    writer.WriteNumber("count", group.Count);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("files");
                foreach (var result in results.Where(item => item.IsChanged))
                    writer.WriteStringValue(SourceFiles.GetDisplayPath(result.Path).Replace('\\', '/'));
                writer.WriteEndArray();
                writer.WriteString("diff", patch);
                writer.WriteStartArray("reverted");
                foreach (var result in results.Where(item => item.Fix.Reverted))
                    writer.WriteStringValue(SourceFiles.GetDisplayPath(result.Path).Replace('\\', '/'));
                writer.WriteEndArray();
                writer.WritePropertyName("remaining");
                writer.WriteRawValue(StyleJsonWriter.Write(report));
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        }

        private static string GetSummary(List<FileFix> results, CheckReport report, bool apply)
        {
            var fixedCount = GroupApplied(results).Sum(group => group.Count);
            var files = results.Count(result => result.IsChanged);
            var errors = report.Findings.Count(finding => finding.Severity == Severity.Error);
            var write = apply ? "записано" : "без записи (--apply - записать)";
            var remaining = $"осталось находок {report.Findings.Count} (error {errors})";
            return $"исправлено {fixedCount} в {files} файлах, {write}; {remaining}";
        }

        // Группы исправлений и файлы, оставленные без исправлений
        private static string GetDetails(List<FileFix> results)
        {
            var text = new StringBuilder();
            foreach (var group in GroupApplied(results))
                text.Append($"  {group.Check} ({group.Rule ?? "-"}): {group.Count}\n");

            foreach (var result in results.Where(item => item.Fix.Reverted))
            {
                var file = SourceFiles.GetDisplayPath(result.Path);
                text.Append($"  не исправлен (замены дают синтаксические ошибки - ошибка проверки): {file}\n");
            }

            return text.ToString();
        }

        private static List<AppliedGroup> GroupApplied(List<FileFix> results)
        {
            return results
                .SelectMany(result => result.Fix.Applied)
                .GroupBy(finding => (finding.Check, finding.Rule))
                .Select(group => new AppliedGroup(group.Key.Check, group.Key.Rule, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Check, StringComparer.Ordinal)
                .ToList();
        }

        private static FixOptions CreateFixOptions(CommandLine commandLine)
        {
            var checks = commandLine.GetOption("--checks");
            var split = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
            return new FixOptions
            {
                MinSeverity = commandLine.HasFlag("--hints") ? Severity.Hint : Severity.Warning,
                Checks = checks?.Split(',', split).ToHashSet(),
            };
        }

        private static Encoding DetectEncoding(string path)
        {
            var preamble = new byte[3];
            using (var stream = File.OpenRead(path))
            {
                var read = stream.Read(preamble, 0, 3);
                if (read == 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
                    return new UTF8Encoding(true);
            }

            return new UTF8Encoding(false);
        }

        // IsChanged - по тексту: дифф не видит смену одних переводов строк
        private record FileFix(string Path, FixResult Fix, string Diff, bool IsChanged);

        private record AppliedGroup(string Check, string? Rule, int Count);
    }
}

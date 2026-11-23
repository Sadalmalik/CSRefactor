using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Fix;
using CSR.Core.Checks;
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

            Замены применяются проходами до устойчивого текста: за проход - непересекающиеся по строкам,
            затем файл разбирается заново. Оставшиеся находки - то, что правит агент.
            Коды возврата: 0 - после исправлений нет находок уровня error, 1 - есть, 2 - ошибка запуска
            """;

        private static readonly JsonWriterOptions JsonOptions = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly HashSet<string> OptionNames =
            ["--config", "--project", "--diff", "--scope", "--format", "--checks"];
        private static readonly HashSet<string> FlagNames = ["--apply", "--hints"];

        public static int Run(IEnumerable<string> args)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки");

            var format = commandLine.GetOption("--format") ?? "diff";
            if (format is not ("diff" or "json"))
                throw new UsageException($"неизвестный формат '{format}'");

            var options = StyleCommand.LoadOptions(commandLine);
            var project = StyleCommand.LoadProject(commandLine);
            var fix = CreateFixOptions(commandLine);
            var diff = commandLine.GetOption("--diff");
            var scope = commandLine.GetOption("--scope") ?? (diff == null ? "file" : "changed-lines");
            if (scope is not ("file" or "changed-lines"))
                throw new UsageException($"неизвестный режим '{scope}'");

            var files = SourceFiles.Collect(commandLine.Positional);
            Dictionary<string, ChangedLines>? changed = null;
            if (scope == "changed-lines")
            {
                changed = GitDiff.Collect(diff ?? "HEAD", commandLine.Positional[0]);
                files = files.Where(changed.ContainsKey).ToList();
            }

            var stopwatch = Stopwatch.StartNew();
            var results = Fix(files, options, project, fix, changed);
            var apply = commandLine.HasFlag("--apply");
            if (apply)
                Write(results);

            var remaining = results.SelectMany(result => result.Fix.Remaining).ToList();
            var report = new CheckReport { FileCount = files.Count, Scope = scope, Findings = remaining };
            var patch = string.Concat(results.Select(result => result.Diff));
            if (format == "json")
                Console.Out.Write(WriteJson(results, patch, report, apply));
            else
                Console.Out.Write(patch);

            Console.Error.Write(WriteSummary(results, report, apply, stopwatch.Elapsed));
            return report.HasErrors ? 1 : 0;
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
                results.Add(new FileFix(file, result, diff));
            });

            return results.OrderBy(result => result.Path, StringComparer.Ordinal).ToList();
        }

        // Запись только измененных файлов, с прежней кодировкой и BOM
        private static void Write(List<FileFix> results)
        {
            foreach (var result in results.Where(item => item.Diff.Length > 0))
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
                foreach (var result in results.Where(item => item.Diff.Length > 0))
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

        private static string WriteSummary(List<FileFix> results, CheckReport report, bool apply, TimeSpan time)
        {
            var groups = GroupApplied(results);
            var fixedCount = groups.Sum(group => group.Count);
            var files = results.Count(result => result.Diff.Length > 0);
            var errors = report.Findings.Count(finding => finding.Severity == Severity.Error);
            var text = new StringBuilder();
            var write = apply ? "записано" : "без записи (--apply - записать)";
            text.Append($"csr fix: исправлено {fixedCount} в {files} файлах, {write}; ");
            text.Append($"осталось находок {report.Findings.Count} (error {errors}), {time.TotalSeconds:F1} с\n");
            foreach (var group in groups)
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

        private record FileFix(string Path, FixResult Fix, string Diff);

        private record AppliedGroup(string Check, string? Rule, int Count);
    }
}

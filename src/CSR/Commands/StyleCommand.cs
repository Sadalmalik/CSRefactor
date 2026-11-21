using System.Collections.Concurrent;
using System.Diagnostics;
using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Reports.Checks;
using CSR.Reports.Config;
using CSR.Reports.Style;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Commands
{
    // csr style: находки стиля по синтаксису файлов. Ничего не пишет рядом с кодом
    public static class StyleCommand
    {
        public const string Usage = """
            csr style <файлы | папки>... --config <style.jsonc>... [опции]

              --config <style.jsonc>         проверки, уровни, метки правил, параметры; несколько - накладываются
              --project <project.jsonc>      символы препроцессора, методы логгера
              --diff <ревизия | файл.diff>   измененные строки: git diff -U0 от ревизии или готовый дифф
              --scope file | changed-lines   все находки файла или только на измененных строках
                                             (без --diff - file; с --diff - changed-lines; changed-lines без --diff - от HEAD)
              --format json | md             формат отчета (по умолчанию json)
              --out <файл>                   записать отчет в файл вместо stdout

            Коды возврата: 0 - нет находок уровня error, 1 - есть, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames =
            ["--config", "--project", "--diff", "--scope", "--format", "--out"];

        public static int Run(IEnumerable<string> args)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, new HashSet<string>());
            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки");

            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "md"))
                throw new UsageException($"неизвестный формат '{format}'");

            var options = LoadOptions(commandLine);
            var project = LoadProject(commandLine);
            var diff = commandLine.GetOption("--diff");
            var scope = commandLine.GetOption("--scope") ?? (diff == null ? "file" : "changed-lines");
            if (scope is not ("file" or "changed-lines"))
                throw new UsageException($"неизвестный режим '{scope}'");

            var files = SourceFiles.Collect(commandLine.Positional);
            Dictionary<string, ChangedLines>? changed = null;
            if (diff != null || scope == "changed-lines")
            {
                changed = GitDiff.Collect(diff ?? "HEAD", commandLine.Positional[0]);
                files = files.Where(changed.ContainsKey).ToList();
            }

            var stopwatch = Stopwatch.StartNew();
            var filter = scope == "changed-lines" ? changed : null;
            var findings = Analyze(files, options, project, filter);
            var report = new CheckReport { FileCount = files.Count, Scope = scope, Findings = findings };
            var text = format == "md" ? StyleMarkdownWriter.Write(report) : StyleJsonWriter.Write(report);
            Output.Write(text, commandLine.GetOption("--out"));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr style: файлов {files.Count}, находок {findings.Count}, {seconds:F1} с");
            return report.HasErrors ? 1 : 0;
        }

        internal static List<Finding> Analyze(
            List<string> files,
            CheckOptions options,
            ProjectOptions project,
            Dictionary<string, ChangedLines>? filter)
        {
            var results = new ConcurrentBag<List<Finding>>();
            Parallel.ForEach(files, file => results.Add(AnalyzeFile(file, options, project, filter)));
            return results
                .SelectMany(findings => findings)
                .OrderBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ThenBy(finding => finding.Column)
                .ToList();
        }

        private static List<Finding> AnalyzeFile(
            string file,
            CheckOptions options,
            ProjectOptions project,
            Dictionary<string, ChangedLines>? filter)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, project.ParseOptions, SourceFiles.GetDisplayPath(file));
            var findings = SyntaxAnalyzer.Analyze(tree, options, project);
            if (filter == null)
                return findings;

            var lines = filter[file];
            return findings.Where(lines.Intersects).ToList();
        }

        private static CheckOptions LoadOptions(CommandLine commandLine)
        {
            var paths = commandLine.GetOptions("--config");
            if (paths.Count == 0)
                throw new UsageException("не задан --config: без конфига все проверки выключены");

            var options = new CheckOptions();
            foreach (var path in paths)
                StyleConfig.Apply(ReadConfig(path), options);

            return options;
        }

        internal static ProjectOptions LoadProject(CommandLine commandLine)
        {
            var path = commandLine.GetOption("--project");
            return path == null ? new ProjectOptions() : ProjectConfig.Parse(ReadConfig(path));
        }

        internal static string ReadConfig(string path)
        {
            if (!File.Exists(path))
                throw new UsageException($"нет файла конфига: {path}");

            return File.ReadAllText(path);
        }
    }
}

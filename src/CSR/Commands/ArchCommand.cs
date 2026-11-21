using System.Diagnostics;
using CSR.Core.Style;
using CSR.Reports.Arch;
using CSR.Reports.Style;

namespace CSR.Commands
{
    // csr arch: архитектурные признаки по синтаксису файлов - находки-решения с вопросом. Ничего не пишет рядом с кодом
    public static class ArchCommand
    {
        public const string Usage = """
            csr arch <файлы | папки>... --config <arch.jsonc>... [опции]

              --config <arch.jsonc>          признаки, уровни, метки правил, параметры; несколько - накладываются
              --project <project.jsonc>      символы препроцессора
              --diff <ревизия | файл.diff>   измененные строки: git diff -U0 от ревизии или готовый дифф
              --scope file | changed-lines   все находки файла или только на измененных строках
                                             (без --diff - file; с --diff - changed-lines; changed-lines без --diff - от HEAD)
              --format json | md             формат отчета (по умолчанию json)
              --out <файл>                   записать отчет в файл вместо stdout

            Признаки по синтаксису одного файла; признаки по всему решению - позже.
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
            var project = StyleCommand.LoadProject(commandLine);
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
            var findings = StyleCommand.Analyze(files, options, project, filter);
            var report = new StyleReport { FileCount = files.Count, Scope = scope, Findings = findings };
            var text = format == "md" ? ArchMarkdownWriter.Write(report) : ArchJsonWriter.Write(report);
            Output.Write(text, commandLine.GetOption("--out"));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr arch: файлов {files.Count}, находок {findings.Count}, {seconds:F1} с");
            return report.HasErrors ? 1 : 0;
        }

        private static StyleOptions LoadOptions(CommandLine commandLine)
        {
            var paths = commandLine.GetOptions("--config");
            if (paths.Count == 0)
                throw new UsageException("не задан --config: без конфига все признаки выключены");

            var options = new StyleOptions();
            foreach (var path in paths)
                ArchConfig.Apply(StyleCommand.ReadConfig(path), options);

            return options;
        }
    }
}

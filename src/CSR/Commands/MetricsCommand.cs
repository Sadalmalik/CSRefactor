using System.Collections.Concurrent;
using System.Diagnostics;
using CSR.Core.Config;
using CSR.Core.Metrics;
using CSR.Reports.Config;
using CSR.Reports.Metrics;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Commands
{
    // csr metrics: метрики по синтаксису файлов, отчет JSON или Markdown. Ничего не пишет рядом с кодом
    public static class MetricsCommand
    {
        public const string Usage = """
            csr metrics <файлы | папки>... [опции]

              --config <metrics.jsonc>   пороги, включение метрик, размер топа
              --project <project.jsonc>  символы препроцессора (по умолчанию UNITY_EDITOR)
              --format json | md         формат отчета (по умолчанию json)
              --top <N>                  размер топа по каждой метрике (по умолчанию 10)
              --all                      в JSON — все функции и типы по файлам
              --out <файл>               записать отчет в файл вместо stdout
            """;

        private static readonly HashSet<string> OptionNames = ["--config", "--project", "--format", "--top", "--out"];
        private static readonly HashSet<string> FlagNames = ["--all"];

        public static int Run(IEnumerable<string> args)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки");

            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "md"))
                throw new UsageException($"неизвестный формат '{format}'");

            var options = LoadOptions(commandLine);
            var project = LoadProject(commandLine);
            var files = SourceFiles.Collect(commandLine.Positional);

            var stopwatch = Stopwatch.StartNew();
            var results = Measure(files, project.ParseOptions);
            var report = MetricsReport.Build(results, options);
            var text = format == "md"
                ? MetricsMarkdownWriter.Write(report)
                : MetricsJsonWriter.Write(report, commandLine.HasFlag("--all"));

            Output.Write(text, commandLine.GetOption("--out"));
            double seconds = stopwatch.Elapsed.TotalSeconds;
            int functions = report.FunctionCount;
            Console.Error.WriteLine($"csr metrics: файлов {files.Count}, функций {functions}, {seconds:F1} с");
            return 0;
        }

        private static List<FileMetrics> Measure(List<string> files, CSharpParseOptions parseOptions)
        {
            var results = new ConcurrentBag<FileMetrics>();
            Parallel.ForEach(files, file => results.Add(MeasureFile(file, parseOptions)));
            return results.OrderBy(result => result.File.File, StringComparer.Ordinal).ToList();
        }

        private static FileMetrics MeasureFile(string file, CSharpParseOptions parseOptions)
        {
            var source = File.ReadAllText(file);
            return MetricsCalculator.Calculate(source, SourceFiles.GetDisplayPath(file), parseOptions);
        }

        private static MetricsOptions LoadOptions(CommandLine commandLine)
        {
            string? path = commandLine.GetOption("--config");
            var options = path == null ? new MetricsOptions() : MetricsConfig.Parse(ReadConfig(path));

            string? top = commandLine.GetOption("--top");
            if (top == null)
                return options;

            if (!int.TryParse(top, out int count) || count < 0)
                throw new UsageException($"--top: ожидается неотрицательное число, получено '{top}'");

            options.Top = count;
            return options;
        }

        private static ProjectOptions LoadProject(CommandLine commandLine)
        {
            string? path = commandLine.GetOption("--project");
            return path == null ? new ProjectOptions() : ProjectConfig.Parse(ReadConfig(path));
        }

        private static string ReadConfig(string path)
        {
            if (!File.Exists(path))
                throw new UsageException($"нет файла конфига: {path}");

            return File.ReadAllText(path);
        }
    }
}

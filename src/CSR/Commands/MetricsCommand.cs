using System.Collections.Concurrent;
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

              --config <metrics.jsonc>   пороги, включение метрик, размер топа; без --config - .csr/metrics.jsonc
                                         в папке цели или выше, иначе config/metrics.jsonc рядом с csr.exe
              --project <project.jsonc>  символы препроцессора (по умолчанию UNITY_EDITOR); поиск - как у --config
              --format json | md         формат отчета (по умолчанию json)
              --top <N>                  размер топа по каждой метрике (по умолчанию 10); 0 - все
              --all                      в JSON - все функции и типы по файлам
              --coverage <opencover.xml> покрытие (OpenCover XML): покрытие и CRAP функций, топ по CRAP;
                                         несколько файлов объединяются
              --edn <файл>               записать снимок crap.edn в формате crapper (без --coverage - :coverage nil)
              --out <файл>               записать отчет в файл вместо stdout
            """;

        private static readonly FileCommand Command = new()
        {
            Name = "metrics",
            Options = ["--top", "--coverage", "--edn"],
            Flags = ["--all"],
            Analyze = Analyze,
        };

        public static int Run(IEnumerable<string> args)
            => FileCommands.Run(Command, args);

        private static FileResult Analyze(FileRun run)
        {
            var config = run.Configs.Read("metrics.jsonc");
            var options = config == null ? new MetricsOptions() : MetricsConfig.Parse(config);
            options.Top = run.CommandLine.ReadCount("--top") ?? options.Top;
            var coverage = LoadCoverage(run.CommandLine.GetOptions("--coverage"));
            var results = Measure(run.Files, run.Project.ParseOptions, coverage);
            var report = MetricsReport.Build(results, options);
            var edn = run.CommandLine.GetOption("--edn");
            if (edn != null)
                Output.Write(CrapEdnWriter.Write(results), edn);

            return new FileResult
            {
                Text = run.Format == "md"
                    ? MetricsMarkdownWriter.Write(report)
                    : MetricsJsonWriter.Write(report, run.CommandLine.HasFlag("--all")),
                Summary = $"файлов {run.Files.Count}, функций {report.FunctionCount}",
            };
        }

        private static List<FileMetrics> Measure(
            List<string> files,
            CSharpParseOptions parseOptions,
            Dictionary<string, FileCoverage>? coverage)
        {
            var results = new ConcurrentBag<FileMetrics>();
            Parallel.ForEach(files, file => results.Add(MeasureFile(file, parseOptions, coverage)));
            return results.OrderBy(result => result.File.File, StringComparer.Ordinal).ToList();
        }

        private static FileMetrics MeasureFile(
            string file,
            CSharpParseOptions parseOptions,
            Dictionary<string, FileCoverage>? coverage)
        {
            var source = File.ReadAllText(file);
            var metrics = MetricsCalculator.Calculate(source, SourceFiles.GetDisplayPath(file), parseOptions);
            if (coverage == null)
                return metrics;

            coverage.TryGetValue(OpenCoverReader.NormalizePath(file), out FileCoverage? fileCoverage);
            Crap.Apply(metrics, fileCoverage);
            return metrics;
        }

        private static Dictionary<string, FileCoverage>? LoadCoverage(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
                return null;

            var coverage = new Dictionary<string, FileCoverage>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                    throw new UsageException($"нет файла покрытия: {path}");

                try
                {
                    OpenCoverReader.Read(File.ReadAllText(path), coverage);
                }
                catch (System.Xml.XmlException exception)
                {
                    throw new UsageException($"покрытие {path}: {exception.Message}");
                }
            }

            return coverage;
        }
    }
}

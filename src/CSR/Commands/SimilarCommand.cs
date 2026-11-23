using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using CSR.Core.Arch;
using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Core.Graph;
using CSR.Core.Similar;
using CSR.Reports.Config;
using CSR.Reports.Similar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Commands
{
    // csr similar: похожий код по синтаксису файлов - пары и кластеры членов, фрагментов, наборов параметров и полей,
    // типов. Ничего не пишет рядом с кодом
    public static class SimilarCommand
    {
        public const string Usage = """
            csr similar <файлы | папки>... [--config <similar.jsonc>] [опции]

              --config <similar.jsonc>       порог, минимумы, окно фрагмента, paths / excludePaths; без --config -
                                             .csr/similar.jsonc в папке цели или выше, иначе config/similar.jsonc
                                             рядом с csr.exe, иначе значения по умолчанию
              --project <project.jsonc>      символы препроцессора; поиск - как у style
              --changed                      только пары с измененным кодом (git diff от HEAD и новые файлы)
                                             против всего кода цели
              --diff <ревизия | файл.diff>   измененные строки для --changed: git diff -U0 от ревизии или готовый дифф
              --kinds <виды>                 через запятую: member, fragment, parameters, fields, type, system
                                             (по умолчанию все, кроме system: системам нужна компиляция файлов)
              --threshold <0..1>             порог оценки членов и фрагментов, L1 (по умолчанию 0.82)
              --set-threshold <0..1>         порог наборов параметров и полей, L2 (по умолчанию 0.5)
              --min-shared <N>               наименьшее число общих элементов пары наборов (по умолчанию 3)
              --type-threshold <0..1>        порог типов, L3 (по умолчанию 0.75)
              --system-threshold <0..1>      порог систем, L4 (по умолчанию 0.5)
              --min-lines <N>                наименьшее число строк единицы (по умолчанию 4)
              --min-nodes <N>                наименьшее число узлов формы (по умолчанию 20)
              --min-shapes <N>               наименьшее число разных поддеревьев формы (по умолчанию 20)
              --fragments <N>                операторов в окне фрагмента; 0 - без фрагментов (по умолчанию 4)
              --top <N>                      в md - первые N кластеров (с --changed - пар); 0 - все
              --format json | md             формат отчета (по умолчанию json)
              --out <файл>                   записать отчет в файл вместо stdout

            Коды возврата: 0 - успех (пары не считаются ошибкой), 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames =
        [
            "--config",
            "--project",
            "--diff",
            "--kinds",
            "--threshold",
            "--set-threshold",
            "--min-shared",
            "--type-threshold",
            "--system-threshold",
            "--min-lines",
            "--min-nodes",
            "--min-shapes",
            "--fragments",
            "--top",
            "--format",
            "--out",
        ];

        private static readonly HashSet<string> FlagNames = ["--changed"];

        public static int Run(IEnumerable<string> args)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки");

            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "md"))
                throw new UsageException($"неизвестный формат '{format}'");

            var options = LoadOptions(commandLine);
            var project = StyleCommand.LoadProject(commandLine);
            int top = ReadInt(commandLine, "--top") ?? 0;
            var diff = commandLine.GetOption("--diff");
            bool isChanged = commandLine.HasFlag("--changed") || diff != null;

            var stopwatch = Stopwatch.StartNew();
            var files = SourceFiles.Collect(commandLine.Positional).Where(file => IsInScope(file, options)).ToList();
            Func<SimilarUnit, bool>? isProbe = null;
            if (isChanged)
                isProbe = GetChangedFilter(GitDiff.Collect(diff ?? "HEAD", commandLine.Positional[0]));

            var units = Collect(files, options, project);
            var result = SimilarSearch.Run(units, options, isProbe);
            var report = new SimilarReport
            {
                FileCount = files.Count,
                UnitCounts = units.GroupBy(unit => unit.Kind).ToDictionary(group => group.Key, group => group.Count()),
                Options = options,
                IsChanged = isChanged,
                Result = result,
            };
            var text = format == "md" ? SimilarMarkdownWriter.Write(report, top) : SimilarJsonWriter.Write(report);
            Output.Write(text, commandLine.GetOption("--out"));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr similar: файлов {files.Count}, единиц {units.Count}, пар {result.Pairs.Count}, "
                + $"кластеров {result.Clusters.Count}, {seconds:F1} с");
            return 0;
        }

        // Единицы L1-L3 по деревьям файлов; системы L4 - по графу типов компиляции этих деревьев
        internal static List<SimilarUnit> Collect(List<string> files, SimilarOptions options, ProjectOptions project)
        {
            var trees = new ConcurrentBag<SyntaxTree>();
            var results = new ConcurrentBag<List<SimilarUnit>>();
            Parallel.ForEach(files, file =>
            {
                var source = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(source, project.ParseOptions, SourceFiles.GetDisplayPath(file));
                trees.Add(tree);
                results.Add(SimilarUnits.Collect(tree, options));
            });

            var units = results.SelectMany(list => list).ToList();
            if (!options.Kinds.Contains(UnitKind.System))
                return units;

            var sorted = trees.OrderBy(tree => tree.FilePath, StringComparer.Ordinal).ToList();
            var compilation = SourceCompilation.Create(sorted);
            var graph = TypeGraphBuilder.Build([compilation], sorted, path => path, CancellationToken.None);
            units.AddRange(SystemUnits.Collect(graph, options));
            return units;
        }

        // Путь от текущей папки проверяется и с ведущим /: маска */Tests/* ловит и папку верхнего уровня
        private static bool IsInScope(string file, SimilarOptions options)
        {
            var path = SourceFiles.GetDisplayPath(file);
            var paths = NameMask.Get(options.Paths, true);
            var excluded = NameMask.Get(options.ExcludePaths, true);
            bool isIncluded = paths.IsEmpty || paths.IsMatch(path) || paths.IsMatch("/" + path);
            return isIncluded && !excluded.IsMatch(path) && !excluded.IsMatch("/" + path);
        }

        // Единица изменена, если задета хотя бы одна ее строка
        private static Func<SimilarUnit, bool> GetChangedFilter(Dictionary<string, ChangedLines> changed)
        {
            var byDisplayPath = changed.ToDictionary(pair => SourceFiles.GetDisplayPath(pair.Key), pair => pair.Value);
            return unit =>
            {
                if (!byDisplayPath.TryGetValue(unit.File, out ChangedLines? lines))
                    return false;

                for (int line = unit.StartLine; line <= unit.EndLine; line++)
                {
                    if (lines.Contains(line))
                        return true;
                }

                return false;
            };
        }

        private static SimilarOptions LoadOptions(CommandLine commandLine)
        {
            var start = ConfigFiles.GetStart(commandLine.Positional[0], Environment.CurrentDirectory);
            var path = ConfigFiles.Find(commandLine.GetOption("--config"), "similar.jsonc", start);
            var options = path == null ? new SimilarOptions() : SimilarConfig.Parse(StyleCommand.ReadConfig(path));

            var kinds = commandLine.GetOption("--kinds");
            if (kinds != null)
                options.Kinds = ParseKinds(kinds);

            options.Threshold = ReadThreshold(commandLine, "--threshold") ?? options.Threshold;
            options.SetThreshold = ReadThreshold(commandLine, "--set-threshold") ?? options.SetThreshold;
            options.TypeThreshold = ReadThreshold(commandLine, "--type-threshold") ?? options.TypeThreshold;
            options.SystemThreshold = ReadThreshold(commandLine, "--system-threshold") ?? options.SystemThreshold;
            options.MinShared = ReadInt(commandLine, "--min-shared") ?? options.MinShared;

            options.MinLines = ReadInt(commandLine, "--min-lines") ?? options.MinLines;
            options.MinNodes = ReadInt(commandLine, "--min-nodes") ?? options.MinNodes;
            options.MinShapes = ReadInt(commandLine, "--min-shapes") ?? options.MinShapes;
            options.FragmentStatements = ReadInt(commandLine, "--fragments") ?? options.FragmentStatements;
            return options;
        }

        private static HashSet<UnitKind> ParseKinds(string text)
        {
            var kinds = new HashSet<UnitKind>();
            foreach (var key in text.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0))
            {
                var kind = UnitKinds.Find(key);
                if (kind == null)
                    throw new UsageException(
                        $"--kinds: неизвестный вид '{key}'; допустимо {string.Join(", ", UnitKinds.AllKeys)}");

                kinds.Add(kind.Value);
            }

            return kinds;
        }

        private static double? ReadThreshold(CommandLine commandLine, string name)
        {
            var text = commandLine.GetOption(name);
            if (text == null)
                return null;

            bool isNumber = double.TryParse(
                text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value);
            if (!isNumber || value is < 0 or > 1)
                throw new UsageException($"{name}: ожидается число от 0 до 1, задано '{text}'");

            return value;
        }

        private static int? ReadInt(CommandLine commandLine, string name)
        {
            var text = commandLine.GetOption(name);
            if (text == null)
                return null;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
                throw new UsageException($"{name}: ожидается неотрицательное целое, задано '{text}'");

            return value;
        }
    }
}

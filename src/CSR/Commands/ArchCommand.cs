using System.Diagnostics;
using CSR.Core.Arch;
using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Reports.Arch;
using CSR.Reports.Checks;
using CSR.Reports.Config;
using Microsoft.CodeAnalysis;

namespace CSR.Commands
{
    // csr arch: архитектурные признаки - находки-решения с вопросом. Режим файлов - признаки по синтаксису одного
    // файла; режим решения (--solution, --projects) - еще и признаки по всему решению. Ничего не пишет рядом с кодом
    public static class ArchCommand
    {
        public const string Usage = """
            csr arch <файлы | папки>... --config <arch.jsonc>... [опции]
            csr arch --solution <файл> [--projects <маска>]... --config <arch.jsonc>... [опции]

              --config <arch.jsonc>          признаки, уровни, метки правил, параметры; несколько - накладываются
              --project <project.jsonc>      символы препроцессора, логгер, механизмы проекта
              --diff <ревизия | файл.diff>   измененные строки: git diff -U0 от ревизии или готовый дифф
              --scope file | changed-lines   все находки файла или только на измененных строках
                                             (без --diff - file; с --diff - changed-lines; changed-lines без --diff - от HEAD)
              --format json | md             формат отчета (по умолчанию json)
              --out <файл>                   записать отчет в файл вместо stdout

            Режим решения: признаки по файлам и по всему решению на проектах решения
              --solution <файл>              .sln, .slnx или .csproj
              --projects <маска>             проекты по маске имени (* - любые символы); несколько - объединяются;
                                             без --projects - все проекты решения
              --no-server                    не обращаться к csr serve, загрузить решение в процессе

            Коды возврата: 0 - нет находок уровня error, 1 - есть, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames =
            ["--config", "--project", "--diff", "--scope", "--format", "--out", "--solution", "--projects"];

        private static readonly HashSet<string> FlagNames = ["--no-server"];

        public static int Run(IEnumerable<string> args)
        {
            var arguments = args.ToList();
            var commandLine = CommandLine.Parse(arguments, OptionNames, FlagNames);
            if (commandLine.GetOption("--solution") != null || commandLine.GetOptions("--projects").Count > 0)
                return WorkspaceCommands.Run("arch", arguments, OptionNames, FlagNames);

            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки (или --solution)");

            var format = GetFormat(commandLine);
            var directory = Environment.CurrentDirectory;
            var options = LoadOptions(commandLine, directory);
            var project = LoadProject(commandLine, directory);
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
            var report = new CheckReport { FileCount = files.Count, Scope = scope, Findings = findings };
            Output.Write(Write(report, format), commandLine.GetOption("--out"));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr arch: файлов {files.Count}, находок {findings.Count}, {seconds:F1} с");
            return report.HasErrors ? 1 : 0;
        }

        // Режим решения: в процессе CLI или на сервере csr serve
        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count > 0)
                throw new UsageException("arch --solution: файлы не задаются, проекты - через --projects");

            if (commandLine.GetOption("--diff") != null || commandLine.GetOption("--scope") is "changed-lines")
                throw new UsageException("arch --solution: режим changed-lines не поддерживается");

            var format = GetFormat(commandLine);
            var directory = context.WorkingDirectory;
            var options = LoadOptions(commandLine, directory);
            var project = LoadProject(commandLine, directory);

            var stopwatch = Stopwatch.StartNew();
            var compilations = await GetCompilationsAsync(context, commandLine.GetOptions("--projects"), cancellation);
            var trees = ArchSolutionAnalyzer.GetTrees(compilations);
            var findings = ArchSolutionAnalyzer.Analyze(
                compilations,
                options,
                project,
                path => GetDisplayPath(path, directory),
                cancellation);
            var report = new CheckReport { FileCount = trees.Count, Scope = "solution", Findings = findings };
            var text = Write(report, format);
            var output = commandLine.GetOption("--out");
            if (output == null)
                context.Output.Write(text);
            else
                Output.Write(text, Path.GetFullPath(Path.Combine(directory, output)));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            var summary = $"проектов {compilations.Count}, файлов {trees.Count}, находок {findings.Count}";
            context.Error.WriteLine($"csr arch: {summary}, {seconds:F1} с");
            return report.HasErrors ? 1 : 0;
        }

        private static async Task<List<Compilation>> GetCompilationsAsync(
            WorkspaceContext context,
            IReadOnlyList<string> projectMasks,
            CancellationToken cancellation)
        {
            var mask = NameMask.Get(projectMasks, true);
            var projects = context.Solution.Current.Projects
                .Where(project => mask.IsEmpty || mask.IsMatch(project.Name))
                .ToList();
            if (projects.Count == 0)
                throw new UsageException($"нет проектов по маске: {string.Join(", ", projectMasks)}");

            var compilations = new List<Compilation>();
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync(cancellation);
                if (compilation != null)
                    compilations.Add(compilation);
            }

            return compilations;
        }

        private static string GetFormat(CommandLine commandLine)
        {
            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "md"))
                throw new UsageException($"неизвестный формат '{format}'");

            return format;
        }

        private static string Write(CheckReport report, string format)
            => format == "md" ? ArchMarkdownWriter.Write(report) : ArchJsonWriter.Write(report);

        private static CheckOptions LoadOptions(CommandLine commandLine, string directory)
        {
            var paths = commandLine.GetOptions("--config");
            if (paths.Count == 0)
                throw new UsageException("не задан --config: без конфига все признаки выключены");

            var options = new CheckOptions();
            foreach (var path in paths)
                ArchConfig.Apply(ReadConfig(path, directory), options);

            return options;
        }

        private static ProjectOptions LoadProject(CommandLine commandLine, string directory)
        {
            var path = commandLine.GetOption("--project");
            return path == null ? new ProjectOptions() : ProjectConfig.Parse(ReadConfig(path, directory));
        }

        // Путь конфига - от папки вызова (на сервере текущая папка процесса другая)
        private static string ReadConfig(string path, string directory)
            => StyleCommand.ReadConfig(Path.GetFullPath(Path.Combine(directory, path)));

        // Путь для отчета: от папки вызова, если файл внутри нее, иначе полный; разделитель - '/'
        private static string GetDisplayPath(string fullPath, string directory)
        {
            var relative = Path.GetRelativePath(directory, fullPath);
            bool isOutside = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);
            return (isOutside ? fullPath : relative).Replace('\\', '/');
        }
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Commands
{
    // Файловая команда анализа: имя, свои опции, форматы и анализ
    public class FileCommand
    {
        public required string Name { get; init; }

        // Свои опции и флаги сверх общих (--config, --project, --format, --out; с HasScope - --diff, --scope)
        public IReadOnlyCollection<string> Options { get; init; } = [];

        public IReadOnlyCollection<string> Flags { get; init; } = [];

        // Первый - по умолчанию
        public IReadOnlyList<string> Formats { get; init; } = ["json", "md"];

        // --diff и --scope: файлы диффа, находки - на измененных строках (changed-lines)
        public bool HasScope { get; init; }

        public string MissingFiles { get; init; } = "не заданы файлы или папки";

        public required Func<FileRun, FileResult> Analyze { get; init; }
    }

    // Итог анализа: отчет (stdout или --out), сводка в stderr (время дописывается), строки после сводки, код возврата
    public class FileResult
    {
        public required string Text { get; init; }

        public required string Summary { get; init; }

        public string Details { get; init; } = "";

        public int ExitCode { get; init; }
    }

    // Вход анализа: аргументы, формат, проект, файлы и измененные строки
    public class FileRun
    {
        public required CommandLine CommandLine { get; init; }

        public required string Format { get; init; }

        public required CommandConfigs Configs { get; init; }

        public required ProjectOptions Project { get; init; }

        public required List<string> Files { get; init; }

        // file или changed-lines (команда без HasScope - file)
        public required string Scope { get; init; }

        // Измененные строки файлов в режиме changed-lines; null - все строки
        public Dictionary<string, ChangedLines>? Changed { get; init; }

        // Находки проверок по файлам параллельно; в changed-lines - только на измененных строках
        public List<Finding> Analyze(CheckOptions options)
        {
            var results = new ConcurrentBag<List<Finding>>();
            Parallel.ForEach(Files, file => results.Add(AnalyzeFile(file, options)));
            return results
                .SelectMany(findings => findings)
                .OrderBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ThenBy(finding => finding.Column)
                .ToList();
        }

        private List<Finding> AnalyzeFile(string file, CheckOptions options)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, Project.ParseOptions, SourceFiles.GetDisplayPath(file));
            var findings = SyntaxAnalyzer.Analyze(tree, options, Project);
            if (Changed == null)
                return findings;

            var lines = Changed[file];
            return findings.Where(lines.Intersects).ToList();
        }
    }

    public static class FileCommands
    {
        private static readonly string[] CommonOptions = ["--config", "--project", "--format", "--out"];
        private static readonly string[] ScopeOptions = ["--diff", "--scope"];

        // Шаги: аргументы -> формат -> проект -> файлы -> анализ -> --out -> сводка со временем команды -> код возврата
        public static int Run(FileCommand command, IEnumerable<string> args)
        {
            var stopwatch = Stopwatch.StartNew();
            var optionNames = CommonOptions.Concat(command.HasScope ? ScopeOptions : []).Concat(command.Options);
            var commandLine = CommandLine.Parse(args, optionNames.ToHashSet(), command.Flags.ToHashSet());
            if (commandLine.Positional.Count == 0)
                throw new UsageException(command.MissingFiles);

            var format = commandLine.GetOption("--format") ?? command.Formats[0];
            if (!command.Formats.Contains(format))
                throw new UsageException($"неизвестный формат '{format}'");

            var run = CreateRun(commandLine, format, command.HasScope);
            var result = command.Analyze(run);
            Output.Write(result.Text, commandLine.GetOption("--out"));
            Console.Error.WriteLine($"csr {command.Name}: {result.Summary}, {stopwatch.Elapsed.TotalSeconds:F1} с");
            Console.Error.Write(result.Details);
            return result.ExitCode;
        }

        // --scope: без --diff - file, с --diff - changed-lines (без --diff - от HEAD); при диффе файлы - только из него
        private static FileRun CreateRun(CommandLine commandLine, string format, bool hasScope)
        {
            var diff = hasScope ? commandLine.GetOption("--diff") : null;
            var defaultScope = diff == null ? "file" : "changed-lines";
            var scope = hasScope ? commandLine.GetOption("--scope") ?? defaultScope : "file";
            if (scope is not ("file" or "changed-lines"))
                throw new UsageException($"неизвестный режим '{scope}'");

            var directory = Environment.CurrentDirectory;
            var start = ConfigFiles.GetStart(commandLine.Positional[0], directory);
            var configs = new CommandConfigs(commandLine, directory, start, Console.Error);
            var project = configs.LoadProject();
            var files = SourceFiles.Collect(commandLine.Positional);
            Dictionary<string, ChangedLines>? changed = null;
            if (diff != null || scope == "changed-lines")
            {
                changed = GitDiff.Collect(diff ?? "HEAD", commandLine.Positional[0]);
                files = files.Where(changed.ContainsKey).ToList();
            }

            return new FileRun
            {
                CommandLine = commandLine,
                Format = format,
                Configs = configs,
                Project = project,
                Files = files,
                Scope = scope,
                Changed = scope == "changed-lines" ? changed : null,
            };
        }
    }
}

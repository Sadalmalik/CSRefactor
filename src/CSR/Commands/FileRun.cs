using System.Collections.Concurrent;
using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Commands
{
    // Вход анализа: аргументы, формат, проект, файлы и измененные строки
    public class FileRun
    {
        public required CommandLine CommandLine { get; init; }

        public required string Format { get; init; }

        public required CommandConfigTool Configs { get; init; }

        public required ProjectOptions Project { get; init; }

        public required List<string> Files { get; init; }

        // file или changed-lines (команда без HasScope - file)
        public required string Scope { get; init; }

        // Измененные строки файлов в режиме changed-lines; null - все строки
        public Dictionary<string, ChangedLines>? Changed { get; init; }

        // --scope: без --diff - file, с --diff - changed-lines (без --diff - от HEAD); при диффе файлы - только из него
        public static FileRun Create(CommandLine commandLine, string format, bool hasScope)
        {
            var diff = hasScope ? commandLine.GetOption("--diff") : null;
            var defaultScope = diff == null ? "file" : "changed-lines";
            var scope = hasScope ? commandLine.GetOption("--scope") ?? defaultScope : "file";
            if (scope is not ("file" or "changed-lines"))
                throw new UsageException($"неизвестный режим '{scope}'");

            var directory = Environment.CurrentDirectory;
            var start = ConfigFileTools.GetStart(commandLine.Positional[0], directory);
            var configs = new CommandConfigTool(commandLine, directory, start, Console.Error);
            var project = configs.LoadProject();
            var files = SourceFileTools.Collect(commandLine.Positional);
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
            var tree = CSharpSyntaxTree.ParseText(source, Project.ParseOptions, SourceFileTools.GetDisplayPath(file));
            var findings = SyntaxAnalyzer.Analyze(tree, options, Project);
            if (Changed == null)
                return findings;

            var lines = Changed[file];
            return findings.Where(lines.Intersects).ToList();
        }
    }
}

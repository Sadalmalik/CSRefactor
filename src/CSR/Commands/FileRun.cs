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

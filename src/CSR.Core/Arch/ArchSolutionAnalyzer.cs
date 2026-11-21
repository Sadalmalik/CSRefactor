using System.Collections.Concurrent;
using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis;

namespace CSR.Core.Arch
{
    // csr arch по решению: признаки по одному файлу - на деревьях компиляций (символы препроцессора проектов),
    // признаки по решению - на всех компиляциях сразу. Дерево, общее для нескольких проектов, - один раз
    public static class ArchSolutionAnalyzer
    {
        public static List<Finding> Analyze(IReadOnlyList<Compilation> compilations, CheckOptions options,
            ProjectOptions project, Func<string, string> displayPath, CancellationToken cancellation)
        {
            var fileOptions = new CheckOptions();
            var fileChecks = options.Checks.Where(configuration => configuration.Check is not SolutionCheck);
            fileOptions.Checks.AddRange(fileChecks);
            var trees = GetTrees(compilations);
            var results = new ConcurrentBag<List<Finding>>();
            var parallel = new ParallelOptions { CancellationToken = cancellation };
            Parallel.ForEach(trees, parallel, tree =>
            {
                var named = tree.WithFilePath(displayPath(tree.FilePath));
                results.Add(SyntaxAnalyzer.Analyze(named, fileOptions, project));
            });

            var findings = results.SelectMany(list => list).ToList();
            foreach (var configuration in options.Checks)
            {
                if (configuration.Check is not SolutionCheck check)
                    continue;

                var context = new SolutionCheckContext(compilations, project, configuration, findings, displayPath,
                    cancellation);
                check.AnalyzeSolution(context);
            }

            return findings
                .OrderBy(finding => finding.File, StringComparer.Ordinal)
                .ThenBy(finding => finding.Line)
                .ThenBy(finding => finding.Column)
                .ThenBy(finding => finding.Check, StringComparer.Ordinal)
                .ToList();
        }

        // Деревья исходников без сгенерированных (obj) и без повторов между проектами
        public static List<SyntaxTree> GetTrees(IReadOnlyList<Compilation> compilations)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var trees = new List<SyntaxTree>();
            foreach (var tree in compilations.SelectMany(compilation => compilation.SyntaxTrees))
            {
                var path = tree.FilePath.Replace('\\', '/');
                if (path.Length == 0 || path.Contains("/obj/") || !seen.Add(path))
                    continue;

                trees.Add(tree);
            }

            return trees;
        }
    }
}

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CSR.Workspace.References;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    public class StringMatch : CodeLocation
    {
        // literal - строковый литерал C#; file - файл другого типа (Lua, YAML-ассет, JSON...)
        public required string Kind { get; init; }
    }

    public class StringScan
    {
        public required IReadOnlyList<StringMatch> Matches { get; init; }

        public required int Total { get; init; }

        public required int ScannedFiles { get; init; }

        // Файлы больше maxFileSizeKb не читались
        public required int SkippedFiles { get; init; }

        public required TimeSpan Time { get; init; }
    }

    // Имя целым словом в строковых литералах C# (nameof - не строка, его правит Renamer)
    // и в файлах других типов: места, которые переименование не видит, - проверить вручную
    public static class StringMatchScanner
    {
        private static readonly HashSet<SyntaxKind> LiteralKinds =
        [
            SyntaxKind.StringLiteralToken,
            SyntaxKind.Utf8StringLiteralToken,
            SyntaxKind.InterpolatedStringTextToken,
            SyntaxKind.SingleLineRawStringLiteralToken,
            SyntaxKind.MultiLineRawStringLiteralToken,
            SyntaxKind.Utf8SingleLineRawStringLiteralToken,
            SyntaxKind.Utf8MultiLineRawStringLiteralToken,
        ];

        // unityAssets: просматривать и ассеты Unity (RefactorOptions.UnityExtensions)
        public static async Task<StringScan> ScanAsync(
            Solution solution,
            string root,
            string name,
            RefactorOptions options,
            bool unityAssets,
            CancellationToken cancellation = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var pattern = new Regex(
                $@"(?<![\p{{L}}\p{{Nd}}_]){Regex.Escape(name)}(?![\p{{L}}\p{{Nd}}_])",
                RegexOptions.CultureInvariant);
            var matches = new ConcurrentBag<StringMatch>();
            await ScanLiteralsAsync(solution, root, name, pattern, matches, cancellation);
            var extensions = new HashSet<string>(options.StringExtensions, StringComparer.OrdinalIgnoreCase);
            if (unityAssets)
                extensions.UnionWith(options.UnityExtensions);

            var files = ScanFiles(root, name, pattern, extensions, options, matches, cancellation);
            var ordered = matches
                .OrderBy(match => match.Kind, StringComparer.Ordinal)
                .ThenBy(match => match.File, StringComparer.Ordinal)
                .ThenBy(match => match.Line)
                .ThenBy(match => match.Column)
                .ToList();
            return new StringScan
            {
                Matches = ordered.Take(options.MaxStringMatches).ToList(),
                Total = ordered.Count,
                ScannedFiles = files.Scanned,
                SkippedFiles = files.Skipped,
                Time = stopwatch.Elapsed,
            };
        }

        private static async Task ScanLiteralsAsync(
            Solution solution,
            string root,
            string name,
            Regex pattern,
            ConcurrentBag<StringMatch> matches,
            CancellationToken cancellation)
        {
            var documents = solution.Projects
                .SelectMany(project => project.Documents)
                .Where(document => document.FilePath != null)
                .GroupBy(document => document.FilePath!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            await Parallel.ForEachAsync(documents, cancellation, async (document, token) =>
            {
                var text = await document.GetTextAsync(token);
                if (!text.ToString().Contains(name, StringComparison.Ordinal))
                    return;

                var tree = await document.GetSyntaxTreeAsync(token);
                if (tree == null)
                    return;

                var syntaxRoot = await tree.GetRootAsync(token);
                foreach (var literal in syntaxRoot.DescendantTokens().Where(item => LiteralKinds.Contains(item.Kind())))
                {
                    foreach (var match in pattern.EnumerateMatches(literal.Text))
                    {
                        var span = new TextSpan(literal.SpanStart + match.Index, match.Length);
                        var location = Location.Create(tree, span);
                        var place = SourceLocations.Create(location, root);
                        matches.Add(new StringMatch
                        {
                            Kind = "literal",
                            File = place.File,
                            Line = place.Line,
                            Column = place.Column,
                            Text = place.Text,
                        });
                    }
                }
            });
        }

        private static FileCounts ScanFiles(
            string root,
            string name,
            Regex pattern,
            HashSet<string> extensions,
            RefactorOptions options,
            ConcurrentBag<StringMatch> matches,
            CancellationToken cancellation)
        {
            if (extensions.Count == 0)
                return new FileCounts(0, 0);

            var exclude = new HashSet<string>(options.StringExclude, StringComparer.OrdinalIgnoreCase);
            var roots = options.StringRoots.Count == 0
                ? [root]
                : options.StringRoots.Select(item => Path.GetFullPath(Path.Combine(root, item))).ToList();
            var files = roots.SelectMany(item => EnumerateFiles(item, extensions, exclude)).Distinct().ToList();
            var limit = (long)options.MaxFileSizeKb * 1024;
            var skipped = 0;
            var parallel = new ParallelOptions { CancellationToken = cancellation };
            Parallel.ForEach(files, parallel, file =>
            {
                if (new FileInfo(file).Length > limit)
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                ScanFile(file, root, name, pattern, matches);
            });

            return new FileCounts(files.Count, skipped);
        }

        private static void ScanFile(
            string file,
            string root,
            string name,
            Regex pattern,
            ConcurrentBag<StringMatch> matches)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                return;
            }

            if (!text.Contains(name, StringComparison.Ordinal))
                return;

            var relative = SourceLocations.GetRelativePath(root, file);
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var match in pattern.EnumerateMatches(lines[i]))
                {
                    var line = lines[i].TrimEnd('\r').Trim();
                    matches.Add(new StringMatch
                    {
                        Kind = "file",
                        File = relative,
                        Line = i + 1,
                        Column = match.Index + 1,
                        Text = line.Length > 200 ? line.Substring(0, 200) + "..." : line,
                    });
                }
            }
        }

        private record struct FileCounts(int Scanned, int Skipped);

        private static IEnumerable<string> EnumerateFiles(
            string root,
            HashSet<string> extensions,
            HashSet<string> exclude)
        {
            if (!Directory.Exists(root))
                yield break;

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            var folders = new Stack<string>();
            folders.Push(root);
            while (folders.Count > 0)
            {
                var folder = folders.Pop();
                foreach (var child in Directory.EnumerateDirectories(folder, "*", options))
                {
                    if (!exclude.Contains(Path.GetFileName(child)))
                        folders.Push(child);
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*", options))
                {
                    if (extensions.Contains(Path.GetExtension(file)))
                        yield return file;
                }
            }
        }
    }
}

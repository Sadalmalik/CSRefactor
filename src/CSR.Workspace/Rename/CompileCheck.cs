using System.Diagnostics;
using CSR.Workspace.References;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    public class CompileResult
    {
        public required int ErrorsBefore { get; init; }

        public required int ErrorsAfter { get; init; }

        // Ошибки сверх базы: база - больше из "до" и "после пустой правки"
        public required IReadOnlyList<CodeLocation> NewErrors { get; init; }

        public required int Projects { get; init; }

        public required TimeSpan Time { get; init; }
    }

    // Компиляция затронутых проектов до и после правки. Генераторы кода проекта могут давать ошибки
    // на любую правку в памяти (замер на amb, design.md 1.2): тогда база - "пустая правка" тех же файлов
    public static class CompileCheck
    {
        private const int MaxReported = 20;

        public static async Task<CompileResult> RunAsync(Solution before, Solution after, string root,
            CancellationToken cancellation = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var changes = after.GetChanges(before).GetProjectChanges().ToList();
            var errorsBefore = 0;
            var errorsAfter = 0;
            var newErrors = new List<CodeLocation>();
            foreach (var change in changes)
            {
                var original = await GetErrorsAsync(before.GetProject(change.ProjectId)!, cancellation);
                var renamed = await GetErrorsAsync(after.GetProject(change.ProjectId)!, cancellation);
                errorsBefore += original.Count;
                errorsAfter += renamed.Count;
                var baseline = CountById(original);
                if (renamed.Count > original.Count)
                {
                    var noop = await GetNoopErrorsAsync(before, change, cancellation);
                    foreach (var (id, count) in CountById(noop))
                        baseline[id] = Math.Max(baseline.GetValueOrDefault(id), count);
                }

                newErrors.AddRange(GetExcess(renamed, original, baseline, root));
            }

            return new CompileResult
            {
                ErrorsBefore = errorsBefore,
                ErrorsAfter = errorsAfter,
                NewErrors = newErrors.Take(MaxReported).ToList(),
                Projects = changes.Count,
                Time = stopwatch.Elapsed,
            };
        }

        private static async Task<List<Diagnostic>> GetErrorsAsync(Project project, CancellationToken cancellation)
        {
            var compilation = await project.GetCompilationAsync(cancellation);
            if (compilation == null)
                return [];

            return compilation.GetDiagnostics(cancellation)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();
        }

        // Те же документы с пробелом в конце: правка без смысла
        private static async Task<List<Diagnostic>> GetNoopErrorsAsync(Solution before, ProjectChanges change,
            CancellationToken cancellation)
        {
            var noop = before;
            foreach (var documentId in change.GetChangedDocuments())
            {
                var text = await before.GetDocument(documentId)!.GetTextAsync(cancellation);
                var space = new TextChange(new TextSpan(text.Length, 0), " ");
                noop = noop.WithDocumentText(documentId, text.WithChanges(space));
            }

            return await GetErrorsAsync(noop.GetProject(change.ProjectId)!, cancellation);
        }

        private static Dictionary<string, int> CountById(List<Diagnostic> diagnostics)
        {
            return diagnostics
                .GroupBy(diagnostic => diagnostic.Id)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        // Сверх базы по коду ошибки; первыми - ошибки, которых до правки не было (по коду, файлу и тексту)
        private static IEnumerable<CodeLocation> GetExcess(List<Diagnostic> diagnostics, List<Diagnostic> original,
            Dictionary<string, int> baseline, string root)
        {
            var known = original.Select(GetKey).ToHashSet();
            foreach (var group in diagnostics.GroupBy(diagnostic => diagnostic.Id))
            {
                var excess = group.Count() - baseline.GetValueOrDefault(group.Key);
                if (excess <= 0)
                    continue;

                foreach (var diagnostic in group.OrderBy(item => known.Contains(GetKey(item))).Take(excess))
                {
                    var location = diagnostic.Location.IsInSource
                        ? SourceLocations.Create(diagnostic.Location, root)
                        : null;
                    yield return new CodeLocation
                    {
                        File = location?.File ?? "",
                        Line = location?.Line ?? 0,
                        Column = location?.Column ?? 0,
                        Text = $"{diagnostic.Id}: {diagnostic.GetMessage()}",
                    };
                }
            }
        }

        private static string GetKey(Diagnostic diagnostic)
            => $"{diagnostic.Id}|{diagnostic.Location.SourceTree?.FilePath}|{diagnostic.GetMessage()}";
    }
}

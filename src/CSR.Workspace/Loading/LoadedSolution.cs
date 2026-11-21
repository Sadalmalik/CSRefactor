using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Loading
{
    // Загруженное решение: текущий снимок Solution и рабочее пространство, из которого он получен.
    // Снимок обновляется правками на диске (сервер) и примененным переименованием
    public class LoadedSolution : IDisposable
    {
        private readonly Microsoft.CodeAnalysis.Workspace? m_Workspace;

        internal LoadedSolution(string path, Solution solution, Microsoft.CodeAnalysis.Workspace? workspace)
        {
            Path = System.IO.Path.GetFullPath(path);
            Root = System.IO.Path.GetDirectoryName(Path)!;
            Current = solution;
            m_Workspace = workspace;
        }

        // Исходный файл решения или проекта (не теневая копия)
        public string Path { get; }

        // Папка исходного решения: от нее считаются относительные пути отчетов
        public string Root { get; }

        public Solution Current { get; private set; }

        public IReadOnlyList<string> Warnings { get; internal init; } = Array.Empty<string>();

        public TimeSpan LoadTime { get; internal init; }

        // Сборка проектов шла с obj в кэше CSR и через теневую копию решения
        public bool Isolated { get; internal init; }

        // Снимок без MSBuild: тесты и инструменты, которые собирают Solution сами
        public static LoadedSolution FromSolution(string path, Solution solution)
            => new(path, solution, null);

        public void Update(Solution solution)
            => Current = solution;

        public IReadOnlyList<Document> FindDocuments(string filePath)
        {
            var fullPath = System.IO.Path.GetFullPath(filePath);
            var ids = Current.GetDocumentIdsWithFilePath(fullPath);
            if (ids.Length > 0)
                return ids.Select(id => Current.GetDocument(id)!).ToList();

            return Current.Projects
                .SelectMany(project => project.Documents)
                .Where(document => string.Equals(document.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public bool Contains(string filePath)
            => FindDocuments(filePath).Count > 0;

        // Перечитать документ с диска; false - файла нет в решении
        public bool RefreshDocument(string filePath)
        {
            var documents = FindDocuments(filePath);
            if (documents.Count == 0)
                return false;

            var text = ReadText(filePath);
            if (text == null)
                return true;

            var solution = Current;
            foreach (var document in documents)
                solution = solution.WithDocumentText(document.Id, text, PreservationMode.PreserveIdentity);

            Current = solution;
            return true;
        }

        public void Dispose()
            => m_Workspace?.Dispose();

        // Файл может быть занят редактором в момент записи: несколько попыток
        private static SourceText? ReadText(string filePath)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return SourceText.From(stream);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
            }

            return null;
        }
    }

    // Загрузка решения или проекта через MSBuildWorkspace (BuildHost Roslyn 5).
    // Если в решении есть проекты старого формата (Unity), сборка идет с obj в кэше CSR и через теневую
    // копию решения: рядом с кодом проекта ничего не пишется
    public static class SolutionLoader
    {
        public static async Task<LoadedSolution> LoadAsync(string path, CancellationToken cancellation = default)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"нет файла решения или проекта: {path}", fullPath);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var isSolution = SolutionFile.IsSolution(fullPath);
            var projects = isSolution ? SolutionFile.ReadProjects(fullPath) : [fullPath];
            var isolated = projects.Any(SolutionFile.IsLegacyProject);
            var properties = new Dictionary<string, string>();
            var openPath = fullPath;
            if (isolated)
            {
                var cache = CachePaths.ForSolution(fullPath);
                var obj = Path.Combine(cache, "obj") + Path.DirectorySeparatorChar;
                properties["BaseIntermediateOutputPath"] = obj;
                properties["IntermediateOutputPath"] = obj;
                if (isSolution)
                    openPath = SolutionFile.WriteShadow(fullPath, cache);
            }

            var workspace = MSBuildWorkspace.Create(properties);
            var warnings = new List<string>();
            workspace.RegisterWorkspaceFailedHandler(failure =>
            {
                lock (warnings)
                    warnings.Add($"{failure.Diagnostic.Kind}: {failure.Diagnostic.Message}");
            });

            try
            {
                var solution = isSolution
                    ? await workspace.OpenSolutionAsync(openPath, cancellationToken: cancellation)
                    : (await workspace.OpenProjectAsync(openPath, cancellationToken: cancellation)).Solution;
                return new LoadedSolution(fullPath, solution, workspace)
                {
                    Warnings = warnings,
                    LoadTime = stopwatch.Elapsed,
                    Isolated = isolated,
                };
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }
    }
}

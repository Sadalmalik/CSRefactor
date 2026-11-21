using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CSR.Workspace.Loading
{
    // Загрузка решения или проекта через MSBuildWorkspace (BuildHost Roslyn 5).
    // Если в решении есть проекты старого формата (Unity), сборка идет с obj и папкой вывода в кэше CSR
    // и через теневую копию решения: рядом с кодом проекта ничего не пишется
    public static class SolutionLoader
    {
        public static Task<LoadedSolution> LoadAsync(string path, CancellationToken cancellation = default)
            => LoadAsync(path, null, cancellation);

        // projectFilter: имя проекта (файл без расширения) -> грузить ли; проекты, на которые ссылаются
        // выбранные, грузятся тоже. null - все проекты решения
        public static async Task<LoadedSolution> LoadAsync(string path, Func<string, bool>? projectFilter,
            CancellationToken cancellation = default)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"нет файла решения или проекта: {path}", fullPath);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var isSolution = SolutionFile.IsSolution(fullPath);
            var projects = isSolution ? SolutionFile.ReadProjects(fullPath) : [fullPath];
            var isolated = projects.Any(SolutionFile.IsLegacyProject);
            var cache = CachePaths.ForSolution(fullPath);
            var workspace = MSBuildWorkspace.Create(isolated ? GetIsolatedProperties(cache) : []);
            var warnings = new List<string>();
            workspace.RegisterWorkspaceFailedHandler(failure =>
            {
                lock (warnings)
                    warnings.Add($"{failure.Diagnostic.Kind}: {failure.Diagnostic.Message}");
            });

            try
            {
                Solution solution;
                if (!isSolution)
                    solution = (await workspace.OpenProjectAsync(fullPath, cancellationToken: cancellation)).Solution;
                else if (projectFilter != null)
                    solution = await OpenProjectsAsync(workspace, projects, projectFilter, cancellation);
                else
                    solution = await OpenSolutionAsync(workspace, fullPath, isolated, cache, cancellation);

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

        // obj и папка вывода - в кэше: PrepareForBuild создает папку вывода, у Unity это Temp\Bin\Debug\<проект>
        private static Dictionary<string, string> GetIsolatedProperties(string cache)
        {
            var obj = Path.Combine(cache, "obj") + Path.DirectorySeparatorChar;
            var bin = Path.Combine(cache, "bin") + Path.DirectorySeparatorChar;
            return new Dictionary<string, string>
            {
                ["BaseIntermediateOutputPath"] = obj,
                ["IntermediateOutputPath"] = obj,
                ["OutputPath"] = bin,
                ["OutDir"] = bin,
            };
        }

        private static Task<Solution> OpenSolutionAsync(MSBuildWorkspace workspace, string path, bool isolated,
            string cache, CancellationToken cancellation)
        {
            var openPath = isolated ? SolutionFile.WriteShadow(path, cache) : path;
            return workspace.OpenSolutionAsync(openPath, cancellationToken: cancellation);
        }

        private static async Task<Solution> OpenProjectsAsync(MSBuildWorkspace workspace, List<string> projects,
            Func<string, bool> projectFilter, CancellationToken cancellation)
        {
            var selected = projects
                .Where(project => projectFilter(Path.GetFileNameWithoutExtension(project)))
                .ToList();
            if (selected.Count == 0)
                throw new FileNotFoundException("в решении нет проектов по фильтру");

            foreach (var project in selected)
            {
                var loaded = workspace.CurrentSolution.Projects.Any(item =>
                    string.Equals(item.FilePath, project, StringComparison.OrdinalIgnoreCase));
                if (!loaded)
                    await workspace.OpenProjectAsync(project, cancellationToken: cancellation);
            }

            return workspace.CurrentSolution;
        }
    }
}

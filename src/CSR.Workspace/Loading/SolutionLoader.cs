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
        public static async Task<LoadedSolution> LoadAsync(
            string path,
            Func<string, bool>? projectFilter,
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
            catch (Exception exception) when (IsBuildHostFailure(exception))
            {
                workspace.Dispose();
                throw new SolutionLoadException(GetBuildHostMessage(exception, projects), exception);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        // BuildHost не запустился (нет dotnet) или не нашел MSBuild: исключение процесса BuildHost
        // (RemoteInvocationException - внутренний тип Roslyn) или запуска процесса из BuildHostProcessManager
        private static bool IsBuildHostFailure(Exception exception)
        {
            if (exception.GetType().FullName == "Microsoft.CodeAnalysis.MSBuild.RemoteInvocationException")
                return true;

            return exception is System.ComponentModel.Win32Exception
                && exception.StackTrace?.Contains("BuildHostProcessManager", StringComparison.Ordinal) == true;
        }

        // Что нужно на машине - по видам проектов решения
        private static string GetBuildHostMessage(Exception exception, List<string> projects)
        {
            var reason = exception.Message.Split('\n')[0].Trim();
            var lines = new List<string> { $"загрузка решения: MSBuild недоступен ({reason})" };
            if (projects.Any(project => !SolutionFile.IsLegacyProject(project)))
                lines.Add("  проекты SDK-стиля: нужен .NET SDK, которым собирается проект, и dotnet в PATH");

            if (projects.Any(SolutionFile.IsLegacyProject))
            {
                lines.Add(
                    "  проекты старого формата (Unity): нужен MSBuild из Visual Studio 2017+ или Build Tools for "
                    + "Visual Studio (компонент MSBuild)");
                lines.Add(
                    "  только Rider: VSINSTALLDIR=<папка Rider>\\tools\\ и VSCMD_VER=<версия> - "
                    + "DOCS.md, «Требования к машине»");
            }

            return string.Join(Environment.NewLine, lines);
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

        private static Task<Solution> OpenSolutionAsync(
            MSBuildWorkspace workspace,
            string path,
            bool isolated,
            string cache,
            CancellationToken cancellation)
        {
            var openPath = isolated ? SolutionFile.WriteShadow(path, cache) : path;
            return workspace.OpenSolutionAsync(openPath, cancellationToken: cancellation);
        }

        private static async Task<Solution> OpenProjectsAsync(
            MSBuildWorkspace workspace,
            List<string> projects,
            Func<string, bool> projectFilter,
            CancellationToken cancellation)
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

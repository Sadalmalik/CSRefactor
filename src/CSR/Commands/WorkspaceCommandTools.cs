using System.Diagnostics;
using System.Text.RegularExpressions;
using CSR.Reports.Config;
using CSR.Workspace.Loading;
using CSR.Workspace.Server;
using CSR.Workspace.Symbols;

namespace CSR.Commands
{
    // Команды над загруженным решением (refs, rename, move, extract, arch): через живой csr serve этого решения, иначе загрузка
    // решения в процессе. Одна и та же обработка - в CLI и на сервере (Execute)
    public static class WorkspaceCommandTools
    {
        private const string ServeHint = "csr serve держит решение загруженным";

        // Файл в позиции символа (файл.cs:строка[:колонка]) или в строках extract (файл.cs:строка-строка)
        private static readonly Regex SourceFilePattern = new(@"^(?<file>.+\.cs):\d+", RegexOptions.IgnoreCase);

        // validate - проверка числа и формы аргументов до поиска и загрузки решения (код 2 без загрузки)
        public static int Run(
            string command,
            IEnumerable<string> args,
            ISet<string> optionNames,
            ISet<string> flagNames,
            Action<CommandLine> validate)
        {
            var arguments = args.ToArray();
            var commandLine = CommandLine.Parse(arguments, optionNames, flagNames);
            validate(commandLine);
            var workingDirectory = Environment.CurrentDirectory;
            var solution = FindSolution(commandLine, workingDirectory);
            var request = new WorkspaceRequest
            {
                Command = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
            };

            if (!commandLine.HasFlag("--no-server"))
            {
                var served = WorkspaceClient.TrySendAsync(solution, request, WorkspaceClient.DefaultConnectTimeout)
                    .GetAwaiter().GetResult();
                if (served != null)
                    return Print(served);
            }

            using var loaded = Load(solution);
            var response = ExecuteAsync(loaded, request, CancellationToken.None).GetAwaiter().GetResult();
            return Print(response);
        }

        // Обработчик запросов: в процессе CLI и на сервере
        public static async Task<WorkspaceResponse> ExecuteAsync(
            LoadedSolution loaded,
            WorkspaceRequest request,
            CancellationToken cancellation)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            int code;
            try
            {
                var context = new WorkspaceContext(loaded, request.WorkingDirectory, output, error);
                code = request.Command switch
                {
                    "refs" => await RefsCommand.ExecuteAsync(context, request.Arguments, cancellation),
                    "rename" => await RenameCommand.ExecuteAsync(context, request.Arguments, cancellation),
                    "move" => await MoveCommand.ExecuteAsync(context, request.Arguments, cancellation),
                    "extract" => await ExtractCommand.ExecuteAsync(context, request.Arguments, cancellation),
                    "arch" => await ArchCommand.ExecuteAsync(context, request.Arguments, cancellation),
                    _ => throw new UsageException($"команда '{request.Command}' не работает с решением"),
                };
            }
            catch (Exception exception) when (exception is UsageException or SymbolException or ConfigException)
            {
                error.WriteLine($"csr: {exception.Message}");
                code = 2;
            }
            catch (System.Text.Json.JsonException exception)
            {
                error.WriteLine($"csr: конфиг: {exception.Message}");
                code = 2;
            }

            return new WorkspaceResponse { ExitCode = code, Output = output.ToString(), Error = error.ToString() };
        }

        public static LoadedSolution Load(string solution)
        {
            Console.Error.WriteLine($"csr: загрузка {solution} (сервер не запущен; {ServeHint})");
            var stopwatch = Stopwatch.StartNew();
            var loaded = SolutionLoader.LoadAsync(solution).GetAwaiter().GetResult();
            var projects = loaded.Current.ProjectIds.Count;
            var documents = loaded.Current.Projects.Sum(project => project.DocumentIds.Count);
            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr: решение загружено: проектов {projects}, документов {documents}");
            Console.Error.WriteLine($"csr: загрузка {seconds:F1} с");
            return loaded;
        }

        // --solution или первое .sln / .slnx вверх от файла символа и текущей папки, затем в ее подпапках
        public static string FindSolution(CommandLine commandLine, string workingDirectory)
        {
            var explicitPath = commandLine.GetOption("--solution");
            if (explicitPath != null)
            {
                var full = Path.GetFullPath(Path.Combine(workingDirectory, explicitPath));
                if (!File.Exists(full))
                    throw new UsageException($"нет файла решения: {explicitPath}");

                return full;
            }

            var starts = new List<string>();
            var file = commandLine.Positional.Count > 0 ? SourceFilePattern.Match(commandLine.Positional[0]) : null;
            if (file is { Success: true })
            {
                var path = Path.GetFullPath(Path.Combine(workingDirectory, file.Groups["file"].Value));
                starts.Add(Path.GetDirectoryName(path)!);
            }

            starts.Add(workingDirectory);
            foreach (var start in starts)
            {
                for (var folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
                {
                    var found = FindInFolder(folder.FullName);
                    if (found != null)
                        return found;
                }
            }

            var nested = Directory.EnumerateDirectories(workingDirectory)
                .Select(FindInFolder)
                .OfType<string>()
                .ToList();
            if (nested.Count == 1)
                return nested[0];

            throw new UsageException("решение не найдено: задайте --solution <файл .sln | .slnx | .csproj>");
        }

        private static string? FindInFolder(string folder)
        {
            var solutions = Directory.EnumerateFiles(folder, "*.sln")
                .Concat(Directory.EnumerateFiles(folder, "*.slnx"))
                .ToList();
            if (solutions.Count > 1)
                throw new UsageException($"в {folder} несколько решений: задайте --solution");

            return solutions.Count == 1 ? solutions[0] : null;
        }

        // Запрос символа до загрузки решения: неверная форма - ошибка запуска
        public static void ValidateSymbol(string text)
        {
            try
            {
                SymbolQuery.Parse(text);
            }
            catch (SymbolException exception)
            {
                throw new UsageException(exception.Message);
            }
        }

        private static int Print(WorkspaceResponse response)
        {
            Console.Out.Write(response.Output);
            Console.Error.Write(response.Error);
            return response.ExitCode;
        }
    }

    // Загруженное решение и потоки вывода одной команды
    public class WorkspaceContext
    {
        public WorkspaceContext(LoadedSolution solution, string workingDirectory, TextWriter output, TextWriter error)
        {
            Solution = solution;
            WorkingDirectory = workingDirectory;
            Output = output;
            Error = error;
        }

        public LoadedSolution Solution { get; }

        public string WorkingDirectory { get; }

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        // Конфиги команды: пути - от папки вызова, проектный слой - от папки решения
        public CommandConfigTool CreateConfigs(CommandLine commandLine)
            => new(commandLine, WorkingDirectory, Solution.Root, Error);
    }
}

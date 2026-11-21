using System.Diagnostics;
using CSR.Workspace.Server;

namespace CSR.Commands
{
    // csr serve: процесс держит решение загруженным; refs и rename находят его сами по пути решения
    public static class ServeCommand
    {
        public const string Usage = """
            csr serve [<решение>] [опции]

              решение                    .sln, .slnx или .csproj (по умолчанию - ближайшее решение вверх по папкам)
              --idle <минуты>            остановиться после простоя (по умолчанию 120, 0 - не останавливаться)
              --detach                   запустить сервер отдельным процессом и вернуться, когда решение загружено
              --log <файл>               журнал сервера (с --detach по умолчанию - в кэше CSR)
              --status                   состояние сервера решения
              --stop                     остановить сервер решения
              --list                     все запущенные серверы

            Без --detach сервер работает до --stop, Ctrl+C или простоя. Перед каждой командой сервер
            перечитывает измененные .cs; при изменении проектов или решения - перезагружает решение.
            Команды - по именованному каналу текущего пользователя.
            Коды возврата: 0 - успех, 1 - сервер не запущен (--status, --stop), 2 - ошибка запуска
            """;

        private static readonly TimeSpan DetachTimeout = TimeSpan.FromMinutes(10);
        private static readonly HashSet<string> OptionNames = ["--idle", "--log", "--solution"];
        private static readonly HashSet<string> FlagNames = ["--detach", "--status", "--stop", "--list"];

        public static int Run(IEnumerable<string> args)
        {
            var arguments = args.ToList();
            var commandLine = CommandLine.Parse(arguments, OptionNames, FlagNames);
            if (commandLine.HasFlag("--list"))
                return List();

            var solution = commandLine.Positional.Count > 0
                ? Path.GetFullPath(commandLine.Positional[0])
                : WorkspaceCommands.FindSolution(commandLine, Environment.CurrentDirectory);
            if (!File.Exists(solution))
                throw new UsageException($"нет файла решения: {solution}");

            if (commandLine.HasFlag("--status"))
                return SendService(solution, WorkspaceProtocol.Ping);

            if (commandLine.HasFlag("--stop"))
                return SendService(solution, WorkspaceProtocol.Stop);

            var idle = ParseIdle(commandLine.GetOption("--idle"));
            if (IsRunning(solution))
            {
                Console.Error.WriteLine($"csr serve: сервер решения уже запущен: {solution}");
                return 0;
            }

            if (commandLine.HasFlag("--detach"))
                return Detach(solution, idle, commandLine.GetOption("--log"));

            return Serve(solution, idle, commandLine.GetOption("--log"));
        }

        private static int Serve(string solution, TimeSpan idle, string? logPath)
        {
            using var log = logPath == null ? null : new StreamWriter(logPath, true);
            var writer = log ?? Console.Error;
            var loaded = WorkspaceCommands.Load(solution);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellation.Cancel();
            };
            var server = new WorkspaceServer(loaded, WorkspaceCommands.ExecuteAsync, idle, writer);
            server.RunAsync(cancellation.Token).GetAwaiter().GetResult();
            return 0;
        }

        // Отдельный процесс без окна; ждем, пока он ответит в канал
        private static int Detach(string solution, TimeSpan idle, string? logPath)
        {
            var log = Path.GetFullPath(logPath ?? ServerRegistry.GetLogPath(solution));
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var minutes = ((int)idle.TotalMinutes).ToString();
            foreach (var argument in new[] { "serve", solution, "--idle", minutes, "--log", log })
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info) ?? throw new UsageException("не удалось запустить процесс сервера");
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < DetachTimeout)
            {
                if (process.HasExited)
                {
                    Console.Error.WriteLine($"csr serve: сервер завершился с кодом {process.ExitCode}; журнал: {log}");
                    return 2;
                }

                var status = WorkspaceClient.SendServiceAsync(solution, WorkspaceProtocol.Ping, TimeSpan.FromSeconds(1))
                    .GetAwaiter().GetResult();
                if (status != null)
                {
                    Console.Out.Write(status.Output);
                    var seconds = stopwatch.Elapsed.TotalSeconds;
                    Console.Error.WriteLine($"csr serve: запущен за {seconds:F1} с; журнал: {log}");
                    return 0;
                }
            }

            Console.Error.WriteLine($"csr serve: сервер не ответил за {DetachTimeout.TotalMinutes} мин; журнал: {log}");
            return 2;
        }

        private static int SendService(string solution, string command)
        {
            var response = WorkspaceClient.SendServiceAsync(solution, command, WorkspaceClient.DefaultConnectTimeout)
                .GetAwaiter().GetResult();
            if (response == null)
            {
                Console.Error.WriteLine($"csr serve: сервер не запущен: {solution}");
                return 1;
            }

            Console.Out.Write(response.Output);
            Console.Error.Write(response.Error);
            return response.ExitCode;
        }

        private static int List()
        {
            foreach (var entry in ServerRegistry.List())
                Console.Out.WriteLine($"{entry.ProcessId}\t{entry.Started:yyyy-MM-dd HH:mm}\t{entry.Solution}");

            return 0;
        }

        private static bool IsRunning(string solution)
        {
            var timeout = WorkspaceClient.DefaultConnectTimeout;
            var response = WorkspaceClient.SendServiceAsync(solution, WorkspaceProtocol.Ping, timeout)
                .GetAwaiter().GetResult();
            return response != null;
        }

        private static TimeSpan ParseIdle(string? text)
        {
            if (text == null)
                return TimeSpan.FromMinutes(120);

            if (!int.TryParse(text, out int minutes) || minutes < 0)
                throw new UsageException($"--idle: ожидается неотрицательное число минут, получено '{text}'");

            return TimeSpan.FromMinutes(minutes);
        }
    }
}

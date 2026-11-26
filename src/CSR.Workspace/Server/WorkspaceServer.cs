using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using CSR.Workspace.Loading;

namespace CSR.Workspace.Server
{
    // csr serve: держит загруженное решение и выполняет команды из именованного канала по одной.
    // Перед командой применяет изменения с диска: текст .cs - сразу, проекты - перезагрузкой решения
    public class WorkspaceServer
    {
        private readonly Func<LoadedSolution, WorkspaceRequest, CancellationToken, Task<WorkspaceResponse>> m_Handler;
        private readonly TimeSpan m_IdleTimeout;
        private readonly TextWriter m_Log;
        private readonly SemaphoreSlim m_Gate = new(1, 1);
        private LoadedSolution m_Loaded;
        private Dictionary<string, string> m_Fingerprint;
        private DateTime m_LastSync = DateTime.UtcNow;
        private DateTime m_LastActivity = DateTime.UtcNow;
        private int m_Active;

        // idleTimeout: остановка после простоя; TimeSpan.Zero - без остановки
        // handler - выполнение команды над решением (CLI: WorkspaceCommandTools.ExecuteAsync)
        public WorkspaceServer(
            LoadedSolution loaded,
            Func<LoadedSolution, WorkspaceRequest, CancellationToken, Task<WorkspaceResponse>> handler,
            TimeSpan idleTimeout,
            TextWriter log)
        {
            m_Loaded = loaded;
            m_Handler = handler;
            m_IdleTimeout = idleTimeout;
            m_Log = log;
            m_Fingerprint = GetFingerprint(loaded.Path);
            PipeName = WorkspaceProtocol.GetPipeName(loaded.Path);
        }

        public string PipeName { get; }

        // Число перезагрузок решения после старта (для тестов и статуса)
        public int Reloads { get; private set; }

        public async Task RunAsync(CancellationToken cancellation)
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var globbing = GetProjects(m_Loaded.Path).Any(project => !SolutionFile.IsLegacyProject(project));
            using var watcher = new SolutionWatcher(m_Loaded.Root, globbing);
            ServerRegistry.Register(new ServerEntry
            {
                ProcessId = Environment.ProcessId,
                Solution = m_Loaded.Path,
                Pipe = PipeName,
                Started = DateTime.Now,
            });
            Log($"готов: {m_Loaded.Path}, канал {PipeName}");
            var idle = WatchIdleAsync(stop);
            try
            {
                await AcceptAsync(watcher, stop);
            }
            finally
            {
                ServerRegistry.Unregister(m_Loaded.Path);
                stop.Cancel();
                await idle;
                await m_Gate.WaitAsync(CancellationToken.None);
                m_Loaded.Dispose();
                Log("остановлен");
            }
        }

        private async Task AcceptAsync(SolutionWatcher watcher, CancellationTokenSource stop)
        {
            while (!stop.IsCancellationRequested)
            {
                var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await pipe.WaitForConnectionAsync(stop.Token);
                }
                catch (OperationCanceledException)
                {
                    await pipe.DisposeAsync();
                    break;
                }

                _ = Task.Run(() => HandleAsync(pipe, watcher, stop));
            }
        }

        private async Task HandleAsync(
            NamedPipeServerStream pipe,
            SolutionWatcher watcher,
            CancellationTokenSource stop)
        {
            Interlocked.Increment(ref m_Active);
            try
            {
                await using (pipe)
                {
                    var request = await WorkspaceProtocol.ReadAsync<WorkspaceRequest>(pipe, stop.Token);
                    if (request == null)
                        return;

                    var response = await ExecuteAsync(request, watcher);
                    await WorkspaceProtocol.WriteAsync(pipe, response, CancellationToken.None);
                    if (request.Command == WorkspaceProtocol.Stop)
                        stop.Cancel();
                }
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
                Log($"канал: {exception.Message}");
            }
            finally
            {
                m_LastActivity = DateTime.UtcNow;
                Interlocked.Decrement(ref m_Active);
            }
        }

        private async Task<WorkspaceResponse> ExecuteAsync(WorkspaceRequest request, SolutionWatcher watcher)
        {
            if (request.Command == WorkspaceProtocol.Stop)
                return new WorkspaceResponse { ExitCode = 0, Output = "", Error = "csr serve: остановлен\n" };

            await m_Gate.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await SyncAsync(watcher);
                if (request.Command == WorkspaceProtocol.Ping)
                    return new WorkspaceResponse { ExitCode = 0, Output = GetStatus(), Error = "" };

                var response = await m_Handler(m_Loaded, request, CancellationToken.None);
                var seconds = stopwatch.Elapsed.TotalSeconds;
                var arguments = string.Join(' ', request.Arguments);
                Log($"{request.Command} {arguments}: код {response.ExitCode}, {seconds:F1} с");
                return response;
            }
            catch (Exception exception)
            {
                Log($"{request.Command}: {exception}");
                return new WorkspaceResponse { ExitCode = 2, Output = "", Error = $"csr serve: {exception.Message}\n" };
            }
            finally
            {
                m_Gate.Release();
            }
        }

        // Изменения с диска с прошлой команды
        private async Task SyncAsync(SolutionWatcher watcher)
        {
            var pending = watcher.Take();
            var now = DateTime.UtcNow;
            if (pending.IsEmpty)
                return;

            if (pending.Reload || pending.ProjectsChanged && !SameFingerprint())
            {
                Log("проекты изменились: перезагрузка решения");
                var fresh = await SolutionLoader.LoadAsync(m_Loaded.Path);
                m_Loaded.Dispose();
                m_Loaded = fresh;
                m_Fingerprint = GetFingerprint(fresh.Path);
                m_LastSync = now;
                Reloads++;
                Log($"решение перезагружено за {fresh.LoadTime.TotalSeconds:F1} с");
                return;
            }

            foreach (var file in pending.ChangedFiles)
                m_Loaded.RefreshDocument(file);

            if (pending.Overflow)
                RefreshModifiedSince(m_LastSync);

            m_LastSync = now;
        }

        private void RefreshModifiedSince(DateTime time)
        {
            var files = m_Loaded.Current.Projects
                .SelectMany(project => project.Documents)
                .Select(document => document.FilePath)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(file => File.Exists(file) && File.GetLastWriteTimeUtc(file) >= time)
                .ToList();
            foreach (var file in files)
                m_Loaded.RefreshDocument(file);
        }

        private string GetStatus()
        {
            var solution = m_Loaded.Current;
            var documents = solution.Projects.Sum(project => project.DocumentIds.Count);
            var loadTime = m_Loaded.LoadTime.TotalSeconds;
            return $"решение: {m_Loaded.Path}\nпроцесс: {Environment.ProcessId}\nканал: {PipeName}\n"
                + $"проектов: {solution.ProjectIds.Count}, документов: {documents}, "
                + $"загрузка {loadTime:F1} с, перезагрузок {Reloads}\n";
        }

        private async Task WatchIdleAsync(CancellationTokenSource stop)
        {
            if (m_IdleTimeout <= TimeSpan.Zero)
                return;

            try
            {
                while (!stop.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, m_IdleTimeout.TotalSeconds)), stop.Token);
                    if (Volatile.Read(ref m_Active) == 0 && DateTime.UtcNow - m_LastActivity > m_IdleTimeout)
                    {
                        Log("простой: остановка");
                        stop.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private bool SameFingerprint()
        {
            var current = GetFingerprint(m_Loaded.Path);
            return current.Count == m_Fingerprint.Count
                && current.All(item => m_Fingerprint.TryGetValue(item.Key, out string? hash) && hash == item.Value);
        }

        // Хеши файла решения и проектов
        private static Dictionary<string, string> GetFingerprint(string path)
        {
            var fingerprint = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in GetProjects(path).Append(path))
            {
                if (File.Exists(file))
                    fingerprint[file] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            }

            return fingerprint;
        }

        private static List<string> GetProjects(string path)
            => SolutionFile.IsSolution(path) ? SolutionFile.ReadProjects(path) : [path];

        private void Log(string message)
        {
            lock (m_Log)
            {
                m_Log.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                m_Log.Flush();
            }
        }
    }
}

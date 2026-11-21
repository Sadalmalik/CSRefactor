using System.IO.Pipes;

namespace CSR.Workspace.Server
{
    // Клиент сервера решения: null - сервер для этого решения не запущен
    public static class WorkspaceClient
    {
        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(300);

        public static async Task<WorkspaceResponse?> TrySendAsync(string solutionPath, WorkspaceRequest request,
            TimeSpan connectTimeout, CancellationToken cancellation = default)
        {
            var pipeName = WorkspaceProtocol.GetPipeName(solutionPath);
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.ConnectAsync(connectTimeout, cancellation);
            }
            catch (TimeoutException)
            {
                return null;
            }

            await WorkspaceProtocol.WriteAsync(pipe, request, cancellation);
            return await WorkspaceProtocol.ReadAsync<WorkspaceResponse>(pipe, cancellation)
                ?? throw new IOException("сервер решения закрыл канал без ответа");
        }

        // Ответ сервера на служебную команду или null
        public static Task<WorkspaceResponse?> SendServiceAsync(string solutionPath, string command,
            TimeSpan connectTimeout, CancellationToken cancellation = default)
        {
            var request = new WorkspaceRequest
            {
                Command = command,
                Arguments = [],
                WorkingDirectory = Environment.CurrentDirectory,
            };
            return TrySendAsync(solutionPath, request, connectTimeout, cancellation);
        }
    }
}

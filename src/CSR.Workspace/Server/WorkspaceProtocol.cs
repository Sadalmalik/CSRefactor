using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using CSR.Workspace.Loading;

namespace CSR.Workspace.Server
{
    // Команда CLI, выполняемая над загруженным решением: в процессе CLI или на сервере
    public class WorkspaceRequest
    {
        public required string Command { get; init; }

        public required string[] Arguments { get; init; }

        // Текущая папка клиента: от нее считаются относительные пути аргументов
        public required string WorkingDirectory { get; init; }
    }

    public class WorkspaceResponse
    {
        public required int ExitCode { get; init; }

        public required string Output { get; init; }

        public required string Error { get; init; }
    }

    // Сообщение в канале: длина (4 байта, little-endian) и JSON в UTF-8
    public static class WorkspaceProtocol
    {
        // Служебные команды сервера
        public const string Ping = "!ping";
        public const string Stop = "!stop";

        private const int MaxMessageSize = 256 * 1024 * 1024;

        public static string GetPipeName(string solutionPath)
            => "csr-" + CachePathTool.GetKey(solutionPath);

        public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellation)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
            await stream.WriteAsync(header, cancellation);
            await stream.WriteAsync(body, cancellation);
            await stream.FlushAsync(cancellation);
        }

        public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellation)
        {
            var header = new byte[4];
            if (!await ReadExactlyAsync(stream, header, cancellation))
                return default;

            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length < 0 || length > MaxMessageSize)
                throw new InvalidDataException($"сообщение канала: длина {length}");

            var body = new byte[length];
            if (!await ReadExactlyAsync(stream, body, cancellation))
                return default;

            return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(body));
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellation)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellation);
                if (read == 0)
                    return false;

                offset += read;
            }

            return true;
        }
    }
}

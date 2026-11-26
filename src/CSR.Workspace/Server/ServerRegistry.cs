using System.Diagnostics;
using System.Text.Json;
using CSR.Workspace.Loading;

namespace CSR.Workspace.Server
{
    public class ServerEntry
    {
        public required int ProcessId { get; init; }

        public required string Solution { get; init; }

        public required string Pipe { get; init; }

        public required DateTime Started { get; init; }
    }

    // Запущенные серверы: файлы <ключ решения>.json в кэше CSR; запись живет, пока жив процесс
    public static class ServerRegistry
    {
        public static void Register(ServerEntry entry)
        {
            Directory.CreateDirectory(CachePathTool.Servers);
            File.WriteAllText(GetPath(entry.Solution), JsonSerializer.Serialize(entry));
        }

        public static void Unregister(string solution)
        {
            try
            {
                File.Delete(GetPath(solution));
            }
            catch (IOException)
            {
            }
        }

        // Записи живых процессов; записи упавших серверов удаляются
        public static List<ServerEntry> List()
        {
            var entries = new List<ServerEntry>();
            if (!Directory.Exists(CachePathTool.Servers))
                return entries;

            foreach (var file in Directory.EnumerateFiles(CachePathTool.Servers, "*.json"))
            {
                var entry = Read(file);
                if (entry != null && IsAlive(entry.ProcessId))
                    entries.Add(entry);
                else
                    TryDelete(file);
            }

            return entries;
        }

        public static string GetLogPath(string solution)
            => Path.ChangeExtension(GetPath(solution), ".log");

        private static string GetPath(string solution)
            => Path.Combine(CachePathTool.Servers, CachePathTool.GetKey(solution) + ".json");

        private static ServerEntry? Read(string file)
        {
            try
            {
                return JsonSerializer.Deserialize<ServerEntry>(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                return null;
            }
        }

        private static bool IsAlive(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}

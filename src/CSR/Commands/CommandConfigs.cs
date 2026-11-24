using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Reports.Config;

namespace CSR.Commands
{
    // Конфиги команды: пути - от папки вызова (на сервере csr serve текущая папка процесса другая),
    // поиск проектного слоя - от start (ConfigFiles); предупреждения - в error (на сервере - поток клиента)
    public class CommandConfigs
    {
        private readonly CommandLine m_CommandLine;
        private readonly string m_Directory;
        private readonly string m_Start;
        private readonly TextWriter m_Error;

        public CommandConfigs(CommandLine commandLine, string directory, string start, TextWriter error)
        {
            m_CommandLine = commandLine;
            m_Directory = directory;
            m_Start = start;
            m_Error = error;
        }

        // Слои конфига проверок (style.jsonc, arch.jsonc); устаревшая проверка - предупреждение
        public CheckOptions LoadChecks(string fileName, Action<string, CheckOptions> apply)
        {
            var paths = ConfigFiles.GetLayers(m_CommandLine.GetOptions("--config"), fileName, m_Start);
            if (paths.Count == 0)
                throw new UsageException(ConfigFiles.GetMissingMessage(fileName));

            var options = new CheckOptions();
            foreach (var path in paths)
                apply(ReadFile(path), options);

            foreach (var configuration in options.Checks)
            {
                var replacement = configuration.Check.Deprecated;
                if (replacement != null)
                    m_Error.WriteLine($"csr: проверка '{configuration.Check.Name}' устарела, удаляется в v0.4: {replacement}");
            }

            return options;
        }

        public ProjectOptions LoadProject()
        {
            var text = Read("--project", "project.jsonc");
            return text == null ? new ProjectOptions() : ProjectConfig.Parse(text);
        }

        // Одиночный конфиг команды (--config): текст или null - нет ни явного, ни найденного
        public string? Read(string fileName)
            => Read("--config", fileName);

        private string? Read(string option, string fileName)
        {
            var path = ConfigFiles.Find(m_CommandLine.GetOption(option), fileName, m_Start);
            return path == null ? null : ReadFile(path);
        }

        private string ReadFile(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(m_Directory, path));
            if (!File.Exists(fullPath))
                throw new UsageException($"нет файла конфига: {path}");

            return File.ReadAllText(fullPath);
        }
    }
}

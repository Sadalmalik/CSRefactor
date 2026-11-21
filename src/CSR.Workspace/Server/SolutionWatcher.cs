namespace CSR.Workspace.Server
{
    // Изменения на диске, накопленные между запросами к серверу
    public class PendingChanges
    {
        public required IReadOnlyCollection<string> ChangedFiles { get; init; }

        // Изменились .props, .targets, появились или удалены проекты или .cs (проекты SDK): перезагрузить решение
        public required bool Reload { get; init; }

        // Изменились .sln, .slnx, .csproj: перезагрузить, если изменилось содержимое (Unity переписывает
        // проекты и без изменений)
        public required bool ProjectsChanged { get; init; }

        // Переполнение буфера событий: перечитать все документы, измененные после прошлой синхронизации
        public required bool Overflow { get; init; }

        public bool IsEmpty
            => ChangedFiles.Count == 0 && !Reload && !ProjectsChanged && !Overflow;
    }

    // Слежение за папкой решения: .cs - перечитать текст; .sln, .slnx, проекты - перезагрузка, если изменилось
    // содержимое; .props, .targets - перезагрузка.
    // Новые и удаленные .cs - перезагрузка только для проектов SDK (файлы подхватываются шаблоном);
    // старые проекты Unity перечисляют файлы сами - перезагрузку даст изменение .csproj
    public class SolutionWatcher : IDisposable
    {
        private static readonly HashSet<string> ProjectExtensions =
            new([".sln", ".slnx", ".csproj"], StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> BuildExtensions =
            new([".props", ".targets"], StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> IgnoredFolders =
            new(["obj", "bin", "Library", "Temp", "Logs", ".git", ".vs", ".idea"], StringComparer.OrdinalIgnoreCase);

        private readonly FileSystemWatcher m_Watcher;
        private readonly string m_Root;
        private readonly bool m_Globbing;
        private readonly object m_Lock = new();
        private HashSet<string> m_Changed = new(StringComparer.OrdinalIgnoreCase);
        private bool m_Reload;
        private bool m_ProjectsChanged;
        private bool m_Overflow;

        public SolutionWatcher(string root, bool globbing)
        {
            m_Root = root;
            m_Globbing = globbing;
            m_Watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            m_Watcher.Changed += (_, change) => OnChanged(change.FullPath);
            m_Watcher.Created += (_, change) => OnStructureChanged(change.FullPath);
            m_Watcher.Deleted += (_, change) => OnStructureChanged(change.FullPath);
            m_Watcher.Renamed += (_, change) =>
            {
                OnStructureChanged(change.OldFullPath);
                OnStructureChanged(change.FullPath);
            };
            m_Watcher.Error += (_, _) => Mark(() => m_Overflow = true);
            m_Watcher.EnableRaisingEvents = true;
        }

        public PendingChanges Take()
        {
            lock (m_Lock)
            {
                var pending = new PendingChanges
                {
                    ChangedFiles = m_Changed,
                    Reload = m_Reload,
                    ProjectsChanged = m_ProjectsChanged,
                    Overflow = m_Overflow,
                };
                m_Changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                m_Reload = false;
                m_ProjectsChanged = false;
                m_Overflow = false;
                return pending;
            }
        }

        public void Dispose()
            => m_Watcher.Dispose();

        private void OnChanged(string path)
        {
            if (IsIgnored(path))
                return;

            var extension = Path.GetExtension(path);
            if (ProjectExtensions.Contains(extension))
                Mark(() => m_ProjectsChanged = true);
            else if (BuildExtensions.Contains(extension))
                Mark(() => m_Reload = true);
            else if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                Mark(() => m_Changed.Add(path));
        }

        private void OnStructureChanged(string path)
        {
            if (IsIgnored(path))
                return;

            var extension = Path.GetExtension(path);
            var isCode = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
            if (ProjectExtensions.Contains(extension))
                Mark(() => m_ProjectsChanged = true);
            else if (BuildExtensions.Contains(extension) || m_Globbing && isCode)
                Mark(() => m_Reload = true);
            else if (isCode)
                Mark(() => m_Changed.Add(path));
        }

        private void Mark(Action action)
        {
            lock (m_Lock)
                action();
        }

        // Служебные папки - только внутри корня решения (сам корень может лежать, например, в Temp)
        private bool IsIgnored(string path)
        {
            var relative = Path.GetRelativePath(m_Root, path);
            var folders = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return folders.Take(folders.Length - 1).Any(IgnoredFolders.Contains);
        }
    }
}

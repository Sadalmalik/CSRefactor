namespace CSR
{
    // Сборка файла: ближайшая папка вверх с манифестом *.asmdef или *.csproj (вложенная сборка - своя), имя -
    // имя манифеста без расширения; без манифеста выше или в папке несколько манифестов одного вида (корень Unity
    // со сгенерированными .csproj) - файл вне сборок
    public class AssemblyFiles
    {
        private static readonly string[] Manifests = ["*.asmdef", "*.csproj"];

        private readonly Dictionary<string, string?> m_Folders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string?> m_Files = new(StringComparer.Ordinal);

        // paths - пути деревьев: по ним же сборку спрашивают метрики
        public AssemblyFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                m_Files[path] = Find(Path.GetDirectoryName(Path.GetFullPath(path)));
        }

        public string? Get(string path)
            => m_Files.GetValueOrDefault(path);

        private string? Find(string? folder)
        {
            if (folder == null)
                return null;

            if (m_Folders.TryGetValue(folder, out string? cached))
                return cached;

            var manifests = Manifests.Select(mask => Directory.EnumerateFiles(folder, mask).ToList())
                .FirstOrDefault(found => found.Count > 0);
            var name = manifests switch
            {
                null => Find(Path.GetDirectoryName(folder)),
                { Count: 1 } => Path.GetFileNameWithoutExtension(manifests[0]),
                _ => null,
            };
            m_Folders[folder] = name;
            return name;
        }
    }
}

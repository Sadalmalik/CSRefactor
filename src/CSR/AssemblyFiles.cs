namespace CSR
{
    // Сборка файла: ближайшая папка вверх с манифестом *.asmdef или *.csproj (вложенная сборка - своя), имя -
    // имя манифеста без расширения; без манифеста выше или в папке несколько манифестов одного вида (корень Unity
    // со сгенерированными .csproj) - файл вне сборок. Файл в Assets проекта Unity без asmdef выше - предопределенная
    // сборка Unity (UnityAssemblyTool); вне Assets без asmdef (Packages) и папка с *.asmref (ссылка на asmdef по GUID) -
    // вне сборок: поиск останавливается на корне проекта Unity
    public class AssemblyFiles
    {
        private static readonly string[] Manifests = ["*.asmdef", "*.csproj"];

        private readonly Dictionary<string, Owner> m_Folders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string?> m_Files = new(StringComparer.Ordinal);

        // paths - пути деревьев: по ним же сборку спрашивают метрики
        public AssemblyFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
                var owner = Find(folder);
                m_Files[path] = owner.Assets == null ? owner.Name : UnityAssemblyTool.GetName(owner.Assets, folder);
            }
        }

        public string? Get(string path)
            => m_Files.GetValueOrDefault(path);

        private Owner Find(string? folder)
        {
            if (folder == null)
                return default;

            if (m_Folders.TryGetValue(folder, out Owner cached))
                return cached;

            var manifests = Manifests.Select(mask => Directory.EnumerateFiles(folder, mask).ToList())
                .FirstOrDefault(found => found.Count > 0);
            var owner = manifests switch
            {
                _ when UnityAssemblyTool.IsProjectRoot(folder) => default,
                null when Directory.EnumerateFiles(folder, "*.asmref").Any() => default,
                null when UnityAssemblyTool.IsAssetsRoot(folder) => new Owner(null, folder),
                null => Find(Path.GetDirectoryName(folder)),
                { Count: 1 } => new Owner(Path.GetFileNameWithoutExtension(manifests[0]), null),
                _ => default,
            };
            m_Folders[folder] = owner;
            return owner;
        }

        // Сборка папки: имя манифеста или корень Assets проекта Unity (имя - по пути файла)
        private readonly record struct Owner(string? Name, string? Assets);
    }
}

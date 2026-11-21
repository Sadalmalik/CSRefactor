namespace CSR.Commands
{
    // Поиск конфигов. Заданы --config - только они, по порядку. Без --config два слоя: по умолчанию (папка config
    // рядом с csr.exe) и проектный (.csr в папке цели или выше, до корня репозитория включительно).
    // Конфиги проверок (style, arch) накладываются: по умолчанию, затем проектный. Одиночные (project, metrics,
    // refactor) не накладываются: проектный, иначе по умолчанию
    public static class ConfigFiles
    {
        public const string ProjectFolder = ".csr";

        public const string DefaultFolder = "config";

        public static string DefaultDirectory
            => Path.Combine(AppContext.BaseDirectory, DefaultFolder);

        public static IReadOnlyList<string> GetLayers(
            IReadOnlyList<string> explicitPaths,
            string fileName,
            string start)
            => GetLayers(explicitPaths, fileName, start, DefaultDirectory);

        // Слои конфига проверок в порядке наложения; пустой список - конфига нет
        public static IReadOnlyList<string> GetLayers(
            IReadOnlyList<string> explicitPaths,
            string fileName,
            string start,
            string defaultDirectory)
        {
            if (explicitPaths.Count > 0)
                return explicitPaths;

            var layers = new List<string>();
            var fallback = Path.Combine(defaultDirectory, fileName);
            if (File.Exists(fallback))
                layers.Add(fallback);

            var project = FindProject(fileName, start);
            if (project != null && !layers.Contains(project, StringComparer.OrdinalIgnoreCase))
                layers.Add(project);

            return layers;
        }

        public static string? Find(string? explicitPath, string fileName, string start)
            => Find(explicitPath, fileName, start, DefaultDirectory);

        // Одиночный конфиг: явный, иначе проектный, иначе по умолчанию; null - нет ни одного
        public static string? Find(string? explicitPath, string fileName, string start, string defaultDirectory)
        {
            if (explicitPath != null)
                return explicitPath;

            var fallback = Path.Combine(defaultDirectory, fileName);
            return FindProject(fileName, start) ?? (File.Exists(fallback) ? fallback : null);
        }

        // Начало поиска проектного слоя: цель-папка или папка цели-файла (путь - от папки вызова)
        public static string GetStart(string target, string workingDirectory)
        {
            var full = Path.GetFullPath(Path.Combine(workingDirectory, target));
            return Directory.Exists(full) ? full : Path.GetDirectoryName(full) ?? full;
        }

        public static string GetMissingMessage(string fileName)
            => $"не задан --config, нет {ProjectFolder}/{fileName} в папке цели или выше "
                + $"и {DefaultFolder}/{fileName} рядом с csr.exe";

        // .csr/<файл> от папки start вверх; корень репозитория (.git) - последняя проверяемая папка
        private static string? FindProject(string fileName, string start)
        {
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, ProjectFolder, fileName);
                if (File.Exists(candidate))
                    return candidate;

                if (Path.Exists(Path.Combine(directory.FullName, ".git")))
                    return null;
            }

            return null;
        }
    }
}

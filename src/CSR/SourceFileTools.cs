namespace CSR
{
    // Файлы .cs из списка файлов и папок. В папках пропускаются bin, obj и скрытые папки (имя с точки)
    public static class SourceFileTools
    {
        private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj" };

        public static List<string> Collect(IEnumerable<string> paths)
        {
            var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    files.Add(Path.GetFullPath(path));
                else if (Directory.Exists(path))
                    AddFolder(files, Path.GetFullPath(path));
                else
                    throw new UsageException($"нет такого файла или папки: {path}");
            }

            return files.ToList();
        }

        // Путь для отчета: относительно текущей папки, если файл внутри нее, иначе полный; разделитель - '/'
        public static string GetDisplayPath(string fullPath)
        {
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, fullPath);
            bool isOutside = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative);
            return (isOutside ? fullPath : relative).Replace('\\', '/');
        }

        private static void AddFolder(SortedSet<string> files, string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.cs"))
                files.Add(file);

            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = Path.GetFileName(child);
                if (name.StartsWith('.') || SkippedFolders.Contains(name))
                    continue;

                AddFolder(files, child);
            }
        }
    }
}

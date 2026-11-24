namespace CSR.Core.Checks
{
    // Область файла по маскам путей paths / excludePaths: * - любые символы, разделитель /, регистр не важен.
    // Путь от текущей папки (Editor/Tool.cs) проверяется и с ведущим /: маска */Editor/* ловит и папку верхнего уровня
    public static class FileScope
    {
        public static readonly CheckParameter Paths = CheckParameter.Strings(
            "paths",
            "маски путей файлов, где проверка работает (* - любые символы, разделитель /); пусто - все");

        public static readonly CheckParameter ExcludePaths = CheckParameter.Strings(
            "excludePaths",
            "маски путей файлов, где проверка не работает (* - любые символы, разделитель /)");

        // Файл в области проверки - по ее параметрам paths и excludePaths, если она их объявила
        public static bool IsInScope(CheckConfiguration configuration, string filePath)
            => IsInScope(GetMasks(configuration, Paths), GetMasks(configuration, ExcludePaths), filePath);

        // Файл под paths (пусто - все) и не под excludePaths
        public static bool IsInScope(IReadOnlyList<string> paths, IReadOnlyList<string> excludePaths, string filePath)
            => (paths.Count == 0 || IsMatch(paths, filePath)) && !IsMatch(excludePaths, filePath);

        public static bool IsMatch(IReadOnlyList<string> masks, string filePath)
        {
            var mask = NameMask.Get(masks, isIgnoreCase: true);
            var path = filePath.Replace('\\', '/');
            return mask.IsMatch(path) || mask.IsMatch($"/{path}");
        }

        private static IReadOnlyList<string> GetMasks(CheckConfiguration configuration, CheckParameter parameter)
        {
            bool isDeclared = configuration.Check.Parameters.Any(candidate => candidate.Name == parameter.Name);
            return isDeclared ? configuration.Settings.GetStrings(parameter.Name) : Array.Empty<string>();
        }
    }
}

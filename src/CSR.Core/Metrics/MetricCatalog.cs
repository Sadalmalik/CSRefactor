namespace CSR.Core.Metrics
{
    public static class MetricKeys
    {
        public const string Cyclomatic = "cyclomatic";
        public const string Cognitive = "cognitive";
        public const string Lines = "lines";
        public const string CodeLines = "code-lines";
        public const string Nesting = "nesting";
        public const string Parameters = "parameters";
        public const string Members = "members";
        public const string Regions = "regions";
        public const string Types = "types";
    }

    public class MetricDefinition
    {
        public required MetricLevel Level { get; init; }

        public required string Key { get; init; }

        public required string Title { get; init; }

        // Порог по умолчанию: значения выше порога считаются в сводке и отмечаются в топе; null - без порога
        public int? DefaultThreshold { get; init; }

        public string Id
            => $"{MetricCatalog.GetLevelKey(Level)}.{Key}";
    }

    // Пороги по умолчанию: сложность - crapper и SonarSource,
    // вложенность и строки файла - замеры эталона (design.md 1.3)
    public static class MetricCatalog
    {
        public static readonly IReadOnlyList<MetricDefinition> All =
        [
            Function(MetricKeys.Cyclomatic, "Цикломатическая сложность", 10),
            Function(MetricKeys.Cognitive, "Когнитивная сложность", 15),
            Function(MetricKeys.Lines, "Строк", 60),
            Function(MetricKeys.CodeLines, "Строк кода", null),
            Function(MetricKeys.Nesting, "Вложенность", 3),
            Function(MetricKeys.Parameters, "Параметров", 5),
            Type(MetricKeys.Lines, "Строк", null),
            Type(MetricKeys.CodeLines, "Строк кода", null),
            Type(MetricKeys.Members, "Членов", null),
            Type(MetricKeys.Regions, "Регионов", null),
            File(MetricKeys.Lines, "Строк", 400),
            File(MetricKeys.CodeLines, "Строк кода", 316),
            File(MetricKeys.Types, "Типов", null),
        ];

        public static string GetLevelKey(MetricLevel level)
        {
            return level switch
            {
                MetricLevel.Function => "function",
                MetricLevel.Type => "type",
                MetricLevel.File => "file",
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
        }

        public static string GetLevelTitle(MetricLevel level)
        {
            return level switch
            {
                MetricLevel.Function => "Функция",
                MetricLevel.Type => "Тип",
                MetricLevel.File => "Файл",
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };
        }

        private static MetricDefinition Function(string key, string title, int? threshold)
            => new() { Level = MetricLevel.Function, Key = key, Title = title, DefaultThreshold = threshold };

        private static MetricDefinition Type(string key, string title, int? threshold)
            => new() { Level = MetricLevel.Type, Key = key, Title = title, DefaultThreshold = threshold };

        private static MetricDefinition File(string key, string title, int? threshold)
            => new() { Level = MetricLevel.File, Key = key, Title = title, DefaultThreshold = threshold };
    }
}

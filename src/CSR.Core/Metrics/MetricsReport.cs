namespace CSR.Core.Metrics
{
    public class MetricSummary
    {
        public required MetricDefinition Metric { get; init; }

        public required int Count { get; init; }

        public required int P50 { get; init; }

        public required int P90 { get; init; }

        public required int P99 { get; init; }

        public required int Max { get; init; }

        public required int? Threshold { get; init; }

        // Число элементов со значением выше порога; 0 без порога
        public required int AboveThreshold { get; init; }

        // Наибольшие значения: по убыванию, при равенстве - по файлу и строке
        public required IReadOnlyList<MetricItem> Top { get; init; }
    }

    // Сводка по набору файлов: перцентили p50 / p90 / p99 / max и топ по каждой включенной метрике
    public class MetricsReport
    {
        public required IReadOnlyList<FileMetrics> Files { get; init; }

        public required IReadOnlyList<MetricSummary> Metrics { get; init; }

        public int TypeCount
            => Files.Sum(file => file.Types.Count);

        public int FunctionCount
            => Files.Sum(file => file.Functions.Count);

        public static MetricsReport Build(IReadOnlyList<FileMetrics> files, MetricsOptions options)
        {
            List<MetricSummary> metrics = MetricCatalog.All
                .Where(options.IsEnabled)
                .Select(metric => Summarize(metric, GetItems(files, metric.Level), options))
                .ToList();

            return new MetricsReport { Files = files, Metrics = metrics };
        }

        // Перцентиль - значение с индексом floor(q * n) отсортированного ряда, как в прототипе spikes/RoslynLoad
        public static int Percentile(IReadOnlyList<int> sorted, double quantile)
        {
            if (sorted.Count == 0)
                return 0;

            int index = (int)Math.Min(sorted.Count - 1, Math.Floor(quantile * sorted.Count));
            return sorted[index];
        }

        private static IEnumerable<MetricItem> GetItems(IReadOnlyList<FileMetrics> files, MetricLevel level)
        {
            return level switch
            {
                MetricLevel.Function => files.SelectMany(file => file.Functions),
                MetricLevel.Type => files.SelectMany(file => file.Types),
                _ => files.Select(file => file.File),
            };
        }

        private static MetricSummary Summarize(MetricDefinition metric, IEnumerable<MetricItem> items,
            MetricsOptions options)
        {
            List<MetricItem> list = items.ToList();
            List<int> values = list.Select(item => item.Values[metric.Key]).Order().ToList();
            int? threshold = options.GetThreshold(metric);
            List<MetricItem> top = list
                .OrderByDescending(item => item.Values[metric.Key])
                .ThenBy(item => item.File, StringComparer.Ordinal)
                .ThenBy(item => item.Line)
                .Take(options.Top)
                .ToList();

            return new MetricSummary
            {
                Metric = metric,
                Count = values.Count,
                P50 = Percentile(values, 0.5),
                P90 = Percentile(values, 0.9),
                P99 = Percentile(values, 0.99),
                Max = values.Count == 0 ? 0 : values[^1],
                Threshold = threshold,
                AboveThreshold = threshold == null ? 0 : values.Count(value => value > threshold),
                Top = top,
            };
        }
    }
}

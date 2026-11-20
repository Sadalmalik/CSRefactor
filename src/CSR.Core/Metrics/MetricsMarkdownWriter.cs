using System.Text;

namespace CSR.Core.Metrics
{
    // Отчет для человека: сводная таблица перцентилей и топ по каждой метрике
    public static class MetricsMarkdownWriter
    {
        public static string Write(MetricsReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Метрики");
            builder.AppendLine();
            builder.AppendLine(
                $"Файлов: {report.Files.Count}, типов: {report.TypeCount}, функций: {report.FunctionCount}.");
            builder.AppendLine();

            builder.AppendLine("## Сводка");
            builder.AppendLine();
            builder.AppendLine("| Уровень | Метрика | p50 | p90 | p99 | max | Порог | Выше порога |");
            builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
            foreach (MetricSummary summary in report.Metrics)
            {
                MetricDefinition metric = summary.Metric;
                string threshold = summary.Threshold?.ToString() ?? "—";
                string above = summary.Threshold == null ? "—" : summary.AboveThreshold.ToString();
                builder.AppendLine($"| {MetricCatalog.GetLevelTitle(metric.Level)} | {metric.Title} | {summary.P50} "
                    + $"| {summary.P90} | {summary.P99} | {summary.Max} | {threshold} | {above} |");
            }

            foreach (MetricSummary summary in report.Metrics)
                WriteTop(builder, summary);

            return builder.ToString();
        }

        private static void WriteTop(StringBuilder builder, MetricSummary summary)
        {
            if (summary.Top.Count == 0)
                return;

            MetricDefinition metric = summary.Metric;
            builder.AppendLine();
            builder.AppendLine($"## {MetricCatalog.GetLevelTitle(metric.Level)}: {metric.Title.ToLowerInvariant()}");
            builder.AppendLine();

            if (metric.Level == MetricLevel.File)
            {
                builder.AppendLine("| Значение | Файл |");
                builder.AppendLine("|---:|---|");
                foreach (MetricItem item in summary.Top)
                    builder.AppendLine($"| {item.Values[metric.Key]} | `{Escape(item.File)}` |");
                return;
            }

            builder.AppendLine("| Значение | Имя | Место |");
            builder.AppendLine("|---:|---|---|");
            foreach (MetricItem item in summary.Top)
            {
                builder.AppendLine(
                    $"| {item.Values[metric.Key]} | `{Escape(item.Name)}` | `{Escape(item.File)}:{item.Line}` |");
            }
        }

        // Вертикальная черта в имени (operator |) разбивает строку таблицы
        private static string Escape(string text)
            => text.Replace("|", "\\|");
    }
}

using System.Globalization;
using System.Text;
using CSR.Core.Metrics;

namespace CSR.Reports.Metrics
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
            foreach (var summary in report.Metrics)
            {
                var metric = summary.Metric;
                var threshold = summary.Threshold?.ToString() ?? "—";
                var above = summary.Threshold == null ? "—" : summary.AboveThreshold.ToString();
                builder.AppendLine($"| {MetricCatalog.GetLevelTitle(metric.Level)} | {metric.Title} | {summary.P50} "
                    + $"| {summary.P90} | {summary.P99} | {summary.Max} | {threshold} | {above} |");
            }

            WriteCrap(builder, report);
            foreach (var summary in report.Metrics)
                WriteTop(builder, summary);

            return builder.ToString();
        }

        private static void WriteCrap(StringBuilder builder, MetricsReport report)
        {
            if (report.CrapTop.Count == 0)
                return;

            builder.AppendLine();
            builder.AppendLine("## CRAP");
            builder.AppendLine();
            builder.AppendLine("| CRAP | Покрытие, % | Сложность | Имя | Место |");
            builder.AppendLine("|---:|---:|---:|---|---|");
            foreach (var function in report.CrapTop)
            {
                var crap = function.Crap!.Value.ToString("0.####", CultureInfo.InvariantCulture);
                var coverage = function.Coverage!.Value.ToString("0.##", CultureInfo.InvariantCulture);
                var complexity = function.Values[MetricKey.Cyclomatic];
                var place = $"{Escape(function.File)}:{function.Line}";
                builder.AppendLine($"| {crap} | {coverage} | {complexity} | `{Escape(function.Name)}` | `{place}` |");
            }
        }

        private static void WriteTop(StringBuilder builder, MetricSummary summary)
        {
            if (summary.Top.Count == 0)
                return;

            var metric = summary.Metric;
            builder.AppendLine();
            builder.AppendLine($"## {MetricCatalog.GetLevelTitle(metric.Level)}: {metric.Title.ToLowerInvariant()}");
            builder.AppendLine();

            if (metric.Level == MetricLevel.File)
            {
                builder.AppendLine("| Значение | Файл |");
                builder.AppendLine("|---:|---|");
                foreach (var item in summary.Top)
                    builder.AppendLine($"| {item.Values[metric.Key]} | `{Escape(item.File)}` |");
                return;
            }

            builder.AppendLine("| Значение | Имя | Место |");
            builder.AppendLine("|---:|---|---|");
            foreach (var item in summary.Top)
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

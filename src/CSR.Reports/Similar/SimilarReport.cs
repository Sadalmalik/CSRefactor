using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Similar;

namespace CSR.Reports.Similar
{
    // Отчет csr similar: сколько единиц сравнивалось и найденные пары и кластеры
    public class SimilarReport
    {
        public required int FileCount { get; init; }

        public required int MemberCount { get; init; }

        public required int FragmentCount { get; init; }

        public required double Threshold { get; init; }

        // --changed: слева в каждой паре - измененная единица
        public required bool IsChanged { get; init; }

        public required SimilarResult Result { get; init; }

        public static string GetKindKey(UnitKind kind)
            => kind == UnitKind.Member ? "member" : "fragment";

        public static string FormatScore(double score)
            => score.ToString("0.00", CultureInfo.InvariantCulture);
    }

    // Отчет для агентов: кластеры и пары целиком
    public static class SimilarJsonWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Write(SimilarReport report)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("files", report.FileCount);
                writer.WriteNumber("members", report.MemberCount);
                writer.WriteNumber("fragments", report.FragmentCount);
                writer.WriteNumber("threshold", report.Threshold);
                writer.WriteBoolean("changed", report.IsChanged);

                writer.WriteStartArray("clusters");
                foreach (var cluster in report.Result.Clusters)
                    WriteCluster(writer, cluster);
                writer.WriteEndArray();

                writer.WriteStartArray("pairs");
                foreach (var pair in report.Result.Pairs)
                    WritePair(writer, pair);
                writer.WriteEndArray();

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCluster(Utf8JsonWriter writer, SimilarCluster cluster)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", SimilarReport.GetKindKey(cluster.Kind));
            writer.WriteNumber("pairs", cluster.PairCount);
            writer.WriteNumber("maxScore", Math.Round(cluster.MaxScore, 3));
            writer.WriteNumber("minScore", Math.Round(cluster.MinScore, 3));
            writer.WriteStartArray("units");
            foreach (var unit in cluster.Units)
                WriteUnit(writer, unit);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WritePair(Utf8JsonWriter writer, SimilarPair pair)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", SimilarReport.GetKindKey(pair.Left.Kind));
            writer.WriteNumber("score", Math.Round(pair.Score, 3));
            writer.WriteNumber("windows", pair.Windows);
            writer.WritePropertyName("left");
            WriteUnit(writer, pair.Left);
            writer.WritePropertyName("right");
            WriteUnit(writer, pair.Right);
            writer.WriteEndObject();
        }

        private static void WriteUnit(Utf8JsonWriter writer, SimilarUnit unit)
        {
            writer.WriteStartObject();
            writer.WriteString("file", unit.File);
            writer.WriteNumber("startLine", unit.StartLine);
            writer.WriteNumber("endLine", unit.EndLine);
            writer.WriteString("member", unit.Member);
            writer.WriteNumber("nodes", unit.NodeCount);
            writer.WriteNumber("shapes", unit.Fingerprints.Length);
            writer.WriteEndObject();
        }
    }

    // Отчет для человека: кластеры (крупные первыми); с --changed - пары "измененное -> похожее"
    public static class SimilarMarkdownWriter
    {
        public static string Write(SimilarReport report, int top)
        {
            var result = report.Result;
            var text = new StringBuilder();
            text.AppendLine("# csr similar");
            text.AppendLine();
            text.AppendLine($"Файлов {report.FileCount}, членов {report.MemberCount}, фрагментов {report.FragmentCount}, "
                + $"порог {SimilarReport.FormatScore(report.Threshold)}; пар {result.Pairs.Count}, "
                + $"кластеров {result.Clusters.Count}.");
            if (report.IsChanged)
                WriteChanged(text, result.Pairs, top);
            else
                WriteClusters(text, result.Clusters, top);

            return text.ToString();
        }

        private static void WriteClusters(StringBuilder text, IReadOnlyList<SimilarCluster> clusters, int top)
        {
            var shown = top > 0 ? clusters.Take(top).ToList() : clusters.ToList();
            for (int i = 0; i < shown.Count; i++)
            {
                var cluster = shown[i];
                var kind = cluster.Kind == UnitKind.Member ? "члены" : "фрагменты";
                text.AppendLine();
                text.AppendLine($"## {i + 1}. {kind}: мест {cluster.Units.Count}, пар {cluster.PairCount}, оценка "
                    + FormatRange(cluster.MinScore, cluster.MaxScore));
                text.AppendLine();
                foreach (var unit in cluster.Units)
                    text.AppendLine("- " + FormatUnit(unit));
            }

            if (shown.Count < clusters.Count)
                text.AppendLine().AppendLine($"Показано кластеров {shown.Count} из {clusters.Count} (--top).");
        }

        private static void WriteChanged(StringBuilder text, IReadOnlyList<SimilarPair> pairs, int top)
        {
            var shown = top > 0 ? pairs.Take(top).ToList() : pairs.ToList();
            foreach (var group in shown.GroupBy(pair => pair.Left))
            {
                text.AppendLine();
                text.AppendLine("## " + FormatUnit(group.Key));
                text.AppendLine();
                foreach (var pair in group)
                    text.AppendLine($"- {SimilarReport.FormatScore(pair.Score)}: {FormatUnit(pair.Right)}");
            }

            if (shown.Count < pairs.Count)
                text.AppendLine().AppendLine($"Показано пар {shown.Count} из {pairs.Count} (--top).");
        }

        private static string FormatRange(double min, double max)
        {
            var low = SimilarReport.FormatScore(min);
            var high = SimilarReport.FormatScore(max);
            return low == high ? low : low + "-" + high;
        }

        private static string FormatUnit(SimilarUnit unit)
        {
            var member = unit.Member.Length > 0 ? $" `{unit.Member}`" : "";
            return $"`{unit.File}:{unit.StartLine}-{unit.EndLine}`{member}, узлов {unit.NodeCount}";
        }
    }
}

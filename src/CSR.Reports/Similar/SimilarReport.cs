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

        public required IReadOnlyDictionary<UnitKind, int> UnitCounts { get; init; }

        public required SimilarOptions Options { get; init; }

        // --changed: слева в каждой паре - измененная единица
        public required bool IsChanged { get; init; }

        public required SimilarResult Result { get; init; }

        public int GetCount(UnitKind kind)
            => UnitCounts.TryGetValue(kind, out int count) ? count : 0;

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
                writer.WriteNumber("members", report.GetCount(UnitKind.Member));
                writer.WriteNumber("fragments", report.GetCount(UnitKind.Fragment));
                writer.WriteNumber("parameterSets", report.GetCount(UnitKind.ParameterSet));
                writer.WriteNumber("fieldSets", report.GetCount(UnitKind.FieldSet));
                writer.WriteNumber("types", report.GetCount(UnitKind.Type));
                writer.WriteNumber("threshold", report.Options.Threshold);
                writer.WriteNumber("setThreshold", report.Options.SetThreshold);
                writer.WriteNumber("typeThreshold", report.Options.TypeThreshold);
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
            writer.WriteString("kind", UnitKinds.GetKey(cluster.Kind));
            writer.WriteNumber("pairs", cluster.PairCount);
            writer.WriteNumber("maxScore", Math.Round(cluster.MaxScore, 3));
            writer.WriteNumber("minScore", Math.Round(cluster.MinScore, 3));
            if (cluster.Common.Count > 0)
                WriteStrings(writer, "common", cluster.Common);

            writer.WriteStartArray("units");
            foreach (var unit in cluster.Units)
            {
                var own = cluster.Differences.TryGetValue(unit, out IReadOnlyList<string>? differences) ? differences : null;
                var bodies = cluster.Bodies.TryGetValue(unit, out IReadOnlyList<string>? described) ? described : null;
                WriteUnit(writer, unit, own, bodies);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WritePair(Utf8JsonWriter writer, SimilarPair pair)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", UnitKinds.GetKey(pair.Left.Kind));
            writer.WriteNumber("score", Math.Round(pair.Score, 3));
            writer.WriteNumber("windows", pair.Windows);
            writer.WritePropertyName("left");
            WriteUnit(writer, pair.Left, null, null);
            writer.WritePropertyName("right");
            WriteUnit(writer, pair.Right, null, null);
            writer.WriteEndObject();
        }

        private static void WriteUnit(
            Utf8JsonWriter writer,
            SimilarUnit unit,
            IReadOnlyList<string>? own,
            IReadOnlyList<string>? bodies)
        {
            writer.WriteStartObject();
            writer.WriteString("file", unit.File);
            writer.WriteNumber("startLine", unit.StartLine);
            writer.WriteNumber("endLine", unit.EndLine);
            writer.WriteString("member", unit.Member);
            writer.WriteNumber("nodes", unit.NodeCount);
            writer.WriteNumber("shapes", unit.Fingerprints.Length);
            if (own != null)
                WriteStrings(writer, "own", own);

            if (bodies != null)
                WriteStrings(writer, "bodies", bodies);

            writer.WriteEndObject();
        }

        private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
        {
            writer.WriteStartArray(name);
            foreach (var value in values)
                writer.WriteStringValue(value);
            writer.WriteEndArray();
        }
    }

    // Отчет для человека: кластеры (крупные первыми) с общей частью и отличиями; с --changed - пары
    // "измененное -> похожее"
    public static class SimilarMarkdownWriter
    {
        // Сколько элементов общей части и отличий показывать в строке
        private const int ShownElements = 12;

        public static string Write(SimilarReport report, int top)
        {
            var result = report.Result;
            var options = report.Options;
            var text = new StringBuilder();
            text.AppendLine("# csr similar");
            text.AppendLine();
            text.AppendLine($"Файлов {report.FileCount}, членов {report.GetCount(UnitKind.Member)}, "
                + $"фрагментов {report.GetCount(UnitKind.Fragment)}, "
                + $"наборов параметров {report.GetCount(UnitKind.ParameterSet)}, "
                + $"наборов полей {report.GetCount(UnitKind.FieldSet)}, типов {report.GetCount(UnitKind.Type)}; "
                + $"пороги {SimilarReport.FormatScore(options.Threshold)} (L1), "
                + $"{SimilarReport.FormatScore(options.SetThreshold)} (L2), "
                + $"{SimilarReport.FormatScore(options.TypeThreshold)} (L3); "
                + $"пар {result.Pairs.Count}, кластеров {result.Clusters.Count}.");
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
                text.AppendLine();
                text.AppendLine($"## {i + 1}. {GetKindTitle(cluster.Kind)}: мест {cluster.Units.Count}, пар {cluster.PairCount}, "
                    + "оценка " + FormatRange(cluster.MinScore, cluster.MaxScore));
                text.AppendLine();
                if (cluster.Common.Count > 0)
                    text.AppendLine("Общее: " + JoinShort(cluster.Common)).AppendLine();

                foreach (var unit in cluster.Units)
                {
                    text.AppendLine("- " + FormatUnit(unit));
                    if (cluster.Differences.TryGetValue(unit, out IReadOnlyList<string>? own) && own.Count > 0)
                        text.AppendLine("  - своё: " + JoinShort(own));

                    if (!cluster.Bodies.TryGetValue(unit, out IReadOnlyList<string>? bodies))
                        continue;

                    foreach (var line in bodies)
                        text.AppendLine("  - " + line);
                }
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
                text.AppendLine($"## {GetKindTitle(group.Key.Kind)}: {FormatUnit(group.Key)}");
                text.AppendLine();
                foreach (var pair in group)
                    text.AppendLine($"- {SimilarReport.FormatScore(pair.Score)}: {FormatUnit(pair.Right)}");
            }

            if (shown.Count < pairs.Count)
                text.AppendLine().AppendLine($"Показано пар {shown.Count} из {pairs.Count} (--top).");
        }

        private static string JoinShort(IReadOnlyList<string> values)
        {
            var shown = string.Join(", ", values.Take(ShownElements).Select(value => $"`{value}`"));
            return values.Count > ShownElements ? $"{shown} и еще {values.Count - ShownElements}" : shown;
        }

        private static string GetKindTitle(UnitKind kind)
        {
            return kind switch
            {
                UnitKind.Member => "члены",
                UnitKind.Fragment => "фрагменты",
                UnitKind.ParameterSet => "наборы параметров",
                UnitKind.FieldSet => "наборы полей",
                _ => "типы",
            };
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
            var size = unit.Kind switch
            {
                UnitKind.ParameterSet or UnitKind.FieldSet => $"элементов {unit.NodeCount}",
                UnitKind.Type => $"полей и членов {unit.NodeCount}",
                _ => $"узлов {unit.NodeCount}",
            };
            return $"`{unit.File}:{unit.StartLine}-{unit.EndLine}`{member}, {size}";
        }
    }
}

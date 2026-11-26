using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Graph;
using CSR.Core.Similar;
using CSR.Core.Text;

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

        // Инвентарь систем вариантов (L4); null - системы не искались
        public IReadOnlyList<ExtensionHost>? Inventory { get; init; }

        // Хостов у каждой точки расширения инвентаря
        public int GetHostCount(ExtensionHost entry)
            => Inventory?.Count(other => other.ExtensionPoint == entry.ExtensionPoint) ?? 0;

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
                writer.WriteNumber("systems", report.GetCount(UnitKind.System));
                writer.WriteNumber("threshold", report.Options.Threshold);
                writer.WriteNumber("setThreshold", report.Options.SetThreshold);
                writer.WriteNumber("typeThreshold", report.Options.TypeThreshold);
                writer.WriteNumber("systemThreshold", report.Options.SystemThreshold);
                writer.WriteBoolean("changed", report.IsChanged);

                writer.WriteStartArray("clusters");
                foreach (var cluster in report.Result.Clusters)
                    WriteCluster(writer, cluster);
                writer.WriteEndArray();

                writer.WriteStartArray("pairs");
                foreach (var pair in report.Result.Pairs)
                    WritePair(writer, pair);
                writer.WriteEndArray();

                if (report.Inventory != null)
                    WriteInventory(writer, report.Inventory);

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCluster(Utf8JsonWriter writer, SimilarCluster cluster)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", UnitKindTool.GetKey(cluster.Kind));
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
            writer.WriteString("kind", UnitKindTool.GetKey(pair.Left.Kind));
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

        // Инвентарь: хост, база вариантов, поле или коллекция, варианты, результат, хостов у базы
        private static void WriteInventory(Utf8JsonWriter writer, IReadOnlyList<ExtensionHost> inventory)
        {
            var hostCounts = inventory.GroupBy(entry => entry.ExtensionPoint).ToDictionary(
                group => group.Key,
                group => group.Count());
            writer.WriteStartArray("inventory");
            foreach (var entry in inventory)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("host");
                WriteNode(writer, entry.Host);
                writer.WritePropertyName("extensionPoint");
                WriteNode(writer, entry.ExtensionPoint);
                writer.WriteString("holder", entry.IsCollection ? "collection" : "field");
                writer.WriteNumber("variantCount", entry.Variants.Count);
                WriteStrings(writer, "variants", entry.Variants.Select(node => node.Name).ToList());
                WriteStrings(writer, "results", entry.Results.Select(node => node.Name).ToList());
                writer.WriteNumber("hostsOfExtensionPoint", hostCounts[entry.ExtensionPoint]);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteNode(Utf8JsonWriter writer, TypeNode node)
        {
            writer.WriteStartObject();
            writer.WriteString("name", node.Name);
            writer.WriteString("file", node.File);
            writer.WriteNumber("startLine", node.StartLine);
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
                + $"наборов полей {report.GetCount(UnitKind.FieldSet)}, типов {report.GetCount(UnitKind.Type)}, "
                + $"систем {report.GetCount(UnitKind.System)}; "
                + $"пороги {SimilarReport.FormatScore(options.Threshold)} (L1), "
                + $"{SimilarReport.FormatScore(options.SetThreshold)} (L2), "
                + $"{SimilarReport.FormatScore(options.TypeThreshold)} (L3), "
                + $"{SimilarReport.FormatScore(options.SystemThreshold)} (L4); "
                + $"пар {result.Pairs.Count}, кластеров {result.Clusters.Count}.");
            if (report.IsChanged)
                WriteChanged(text, result.Pairs, top);
            else
                WriteClusters(text, result.Clusters, top);

            if (report.Inventory != null)
                WriteInventory(text, report.Inventory, top);

            return text.ToString();
        }

        private static void WriteClusters(StringBuilder text, IReadOnlyList<SimilarCluster> clusters, int top)
        {
            var shown = clusters.TakeTop(top).ToList();
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

        // Инвентарь систем вариантов по базам: сначала базы с большим числом вариантов
        private static void WriteInventory(StringBuilder text, IReadOnlyList<ExtensionHost> inventory, int top)
        {
            var points = inventory.GroupBy(entry => entry.ExtensionPoint)
                .OrderByDescending(group => group.Key.In.Count)
                .ThenBy(group => group.Key.Name, StringComparer.Ordinal)
                .ToList();
            text.AppendLine();
            text.AppendLine($"## Системы вариантов: баз {points.Count}, хостов {inventory.Count}");
            var shown = points.TakeTop(top).ToList();
            foreach (var point in shown)
            {
                var first = point.First();
                var node = point.Key;
                text.AppendLine();
                text.AppendLine($"- база `{node.Name}` (`{node.File}:{node.StartLine}`), вариантов {first.Variants.Count}, "
                    + $"хостов {point.Count()}");
                text.AppendLine("  - хосты: " + JoinShort(point.Select(entry => entry.Host.Name).Distinct().ToList()));
                text.AppendLine("  - варианты: " + JoinShort(first.Variants.Select(variant => variant.Name).ToList()));
                if (first.Results.Count > 0)
                    text.AppendLine("  - результат: " + JoinShort(first.Results.Select(result => result.Name).ToList()));
            }

            if (shown.Count < points.Count)
                text.AppendLine().AppendLine($"Показано баз {shown.Count} из {points.Count} (--top).");
        }

        private static void WriteChanged(StringBuilder text, IReadOnlyList<SimilarPair> pairs, int top)
        {
            var shown = pairs.TakeTop(top).ToList();
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
            => FindingTextTools.JoinNames(values.Select(value => $"`{value}`").ToList(), ShownElements);

        private static string GetKindTitle(UnitKind kind)
        {
            return kind switch
            {
                UnitKind.Member => "члены",
                UnitKind.Fragment => "фрагменты",
                UnitKind.ParameterSet => "наборы параметров",
                UnitKind.FieldSet => "наборы полей",
                UnitKind.Type => "типы",
                _ => "системы",
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
                UnitKind.System => $"типов {unit.NodeCount}",
                _ => $"узлов {unit.NodeCount}",
            };
            return $"`{unit.File}:{unit.StartLine}-{unit.EndLine}`{member}, {size}";
        }
    }
}

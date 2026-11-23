namespace CSR.Core.Graph
{
    // Хост точки расширения: тип держит полем или коллекцией абстрактный класс или интерфейс с вариантами.
    // Роли: хост, база вариантов, варианты, результат (что возвращают члены базы, иначе - варианты)
    public class ExtensionHost
    {
        public required TypeNode Host { get; init; }

        public required TypeNode ExtensionPoint { get; init; }

        // Поле (одна ссылка) или коллекция
        public required bool IsCollection { get; init; }

        public required IReadOnlyList<TypeNode> Variants { get; init; }

        public required IReadOnlyList<TypeNode> Results { get; init; }
    }

    // Инвентарь систем вариантов по графу типов: каждый хост каждой точки расширения с не меньше minVariants
    // вариантами (прямые наследники и реализации)
    public static class ExtensionHosts
    {
        public static List<ExtensionHost> Find(TypeGraph graph, int minVariants)
        {
            var hosts = new List<ExtensionHost>();
            foreach (var host in graph.Nodes.Where(node => node.Kind is TypeNodeKind.Class or TypeNodeKind.Struct
                or TypeNodeKind.Record))
            {
                var used = host.GetOut(EdgeKind.Collection, EdgeKind.Field)
                    .Where(edge => edge.To.IsExtensionPoint && edge.To != host)
                    .GroupBy(edge => edge.To);
                foreach (var group in used)
                {
                    var point = group.Key;
                    var variants = GetVariants(point);
                    if (variants.Count < minVariants)
                        continue;

                    hosts.Add(new ExtensionHost
                    {
                        Host = host,
                        ExtensionPoint = point,
                        IsCollection = group.Any(edge => edge.Kind == EdgeKind.Collection),
                        Variants = variants,
                        Results = GetResults(point, variants),
                    });
                }
            }

            return hosts
                .OrderBy(entry => entry.Host.File, StringComparer.Ordinal)
                .ThenBy(entry => entry.Host.StartLine)
                .ToList();
        }

        public static List<TypeNode> GetVariants(TypeNode point)
        {
            return point.GetIn(EdgeKind.Inherits, EdgeKind.Implements)
                .Select(edge => edge.From)
                .Distinct()
                .OrderBy(node => node.Name, StringComparer.Ordinal)
                .ToList();
        }

        // Результат: типы, которые возвращают члены базы; если у базы их нет - общие для вариантов
        private static List<TypeNode> GetResults(TypeNode point, IReadOnlyList<TypeNode> variants)
        {
            var own = point.GetOut(EdgeKind.Returns).Select(edge => edge.To).Where(node => node != point).ToList();
            if (own.Count > 0)
                return own.Distinct().ToList();

            return variants.SelectMany(variant => variant.GetOut(EdgeKind.Returns).Select(edge => edge.To))
                .Where(node => node != point && !variants.Contains(node))
                .GroupBy(node => node)
                .Where(group => group.Count() >= 2)
                .Select(group => group.Key)
                .ToList();
        }
    }
}

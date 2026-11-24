using CSR.Core.Graph;

namespace CSR.Core.Structure
{
    // Метрики пакета - папки или пространства имен (Мартин; Ньюман):
    // - Ca - типы снаружи, которые зависят от типов пакета; Ce - типы снаружи, от которых зависят типы пакета;
    // - нестабильность I = Ce / (Ca + Ce), абстрактность A - доля абстрактных классов и интерфейсов,
    //   расстояние D = |A + I - 1|;
    // - внутренний граф (ребра между типами пакета, без направления): сообщества (распространение меток),
    //   модулярность Q, доля внешних ребер. Варианты одной базы (общий прямой предок, хоть и вне пакета) связаны
    //   между собой: папка независимых вариантов - каталог расширений, а не свалка
    public class PackageMetrics
    {
        public required string Name { get; init; }

        public required IReadOnlyList<TypeNode> Types { get; init; }

        public required int Afferent { get; init; }

        public required int Efferent { get; init; }

        public required int InternalEdges { get; init; }

        public required int ExternalEdges { get; init; }

        public required int Communities { get; init; }

        public required double Modularity { get; init; }

        // Доля типов без внутренних связей
        public required double IsolatedShare { get; init; }

        public double Instability
            => Afferent + Efferent == 0 ? 0 : (double)Efferent / (Afferent + Efferent);

        public double Abstractness
            => Types.Count == 0 ? 0 : (double)Types.Count(type => type.IsExtensionPoint) / Types.Count;

        public double Distance
            => Math.Abs(Abstractness + Instability - 1);

        public double ExternalShare
            => InternalEdges + ExternalEdges == 0 ? 0 : (double)ExternalEdges / (InternalEdges + ExternalEdges);

        // Пакеты по ключу узла (папка файла или пространство имен)
        public static List<PackageMetrics> Build(TypeGraph graph, Func<TypeNode, string> getPackage)
        {
            var packages = graph.Nodes.GroupBy(getPackage).ToDictionary(group => group.Key, group => group.ToList());
            var result = new List<PackageMetrics>();
            foreach (var pair in packages)
            {
                var members = new HashSet<TypeNode>(pair.Value);
                var outside = pair.Value.SelectMany(node => node.Out).Where(edge => !members.Contains(edge.To)).ToList();
                var inside = pair.Value.SelectMany(node => node.Out)
                    .Where(edge => members.Contains(edge.To) && edge.From != edge.To)
                    .ToList();
                var incoming = pair.Value.SelectMany(node => node.In).Where(edge => !members.Contains(edge.From));
                var linked = inside.Concat(GetSiblingEdges(pair.Value)).ToList();
                var communities = LabelCommunities.Find(pair.Value, linked);
                var touched = new HashSet<TypeNode>(linked.SelectMany(edge => new[] { edge.From, edge.To }));
                result.Add(new PackageMetrics
                {
                    Name = pair.Key,
                    Types = pair.Value,
                    Afferent = incoming.Select(edge => edge.From).Distinct().Count(),
                    Efferent = outside.Select(edge => edge.To).Distinct().Count(),
                    InternalEdges = inside.Count,
                    ExternalEdges = outside.Count,
                    Communities = communities.Count,
                    Modularity = LabelCommunities.GetModularity(communities, linked),
                    IsolatedShare = pair.Value.Count(node => !touched.Contains(node)) / (double)pair.Value.Count,
                });
            }

            return result.OrderBy(package => package.Name, StringComparer.Ordinal).ToList();
        }

        // Ребра между вариантами одной прямой базы: цепочкой, чтобы не было квадрата
        private static IEnumerable<TypeEdge> GetSiblingEdges(IReadOnlyList<TypeNode> types)
        {
            var byBase = types.SelectMany(type => type.GetOut(EdgeKind.Inherits, EdgeKind.Implements)
                    .Select(edge => (Base: edge.To, Type: type)))
                .GroupBy(pair => pair.Base);
            foreach (var group in byBase)
            {
                var siblings = group.Select(pair => pair.Type).Distinct().ToList();
                for (int i = 1; i < siblings.Count; i++)
                    yield return new TypeEdge { From = siblings[i - 1], To = siblings[i], Kind = EdgeKind.Inherits };
            }
        }
    }
}

using CSR.Core.Graph;

namespace CSR.Core.Structure
{
    // Сообщества внутреннего графа пакета: распространение меток по ребрам без направления (узел берет самую частую
    // метку соседей, при равенстве - меньшую), порядок обхода - по ключу, не больше Iterations проходов.
    // Модулярность Ньюмана: Q = сумма по сообществам (внутренних ребер / m - (степеней / 2m)^2)
    public static class LabelCommunityTool
    {
        private const int Iterations = 20;

        public static List<List<TypeNode>> Find(IReadOnlyList<TypeNode> nodes, IReadOnlyList<TypeEdge> edges)
        {
            var ordered = nodes.OrderBy(node => node.Key, StringComparer.Ordinal).ToList();
            var labels = new Dictionary<TypeNode, int>();
            for (int i = 0; i < ordered.Count; i++)
                labels[ordered[i]] = i;

            var neighbours = ordered.ToDictionary(node => node, _ => new List<TypeNode>());
            foreach (var edge in edges)
            {
                neighbours[edge.From].Add(edge.To);
                neighbours[edge.To].Add(edge.From);
            }

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                bool isChanged = false;
                foreach (var node in ordered)
                {
                    var around = neighbours[node];
                    if (around.Count == 0)
                        continue;

                    int best = around.GroupBy(neighbour => labels[neighbour])
                        .OrderByDescending(group => group.Count())
                        .ThenBy(group => group.Key)
                        .First()
                        .Key;
                    if (best == labels[node])
                        continue;

                    labels[node] = best;
                    isChanged = true;
                }

                if (!isChanged)
                    break;
            }

            return ordered.GroupBy(node => labels[node]).Select(group => group.ToList()).ToList();
        }

        public static double GetModularity(IReadOnlyList<List<TypeNode>> communities, IReadOnlyList<TypeEdge> edges)
        {
            if (edges.Count == 0)
                return 0;

            var community = new Dictionary<TypeNode, int>();
            for (int i = 0; i < communities.Count; i++)
            {
                foreach (var node in communities[i])
                    community[node] = i;
            }

            double total = edges.Count;
            var inside = new double[communities.Count];
            var degrees = new double[communities.Count];
            foreach (var edge in edges)
            {
                int from = community[edge.From];
                int to = community[edge.To];
                degrees[from]++;
                degrees[to]++;
                if (from == to)
                    inside[from]++;
            }

            double modularity = 0;
            for (int i = 0; i < communities.Count; i++)
                modularity += inside[i] / total - Math.Pow(degrees[i] / (2 * total), 2);

            return modularity;
        }
    }
}

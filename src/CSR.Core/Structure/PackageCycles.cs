using CSR.Core.Graph;

namespace CSR.Core.Structure
{
    // Связь между двумя пакетами: сколько ребер типов идет из From в To и пример
    public class PackageLink
    {
        public required string From { get; init; }

        public required string To { get; init; }

        public required int Count { get; init; }

        public required TypeEdge Example { get; init; }

        // Ребер типов в обратную сторону (To -> From); 0 - обратной связи нет
        public int Reverse { get; init; }
    }

    // Цикл зависимостей пакетов: сильно связная компонента из двух и больше пакетов. Замыкающие связи - сначала те,
    // у которых есть обратная связь (цикл из двух пакетов), по отношению "ребер туда / ребер обратно" (1 туда при
    // 50 обратно - кандидат на разворот), затем остальные по возрастанию числа ребер
    public class PackageCycle
    {
        public required IReadOnlyList<string> Packages { get; init; }

        public required IReadOnlyList<PackageLink> Links { get; init; }

        public static List<PackageCycle> Find(TypeGraph graph, Func<TypeNode, string> getPackage)
        {
            var links = graph.Nodes.SelectMany(node => node.Out)
                .Where(edge => getPackage(edge.From) != getPackage(edge.To))
                .GroupBy(edge => (From: getPackage(edge.From), To: getPackage(edge.To)))
                .Select(group => new PackageLink
                {
                    From = group.Key.From,
                    To = group.Key.To,
                    Count = group.Sum(edge => edge.Count),
                    Example = group.First(),
                })
                .ToList();

            var packages = graph.Nodes.Select(getPackage).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
            var outgoing = packages.ToDictionary(name => name, _ => new List<string>());
            foreach (var link in links)
                outgoing[link.From].Add(link.To);

            var cycles = new List<PackageCycle>();
            foreach (var component in StrongComponentTool.Find(packages, outgoing).Where(component => component.Count > 1))
            {
                var members = new HashSet<string>(component);
                var inside = links.Where(link => members.Contains(link.From) && members.Contains(link.To)).ToList();
                var counts = inside.ToDictionary(link => link.From + "\n" + link.To, link => link.Count);
                cycles.Add(new PackageCycle
                {
                    Packages = component.OrderBy(name => name, StringComparer.Ordinal).ToList(),
                    Links = inside
                        .Select(link => new PackageLink
                        {
                            From = link.From,
                            To = link.To,
                            Count = link.Count,
                            Example = link.Example,
                            Reverse = counts.TryGetValue(link.To + "\n" + link.From, out int reverse) ? reverse : 0,
                        })
                        .OrderByDescending(link => link.Reverse > 0)
                        .ThenBy(link => link.Reverse > 0 ? (double)link.Count / link.Reverse : link.Count)
                        .ThenBy(link => link.From, StringComparer.Ordinal)
                        .ToList(),
                });
            }

            return cycles.OrderByDescending(cycle => cycle.Packages.Count).ToList();
        }
    }

    // Сильно связные компоненты (Тарьян, без рекурсии)
    public static class StrongComponentTool
    {
        public static List<List<string>> Find(IReadOnlyList<string> nodes, IReadOnlyDictionary<string, List<string>> outgoing)
        {
            var index = new Dictionary<string, int>();
            var low = new Dictionary<string, int>();
            var onStack = new HashSet<string>();
            var stack = new Stack<string>();
            var components = new List<List<string>>();
            int counter = 0;
            foreach (var start in nodes)
            {
                if (index.ContainsKey(start))
                    continue;

                var work = new Stack<(string Node, int Next)>();
                work.Push((start, 0));
                index[start] = low[start] = counter++;
                stack.Push(start);
                onStack.Add(start);
                while (work.Count > 0)
                {
                    var (node, next) = work.Pop();
                    var targets = outgoing[node];
                    if (next < targets.Count)
                    {
                        work.Push((node, next + 1));
                        var target = targets[next];
                        if (!index.ContainsKey(target))
                        {
                            index[target] = low[target] = counter++;
                            stack.Push(target);
                            onStack.Add(target);
                            work.Push((target, 0));
                        }
                        else if (onStack.Contains(target))
                        {
                            low[node] = Math.Min(low[node], index[target]);
                        }

                        continue;
                    }

                    if (work.Count > 0)
                    {
                        var parent = work.Peek().Node;
                        low[parent] = Math.Min(low[parent], low[node]);
                    }

                    if (low[node] != index[node])
                        continue;

                    var component = new List<string>();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    }
                    while (member != node);

                    components.Add(component);
                }
            }

            return components;
        }
    }
}

using CSR.Core.Graph;

namespace CSR.Core.Structure
{
    // Звено цепочки владения: хост держит базу вариантов, дальше идет через ее вариант (или его наследника)
    public class OwnershipStep
    {
        public required TypeNode Host { get; init; }

        public required TypeNode ExtensionPoint { get; init; }
    }

    // Самая длинная цепочка вложенных полиморфных частей от типа
    public class OwnershipChain
    {
        public required TypeNode Root { get; init; }

        public required IReadOnlyList<OwnershipStep> Steps { get; init; }

        public int Depth
            => Steps.Count;
    }

    // Глубина вложенности владения: тип держит базу вариантов (полем или коллекцией, инвентарь ExtensionHosts) -
    // уровень 1; вариант этой базы или наследник варианта держит свою базу вариантов - уровень 2; и так далее.
    // Пример: агент движения -> движения -> составное движение и его наследник-цепочка -> состояния.
    // Не уровни:
    // - повтор базы в цепочке - рекурсия (выражение держит выражения, вычислитель - вычислитель), один уровень;
    // - инфраструктурная база (больше maxHosts хостов: действия, вычислители, условия, которые держат полем сотни
    //   типов) - данные, а не вложенная часть системы;
    // - реестр (больше maxVariants вариантов: части сущности, обработчики событий, сетевые объекты) - его хост держит
    //   все подряд, это не вложенность одной системы;
    // - реализация интерфейса-возможности типом другой системы: через интерфейс цепочка идет только в варианты без
    //   своей базы из исходного кода (агент движения - часть сущности, а не часть держателя списка ITagSource).
    // Цикл обрывается: тип, который уже есть на пути, дает 0
    public static class OwnershipDepth
    {
        public static List<OwnershipChain> Find(TypeGraph graph, int minVariants, int maxHosts, int maxVariants)
        {
            var entries = ExtensionHosts.Find(graph, minVariants);
            var infrastructure = new HashSet<TypeNode>(entries.GroupBy(entry => entry.ExtensionPoint)
                .Where(group => group.Count() > maxHosts || group.First().Variants.Count > maxVariants)
                .Select(group => group.Key));
            var hosted = entries.Where(entry => !infrastructure.Contains(entry.ExtensionPoint))
                .GroupBy(entry => entry.Host)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.ExtensionPoint).Distinct().ToList());
            var chains = new List<OwnershipChain>();
            foreach (var host in hosted.Keys)
            {
                var steps = GetLongest(host, hosted, infrastructure, new HashSet<TypeNode>(), new HashSet<TypeNode>());
                chains.Add(new OwnershipChain { Root = host, Steps = steps });
            }

            return chains
                .OrderByDescending(chain => chain.Depth)
                .ThenBy(chain => chain.Root.File, StringComparer.Ordinal)
                .ThenBy(chain => chain.Root.StartLine)
                .ToList();
        }

        private static List<OwnershipStep> GetLongest(
            TypeNode host,
            Dictionary<TypeNode, List<TypeNode>> hosted,
            HashSet<TypeNode> infrastructure,
            HashSet<TypeNode> path,
            HashSet<TypeNode> usedPoints)
        {
            if (!hosted.TryGetValue(host, out List<TypeNode>? points) || !path.Add(host))
                return new List<OwnershipStep>();

            var best = new List<OwnershipStep>();
            foreach (var point in points.Where(point => !usedPoints.Contains(point)))
            {
                usedPoints.Add(point);
                var tail = new List<OwnershipStep>();
                foreach (var part in GetParts(point, infrastructure).Where(part => !path.Contains(part)))
                {
                    var candidate = GetLongest(part, hosted, infrastructure, path, usedPoints);
                    if (candidate.Count > tail.Count)
                        tail = candidate;
                }

                usedPoints.Remove(point);
                if (tail.Count + 1 > best.Count)
                    best = new[] { new OwnershipStep { Host = host, ExtensionPoint = point } }.Concat(tail).ToList();
            }

            path.Remove(host);
            return best;
        }

        // Части базы: варианты и все их наследники (наследник-цепочка держит поля своих баз). У интерфейса - только
        // варианты без базового класса из исходного кода. В наследников инфраструктуры и реестра не спускаемся:
        // EntityPart реализует ITagSource, но части сущности - не части держателя ITagSource
        private static IEnumerable<TypeNode> GetParts(TypeNode point, HashSet<TypeNode> infrastructure)
        {
            var seen = new HashSet<TypeNode>();
            var variants = ExtensionHosts.GetVariants(point);
            if (point.Kind == TypeNodeKind.Interface)
                variants = variants.Where(variant => !variant.GetOut(EdgeKind.Inherits).Any()).ToList();

            var queue = new Queue<TypeNode>(variants);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!seen.Add(node))
                    continue;

                yield return node;
                if (infrastructure.Contains(node))
                    continue;

                foreach (var child in node.GetIn(EdgeKind.Inherits).Select(edge => edge.From))
                    queue.Enqueue(child);
            }
        }
    }
}

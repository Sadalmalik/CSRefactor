using CSR.Core.Checks;
using CSR.Core.Graph;
using CSR.Core.Metrics;
using Microsoft.CodeAnalysis;

namespace CSR.Core.Structure
{
    // Сборка (проект или asmdef): строки кода файлов цели и зависимости по использованию типов - тип сборки
    // использует тип другой сборки. Ссылка манифеста без использования типов не видна
    public class AssemblyMetrics
    {
        public required string Name { get; init; }

        public required int Lines { get; init; }

        public required int Types { get; init; }

        // Сборки, от которых зависит эта, по имени
        public required IReadOnlyList<string> Dependencies { get; init; }

        public bool IsSmall(StructureOptions options)
            => options.MinAssemblyLines > 0 && Lines < options.MinAssemblyLines;

        // getAssembly - сборка файла по пути дерева или null (файл вне сборок)
        public static List<AssemblyMetrics> Build(
            TypeGraph graph,
            IReadOnlyCollection<SyntaxTree> trees,
            Func<string, string?> getAssembly)
        {
            var lines = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var tree in trees)
            {
                var assembly = getAssembly(tree.FilePath);
                if (assembly == null)
                    continue;

                lines.TryGetValue(assembly, out int count);
                lines[assembly] = count + LineCounter.CountCode(tree.GetRoot(), tree.GetText());
            }

            var nodes = graph.Nodes.Where(node => getAssembly(node.File) != null)
                .ToLookup(node => getAssembly(node.File)!);
            return lines.Keys.OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new AssemblyMetrics
                {
                    Name = name,
                    Lines = lines[name],
                    Types = nodes[name].Count(),
                    Dependencies = nodes[name].SelectMany(node => node.Out)
                        .Select(edge => getAssembly(edge.To.File))
                        .OfType<string>()
                        .Where(other => other != name)
                        .Distinct()
                        .OrderBy(other => other, StringComparer.Ordinal)
                        .ToList(),
                })
                .ToList();
        }
    }

    // Самая длинная цепочка зависимостей между сборками слоя: шаги - сборки, сборки одного цикла - один шаг
    public class LayerChain
    {
        public required string Layer { get; init; }

        // Шаг - имена сборок; больше одной - цикл
        public required IReadOnlyList<IReadOnlyList<string>> Steps { get; init; }

        // Число зависимостей в цепочке; слой без сборок или без зависимостей внутри - 0
        public int Depth
            => Math.Max(Steps.Count - 1, 0);

        public bool IsDeep(StructureOptions options)
            => options.LayerDepth > 0 && Depth > options.LayerDepth;

        // layers - имя слоя и маски имен его сборок (* - любые символы)
        public static List<LayerChain> Find(
            IReadOnlyList<AssemblyMetrics> assemblies,
            IReadOnlyDictionary<string, List<string>> layers)
        {
            return layers.OrderBy(layer => layer.Key, StringComparer.Ordinal)
                .Select(layer => FindLongest(layer.Key, assemblies, NameMask.Get(layer.Value)))
                .ToList();
        }

        private static LayerChain FindLongest(string layer, IReadOnlyList<AssemblyMetrics> assemblies, NameMask mask)
        {
            var members = assemblies.Where(assembly => mask.IsMatch(assembly.Name)).ToList();
            var names = members.Select(assembly => assembly.Name).ToList();
            var inLayer = new HashSet<string>(names, StringComparer.Ordinal);
            var outgoing = members.ToDictionary(
                assembly => assembly.Name,
                assembly => assembly.Dependencies.Where(inLayer.Contains).ToList());

            // Сжатие циклов: компоненты Тарьяна выходят в обратном топологическом порядке - зависимости раньше
            var components = StrongComponents.Find(names, outgoing);
            var componentOf = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < components.Count; i++)
            {
                foreach (var name in components[i])
                    componentOf[name] = i;
            }

            var longest = new List<IReadOnlyList<IReadOnlyList<string>>>();
            for (int i = 0; i < components.Count; i++)
            {
                var next = components[i].SelectMany(name => outgoing[name])
                    .Select(name => componentOf[name])
                    .Where(target => target != i)
                    .Distinct()
                    .Select(target => longest[target])
                    .OrderByDescending(chain => chain.Count)
                    .FirstOrDefault() ?? [];
                var step = components[i].OrderBy(name => name, StringComparer.Ordinal).ToList();
                longest.Add([step, ..next]);
            }

            var steps = longest.OrderByDescending(chain => chain.Count).FirstOrDefault() ?? [];
            return new LayerChain { Layer = layer, Steps = steps };
        }
    }
}

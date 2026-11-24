using CSR.Core.Graph;
using Microsoft.CodeAnalysis;

namespace CSR.Core.Structure
{
    // Пороги структурных метрик (structure.jsonc). Превышение - отметка в отчете, а не находка
    public class StructureOptions
    {
        // Глубина вложенности владения: больше - отметка (слова пользователя: три уровня - предел)
        public int OwnershipDepth { get; set; } = 3;

        // Глубина наследования среди типов исходного кода: больше - отметка
        public int InheritanceDepth { get; set; } = 4;

        // Объект-бог (Lanza - Marinescu): WMC не меньше, ATFD больше и TCC меньше порогов
        public int GodWmc { get; set; } = 47;

        public int GodAtfd { get; set; } = 5;

        public double GodTcc { get; set; } = 1.0 / 3;

        // Свалка: пакет не меньше DumpSize типов и сообществ не меньше DumpCommunities или модулярность не больше
        // DumpModularity при доле изолированных типов не меньше половины
        public int DumpSize { get; set; } = 30;

        public int DumpCommunities { get; set; } = 8;

        // База вариантов с большим числом хостов - инфраструктура: в глубине владения не уровень
        public int InfrastructureHosts { get; set; } = 5;

        // База вариантов с большим числом вариантов - реестр (части сущности, обработчики событий): не уровень
        public int RegistryVariants { get; set; } = 30;

        // Размер топов отчета
        public int Top { get; set; } = 10;
    }

    // Метрики типа на графе и по телам
    public class TypeMetrics
    {
        public required TypeNode Node { get; init; }

        // Глубина наследования среди типов исходного кода (база вне исходного кода не считается)
        public required int InheritanceDepth { get; init; }

        // Прямые наследники и реализации
        public required int Children { get; init; }

        // Связанные типы: разные соседи по ребрам в обе стороны
        public required int Coupling { get; init; }

        // Входящая степень: сколько разных типов зависят от этого
        public required int Dependents { get; init; }

        public TypeFacts? Facts { get; init; }

        public bool IsGod(StructureOptions options)
        {
            return Facts != null && Facts.Wmc >= options.GodWmc && Facts.Atfd > options.GodAtfd
                && Facts.Tcc < options.GodTcc;
        }
    }

    public class StructureResult
    {
        public required TypeGraph Graph { get; init; }

        public required IReadOnlyList<TypeMetrics> Types { get; init; }

        public required IReadOnlyList<OwnershipChain> Ownership { get; init; }

        public required IReadOnlyList<PackageMetrics> Folders { get; init; }

        public required IReadOnlyList<PackageMetrics> Namespaces { get; init; }

        public required IReadOnlyList<PackageCycle> FolderCycles { get; init; }

        public required IReadOnlyList<PackageCycle> NamespaceCycles { get; init; }

        public IEnumerable<TypeMetrics> GetGods(StructureOptions options)
        {
            return Types.Where(type => type.IsGod(options))
                .OrderByDescending(type => type.Facts!.Wmc)
                .ThenBy(type => type.Node.Name, StringComparer.Ordinal);
        }

        // Свалка: большой пакет без деления на системы - много сообществ или большая доля изолированных типов
        public static bool IsDump(PackageMetrics package, StructureOptions options)
        {
            return package.Types.Count >= options.DumpSize
                && (package.Communities >= options.DumpCommunities || package.IsolatedShare >= 0.5);
        }
    }

    // Структурные метрики по графу типов: типы, владение, папки и пространства имен, циклы
    public static class StructureAnalysis
    {
        public static StructureResult Run(
            TypeGraph graph,
            IReadOnlyList<Compilation> compilations,
            IReadOnlyCollection<SyntaxTree> trees,
            StructureOptions options,
            CancellationToken cancellation)
        {
            var facts = TypeFacts.Build(graph, compilations, trees, cancellation);
            var types = graph.Nodes
                .Select(node => new TypeMetrics
                {
                    Node = node,
                    InheritanceDepth = GetInheritanceDepth(node),
                    Children = node.GetIn(EdgeKind.Inherits, EdgeKind.Implements).Select(edge => edge.From).Distinct().Count(),
                    Coupling = node.Out.Select(edge => edge.To).Concat(node.In.Select(edge => edge.From))
                        .Where(other => other != node)
                        .Distinct()
                        .Count(),
                    Dependents = node.In.Select(edge => edge.From).Where(other => other != node).Distinct().Count(),
                    Facts = facts.TryGetValue(node, out TypeFacts? fact) ? fact : null,
                })
                .ToList();

            return new StructureResult
            {
                Graph = graph,
                Types = types,
                Ownership = OwnershipDepth.Find(graph, 2, options.InfrastructureHosts, options.RegistryVariants),
                Folders = PackageMetrics.Build(graph, GetFolder),
                Namespaces = PackageMetrics.Build(graph, node => node.Namespace),
                FolderCycles = PackageCycle.Find(graph, GetFolder),
                NamespaceCycles = PackageCycle.Find(graph, node => node.Namespace),
            };
        }

        public static string GetFolder(TypeNode node)
        {
            int slash = node.File.LastIndexOf('/');
            return slash < 0 ? "." : node.File.Substring(0, slash);
        }

        private static int GetInheritanceDepth(TypeNode node)
        {
            int depth = 0;
            var seen = new HashSet<TypeNode> { node };
            var current = node.GetOut(EdgeKind.Inherits).Select(edge => edge.To).FirstOrDefault();
            while (current != null && seen.Add(current))
            {
                depth++;
                current = current.GetOut(EdgeKind.Inherits).Select(edge => edge.To).FirstOrDefault();
            }

            return depth;
        }
    }
}

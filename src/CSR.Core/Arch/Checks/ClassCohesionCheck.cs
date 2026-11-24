using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Arch.Checks
{
    // Класс-комбайн: крупный класс (partial - все файлы), методы и свойства которого распадаются на части,
    // не связанные вызовами и общими полями (связные компоненты графа "член - член, который он использует";
    // мера LCOM4). Конструкторы и методы из ignoreNames (инициализация, очистка: трогают все поля) в связях
    // не участвуют; свойства связывают, но в размер части не входят (класс данных - группы свойств вокруг полей).
    // Находка - от двух частей по minPartSize методов; у части - поля, регионы и внешние потребители
    public class ClassCohesionCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("minMethods", 12, "методов в классе, от которых он проверяется"),
            CheckParameter.Int("minPartSize", 3, "методов в части, чтобы она считалась"),
            CheckParameter.Strings(
                "ignoreNames",
                "маски методов, которые не связывают части (инициализация, очистка)",
                "Awake",
                "Start",
                "OnEnable",
                "OnDisable",
                "OnDestroy",
                "OnValidate",
                "Reset",
                "Dispose",
                "Clear",
                "Init",
                "Initialize",
                "ToString"),
        ];

        public override string Name
            => "class-cohesion";

        public override string Description
            => "класс из несвязанных частей";

        public override string Question
            => "Части - разные задачи? Если да - вынести часть в свой класс; если нет - оставить";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var ignoredNames = NameMask.Get(context.Settings.GetStrings("ignoreNames"));
            var regions = new Dictionary<SyntaxTree, List<Region>>();
            foreach (var declared in context.Index.Types)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                var type = declared.Symbol;
                if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                    continue;

                var graph = MemberGraph.Build(context, type, ignoredNames);
                if (graph.MethodCount < context.Settings.GetInt("minMethods"))
                    continue;

                var parts = graph.GetParts(context.Settings.GetInt("minPartSize"));
                if (parts.Count < 2)
                    continue;

                bool isSplit = type.DeclaringSyntaxReferences.Length > 1;
                var descriptions = parts.Select((part, index) => $"{index + 1}) "
                    + DescribePart(context, part, graph, regions, isSplit));
                var location = type.Locations.First(candidate => candidate.IsInSource);
                var message = $"'{type.Name}': несвязанных частей {parts.Count} (методов {graph.MethodCount}): "
                    + string.Join("; ", descriptions);
                context.Report(location, message);
            }
        }

        private static string DescribePart(
            SolutionCheckContext context,
            IReadOnlyList<ISymbol> part,
            MemberGraph graph,
            Dictionary<SyntaxTree, List<Region>> regions,
            bool isSplit)
        {
            var operations = part.Where(graph.IsOperation).Select(member => member.Name).Distinct().ToList();
            var fields = part.Where(member => !graph.IsOperation(member)).Select(member => member.Name).ToList();
            var text = SyntaxParts.JoinNames(operations, 4);
            if (fields.Count > 0)
                text += $" | поля {SyntaxParts.JoinNames(fields, 3)}";

            var syntax = part.Where(graph.IsOperation)
                .SelectMany(member => member.DeclaringSyntaxReferences)
                .Select(reference => reference.GetSyntax(context.Cancellation))
                .ToList();
            var regionNames = syntax.Select(node => GetRegion(node, regions))
                .Where(name => name != null)
                .Select(name => name!)
                .Distinct()
                .ToList();
            if (regionNames.Count > 0)
                text += $" | регион {SyntaxParts.JoinNames(regionNames, 2)}";

            if (isSplit)
            {
                var files = syntax.Select(node => Path.GetFileName(node.SyntaxTree.FilePath)).Distinct().ToList();
                text += $" | {SyntaxParts.JoinNames(files, 2)}";
            }

            var consumers = graph.GetConsumers(part);
            if (consumers.Count > 0)
                text += $" | потребители {SyntaxParts.JoinNames(consumers, 3)}";

            return text;
        }

        // Ближайший #region, внутри которого начинается член; null - вне регионов
        private static string? GetRegion(SyntaxNode node, Dictionary<SyntaxTree, List<Region>> cache)
        {
            if (!cache.TryGetValue(node.SyntaxTree, out List<Region>? regions))
            {
                regions = CollectRegions(node.SyntaxTree.GetRoot());
                cache[node.SyntaxTree] = regions;
            }

            var start = node.SpanStart;
            return regions.Where(region => region.Span.Contains(start))
                .OrderBy(region => region.Span.Length)
                .Select(region => region.Name)
                .FirstOrDefault();
        }

        private static List<Region> CollectRegions(SyntaxNode root)
        {
            var regions = new List<Region>();
            var open = new Stack<RegionDirectiveTriviaSyntax>();
            foreach (var trivia in root.DescendantTrivia())
            {
                switch (trivia.GetStructure())
                {
                    case RegionDirectiveTriviaSyntax region:
                        open.Push(region);
                        break;
                    case EndRegionDirectiveTriviaSyntax end when open.Count > 0:
                        var start = open.Pop();
                        var name = start.EndOfDirectiveToken.LeadingTrivia.ToString().Trim();
                        regions.Add(new Region(TextSpan.FromBounds(start.SpanStart, end.Span.End), name));
                        break;
                }
            }

            return regions;
        }

        // Члены класса и связи между ними: операции (методы, свойства с телом) и состояние (поля, автосвойства,
        // события); связь - ссылка из тела одного члена на другой
        private class MemberGraph
        {
            private readonly Dictionary<string, int> m_Indices = new();
            private readonly List<ISymbol> m_Members = new();
            private readonly List<bool> m_IsOperation = new();
            private readonly List<HashSet<string>> m_Consumers = new();
            private UnionFind m_Parts = new(0);

            public int MethodCount { get; private set; }

            public static MemberGraph Build(SolutionCheckContext context, INamedTypeSymbol type, NameMask ignoredNames)
            {
                var graph = new MemberGraph();
                foreach (var member in type.GetMembers())
                {
                    var kind = GetKind(member, ignoredNames, context.Cancellation);
                    if (kind != MemberKind.None)
                        graph.Add(member, kind == MemberKind.Operation);
                }

                graph.m_Parts = new UnionFind(graph.m_Members.Count);
                if (graph.MethodCount == 0)
                    return graph;

                var typeKey = SolutionIndex.GetKey(type);
                for (var index = 0; index < graph.m_Members.Count; index++)
                {
                    foreach (var reference in context.Index.GetReferences(graph.m_Members[index]))
                        graph.Link(index, reference, typeKey, context.Cancellation);
                }

                return graph;
            }

            public bool IsOperation(ISymbol member)
                => m_IsOperation[m_Indices[SolutionIndex.GetKey(member)]];

            // Связные части, в которых не меньше minSize методов; члены - в порядке объявления
            public List<List<ISymbol>> GetParts(int minSize)
            {
                return Enumerable.Range(0, m_Members.Count)
                    .GroupBy(m_Parts.Find)
                    .Where(group => group.Count(index => m_Members[index] is IMethodSymbol) >= minSize)
                    .Select(group => group.Select(index => m_Members[index]).ToList())
                    .ToList();
            }

            // Внешние типы, которые используют члены части
            public List<string> GetConsumers(IReadOnlyList<ISymbol> part)
            {
                return part.SelectMany(member => m_Consumers[m_Indices[SolutionIndex.GetKey(member)]])
                    .Distinct()
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();
            }

            private void Add(ISymbol member, bool isOperation)
            {
                var key = SolutionIndex.GetKey(member);
                if (m_Indices.ContainsKey(key))
                    return;

                m_Indices[key] = m_Members.Count;
                m_Members.Add(member);
                m_IsOperation.Add(isOperation);
                m_Consumers.Add(new HashSet<string>());
                if (member is IMethodSymbol)
                    MethodCount++;
            }

            private void Link(int target, SymbolReference reference, string typeKey, CancellationToken cancellation)
            {
                var owner = GetOwner(reference, cancellation);
                if (owner == null)
                    return;

                var ownerType = owner.ContainingType;
                if (ownerType == null || SolutionIndex.GetKey(ownerType) != typeKey)
                {
                    var consumer = ownerType ?? owner as INamedTypeSymbol;
                    if (consumer != null)
                        m_Consumers[target].Add(consumer.Name);
                    return;
                }

                if (m_Indices.TryGetValue(SolutionIndex.GetKey(owner), out int source) && source != target)
                    m_Parts.Union(target, source);
            }

            // Член, в теле или инициализаторе которого стоит ссылка; тип - если ссылка вне членов (база, атрибут)
            private static ISymbol? GetOwner(SymbolReference reference, CancellationToken cancellation)
            {
                SyntaxNode? previous = null;
                foreach (var ancestor in reference.Node.Ancestors())
                {
                    switch (ancestor)
                    {
                        case BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax:
                            return reference.Model.GetDeclaredSymbol(ancestor, cancellation);
                        case BaseFieldDeclarationSyntax when previous is VariableDeclarationSyntax declaration:
                            var variable = declaration.Variables.FirstOrDefault(candidate =>
                                candidate.Span.Contains(reference.Node.Span));
                            return variable == null ? null : reference.Model.GetDeclaredSymbol(variable, cancellation);
                        case BaseTypeDeclarationSyntax:
                            return reference.Model.GetDeclaredSymbol(ancestor, cancellation);
                    }

                    previous = ancestor;
                }

                return null;
            }

            private static MemberKind GetKind(ISymbol member, NameMask ignoredNames, CancellationToken cancellation)
            {
                if (member.IsImplicitlyDeclared || member.IsAbstract
                    || !member.Locations.Any(location => location.IsInSource))
                    return MemberKind.None;

                switch (member)
                {
                    case IMethodSymbol method:
                        return method.MethodKind == MethodKind.Ordinary && !ignoredNames.IsMatch(method.Name)
                            ? MemberKind.Operation
                            : MemberKind.None;
                    case IPropertySymbol property:
                        return IsAutoProperty(property, cancellation) ? MemberKind.State : MemberKind.Operation;
                    case IFieldSymbol field:
                        return field.IsConst || field.AssociatedSymbol != null ? MemberKind.None : MemberKind.State;
                    case IEventSymbol:
                        return MemberKind.State;
                    default:
                        return MemberKind.None;
                }
            }

            private static bool IsAutoProperty(IPropertySymbol property, CancellationToken cancellation)
            {
                return property.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax(cancellation))
                    .OfType<PropertyDeclarationSyntax>()
                    .Any(declaration => declaration.ExpressionBody == null && declaration.AccessorList != null
                        && declaration.AccessorList.Accessors.All(accessor => accessor.Body == null
                            && accessor.ExpressionBody == null));
            }
        }

        private class Region
        {
            public Region(TextSpan span, string name)
            {
                Span = span;
                Name = name;
            }

            public TextSpan Span { get; }

            public string Name { get; }
        }

        private enum MemberKind
        {
            None,
            Operation,
            State,
        }
    }
}

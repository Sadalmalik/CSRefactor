using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Graph
{
    // Построение графа типов по компиляциям: узлы - типы, объявленные в выбранных деревьях; ребра - по символам
    // (базы, интерфейсы, поля, сигнатуры) и по телам (new, вызов виртуального или интерфейсного члена).
    // Коллекция - массив, тип с IEnumerable<T> или известное имя (List, Dictionary - по значению) при неразрешенной
    // библиотеке
    public static class TypeGraphBuilder
    {
        private static readonly HashSet<string> CollectionNames =
        [
            "IEnumerable",
            "ICollection",
            "IList",
            "IReadOnlyList",
            "IReadOnlyCollection",
            "List",
            "HashSet",
            "ISet",
            "IReadOnlySet",
            "SortedSet",
            "Queue",
            "Stack",
            "LinkedList",
            "Dictionary",
            "IDictionary",
            "IReadOnlyDictionary",
            "SortedDictionary",
            "ConcurrentDictionary",
            "ImmutableArray",
            "ImmutableList",
        ];

        public static TypeGraph Build(
            IReadOnlyList<Compilation> compilations,
            IReadOnlyCollection<SyntaxTree> trees,
            Func<string, string> displayPath,
            CancellationToken cancellation)
        {
            var selected = new HashSet<SyntaxTree>(trees);
            var sources = compilations
                .SelectMany(compilation => compilation.SyntaxTrees.Where(selected.Contains)
                    .Select(tree => new Source(compilation, tree)))
                .ToList();

            var nodes = new ConcurrentDictionary<string, TypeNode>();
            var symbols = new ConcurrentDictionary<string, INamedTypeSymbol>();
            Parallel.ForEach(sources, new ParallelOptions { CancellationToken = cancellation }, source =>
                AddNodes(source, displayPath, nodes, symbols));

            var graph = new TypeGraph(new Dictionary<string, TypeNode>(nodes));
            var edges = new EdgeSet(graph);
            foreach (var pair in symbols)
                AddSymbolEdges(graph, edges, graph.Find(pair.Key)!, pair.Value);

            var bodyEdges = new ConcurrentBag<List<BodyEdge>>();
            Parallel.ForEach(sources, new ParallelOptions { CancellationToken = cancellation }, source =>
                bodyEdges.Add(GetBodyEdges(source, cancellation)));

            foreach (var edge in bodyEdges.SelectMany(list => list))
            {
                var fromNode = graph.Find(edge.From);
                var toNode = graph.Find(edge.To);
                if (fromNode != null && toNode != null)
                    edges.Add(fromNode, toNode, edge.Kind);
            }

            return graph;
        }

        private static void AddNodes(
            Source source,
            Func<string, string> displayPath,
            ConcurrentDictionary<string, TypeNode> nodes,
            ConcurrentDictionary<string, INamedTypeSymbol> symbols)
        {
            var model = source.Compilation.GetSemanticModel(source.Tree);
            var root = source.Tree.GetRoot();
            foreach (var declaration in root.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax
                or DelegateDeclarationSyntax))
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                    continue;

                var key = TypeGraph.GetKey(symbol);
                if (!symbols.TryAdd(key, symbol))
                    continue;

                var span = source.Tree.GetLineSpan(declaration.Span);
                nodes[key] = new TypeNode
                {
                    Key = key,
                    Name = symbol.Name,
                    Namespace = symbol.ContainingNamespace?.IsGlobalNamespace == false
                        ? symbol.ContainingNamespace.ToDisplayString()
                        : "",
                    File = displayPath(source.Tree.FilePath),
                    StartLine = span.StartLinePosition.Line + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    Kind = GetKind(symbol),
                    IsAbstract = symbol.IsAbstract && symbol.TypeKind == TypeKind.Class,
                    IsSealed = symbol.IsSealed && symbol.TypeKind == TypeKind.Class,
                    IsGeneric = symbol.IsGenericType,
                    FieldCount = symbol.GetMembers().Count(IsDataMember),
                };
            }
        }

        // Ребра по символу: базы, интерфейсы, поля и свойства, сигнатуры методов; вложенность - владелец
        private static void AddSymbolEdges(TypeGraph graph, EdgeSet edges, TypeNode node, INamedTypeSymbol symbol)
        {
            if (symbol.ContainingType != null)
                node.Container = graph.Find(symbol.ContainingType);

            if (symbol.BaseType != null && graph.Find(symbol.BaseType) is { } baseNode)
                edges.Add(node, baseNode, EdgeKind.Inherits);

            var interfaceKind = symbol.TypeKind == TypeKind.Interface ? EdgeKind.Inherits : EdgeKind.Implements;
            foreach (var implemented in symbol.Interfaces)
            {
                if (graph.Find(implemented) is { } interfaceNode)
                    edges.Add(node, interfaceNode, interfaceKind);
            }

            foreach (var member in symbol.GetMembers())
            {
                switch (member)
                {
                    case IFieldSymbol { IsImplicitlyDeclared: false, IsConst: false } field:
                        AddTypeEdges(graph, edges, node, field.Type, EdgeKind.Field);
                        break;
                    case IPropertySymbol property:
                        AddTypeEdges(graph, edges, node, property.Type, EdgeKind.Field);
                        break;
                    case IMethodSymbol { IsImplicitlyDeclared: false } method when method.MethodKind is MethodKind.Ordinary
                        or MethodKind.Constructor or MethodKind.DelegateInvoke:
                        if (!method.ReturnsVoid)
                            AddMentioned(graph, edges, node, method.ReturnType, EdgeKind.Returns);

                        foreach (var parameter in method.Parameters)
                            AddMentioned(graph, edges, node, parameter.Type, EdgeKind.Parameter);
                        break;
                }
            }
        }

        // Тип поля: сам тип - Field, элементы коллекции - Collection, другие аргументы типа - Field
        private static void AddTypeEdges(TypeGraph graph, EdgeSet edges, TypeNode from, ITypeSymbol type, EdgeKind kind)
        {
            if (graph.Find(type) is { } direct)
            {
                edges.Add(from, direct, kind);
                return;
            }

            var elements = GetElements(type);
            if (elements != null)
            {
                foreach (var element in elements)
                    AddMentioned(graph, edges, from, element, EdgeKind.Collection);
                return;
            }

            if (type is INamedTypeSymbol named)
            {
                foreach (var argument in named.TypeArguments)
                    AddMentioned(graph, edges, from, argument, kind);
            }
        }

        // Все типы графа в записи типа: сам тип, элементы массивов, аргументы типа
        private static void AddMentioned(TypeGraph graph, EdgeSet edges, TypeNode from, ITypeSymbol type, EdgeKind kind)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    AddMentioned(graph, edges, from, array.ElementType, kind);
                    return;
                case INamedTypeSymbol named:
                    if (graph.Find(named) is { } node)
                        edges.Add(from, node, kind);

                    foreach (var argument in named.TypeArguments)
                        AddMentioned(graph, edges, from, argument, kind);
                    return;
            }
        }

        // Элементы коллекции или null - тип не коллекция
        private static IEnumerable<ITypeSymbol>? GetElements(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
                return [array.ElementType];

            if (type is not INamedTypeSymbol { IsGenericType: true } named)
                return null;

            bool isEnumerable = CollectionNames.Contains(named.Name)
                || named.AllInterfaces.Any(candidate => candidate.Name == "IEnumerable" && candidate.IsGenericType);
            if (!isEnumerable)
                return null;

            // словарь - коллекция значений: ключ - адрес, а не вариант
            return named.Name.Contains("Dictionary") && named.TypeArguments.Length == 2
                ? [named.TypeArguments[1]]
                : named.TypeArguments;
        }

        // Ребра по телам: new T и вызов виртуального, абстрактного или интерфейсного члена T
        private static List<BodyEdge> GetBodyEdges(Source source, CancellationToken cancellation)
        {
            var result = new List<BodyEdge>();
            var model = source.Compilation.GetSemanticModel(source.Tree);
            foreach (var node in source.Tree.GetRoot(cancellation).DescendantNodes())
            {
                if (node is not (BaseObjectCreationExpressionSyntax or InvocationExpressionSyntax))
                    continue;

                var owner = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                if (owner == null || model.GetDeclaredSymbol(owner, cancellation) is not INamedTypeSymbol ownerSymbol)
                    continue;

                var from = TypeGraph.GetKey(ownerSymbol);
                if (node is BaseObjectCreationExpressionSyntax creation)
                {
                    if (model.GetTypeInfo(creation, cancellation).Type is INamedTypeSymbol created)
                        result.Add(new BodyEdge(from, created, EdgeKind.Creates));
                    continue;
                }

                var info = model.GetSymbolInfo(node, cancellation);
                var method = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (method != null && IsVirtualCall(method))
                    result.Add(new BodyEdge(from, method.ContainingType, EdgeKind.CallsVirtual));
            }

            return result;
        }

        private static bool IsVirtualCall(IMethodSymbol method)
            => method.IsVirtual || method.IsAbstract || method.IsOverride || method.ContainingType.TypeKind == TypeKind.Interface;

        private static bool IsDataMember(ISymbol member)
        {
            return member switch
            {
                IFieldSymbol field => !field.IsImplicitlyDeclared && !field.IsConst && !field.IsStatic,
                IPropertySymbol property => !property.IsStatic,
                _ => false,
            };
        }

        private static TypeNodeKind GetKind(INamedTypeSymbol symbol)
        {
            if (symbol.IsRecord)
                return TypeNodeKind.Record;

            return symbol.TypeKind switch
            {
                TypeKind.Struct => TypeNodeKind.Struct,
                TypeKind.Interface => TypeNodeKind.Interface,
                TypeKind.Enum => TypeNodeKind.Enum,
                TypeKind.Delegate => TypeNodeKind.Delegate,
                _ => TypeNodeKind.Class,
            };
        }

        private class Source
        {
            public Source(Compilation compilation, SyntaxTree tree)
            {
                Compilation = compilation;
                Tree = tree;
            }

            public Compilation Compilation { get; }

            public SyntaxTree Tree { get; }
        }

        // Ребро из тела: ключ типа-владельца, тип-цель, вид
        private class BodyEdge
        {
            public BodyEdge(string from, ITypeSymbol to, EdgeKind kind)
            {
                From = from;
                To = to;
                Kind = kind;
            }

            public string From { get; }

            public ITypeSymbol To { get; }

            public EdgeKind Kind { get; }
        }

        // Ребра без повторов: повтор увеличивает Count
        private class EdgeSet
        {
            private readonly Dictionary<string, TypeEdge> m_Edges = new();

            public EdgeSet(TypeGraph graph)
            {
                foreach (var edge in graph.Nodes.SelectMany(node => node.Out))
                    m_Edges[GetKey(edge.From, edge.To, edge.Kind)] = edge;
            }

            public void Add(TypeNode from, TypeNode to, EdgeKind kind)
            {
                var key = GetKey(from, to, kind);
                if (m_Edges.TryGetValue(key, out TypeEdge? existing))
                {
                    existing.Count++;
                    return;
                }

                var edge = new TypeEdge { From = from, To = to, Kind = kind };
                m_Edges[key] = edge;
                from.AddOut(edge);
                to.AddIn(edge);
            }

            private static string GetKey(TypeNode from, TypeNode to, EdgeKind kind)
                => from.Key + "\n" + to.Key + "\n" + (int)kind;
        }
    }
}

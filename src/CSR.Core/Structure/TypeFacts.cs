using System.Collections.Concurrent;
using CSR.Core.Graph;
using CSR.Core.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Structure
{
    // Метрики типа по телам (Lanza - Marinescu):
    // - WMC - сумма цикломатической сложности функций типа (методы, конструкторы, аксессоры, локальные функции);
    // - ATFD - число разных полей и свойств чужих типов исходного кода, к которым тип обращается (свои и баз - не
    //   чужие);
    // - TCC - доля пар методов экземпляра, которые обращаются хотя бы к одному общему полю или свойству типа;
    //   меньше двух методов - 1
    public class TypeFacts
    {
        public required int Wmc { get; init; }

        public required int Atfd { get; init; }

        public required double Tcc { get; init; }

        public required int MethodCount { get; init; }

        public static Dictionary<TypeNode, TypeFacts> Build(
            TypeGraph graph,
            IReadOnlyList<Compilation> compilations,
            IReadOnlyCollection<SyntaxTree> trees,
            CancellationToken cancellation)
        {
            var selected = new HashSet<SyntaxTree>(trees);
            var sources = compilations
                .SelectMany(compilation => compilation.SyntaxTrees.Where(selected.Contains)
                    .Select(tree => compilation.GetSemanticModel(tree)))
                .ToList();
            var parts = new ConcurrentBag<Part>();
            Parallel.ForEach(sources, new ParallelOptions { CancellationToken = cancellation }, model =>
            {
                foreach (var declaration in model.SyntaxTree.GetRoot(cancellation).DescendantNodes()
                    .OfType<TypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration, cancellation) is INamedTypeSymbol symbol)
                        parts.Add(Measure(model, declaration, symbol, cancellation));
                }
            });

            var facts = new Dictionary<TypeNode, TypeFacts>();
            foreach (var group in parts.GroupBy(part => part.Key))
            {
                var node = graph.Find(group.Key);
                if (node == null)
                    continue;

                var methods = group.SelectMany(part => part.MethodFields).ToList();
                facts[node] = new TypeFacts
                {
                    Wmc = group.Sum(part => part.Wmc),
                    Atfd = group.SelectMany(part => part.Foreign).Distinct().Count(),
                    Tcc = GetTcc(methods),
                    MethodCount = methods.Count,
                };
            }

            return facts;
        }

        private static Part Measure(
            SemanticModel model,
            TypeDeclarationSyntax declaration,
            INamedTypeSymbol symbol,
            CancellationToken cancellation)
        {
            var part = new Part(TypeGraph.GetKey(symbol));
            var own = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            for (var type = symbol; type != null; type = type.BaseType)
                own.Add(type.OriginalDefinition);

            foreach (var function in GetFunctions(declaration))
            {
                part.Wmc += CyclomaticComplexity.Calculate(function);
                var fields = new HashSet<string>();
                foreach (var name in FunctionTools.OwnNodes(function).OfType<SimpleNameSyntax>())
                {
                    var target = model.GetSymbolInfo(name, cancellation).Symbol;
                    if (target is not (IFieldSymbol { IsConst: false } or IPropertySymbol) || target.IsStatic)
                        continue;

                    var container = target.ContainingType.OriginalDefinition;
                    if (own.Contains(container))
                        fields.Add(target.Name);
                    else if (container.Locations.Any(location => location.IsInSource))
                        part.Foreign.Add(TypeGraph.GetKey(container) + "." + target.Name);
                }

                if (function is MethodDeclarationSyntax method && !method.Modifiers.Any(modifier => modifier.Text == "static"))
                    part.MethodFields.Add(fields);
            }

            return part;
        }

        // Функции самого типа без вложенных типов
        private static IEnumerable<SyntaxNode> GetFunctions(TypeDeclarationSyntax declaration)
        {
            return declaration.DescendantNodes(node => node == declaration || node is not BaseTypeDeclarationSyntax)
                .Where(FunctionTools.IsFunction);
        }

        // Доля пар методов с общим полем; меньше двух методов - 1
        private static double GetTcc(List<HashSet<string>> methods)
        {
            if (methods.Count < 2)
                return 1;

            int connected = 0;
            for (int i = 0; i < methods.Count; i++)
            {
                for (int j = i + 1; j < methods.Count; j++)
                {
                    if (methods[i].Overlaps(methods[j]))
                        connected++;
                }
            }

            return connected / (methods.Count * (methods.Count - 1) / 2.0);
        }

        // Часть типа из одного объявления (partial - несколько частей)
        private class Part
        {
            public Part(string key)
            {
                Key = key;
            }

            public string Key { get; }

            public int Wmc { get; set; }

            public HashSet<string> Foreign { get; } = new();

            public List<HashSet<string>> MethodFields { get; } = new();
        }
    }
}

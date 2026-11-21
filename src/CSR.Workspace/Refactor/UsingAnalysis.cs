using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Workspace.Refactor
{
    // Что из директив using нужно коду узлов: пространства имен типов и методов расширения (в том числе неявных:
    // foreach, await, запросы LINQ, инициализаторы коллекций), типы using static, имена псевдонимов
    public class UsingUsage
    {
        public HashSet<string> Namespaces { get; } = new(StringComparer.Ordinal);

        public HashSet<string> StaticTypes { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);
    }

    public static class UsingAnalysis
    {
        public static UsingUsage Collect(SemanticModel model, IEnumerable<SyntaxNode> nodes)
        {
            var usage = new UsingUsage();
            foreach (var node in nodes.SelectMany(root => root.DescendantNodesAndSelf(descendIntoTrivia: true)))
            {
                switch (node)
                {
                    case SimpleNameSyntax name:
                        usage.Names.Add(name.Identifier.ValueText);
                        AddName(usage, model.GetSymbolInfo(name), name);
                        break;
                    case QueryClauseSyntax clause:
                        var info = model.GetQueryClauseInfo(clause);
                        AddMethod(usage, info.OperationInfo.Symbol);
                        AddMethod(usage, info.CastInfo.Symbol);
                        break;
                    case SelectOrGroupClauseSyntax or OrderingSyntax:
                        AddMethod(usage, model.GetSymbolInfo(node).Symbol);
                        break;
                    case ForEachStatementSyntax loop:
                        AddMethod(usage, model.GetForEachStatementInfo(loop).GetEnumeratorMethod);
                        break;
                    case AwaitExpressionSyntax awaited:
                        AddMethod(usage, model.GetAwaitExpressionInfo(awaited).GetAwaiterMethod);
                        break;
                    case InitializerExpressionSyntax initializer
                        when initializer.IsKind(SyntaxKind.CollectionInitializerExpression):
                        foreach (var expression in initializer.Expressions)
                            AddMethod(usage, model.GetCollectionInitializerSymbolInfo(expression).Symbol);
                        break;
                }
            }

            return usage;
        }

        // Директива нужна коду; глобальная и неразрешенная - нужны всегда
        public static bool IsNeeded(SemanticModel model, UsingDirectiveSyntax directive, UsingUsage usage)
        {
            if (directive.GlobalKeyword != default)
                return true;

            if (directive.Alias != null)
                return usage.Names.Contains(directive.Alias.Name.Identifier.ValueText);

            var symbol = model.GetSymbolInfo(directive.NamespaceOrType).Symbol;
            if (directive.StaticKeyword != default)
                return symbol is not INamedTypeSymbol type || usage.StaticTypes.Contains(GetKey(type));

            return symbol is not INamespaceSymbol space || usage.Namespaces.Contains(GetKey(space));
        }

        private static void AddName(UsingUsage usage, SymbolInfo info, SimpleNameSyntax name)
        {
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor)
                symbol = constructor.ContainingType;

            if (symbol is IMethodSymbol { IsExtensionMethod: true } or IMethodSymbol { ReducedFrom: not null })
            {
                AddMethod(usage, symbol);
                return;
            }

            if (symbol == null || IsQualified(name))
                return;

            if (symbol is INamedTypeSymbol type)
            {
                usage.Namespaces.Add(GetKey(type.ContainingNamespace));
                if (type.ContainingType != null)
                    usage.StaticTypes.Add(GetKey(type.ContainingType));

                return;
            }

            if (symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol && symbol.IsStatic)
                usage.StaticTypes.Add(GetKey(symbol.ContainingType));
        }

        // Метод расширения: пространство имен его статического класса (и сам класс - для using static)
        private static void AddMethod(UsingUsage usage, ISymbol? symbol)
        {
            if (symbol is not IMethodSymbol method || !method.IsExtensionMethod && method.ReducedFrom == null)
                return;

            var type = (method.ReducedFrom ?? method).ContainingType;
            usage.Namespaces.Add(GetKey(type.ContainingNamespace));
            usage.StaticTypes.Add(GetKey(type));
        }

        // Правая часть квалифицированного имени или доступа к члену: using для нее не нужен
        private static bool IsQualified(SimpleNameSyntax name)
        {
            return name.Parent switch
            {
                QualifiedNameSyntax qualified => qualified.Right == name,
                MemberAccessExpressionSyntax access => access.Name == name,
                AliasQualifiedNameSyntax alias => alias.Name == name,
                MemberBindingExpressionSyntax => true,
                _ => false,
            };
        }

        // Ключ по полному имени: пространство имен из метаданных и из директивы - разные объекты символов
        private static string GetKey(ISymbol symbol)
            => symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}

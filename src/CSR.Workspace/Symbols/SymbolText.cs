using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Symbols
{
    // Имена символов в запросах и отчетах
    public static class SymbolText
    {
        // Namespace.Type.Member без параметров и аргументов типа: так символ задается в командной строке
        private static readonly SymbolDisplayFormat QualifiedFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.None,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

        // Для отчета: с параметрами методов и аргументами типа
        private static readonly SymbolDisplayFormat SignatureFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType
                | SymbolDisplayParameterOptions.IncludeParamsRefOut,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private static readonly SymbolDisplayFormat ParameterTypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

        public static string Qualified(ISymbol symbol)
            => symbol.ToDisplayString(QualifiedFormat);

        public static string Signature(ISymbol symbol)
            => symbol.ToDisplayString(SignatureFormat);

        // Как символ задается в запросе: Namespace.Type.Method(int, Item) - для списка перегрузок
        public static string Query(ISymbol symbol)
        {
            if (symbol is not IMethodSymbol method)
                return Qualified(symbol);

            return $"{Qualified(method)}({string.Join(", ", method.Parameters.Select(ParameterType))})";
        }

        public static string ParameterType(IParameterSymbol parameter)
            => parameter.Type.ToDisplayString(ParameterTypeFormat);

        public static string Kind(ISymbol symbol)
        {
            return symbol switch
            {
                INamedTypeSymbol type => type.TypeKind.ToString().ToLowerInvariant(),
                IMethodSymbol { MethodKind: MethodKind.Constructor } => "constructor",
                IMethodSymbol => "method",
                IPropertySymbol { IsIndexer: true } => "indexer",
                _ => symbol.Kind.ToString().ToLowerInvariant(),
            };
        }
    }
}

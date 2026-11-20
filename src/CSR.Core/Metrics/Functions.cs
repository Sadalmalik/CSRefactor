using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Metrics
{
    // Функция для метрик: метод, конструктор, деструктор, оператор, аксессор, локальная функция,
    // свойство и индексатор с телом-выражением (int X => ...) - как аксессор get
    // Лямбды и анонимные методы входят в объемлющую функцию, локальные функции считаются отдельно
    public static class Functions
    {
        public static bool IsFunction(SyntaxNode node)
        {
            return node is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or PropertyDeclarationSyntax { ExpressionBody: not null }
                or IndexerDeclarationSyntax { ExpressionBody: not null };
        }

        public static IEnumerable<SyntaxNode> OwnNodes(SyntaxNode function)
            => function.DescendantNodes(node => node == function || !IsFunction(node));

        public static IEnumerable<SyntaxToken> OwnTokens(SyntaxNode function)
            => function.DescendantTokens(node => node == function || !IsFunction(node));

        public static string GetKind(SyntaxNode function)
        {
            return function switch
            {
                MethodDeclarationSyntax => "method",
                ConstructorDeclarationSyntax => "constructor",
                DestructorDeclarationSyntax => "destructor",
                OperatorDeclarationSyntax => "operator",
                ConversionOperatorDeclarationSyntax => "operator",
                AccessorDeclarationSyntax => "accessor",
                PropertyDeclarationSyntax or IndexerDeclarationSyntax => "accessor",
                LocalFunctionStatementSyntax => "local-function",
                _ => "unknown",
            };
        }

        public static string GetName(SyntaxNode function)
        {
            return function switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
                DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.Text,
                OperatorDeclarationSyntax op => "operator " + op.OperatorToken.Text,
                ConversionOperatorDeclarationSyntax conversion
                    => $"{conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}",
                AccessorDeclarationSyntax accessor => GetOwnerName(accessor) + "." + accessor.Keyword.Text,
                PropertyDeclarationSyntax property => property.Identifier.Text + ".get",
                IndexerDeclarationSyntax => "this[].get",
                LocalFunctionStatementSyntax local => local.Identifier.Text,
                _ => "?",
            };
        }

        // У аксессора индексатора - параметры индексатора, у аксессора свойства и события - нет
        public static BaseParameterListSyntax? GetParameters(SyntaxNode function)
        {
            return function switch
            {
                BaseMethodDeclarationSyntax method => method.ParameterList,
                LocalFunctionStatementSyntax local => local.ParameterList,
                IndexerDeclarationSyntax indexer => indexer.ParameterList,
                AccessorDeclarationSyntax { Parent.Parent: IndexerDeclarationSyntax indexer } => indexer.ParameterList,
                _ => null,
            };
        }

        public static BlockSyntax? GetBody(SyntaxNode function)
            => function.ChildNodes().OfType<BlockSyntax>().FirstOrDefault();

        private static string GetOwnerName(AccessorDeclarationSyntax accessor)
        {
            return accessor.Parent?.Parent switch
            {
                PropertyDeclarationSyntax property => property.Identifier.Text,
                IndexerDeclarationSyntax => "this[]",
                EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.Text,
                _ => "?",
            };
        }
    }
}

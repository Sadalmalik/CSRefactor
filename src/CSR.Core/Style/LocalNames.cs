using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style
{
    // Локальные имена по синтаксису: параметры, переменные, foreach, out var и шаблоны, catch
    public static class LocalNames
    {
        public static HashSet<string> GetDeclared(SyntaxNode node)
        {
            var names = new HashSet<string>();
            foreach (var item in node.DescendantNodesAndSelf())
            {
                var identifier = item switch
                {
                    ParameterSyntax parameter => parameter.Identifier,
                    VariableDeclaratorSyntax variable => variable.Identifier,
                    ForEachStatementSyntax loop => loop.Identifier,
                    SingleVariableDesignationSyntax designation => designation.Identifier,
                    CatchDeclarationSyntax declaration => declaration.Identifier,
                    _ => default,
                };
                if (identifier.Text.Length > 0)
                    names.Add(identifier.Text);
            }

            return names;
        }

        // Типы всех объявлений имени в узле; null - тип не записан (var, параметр лямбды, переменная запроса)
        public static List<TypeSyntax?> FindDeclarationTypes(SyntaxNode node, string name)
        {
            var types = new List<TypeSyntax?>();
            foreach (var item in node.DescendantNodesAndSelf())
            {
                var (identifier, type) = item switch
                {
                    ParameterSyntax parameter => (parameter.Identifier, parameter.Type),
                    VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } variable
                        => (variable.Identifier, declaration.Type),
                    ForEachStatementSyntax loop => (loop.Identifier, loop.Type),
                    SingleVariableDesignationSyntax designation => (designation.Identifier, GetDesignationType(designation)),
                    CatchDeclarationSyntax declaration => (declaration.Identifier, declaration.Type),
                    QueryClauseSyntax or JoinIntoClauseSyntax or QueryContinuationSyntax => (GetQueryIdentifier(item), null),
                    _ => (default(SyntaxToken), null),
                };
                if (identifier.Text != name)
                    continue;

                types.Add(type is { IsVar: true } ? null : type);
            }

            return types;
        }

        private static TypeSyntax? GetDesignationType(SingleVariableDesignationSyntax designation)
        {
            return designation.Parent switch
            {
                DeclarationExpressionSyntax declaration => declaration.Type,
                DeclarationPatternSyntax pattern => pattern.Type,
                _ => null,
            };
        }

        private static SyntaxToken GetQueryIdentifier(SyntaxNode clause)
        {
            return clause switch
            {
                FromClauseSyntax from => from.Identifier,
                LetClauseSyntax let => let.Identifier,
                JoinClauseSyntax join => join.Identifier,
                JoinIntoClauseSyntax into => into.Identifier,
                QueryContinuationSyntax continuation => continuation.Identifier,
                _ => default,
            };
        }

        // Имена const-локальных: значение подставляется, переменная не захватывается
        public static HashSet<string> GetConstants(SyntaxNode node)
        {
            var constants = node.DescendantNodesAndSelf()
                .OfType<LocalDeclarationStatementSyntax>()
                .Where(local => local.IsConst)
                .SelectMany(local => local.Declaration.Variables)
                .Select(variable => variable.Identifier.Text);
            return new HashSet<string>(constants);
        }

        // Имена переменных в выражении: без правой части x.Name и x?.Name, имени аргумента name: и nameof(...)
        public static IEnumerable<string> GetUsed(SyntaxNode node)
        {
            return node.DescendantNodesAndSelf(child => !IsNameOf(child))
                .OfType<IdentifierNameSyntax>()
                .Where(name => !IsMemberName(name))
                .Select(name => name.Identifier.Text);
        }

        private static bool IsNameOf(SyntaxNode node)
            => node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } };

        private static bool IsMemberName(IdentifierNameSyntax name)
        {
            return name.Parent switch
            {
                MemberAccessExpressionSyntax access => access.Name == name,
                MemberBindingExpressionSyntax => true,
                NameColonSyntax => true,
                _ => false,
            };
        }
    }
}

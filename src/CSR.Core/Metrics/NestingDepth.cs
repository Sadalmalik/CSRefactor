using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Metrics
{
    // Наибольшая вложенность операторов в теле функции: уровни if (else if - тот же уровень), циклов, try, switch,
    // using, lock. Функция с телом-выражением или без тела - 0
    public static class NestingDepth
    {
        public static int Calculate(SyntaxNode function)
        {
            BlockSyntax? body = Functions.GetBody(function);
            if (body == null)
                return 0;

            return body.DescendantNodes(node => node == function || !Functions.IsFunction(node))
                .Where(node => node is StatementSyntax and not BlockSyntax)
                .Select(node => node.Ancestors().TakeWhile(ancestor => ancestor != body).Count(IsNestingStatement))
                .DefaultIfEmpty(0)
                .Max();
        }

        private static bool IsNestingStatement(SyntaxNode node)
        {
            return node is IfStatementSyntax { Parent: not ElseClauseSyntax }
                or WhileStatementSyntax
                or ForStatementSyntax
                or CommonForEachStatementSyntax
                or DoStatementSyntax
                or TryStatementSyntax
                or SwitchStatementSyntax
                or UsingStatementSyntax
                or LockStatementSyntax;
        }
    }
}

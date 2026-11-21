using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // if (x == null) x = value; -> x ??= value;
    // Без семантики: у объектов Unity (сравнение с null перегружено) ??= не годится - уровень находки задает конфиг
    public class CoalesceAssignmentCheck : StyleCheck
    {
        public override string Name
            => "coalesce-assignment";

        public override string Description
            => "присваивание по проверке на null вместо ??=";

        public override void Analyze(CheckContext context)
        {
            foreach (var node in context.Root.DescendantNodes().OfType<IfStatementSyntax>())
            {
                if (node.Else != null || !node.Condition.IsKind(SyntaxKind.EqualsExpression))
                    continue;

                var condition = (BinaryExpressionSyntax)node.Condition;
                var target = condition.Right.IsKind(SyntaxKind.NullLiteralExpression) ? condition.Left : null;
                var assignment = GetSingleAssignment(node.Statement);
                if (target == null || assignment == null || !IsSame(target, assignment.Left))
                    continue;

                var text = $"{assignment.Left} ??= {assignment.Right};";
                context.Report(node.Span, $"-> '{text}'", context.ReplaceInLines(node.Span, text));
            }
        }

        private static AssignmentExpressionSyntax? GetSingleAssignment(StatementSyntax statement)
        {
            if (statement is BlockSyntax { Statements.Count: 1 } block)
                statement = block.Statements[0];

            return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    ? assignment
                    : null;
        }

        private static bool IsSame(SyntaxNode left, SyntaxNode right)
            => left.WithoutTrivia().ToString() == right.WithoutTrivia().ToString();
    }
}

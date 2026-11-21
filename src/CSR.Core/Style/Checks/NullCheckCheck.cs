using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Форма проверки на null: x == null / x != null или x is null / x is not null
    public class NullCheckCheck : StyleCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "equality - == null и != null, pattern - is null и is not null",
                "equality", "pattern"),
        ];

        public override string Name
            => "null-check";

        public override string Description
            => "форма проверки на null";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferEquality = context.Settings.GetString("prefer") == "equality";
            foreach (var node in context.Root.DescendantNodes())
            {
                if (preferEquality && node is IsPatternExpressionSyntax pattern)
                    CheckPattern(context, pattern);
                else if (!preferEquality && node is BinaryExpressionSyntax binary)
                    CheckEquality(context, binary);
            }
        }

        private static void CheckPattern(CheckContext context, IsPatternExpressionSyntax node)
        {
            var op = node.Pattern switch
            {
                ConstantPatternSyntax constant when IsNull(constant.Expression) => "==",
                UnaryPatternSyntax { Pattern: ConstantPatternSyntax constant } when IsNull(constant.Expression) => "!=",
                _ => null,
            };

            if (op == null)
                return;

            var replacement = context.ReplaceInLines(node.Span, $"{node.Expression} {op} null");
            context.Report(node.Span, $"'{node.Pattern}' -> '{op} null'", replacement);
        }

        private static void CheckEquality(CheckContext context, BinaryExpressionSyntax node)
        {
            bool isEquals = node.IsKind(SyntaxKind.EqualsExpression);
            if (!isEquals && !node.IsKind(SyntaxKind.NotEqualsExpression))
                return;

            ExpressionSyntax? operand = IsNull(node.Right) ? node.Left : IsNull(node.Left) ? node.Right : null;
            if (operand == null)
                return;

            var pattern = isEquals ? "is null" : "is not null";
            var replacement = context.ReplaceInLines(node.Span, $"{operand} {pattern}");
            context.Report(node.Span, $"'{node.OperatorToken.Text} null' -> '{pattern}'", replacement);
        }

        private static bool IsNull(ExpressionSyntax expression)
            => expression.IsKind(SyntaxKind.NullLiteralExpression);
    }
}

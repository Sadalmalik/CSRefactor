using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Тип локальной с булевым инициализатором (сравнение, && / ||, !, true / false, is): bool или var
    public class BoolLocalTypeCheck : StyleCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "explicit - bool x = ..., var - var x = ...", "explicit", "var"),
        ];

        public override string Name
            => "bool-local-type";

        public override string Description
            => "тип булевой локальной";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferExplicit = context.Settings.GetString("prefer") == "explicit";
            foreach (var statement in context.Root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                var declaration = statement.Declaration;
                if (declaration.Variables.Count != 1 || declaration.Variables[0].Initializer == null)
                    continue;

                var value = declaration.Variables[0].Initializer!.Value;
                if (!IsBoolean(value))
                    continue;

                var type = declaration.Type;
                var name = declaration.Variables[0].Identifier.Text;
                bool isBool = type is PredefinedTypeSyntax { Keyword.Text: "bool" };
                if (preferExplicit ? !type.IsVar : !isBool)
                    continue;

                var wanted = preferExplicit ? "bool" : "var";
                var replacement = context.ReplaceInLines(type.Span, wanted);
                context.Report(type.Span, $"'{type} {name}' -> '{wanted} {name}'", replacement);
            }
        }

        private static bool IsBoolean(ExpressionSyntax expression)
        {
            return expression switch
            {
                ParenthesizedExpressionSyntax parenthesized => IsBoolean(parenthesized.Expression),
                PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.LogicalNotExpression),
                LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.TrueLiteralExpression)
                    || literal.IsKind(SyntaxKind.FalseLiteralExpression),
                IsPatternExpressionSyntax => true,
                BinaryExpressionSyntax binary => IsBooleanOperator(binary),
                _ => false,
            };
        }

        private static bool IsBooleanOperator(BinaryExpressionSyntax binary)
        {
            return binary.Kind() is SyntaxKind.EqualsExpression
                or SyntaxKind.NotEqualsExpression
                or SyntaxKind.LessThanExpression
                or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression
                or SyntaxKind.GreaterThanOrEqualExpression
                or SyntaxKind.LogicalAndExpression
                or SyntaxKind.LogicalOrExpression
                or SyntaxKind.IsExpression;
        }
    }
}

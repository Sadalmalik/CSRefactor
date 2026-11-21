using System.Globalization;
using CSR.Core.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Поведенческое число литералом в логике: операнд сравнения, арифметики или составного присваивания
    // в теле члена (speed > 0.1f, angle - 100f, timer += 0.5f). Не ищется: нейтральные значения (маски neutral по
    // нормализованному тексту: 0.50f -> 0.5, 1e-5 -> 1e-05), индексы в [], инициализаторы полей и констант,
    // атрибуты, методы из ignoreMethods (отрисовка). Шум высокий - уровень подсказки
    public class NumericLiteralCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("neutral", "маски нейтральных значений: нормализованный текст, у отрицательных '-'",
                "0", "1", "-1", "2", "0.5"),
            CheckParameter.Bool("integers", true, "искать и целые литералы"),
            CheckParameter.Strings("ignoreMethods", "маски имен членов, где литералы не ищутся",
                "OnDrawGizmos*", "Draw*", "OnGUI", "GetHashCode"),
        ];

        private static readonly HashSet<SyntaxKind> BinaryKinds =
        [
            SyntaxKind.LessThanExpression, SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression,
            SyntaxKind.AddExpression, SyntaxKind.SubtractExpression, SyntaxKind.MultiplyExpression,
            SyntaxKind.DivideExpression,
        ];

        private static readonly HashSet<SyntaxKind> AssignmentKinds =
        [
            SyntaxKind.AddAssignmentExpression, SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression, SyntaxKind.DivideAssignmentExpression,
        ];

        public override string Name
            => "numeric-literal";

        public override string Description
            => "поведенческое число литералом";

        public override string Question
            => "Это число подбирает дизайнер? Тогда - поле настроек системы с подсказкой; эпсилон и допуск - оставить";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        protected override void AnalyzeFile(CheckContext context)
        {
            var neutral = NameMask.Get(context.Settings.GetStrings("neutral"));
            var ignoredMethods = NameMask.Get(context.Settings.GetStrings("ignoreMethods"));
            bool isIntegerIncluded = context.Settings.GetBool("integers");
            foreach (var literal in context.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.NumericLiteralExpression))
                    continue;

                if (!isIntegerIncluded && literal.Token.Value is not (float or double or decimal))
                    continue;

                var operand = GetOperand(literal);
                var expression = operand.Parent;
                if (!IsFormula(expression) || IsExcluded(literal, ignoredMethods))
                    continue;

                var value = Normalize(literal, operand != literal);
                if (!neutral.IsMatch(value))
                    context.Report(operand.Span, $"число {operand} в '{ArchSyntax.Shorten(expression!, 60)}'");
            }
        }

        // Литерал с унарным минусом, скобками и приведением: -0.3f, (float)3
        private static ExpressionSyntax GetOperand(LiteralExpressionSyntax literal)
        {
            ExpressionSyntax operand = literal;
            while (IsWrapper(operand.Parent))
                operand = (ExpressionSyntax)operand.Parent!;

            return operand;
        }

        private static bool IsWrapper(SyntaxNode? node)
            => node is PrefixUnaryExpressionSyntax or ParenthesizedExpressionSyntax or CastExpressionSyntax;

        private static bool IsFormula(SyntaxNode? expression)
        {
            return expression switch
            {
                BinaryExpressionSyntax binary => BinaryKinds.Contains(binary.Kind()),
                AssignmentExpressionSyntax assignment => AssignmentKinds.Contains(assignment.Kind()),
                _ => false,
            };
        }

        private static bool IsExcluded(SyntaxNode literal, NameMask ignoredMethods)
        {
            foreach (var ancestor in literal.Ancestors())
            {
                switch (ancestor)
                {
                    case BracketedArgumentListSyntax or AttributeSyntax or ParameterSyntax:
                        return true;
                    case FieldDeclarationSyntax or EnumMemberDeclarationSyntax:
                        return true;
                    case LocalDeclarationStatementSyntax local when local.IsConst:
                        return true;
                    case BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax:
                        var name = ArchSyntax.GetEnclosingMemberName(literal);
                        return name != null && ignoredMethods.IsMatch(name);
                }
            }

            return true;
        }

        // Значение без суффикса и лишних нулей; со знаком минус, если он есть у операнда
        private static string Normalize(LiteralExpressionSyntax literal, bool isWrapped)
        {
            var value = literal.Token.Value;
            var text = value switch
            {
                float single => single.ToString("R", CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                decimal exact => TrimFraction(exact.ToString(CultureInfo.InvariantCulture)),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? literal.Token.Text,
            };

            bool isNegative = isWrapped && literal.Ancestors()
                .TakeWhile(IsWrapper)
                .Count(node => node.IsKind(SyntaxKind.UnaryMinusExpression)) % 2 == 1;
            return (isNegative ? "-" : "") + text.ToLowerInvariant();
        }

        private static string TrimFraction(string text)
            => text.IndexOf('.') >= 0 ? text.TrimEnd('0').TrimEnd('.') : text;
    }
}

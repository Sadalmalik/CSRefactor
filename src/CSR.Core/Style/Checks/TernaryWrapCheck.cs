using CSR.Core.Checks;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Тернарный оператор столбиком: условие в строке, '? a' и ': b' с новой строки +4 к строке тернарника,
    // вложенный тернарник в ветви - каждый уровень еще +4. Находка: тернарник в одну строку длиннее max,
    // вложенный тернарник в одну строку, '?' или ':' в конце строки. Замена - для однострочного тернарника
    // в голове инструкции (значение, return, '=>', аргумент); иначе - без замены
    public class TernaryWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки с тернарником"),
            CheckParameter.Bool("nested", true, "вложенный тернарник в одну строку - столбиком при любой длине"),
        ];

        public override string Name
            => "ternary-wrap";

        public override string Description
            => "тернарный оператор столбиком";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            bool nested = context.Settings.GetBool("nested");
            var text = context.Text;
            foreach (var conditional in context.Root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
            {
                if (conditional.Parent is ConditionalExpressionSyntax || TextLines.HasCommentOrDirective(conditional))
                    continue;

                if (!TextLines.IsSingleLine(text, conditional.Span))
                {
                    CheckTrailing(context, conditional);
                    continue;
                }

                bool isLong = TextLines.GetLineLength(text, conditional.SpanStart) > max;
                bool isNested = nested && HasNestedBranch(conditional);
                if (!isLong && !isNested)
                    continue;

                var message = isLong ? "тернарник длиннее порога - столбиком" : "вложенный тернарник в одну строку";
                if (IsHeadPosition(conditional))
                    ReportColumn(context, conditional, message);
                else
                    context.Report(conditional.Span, message + "; тернарник внутри выражения - вынести в переменную");
            }
        }

        // Многострочный тернарник: '?' и ':' - в начале строки
        private static void CheckTrailing(CheckContext context, ConditionalExpressionSyntax conditional)
        {
            foreach (var token in new[] { conditional.QuestionToken, conditional.ColonToken })
            {
                var nextLine = TextLines.GetLineNumber(context.Text, token.GetNextToken().SpanStart);
                if (nextLine > TextLines.GetLineNumber(context.Text, token.SpanStart))
                    context.Report(token.Span, $"'{token.Text}' в конце строки - в начало строки ветви");
            }
        }

        private static bool HasNestedBranch(ConditionalExpressionSyntax conditional)
            => conditional.WhenTrue is ConditionalExpressionSyntax
                || conditional.WhenFalse is ConditionalExpressionSyntax;

        // Тернарник - значение присваивания или инициализатора, return, тело '=>' или лямбды, аргумент вызова
        private static bool IsHeadPosition(ConditionalExpressionSyntax conditional)
        {
            return conditional.Parent switch
            {
                EqualsValueClauseSyntax or ArrowExpressionClauseSyntax or ReturnStatementSyntax => true,
                ArgumentSyntax or LambdaExpressionSyntax or YieldStatementSyntax => true,
                AssignmentExpressionSyntax assignment => assignment.Right == conditional,
                _ => false,
            };
        }

        private static void ReportColumn(CheckContext context, ConditionalExpressionSyntax conditional, string message)
        {
            var text = context.Text;
            var line = text.Lines.GetLineFromPosition(conditional.SpanStart);
            var indent = TextLayout.GetIndent(text, conditional.SpanStart);
            var lines = new List<string>
            {
                text.ToString(TextSpan.FromBounds(line.Start, conditional.Condition.Span.End)),
            };

            AddBranches(lines, conditional, TextLayout.Deeper(indent));
            lines[lines.Count - 1] += text.ToString(TextSpan.FromBounds(conditional.Span.End, line.End));
            context.Report(line.Span, message, context.ReplaceLines(line.LineNumber, line.LineNumber, lines));
        }

        // '? a' и ': b' с отступом branchIndent; вложенный тернарник в ветви - его ветви еще +4
        private static void AddBranches(
            List<string> lines,
            ConditionalExpressionSyntax conditional,
            string branchIndent)
        {
            AddBranch(lines, "?", conditional.WhenTrue, branchIndent);
            AddBranch(lines, ":", conditional.WhenFalse, branchIndent);
        }

        private static void AddBranch(List<string> lines, string mark, ExpressionSyntax branch, string indent)
        {
            if (branch is not ConditionalExpressionSyntax inner)
            {
                lines.Add($"{indent}{mark} {Normalize(branch)}");
                return;
            }

            lines.Add($"{indent}{mark} {Normalize(inner.Condition)}");
            AddBranches(lines, inner, TextLayout.Deeper(indent));
        }

        private static string Normalize(SyntaxNode node)
            => node.ToString().Trim();
    }
}

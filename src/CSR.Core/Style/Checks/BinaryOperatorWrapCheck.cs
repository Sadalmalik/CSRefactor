using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Место бинарного оператора при переносе выражения: в начале строки продолжения или в конце строки.
    // Замена - перенос оператора между соседними строками; отступ строки продолжения не меняется
    public class BinaryOperatorWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice(
                "prefer",
                "leading - оператор в начале строки, trailing - в конце",
                "leading",
                "trailing"),
            CheckParameter.Strings("operators", "проверяемые операторы", "&&", "||"),
        ];

        public override string Name
            => "binary-op-wrap";

        public override string Description
            => "место оператора при переносе";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferLeading = context.Settings.GetString("prefer") == "leading";
            var operators = context.Settings.GetStrings("operators");
            var lines = context.Text.Lines;
            foreach (var binary in context.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                var token = binary.OperatorToken;
                if (!operators.Contains(token.Text))
                    continue;

                var leftLine = lines.GetLineFromPosition(binary.Left.Span.End).LineNumber;
                var operatorLine = lines.GetLineFromPosition(token.SpanStart).LineNumber;
                var rightLine = lines.GetLineFromPosition(binary.Right.SpanStart).LineNumber;
                if (preferLeading && leftLine == operatorLine && operatorLine < rightLine)
                {
                    var message = $"'{token.Text}' в конце строки - перенести в начало следующей";
                    var replacement = MoveToNextLine(context, token, rightLine);
                    var span = replacement == null ? token.Span : GetLinesSpan(context, operatorLine, rightLine);
                    context.Report(span, message, replacement);
                }
                else if (!preferLeading && leftLine < operatorLine && operatorLine == rightLine)
                {
                    var message = $"'{token.Text}' в начале строки - перенести в конец предыдущей";
                    var replacement = MoveToPreviousLine(context, token, leftLine);
                    var span = replacement == null ? token.Span : GetLinesSpan(context, leftLine, operatorLine);
                    context.Report(span, message, replacement);
                }
            }
        }

        // Оператор в конце строки -> в начало строки правого операнда (она - следующая, комментариев нет)
        private static LineReplacement? MoveToNextLine(CheckContext context, SyntaxToken token, int rightLine)
        {
            var text = context.Text;
            var operatorLine = text.Lines.GetLineFromPosition(token.SpanStart);
            if (rightLine != operatorLine.LineNumber + 1 || HasComment(token))
                return null;

            var before = text.ToString(TextSpan.FromBounds(operatorLine.Start, token.SpanStart)).TrimEnd();
            var next = TextLines.GetLine(text, rightLine);
            var indent = next.Substring(0, next.Length - next.TrimStart().Length);
            return context.ReplaceLines(operatorLine.LineNumber, rightLine, [before, $"{indent}{token.Text} {next.TrimStart()}"]);
        }

        // Оператор в начале строки -> в конец строки левого операнда (она - предыдущая)
        private static LineReplacement? MoveToPreviousLine(CheckContext context, SyntaxToken token, int leftLine)
        {
            var text = context.Text;
            var operatorLine = text.Lines.GetLineFromPosition(token.SpanStart);
            if (leftLine != operatorLine.LineNumber - 1 || HasComment(token))
                return null;

            var previous = TextLines.GetLine(text, leftLine).TrimEnd();
            var rest = text.ToString(TextSpan.FromBounds(token.Span.End, operatorLine.End)).TrimStart();
            var indent = TextLines.GetIndent(text, token.SpanStart);
            return context.ReplaceLines(leftLine, operatorLine.LineNumber, [$"{previous} {token.Text}", indent + rest]);
        }

        private static TextSpan GetLinesSpan(CheckContext context, int first, int last)
            => TextSpan.FromBounds(context.Text.Lines[first].Start, context.Text.Lines[last].End);

        private static bool HasComment(SyntaxToken token)
        {
            var previous = token.GetPreviousToken();
            return previous.TrailingTrivia.Concat(token.LeadingTrivia).Concat(token.TrailingTrivia)
                .Any(trivia => trivia.ToString().TrimStart().StartsWith("/", StringComparison.Ordinal));
        }
    }
}

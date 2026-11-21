using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Место бинарного оператора при переносе выражения: в начале строки продолжения или в конце строки
    public class BinaryOperatorWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "leading - оператор в начале строки, trailing - в конце", "leading",
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
                    context.Report(token.Span, $"'{token.Text}' в конце строки - перенести в начало следующей");
                else if (!preferLeading && leftLine < operatorLine && operatorLine == rightLine)
                    context.Report(token.Span, $"'{token.Text}' в начале строки - перенести в конец предыдущей");
            }
        }
    }
}

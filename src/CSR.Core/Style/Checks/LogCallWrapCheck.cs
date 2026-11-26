using CSR.Core.Checks;
using CSR.Core.Syntax;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Вызов логгера - одной строкой; допустим перенос всех аргументов на следующую строку после '('.
    // Однострочный вызов в строке длиннее maxLength - находка: сначала сократить текст, затем перенести аргумент.
    // После переноса аргументов текст длиннее строки делится на части по строкам (SplitText, R67); склейка,
    // которая помещается в maxLength одной частью, - находка. Методы логгера - в project.jsonc
    public class LogCallWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("maxLength", 0, "наибольшая длина строки с однострочным вызовом лога; 0 - не проверять"),
        ];

        public override string Name
            => "log-call-wrap";

        public override string Description
            => "вызов лога не в одну строку или длиннее порога";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var maxLength = context.Settings.GetInt("maxLength");
            var text = context.Text;
            foreach (var invocation in context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var first = TextLayout.GetLineNumber(text, invocation.SpanStart);
                var last = TextLayout.GetLineNumber(text, invocation.Span.End);
                if (!LoggerCallTool.IsLoggerCall(context, invocation))
                    continue;

                if (first == last)
                {
                    var length = text.Lines[first].Span.Length;
                    var message = $"лог в строке длиннее {maxLength}: {length} - сократить текст";
                    if (maxLength > 0 && length > maxLength)
                        context.Report(invocation.Span, message);

                    continue;
                }

                var arguments = invocation.ArgumentList;
                var openLine = TextLayout.GetLineNumber(text, arguments.OpenParenToken.SpanStart);
                var argumentsLine = arguments.Arguments.Count == 0
                    ? last
                    : TextLayout.GetLineNumber(text, arguments.Arguments[0].SpanStart);

                if (openLine != first || argumentsLine != first + 1)
                {
                    context.Report(invocation.Span, "вызов лога не в одну строку");
                    continue;
                }

                var problem = GetSplitProblem(context.Text, arguments, maxLength);
                if (problem != null)
                    context.Report(invocation.Span, problem);
            }
        }

        // Аргументы после '(' в несколько строк: переносы - только после '+' текста, разделенного по строкам
        private static string? GetSplitProblem(SourceText text, ArgumentListSyntax arguments, int maxLength)
        {
            if (arguments.Arguments.Count == 0)
                return "вызов лога не в одну строку";

            var splits = arguments.Arguments
                .Select(argument => SplitText.GetParts(text, argument.Expression))
                .Where(parts => parts != null)
                .Select(parts => parts!)
                .ToList();
            var allowed = new HashSet<SyntaxToken>(splits
                .SelectMany(parts => parts.Skip(1))
                .Select(part => part.GetFirstToken().GetPreviousToken()));

            var token = arguments.Arguments[0].GetFirstToken();
            while (token != arguments.CloseParenToken)
            {
                var next = token.GetNextToken();
                var line = TextLayout.GetLineNumber(text, token.SpanStart);
                bool isBreak = TextLayout.GetLineNumber(text, next.SpanStart) > line;
                if (isBreak && !allowed.Contains(token))
                    return "вызов лога не в одну строку";

                token = next;
            }

            if (maxLength > 0 && splits.Any(parts => GetJoinedLineLength(text, parts) <= maxLength))
                return "текст лога помещается в строку - одной строкой без склейки";

            return null;
        }

        // Длина строки с текстом одной частью: начало строки первой части, текст, хвост строки последней части
        private static int GetJoinedLineLength(SourceText text, IReadOnlyList<ExpressionSyntax> parts)
        {
            var lastLine = text.Lines.GetLineFromPosition(parts[parts.Count - 1].Span.End);
            var tail = lastLine.End - parts[parts.Count - 1].Span.End;
            return TextLayout.GetColumn(text, parts[0].SpanStart) + SplitText.GetJoinedLength(parts) + tail;
        }
    }
}

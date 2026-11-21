using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Вызов логгера - одной строкой; допустим перенос всех аргументов на следующую строку после '('.
    // Однострочный вызов в строке длиннее maxLength - находка: сначала сократить текст, затем перенести аргумент.
    // Методы логгера - в project.jsonc
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
            var lines = context.Text.Lines;
            foreach (var invocation in context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var first = lines.GetLineFromPosition(invocation.SpanStart).LineNumber;
                var last = lines.GetLineFromPosition(invocation.Span.End).LineNumber;
                if (!Invocations.IsLoggerCall(context, invocation))
                    continue;

                if (first == last)
                {
                    var length = lines[first].Span.Length;
                    var message = $"лог в строке длиннее {maxLength}: {length} - сократить текст";
                    if (maxLength > 0 && length > maxLength)
                        context.Report(invocation.Span, message);

                    continue;
                }

                var arguments = invocation.ArgumentList;
                var openLine = lines.GetLineFromPosition(arguments.OpenParenToken.SpanStart).LineNumber;
                var argumentsLine = arguments.Arguments.Count == 0
                    ? last
                    : lines.GetLineFromPosition(arguments.Arguments[0].SpanStart).LineNumber;

                bool isArgumentsWrapped = last == first + 1 && openLine == first && argumentsLine == last;
                if (!isArgumentsWrapped)
                    context.Report(invocation.Span, "вызов лога не в одну строку");
            }
        }
    }
}

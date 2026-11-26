using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Текст лога и исключения: конкатенация вместо интерполяции, точка в конце.
    // Лог - вызов метода логгера из project.jsonc; исключение - new *Exception(...) в throw.
    // Текст, разделенный по строкам (SplitText, C22), - не конкатенация
    public class MessageTextCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Bool("concatenation", true, "склейка строк через + - находка"),
            CheckParameter.Bool("trailingPeriod", true, "точка в конце текста - находка"),
            CheckParameter.Bool("exceptions", true, "проверять тексты исключений в throw"),
        ];

        public override string Name
            => "message-text";

        public override string Description
            => "текст лога или исключения";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool exceptions = context.Settings.GetBool("exceptions");
            foreach (var node in context.Root.DescendantNodes())
            {
                var arguments = node switch
                {
                    InvocationExpressionSyntax invocation when LoggerCallTool.IsLoggerCall(context, invocation)
                        => invocation.ArgumentList,
                    BaseObjectCreationExpressionSyntax creation when exceptions && IsThrownException(creation)
                        => creation.ArgumentList,
                    _ => null,
                };

                if (arguments == null)
                    continue;

                foreach (var argument in arguments.Arguments)
                {
                    if (Invocations.IsStringLike(argument.Expression))
                        CheckMessage(context, argument.Expression);
                }
            }
        }

        private static void CheckMessage(CheckContext context, ExpressionSyntax message)
        {
            bool isSplit = SplitText.GetParts(context.Text, message) != null;
            if (context.Settings.GetBool("concatenation") && message is BinaryExpressionSyntax && !isSplit)
                context.Report(message.Span, "текст склеен через + - нужна интерполяция $\"...\"");

            if (!context.Settings.GetBool("trailingPeriod"))
                return;

            var period = FindTrailingPeriod(message);
            if (period != null)
            {
                var replacement = context.ReplaceInLines(new TextSpan(period.Value, 1), "");
                context.Report(new TextSpan(period.Value, 1), "точка в конце текста", replacement);
            }
        }

        // Позиция точки в конце последней текстовой части сообщения; многоточие не считается
        private static int? FindTrailingPeriod(ExpressionSyntax message)
        {
            var last = message;
            while (last is BinaryExpressionSyntax binary)
                last = binary.Right;

            string text;
            int end;
            switch (last)
            {
                case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken):
                    text = literal.Token.ValueText;
                    end = literal.Token.Span.End - 1;
                    break;
                case InterpolatedStringExpressionSyntax
                {
                    Contents: { Count: > 0 } contents,
                    StringStartToken.Text: "$\"" or "$@\"" or "@$\"",
                } when contents[contents.Count - 1] is InterpolatedStringTextSyntax part:
                    text = part.TextToken.ValueText;
                    end = part.TextToken.Span.End;
                    break;
                default:
                    return null;
            }

            if (!text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("..", StringComparison.Ordinal))
                return null;

            return end - 1;
        }

        private static bool IsThrownException(BaseObjectCreationExpressionSyntax creation)
        {
            if (creation.Parent is not (ThrowStatementSyntax or ThrowExpressionSyntax))
                return false;

            return creation is ImplicitObjectCreationExpressionSyntax
                || creation is ObjectCreationExpressionSyntax { Type: var type }
                && type.ToString().EndsWith("Exception", StringComparison.Ordinal);
        }
    }
}

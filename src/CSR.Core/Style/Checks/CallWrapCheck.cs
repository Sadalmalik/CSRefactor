using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Длинный вызов столбиком: перенос после '(', аргумент на строку с отступом +4 к строке вызова, ')' на строке
    // последнего аргумента. Находка: строка длиннее max с вызовом в одну строку (переносится внешний, вложенные -
    // следующим проходом csr fix); перенос посреди списка (первый аргумент в строке вызова).
    // Не трогает: условия if / while (свое правило переноса), вызовы логгера (log-call-wrap), строки с комментарием
    public class CallWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки"),
        ];

        public override string Name
            => "call-wrap";

        public override string Description
            => "длинный вызов столбиком";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            var lists = context.Root.DescendantNodes()
                .OfType<ArgumentListSyntax>()
                .Where(list => list.Arguments.Count > 0 && IsChecked(context, list))
                .ToList();
            var text = context.Text;
            var oneLine = lists.Where(list => TextLines.IsSingleLine(text, list.Span)).ToList();
            foreach (var group in oneLine.GroupBy(list => TextLines.GetLineNumber(text, list.SpanStart)))
            {
                var line = text.Lines[group.Key];
                // длину портит выровненный отступ продолжения, а не вызов: с нормальным отступом (+4) строка
                // может поместиться - тогда переносить нечего (отступ - condition-wrap или агент)
                var excess = TextLines.GetContinuationExcess(context.Root, text, group.Key);
                if (line.Span.Length - excess <= max)
                    continue;

                var candidate = group
                    .Where(list => !group.Any(other => other != list && other.Span.Contains(list.Span)))
                    .Where(list => TextLines.GetColumn(text, list.OpenParenToken.Span.End) - excess < max)
                    .OrderByDescending(list => list.Span.Length)
                    .FirstOrDefault();
                if (candidate != null)
                    Report(context, candidate, "вызов длиннее порога - столбиком");
            }

            foreach (var list in lists.Except(oneLine))
            {
                var openLine = TextLines.GetLineNumber(text, list.OpenParenToken.SpanStart);
                var allSingle = list.Arguments.All(argument => TextLines.IsSingleLine(text, argument.Span));
                if (allSingle && TextLines.GetLineNumber(text, list.Arguments[0].SpanStart) == openLine)
                    Report(context, list, "перенос посреди списка аргументов - после '(', аргумент на строку");
            }
        }

        // Вызов - "голова" инструкции: сама инструкция, значение присваивания или инициализатора, return, тело '=>'
        // или лямбды, аргумент другого такого вызова. Внутри тернарника, бинарного выражения, цепочки вызовов
        // раскладка не однозначна (F24, F25, F26 или вынос в переменную - F20)
        private static bool IsHeadPosition(ArgumentListSyntax list)
        {
            SyntaxNode current = list.Parent!;
            while (current.Parent is { } parent)
            {
                switch (parent)
                {
                    case ArgumentSyntax or ArgumentListSyntax or AwaitExpressionSyntax:
                    case ExpressionElementSyntax or CollectionExpressionSyntax or InitializerExpressionSyntax:
                    case InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax
                        when current is ArgumentListSyntax or InitializerExpressionSyntax:
                    case AssignmentExpressionSyntax assignment when assignment.Right == current:
                        current = parent;
                        continue;
                    case EqualsValueClauseSyntax or ArrowExpressionClauseSyntax or LambdaExpressionSyntax:
                    case ReturnStatementSyntax or ExpressionStatementSyntax:
                    case ThrowStatementSyntax or YieldStatementSyntax:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool IsChecked(CheckContext context, ArgumentListSyntax list)
        {
            if (TextLines.HasCommentOrDirective(list))
                return false;

            if (list.Parent is InvocationExpressionSyntax invocation
                && (Invocations.IsLoggerCall(context, invocation) || IsNameof(invocation)))
                return false;

            foreach (var ancestor in list.Ancestors())
            {
                switch (ancestor)
                {
                    case InterpolationSyntax:
                        return false;
                    case IfStatementSyntax ifStatement when ifStatement.Condition.Span.Contains(list.Span):
                        return false;
                    case WhileStatementSyntax whileStatement when whileStatement.Condition.Span.Contains(list.Span):
                        return false;
                    case ArrowExpressionClauseSyntax arrow when IsInlineArrow(context, arrow, list):
                        return false;
                    case StatementSyntax or MemberDeclarationSyntax:
                        return true;
                }
            }

            return true;
        }

        // '=>' члена в строке объявления и в строке вызова: сначала переносится '=>' (expression-arrow),
        // вызов - следующим проходом уже на своей строке
        private static bool IsInlineArrow(
            CheckContext context,
            ArrowExpressionClauseSyntax arrow,
            ArgumentListSyntax list)
        {
            if (arrow.Parent is AccessorDeclarationSyntax)
                return false;

            var text = context.Text;
            var arrowLine = TextLines.GetLineNumber(text, arrow.ArrowToken.SpanStart);
            return arrowLine == TextLines.GetLineNumber(text, list.SpanStart)
                && arrowLine == TextLines.GetLineNumber(text, arrow.ArrowToken.GetPreviousToken().SpanStart);
        }

        private static bool IsNameof(InvocationExpressionSyntax invocation)
            => invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" };

        private static void Report(CheckContext context, ArgumentListSyntax list, string message)
        {
            var text = context.Text;
            if (!IsHeadPosition(list))
            {
                var nested = message + "; вызов внутри выражения - раскладка по F24 / F25 или вынос в переменную";
                context.Report(
                    TextSpan.FromBounds(list.OpenParenToken.SpanStart, list.CloseParenToken.Span.End),
                    nested);
                return;
            }

            var openLine = text.Lines.GetLineFromPosition(list.OpenParenToken.SpanStart);
            var closeLine = text.Lines.GetLineFromPosition(list.CloseParenToken.SpanStart);
            var inner = TextLines.Deeper(TextLines.GetIndent(text, list.OpenParenToken.SpanStart));
            var lines = new List<string>
            {
                text.ToString(TextSpan.FromBounds(openLine.Start, list.OpenParenToken.Span.End)),
            };

            var arguments = list.Arguments;
            for (var i = 0; i < arguments.Count; i++)
            {
                var separator = i < arguments.Count - 1 ? "," : ")";
                lines.Add(inner + arguments[i].ToString().Trim() + separator);
            }

            lines[lines.Count - 1] += text.ToString(TextSpan.FromBounds(list.CloseParenToken.Span.End, closeLine.End));
            context.Report(TextSpan.FromBounds(openLine.Start, closeLine.End), message, lines);
        }
    }
}

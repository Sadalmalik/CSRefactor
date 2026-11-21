using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Перенос логического условия: цепочка '&&' / '||' в строке длиннее max - по операнду на строку, оператор
    // в начале строки продолжения с отступом +4 к оператору (в return '|| ' встает под первый операнд);
    // аргументы вызова в перенесенном условии if / while на уровне тела - на +8, чтобы не совпасть с телом
    public class ConditionWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки с цепочкой"),
            CheckParameter.Bool("argumentIndent", true, "аргументы вызова в перенесенном условии if / while - на +8"),
        ];

        public override string Name
            => "condition-wrap";

        public override string Description
            => "перенос логического условия";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            foreach (var node in context.Root.DescendantNodes())
            {
                var condition = GetCondition(node);
                if (condition == null || TextLines.HasCommentOrDirective(condition))
                    continue;

                CheckChain(context, node, condition, max);
                if (context.Settings.GetBool("argumentIndent") && node is IfStatementSyntax or WhileStatementSyntax)
                    CheckArguments(context, node, condition);
            }
        }

        private static ExpressionSyntax? GetCondition(SyntaxNode node)
        {
            return node switch
            {
                IfStatementSyntax ifStatement => ifStatement.Condition,
                WhileStatementSyntax whileStatement => whileStatement.Condition,
                ReturnStatementSyntax returnStatement => returnStatement.Expression,
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax } value => value.Value,
                _ => null,
            };
        }

        // Цепочка одного оператора верхнего уровня в одну строку длиннее max
        private static void CheckChain(CheckContext context, SyntaxNode statement, ExpressionSyntax condition, int max)
        {
            var text = context.Text;
            if (condition is not BinaryExpressionSyntax binary || !IsLogical(binary)
                || !TextLines.IsSingleLine(text, condition.Span)
                || TextLines.GetLineLength(text, condition.SpanStart) <= max)
                return;

            var operands = new List<ExpressionSyntax>();
            Flatten(binary, binary.Kind(), operands);
            var line = text.Lines.GetLineFromPosition(condition.SpanStart);
            var indent = TextLines.Deeper(TextLines.GetIndent(text, statement.SpanStart));
            var operatorText = binary.OperatorToken.Text;
            var lines = new List<string> { text.ToString(TextSpan.FromBounds(line.Start, operands[0].Span.End)) };
            for (var i = 1; i < operands.Count; i++)
                lines.Add($"{indent}{operatorText} {operands[i]}");

            lines[lines.Count - 1] += text.ToString(TextSpan.FromBounds(condition.Span.End, line.End));
            context.Report(line.Span, $"цепочка '{operatorText}' длиннее порога - по операнду на строку", lines);
        }

        private static bool IsLogical(BinaryExpressionSyntax binary)
            => binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression);

        // ((a || b) || c) -> a, b, c; операнды другого оператора остаются целыми
        private static void Flatten(ExpressionSyntax expression, SyntaxKind kind, List<ExpressionSyntax> operands)
        {
            if (expression is BinaryExpressionSyntax binary && binary.IsKind(kind))
            {
                Flatten(binary.Left, kind, operands);
                Flatten(binary.Right, kind, operands);
                return;
            }

            operands.Add(expression);
        }

        // Аргумент с начала строки внутри перенесенного условия на уровне тела (+4) - на +8
        private static void CheckArguments(CheckContext context, SyntaxNode statement, ExpressionSyntax condition)
        {
            var text = context.Text;
            if (TextLines.IsSingleLine(text, condition.Span))
                return;

            var body = TextLines.Deeper(TextLines.GetIndent(text, statement.SpanStart));
            var wanted = TextLines.Deeper(body);
            var arguments = condition.DescendantNodes().OfType<ArgumentSyntax>();
            foreach (var argument in arguments)
            {
                var line = text.Lines.GetLineFromPosition(argument.SpanStart);
                var value = line.ToString();
                var indent = value.Substring(0, value.Length - value.TrimStart().Length);
                if (line.Start + indent.Length != argument.SpanStart || indent != body)
                    continue;

                var message = "аргумент вызова в условии на уровне тела - на +8";
                context.Report(line.Span, message, [wanted + value.TrimStart()]);
            }
        }
    }
}

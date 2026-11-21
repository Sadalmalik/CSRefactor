using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Перенос логического условия: цепочка '&&' / '||' в строке длиннее max - по операнду на строку, оператор
    // в начале строки продолжения с отступом +4 к оператору (в return '|| ' встает под первый операнд);
    // аргументы вызова в перенесенном условии if / while на уровне тела - на +8, чтобы не совпасть с телом;
    // строка продолжения цепочки ('&&' / '||' в начале) - на +4 к инструкции: в if / while всегда (F24),
    // в присваивании и инициализаторе - по assignmentIndent (гайд отступ там не задает), в return - не трогается
    public class ConditionWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки с цепочкой"),
            CheckParameter.Bool("argumentIndent", true, "аргументы вызова в перенесенном условии if / while - на +8"),
            CheckParameter.Bool("continuationIndent", true, "продолжение цепочки в условии if / while - на +4"),
            CheckParameter.Bool(
                "assignmentIndent",
                false,
                "продолжение цепочки в присваивании и инициализаторе - на +4"),
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
                bool isLoopOrIf = node is IfStatementSyntax or WhileStatementSyntax;
                if (context.Settings.GetBool("argumentIndent") && isLoopOrIf)
                    CheckArguments(context, node, condition);

                bool isAssignment = node is EqualsValueClauseSyntax or ExpressionStatementSyntax;
                if (isLoopOrIf && context.Settings.GetBool("continuationIndent")
                    || isAssignment && context.Settings.GetBool("assignmentIndent"))
                    CheckContinuation(context, node, condition);
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
                ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } => assignment.Right,
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

        // Оператор цепочки верхнего уровня в начале строки: отступ - ровно +4 к строке инструкции
        private static void CheckContinuation(CheckContext context, SyntaxNode statement, ExpressionSyntax condition)
        {
            if (condition is not BinaryExpressionSyntax binary || !IsLogical(binary))
                return;

            var text = context.Text;
            var normal = TextLines.Deeper(TextLines.GetIndent(text, statement.SpanStart));
            var operators = new List<SyntaxToken>();
            CollectOperators(binary, binary.Kind(), operators);
            foreach (var token in operators)
            {
                var line = text.Lines.GetLineFromPosition(token.SpanStart);
                var value = line.ToString();
                var indent = value.Substring(0, value.Length - value.TrimStart().Length);
                if (line.Start + indent.Length != token.SpanStart || indent == normal)
                    continue;

                context.Report(line.Span, "отступ продолжения условия - +4 к инструкции", [normal + value.TrimStart()]);
            }
        }

        private static void CollectOperators(ExpressionSyntax expression, SyntaxKind kind, List<SyntaxToken> operators)
        {
            if (expression is not BinaryExpressionSyntax binary || !binary.IsKind(kind))
                return;

            CollectOperators(binary.Left, kind, operators);
            operators.Add(binary.OperatorToken);
            CollectOperators(binary.Right, kind, operators);
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

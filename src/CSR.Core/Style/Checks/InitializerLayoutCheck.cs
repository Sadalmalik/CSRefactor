using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Инициализатор объекта, коллекции или массива столбиком: '{' на строке под 'new' с отступом строки 'new',
    // элемент на строку +4, запятая после последнего (trailingComma). Находка: однострочный длиннее max;
    // в многострочном - '{' в конце строки с 'new', два элемента в одной строке, нет запятой после последнего.
    // Короткий однострочный - допустим
    public class InitializerLayoutCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки однострочного инициализатора"),
            CheckParameter.Bool("trailingComma", true, "запятая после последнего элемента многострочного"),
        ];

        public override string Name
            => "initializer-layout";

        public override string Description
            => "инициализатор столбиком";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            bool trailingComma = context.Settings.GetBool("trailingComma");
            foreach (var initializer in context.Root.DescendantNodes().OfType<InitializerExpressionSyntax>())
            {
                if (!IsChecked(initializer) || TextLines.HasCommentOrDirective(initializer))
                    continue;

                var problem = GetProblem(context.Text, initializer, max, trailingComma);
                if (problem != null)
                    Report(context, initializer, problem, trailingComma);
            }
        }

        private static bool IsChecked(InitializerExpressionSyntax initializer)
        {
            var kind = initializer.Kind();
            bool isOwnInitializer = kind
                is SyntaxKind.ObjectInitializerExpression
                or SyntaxKind.CollectionInitializerExpression
                or SyntaxKind.ArrayInitializerExpression;
            return isOwnInitializer && initializer.Expressions.Count > 0
                && initializer.Parent is BaseObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax
                    or ImplicitArrayCreationExpressionSyntax;
        }

        private static string? GetProblem(
            SourceText text,
            InitializerExpressionSyntax initializer,
            int max,
            bool trailingComma)
        {
            var expressions = initializer.Expressions;
            if (expressions.Any(expression => !TextLines.IsSingleLine(text, expression.Span)))
                return null;

            if (TextLines.IsSingleLine(text, initializer.Span))
            {
                var length = TextLines.GetLineLength(text, initializer.SpanStart);
                return length > max ? "инициализатор длиннее порога - столбиком" : null;
            }

            var newLine = TextLines.GetLineNumber(text, initializer.Parent!.SpanStart);
            if (TextLines.GetLineNumber(text, initializer.OpenBraceToken.SpanStart) == newLine)
                return "'{' в строке с 'new' - на следующую строку";

            var lines = expressions.Select(expression => TextLines.GetLineNumber(text, expression.SpanStart));
            if (lines.Distinct().Count() != expressions.Count)
                return "два элемента в одной строке - элемент на строку";

            bool hasTrailing = expressions.SeparatorCount == expressions.Count;
            return trailingComma && !hasTrailing ? "нет запятой после последнего элемента" : null;
        }

        // Строки от строки 'new' до строки '}' включительно; текст до 'new {' и после '}' сохраняется
        private static void Report(
            CheckContext context,
            InitializerExpressionSyntax initializer,
            string problem,
            bool trailingComma)
        {
            var text = context.Text;
            var creation = initializer.Parent!;
            var firstLine = text.Lines.GetLineFromPosition(creation.SpanStart);
            var closeLine = text.Lines.GetLineFromPosition(initializer.CloseBraceToken.SpanStart);
            var indent = TextLines.GetIndent(text, creation.SpanStart);
            var inner = TextLines.Deeper(indent);
            var head = text.ToString(
                TextSpan.FromBounds(firstLine.Start, initializer.OpenBraceToken.SpanStart)).TrimEnd();
            if (head.Contains('\n'))
                return;

            var lines = new List<string> { head, indent + "{" };
            var expressions = initializer.Expressions;
            for (var i = 0; i < expressions.Count; i++)
            {
                bool last = i == expressions.Count - 1;
                var comma = !last || trailingComma ? "," : "";
                lines.Add(inner + expressions[i].ToString().Trim() + comma);
            }

            var tail = text.ToString(TextSpan.FromBounds(initializer.CloseBraceToken.Span.End, closeLine.End));
            lines.Add(indent + "}" + tail);
            context.Report(TextSpan.FromBounds(firstLine.Start, closeLine.End), problem, lines);
        }
    }
}

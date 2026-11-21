using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Место '=>' у члена с телом-выражением: в конце строки - всегда находка; у свойства - на следующей строке
    // (+4 к отступу члена); у метода - в строке сигнатуры, если строка помещается в max, иначе на следующей
    public class ExpressionArrowCheck : SyntaxCheck
    {
        private const string MethodsDescription = "методы, конструкторы, операторы, локальные функции: "
            + "fit - в строке сигнатуры, если помещается в max; next-line - всегда с новой строки; any - любое";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice(
                "properties",
                "свойства и индексаторы: next-line - '=>' с новой строки; any - любое",
                "next-line",
                "any"),
            CheckParameter.Choice(
                "methods",
                MethodsDescription,
                "fit",
                "next-line",
                "any"),
            CheckParameter.Int("max", 120, "длина строки для methods: fit"),
        ];

        public override string Name
            => "expression-arrow";

        public override string Description
            => "место '=>' у члена с телом-выражением";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var properties = context.Settings.GetString("properties");
            var methods = context.Settings.GetString("methods");
            var max = context.Settings.GetInt("max");
            foreach (var clause in context.Root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>())
            {
                bool isProperty = clause.Parent is PropertyDeclarationSyntax or IndexerDeclarationSyntax;
                bool isMethod = clause.Parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax;
                if (!isProperty && !isMethod || HasComments(clause))
                    continue;

                var arrow = clause.ArrowToken;
                var text = context.Text;
                var arrowLine = TextLines.GetLineNumber(text, arrow.SpanStart);
                var indent = TextLines.Deeper(TextLines.GetIndent(text, GetDeclarationStart(clause.Parent!)));
                if (TextLines.GetLineNumber(text, clause.Expression.SpanStart) > arrowLine)
                {
                    ReportTrailing(context, clause, indent);
                    continue;
                }

                if (TextLines.GetLineNumber(text, arrow.GetPreviousToken().SpanStart) != arrowLine)
                    continue;

                var rule = isProperty ? properties : methods;
                bool split = rule == "next-line" || rule == "fit" && TextLines.GetLineLength(
                    text,
                    arrow.SpanStart) > max;
                if (split)
                    ReportSplit(context, arrow, indent, isProperty);
            }
        }

        // '=>' в конце строки: переносится в начало строки выражения
        private static void ReportTrailing(CheckContext context, ArrowExpressionClauseSyntax clause, string indent)
        {
            var text = context.Text;
            var arrowLine = text.Lines.GetLineFromPosition(clause.ArrowToken.SpanStart);
            var expressionLine = text.Lines.GetLineFromPosition(clause.Expression.SpanStart);
            if (expressionLine.LineNumber != arrowLine.LineNumber + 1)
                return;

            var before = text.ToString(TextSpan.FromBounds(arrowLine.Start, clause.ArrowToken.SpanStart)).TrimEnd();
            var expression = expressionLine.ToString().TrimStart();
            var span = TextSpan.FromBounds(arrowLine.Start, expressionLine.End);
            context.Report(span, "'=>' в конце строки", [before, $"{indent}=> {expression}"]);
        }

        // '=>' в строке объявления: с '=>' - на следующую строку
        private static void ReportSplit(CheckContext context, SyntaxToken arrow, string indent, bool isProperty)
        {
            var line = context.Text.Lines.GetLineFromPosition(arrow.SpanStart);
            var before = context.Text.ToString(TextSpan.FromBounds(line.Start, arrow.SpanStart)).TrimEnd();
            var after = context.Text.ToString(TextSpan.FromBounds(arrow.SpanStart, line.End));
            var message = isProperty ? "'=>' свойства - с новой строки" : "строка с '=>' длиннее порога";
            context.Report(line.Span, message, [before, indent + after]);
        }

        private static bool HasComments(ArrowExpressionClauseSyntax clause)
        {
            var previous = clause.ArrowToken.GetPreviousToken();
            return previous.TrailingTrivia.Concat(clause.ArrowToken.LeadingTrivia).Concat(
                clause.ArrowToken.TrailingTrivia)
                .Any(
                    trivia => trivia.ToString().TrimStart().StartsWith(
                        "/",
                        StringComparison.Ordinal) || trivia.IsDirective);
        }

        // Начало объявления без атрибутов: от его строки считается отступ
        private static int GetDeclarationStart(SyntaxNode member)
        {
            var first = member.DescendantTokens()
                .First(token => !token.Parent!.AncestorsAndSelf().OfType<AttributeListSyntax>().Any());
            return first.SpanStart;
        }
    }
}

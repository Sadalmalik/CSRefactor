using CSR.Core.Checks;
using CSR.Core.Syntax;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Место '=>' у члена с телом-выражением: в конце строки - всегда находка; у свойства и метода - по своему правилу:
    // next-line - на следующей строке (+4 к отступу члена); fit - в строке объявления, если строка помещается в max,
    // иначе на следующей (у свойства перенос, который помещается в max одной строкой, - тоже находка)
    public class ExpressionArrowCheck : SyntaxCheck
    {
        private const string PropertiesDescription = "свойства и индексаторы: next-line - '=>' с новой строки; "
            + "fit - в строке объявления, если помещается в max, иначе с новой строки, перенос, который помещается, "
            + "собирается в одну строку; any - любое";

        private const string MethodsDescription = "методы, конструкторы, операторы, локальные функции: "
            + "fit - в строке сигнатуры, если помещается в max, перенос не собирается; next-line - всегда с новой строки; "
            + "any - любое";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice(
                "properties",
                PropertiesDescription,
                "next-line",
                "fit",
                "any"),
            CheckParameter.Choice(
                "methods",
                MethodsDescription,
                "fit",
                "next-line",
                "any"),
            CheckParameter.Int("max", 120, "длина строки для fit"),
            CheckParameter.Bool(
                "ignoreSingle",
                false,
                "не проверять место '=>' у одиночного свойства: ближайшие члены типа выше и ниже - не '=>'-свойства"),
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
            bool ignoreSingle = context.Settings.GetBool("ignoreSingle");
            foreach (var clause in context.Root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>())
            {
                bool isProperty = clause.Parent is PropertyDeclarationSyntax or IndexerDeclarationSyntax;
                bool isMethod = clause.Parent is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax;
                if (!isProperty && !isMethod || HasComments(clause))
                    continue;

                var arrow = clause.ArrowToken;
                var text = context.Text;
                var arrowLine = TextLines.GetLineNumber(text, arrow.SpanStart);
                var start = SyntaxParts.GetDeclarationStart(clause.Parent!);
                var indent = TextLayout.Deeper(TextLayout.GetIndent(text, start));
                if (TextLines.GetLineNumber(text, clause.Expression.SpanStart) > arrowLine)
                {
                    ReportTrailing(context, clause, indent);
                    continue;
                }

                var rule = isProperty ? properties : methods;
                if (rule == "any" || isProperty && ignoreSingle && IsSingle(clause.Parent!))
                    continue;

                if (TextLines.GetLineNumber(text, arrow.GetPreviousToken().SpanStart) != arrowLine)
                {
                    if (isProperty && rule == "fit")
                        ReportJoin(context, clause, max);
                    continue;
                }

                if (rule == "next-line" || TextLines.GetLineLength(text, arrow.SpanStart) > max)
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
            context.Report(
                span,
                "'=>' в конце строки",
                context.ReplaceLines(arrowLine.LineNumber, expressionLine.LineNumber, [before, $"{indent}=> {expression}"]));
        }

        // '=>' в строке объявления: с '=>' - на следующую строку
        private static void ReportSplit(CheckContext context, SyntaxToken arrow, string indent, bool isProperty)
        {
            var line = context.Text.Lines.GetLineFromPosition(arrow.SpanStart);
            var before = context.Text.ToString(TextSpan.FromBounds(line.Start, arrow.SpanStart)).TrimEnd();
            var after = context.Text.ToString(TextSpan.FromBounds(arrow.SpanStart, line.End));
            var message = isProperty ? "'=>' свойства - с новой строки" : "строка с '=>' длиннее порога";
            context.Report(line.Span, message, context.ReplaceLines(line.Span, [before, indent + after]));
        }

        // '=>' свойства - на строке после объявления, свойство целиком помещается в max одной строкой: сборка в одну
        private static void ReportJoin(CheckContext context, ArrowExpressionClauseSyntax clause, int max)
        {
            var text = context.Text;
            var arrowLine = text.Lines.GetLineFromPosition(clause.ArrowToken.SpanStart);
            var declarationLine = text.Lines.GetLineFromPosition(clause.ArrowToken.GetPreviousToken().SpanStart);
            var end = clause.Parent!.GetLastToken().Span.End;
            if (declarationLine.LineNumber != arrowLine.LineNumber - 1 || end > arrowLine.End)
                return;

            if (!string.IsNullOrWhiteSpace(text.ToString(TextSpan.FromBounds(end, arrowLine.End))))
                return;

            var joined = declarationLine.ToString().TrimEnd() + " " + arrowLine.ToString().TrimStart();
            if (joined.Length > max)
                return;

            context.Report(
                TextSpan.FromBounds(declarationLine.Start, arrowLine.End),
                "'=>' свойства помещается в строку объявления",
                context.ReplaceLines(declarationLine.LineNumber, arrowLine.LineNumber, [joined]));
        }

        // Одиночное свойство: ближайшие члены того же типа выше и ниже - не '=>'-свойства
        private static bool IsSingle(SyntaxNode member)
        {
            // Код с ошибкой (CS0116): Roslyn кладет свойство в пространство имен или файл
            if (member.Parent is not TypeDeclarationSyntax type)
                return false;

            var members = type.Members;
            var index = members.IndexOf((MemberDeclarationSyntax)member);
            var previous = index > 0 ? members[index - 1] : null;
            var next = index + 1 < members.Count ? members[index + 1] : null;
            return !IsArrowProperty(previous) && !IsArrowProperty(next);
        }

        private static bool IsArrowProperty(MemberDeclarationSyntax? member)
            => member is PropertyDeclarationSyntax { ExpressionBody: not null }
                or IndexerDeclarationSyntax { ExpressionBody: not null };

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
    }
}

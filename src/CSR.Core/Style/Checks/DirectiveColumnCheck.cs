using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Директива препроцессора не с колонки 0
    public class DirectiveColumnCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("directives", "директивы без # (region, endregion, if, ...); пусто - все"),
        ];

        public override string Name
            => "directive-column";

        public override string Description
            => "директива не с колонки 0";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var directives = context.Settings.GetStrings("directives");
            foreach (var trivia in context.Root.DescendantTrivia())
            {
                if (!trivia.IsDirective || trivia.GetStructure() is not DirectiveTriviaSyntax directive)
                    continue;

                var keyword = directive.DirectiveNameToken.Text;
                if (directives.Count > 0 && !directives.Contains(keyword))
                    continue;

                var line = context.Text.Lines.GetLineFromPosition(trivia.SpanStart);
                if (trivia.SpanStart == line.Start)
                    continue;

                var indent = TextSpan.FromBounds(line.Start, trivia.SpanStart);
                context.Report(trivia.Span, $"'#{keyword}' не с колонки 0", context.ReplaceInLines(indent, ""));
            }
        }
    }
}

using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Документационный комментарий /// или /** */; замена - удаление его строк
    public class XmlDocCheck : SyntaxCheck
    {
        public override string Name
            => "xml-doc";

        public override string Description
            => "XML-документация";

        public override void Analyze(CheckContext context)
        {
            foreach (var trivia in context.Root.DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                    continue;

                var span = TrimLineBreaks(context.Text, trivia.FullSpan);
                var lines = context.GetLinesSpan(span);
                bool isAlone = context.Text.ToString(TextSpan.FromBounds(lines.Start, span.Start)).Trim().Length == 0
                    && context.Text.ToString(TextSpan.FromBounds(span.End, lines.End)).Trim().Length == 0;

                context.Report(span, "XML-документация", isAlone ? Array.Empty<string>() : null);
            }
        }

        private static TextSpan TrimLineBreaks(SourceText text, TextSpan span)
        {
            var end = span.End;
            while (end > span.Start && (text[end - 1] == '\n' || text[end - 1] == '\r'))
                end--;

            return TextSpan.FromBounds(span.Start, end);
        }
    }
}

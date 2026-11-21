using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Правки целыми строками: строки узла с его комментариями, удаление строк с выравниванием пустых строк
    public static class SourceLines
    {
        // Строки узла: от первой строки комментария или атрибута перед ним (пустые строки и директивы
        // препроцессора перед узлом - не его) до конца строки последнего токена вместе с переводом строки
        public static TextSpan GetNodeLines(SourceText text, SyntaxNode node)
        {
            var start = node.SpanStart;
            var leading = node.GetLeadingTrivia();
            for (var i = leading.Count - 1; i >= 0; i--)
            {
                var trivia = leading[i];
                if (trivia.IsDirective)
                    break;

                if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                    && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    start = trivia.SpanStart;
            }

            var first = text.Lines.GetLineFromPosition(start);
            var last = text.Lines.GetLineFromPosition(node.Span.End);
            return TextSpan.FromBounds(first.Start, last.EndIncludingLineBreak);
        }

        // Удаление строк (span - целые строки); на каждом стыке пустых строк остается не больше одной,
        // после '{', перед '}', в начале и в конце файла - ни одной
        public static SourceText RemoveLines(SourceText text, IEnumerable<TextSpan> spans)
        {
            foreach (var span in spans.OrderByDescending(item => item.Start))
            {
                text = text.WithChanges(new TextChange(span, ""));
                text = NormalizeBlankLines(text, span.Start);
            }

            return text;
        }

        private static SourceText NormalizeBlankLines(SourceText text, int position)
        {
            if (text.Length == 0)
                return text;

            var line = text.Lines.GetLineFromPosition(Math.Min(position, text.Length)).LineNumber;
            var previous = line - 1;
            while (previous >= 0 && IsBlank(text, previous))
                previous--;

            var next = line;
            while (next < text.Lines.Count && IsBlank(text, next))
                next++;

            var start = previous < 0 ? 0 : text.Lines[previous].EndIncludingLineBreak;
            var end = next < text.Lines.Count ? text.Lines[next].Start : text.Length;
            if (start >= end)
                return text;

            bool isEdge = previous < 0 || next >= text.Lines.Count
                || text.Lines[previous].ToString().TrimEnd().EndsWith("{", StringComparison.Ordinal)
                || text.Lines[next].ToString().TrimStart().StartsWith("}", StringComparison.Ordinal);
            var blank = isEdge ? "" : GetLineBreak(text);
            return text.WithChanges(new TextChange(TextSpan.FromBounds(start, end), blank));
        }

        public static string GetLineBreak(SourceText text)
            => text.ToString().Contains("\r\n") ? "\r\n" : "\n";

        private static bool IsBlank(SourceText text, int line)
            => string.IsNullOrWhiteSpace(text.Lines[line].ToString());
    }
}

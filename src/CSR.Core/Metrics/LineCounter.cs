using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Metrics
{
    // Физические строки - от первой до последней строки узла. Строки кода - строки, на которых есть хотя бы один токен:
    // пустые строки, комментарии, директивы препроцессора и выключенный директивами код не считаются
    public static class LineCounter
    {
        public static int CountPhysical(SyntaxNode node)
        {
            var span = node.GetLocation().GetLineSpan();
            return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }

        public static int CountCode(SyntaxNode node, SourceText text)
        {
            int count = 0;
            int lastCounted = -1;
            foreach (var token in node.DescendantTokens())
            {
                if (token.Span.IsEmpty)
                    continue;

                int start = text.Lines.GetLineFromPosition(token.SpanStart).LineNumber;
                int end = text.Lines.GetLineFromPosition(token.Span.End - 1).LineNumber;
                start = Math.Max(start, lastCounted + 1);
                if (end < start)
                    continue;

                count += end - start + 1;
                lastCounted = end;
            }

            return count;
        }
    }
}

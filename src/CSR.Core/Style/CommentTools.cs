using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style
{
    // Однострочный комментарий // (не ///) с текстом после // и местом в строке
    public class LineComment
    {
        public required SyntaxTrivia Trivia { get; init; }

        // Текст после //, без пробелов по краям
        public required string Content { get; init; }

        public required int Line { get; init; }

        // Перед комментарием в строке только пробелы
        public required bool IsFullLine { get; init; }

        public TextSpan Span
            => Trivia.Span;
    }

    public static class CommentTools
    {
        public static List<LineComment> GetLineComments(SyntaxNode root, SourceText text)
        {
            var comments = new List<LineComment>();
            foreach (var trivia in root.DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    continue;

                var raw = trivia.ToString();
                bool isDocumentation = raw.StartsWith("///", StringComparison.Ordinal)
                    && !raw.StartsWith("////", StringComparison.Ordinal);
                if (isDocumentation)
                    continue;

                var line = text.Lines.GetLineFromPosition(trivia.SpanStart);
                var before = text.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart));
                comments.Add(new LineComment
                {
                    Trivia = trivia,
                    Content = raw.Substring(2).Trim(),
                    Line = line.LineNumber,
                    IsFullLine = before.Trim().Length == 0,
                });
            }

            return comments;
        }

        // Комментарий в строке после кода: span от конца кода (без пробелов) до конца комментария
        public static TextSpan GetTrailingSpan(LineComment comment, SourceText text)
        {
            var line = text.Lines[comment.Line];
            var start = comment.Span.Start;
            while (start > line.Start && char.IsWhiteSpace(text[start - 1]))
                start--;

            return TextSpan.FromBounds(start, comment.Span.End);
        }
    }
}

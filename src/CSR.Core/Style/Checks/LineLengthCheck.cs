using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    public class LineLengthCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "наибольшая длина строки в символах"),
            CheckParameter.Bool("ignoreComments", true, "не проверять строки, которые начинаются с комментария"),
            CheckParameter.Bool("ignoreStringLines", true,
                "не проверять строки, которые начинаются со строкового литерала (перенесенный текст сообщения)"),
        ];

        public override string Name
            => "line-length";

        public override string Description
            => "строка длиннее порога";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            bool ignoreComments = context.Settings.GetBool("ignoreComments");
            bool ignoreStrings = context.Settings.GetBool("ignoreStringLines");
            foreach (var line in context.Text.Lines)
            {
                var length = line.Span.Length;
                if (length <= max)
                    continue;

                var start = FirstNonSpace(context.Text, line);
                if (ignoreComments && IsComment(context.Root.FindTrivia(start)))
                    continue;

                if (ignoreStrings && IsString(context.Root.FindToken(start)))
                    continue;

                var span = TextSpan.FromBounds(line.Start + max, line.End);
                context.Report(span, $"строка длиннее {max} символов: {length}");
            }
        }

        private static int FirstNonSpace(SourceText text, TextLine line)
        {
            var position = line.Start;
            while (position < line.End && char.IsWhiteSpace(text[position]))
                position++;

            return position;
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }

        private static bool IsString(SyntaxToken token)
        {
            return token.IsKind(SyntaxKind.StringLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedStringStartToken)
                || token.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken)
                || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedSingleLineRawStringStartToken)
                || token.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken);
        }
    }
}

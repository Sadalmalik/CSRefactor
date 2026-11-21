using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style
{
    // Текст, разделенный по строкам (C22): склейка '+' только строковых литералов и интерполяций, '+' в конце строки,
    // каждая следующая часть - с новой строки. Такая склейка - не конкатенация (C23)
    public static class SplitText
    {
        // Части разделенного текста по порядку; null - не разделенный текст
        public static IReadOnlyList<ExpressionSyntax>? GetParts(SourceText text, ExpressionSyntax expression)
        {
            if (expression is not BinaryExpressionSyntax)
                return null;

            var parts = new List<ExpressionSyntax>();
            var operators = new List<SyntaxToken>();
            if (!Collect(expression, parts, operators))
                return null;

            for (var i = 0; i < operators.Count; i++)
            {
                var operatorLine = TextLines.GetLineNumber(text, operators[i].SpanStart);
                bool isLineEnd = TextLines.GetLineNumber(text, parts[i].Span.End) == operatorLine
                    && TextLines.GetLineNumber(text, parts[i + 1].SpanStart) > operatorLine;
                if (!isLineEnd)
                    return null;
            }

            return parts;
        }

        // Длина текста одной частью: у стыка убираются закрывающая кавычка и начало следующей части;
        // литерал первой частью при интерполяции дальше получает '$'
        public static int GetJoinedLength(IReadOnlyList<ExpressionSyntax> parts)
        {
            var length = parts.Sum(part => part.Span.Length);
            for (var i = 1; i < parts.Count; i++)
                length -= 1 + GetPrefixLength(parts[i]);

            bool isInterpolated = parts.Any(part => part is InterpolatedStringExpressionSyntax);
            return isInterpolated && parts[0] is LiteralExpressionSyntax ? length + 1 : length;
        }

        private static bool Collect(
            ExpressionSyntax expression,
            List<ExpressionSyntax> parts,
            List<SyntaxToken> operators)
        {
            switch (expression)
            {
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                    if (!Collect(binary.Left, parts, operators))
                        return false;

                    operators.Add(binary.OperatorToken);
                    return Collect(binary.Right, parts, operators);
                case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken):
                case InterpolatedStringExpressionSyntax interpolated
                    when interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedStringStartToken)
                        || interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken):
                    parts.Add(expression);
                    return true;
                default:
                    return false;
            }
        }

        // Начало части до текста: '"', '@"', '$"', '$@"'
        private static int GetPrefixLength(ExpressionSyntax part)
        {
            if (part is InterpolatedStringExpressionSyntax interpolated)
                return interpolated.StringStartToken.Span.Length;

            return ((LiteralExpressionSyntax)part).Token.Text.StartsWith("@", StringComparison.Ordinal) ? 2 : 1;
        }
    }
}

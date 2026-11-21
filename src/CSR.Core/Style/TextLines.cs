using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style
{
    // Строки исходного текста для проверок, которые переносят и склеивают строки
    public static class TextLines
    {
        // Отступ строки, в которой стоит позиция
        public static string GetIndent(SourceText text, int position)
        {
            var line = text.Lines.GetLineFromPosition(position);
            var value = line.ToString();
            return value.Substring(0, value.Length - value.TrimStart().Length);
        }

        // Отступ на уровень глубже: отступ кончается табом - таб, иначе 4 пробела (смешанный старый отступ
        // не становится еще более смешанным)
        public static string Deeper(string indent)
            => indent + (indent.EndsWith("\t", StringComparison.Ordinal) ? "\t" : "    ");

        public static int GetLineNumber(SourceText text, int position)
            => text.Lines.GetLineFromPosition(position).LineNumber;

        public static string GetLine(SourceText text, int lineNumber)
            => text.Lines[lineNumber].ToString();

        // Длина строки, в которой стоит позиция
        public static int GetLineLength(SourceText text, int position)
            => text.Lines.GetLineFromPosition(position).Span.Length;

        // Колонка позиции с 0
        public static int GetColumn(SourceText text, int position)
            => position - text.Lines.GetLineFromPosition(position).Start;

        public static bool IsBlank(SourceText text, int lineNumber)
            => string.IsNullOrWhiteSpace(text.Lines[lineNumber].ToString());

        // В узле есть комментарий или директива: перенос строк мог бы их сломать или потерять
        public static bool HasCommentOrDirective(SyntaxNode node)
        {
            return node.DescendantTrivia(descendIntoTrivia: true).Any(trivia =>
                trivia.IsDirective
                || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia));
        }

        // Строка продолжения выражения, начатая бинарным оператором ('&&', '||', '+', '??'...): насколько ее отступ
        // больше нормы - отступа строки, где начинается оператор (инструкция или член), +4. 0 - не продолжение
        // или отступ не больше нормы
        public static int GetContinuationExcess(SyntaxNode root, SourceText text, int lineNumber)
        {
            var line = text.Lines[lineNumber];
            var value = line.ToString();
            var indent = value.Length - value.TrimStart().Length;
            if (indent == value.Length)
                return 0;

            var token = root.FindToken(line.Start + indent);
            if (token.Parent is not Microsoft.CodeAnalysis.CSharp.Syntax.BinaryExpressionSyntax binary
                || binary.OperatorToken != token)
                return 0;

            var owner = token.Parent.Ancestors()
                .FirstOrDefault(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax
                    or Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax);
            if (owner == null)
                return 0;

            var normal = Deeper(GetIndent(text, owner.SpanStart)).Length;
            return Math.Max(0, indent - normal);
        }

        // Узел или токен в одной строке
        public static bool IsSingleLine(SourceText text, TextSpan span)
            => GetLineNumber(text, span.Start) == GetLineNumber(text, span.End);
    }
}

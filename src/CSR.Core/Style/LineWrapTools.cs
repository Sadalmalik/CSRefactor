using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style
{
    // Строки исходного текста для проверок, которые переносят и склеивают строки
    public static class LineWrapTools
    {
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

            var normal = TextLayout.Deeper(TextLayout.GetIndent(text, owner.SpanStart)).Length;
            return Math.Max(0, indent - normal);
        }
    }
}

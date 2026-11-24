using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Syntax
{
    public static class Invocations
    {
        // Текст вызываемого выражения без пробелов и аргументов типа: Debug.Log, Log.Default.Error
        public static string GetTargetText(InvocationExpressionSyntax invocation)
        {
            var text = invocation.Expression is GenericNameSyntax generic
                ? generic.Identifier.Text
                : invocation.Expression.ToString();

            var builder = new System.Text.StringBuilder(text.Length);
            foreach (var symbol in text)
            {
                if (!char.IsWhiteSpace(symbol))
                    builder.Append(symbol);
            }

            return builder.ToString();
        }

        public static bool IsLoggerCall(CheckContext context, InvocationExpressionSyntax invocation)
            => context.Project.IsLoggerMethod(GetTargetText(invocation));

        // Текстовые аргументы сообщения: литерал, интерполяция или сложение со строкой
        public static bool IsStringLike(ExpressionSyntax expression)
        {
            return expression switch
            {
                LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
                InterpolatedStringExpressionSyntax => true,
                BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.AddExpression)
                    && (IsStringLike(binary.Left) || IsStringLike(binary.Right)),
                ParenthesizedExpressionSyntax parenthesized => IsStringLike(parenthesized.Expression),
                _ => false,
            };
        }
    }
}

using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style
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

        // Имя атрибута без пространства имен и суффикса Attribute
        public static string GetSimpleName(NameSyntax name)
        {
            var text = name switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
                AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
                SimpleNameSyntax simple => simple.Identifier.Text,
                _ => name.ToString(),
            };

            return text.EndsWith("Attribute", StringComparison.Ordinal) ? text.Substring(0, text.Length - 9) : text;
        }

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

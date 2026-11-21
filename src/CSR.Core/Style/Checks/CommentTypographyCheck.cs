using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Типографские знаки в комментариях, в текстах логов, атрибутов-подсказок и ToString
    public class CommentTypographyCheck : StyleCheck
    {
        private static readonly Dictionary<string, string> DefaultReplacements = new()
        {
            ["—"] = "-",
            ["–"] = "-",
            ["«"] = "\"",
            ["»"] = "\"",
            ["→"] = "->",
            ["×"] = "x",
            ["≈"] = "~",
            ["≤"] = "<=",
            ["≥"] = ">=",
        };

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Map("replace", DefaultReplacements, "знак и его замена"),
            CheckParameter.Strings("attributes", "атрибуты, строки которых проверяются", "Tooltip"),
            CheckParameter.Bool("logs", true, "проверять тексты вызовов логгера (методы - в project.jsonc)"),
            CheckParameter.Bool("toString", true, "проверять строки внутри методов ToString"),
        ];

        public override string Name
            => "comment-typography";

        public override string Description
            => "типографские знаки вместо ASCII";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var replacements = context.Settings.GetMap("replace");
            foreach (var trivia in context.Root.DescendantTrivia())
            {
                if (IsComment(trivia))
                    Check(context, trivia.Span, replacements);
            }

            foreach (var token in context.Root.DescendantTokens())
            {
                if (IsStringText(token) && IsCheckedString(context, token))
                    Check(context, token.Span, replacements);
            }
        }

        private static void Check(CheckContext context, TextSpan span, IReadOnlyDictionary<string, string> replacements)
        {
            var text = context.Text.ToString(span);
            var changes = new List<TextChange>();
            var found = new StringBuilder();
            foreach (var pair in replacements)
            {
                var index = text.IndexOf(pair.Key, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                found.Append(found.Length == 0 ? "" : ", ").Append($"'{pair.Key}' -> '{pair.Value}'");
                while (index >= 0)
                {
                    changes.Add(new TextChange(new TextSpan(span.Start + index, pair.Key.Length), pair.Value));
                    index = text.IndexOf(pair.Key, index + pair.Key.Length, StringComparison.Ordinal);
                }
            }

            if (changes.Count == 0)
                return;

            changes.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
            context.Report(changes[0].Span, $"типографские знаки: {found}", context.ReplaceInLines(changes));
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }

        private static bool IsStringText(SyntaxToken token)
        {
            return token.IsKind(SyntaxKind.StringLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken);
        }

        private static bool IsCheckedString(CheckContext context, SyntaxToken token)
        {
            var attributes = context.Settings.GetStrings("attributes");
            bool logs = context.Settings.GetBool("logs");
            bool toString = context.Settings.GetBool("toString");
            foreach (var ancestor in token.Parent!.Ancestors())
            {
                if (ancestor is AttributeSyntax attribute)
                    return attributes.Contains(Invocations.GetSimpleName(attribute.Name));

                bool isLog = ancestor is InvocationExpressionSyntax invocation
                    && Invocations.IsLoggerCall(context, invocation);
                if (logs && isLog)
                    return true;

                if (toString && ancestor is MethodDeclarationSyntax { Identifier.Text: "ToString" })
                    return true;

                if (ancestor is MemberDeclarationSyntax)
                    return false;
            }

            return false;
        }
    }
}

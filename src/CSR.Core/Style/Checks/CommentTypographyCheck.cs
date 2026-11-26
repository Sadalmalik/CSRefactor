using System.Text;
using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Типографские знаки в комментариях, в текстах логов, атрибутов-подсказок и ToString
    public class CommentTypographyCheck : SyntaxCheck
    {
        private static readonly Dictionary<string, string> DefaultReplacements = new()
        {
            ["—"] = "-",
            ["–"] = "-",
            ["«"] = "\"",
            ["»"] = "\"",
            ["→"] = "->",
            ["×"] = "*",
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
                if (SyntaxParts.IsComment(trivia))
                    Check(context, trivia.Span, replacements, null);
            }

            foreach (var token in context.Root.DescendantTokens())
            {
                if (IsStringText(token) && IsCheckedString(context, token))
                    Check(context, token.Span, replacements, token);
            }
        }

        // literal: строковый литерал - кавычки замены экранируются по его виду; null - комментарий
        private static void Check(
            CheckContext context,
            TextSpan span,
            IReadOnlyDictionary<string, string> replacements,
            SyntaxToken? literal)
        {
            var text = context.Text.ToString(span);
            var changes = new List<TextChange>();
            var found = new StringBuilder();
            bool replaceable = true;
            foreach (var pair in replacements)
            {
                var index = text.IndexOf(pair.Key, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                found.Append(found.Length == 0 ? "" : ", ").Append($"'{pair.Key}' -> '{pair.Value}'");
                while (index >= 0)
                {
                    var value = literal == null ? pair.Value : EscapeQuotes(literal.Value, pair.Value);
                    replaceable &= value != null;
                    changes.Add(new TextChange(new TextSpan(span.Start + index, pair.Key.Length), value ?? ""));
                    index = text.IndexOf(pair.Key, index + pair.Key.Length, StringComparison.Ordinal);
                }
            }

            if (changes.Count == 0)
                return;

            // находка - на все строки с заменами: replacement заменяет строки line..endLine целиком
            changes.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
            var replacement = replaceable ? context.ReplaceInLines(changes) : null;
            var reported = TextSpan.FromBounds(changes[0].Span.Start, changes.Max(change => change.Span.End));
            context.Report(reported, $"типографские знаки: {found}", replacement);
        }

        // Кавычка замены внутри литерала: \" в обычной строке, "" в verbatim (@); в raw-строке кавычка рядом
        // с разделителем ломает литерал - замены нет (null), правит агент
        private static string? EscapeQuotes(SyntaxToken literal, string value)
        {
            if (!value.Contains("\""))
                return value;

            if (IsRawString(literal))
                return null;

            return value.Replace("\"", IsVerbatimString(literal) ? "\"\"" : "\\\"");
        }

        private static bool IsRawString(SyntaxToken literal)
        {
            if (literal.IsKind(SyntaxKind.InterpolatedStringTextToken))
                return GetInterpolatedStart(literal).Contains("\"\"\"");

            return literal.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                || literal.IsKind(SyntaxKind.MultiLineRawStringLiteralToken);
        }

        // @"...", $@"...", @$"..."
        private static bool IsVerbatimString(SyntaxToken literal)
        {
            if (literal.IsKind(SyntaxKind.InterpolatedStringTextToken))
                return GetInterpolatedStart(literal).Contains("@");

            return literal.Text.StartsWith("@", StringComparison.Ordinal);
        }

        // Начало интерполированной строки: $", $@", $"""
        private static string GetInterpolatedStart(SyntaxToken literal)
        {
            var expression = literal.Parent?.Parent as InterpolatedStringExpressionSyntax;
            return expression?.StringStartToken.Text ?? "";
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
                    return attributes.Contains(TypeNames.GetAttributeName(attribute.Name));

                bool isLog = ancestor is InvocationExpressionSyntax invocation
                    && LoggerCallTool.IsLoggerCall(context, invocation);
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

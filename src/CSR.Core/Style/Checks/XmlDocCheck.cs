using System.Text;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Документационный комментарий /// или /** */. Замена по explanation: comment - текст <summary> одной строкой //
    // (пояснение не теряется; пересказ имени агент удаляет сам), если других текстов (param, returns, remarks) нет
    // и строка помещается в max, иначе - без замены; блок без текста удаляется; delete - удаление блока;
    // none - без замены
    public class XmlDocCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice(
                "explanation",
                "comment - summary в строку //, delete - удалить блок, none - без замены",
                "comment",
                "delete",
                "none"),
            CheckParameter.Int("max", 120, "длина строки // для explanation: comment"),
        ];

        public override string Name
            => "xml-doc";

        public override string Description
            => "XML-документация";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var explanation = context.Settings.GetString("explanation");
            var max = context.Settings.GetInt("max");
            foreach (var trivia in context.Root.DescendantTrivia())
            {
                if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                    continue;

                var span = TrimLineBreaks(context.Text, trivia.FullSpan);
                var lines = context.GetLinesSpan(span);
                bool isAlone = context.Text.ToString(TextSpan.FromBounds(lines.Start, span.Start)).Trim().Length == 0
                    && context.Text.ToString(TextSpan.FromBounds(span.End, lines.End)).Trim().Length == 0;
                var replacement = isAlone ? GetReplacement(context, trivia, span, explanation, max) : null;
                context.Report(span, "XML-документация", replacement);
            }
        }

        private static IReadOnlyList<string>? GetReplacement(
            CheckContext context,
            SyntaxTrivia trivia,
            TextSpan span,
            string explanation,
            int max)
        {
            if (explanation == "none")
                return null;

            if (explanation == "delete" || trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation)
                return Array.Empty<string>();

            var summary = "";
            bool other = false;
            foreach (var element in documentation.Content.OfType<XmlElementSyntax>())
            {
                var text = GetText(element.Content);
                if (element.StartTag.Name.ToString() == "summary")
                    summary = text;
                else
                    other |= text.Length > 0;
            }

            var loose = GetText(documentation.Content.Where(node => node is XmlTextSyntax));
            if (other || loose.Length > 0 && summary.Length > 0)
                return null;

            summary = summary.Length > 0 ? summary : loose;
            if (summary.Length == 0)
                return Array.Empty<string>();

            var indent = TextLines.GetIndent(context.Text, span.Start);
            var line = $"{indent}// {summary.TrimEnd('.')}";
            return line.Length <= max ? [line] : null;
        }

        // Текст узлов документации одной строкой: <see cref="X"/> и <paramref name="x"/> - имя
        private static string GetText(IEnumerable<XmlNodeSyntax> nodes)
        {
            var text = new StringBuilder();
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case XmlTextSyntax xmlText:
                        foreach (var token in xmlText.TextTokens)
                            text.Append(token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken) ? " " : token.Text);
                        break;
                    case XmlEmptyElementSyntax empty:
                        text.Append(GetReference(empty));
                        break;
                    case XmlElementSyntax element:
                        text.Append(GetText(element.Content));
                        break;
                }
            }

            return string.Join(" ", text.ToString().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string GetReference(XmlEmptyElementSyntax element)
        {
            foreach (var attribute in element.Attributes)
            {
                switch (attribute)
                {
                    case XmlCrefAttributeSyntax cref:
                        return cref.Cref.ToString();
                    case XmlNameAttributeSyntax name:
                        return name.Identifier.ToString();
                }
            }

            return "";
        }

        private static TextSpan TrimLineBreaks(SourceText text, TextSpan span)
        {
            var end = span.End;
            while (end > span.Start && (text[end - 1] == '\n' || text[end - 1] == '\r'))
                end--;

            return TextSpan.FromBounds(span.Start, end);
        }
    }
}

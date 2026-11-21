using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Сигнатура столбиком: перенос после '(', параметр на строку с отступом +4 к объявлению, ')' на строке
    // последнего параметра; ': base(...)' / ': this(...)' - отдельной строкой +4. Находка: однострочная сигнатура
    // длиннее max или с minParameters+ параметрами; перенос посреди списка (первый параметр в строке имени);
    // ')' на отдельной строке
    public class SignatureWrapCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("max", 120, "длина строки однострочной сигнатуры (до ')')"),
            CheckParameter.Int("minParameters", 5, "с этого числа параметров - столбиком; 0 - только по длине"),
            CheckParameter.Bool(
                "initializerOnOwnLine",
                true,
                "в сигнатуре столбиком ': base(...)' - отдельной строкой"),
        ];

        public override string Name
            => "signature-wrap";

        public override string Description
            => "сигнатура столбиком";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var max = context.Settings.GetInt("max");
            var minParameters = context.Settings.GetInt("minParameters");
            bool initializerOnOwnLine = context.Settings.GetBool("initializerOnOwnLine");
            foreach (var list in context.Root.DescendantNodes().OfType<ParameterListSyntax>())
            {
                bool isSignature = list.Parent
                    is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or DelegateDeclarationSyntax;
                if (!isSignature || list.Parameters.Count == 0 || TextLines.HasCommentOrDirective(list))
                    continue;

                var problem = GetProblem(context.Text, list, max, minParameters);
                if (problem == null)
                    continue;

                var initializer = initializerOnOwnLine ? GetInitializer(context.Text, list) : null;
                Report(context, list, initializer, problem);
            }
        }

        private static string? GetProblem(SourceText text, ParameterListSyntax list, int max, int minParameters)
        {
            var parameters = list.Parameters;
            if (parameters.Any(parameter => !TextLines.IsSingleLine(text, parameter.Span)))
                return null;

            var openLine = TextLines.GetLineNumber(text, list.OpenParenToken.SpanStart);
            var closeLine = TextLines.GetLineNumber(text, list.CloseParenToken.SpanStart);
            if (openLine == closeLine)
            {
                if (TextLines.GetColumn(text, list.CloseParenToken.Span.End) > max)
                    return "сигнатура длиннее порога - столбиком";

                return minParameters > 0 && parameters.Count >= minParameters
                    ? $"параметров {parameters.Count} - столбиком"
                    : null;
            }

            if (TextLines.GetLineNumber(text, parameters[0].SpanStart) == openLine)
                return "перенос посреди списка параметров - после '(', параметр на строку";

            var lastLine = TextLines.GetLineNumber(text, parameters.Last().SpanStart);
            if (closeLine != lastLine)
                return "')' на отдельной строке";

            // меньше minParameters параметров в несколько строк - любая форма
            var lines = parameters.Select(parameter => TextLines.GetLineNumber(text, parameter.SpanStart));
            bool crowded = lines.Distinct().Count() != parameters.Count;
            bool many = minParameters > 0 && parameters.Count >= minParameters;
            return crowded && many ? "два параметра в одной строке" : null;
        }

        // ': base(...)' в строке ')' - переносится; уже на своей строке - не трогается
        private static ConstructorInitializerSyntax? GetInitializer(SourceText text, ParameterListSyntax list)
        {
            if (list.Parent is not ConstructorDeclarationSyntax { Initializer: { } initializer })
                return null;

            var closeLine = TextLines.GetLineNumber(text, list.CloseParenToken.SpanStart);
            return TextLines.GetLineNumber(text, initializer.SpanStart) == closeLine ? initializer : null;
        }

        private static void Report(
            CheckContext context,
            ParameterListSyntax list,
            ConstructorInitializerSyntax? initializer,
            string problem)
        {
            var text = context.Text;
            var member = list.Parent!;
            var indent = TextLines.GetIndent(text, GetDeclarationStart(member));
            var inner = TextLines.Deeper(indent);
            var openLine = text.Lines.GetLineFromPosition(list.OpenParenToken.SpanStart);
            var closeLine = text.Lines.GetLineFromPosition(list.CloseParenToken.SpanStart);
            var lines = new List<string>
            {
                text.ToString(TextSpan.FromBounds(openLine.Start, list.OpenParenToken.Span.End)),
            };

            var parameters = list.Parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                var separator = i < parameters.Count - 1 ? "," : ")";
                lines.Add(inner + parameters[i].ToString().Trim() + separator);
            }

            var rest = text.ToString(TextSpan.FromBounds(list.CloseParenToken.Span.End, closeLine.End));
            if (initializer != null)
            {
                var start = initializer.SpanStart - list.CloseParenToken.Span.End;
                lines[lines.Count - 1] += rest.Substring(0, start).TrimEnd();
                lines.Add(inner + rest.Substring(start));
            }
            else
            {
                lines[lines.Count - 1] += rest;
            }

            context.Report(TextSpan.FromBounds(openLine.Start, closeLine.End), problem, lines);
        }

        private static int GetDeclarationStart(SyntaxNode member)
        {
            var first = member.DescendantTokens()
                .First(token => !token.Parent!.AncestorsAndSelf().OfType<AttributeListSyntax>().Any());
            return first.SpanStart;
        }
    }
}

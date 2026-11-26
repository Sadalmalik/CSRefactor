using CSR.Core.Checks;
using CSR.Core.Syntax;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Сигнатура столбиком: перенос после '(', параметр на строку с отступом +4 к объявлению, ')' на строке
    // последнего параметра; ': base(...)' / ': this(...)' и каждое ограничение 'where' - отдельной строкой +4.
    // Находка: однострочная сигнатура длиннее max или с minParameters+ параметрами; перенос посреди списка (первый
    // параметр в строке имени); ')' на отдельной строке; ': base(...)' или 'where' в строке ')' у сигнатуры столбиком
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

                var tails = GetTails(context.Text, list, initializerOnOwnLine);
                var problem = GetProblem(context.Text, list, max, minParameters);
                if (problem == null && tails.Count > 0 && IsColumn(context.Text, list))
                    problem = "': base' или 'where' в строке ')' - отдельной строкой";

                if (problem == null)
                    continue;

                Report(context, list, tails, problem);
            }
        }

        private static string? GetProblem(SourceText text, ParameterListSyntax list, int max, int minParameters)
        {
            var parameters = list.Parameters;
            if (parameters.Any(parameter => !TextLayout.IsSingleLine(text, parameter.Span)))
                return null;

            var openLine = TextLayout.GetLineNumber(text, list.OpenParenToken.SpanStart);
            var closeLine = TextLayout.GetLineNumber(text, list.CloseParenToken.SpanStart);
            if (openLine == closeLine)
            {
                if (TextLayout.GetColumn(text, list.CloseParenToken.Span.End) > max)
                    return "сигнатура длиннее порога - столбиком";

                return minParameters > 0 && parameters.Count >= minParameters
                    ? $"параметров {parameters.Count} - столбиком"
                    : null;
            }

            if (TextLayout.GetLineNumber(text, parameters[0].SpanStart) == openLine)
                return "перенос посреди списка параметров - после '(', параметр на строку";

            var lastLine = TextLayout.GetLineNumber(text, parameters.Last().SpanStart);
            if (closeLine != lastLine)
                return "')' на отдельной строке";

            // меньше minParameters параметров в несколько строк - любая форма
            var lines = parameters.Select(parameter => TextLayout.GetLineNumber(text, parameter.SpanStart));
            bool crowded = lines.Distinct().Count() != parameters.Count;
            bool many = minParameters > 0 && parameters.Count >= minParameters;
            return crowded && many ? "два параметра в одной строке" : null;
        }

        // Начала ': base(...)' и ограничений 'where' в строке ')' - переносятся; уже на своей строке - не трогаются
        private static List<int> GetTails(SourceText text, ParameterListSyntax list, bool initializerOnOwnLine)
        {
            var nodes = new List<SyntaxNode>();
            if (initializerOnOwnLine && list.Parent is ConstructorDeclarationSyntax { Initializer: { } initializer })
                nodes.Add(initializer);

            nodes.AddRange(list.Parent switch
            {
                MethodDeclarationSyntax method => method.ConstraintClauses,
                LocalFunctionStatementSyntax function => function.ConstraintClauses,
                DelegateDeclarationSyntax declaration => declaration.ConstraintClauses,
                _ => Enumerable.Empty<SyntaxNode>(),
            });

            var closeLine = TextLayout.GetLineNumber(text, list.CloseParenToken.SpanStart);
            return nodes
                .Where(node => TextLayout.GetLineNumber(text, node.SpanStart) == closeLine)
                .Select(node => node.SpanStart)
                .ToList();
        }

        // Параметры уже столбиком: первый - на строке после '(', каждый - в одну строку
        private static bool IsColumn(SourceText text, ParameterListSyntax list)
        {
            var openLine = TextLayout.GetLineNumber(text, list.OpenParenToken.SpanStart);
            return TextLayout.GetLineNumber(text, list.Parameters[0].SpanStart) > openLine
                && list.Parameters.All(parameter => TextLayout.IsSingleLine(text, parameter.Span));
        }

        private static void Report(
            CheckContext context,
            ParameterListSyntax list,
            List<int> tails,
            string problem)
        {
            var text = context.Text;
            var member = list.Parent!;
            var indent = TextLayout.GetIndent(text, SyntaxParts.GetDeclarationStart(member));
            var inner = TextLayout.Deeper(indent);
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

            // хвост строки ')' до первого переносимого куска остается на ней; каждый кусок - своей строкой
            var ends = tails.Skip(1).Append(closeLine.End).ToList();
            var first = tails.Count > 0 ? tails[0] : closeLine.End;
            var rest = text.ToString(TextSpan.FromBounds(list.CloseParenToken.Span.End, first));
            lines[lines.Count - 1] += tails.Count > 0 ? rest.TrimEnd() : rest;
            for (var i = 0; i < tails.Count; i++)
            {
                var part = text.ToString(TextSpan.FromBounds(tails[i], ends[i]));
                lines.Add(inner + (i < tails.Count - 1 ? part.TrimEnd() : part));
            }

            context.Report(
                TextSpan.FromBounds(openLine.Start, closeLine.End),
                problem,
                context.ReplaceLines(openLine.LineNumber, closeLine.LineNumber, lines));
        }
    }
}

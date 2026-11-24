using CSR.Core.Checks;
using CSR.Core.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Пустые строки вокруг #region / #endregion: внутри региона - inside (после #region и перед #endregion),
    // между #endregion и следующим #region - between, между регионом и членом вне региона - outside;
    // у границы типа ('{' перед #region, '}' после #endregion) - вплотную. Соседняя директива (#if, вложенный
    // регион) - не проверяется
    public class RegionLayoutCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("inside", 1, "пустых строк после #region и перед #endregion"),
            CheckParameter.Int("between", 2, "пустых строк между #endregion и следующим #region"),
            CheckParameter.Int("outside", 1, "пустых строк между регионом и членом вне региона"),
        ];

        public override string Name
            => "region-layout";

        public override string Description
            => "пустые строки вокруг регионов";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var text = context.Text;
            var settings = context.Settings;
            foreach (var directive in context.Root.DescendantTrivia())
            {
                var isRegion = directive.IsKind(SyntaxKind.RegionDirectiveTrivia);
                if (!isRegion && !directive.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
                    continue;

                var line = TextLines.GetLineNumber(text, directive.SpanStart);
                var previous = FindNonBlank(text, line, -1);
                var next = FindNonBlank(text, line, 1);
                if (isRegion)
                {
                    CheckGap(context, previous, line, GetBeforeRegion(text, previous, settings), "перед #region");
                    CheckGap(
                        context,
                        line,
                        next,
                        IsDirective(text, next) ? null : settings.GetInt("inside"),
                        "после #region");
                }
                else
                {
                    CheckGap(
                        context,
                        previous,
                        line,
                        IsDirective(text, previous) ? null : settings.GetInt("inside"),
                        "перед #endregion");
                    CheckGap(context, line, next, GetAfterEndRegion(text, next, settings), "после #endregion");
                }
            }
        }

        // Перед #region: '{' - вплотную; директива (в том числе #endregion - его проверяет он сам) - нет проверки
        private static int? GetBeforeRegion(SourceText text, int previous, CheckSettings settings)
        {
            if (previous < 0 || IsDirective(text, previous))
                return null;

            return TextLines.GetLine(text, previous).Trim() == "{" ? 0 : settings.GetInt("outside");
        }

        // После #endregion: '}' - вплотную, #region - between, другая директива - нет проверки
        private static int? GetAfterEndRegion(SourceText text, int next, CheckSettings settings)
        {
            if (next < 0)
                return null;

            var value = TextLines.GetLine(text, next).Trim();
            if (value.StartsWith("}", StringComparison.Ordinal))
                return 0;

            if (value.StartsWith("#region", StringComparison.Ordinal))
                return settings.GetInt("between");

            return IsDirective(text, next) ? null : settings.GetInt("outside");
        }

        // Между строками first и last (first < last) - want пустых строк
        private static void CheckGap(
            CheckContext context,
            int first,
            int last,
            int? want,
            string place)
        {
            if (want == null || first < 0 || last < 0)
                return;

            var gap = last - first - 1;
            if (gap == want)
                return;

            var text = context.Text;
            var lines = new List<string> { TextLines.GetLine(text, first) };
            lines.AddRange(Enumerable.Repeat("", want.Value));
            lines.Add(TextLines.GetLine(text, last));
            var span = TextSpan.FromBounds(text.Lines[first].Start, text.Lines[last].End);
            context.Report(span, $"{place}: пустых строк {gap}, нужно {want}", context.ReplaceLines(first, last, lines));
        }

        private static int FindNonBlank(SourceText text, int line, int direction)
        {
            for (var index = line + direction; index >= 0 && index < text.Lines.Count; index += direction)
            {
                if (!TextLayout.IsBlank(text, index))
                    return index;
            }

            return -1;
        }

        private static bool IsDirective(SourceText text, int line)
            => line >= 0 && TextLines.GetLine(text, line).TrimStart().StartsWith("#", StringComparison.Ordinal);
    }
}

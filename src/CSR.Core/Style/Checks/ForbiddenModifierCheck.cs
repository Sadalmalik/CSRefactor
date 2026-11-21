using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Запрещенные модификаторы и их сочетания у типов и членов: sealed, private protected, protected internal
    public class ForbiddenModifierCheck : StyleCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("modifiers", "модификатор или сочетание через пробел (в любом порядке)",
                "sealed", "private protected", "protected internal"),
        ];

        public override string Name
            => "forbidden-modifier";

        public override string Description
            => "запрещенный модификатор";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var forbidden = context.Settings.GetStrings("modifiers")
                .Select(entry => entry.Split(' ').Where(part => part.Length > 0).ToArray())
                .ToList();

            foreach (var member in context.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var modifiers = member.Modifiers;
                if (modifiers.Count == 0)
                    continue;

                var present = modifiers.Select(token => token.Text).ToList();
                foreach (var combination in forbidden)
                {
                    if (!combination.All(present.Contains))
                        continue;

                    var first = modifiers.First(token => combination.Contains(token.Text));
                    var last = modifiers.Last(token => combination.Contains(token.Text));
                    var span = TextSpan.FromBounds(first.SpanStart, last.Span.End);
                    context.Report(span, $"модификатор '{string.Join(" ", combination)}'");
                }
            }
        }
    }
}
